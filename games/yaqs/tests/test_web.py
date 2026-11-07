import json
import re
from urllib.parse import unquote

import anyio
import pytest
from helpers import ATTACK, CharacterEncoder

from yaqs.mcp import build_server
from yaqs.service import KnowledgeService
from yaqs.web import STATIC, build_app, mount


def get(app, path, host="127.0.0.1:9035"):
    """One GET through the ASGI app; returns status, headers and body."""
    raw, _, query = path.partition("?")
    scope = {
        "type": "http",
        "asgi": {"version": "3.0"},
        "http_version": "1.1",
        "method": "GET",
        "scheme": "http",
        "path": unquote(raw),
        "raw_path": raw.encode(),
        "query_string": query.encode(),
        "root_path": "",
        "headers": [(b"host", host.encode())],
        "client": ("127.0.0.1", 50000),
        "server": ("127.0.0.1", 9035),
    }
    messages = []
    requested = False

    async def run():
        done = anyio.Event()

        async def receive():
            nonlocal requested
            if not requested:
                requested = True
                return {"type": "http.request", "body": b"", "more_body": False}
            await done.wait()  # the client disconnects once the response is complete
            return {"type": "http.disconnect"}

        async def send(message):
            messages.append(message)
            if message["type"] == "http.response.body" and not message.get("more_body"):
                done.set()

        await app(scope, receive, send)

    anyio.run(run)
    headers = {key.decode(): value.decode() for key, value in messages[0]["headers"]}
    body = b"".join(message.get("body", b"") for message in messages[1:])
    return messages[0]["status"], headers, body


@pytest.fixture
def app(library, encoder):
    service = KnowledgeService(library.path)
    service.encoder = encoder
    return build_app(service)


def test_page_and_assets_are_served_with_a_strict_policy(app):
    status, headers, body = get(app, "/")
    assert status == 200 and b'<script src="app.js"' in body
    assert "script-src 'self'" in headers["content-security-policy"]
    assert "unsafe-inline" not in headers["content-security-policy"]
    assert get(app, "/app.js")[1]["content-type"].startswith("text/javascript")
    assert get(app, "/app.css")[0] == 200


def test_script_never_parses_book_text_as_html(app):
    script = get(app, "/app.js")[2].decode()
    assert "innerHTML" not in script and "insertAdjacentHTML" not in script


def test_search_page_passage_and_books_return_compact_views(app):
    status, _, body = get(app, "/api/search?q=attack&mode=lexical&limit=5")
    hits = json.loads(body)["hits"]
    assert status == 200 and hits[0]["text"] == ATTACK
    assert {"book_id", "page_number", "chunk_ids", "score"} <= set(hits[0])
    assert "sha256" not in hits[0]
    page = json.loads(get(app, f"/api/page/{hits[0]['book_id']}/1")[2])
    assert page["text"] == ATTACK and page["complete"]
    passage = json.loads(get(app, f"/api/passage/{hits[0]['id']}?neighbors=1")[2])
    assert [hit["page_number"] for hit in passage["hits"]] == [1, 2]
    books = json.loads(get(app, "/api/books")[2])
    assert books == [
        {"id": 1, "title": "Game reference", "author": "Author", "page_count": 2, "chunks": 2}
    ]
    assert json.loads(get(app, "/api/stats")[2])["counts"]["books"] == 1


def test_bad_requests_are_json_errors(app):
    status, _, body = get(app, "/api/search?q=attack&mode=fuzzy")
    assert status == 400 and "mode" in json.loads(body)["error"]
    assert get(app, "/api/search?q=attack&limit=many")[0] == 400
    assert get(app, "/api/search?q=")[0] == 400
    status, _, body = get(app, "/api/page/1/99")
    assert status == 404 and "Unknown" in json.loads(body)["error"]
    assert get(app, "/api/passage/999")[0] == 404


def test_runtime_failures_are_json_errors_with_the_reason(library):
    class MissingModel(CharacterEncoder):
        def encode(self, texts, **kwargs):
            raise RuntimeError("Qwen checkpoint is missing model.safetensors")

    service = KnowledgeService(library.path)
    service.encoder = MissingModel()
    status, headers, body = get(build_app(service), "/api/search?q=attack&mode=semantic")
    assert status == 500 and "model.safetensors" in json.loads(body)["error"]
    assert "script-src 'self'" in headers["content-security-policy"]


def test_foreign_host_names_are_refused(app):
    assert get(app, "/api/books", host="evil.example:9035")[0] == 403
    assert get(app, "/", host="evil.example")[0] == 403
    assert get(app, "/api/books", host="localhost:9035")[0] == 200


def test_serve_web_mounts_the_ui_next_to_mcp(library, encoder):
    service = KnowledgeService(library.path)
    service.encoder = encoder
    server = build_server(service)
    mount(server, service)
    app = server.streamable_http_app()
    paths = {route.path for route in app.routes}
    assert {"/mcp", "/", "/app.js", "/api/search", "/api/page/{book_id:int}/{page:int}"} <= paths
    assert json.loads(get(app, "/api/books")[2])[0]["title"] == "Game reference"


def test_every_interface_string_has_english_and_russian_text():
    script = (STATIC / "app.js").read_text()
    english, russian = script.split("\n  ru: {\n")
    english = english.split("\n  en: {\n")[1]
    russian = russian.split("\n};\n")[0]

    def keys(table):
        return set(re.findall(r"^    (\w+):", table, re.MULTILINE))

    used = set(re.findall(r'\bt\("(\w+)"', script))
    used |= set(re.findall(r'data-i18n(?:-[\w-]+)?="(\w+)"', (STATIC / "index.html").read_text()))
    assert keys(english) == keys(russian)
    assert used <= keys(english)

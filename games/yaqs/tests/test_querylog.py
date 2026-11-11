import json
import sys

import anyio
import pytest
from helpers import ATTACK, CharacterEncoder
from mcp import Client
from mcp_types import Implementation
from test_web import get

from yaqs import cli
from yaqs.mcp import build_server
from yaqs.querylog import Caller, default_path
from yaqs.service import KnowledgeService
from yaqs.web import build_app


def records(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


@pytest.fixture
def logged(library, encoder, tmp_path):
    service = KnowledgeService(library.path, query_log=tmp_path / "queries.jsonl")
    service.encoder = encoder
    return service


def test_records_keep_arguments_and_answer_ids_but_no_text(logged):
    hit = logged.search("attack", mode="lexical", detail="compact")["hits"][0]
    logged.page(hit["book_id"], 1, caller=Caller("cli"))
    (search, page) = records(logged.log.path)
    assert search["interface"] == "python" and search["operation"] == "search"
    assert search["arguments"] == {
        "query": "attack",
        "limit": 5,
        "book_id": None,
        "mode": "lexical",
        "max_tokens": 4096,
        "detail": "compact",
    }
    first = search["result"]["hits"][0]
    assert first["id"] == hit["id"] and first["page_number"] == 1 and "score" in first
    assert search["result"]["used_tokens"] == len(ATTACK)
    assert search["elapsed_ms"] >= 0 and search["time"].endswith("Z")
    assert ATTACK not in logged.log.path.read_text()
    assert page["interface"] == "cli" and page["result"]["complete"] is True


def test_failed_requests_are_logged_with_the_reason(logged):
    with pytest.raises(KeyError):
        logged.passage(999)
    with pytest.raises(ValueError):
        logged.search("attack", mode="fuzzy")
    missing, fuzzy = records(logged.log.path)
    assert "999" in missing["error"] and not missing["error"].startswith("'")
    assert "mode" in fuzzy["error"] and "result" not in fuzzy


def test_python_api_logs_nothing_without_a_path(library, encoder, tmp_path):
    service = KnowledgeService(library.path)
    service.encoder = encoder
    service.search("attack", mode="lexical")
    assert service.log is None
    assert not list(tmp_path.glob("*.jsonl"))


def test_web_requests_are_logged_as_web(logged):
    app = build_app(logged)
    assert get(app, "/api/search?q=attack&mode=lexical")[0] == 200
    assert get(app, "/api/stats")[0] == 200
    assert get(app, "/api/passage/999")[0] == 404
    assert [(r["interface"], r["operation"]) for r in records(logged.log.path)] == [
        ("web", "search"),
        ("web", "stats"),
        ("web", "passage"),
    ]


@pytest.mark.parametrize("mode", ["legacy", "auto"])
def test_mcp_calls_name_the_client(logged, mode):
    async def run():
        info = Implementation(name="test-agent", version="1.2")
        async with Client(build_server(logged), client_info=info, mode=mode) as client:
            await client.call_tool("search_knowledge", {"query": "attack", "mode": "lexical"})
            await client.call_tool("list_books", {})

    anyio.run(run)
    search, books = records(logged.log.path)
    assert search["interface"] == "mcp" and search["client"] == "test-agent 1.2"
    assert search["result"]["hits"] and books["result"] == {"books": 1}


def test_unwritable_log_warns_once_and_answers(library, encoder, tmp_path, capsys):
    blocker = tmp_path / "file"
    blocker.write_text("")
    service = KnowledgeService(library.path, query_log=blocker / "queries.jsonl")
    service.encoder = encoder
    assert service.search("attack", mode="lexical")["hits"]
    assert service.books()
    assert capsys.readouterr().err.count("Query log") == 1


def test_log_path_defaults_beside_the_database_and_can_be_turned_off(monkeypatch, tmp_path):
    database = tmp_path / "knowledge.sqlite3"
    monkeypatch.delenv("YAQS_QUERY_LOG", raising=False)
    assert default_path(database) == tmp_path / "knowledge.queries.jsonl"
    monkeypatch.setenv("YAQS_QUERY_LOG", str(tmp_path / "elsewhere.jsonl"))
    assert default_path(database) == tmp_path / "elsewhere.jsonl"
    monkeypatch.setenv("YAQS_QUERY_LOG", "off")
    assert default_path(database) is None


def test_cli_search_is_logged_beside_the_database(library, monkeypatch, capsys):
    monkeypatch.delenv("YAQS_QUERY_LOG", raising=False)
    monkeypatch.setattr("yaqs.service.QwenEncoder", lambda device: CharacterEncoder())
    arguments = ["yaqs", "search", "attack", "--mode", "lexical", "--db", str(library.path)]
    monkeypatch.setattr(sys, "argv", arguments)
    cli.main()
    assert json.loads(capsys.readouterr().out)["hits"]
    (record,) = records(library.path.with_suffix(".queries.jsonl"))
    assert record["interface"] == "cli" and record["arguments"]["query"] == "attack"

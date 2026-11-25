import io

import pytest
from PIL import Image
from starlette.testclient import TestClient

from shotsearch.web import build_app


@pytest.fixture
def client(service):
    return TestClient(build_app(service), base_url="http://127.0.0.1")


def test_page_and_search(client):
    assert "Поиск скриншотов" in client.get("/").text
    answer = client.get("/api/search?shot=10_00&ranking=look&limit=2&pool=2").json()
    assert [r["game"]["appid"] for r in answer["results"]][0] == 12
    filtered = client.get("/api/search?shot=12_00&tags=Horror,Adventure&limit=5&pool=5").json()
    assert {r["game"]["appid"] for r in filtered["results"]} == {10}


def test_errors(client):
    assert client.get("/api/search").status_code == 400
    assert client.get("/api/search?shot=10_00&limit=x").status_code == 400
    assert client.get("/api/search?shot=77_00").status_code == 404
    assert client.get("/shot/77_00").status_code == 404
    assert client.get("/shot/..%2F..%2Fetc%2Fpasswd").status_code == 404
    assert client.get("/upload/zzz").status_code == 400
    assert client.post("/api/upload", content=b"nope").status_code == 400
    assert client.post("/api/fetch?url=file:///etc/passwd").status_code == 400


def test_files_and_upload(client):
    shot = client.get("/shot/10_01")
    assert shot.status_code == 200 and shot.headers["content-type"] == "image/jpeg"
    data = io.BytesIO()
    Image.new("RGB", (40, 30), (0, 200, 0)).save(data, "PNG")
    upload = client.post("/api/upload", content=data.getvalue()).json()
    assert client.get(f"/upload/{upload['id']}").status_code == 200


def test_foreign_host_is_refused(service):
    evil = TestClient(build_app(service), base_url="http://evil.example")
    assert evil.get("/api/info").status_code == 403
    assert evil.get("/").status_code == 403

import json
import sys

import pytest

from yaqs import cli, default_database
from yaqs.service import KnowledgeService


@pytest.mark.parametrize(
    "arguments",
    [
        ["--db", "custom.sqlite3", "search", "question"],
        ["search", "question", "--db", "custom.sqlite3"],
    ],
)
def test_database_option_works_before_or_after_command(monkeypatch, capsys, arguments):
    monkeypatch.setattr(sys, "argv", ["yaqs", *arguments])
    monkeypatch.setattr(cli, "execute", lambda args: {"db": str(args.db), "mode": args.mode})
    cli.main()
    assert json.loads(capsys.readouterr().out) == {"db": "custom.sqlite3", "mode": "semantic"}


def test_database_environment_is_resolved_at_command_time(monkeypatch, tmp_path, capsys):
    path = tmp_path / "chosen.sqlite3"
    monkeypatch.setenv("YAQS_DB", str(path))
    assert default_database() == path
    monkeypatch.setattr(sys, "argv", ["yaqs", "stats"])
    monkeypatch.setattr(cli, "execute", lambda args: {"db": str(args.db)})
    cli.main()
    assert json.loads(capsys.readouterr().out)["db"] == str(path)


def test_missing_database_returns_json_error_without_traceback(monkeypatch, tmp_path, capsys):
    monkeypatch.setattr(sys, "argv", ["yaqs", "stats", "--db", str(tmp_path / "absent.sqlite3")])
    with pytest.raises(SystemExit) as error:
        cli.main()
    assert error.value.code == 1
    result = capsys.readouterr()
    assert not result.out
    assert "No database" in json.loads(result.err)["error"]


def test_semantic_default_reaches_service_and_has_correct_score_note(library, encoder):
    service = KnowledgeService(library.path)
    service.encoder = encoder
    result = service.search("совсем другой язык")
    assert result["mode"] == "semantic"
    assert result["hits"] and result["hits"][0]["score"] == pytest.approx(1)
    assert "Cosine" in result["score_note"]


def test_doctor_distinguishes_missing_checkpoint(library, monkeypatch):
    from yaqs import diagnostics

    monkeypatch.setattr(
        diagnostics, "cache_status", lambda: {"ready": False, "missing": ["model.safetensors"]}
    )
    result = diagnostics.diagnose(library.path)
    assert result["database"]["ready"]
    assert not result["ready"]
    assert "setup" in result["model"]["hint"]


def test_interrupt_reports_brief_error(monkeypatch, capsys):
    def interrupt(args):
        raise KeyboardInterrupt

    monkeypatch.setattr(sys, "argv", ["yaqs", "stats"])
    monkeypatch.setattr(cli, "execute", interrupt)
    with pytest.raises(SystemExit) as error:
        cli.main()
    assert error.value.code == 130
    assert json.loads(capsys.readouterr().err)["error"] == "Interrupted"

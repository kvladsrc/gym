import threading

import pytest

from yaqs.ingest import import_lock


def test_other_importers_can_wait_without_starting_a_second_indexer(tmp_path):
    path = tmp_path / "knowledge.sqlite3"
    entered = threading.Event()
    started = threading.Event()

    def waiting_importer():
        started.set()
        with import_lock(path, wait=True):
            entered.set()

    with import_lock(path):
        with pytest.raises(RuntimeError, match="Another writer"), import_lock(path):
            pytest.fail("Concurrent import acquired the same lock")
        worker = threading.Thread(target=waiting_importer, daemon=True)
        worker.start()
        assert started.wait(1)
        assert not entered.wait(0.05)
    assert entered.wait(1)
    worker.join(1)
    assert not worker.is_alive()

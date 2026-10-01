"""The on-disk store of offloaded model blocks (model_server_sdk.offload)."""

from pathlib import Path

import pytest

from model_server_sdk import offload


def test_store_is_written_once_and_reused(tmp_path: Path) -> None:
    directory, complete = offload.prepare(tmp_path, "a", needed_bytes=0)
    assert not complete
    (directory / "group_x.safetensors").write_bytes(b"blocks")
    offload.mark_complete(directory)
    again, complete = offload.prepare(tmp_path, "a", needed_bytes=0)
    assert complete
    assert (again / "group_x.safetensors").read_bytes() == b"blocks"


def test_incomplete_store_is_wiped(tmp_path: Path) -> None:
    directory, _ = offload.prepare(tmp_path, "a", needed_bytes=0)
    (directory / "group_x.safetensors").write_bytes(b"trunc")  # interrupted first start
    directory, complete = offload.prepare(tmp_path, "a", needed_bytes=0)
    assert not complete
    assert list(directory.iterdir()) == []


def test_other_fingerprints_are_removed(tmp_path: Path) -> None:
    old, _ = offload.prepare(tmp_path, "old-revision", needed_bytes=0)
    offload.mark_complete(old)
    offload.prepare(tmp_path, "new-revision", needed_bytes=0)
    assert not old.exists()


def test_disk_space_is_checked_before_writing(tmp_path: Path) -> None:
    with pytest.raises(offload.OffloadError, match="GB free"):
        offload.prepare(tmp_path, "a", needed_bytes=10**18)


def test_only_one_server_uses_the_store(tmp_path: Path) -> None:
    with (
        offload.lock(tmp_path),
        pytest.raises(offload.OffloadError, match="another server"),
        offload.lock(tmp_path),
    ):
        pass
    with offload.lock(tmp_path):  # released
        pass


def test_fingerprint_is_a_safe_name() -> None:
    assert offload.fingerprint("741f7c3c", "x/y.gguf", "torch2.5.1+cu124") == (
        "741f7c3c-x_y.gguf-torch2.5.1_cu124"
    )


def test_store_location_follows_xdg(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    monkeypatch.setenv("XDG_CACHE_HOME", str(tmp_path))
    assert offload.base_directory("flux") == tmp_path / "assets-studio" / "flux-offload"

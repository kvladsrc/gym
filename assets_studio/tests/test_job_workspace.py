"""Diagnostics retain the original work on both successful and failed jobs."""

from contextlib import nullcontext
from pathlib import Path

import pytest
from asset_processing.workspace import job_workspace


@pytest.mark.parametrize("failure", [False, True])
@pytest.mark.parametrize("retain", [False, True])
def test_job_workspace_cleanup_and_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, retain: bool, failure: bool
) -> None:
    monkeypatch.delenv("ASSETS_STUDIO_DEBUG_DIR", raising=False)
    if retain:
        monkeypatch.setenv("ASSETS_STUDIO_DEBUG_DIR", str(tmp_path / "diagnostics"))
    with (
        pytest.raises(RuntimeError, match="inference failed") if failure else nullcontext(),
        job_workspace("capture-") as work,
    ):
        (work / "motion.bvh").write_bytes(b"intermediate")
        if failure:
            raise RuntimeError("inference failed")
    assert work.exists() == retain
    if retain:
        assert (work / "motion.bvh").read_bytes() == b"intermediate"
        with job_workspace("capture-") as other:
            assert other != work

"""Studio configuration: data location and model servers (ADR-004)."""

import os
import tomllib
from pathlib import Path

from model_server_sdk.contract import Slug
from pydantic import BaseModel, ConfigDict, Field, HttpUrl, field_validator


def _xdg(variable: str, fallback: str) -> Path:
    return Path(os.environ.get(variable) or Path.home() / fallback)


DEFAULT_CONFIG_PATH = _xdg("XDG_CONFIG_HOME", ".config") / "assets-studio" / "studio.toml"
DEFAULT_DATA_DIR = _xdg("XDG_DATA_HOME", ".local/share") / "assets-studio"


class Server(BaseModel):
    """A model server the user starts manually; the studio sorts its tasks
    into sections by what they produce."""

    model_config = ConfigDict(extra="forbid", frozen=True)

    id: Slug
    url: HttpUrl
    # Shown in the UI; by default the model's name from /v1/info.
    title: str | None = None
    # Seconds to wait for a generation; None waits indefinitely (ADR-002).
    timeout_s: float | None = Field(default=None, gt=0)

    @property
    def base_url(self) -> str:
        return str(self.url).rstrip("/")


class StudioConfig(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    data_dir: Path = DEFAULT_DATA_DIR
    servers: tuple[Server, ...] = ()
    max_import_bytes: int = Field(default=200 * 1024 * 1024, gt=0)
    poll_interval_s: float = Field(default=2.0, gt=0)

    @field_validator("servers")
    @classmethod
    def _unique_ids(cls, servers: tuple[Server, ...]) -> tuple[Server, ...]:
        ids = [server.id for server in servers]
        if len(ids) != len(set(ids)):
            raise ValueError("server ids must be unique")
        return servers

    def server(self, server_id: str) -> Server:
        for server in self.servers:
            if server.id == server_id:
                return server
        raise KeyError(server_id)


def load_config(path: Path = DEFAULT_CONFIG_PATH) -> StudioConfig:
    """Read the TOML config; a missing file gives the defaults with no servers."""
    if not path.exists():
        return StudioConfig()
    with path.open("rb") as file:
        data = tomllib.load(file)
    if "tabs" in data:
        raise ValueError(
            f"{path}: [[tabs]] was replaced by [[servers]] (id, url, optional title); "
            "keep the old ids, jobs refer to them (docs/adr/0004-sections-and-deletion.md)"
        )
    return StudioConfig.model_validate(data)

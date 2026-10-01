"""Asset library: import files and look them up."""

from pathlib import Path

import httpx2 as httpx

from studio.config import StudioConfig
from studio.domain import MIME_KINDS, Asset, AssetKind, Origin, new_id
from studio.media import UnsupportedMedia, detect_mime, for_storage
from studio.storage import repository
from studio.storage.blobs import BlobStore
from studio.storage.repository import Repository, now

_URL_TIMEOUT_S = 30.0


class ImportError_(ValueError):
    """A file cannot be imported: wrong format, too large or unreachable."""


class AssetInUse(ValueError):
    """An unfinished job needs the asset as an input."""


class Library:
    def __init__(self, config: StudioConfig, repository: Repository, blobs: BlobStore) -> None:
        self._config = config
        self._repository = repository
        self._blobs = blobs

    def import_bytes(
        self,
        data: bytes,
        *,
        title: str | None = None,
        origin: Origin = Origin.UPLOAD,
        source_url: str | None = None,
    ) -> Asset:
        if len(data) > self._config.max_import_bytes:
            raise ImportError_(f"file is larger than {self._config.max_import_bytes} bytes")
        try:
            mime = detect_mime(data)
            # Text normalised, video in the canonical MP4 (ADR-003).
            data, mime = for_storage(data, mime)
        except UnsupportedMedia as error:
            raise ImportError_(str(error)) from error
        with self._blobs.lock:  # the file and its asset, together
            asset = Asset(
                id=new_id(),
                kind=MIME_KINDS[mime],
                mime=mime,
                size_bytes=len(data),
                blob_sha256=self._blobs.put(data, mime),
                origin=origin,
                created_at=now(),
                title=title,
                source_url=source_url,
            )
            self._repository.add_asset(asset)
        return asset

    def import_url(self, url: str, *, title: str | None = None) -> Asset:
        if not url.startswith(("http://", "https://")):
            raise ImportError_("only http(s) URLs can be imported")
        limit = self._config.max_import_bytes
        try:
            with httpx.stream(
                "GET", url, follow_redirects=True, timeout=_URL_TIMEOUT_S
            ) as response:
                response.raise_for_status()
                chunks: list[bytes] = []
                size = 0
                for chunk in response.iter_bytes():
                    size += len(chunk)
                    if size > limit:
                        raise ImportError_(f"file is larger than {limit} bytes")
                    chunks.append(chunk)
        except httpx.HTTPError as error:
            raise ImportError_(f"cannot download {url}: {error}") from error
        return self.import_bytes(b"".join(chunks), title=title, origin=Origin.URL, source_url=url)

    def asset(self, asset_id: str) -> Asset | None:
        return self._repository.asset(asset_id)

    def assets(
        self, *, kind: AssetKind | None = None, favorite: bool | None = None, limit: int = 100
    ) -> list[Asset]:
        return self._repository.assets(kind=kind, favorite=favorite, limit=limit)

    def update(
        self, asset_id: str, *, title: str | None = None, favorite: bool | None = None
    ) -> Asset | None:
        """Rename or (un)mark as favourite; an empty title clears it."""
        return self._repository.update_asset(asset_id, title=title, favorite=favorite)

    def delete(self, asset_id: str) -> Asset | None:
        """Remove an asset from the library (ADR-004); None if there is none.

        Its jobs and lineage keep referring to it; its file goes when no other
        asset has the same content. ``AssetInUse`` while an unfinished job
        needs it as an input.
        """
        with self._blobs.lock:
            try:
                result = self._repository.delete_asset(asset_id)
            except repository.AssetInUse as error:
                raise AssetInUse(str(error)) from error
            if result is None:
                return None
            asset, unused = result
            if unused:
                self._blobs.remove(asset.blob_sha256, asset.mime)
        return asset

    def file(self, asset: Asset) -> Path:
        return self._blobs.path(asset.blob_sha256, asset.mime)

    def parents(self, asset_id: str) -> list[tuple[str, str]]:
        return self._repository.parents(asset_id)

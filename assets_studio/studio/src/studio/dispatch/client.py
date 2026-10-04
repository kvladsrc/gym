"""HTTP client for the model-server contract (ADR-001)."""

from dataclasses import dataclass
from typing import Any

import httpx2 as httpx
from model_server_sdk.contract import ErrorResponse, GenerateResponse, Info
from pydantic import ValidationError

_INFO_TIMEOUT_S = 5.0


@dataclass(frozen=True)
class ServerUnavailable:
    """The server did not answer ``/v1/info`` (not started, crashed, wrong URL)."""

    reason: str


class GenerateFailure(Exception):
    """A generation did not produce a result. ``code`` is stored in the job."""

    def __init__(self, code: str, message: str, *, retryable: bool) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.retryable = retryable


class NotSent(Exception):
    """The request never reached the server; it is safe to send it again."""


class ModelClient:
    def __init__(self, base_url: str) -> None:
        self.base_url = base_url
        self._http = httpx.Client(base_url=base_url)

    def close(self) -> None:
        self._http.close()

    def info(self) -> Info | ServerUnavailable:
        try:
            response = self._http.get("/v1/info", timeout=_INFO_TIMEOUT_S)
            response.raise_for_status()
            return Info.model_validate(response.json())
        except httpx.HTTPError as error:
            return ServerUnavailable(f"{type(error).__name__}: {error}")
        except (ValueError, ValidationError) as error:
            return ServerUnavailable(f"not a model server: {error}")

    def generate(self, request: dict[str, Any], *, timeout_s: float | None) -> GenerateResponse:
        """Send one generation. Without ``timeout_s`` wait for the answer indefinitely:
        generations can take minutes."""
        timeout = httpx.Timeout(_INFO_TIMEOUT_S, read=timeout_s)
        try:
            response = self._http.post("/v1/generate", json=request, timeout=timeout)
        except (httpx.ConnectError, httpx.ConnectTimeout, httpx.PoolTimeout) as error:
            raise NotSent(str(error)) from error
        except httpx.TimeoutException as error:
            raise GenerateFailure("timeout", f"no response: {error}", retryable=False) from error
        except httpx.TransportError as error:
            raise GenerateFailure(
                "connection_lost", f"{type(error).__name__}: {error}", retryable=False
            ) from error
        if response.status_code == 200:
            try:
                return GenerateResponse.model_validate(response.json())
            except (ValueError, ValidationError) as error:
                raise GenerateFailure(
                    "invalid_response", f"malformed response: {error}", retryable=False
                ) from error
        try:
            detail = ErrorResponse.model_validate(response.json()).error
        except (ValueError, ValidationError):
            # A 5xx without the contract's error body is the server failing
            # mid-request, typically stopped while generating (the launcher,
            # Ctrl+C): retried like any retryable error, at most a few times.
            server_failed = response.status_code >= 500
            raise GenerateFailure(
                "server_error" if server_failed else "invalid_response",
                f"HTTP {response.status_code}: {response.text[:300]}",
                retryable=server_failed,
            ) from None
        raise GenerateFailure(detail.code, detail.message, retryable=detail.retryable)

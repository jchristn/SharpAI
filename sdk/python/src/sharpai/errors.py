"""Error types for the SharpAI Python SDK."""

from __future__ import annotations

from typing import Any, Optional


class SharpAIError(Exception):
    """Raised when the SharpAI server returns a non-2xx response.

    Attributes:
        status: HTTP status code.
        message: Human-readable message extracted from the response body when available.
        body: The parsed (or raw) response body.
    """

    def __init__(self, status: int, message: str, body: Optional[Any] = None) -> None:
        super().__init__(f"{status}: {message}")
        self.status = status
        self.message = message
        self.body = body

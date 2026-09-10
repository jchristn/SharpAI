"""Synchronous SharpAI client built on the standard library (no third-party dependencies).

The client targets the loopback address by default and covers the Ollama-compatible API, the
OpenAI-compatible API, request history, authentication, and the account/RBAC management surface.
"""

from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Dict, Iterator, List, Optional

from .errors import SharpAIError

__all__ = ["SharpAIClient"]

_JSON = "application/json"


class SharpAIClient:
    """A synchronous client for a SharpAI server.

    Authentication is optional (the server is open by default). Supply a bearer ``token``, an admin
    ``api_key``, or an ``access_key``/``secret_key`` pair; or call :meth:`login` to obtain and store a
    session token.
    """

    def __init__(
        self,
        base_url: str = "http://127.0.0.1:8000",
        *,
        api_key: Optional[str] = None,
        token: Optional[str] = None,
        access_key: Optional[str] = None,
        secret_key: Optional[str] = None,
        timeout: float = 60.0,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key
        self.token = token
        self.access_key = access_key
        self.secret_key = secret_key
        self.timeout = timeout

    # ---- low-level ----

    def _auth_headers(self) -> Dict[str, str]:
        headers: Dict[str, str] = {}
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        if self.api_key:
            headers["x-api-key"] = self.api_key
        if self.access_key and self.secret_key:
            headers["x-access-key"] = self.access_key
            headers["x-secret-key"] = self.secret_key
        return headers

    def _build_url(self, path: str, query: Optional[Dict[str, Any]] = None) -> str:
        url = f"{self.base_url}{path if path.startswith('/') else '/' + path}"
        if query:
            pairs = {k: str(v) for k, v in query.items() if v is not None and v != ""}
            if pairs:
                url = f"{url}?{urllib.parse.urlencode(pairs)}"
        return url

    def _open(self, method: str, path: str, body: Optional[Any], query: Optional[Dict[str, Any]], extra_headers: Optional[Dict[str, str]]):
        headers = self._auth_headers()
        if extra_headers:
            headers.update(extra_headers)
        data: Optional[bytes] = None
        if body is not None:
            headers["Content-Type"] = _JSON
            data = json.dumps(body).encode("utf-8")
        request = urllib.request.Request(self._build_url(path, query), data=data, headers=headers, method=method)
        try:
            return urllib.request.urlopen(request, timeout=self.timeout)
        except urllib.error.HTTPError as error:
            raw = error.read().decode("utf-8", "replace")
            parsed = _safe_json(raw)
            raise SharpAIError(error.code, _extract_error(parsed) or error.reason or "request failed", parsed) from None

    def _request(
        self,
        method: str,
        path: str,
        *,
        body: Optional[Any] = None,
        query: Optional[Dict[str, Any]] = None,
        extra_headers: Optional[Dict[str, str]] = None,
    ) -> Any:
        with self._open(method, path, body, query, extra_headers) as response:
            text = response.read().decode("utf-8", "replace")
        return _safe_json(text) if text else None

    # ---- health / settings ----

    def health(self) -> Any:
        return self._request("GET", "/health")

    def ready(self) -> Any:
        return self._request("GET", "/ready")

    def get_settings(self) -> Any:
        return self._request("GET", "/api/settings")

    def update_settings(self, settings: Any) -> Any:
        return self._request("PUT", "/api/settings", body=settings)

    # ---- models (Ollama-compatible) ----

    def list_models(self) -> Any:
        return self._request("GET", "/api/tags")

    def running_models(self) -> Any:
        return self._request("GET", "/api/ps")

    def show_model(self, name: str) -> Any:
        return self._request("POST", "/api/show", body={"name": name})

    def delete_model(self, name: str) -> Any:
        return self._request("DELETE", "/api/delete", body={"name": name})

    def import_model(self, path: str, name: Optional[str] = None) -> Any:
        """Import a GGUF model already present on the server's local filesystem (no download, no token)."""
        body: dict = {"path": path}
        if name:
            body["name"] = name
        return self._request("POST", "/api/import", body=body)

    def list_presets(self, query: Optional[dict] = None) -> Any:
        """List Modelfile-equivalent presets."""
        return self._request("GET", "/v1.0/models/presets", query=query)

    def create_preset(self, name: str, model: str, **fields: Any) -> Any:
        """Create a Modelfile-equivalent preset. Extra fields: system, temperature, max_tokens, top_p, template, stop."""
        body: dict = {"name": name, "model": model, **fields}
        return self._request("POST", "/v1.0/models/presets", body=body)

    def get_preset(self, name: str) -> Any:
        return self._request("GET", f"/v1.0/models/presets/{urllib.parse.quote(name)}")

    def delete_preset(self, name: str) -> Any:
        return self._request("DELETE", f"/v1.0/models/presets/{urllib.parse.quote(name)}")

    def unload_model(self, name: Optional[str] = None) -> Any:
        return self._request("POST", "/api/unload", body={"name": name} if name else {})

    def pull_model(self, name: str) -> Iterator[Any]:
        """Pull a model, yielding parsed newline-delimited progress objects."""
        with self._open("POST", "/api/pull", {"name": name}, None, None) as response:
            for raw in response:
                line = raw.decode("utf-8", "replace").strip()
                if line:
                    yield _safe_json(line)

    # ---- inference (Ollama-compatible) ----

    def chat(self, model: str, messages: List[Dict[str, Any]], *, stream: bool = False, **options: Any) -> Any:
        return self._request("POST", "/api/chat", body={"model": model, "messages": messages, "stream": stream, **options})

    def generate(self, model: str, prompt: str, *, stream: bool = False, **options: Any) -> Any:
        return self._request("POST", "/api/generate", body={"model": model, "prompt": prompt, "stream": stream, **options})

    def embeddings(self, model: str, input: Any) -> Any:
        return self._request("POST", "/api/embed", body={"model": model, "input": input})

    # ---- OpenAI-compatible ----

    def openai_models(self) -> Any:
        return self._request("GET", "/v1/models")

    def openai_chat(self, model: str, messages: List[Dict[str, Any]], **options: Any) -> Any:
        return self._request("POST", "/v1/chat/completions", body={"model": model, "messages": messages, **options})

    def openai_completions(self, model: str, prompt: str, **options: Any) -> Any:
        return self._request("POST", "/v1/completions", body={"model": model, "prompt": prompt, **options})

    def openai_embeddings(self, model: str, input: Any) -> Any:
        return self._request("POST", "/v1/embeddings", body={"model": model, "input": input})

    # ---- request history ----

    def request_history(self, **query: Any) -> Any:
        return self._request("GET", "/v1.0/api/request-history", query=query)

    def request_history_summary(self, **query: Any) -> Any:
        return self._request("GET", "/v1.0/api/request-history/summary", query=query)

    def request_history_entry(self, entry_id: str) -> Any:
        return self._request("GET", f"/v1.0/api/request-history/{urllib.parse.quote(entry_id)}")

    # ---- authentication ----

    def login(self, email: str, password: str, tenant_guid: Optional[str] = None) -> Any:
        headers = {"x-email": email, "x-password": password}
        if tenant_guid:
            headers["x-tenant-guid"] = tenant_guid
        result = self._request("POST", "/v1.0/token", extra_headers=headers)
        if isinstance(result, dict) and result.get("token"):
            self.token = result["token"]
        return result

    def session(self) -> Any:
        return self._request("GET", "/v1.0/token")

    def logout(self) -> Any:
        result = self._request("DELETE", "/v1.0/token")
        self.token = None
        return result

    def audit(self, **query: Any) -> Any:
        return self._request("GET", "/v1.0/api/audit", query=query)

    # ---- account / RBAC management ----

    def list_tenants(self, **query: Any) -> Any:
        return self._request("GET", "/v1.0/tenants", query=query)

    def create_tenant(self, name: str) -> Any:
        return self._request("POST", "/v1.0/tenants", body={"Name": name})

    def list_users(self, tenant_guid: str, **query: Any) -> Any:
        return self._request("GET", f"/v1.0/tenants/{tenant_guid}/users", query=query)

    def create_user(self, tenant_guid: str, email: str, password: str, **fields: Any) -> Any:
        body = {"Email": email, "Password": password, **fields}
        return self._request("POST", f"/v1.0/tenants/{tenant_guid}/users", body=body)

    def delete_user(self, tenant_guid: str, user_guid: str) -> Any:
        return self._request("DELETE", f"/v1.0/tenants/{tenant_guid}/users/{user_guid}")

    def list_credentials(self, tenant_guid: str, **query: Any) -> Any:
        return self._request("GET", f"/v1.0/tenants/{tenant_guid}/credentials", query=query)

    def create_credential(self, tenant_guid: str, user_guid: str, name: str) -> Any:
        return self._request("POST", f"/v1.0/tenants/{tenant_guid}/credentials", body={"UserGuid": user_guid, "Name": name})

    def list_roles(self, tenant_guid: str, **query: Any) -> Any:
        return self._request("GET", f"/v1.0/tenants/{tenant_guid}/roles", query=query)

    def create_assignment(self, tenant_guid: str, user_guid: str, *, role_guid: Optional[str] = None, role_name: Optional[str] = None, **fields: Any) -> Any:
        body: Dict[str, Any] = {"UserGuid": user_guid, **fields}
        if role_guid:
            body["RoleGuid"] = role_guid
        if role_name:
            body["RoleName"] = role_name
        return self._request("POST", f"/v1.0/tenants/{tenant_guid}/assignments", body=body)

    def user_permissions(self, tenant_guid: str, user_guid: str) -> Any:
        return self._request("GET", f"/v1.0/tenants/{tenant_guid}/users/{user_guid}/permissions")

    def credential_permissions(self, tenant_guid: str, credential_guid: str) -> Any:
        return self._request("GET", f"/v1.0/tenants/{tenant_guid}/credentials/{credential_guid}/permissions")


def _safe_json(text: str) -> Any:
    try:
        return json.loads(text)
    except (json.JSONDecodeError, ValueError):
        return text


def _extract_error(parsed: Any) -> Optional[str]:
    if isinstance(parsed, dict):
        error = parsed.get("error")
        if isinstance(error, str):
            return error
        if isinstance(error, dict) and isinstance(error.get("message"), str):
            return error["message"]
        if isinstance(parsed.get("message"), str):
            return parsed["message"]
    return None

"""Unit tests for the SharpAI Python SDK. HTTP is mocked, so no live server is required.

Run: python -m unittest discover -s tests   (from sdk/python, with src on PYTHONPATH)
"""

import io
import json
import sys
import unittest
import urllib.error
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

from sharpai import SharpAIClient, SharpAIError  # noqa: E402


class FakeResponse:
    def __init__(self, body: bytes = b"", lines=None):
        self._body = body
        self._lines = lines or []

    def read(self):
        return self._body

    def __enter__(self):
        return self

    def __exit__(self, *args):
        return False

    def __iter__(self):
        return iter(self._lines)


class ClientTests(unittest.TestCase):
    def test_builds_url_and_uses_get(self):
        with mock.patch("urllib.request.urlopen", return_value=FakeResponse(b'{"models":[]}')) as opener:
            client = SharpAIClient("http://127.0.0.1:8000/")
            result = client.list_models()
        request = opener.call_args[0][0]
        self.assertEqual(request.full_url, "http://127.0.0.1:8000/api/tags")
        self.assertEqual(request.method, "GET")
        self.assertEqual(result, {"models": []})

    def test_bearer_and_apikey_headers(self):
        with mock.patch("urllib.request.urlopen", return_value=FakeResponse(b"{}")) as opener:
            client = SharpAIClient("http://127.0.0.1:8000", token="abc", api_key="key")
            client.session()
        request = opener.call_args[0][0]
        self.assertEqual(request.headers.get("Authorization"), "Bearer abc")
        self.assertEqual(request.headers.get("X-api-key"), "key")

    def test_query_params_encoded(self):
        with mock.patch("urllib.request.urlopen", return_value=FakeResponse(b"{}")) as opener:
            client = SharpAIClient("http://127.0.0.1:8000")
            client.request_history(pageSize=25, method="GET")
        url = opener.call_args[0][0].full_url
        self.assertIn("pageSize=25", url)
        self.assertIn("method=GET", url)

    def test_login_stores_token(self):
        body = json.dumps({"token": "tok123", "userId": "usr_1", "tenantId": "ten_1"}).encode()
        with mock.patch("urllib.request.urlopen", return_value=FakeResponse(body)) as opener:
            client = SharpAIClient("http://127.0.0.1:8000")
            result = client.login("a@b.c", "pw", "ten_1")
        request = opener.call_args[0][0]
        self.assertEqual(request.headers.get("X-email"), "a@b.c")
        self.assertEqual(request.headers.get("X-password"), "pw")
        self.assertEqual(result["token"], "tok123")
        self.assertEqual(client.token, "tok123")

    def test_http_error_raises_sharpai_error(self):
        error = urllib.error.HTTPError(
            url="http://127.0.0.1:8000/api/settings",
            code=403,
            msg="Forbidden",
            hdrs=None,
            fp=io.BytesIO(json.dumps({"error": {"message": "nope"}}).encode()),
        )
        with mock.patch("urllib.request.urlopen", side_effect=error):
            client = SharpAIClient("http://127.0.0.1:8000")
            with self.assertRaises(SharpAIError) as ctx:
                client.get_settings()
        self.assertEqual(ctx.exception.status, 403)
        self.assertEqual(ctx.exception.message, "nope")

    def test_pull_streams_json_lines(self):
        lines = [b'{"status":"pulling"}', b"", b'{"status":"done"}']
        with mock.patch("urllib.request.urlopen", return_value=FakeResponse(lines=lines)):
            client = SharpAIClient("http://127.0.0.1:8000")
            events = list(client.pull_model("my-model"))
        self.assertEqual(events, [{"status": "pulling"}, {"status": "done"}])


if __name__ == "__main__":
    unittest.main()

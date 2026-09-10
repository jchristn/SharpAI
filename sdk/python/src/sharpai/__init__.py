"""SharpAI Python SDK.

A zero-dependency client for a SharpAI server (Ollama- and OpenAI-compatible APIs, request history,
authentication, and account/RBAC management).
"""

from .client import SharpAIClient
from .errors import SharpAIError

__version__ = "5.0.0"
__all__ = ["SharpAIClient", "SharpAIError", "__version__"]

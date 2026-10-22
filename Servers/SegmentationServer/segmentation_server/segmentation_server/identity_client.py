"""Which annotation contexts this segmentation server is local to, asked of the Identity server.

The server authenticates as the shared "segmentation" OAuth client (client credentials, ``Viking.Annotation`` scope)
and calls ``Permissions/AssignedAnnotationContexts?service=<name>``, which answers with every context reached from
the local resources of the SegmentationService resource named ``<name>``. This mirrors the correction service's
IdentityAssignmentClient (Servers/GrpcSectionCorrectionService).

The list is refreshed every ``refresh_seconds``, and sooner (at most once a minute) when a stream names a context
the last list did not have, so a context made local in Identity is picked up without a restart. A failed refresh
keeps the previous list.
"""

from __future__ import annotations

import asyncio
import json
import logging
import os
import time
import urllib.parse
import urllib.request
from dataclasses import dataclass
from typing import Callable, Dict, Optional

logger = logging.getLogger(__name__)

DEFAULT_CLIENT_ID = "segmentation"
DEFAULT_REFRESH_SECONDS = 600.0
# How soon an unknown context id may trigger another refresh.
UNKNOWN_CONTEXT_REFRESH_SECONDS = 60.0
_TIMEOUT_SECONDS = 30.0

# (url, form or None, headers) -> response body. Injected in tests.
HttpCall = Callable[[str, Optional[Dict[str, str]], Dict[str, str]], bytes]


@dataclass(frozen=True)
class LocalContext:
    """One annotation context this server reads tiles for."""

    annotation_context_id: int
    name: str
    # The context's VikingXML URL.
    endpoint: str


def _http_call(url: str, form: Optional[Dict[str, str]], headers: Dict[str, str]) -> bytes:
    data = urllib.parse.urlencode(form).encode("ascii") if form is not None else None
    request = urllib.request.Request(url, data=data, headers=headers, method="POST" if form is not None else "GET")
    with urllib.request.urlopen(request, timeout=_TIMEOUT_SECONDS) as response:
        return response.read()


def _with_slash(url: str) -> str:
    return url if url.endswith("/") else url + "/"


@dataclass(frozen=True)
class IdentityOptions:
    endpoint: str
    api_endpoint: str
    client_id: str
    client_secret: str
    service_name: str

    @classmethod
    def from_env(cls) -> Optional["IdentityOptions"]:
        """The options from ``IdentityServer__*`` and ``SEGMENTATION_SERVICE_NAME``, or None when any required one is unset."""
        endpoint = os.environ.get("IdentityServer__Endpoint", "").strip()
        secret = os.environ.get("IdentityServer__ClientSecret", "").strip()
        name = os.environ.get("SEGMENTATION_SERVICE_NAME", "").strip()
        if not (endpoint and secret and name):
            return None
        return cls(
            endpoint=endpoint,
            api_endpoint=os.environ.get("IdentityServer__ApiEndpoint", "").strip() or endpoint,
            client_id=os.environ.get("IdentityServer__ClientId", "").strip() or DEFAULT_CLIENT_ID,
            client_secret=secret,
            service_name=name,
        )


class IdentityAssignments:
    """The local annotation contexts by id, refreshed from Identity in the background of the calls that need them."""

    def __init__(
        self,
        options: IdentityOptions,
        http_call: HttpCall = _http_call,
        refresh_seconds: float = DEFAULT_REFRESH_SECONDS,
        time_fn: Callable[[], float] = time.monotonic,
    ) -> None:
        self._options = options
        self._http_call = http_call
        self._refresh_seconds = refresh_seconds
        self._time_fn = time_fn
        self._contexts: Dict[int, LocalContext] = {}
        self._fetched_at: Optional[float] = None
        self._attempted_at: Optional[float] = None
        self._lock = asyncio.Lock()

    def fetch(self) -> Dict[int, LocalContext]:
        """Ask Identity for the local contexts now. Blocking; raises on any failure."""
        options = self._options
        token_body = self._http_call(
            urllib.parse.urljoin(_with_slash(options.endpoint), "connect/token"),
            {
                "grant_type": "client_credentials",
                "client_id": options.client_id,
                "client_secret": options.client_secret,
                "scope": "Viking.Annotation",
            },
            {"Accept": "application/json"},
        )
        token = json.loads(token_body)["access_token"]
        url = urllib.parse.urljoin(
            _with_slash(options.api_endpoint),
            "Permissions/AssignedAnnotationContexts?service=" + urllib.parse.quote(options.service_name, safe=""),
        )
        rows = json.loads(self._http_call(url, None, {"Accept": "application/json", "Authorization": f"Bearer {token}"}))
        contexts: Dict[int, LocalContext] = {}
        for row in rows:
            lowered = {str(key).lower(): value for key, value in row.items()}
            context_id = int(lowered.get("annotationcontextid") or 0)
            endpoint = lowered.get("endpoint")
            if context_id > 0 and endpoint:
                contexts[context_id] = LocalContext(context_id, str(lowered.get("name") or ""), str(endpoint))
        return contexts

    async def get(self, annotation_context_id: int) -> Optional[LocalContext]:
        """The local context with this id, or None when this server is not local to it."""
        now = self._time_fn()
        stale = self._fetched_at is None or now - self._fetched_at >= self._refresh_seconds
        unknown = annotation_context_id not in self._contexts
        recently_tried = self._attempted_at is not None and now - self._attempted_at < UNKNOWN_CONTEXT_REFRESH_SECONDS
        if (stale or unknown) and not recently_tried:
            await self._refresh()
        return self._contexts.get(annotation_context_id)

    async def _refresh(self) -> None:
        async with self._lock:
            now = self._time_fn()
            if self._attempted_at is not None and now - self._attempted_at < UNKNOWN_CONTEXT_REFRESH_SECONDS:
                return
            self._attempted_at = now
            try:
                contexts = await asyncio.get_running_loop().run_in_executor(None, self.fetch)
            except Exception as e:
                logger.warning(
                    "Identity refresh of local contexts for %s failed (%s); keeping %d known",
                    self._options.service_name,
                    e,
                    len(self._contexts),
                )
                return
            self._contexts = contexts
            self._fetched_at = now
            logger.info(
                "Segmentation service %s is local to %d annotation context(s): %s",
                self._options.service_name,
                len(contexts),
                ", ".join(sorted(c.name for c in contexts.values())) or "none",
            )

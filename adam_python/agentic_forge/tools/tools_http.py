import asyncio
import threading
from typing import Any, Dict, Optional
import httpx

def fmt(v: float) -> str:
    s = f"{float(v):.2f}"
    return s

class HttpRequestMixin:
    """
    Mixin che fornisce metodi per fare POST HTTP (async e sync).
    - _http_post(...)       -> async
    - _http_post_sync(...)  -> sync, compatibile anche quando c'è già un event loop attivo
    """

    async def _http_post(
        self,
        url: str,
        payload: Optional[Dict[str, Any]] = None,
        headers: Optional[Dict[str, str]] = None,
        timeout: Optional[float] = None,
    ) -> Dict[str, Any]:
        async with httpx.AsyncClient(timeout=timeout) as client:
            resp = await client.post(url, json=payload, headers=headers)
            try:
                data = resp.json()
            except Exception:
                data = resp.text
            return {
                "status": resp.status_code,
                "ok": resp.is_success,
                "data": data,
                "headers": dict(resp.headers),
            }

    def _http_post_sync(
        self,
        url: str,
        payload: Optional[Dict[str, Any]] = None,
        headers: Optional[Dict[str, str]] = None,
        timeout: Optional[float] = None,
    ) -> Dict[str, Any]:
        """
        Esegue la versione async anche in contesti dove c'è già un event loop (es. Jupyter, LangGraph async).
        Se non c'è loop, usa asyncio.run; se c'è, fa girare la coroutine in un thread separato.
        """
        try:
            asyncio.get_running_loop()
            running = True
        except RuntimeError:
            running = False

        if not running:
            return asyncio.run(self._http_post(url, payload=payload, headers=headers, timeout=timeout))

        # Se c'è già un loop, usiamo un nuovo loop in un thread dedicato
        result_box: Dict[str, Any] = {}
        exc_box: Dict[str, BaseException] = {}

        def _runner():
            loop = asyncio.new_event_loop()
            try:
                asyncio.set_event_loop(loop)
                result = loop.run_until_complete(
                    self._http_post(url, payload=payload, headers=headers, timeout=timeout)
                )
                result_box["result"] = result
            except BaseException as e:
                exc_box["exc"] = e
            finally:
                loop.close()

        t = threading.Thread(target=_runner, daemon=True)
        t.start()
        t.join()

        if "exc" in exc_box:
            raise exc_box["exc"]
        return result_box["result"]


from abc import ABC, abstractmethod
from typing import Any, Dict, Optional
from pydantic import BaseModel, Field
from langchain_core.tools import BaseTool


class HTTPPostTool(BaseTool, HttpRequestMixin, ABC):
    """
    BaseTool per tool che fanno POST HTTP.
    Dichiari:
      - endpoint: URL dell'API
      - timeout:  (float | None) -> None = no-timeout (sconsigliato in generale)
      - default_headers: headers comuni

    Implementi:
      - build_payload(**tool_args) -> Dict[str, Any]
    """

    endpoint: str
    timeout: Optional[float] = 15.0
    default_headers: Dict[str, str] = Field(default_factory=dict)

    @abstractmethod
    def build_payload(self, **tool_args) -> Dict[str, Any]:
        """Trasforma gli argomenti del tool nel payload JSON della POST."""

    def process_response(self, response: Dict[str, Any]) -> Any:
        """Processa la risposta della POST e ritorna il risultato desiderato."""
        return response

    # --- Sincrono ---
    def _run(self, **tool_args) -> Dict[str, Any]:
        payload = self.build_payload(**tool_args)
        response= self._http_post_sync(
            self.endpoint,
            payload=payload,
            headers=self.default_headers,
            timeout=self.timeout,
        )
        return self.process_response(response)

    # --- Asincrono ---
    async def _arun(self, **tool_args) -> Dict[str, Any]:
        payload = self.build_payload(**tool_args)
        response = await self._http_post(
            self.endpoint,
            payload=payload,
            headers=self.default_headers,
            timeout=self.timeout,
        )
        return self.process_response(response)
    


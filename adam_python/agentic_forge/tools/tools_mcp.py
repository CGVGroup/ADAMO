import asyncio
import threading
from langchain.tools import BaseTool
from langchain_mcp_adapters.client import MultiServerMCPClient
from typing import List

def load_mcp_tools_from_url_sync(url: str) -> List[BaseTool]:
        """Carica tools da server MCP streamable-http in modo sincrono (thread+event loop)."""
        def _run(coro, out_list):
            loop = asyncio.new_event_loop()
            try:
                out_list.append(loop.run_until_complete(coro))
            finally:
                loop.close()

        async def _aget_tools() -> List[BaseTool]:
            # Normalizza URL: alcuni client gradiscono trailing slash
            url_norm = url if url.endswith("/") else (url + "/")
            client = MultiServerMCPClient({
                "remote": {
                    "transport": "streamable_http",
                    "url": url_norm,
                    # "headers": {...}  # se ti serve auth
                }
            })
            tools = await client.get_tools()
            # tools è già una lista di LangChain BaseTool compatibili
            return tools  # type: ignore[return-value]

        out: List[List[BaseTool]] = []
        t = threading.Thread(target=_run, args=(_aget_tools(), out))
        t.start()
        t.join()
        if not out or not out[0]:
            raise RuntimeError(f"Nessun tool caricato dal server MCP: {url}")
        return out[0]
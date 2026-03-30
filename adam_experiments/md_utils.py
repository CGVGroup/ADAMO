from typing import List, Any, Dict, Union
from langchain_core.messages import HumanMessage, AIMessage, ToolMessage
import json
import html

def _guess_mime_from_b64(b64: str) -> str:
    s = str(b64).strip().replace("\n", "").replace("\r", "")[:20]
    if s.startswith("/9j/"):                  # JPEG
        return "image/jpeg"
    if s.startswith("iVBORw0KGgo"):          # PNG
        return "image/png"
    if s.startswith("R0lGOD"):               # GIF
        return "image/gif"
    if s.startswith("Qk"):                   # BMP
        return "image/bmp"
    if s.startswith("PHN2"):                 # "<svg" base64
        return "image/svg+xml"
    return "image/png"  # fallback sensato


def _md_escape(t: str) -> str:
    if not isinstance(t, str):
        t = str(t)
    repl = {
        '\\': '\\\\', '`': '\\`', '*': '\\*', '_': '\\_',
        '{': '\\{', '}': '\\}', '[': '\\[', ']': '\\]',
        '(': '\\(', ')': '\\)', '#': '\\#', '+': '\\+',
        '-': '\\-', '!': '\\!', '|': '\\|',
    }
    return "".join(repl.get(ch, ch) for ch in t)

def _role(m: Any) -> str:
    if isinstance(m, HumanMessage): return "Human"
    if isinstance(m, AIMessage):    return "AI"
    if isinstance(m, ToolMessage):
        return f"Tool Response: {m.name}" if getattr(m, "name", None) else "Tool Response"
    return m.__class__.__name__

def _to_data_uri(raw: str) -> str:
    """Accetta base64 puro o data URI; restituisce sempre una data URI corretta."""
    b64 = str(raw).strip().replace("\n", "").replace("\r", "")
    if not b64:
        return ""
    if b64.startswith("data:") or ";base64," in b64:
        return b64
    mime = _guess_mime_from_b64(b64)
    return f"data:{mime};base64,{b64}"

def _split_text_and_images(content: Union[str, List[Dict[str, Any]]]) -> Dict[str, List[str]]:
    """Ritorna {'texts': [...], 'images': [<img HTML>...]}."""
    texts: List[str] = []
    imgs: List[str] = []

    if isinstance(content, str):
        if content.strip():
            texts.append(_md_escape(content))
        return {"texts": texts, "images": imgs}

    for item in content or []:
        t = (item.get("type") or "").lower()
        if t == "text":
            txt = str(item.get("text", "")).strip()
            if txt:
                texts.append(_md_escape(txt))
        elif t == "image_url":
            raw = (item.get("image_url") or {}).get("url", "")
            data_uri = _to_data_uri(raw)
            if data_uri:
                imgs.append(f'<img alt="image" src="{html.escape(data_uri)}" />')
        else:
            # preserva blocchi non standard come JSON
            blob = "```json\n" + json.dumps(item, ensure_ascii=False, indent=2) + "\n```"
            texts.append(blob)

    return {"texts": texts, "images": imgs}



def _render_tool_calls(ai_msg: AIMessage) -> str:
    addkw = getattr(ai_msg, "additional_kwargs", {}) or {}
    calls = addkw.get("tool_calls")
    if calls is None:
        calls = getattr(ai_msg, "tool_calls", None)
    if not calls:
        return ""

    blocks = ["**Tool calls**"]
    for tc in calls:
        name = (tc.get("function") or {}).get("name") or tc.get("name") or "function"
        args = (tc.get("function") or {}).get("arguments") or tc.get("args") or {}
        if isinstance(args, str):
            try:
                args = json.loads(args)
            except Exception:
                pass
        args_str = json.dumps(args, ensure_ascii=False, indent=2)
        blocks.append(
            f"- **Tool call:** `{_md_escape(str(name))}`\n\n"
            f"```json\n{args_str}\n```"
        )
    return "\n".join(blocks).strip() + "\n"


# Background soft a seconda del ruolo (dark-mode friendly)
_ROLE_CLASS = {
    "Human": "human",
    "AI": "ai",
}

def _wrap_msg(role: str, body: str) -> str:
    css_class = _ROLE_CLASS.get(role, "tool")
    return (
        f'<details class="msg {css_class}">\n'
        f'<summary><strong>{role}</strong></summary>\n\n'
        f"{body.strip()}\n\n"
        f"</details>\n"
    )


def _section_images_and_content(content: Union[str, List[Dict[str, Any]]]) -> str:
    split = _split_text_and_images(content)
    parts: List[str] = []

    # Immagini
    if split["images"]:
        parts.append("**Images**")
        parts.extend(split["images"])
        parts.append("")  # newline

    # Testo
    parts.append("**Content**")
    if split["texts"]:
        parts.append("\n".join(split["texts"]))
    else:
        parts.append("_(no text)_")

    return "\n".join(parts)


def _render_instrumentations(ai_msg: AIMessage) -> str:
    meta = getattr(ai_msg, "response_metadata", {}) or {}

    # 1) Generation timing (del messaggio AI)
    gen_timing = meta.get("timing_ms") or {}

    # 2) Per-tool timings (dalle tool_calls)
    addkw = getattr(ai_msg, "additional_kwargs", {}) or {}
    calls = addkw.get("tool_calls") or getattr(ai_msg, "tool_calls", None) or []

    per_tool_lines: List[str] = []
    elapsed_list: List[float] = []

    def _elapsed_from(tms: dict) -> float | None:
        if not isinstance(tms, dict):
            return None
        if "elapsed" in tms and isinstance(tms["elapsed"], (int, float)):
            return float(tms["elapsed"])
        # fallback: se presenti t0/t1 numerici, calcola
        t0 = tms.get("t0"); t1 = tms.get("t1")
        if isinstance(t0, (int, float)) and isinstance(t1, (int, float)):
            return float(t1) - float(t0)
        return None

    for tc in calls:
        name = (tc.get("function") or {}).get("name") or tc.get("name") or "function"
        tms = (tc.get("timings_ms")
               or (tc.get("function") or {}).get("timings_ms")
               or {})
        if tms:
            per_tool_lines.append(f"  - `{_md_escape(str(name))}` → `{tms}`")
            el = _elapsed_from(tms)
            if el is not None:
                elapsed_list.append(el)

    # 3) Aggregate tool timings (derivati)
    aggregate_line = None
    if elapsed_list:
        total = sum(elapsed_list)
        aggregate_line = f"- **Tool timings (aggregate, ms):** `{{'elapsed_sum': {total}}}`"

    # 4) Token usage compatti
    usage = meta.get("token_usage") or getattr(ai_msg, "usage_metadata", None) or {}
    ct = usage.get("completion_tokens")
    pt = usage.get("prompt_tokens")
    tt = usage.get("total_tokens")

    lines: List[str] = []
    if gen_timing:
        lines.append(f"- **Generation timing (ms):** `{gen_timing}`")
    if aggregate_line:
        lines.append(aggregate_line)
    if per_tool_lines:
        lines.append("- **Tool timings (per call, ms):**")
        lines.extend(per_tool_lines)

    tok_bits = []
    if pt is not None: tok_bits.append(f"prompt={pt}")
    if ct is not None: tok_bits.append(f"completion={ct}")
    if tt is not None: tok_bits.append(f"total={tt}")
    if tok_bits:
        lines.append(f"- **Token usage:** " + ", ".join(tok_bits))

    if not lines:
        return ""

    body = "\n".join(lines)
    return (
        "<details>\n"
        "<summary>📊 <strong>Instrumentations</strong></summary>\n\n"
        f"{body}\n"
        "</details>\n"
    )



def msg_to_md(messages: List[Any]) -> str:
    out: List[str] = []

    for m in messages:
        role = _role(m)

        body_parts: List[str] = []

        # Images + Content
        body_parts.append(_section_images_and_content(getattr(m, "content", "")))

        # AI: tool calls
        if isinstance(m, AIMessage):
            tc = _render_tool_calls(m)
            if tc.strip():
                body_parts.append(tc)

            inst = _render_instrumentations(m)
            if inst.strip():
                body_parts.append(inst)

        body = "\n\n".join(body_parts)
        out.append(_wrap_msg(role, body))

    # CSS inline per i colori pastello
    style = """
<style>
.msg { padding:10px; margin:10px 0; border-radius:8px; }
.msg.human { background: #2a2a2a; border: 1px solid #555; }
.msg.ai    { background: #262b36; border: 1px solid #445; }
.msg.tool  { background: #2e3330; border: 1px solid #474; }
summary { cursor:pointer; }
</style>
"""

    return style + "\n".join(out) + "\n"

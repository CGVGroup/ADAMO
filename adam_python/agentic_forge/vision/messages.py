from langchain_core.messages import HumanMessage
from typing import Union, Optional, List
from pathlib import Path
from PIL import Image
from io import BytesIO
import base64, mimetypes, os, re, urllib.parse, urllib.request

def VisionHumanMessage(
    text: Optional[str] = None,
    image: Optional[Union[str, Path, bytes, Image.Image]] = None,
    mime: Optional[str] = None
) -> HumanMessage:
    """
    Accetta: 
      - testo opzionale
      - immagine come: data URI, base64 nudo, path locale (str/Path), URL (http/https/file), bytes, PIL.Image
    """
    if not text and not image:
        raise ValueError("Devi fornire almeno `text` o `image`.")

    content: List[dict] = []
    if text:
        content.append({"type": "text", "text": text})

    if image is not None:
        url: Optional[str] = None
        detected_mime: str = mime or "image/png"

        # --- str: potrebbe essere data URI, base64, path, URL ---
        if isinstance(image, str):
            s = image.strip()

            # 1) data URI
            if s.startswith("data:image"):
                url = s
                # prova a leggere il mime dalla data URI
                m = re.match(r"^data:([^;]+);base64,", s, flags=re.I)
                if m:
                    detected_mime = m.group(1)

            else:
                # 2) URL?
                parsed = urllib.parse.urlparse(s)
                is_url = parsed.scheme in {"http", "https", "file"}
                if is_url:
                    if parsed.scheme == "file":
                        # file:// URL -> carica come file locale
                        local_path = Path(urllib.request.url2pathname(parsed.path))
                        b64 = _encode_image_to_base64(local_path)
                        detected_mime = mime or _guess_mime_from_path(local_path) or "image/png"
                        url = f"data:{detected_mime};base64,{b64}"
                    else:
                        # http/https: passa come URL remoto (se il tuo modello lo supporta)
                        url = s
                        # prova a dedurre il mime dall'estensione se c'è
                        ext_mime = _guess_mime_from_path(Path(parsed.path))
                        if ext_mime:
                            detected_mime = ext_mime

                # 3) path locale?
                elif os.path.exists(s):
                    p = Path(s)
                    b64 = _encode_image_to_base64(p)
                    detected_mime = mime or _guess_mime_from_path(p) or "image/png"
                    url = f"data:{detected_mime};base64,{b64}"

                # 4) base64 nudo?
                elif _is_base64(s):
                    detected_mime = mime or "image/png"
                    url = f"data:{detected_mime};base64,{s}"

                else:
                    raise ValueError("Stringa `image` non riconosciuta: non è data URI, URL, path esistente o base64 valido.")

        # --- Path ---
        elif isinstance(image, Path):
            b64 = _encode_image_to_base64(image)
            detected_mime = mime or _guess_mime_from_path(image) or "image/png"
            url = f"data:{detected_mime};base64,{b64}"

        # --- bytes ---
        elif isinstance(image, (bytes, bytearray)):
            b64 = base64.b64encode(image if isinstance(image, bytes) else bytes(image)).decode("utf-8")
            detected_mime = mime or "image/png"
            url = f"data:{detected_mime};base64,{b64}"

        # --- PIL.Image ---
        elif isinstance(image, Image.Image):
            buf = BytesIO()
            # se non sai il formato, salva come PNG
            fmt = (image.format or "PNG")
            image.save(buf, format=fmt)
            b64 = base64.b64encode(buf.getvalue()).decode("utf-8")
            detected_mime = mime or _guess_mime_from_format(fmt) or "image/png"
            url = f"data:{detected_mime};base64,{b64}"

        else:
            raise TypeError("Tipo immagine non supportato.")

        content.append({
            "type": "image_url",
            "image_url": {"url": url}
        })

    return HumanMessage(content=content)


def _encode_image_to_base64(image: Union[str, Path, bytes, Image.Image]) -> str:
    if isinstance(image, (str, Path)):
        p = Path(image)
        with open(p, "rb") as f:
            img_bytes = f.read()
    elif isinstance(image, bytes):
        img_bytes = image
    elif isinstance(image, Image.Image):
        buf = BytesIO()
        fmt = (image.format or "PNG")
        image.save(buf, format=fmt)
        img_bytes = buf.getvalue()
    else:
        raise TypeError("Tipo immagine non supportato.")
    return base64.b64encode(img_bytes).decode("utf-8")


def _is_base64(s: str) -> bool:
    # tollerante ma sicuro
    try:
        # rimuovi eventuali spazi/newline
        s_clean = re.sub(r"\s+", "", s)
        # validazione stretta
        decoded = base64.b64decode(s_clean, validate=True)
        # evita falsi positivi su stringhe corte
        return len(decoded) > 0
    except Exception:
        return False


def _guess_mime_from_path(p: Path) -> Optional[str]:
    mime, _ = mimetypes.guess_type(str(p))
    return mime


def _guess_mime_from_format(fmt: str) -> Optional[str]:
    fmt = fmt.upper()
    if fmt == "PNG": return "image/png"
    if fmt in {"JPG", "JPEG"}: return "image/jpeg"
    if fmt == "WEBP": return "image/webp"
    if fmt == "GIF": return "image/gif"
    if fmt == "BMP": return "image/bmp"
    if fmt == "TIFF": return "image/tiff"
    return None

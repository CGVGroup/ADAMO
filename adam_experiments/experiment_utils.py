import pandas as pd
from pathlib import Path
from datetime import datetime
from typing import Optional,Sequence,Tuple, Dict, Any, Union
import os
import shutil
import sys
sys.path.append("../adam_python")
sys.path.append("../adam_experiments")

from langchain_core.messages import BaseMessage,AIMessage,HumanMessage

from langgraph.checkpoint.sqlite import SqliteSaver
import matplotlib.pyplot as plt

import yaml


import re
from pathlib import Path
import pandas as pd
from typing import Any, Dict, List,Union
import numpy as np

from fractions import Fraction

import base64
import io
from PIL import Image

from md_utils import msg_to_md


AggInput = Union[str, Path, pd.DataFrame]


_DEFAULT_METRICS = [
    # vecchie/base
    "solutionCheck", "inputImages",
    "inputTokens", "outputTokens", "totalTokens",
    "completedTools", "failedTools",

    # nuove (last + aggregati)
    "inputTokensLast", "outputTokensLast", "totalTokensLast",
    "inputImagesLast",
    "totalCalls", "toolsCount",
    "totalGenerationTime", "avgPerGenerationTime",
    "totalToolTime", "avgPerToolTime",

    "toolWalk","toolLook","toolDropObject","toolPickObject"
]

_TOOLS=["Walk","Look","DropObject","PickObject"]






SRC_DATA_PATH=Path("../adam_unity/Assets/BenchmarkData")
DST_DATA_PATH=Path("./exp0")

EPISODE_CSV=Path("runData.csv")
RESULT_CSV=Path("results.csv")

EXP_RUNS_RES=Path("exp_runs_results.csv")
EXP_RUNS_RES_AGG=Path("exp_runs_results_aggregated.csv")

CHECKPOINTER_DB_PATH=Path("../adam_python/checkpoints/checkpoint.db")
CHECKPOINTER_CONF_PATH=Path("../adam_python/agentic_forge/configs/checkpointer_config.yaml")

EPISODE_CSV_COLS=["scene","taskId","episodeDifficulty","taskPrompt","solutionChecker",
                  "graphicalResolution","graphicalLighting","model","objectIdentifier",
                  "coordinatesType","status","repetitions"]
RESULT_CSV_COLS=["repIndex","solutionCheck","completedTools","stoppedTools","failedTools","threadId"]

EPISODE_DEF_YAML=Path("../episodes_definition.yaml")

CHEKPOINTER_COLS=[""]


def copy_folder(
    src: str | Path,
    dest: str | Path,
    timestamp_fmt: Union[str,None] = "%Y%m%d-%H%M%S",
    preserve_symlinks: bool = True,
    exclude_exts: Optional[Sequence[str]] = [".meta"],
    case_sensitive: bool = False,
) -> Path:
    """
    Copia ricorsivamente la directory `src` in una nuova directory `dest_<timestamp>`,
    escludendo file/cartelle che matchano le estensioni in `exclude_exts`.

    - `exclude_exts`: lista di estensioni (con o senza '.') da escludere.
        Esempi: [".log", "tmp", ".tar.gz", ".git"]
      Criteri di esclusione:
        * File: se suffix finale o catena completa di suffix (".tar.gz") è in lista.
        * Cartelle: se "."+nome_cartella è in lista (es. ".git") OPPURE se il nome
          termina con una delle estensioni (es. "build.tmp").
    - `case_sensitive`: se False (default) normalizza tutto a lowercase.

    Restituisce il Path della destinazione finale creata.
    """
    src = Path(src)
    if not src.exists():
        raise FileNotFoundError(f"Sorgente non trovata: {src}")
    if not src.is_dir():
        raise NotADirectoryError(f"Sorgente non è una directory: {src}")

    dest = Path(dest)
    dest_parent = dest.parent if dest.parent != Path("") else Path(".")
    dest_name_ts=dest.name
    if timestamp_fmt:
        now = datetime.now().astimezone()
        ts = now.strftime(timestamp_fmt)

        
        dest_name_ts = f"{dest.name}_{ts}" if dest.name else ts
        dest_final = dest_parent / dest_name_ts
    else:
        dest_final=dest

    if dest_final.exists():
        counter = 1
        while True:
            candidate = dest_parent / f"{dest_name_ts}-{counter:03d}"
            if candidate.exists():
                
                counter += 1
            else:
                dest_final = candidate
                break
    dest_final.parent.mkdir(parents=True, exist_ok=True)

    # --- Normalizzazione estensioni da escludere ---
    def _normalize_exts(exts: Sequence[str]) -> set[str]:
        norm = set()
        for e in exts:
            if not e:
                continue
            s = e.strip()
            if not s.startswith("."):
                s = "." + s
            norm.add(s if case_sensitive else s.lower())
        return norm

    blocked = _normalize_exts(exclude_exts or ())

    def _name_matches_ext(name: str) -> bool:
        """Ritorna True se il nome (file o cartella) matcha una delle estensioni bloccate."""
        if not case_sensitive:
            name_cmp = name.lower()
        else:
            name_cmp = name

        p = Path(name_cmp)

        # Estensioni potenziali per i file
        suffix_last = p.suffix  # es: ".gz"
        suffix_chain = "".join(p.suffixes)  # es: ".tar.gz"

        # Per directory: trattiamo ".<nome_dir>" come estensione (es. ".git")
        dot_name = "." + name_cmp  # es: ".git"

        # Match diretto su file (ultima o catena completa)
        if suffix_last and (suffix_last in blocked or suffix_chain in blocked):
            return True

        # Match su cartella (o fallback su nome "decorato")
        if dot_name in blocked:
            return True

        # Match "nome termina con estensione" (utile per cartelle tipo "cache.tmp")
        for ext in blocked:
            if name_cmp.endswith(ext):
                return True

        return False

    def _ignore_func(dirpath: str, names: list[str]) -> set[str]:
        to_ignore: set[str] = set()
        base = Path(dirpath)
        for n in names:
            p = base / n
            # Determina se è directory per consentire l'esclusione preventiva (non discendere)
            try:
                is_dir = p.is_dir()
            except OSError:
                # In caso di permessi/ENOENT, lascia decidere al match per nome
                is_dir = False

            if _name_matches_ext(n):
                to_ignore.add(n)
                continue

            # Se è symlink e preserviamo i link: trattalo come file (non discendere)
            if preserve_symlinks and p.is_symlink():
                # Se il nome del link matcha estensioni, escludi
                if _name_matches_ext(n):
                    to_ignore.add(n)

        return to_ignore

    shutil.copytree(
        src,
        dest_final,
        symlinks=preserve_symlinks,
        ignore_dangling_symlinks=True,
        copy_function=shutil.copy2,
        dirs_exist_ok=False,
        ignore=_ignore_func if blocked else None,
    )

    return dest_final

# ---------------------------------------------------------------------------
# Utilities
# ---------------------------------------------------------------------------


def _mask_images_inplace(msgs: List[BaseMessage]) -> Tuple[int, int]:
    """
    Maschera in-place gli URL base64 delle immagini nei HumanMessage, sostituendo
    il valore con '<bs64_image_placeholder>' per evitare log/memoria enormi.

    Inoltre calcola e ritorna le dimensioni (width, height) in pixel della PRIMA
    immagine trovata e decodificata correttamente. Si assume che tutte le immagini
    abbiano le stesse dimensioni; quindi basta la prima. Se nessuna immagine viene
    letta con successo, ritorna (0, 0).

    Formati accettati per l'URL immagine:
      - data URL: 'data:image/<fmt>;base64,<payload>'
      - semplice base64 senza prefisso 'data:...' (proverà a decodificarlo)
      - (altri tipi verranno mascherati comunque se presenti)
    """
    got_wh: bool = False
    width: int = 0
    height: int = 0

    def _extract_b64_payload(url: str) -> Optional[bytes]:
        """Ritorna i bytes decodificati da un url/payload base64, se possibile."""
        if not url:
            return None
        # Caso 1: data URL con header
        if url.startswith("data:"):
            # Esempio: data:image/png;base64,AAAA...
            try:
                header, b64part = url.split(",", 1)
            except ValueError:
                return None
            # Verifica che sia base64
            if ";base64" in header:
                try:
                    return base64.b64decode(b64part, validate=False)
                except Exception:
                    return None
            return None
        # Caso 2: payload base64 puro (senza header)
        # Proviamo a decodificarlo; se fallisce, non è base64 valido.
        try:
            return base64.b64decode(url, validate=False)
        except Exception:
            return None

    for m in msgs:
        try:
            if isinstance(m, HumanMessage) and isinstance(m.content, list):
                for part in m.content:
                    # Part schema tipico: {"type":"image_url","image_url":{"url": "..."}}
                    if isinstance(part, dict) and part.get("type") == "image_url":
                        img = part.get("image_url")
                        if isinstance(img, dict) and "url" in img:
                            url = img.get("url") or ""
                            # Se non abbiamo ancora width/height, proviamo a calcolare dalla prima immagine
                            if not got_wh:
                                payload = _extract_b64_payload(url)
                                if payload:
                                    try:
                                        with Image.open(io.BytesIO(payload)) as im:
                                            width, height = im.size
                                            got_wh = True
                                    except Exception:
                                        # ignoriamo errori di parsing immagine
                                        pass
                            # In ogni caso: maschera l'URL
                            img["url"] = "<bs64_image_placeholder>"
        except Exception:
            # Non interrompere il masking in caso di formati non previsti
            continue

    return width, height


def _msgs_fingerprint(msgs: List[BaseMessage]) -> str:
    """
    Fingerprint leggero per deduplicare snapshot consecutivi identici.
    Non è un hash crittografico: basta una firma corta e stabile.
    """
    parts = []
    for m in msgs:
        mtype = m.__class__.__name__
        mid = getattr(m, "id", "") or ""
        # Usa una porzione ridotta del contenuto per evitare firme giganti
        c = getattr(m, "content", "")
        if isinstance(c, list):
            c = str(c[:1])
        elif isinstance(c, str):
            c = c[:64]
        else:
            c = str(type(c))
        parts.append(f"{mtype}:{mid}:{c}")
    return "|".join(parts)


def _is_model_ai_only_flag(ai: AIMessage) -> bool:
    """
    TRUE se l'AIMessage è stato generato dal modello.
    La *sola* regola richiesta: usare il flag response_metadata['is_dev_injected'].
    - Se is_dev_injected == True  -> è *iniettato dallo sviluppatore*  -> FALSE
    - Altrimenti (manca o False)  -> trattalo come *generato dal modello* -> TRUE
    """
    rm = getattr(ai, "response_metadata", None) or {}
    return not bool(rm.get("is_dev_injected", False))


# ---------------------------------------------------------------------------
# Funzione principale
# ---------------------------------------------------------------------------

def get_ckpt_messages(
    thread_id: str,
    saver,                        # es. SqliteSaver di LangGraph
    limit: Optional[int] = None,  # None -> tutti i checkpoint
    include_empty: bool = False,  # se True conserva anche snapshot senza messaggi
    mask_images: bool = False,    # se True maschera immagini base64 nei HumanMessage
    order: str = "newest_first",  # "oldest_first" | "newest_first"
    dedupe_consecutive: bool = True,  # rimuove snapshot consecutivi identici
) -> Tuple[List[List[BaseMessage]], str]:

    def _warn(msg: str) -> None:
        print(f"[WARN][get_ckpt_messages][{thread_id}] {msg}")

    # 1) Leggi i checkpoint per thread
    cfg = {"configurable": {"thread_id": thread_id}}
    try:
        gen = saver.list(cfg, limit=limit)
        tuples = list(gen)
        try:
            close = getattr(gen, "close", None)
            if callable(close):
                close()
        except Exception:
            pass
    except Exception as e:
        _warn(f"errore nel leggere i checkpoint: {e}")
        return [], ""

    # 2) Ordina dal più vecchio al più nuovo
    def _key(t):
        cp = getattr(t, "checkpoint", {}) or {}
        ts = cp.get("ts") or ""  # ISO timestamp se presente
        cid = (cp.get("id")
               or t.config.get("configurable", {}).get("checkpoint_id")
               or "")
        return (ts, cid)
    tuples.sort(key=_key)

    # 3) Estrai snapshot messages (mask/ dedupe opzionali)
    snapshots: List[List[BaseMessage]] = []
    last_fp = None
    for i, t in enumerate(tuples):
        cp = getattr(t, "checkpoint", {}) or {}
        msgs = (cp.get("channel_values") or {}).get("messages") or []
        if not msgs and not include_empty:
            continue

        mcopy = list(msgs)
        if mask_images:
            try:
                _, _ = _mask_images_inplace(mcopy)
            except Exception as e:
                _warn(f"mask_images fallita sullo snapshot {i}: {e}")

        if dedupe_consecutive:
            fp = _msgs_fingerprint(mcopy)
            if last_fp is not None and fp == last_fp:
                continue
            last_fp = fp

        snapshots.append(mcopy)

    if not snapshots:
        _warn("nessuno snapshot trovato (o tutti filtrati).")
        return [], ""

    # 4) Segmenta per chiamata al modello
    episodes: List[List[BaseMessage]] = []
    seen_ai_ids = set()

    for i, snap in enumerate(snapshots):
        ai_idx = None
        ai_id = None
        for idx, m in enumerate(snap):
            if isinstance(m, AIMessage) and _is_model_ai_only_flag(m):
                mid = getattr(m, "id", None)
                if mid and mid not in seen_ai_ids:
                    ai_idx = idx
                    ai_id = mid
                    break

        if ai_idx is not None:
            episode_msgs = snap[:ai_idx + 1]
            if not episode_msgs and i > 0:
                prev = snapshots[i - 1]
                if prev:
                    episode_msgs = prev + [snap[ai_idx]]
            if episode_msgs or include_empty:
                episodes.append(list(episode_msgs))
            if ai_id:
                seen_ai_ids.add(ai_id)

    # 5) Ordine richiesto
    if order == "newest_first":
        episodes.reverse()

    # md ultimo snapshot (sempre best-effort)
    try:
        md = msg_to_md(snapshots[-1])
    except Exception:
        md = ""

    # Fallback: nessun episodio segmentato → warning ma ritorna snapshot “last” per consentire metriche base
    if not episodes:
        _warn("nessun episodio segmentabile (nessun AIMessage valido). Ritorno fallback con ultimo snapshot.")
        return [snapshots[-1]], md

    return episodes, md


from typing import Any, Dict, List, Optional, Union
import pandas as pd
import yaml
from pathlib import Path

def _count_imgs(msgs:list[BaseMessage]):
    input_imgs=0
    for msg in msgs:
        if isinstance(msg,HumanMessage):
            cont=msg.content
            for part in cont:
                if part["type"]=='image_url':
                    input_imgs+=1

    return input_imgs

def _count_tools(msgs: list[BaseMessage], tools_names: list[str] = _TOOLS):
    tools_count = 0
    per_tools_count = {f"tool{k}": 0 for k in tools_names}

    for m in msgs:
        if isinstance(m, AIMessage) and isinstance(getattr(m, "additional_kwargs", None), dict):
            tool_calls = m.additional_kwargs.get('tool_calls') or []
            if not isinstance(tool_calls, list):
                continue
            tools_count += len(tool_calls)
            for tc in tool_calls:
                try:
                    func = (tc or {}).get("function") or {}
                    name = func.get("name")
                    if name and f"tool{name}" in per_tools_count:
                        per_tools_count[f"tool{name}"] += 1
                except Exception:
                    # best-effort: ignora tool_call malformate
                    pass

    return tools_count, per_tools_count

def _count_timing(msgs: list[BaseMessage]) -> Dict[str, float]:
    total_generation_time = 0.0
    gen_count = 0
    total_tool_time = 0.0
    tool_count = 0

    def _warn_local(what: str):
        # NB: qui non abbiamo il thread_id; il warning di dettaglio viene dato
        #     dal chiamante quando rileva valori mancanti. Manteniamo un messaggio sintetico.
        print(f"[WARN][_count_timing] {what}")

    def _first_present(d: Dict[str, Any], keys: list[str], default=None):
        for k in keys:
            if isinstance(d, dict) and k in d and d[k] is not None:
                return d[k]
        return default

    def _sum_parts(d: Dict[str, Any], parts: list[str]) -> float:
        s, found = 0.0, False
        for k in parts:
            v = d.get(k)
            if isinstance(v, (int, float)):
                s += float(v); found = True
        return s if found else 0.0

    for m in msgs:
        if not isinstance(m, AIMessage):
            continue

        # timing generazione modello (tollerante: timing_ms / timings_ms)
        try:
            rm = getattr(m, "response_metadata", {}) or {}
            tgen = _first_present(rm, ["timing_ms", "timings_ms"], {}) or {}
            if isinstance(tgen, dict) and isinstance(tgen.get("elapsed"), (int, float)):
                total_generation_time += float(tgen["elapsed"])
                gen_count += 1
        except Exception as e:
            _warn_local(f"errore su timing generazione: {e}")

        # timing tool calls (tollerante e best-effort)
        try:
            tool_calls = (m.additional_kwargs or {}).get("tool_calls") or []
            for tc in tool_calls:
                if not isinstance(tc, dict):
                    continue
                ttool = _first_present(tc, ["timings_ms", "timing_ms", "timings", "timing"], {}) or {}
                elapsed = None
                if isinstance(ttool, dict):
                    if isinstance(ttool.get("elapsed"), (int, float)):
                        elapsed = float(ttool["elapsed"])
                    else:
                        # prova a ricostruire somma da componenti note, se esistono
                        elapsed = _sum_parts(ttool, ["queue_ms", "http_ms", "exec_ms", "latency_ms", "duration_ms"])
                if isinstance(elapsed, (int, float)) and elapsed > 0:
                    total_tool_time += elapsed
                    tool_count += 1
                # else: nessun timing disponibile -> ignora
        except Exception as e:
            _warn_local(f"errore su timing tool_calls: {e}")

    return {
        "totalGenerationTime": total_generation_time,
        "avgPerGenerationTime": (total_generation_time / gen_count) if gen_count else 0.0,
        "totalToolTime": total_tool_time,
        "avgPerToolTime": (total_tool_time / tool_count) if tool_count else 0.0,
    }

def get_ckpt_data(msg_ckpts: list[list[BaseMessage]], tools_names: list[str] = _TOOLS):
    # Totali su tutte le call
    input_tokens = output_tokens = total_tokens = input_imgs = 0

    # Solo last
    input_tokens_last = output_tokens_last = total_tokens_last = input_imgs_last = 0
    tools_count_last = 0
    per_tools_count = {k: 0 for k in tools_names}
    timing_last = {"totalGenerationTime": 0, "avgPerGenerationTime": 0.0,
                   "totalToolTime": 0, "avgPerToolTime": 0.0}

    if not msg_ckpts:
        return {
            "inputTokens": 0, "outputTokens": 0, "totalTokens": 0, "inputImages": 0,
            "inputTokensLast": 0, "outputTokensLast": 0, "totalTokensLast": 0, "inputImagesLast": 0,
            "totalCalls": 0, "toolsCount": 0, **{f"{k}": 0 for k in tools_names},
            **timing_last
        }

    for i, ckpt in enumerate(msg_ckpts):
        last_ai: AIMessage = ckpt[-1]
        #TODO capire sta roba rimettere gli assert
        # if not isinstance(last_ai, AIMessage) or not last_ai.additional_kwargs.get("is_developer_injected", False):
        #     print(f"[WARNING] Last message is not AI message for {msg_ckpts}")
        #     continue
      

        # Totali: somma su tutti i checkpoint
        usage = last_ai.response_metadata.get("token_usage", {}) or {}
        input_tokens += int(usage.get("prompt_tokens") or 0)
        output_tokens += int(usage.get("completion_tokens") or 0)
        total_tokens += int(usage.get("total_tokens") or (usage.get("prompt_tokens", 0) + usage.get("completion_tokens", 0)))
        input_imgs += _count_imgs(ckpt)

        # Last: solo per il primo elemento (assumi msg_ckpts ordinati dal più recente al più vecchio)
        if i == 0:
            input_tokens_last = int(usage.get("prompt_tokens") or 0)
            output_tokens_last = int(usage.get("completion_tokens") or 0)
            total_tokens_last = int(usage.get("total_tokens") or (input_tokens_last + output_tokens_last))
            input_imgs_last = _count_imgs(ckpt)

            tools_count_last, per_tools_raw = _count_tools(ckpt, tools_names=tools_names)
            # rinomina in camelCase “tool<Name>”
            per_tools_count = {f"{k}": v for k, v in per_tools_raw.items()}
            try:
                timing_last = _count_timing(ckpt)  # solo last
            except Exception as e:
                raise e

    return {
        # Totali
        "inputTokens": input_tokens,
        "outputTokens": output_tokens,
        "totalTokens": total_tokens,
        "inputImages": input_imgs,

        # Last
        "inputTokensLast": input_tokens_last,
        "outputTokensLast": output_tokens_last,
        "totalTokensLast": total_tokens_last,
        "inputImagesLast": input_imgs_last,

        # Conteggi (solo last per tools/timing)
        "totalCalls": len(msg_ckpts),
        "toolsCount": tools_count_last,
        **per_tools_count,
        **timing_last,
    }

def compute_episode_result(
    episode_def_path: Path,
    episode_res_path: Path,
    episodes_def_yaml_path: Path,   # YAML con taxonomy+episodes
    saver:SqliteSaver,
    delete_db_data:bool=False,
    compute_md:bool=True
) -> pd.DataFrame:
    """
    Costruisce un DF con una riga per ogni ripetizione (status == 'Ended').
    Le metriche sono aggiunte dinamicamente prendendo *tutte* le chiavi ritornate da get_ckpt_data
    (es.: inputTokens, outputTokens, totalTokens, inputImages, inputTokensLast, ..., totalCalls, toolsCount,
     tool<NomeTool>, totalGenerationTime, avgPerGenerationTime, totalToolTime, avgPerToolTime, ...).

    - Nessuna rimappatura: le colonne prendono esattamente i nomi delle chiavi.
    - Le colonne taxonomy sono lette da episodes_def_yaml_path.
    - Le colonne mancanti (perché non prodotte in una riga) vengono create e valorizzate a NA, poi cast soft a numerico.
    """
    import pandas as pd

    df_def = pd.read_csv(episode_def_path, delimiter=";")
    df_res = pd.read_csv(episode_res_path, delimiter=";")

    if len(df_def) != 1:
        raise ValueError(f"Expected exactly 1 row in {episode_def_path}, found {len(df_def)}.")

    if "repetitions" in df_def.columns:
        rep = int(df_def.iloc[0]["repetitions"])
        if len(df_res) != rep:
            print(f"Warning: Inconsistent repetitions={rep} but {episode_res_path} has {len(df_res)} rows.")
        # aggiorna ripetizioni effettive concluse e salva l'atteso
        df_def.loc[0, "repetitions"] = len(df_res[df_res.status == "Ended"])
        df_def.loc[0, "expectedRepetitions"] = rep

    # replica definizione su tutte le righe dei risultati "Ended"
    row_def = df_def.iloc[0].to_dict()
    out = df_res.copy()[df_res.status == "Ended"]
    for k, v in row_def.items():
        out[k] = v

    # === taxonomy dal YAML episodi/spec ===
    tax_levels = get_token_levels_index(str(episodes_def_yaml_path))
    tax_cols: List[str] = []
    seen = set()
    for scene_dict in tax_levels.values():
        for task_dict in scene_dict.values():
            for key in task_dict.keys():
                if key not in seen:
                    seen.add(key)
                    tax_cols.append(key)
    for c in tax_cols:
        out[c] = None

    # === metriche dai checkpoints ===
    metric_keys_seen: set[str] = set()
    md_dict:dict[str,str]={}

    for idx, row in out.iterrows():
        scene_id = row.get("scene") or row.get("sceneId")
        task_id = row.get("taskId")
        tid = row.get("threadId")
        msg_ckpts=None
        metrics = {}
        if pd.notna(tid):
            try:
                msg_ckpts,md  = get_ckpt_messages(str(tid), saver)
                if compute_md:
                    md_dict[tid]=md
                if delete_db_data and compute_md:
                    saver.delete_thread(tid)
            except Exception as e:
                print(row)
                raise e
            if msg_ckpts:
                metrics = get_ckpt_data(msg_ckpts)  # <-- chiavi già camelCase

        # registra chiavi metriche viste e scrivi valori per la riga corrente
        for k, v in (metrics or {}).items():
            metric_keys_seen.add(k)
            if k not in out.columns:
                out[k] = pd.NA
            out.loc[idx, k] = v

        # taxonomy per (sceneId, taskId)
        task_tax = (tax_levels.get(scene_id) or {}).get(task_id) or {}
        if task_tax:
            for c in tax_cols:
                out.loc[idx, c] = task_tax.get(c, None)
       

    # assicura colonne per tutte le metriche viste
    for k in sorted(metric_keys_seen):
        if k not in out.columns:
            out[k] = pd.NA

    # cast soft a numerico sulle metriche
    for c in metric_keys_seen:
        out[c] = pd.to_numeric(out[c], errors="coerce")

    # ordine colonne: definizione → taxonomy → metriche → risultati originali
    def_cols = list(df_def.columns)
    res_cols = [c for c in df_res.columns if c not in df_def.columns]
    metric_cols = [c for c in out.columns if c not in def_cols + res_cols + tax_cols]
    final_cols = def_cols + tax_cols + metric_cols + res_cols
    out = out[final_cols]

    # pinning di alcune colonne
    pinned = [c for c in ["scene", "sceneId", "taskId", "repIndex"] if c in out.columns]
    remaining = [c for c in out.columns if c not in pinned]
    out = out[pinned + remaining].reset_index(drop=True)

    return out,md_dict



def compute_experiment_result(
    exp_folder: Path,
    saver: Optional[SqliteSaver] = None,
    from_csv: bool = False,
    episode_csv_name: str = EPISODE_CSV,
    result_csv_name: str = RESULT_CSV,
    ep_def_yaml: Path = EPISODE_DEF_YAML,
    save: bool = True,
    delete_db_data: bool = False,
    compute_md: bool = True
):
    """
    Produce:
      - out_all: una riga per ripetizione tramite compute_episode_result (colonne metriche dinamiche mantenute)
      - out_agg: una riga per episodio (media + deviazione standard per ogni colonna numerica)

    Allinea le colonne tra episodi diversi (unione colonne, riempie mancanti) 
    e applica cast numerico morbido dove possibile.
    """
    import pandas as pd
    import re
    from tqdm import tqdm

    def _to_episode_id(thread_id: str) -> str:
        if not isinstance(thread_id, str):
            return ""
        # rimuove il suffisso _rep<N> alla fine
        return thread_id.split("_")[0]

    exp_folder = Path(exp_folder)

    # ============================================================
    # ==================   FROM RAW EXPERIMENT   ==================
    # ============================================================
    if not from_csv:
        dfs: List[pd.DataFrame] = []

        for p in tqdm(sorted(exp_folder.iterdir()), desc="Computing results for each episode repetitions"):
            if not p.is_dir():
                continue

            ep_def = p / episode_csv_name
            ep_res = p / result_csv_name
            md_folder = p / Path("md_logs")

            if not (ep_def.exists() and ep_res.exists()):
                continue

            df, md_dict = compute_episode_result(ep_def, ep_res, ep_def_yaml, saver, delete_db_data, compute_md)

            if not df.empty:
                dfs.append(df)

                # Salvataggio MD
                if compute_md:
                    md_folder.mkdir(parents=True, exist_ok=True)
                    for k, v in (md_dict or {}).items():
                        fname = k if k.lower().endswith(".md") else f"{k}.md"
                        (md_folder / fname).write_text(str(v), encoding="utf-8")

        if not dfs:
            return pd.DataFrame(), pd.DataFrame()

        # ============================================================
        # === Unione colonne su tutti i DF (outer union) ============
        # ============================================================
        all_cols = set()
        for df in dfs:
            all_cols.update(df.columns.tolist())
        all_cols = list(all_cols)

        normed = []
        for df in dfs:
            tmp = df.copy()

            # colonne mancanti → NA
            missing = [c for c in all_cols if c not in tmp.columns]
            for c in missing:
                tmp[c] = pd.NA
            tmp = tmp[all_cols]

            # soft numeric cast
            for c in tmp.columns:
                if tmp[c].dtype == object:
                    tmp[c] = pd.to_numeric(tmp[c], errors="ignore")

            normed.append(tmp)

        out_all = pd.concat(normed, ignore_index=True).reset_index(drop=True)

        # ============================================================
        # ============= Aggregato: UNA riga per episodio =============
        # ============================================================
        aggregated_dfs: List[pd.DataFrame] = []

        # ricava episodio base da threadId
        out_all["episodeId"] = out_all["threadId"].apply(lambda x: _to_episode_id(str(x)))

        for ep_id, g in out_all.groupby("episodeId"):
            g = g.copy()

            # colonne numeriche
            num_cols = g.select_dtypes(include=["number"]).columns.tolist()
            if "repIndex" in num_cols:
                num_cols.remove("repIndex")

            # media e std per episodio
            means = g[num_cols].mean(numeric_only=True)
            stds = g[num_cols].std(numeric_only=True)

            # riga aggregata basata sulla prima
            agg_row = g.iloc[0].copy()

            # inserisce media
            for c in num_cols:
                agg_row[c] = means[c]

            # inserisce std come <colname>_std
            for c in num_cols:
                agg_row[f"{c}_std"] = stds[c]

            aggregated_dfs.append(pd.DataFrame([agg_row]))

        out_agg = pd.concat(aggregated_dfs, ignore_index=True).reset_index(drop=True)

        # ============================================================
        # ===================== Salvataggio ==========================
        # ============================================================
        if save:
            out_all.to_csv(Path(exp_folder) / EXP_RUNS_RES, index=False, sep=";")
            out_agg.to_csv(Path(exp_folder) / EXP_RUNS_RES_AGG, index=False, sep=";")

    # ============================================================
    # ===================== FROM CSV MODE ========================
    # ============================================================
    elif from_csv and save:
        raise Exception("You are getting results from csv while save is True: this makes no sense")

    elif from_csv and saver is not None:
        raise Exception("With from_csv=True, set saver=None and save=False")

    else:
        out_all = pd.read_csv(Path(exp_folder) / EXP_RUNS_RES, sep=";")
        out_agg = pd.read_csv(Path(exp_folder) / EXP_RUNS_RES_AGG, sep=";")

    return out_all, out_agg





def plot_aggregated_metrics(
    data: AggInput,
    by: Optional[List[str]] = None,
    metrics: Optional[List[str]] = None,
) -> Tuple[pd.DataFrame, List[Tuple[plt.Figure, plt.Axes]]]:
    """
    Carica df aggregato (o usa df fornito), raggruppa per `by` e plott(a) le metriche richieste.

    Parametri
    ---------
    data : str | Path | pd.DataFrame
        DataFrame aggregato o path al CSV del df aggregato.
    by : list[str] | None
        Colonne per il groupby. Default: ["episodeId"].
        Colonne ammesse: episodeId, scene, taskId, episodeDifficulty,
                         graphicalResolution, graphicalLighting, model,
                         objectIdentifier, coordinatesType.
    metrics : list[str] | None
        Metriche da plottare. Default: tutte:
            ["solutionCheck",
             "inputTokens","outputTokens","totalTokens",
             "generationTimeMs","toolsTimeMs","totalTimeMs",
             "completedTools","failedTools"]

    Ritorna
    -------
    (df_grouped, figs_axes)
        df_grouped: DataFrame con le medie per gruppo delle metriche richieste.
        figs_axes : lista di tuple (fig, ax) dei grafici creati (un grafico per metrica).
    """
    # 1) Carica
    if isinstance(data, (str, Path)):
        df = pd.read_csv(data, sep=";")
    elif isinstance(data, pd.DataFrame):
        df = data.copy()
    else:
        raise TypeError("`data` deve essere un DataFrame o un path a CSV.")

    if df.empty:
        raise ValueError("Il DataFrame è vuoto.")

    # 2) Defaults e validazioni
    by = by or ["episodeId"]
    metrics = metrics or list(_DEFAULT_METRICS)

    missing_by = [c for c in by if c not in df.columns]
    if missing_by:
        raise ValueError(f"Colonne in `by` non presenti nel df: {missing_by}")

    missing_metrics = [m for m in metrics if m not in df.columns]
    if missing_metrics:
        raise ValueError(f"Metriche non presenti nel df: {missing_metrics}")

    # 3) Group & mean su metriche richieste
    grp = df.groupby(by, dropna=False)
    df_grouped = grp[metrics].mean(numeric_only=True).reset_index()

    # 4) Crea una colonna label per asse x, combinando i `by` (se >1)
    if len(by) == 1:
        df_grouped["_label_"] = df_grouped[by[0]].astype(str)
    else:
        df_grouped["_label_"] = df_grouped[by].astype(str).agg(" | ".join, axis=1)

    # 5) Plot: una figura per metrica (bar chart semplice)
    figs_axes: List[Tuple[plt.Figure, plt.Axes]] = []
    x_labels = df_grouped["_label_"].tolist()
    x_pos = list(range(len(x_labels)))

    for met in metrics:
        y = df_grouped[met].tolist()

        fig, ax = plt.subplots(figsize=(max(6, min(16, 0.4 * len(x_labels) + 4)), 4.8))
        ax.bar(x_pos, y)
        ax.set_title(f"{met} per {' + '.join(by)}")
        ax.set_xlabel(" / ".join(by))
        ax.set_ylabel(met)

        # etichette sull'asse X verticali
        ax.set_xticks(x_pos)
        ax.set_xticklabels(x_labels, rotation=45, ha="center", va="top")
        ax.tick_params(axis="x", labelrotation=45)  

        ax.grid(True, axis="y", linestyle="--", alpha=0.4)
        fig.tight_layout()

        figs_axes.append((fig, ax))

    # 6) Output
    return df_grouped.drop(columns=["_label_"]), figs_axes



################Taxonomy helpers




def get_token_levels_index(
    episodes_or_yaml: Union[Path, str, Dict[str, Any]]
) -> Dict[str, Dict[str, Dict[str, Optional[int]]]]:
    """
    Output:
      { scene_id: { task_id: { <token> | taxAct | taxRef | taxRel | tax : Optional[int]/int } } }

    Regole:
    - Universo token da taxonomy_spec/axonomy_spec.levels.tokens[act|ref|rel], tutti inizializzati a None.
    - Se un token è presente nel task:
        * level == 0  -> valore 0 (presenza esplicita a costo zero)
        * level > 0   -> somma dei 'level' dichiarati (se ripetuto).
    - Totali per dimensione:
        * taxAct / taxRef / taxRel = None se NESSUN token di quella dimensione è presente,
          altrimenti somma dei livelli (gli 0 restano 0).
    - tax = somma di (taxAct, taxRef, taxRel) trattando i None come 0.
    """
    # Carica input
    if isinstance(episodes_or_yaml, (str, Path)):
        with open(episodes_or_yaml, "r", encoding="utf-8") as fp:
            doc = yaml.safe_load(fp)
    else:
        doc = episodes_or_yaml

    # Spec (supporta refuso "axonomy_spec")
    spec_root = doc.get("taxonomy_spec") or doc.get("axonomy_spec") or {}
    spec_tokens = (spec_root.get("levels") or {}).get("tokens") or {}

    dims = ("act", "ref", "rel")
    dim_to_tokens: Dict[str, list[str]] = {d: list((spec_tokens.get(d, {}) or {}).keys()) for d in dims}
    all_tokens: list[str] = [t for d in dims for t in dim_to_tokens[d]]

    out: Dict[str, Dict[str, Dict[str, Optional[int]]]] = {}
    scenes = (doc.get("episodes") or {}).get("scenes", []) or []

    for scene in scenes:
        sid = scene.get("id")
        if not sid:
            continue
        out[sid] = {}

        for task in scene.get("tasks", []) or []:
            tid = task.get("id")
            if not tid:
                continue

            # inizializza
            row: Dict[str, Optional[int]] = {tok: None for tok in all_tokens}

            taxonomy = task.get("taxonomy", {}) or {}
            # popola livelli token
            for d in dims:
                for t in (taxonomy.get(d, {}) or {}).get("tokens", []) or []:
                    tok = t.get("token")
                    if not tok:
                        continue
                    lvl = int(t.get("level", 0))
                    if row.get(tok) is None:
                        row[tok] = lvl
                    else:
                        row[tok] = (row[tok] or 0) + lvl

            # totali per dimensione: None se nessun token presente, altrimenti somma
            dim_totals: Dict[str, Optional[int]] = {}
            for d in dims:
                tokens_d = dim_to_tokens.get(d, []) or []
                any_present = any(row[tok] is not None for tok in tokens_d)
                if not any_present:
                    dim_totals[d] = None
                else:
                    dim_totals[d] = sum((row[tok] or 0) for tok in tokens_d)

            # assegna taxAct/Ref/Rel
            row["taxAct"] = dim_totals["act"]
            row["taxRef"] = dim_totals["ref"]
            row["taxRel"] = dim_totals["rel"]

            # totale complessivo: somma trattando None come 0
            row["tax"] = sum(v or 0 for v in (row["taxAct"], row["taxRef"], row["taxRel"]))

            out[sid][tid] = row

    return out


# pip install plotly pyyaml pandas ipywidgets
from typing import Dict, Any, Union, Optional, Tuple, List
import textwrap, yaml, pandas as pd
import plotly.graph_objects as go
import ipywidgets as W

# ---------------- utils ----------------
def _load_episodes(episodes_or_yaml: Union[str, Dict[str, Any]]) -> Dict[str, Any]:
    if isinstance(episodes_or_yaml, dict):
        return episodes_or_yaml
    try:
        with open(episodes_or_yaml, "r", encoding="utf-8") as fp:
            return yaml.safe_load(fp)
    except FileNotFoundError:
        return yaml.safe_load(episodes_or_yaml)

def _wrap_html(s: Any, w=90, max_len=1500) -> str:
    return "<br>".join(textwrap.wrap(str(s), width=w))[:max_len]

def _collect_token_breakdown(dim_obj: Dict[str, Any]) -> Dict[str, int]:
    """
    Aggregate token levels for a dimension (act/ref/rel).
    IMPORTANT: keep tokens with level == 0 (present but zero-cost),
    so they appear in the details panel.
    """
    out: Dict[str, int] = {}
    for t in (dim_obj or {}).get("tokens", []) or []:
        tok = t.get("token")
        lvl = int(t.get("level", 0))
        if not tok:
            continue
        out[tok] = out.get(tok, 0) + lvl  # keep 0 too
    return out

def _fmt_dim_details(name: str, dim_level: int, tok_map: Dict[str, int]) -> str:
    """
    Pretty print for a dimension; list tokens even if dim_level == 0
    (percentages computed safely).
    """
    if not tok_map:  # truly no tokens declared
        return f"<b>{name}</b>: {dim_level} (no tokens)"
    items = sorted(tok_map.items(), key=lambda kv: kv[1], reverse=True)
    lines = [f"<b>{name}</b>: {dim_level}"]
    for tok, lvl in items:
        pct = (lvl * 100.0 / dim_level) if dim_level > 0 else 0.0
        lines.append(f"&nbsp;&nbsp;• {tok}: {lvl} ({pct:.0f}%)")
    return "<br>".join(lines)

# --------------- main ------------------
def plot_taxonomy_difficulty(
    episodes_or_yaml: Union[str, Dict[str, Any]],
    title: str = "Task difficulty in taxonomy space",
    color_by: str = "tax",            # color by total difficulty
    size_by: str = "taxRel",          # marker size ~ REL
    size_max: int = 22,
    axis_padding: float = 0.05,       # % padding on axis maxima
    panel_width: int = 420,           # right panel width
    fig_width: int = 750,             # figure width
    fig_height: int = 520,            # figure/panel height
    save_html: Optional[str] = None,  # save only the figure (no right panel)
    show: bool = True,
):
    """
    Returns (df, ui) where `ui` is an HBox: [3D Figure | Details Panel].
    Axes start at 0 so (0,0,0) is the common origin.
    """
    doc = _load_episodes(episodes_or_yaml)
    scenes = (doc.get("episodes") or {}).get("scenes", []) or []

    rows: List[Dict[str, Any]] = []
    breakdowns: List[Dict[str, Any]] = []

    for scene in scenes:
        scene_id = scene.get("id")
        for task in scene.get("tasks", []) or []:
            tax = task.get("taxonomy", {}) or {}
            act = tax.get("act", {}) or {}
            ref = tax.get("ref", {}) or {}
            rel = tax.get("rel", {}) or {}

            a = int(act.get("level", 0))
            r = int(ref.get("level", 0))
            l = int(rel.get("level", 0))
            total = int(tax.get("level", a + r + l))

            rows.append({
                "sceneId": scene_id,
                "taskId": task.get("id"),
                "prompt": task.get("prompt"),
                "taxAct": a, "taxRef": r, "taxRel": l, "tax": total,
            })
            breakdowns.append({
                "act_tokens": _collect_token_breakdown(act),
                "ref_tokens": _collect_token_breakdown(ref),   # keeps zero-level tokens
                "rel_tokens": _collect_token_breakdown(rel),
            })

    if not rows:
        raise ValueError("No tasks found in episodes.")

    df = pd.DataFrame(rows)

    # Axis ranges: common origin (0,0,0)
    def _rng(col):
        mx = max(1, int(df[col].max()))
        return [0, max(1, int(mx * (1 + axis_padding)))]
    x_range, y_range, z_range = _rng("taxAct"), _rng("taxRef"), _rng("taxRel")

    # Hover text (English; no distance)
    hover_text = (
        "sceneId: " + df["sceneId"].astype(str) +
        "<br>taskId: " + df["taskId"].astype(str) +
        "<br>ACT,REF,REL: (" + df["taxAct"].astype(str) + ", " + df["taxRef"].astype(str) + ", " + df["taxRel"].astype(str) + ")" +
        "<br>Total: " + df["tax"].astype(str) +
        "<br><br><b>Click for details</b>"
    )

    # 3D FigureWidget (click -> details)
    marker_sizes = ((df[size_by] - df[size_by].min()).fillna(0) + 1) * (size_max / 5)
    fig = go.FigureWidget(
        data=[go.Scatter3d(
            x=df["taxAct"], y=df["taxRef"], z=df["taxRel"],
            mode="markers",
            marker=dict(size=marker_sizes, color=df[color_by],
                        colorscale="Viridis", showscale=True, opacity=0.9),
            customdata=pd.concat([
                df[["sceneId","taskId","taxAct","taxRef","taxRel","tax","prompt"]].reset_index(drop=True),
                pd.Series([b["act_tokens"] for b in breakdowns], name="act_tokens"),
                pd.Series([b["ref_tokens"] for b in breakdowns], name="ref_tokens"),
                pd.Series([b["rel_tokens"] for b in breakdowns], name="rel_tokens"),
            ], axis=1).values,
            text=hover_text, hovertemplate="%{text}", name="tasks",
        )]
    )
    fig.update_layout(
        title=title,
        width=fig_width, height=fig_height,
        scene=dict(
            xaxis=dict(title="ACT level", autorange=False, range=x_range, tick0=0, zeroline=True),
            yaxis=dict(title="REF level", autorange=False, range=y_range, tick0=0, zeroline=True),
            zaxis=dict(title="REL level", autorange=False, range=z_range, tick0=0, zeroline=True),
            camera=dict(eye=dict(x=1.6, y=1.6, z=0.8)),
        ),
        margin=dict(l=0, r=0, t=50, b=0),
    )

    # Right-side details panel
    details = go.FigureWidget()
    details.add_annotation(
        x=0, y=1, xanchor="left", yanchor="top", align="left",
        text="Click a point to view details (token breakdown by dimension).",
        showarrow=False
    )
    details.update_layout(
        width=panel_width, height=fig_height,
        margin=dict(l=10, r=10, t=10, b=10),
        xaxis=dict(visible=False), yaxis=dict(visible=False),
    )

    def _format_details(d):
        scene_id, task_id = d[0], d[1]
        a, r, l = int(d[2]), int(d[3]), int(d[4])
        tot = int(d[5])
        prompt = _wrap_html(d[6], w=54)
        act_toks, ref_toks, rel_toks = d[7], d[8], d[9]
        breakdown_html = "<br>".join([
            _fmt_dim_details("ACT", a, act_toks),
            _fmt_dim_details("REF", r, ref_toks),  # will show tokens with 0 too
            _fmt_dim_details("REL", l, rel_toks),
        ])
        return (
            f"<b>sceneId</b>: {scene_id}<br>"
            f"<b>taskId</b>: {task_id}<br>"
            f"<b>ACT,REF,REL</b>: ({a}, {r}, {l})<br>"
            f"<b>Total</b>: {tot}<br>"
            f"{breakdown_html}<br>"
            f"<b>Prompt</b>: {prompt}"
        )

    scatter = fig.data[0]
    @scatter.on_click
    def _on_click(trace, points, selector):
        if not points.point_inds:
            return
        i = points.point_inds[0]
        d = scatter.customdata[i]
        details.layout.annotations[0].update(text=_format_details(d))

    ui = W.HBox(
        [fig, details],
        layout=W.Layout(width=f"{fig_width+panel_width+30}px")
    )

    if save_html:
        fig.write_html(save_html, include_plotlyjs="cdn", full_html=True)
    if show:
        from IPython.display import display
        display(ui)

    return df, ui


# -------------------------
# Helpers
# -------------------------
def _load_df(data_or_path, sep_fallback=';'):
    if isinstance(data_or_path, pd.DataFrame):
        return data_or_path.copy()
    if isinstance(data_or_path, str):
        try:
            return pd.read_csv(data_or_path)
        except Exception:
            return pd.read_csv(data_or_path, sep=sep_fallback)
    raise TypeError("Input must be a pandas DataFrame or a path to a CSV file.")


def _ensure_columns(df, required):
    missing = set(required) - set(df.columns)
    if missing:
        raise ValueError(f"Missing required columns: {missing}. Available columns: {list(df.columns)}")


# ============================================================
# 1) plot_completion_rate_by_episode  (generalizzato con 'by')
# ============================================================
def plot_completion_rate_by_episode(
    data_or_path,
    by=("model",),                  # colonne per definire i gruppi (N barre per episodio = N combinazioni uniche)
    episode_col="taskId",
    metric_col="solutionCheck",
    tax_col="tax",
    agg="mean",
    figsize=(14, 7),
    title="Completion rate × episode × group",
    # --- Nuove opzioni ---
    annot=True,                     # mostrare il valore sopra ogni barra
    annot_fontsize=9,               # dimensione del testo delle etichette sopra le barre
    annot_fmt="{:.2f}",             # formato etichette barre
    legend_loc="best",              # posizione leggenda (es. 'upper right', 'best', ecc.)
    legend_ncol=1,                  # colonne della leggenda
    legend_frame=False,             # bordo del riquadro leggenda
    by_name_map=None,               # mapping nomi colonne -> etichette leggenda (es. {"model": "Model", "scene": "Scene"})
    xtick_rotation=45,              # rotazione etichette asse X
    xtick_ha="right",               # allineamento orizzontale etichette asse X
):
    """
    Crea un grafico a barre raggruppate del valore di `metric_col` per episodio, con serie definite da `by`.
    Gli episodi vengono ordinati per livello C/D (`tax_col`) e poi alfabeticamente per `episode_col`.

    Parametri
    ---------
    data_or_path : pandas.DataFrame o str
        Dati in memoria o path a un CSV.
    by : tuple/list di str, default=("model",)
        Colonne che definiscono i gruppi: ogni combinazione unica genera una serie distinta (N barre per episodio).
    episode_col : str, default="taskId"
        Nome della colonna che identifica l'episodio (asse X).
    metric_col : str, default="solutionCheck"
        Nome della colonna metrica da plottare (altezza barre). Tipicamente completion rate.
    tax_col : str, default="tax"
        Colonna con il livello C/D usata per l'ordinamento degli episodi.
    agg : {"mean","median","sum"} o callable, default="mean"
        Funzione di aggregazione per combinare più righe con stesso episodio×gruppo.
    figsize : tuple, default=(14, 7)
        Dimensioni della figura matplotlib (width, height).
    title : str, default="Completion rate × episode × group"
        Titolo del grafico.

    Opzioni di formattazione
    ------------------------
    annot : bool, default=True
        Se True, stampa un'etichetta numerica sopra ogni barra.
    annot_fontsize : int, default=9
        Dimensione del testo delle etichette sopra le barre.
    annot_fmt : str, default="{:.2f}"
        Formato delle etichette (es. 2 decimali).
    legend_loc : str, default="best"
        Posizione della leggenda (valori matplotlib: 'best', 'upper right', 'lower left', ecc.).
    legend_ncol : int, default=1
        Numero di colonne nella leggenda.
    legend_frame : bool, default=False
        Se True disegna il riquadro della leggenda.
    by_name_map : dict o None, default=None
        Mapping dai nomi delle colonne di `by` alle etichette mostrate nel grafico/leggenda.
        Esempio: {"model": "Model", "scene": "Scene"}.
        Se None, usa i nomi originali delle colonne.
    xtick_rotation : int/float, default=45
        Rotazione delle etichette dell'asse X.
    xtick_ha : str, default="right"
        Allineamento orizzontale delle etichette dell'asse X (es. "right", "center").

    Note
    ----
    - Etichette episodio: "<taskId> (C/D Lev.=<tax>)".
    - Il titolo della leggenda è la join delle etichette di `by` con " × ".
    - N serie = numero di combinazioni uniche nelle colonne `by`.

    Ritorna
    -------
    pandas.DataFrame
        Tabella pivot utilizzata per il grafico (righe=episodi, colonne=gruppi).
    """
    df = _load_df(data_or_path)
    _ensure_columns(df, {episode_col, metric_col, tax_col, *by})

    # cast e ordinamento
    df[tax_col] = pd.to_numeric(df[tax_col], errors="coerce")
    df = df.sort_values([tax_col, episode_col], ascending=[True, True])

    # etichetta episodio ordinata
    df["episode_label"] = df[episode_col].astype(str) + " (C/D Lev.=" + df[tax_col].astype(str) + ")"
    ordered_labels = df["episode_label"].unique().tolist()
    df["episode_label"] = pd.Categorical(df["episode_label"], categories=ordered_labels, ordered=True)

    # mapping "bello" dei nomi delle colonne per la leggenda
    by_name_map = by_name_map or {}
    display_by = [by_name_map.get(col, col) for col in by]

    # etichetta gruppo: "col=val | col2=val2 ..."
    def _group_label(row):
        parts = [f"{by_name_map.get(col, col)}={row[col]}" for col in by]
        return " | ".join(parts)

    df["_group"] = df.apply(_group_label, axis=1)

    # aggregazione
    if isinstance(agg, str):
        pivot_df = (
            df.pivot_table(index="episode_label", columns="_group", values=metric_col, aggfunc=agg)
              .reindex(ordered_labels)
        )
    else:
        pivot_df = (
            df.groupby(["episode_label", "_group"], sort=False)[metric_col]
              .apply(agg)
              .unstack("_group")
              .reindex(ordered_labels)
        )

    pivot_df = pivot_df.fillna(0.0)

    # plotting
    groups = pivot_df.columns.tolist()
    x = np.arange(len(pivot_df.index))
    width = 0.8 / max(1, len(groups))

    fig, ax = plt.subplots(figsize=figsize)
    for i, g in enumerate(groups):
        bars = ax.bar(x + i * width, pivot_df[g].values, width, label=g)
        if annot:
            for bar in bars:
                h = bar.get_height()
                ax.annotate(
                    annot_fmt.format(h),
                    xy=(bar.get_x() + bar.get_width()/2, h),
                    xytext=(0, 3),
                    textcoords="offset points",
                    ha="center", va="bottom",
                    fontsize=annot_fontsize
                )

    ax.set_xlabel("Episode (ordered by C/D Level)")
    ax.set_ylabel(metric_col.replace("_", " ").title())
    ax.set_title(title)
    ax.set_xticks(x + width * (len(groups) - 1) / 2)
    ax.set_xticklabels(pivot_df.index, rotation=xtick_rotation, ha=xtick_ha)

    # Titolo leggenda = join dei nomi (rimappati) in by
    legend_title = " × ".join(display_by)
    leg = ax.legend(title=legend_title, loc=legend_loc, ncol=legend_ncol, frameon=legend_frame)
    if leg is not None and leg.get_title() is not None:
        # allinea meglio il titolo se più colonne
        leg.get_title().set_multialignment('center')

    ax.grid(axis="y", linestyle="--", alpha=0.7)
    plt.tight_layout()
    plt.show()

    return pivot_df


# ========================================
# 2) summarize_performance (generalizzato)
# ========================================
def summarize_performance(
    data_or_path,
    by=("model",),
    metrics=tuple(_DEFAULT_METRICS),
    renames=None,
    agg="mean",
    sep_fallback=";",
    round_ndigits=3,
):
    """
    Produce una tabella aggregata per 'by' con statistiche sulle colonne in 'metrics'.

    Parametri
    ---------
    data_or_path : DataFrame o str
    by : tuple/list di str
        Colonne di raggruppamento (es. ("model",) o ("model","scene")).
    metrics : iterable di str
        Colonne numeriche da aggregare (presenti nei dati).
    renames : dict or None
        Mappa per rinominare le colonne output. Se None, usare default leggibili.
    agg : {"mean","median","sum"} o callable
        Funzione di aggregazione.
    sep_fallback : str
        Separatore alternativo per CSV.
    round_ndigits : int
        Numero di decimali per il rounding.

    Ritorna
    -------
    DataFrame aggregato.
    """
    df = _load_df(data_or_path, sep_fallback=sep_fallback)

    # Normalizza metrics a lista (così possiamo eventualmente aggiungere colonne)
    metrics = list(metrics)

    needed = set(by) | set(metrics)
    _ensure_columns(df, needed)

    # --------------------------------------------------------
    # Nuova metrica: tempo di generazione medio per singola model call
    # genTimePerCall = totalGenerationTime / totalCalls
    # --------------------------------------------------------
    if "totalGenerationTime" in df.columns and "totalCalls" in df.columns:
        calls_nonzero = df["totalCalls"].replace(0, np.nan)
        df["genTimePerCall"] = df["totalGenerationTime"] / calls_nonzero

        if "genTimePerCall" not in metrics:
            metrics.append("genTimePerCall")

    # aggregazione
    if isinstance(agg, str):
        agg_df = df.groupby(list(by))[metrics].agg(agg).reset_index()
    else:
        agg_df = df.groupby(list(by))[metrics].apply(lambda g: g.aggregate(agg)).reset_index()

    # rinomina
    if renames is None:
        default_names = {
            "scene": "Scene",
            "model": "Model",
            "objectIdentifier": "Labeling scheme",

            # outcomes
            "solutionCheck": "Avg (reps) – Completion rate",

            # model calls / tokens
            "totalCalls": "Avg (reps) – #Model calls",

            "totalTokens": "Avg (reps) – Token usage (total)",
            "totalTokensLast": "Avg (reps) – Token usage (last turn)",

            # images
            "inputImages": "Avg (reps) – #Input images (total)",
            "inputImagesLast": "Avg (reps) – #Input images (last turn)",

            # generation time
            "totalGenerationTime": "Avg (reps) – Tot generation time (ms)",
            "avgPerGenerationTime": "Avg (reps) – Per-turn avg generation time (ms)",
            "genTimePerCall": "Avg (reps) – Generation time per model call (ms)",

            # tool time
            "totalToolTime": "Avg (reps) – Tot tool time (ms)",
            "avgPerToolTime": "Avg (reps) – Per-turn avg tool time (ms)",

            # tools count (from last turn)
            "toolsCount": "Avg (reps) – #Tool calls (last turn)",

            # per-tool counts (last turn)
            "toolWalk": "Avg (reps) – #Walk (last turn)",
            "toolLook": "Avg (reps) – #Look (last turn)",
            "toolPickObject": "Avg (reps) – #Pick (last turn)",
            "toolDropObject": "Avg (reps) – #Drop (last turn)",
        }
        renames = {k: v for k, v in default_names.items() if k in agg_df.columns}

    agg_df = agg_df.rename(columns=renames)
    agg_df = agg_df[list(renames.values())]

    # arrotonda solo numeriche
    num_cols = agg_df.select_dtypes(include="number").columns
    agg_df[num_cols] = agg_df[num_cols].round(round_ndigits)

    return agg_df



# ---------- utilities ----------
def _load_df(data_or_path, sep_fallback=';'):
    if isinstance(data_or_path, pd.DataFrame):
        return data_or_path.copy()
    if isinstance(data_or_path, str):
        try:
            return pd.read_csv(data_or_path)
        except Exception:
            return pd.read_csv(data_or_path, sep=sep_fallback)
    raise TypeError("Input must be a pandas DataFrame or a path to a CSV file.")

def _ensure_columns(df, required):
    missing = set(required) - set(df.columns)
    if missing:
        raise ValueError(f"Missing required columns: {missing}. Available: {list(df.columns)}")

def _auto_color_lut(labels, cmap_name="tab10"):
    cmap = plt.get_cmap(cmap_name)
    colors = cmap(np.linspace(0, 1, max(1, len(labels))))
    return {lab: colors[i] for i, lab in enumerate(labels)}


# -------------------------
# Utilities di supporto
# -------------------------
def _load_df(data_or_path, sep_fallback=';'):
    """
    Carica i dati da DataFrame o da CSV.

    Parametri
    ----------
    data_or_path : pandas.DataFrame | str
        DataFrame già in memoria oppure percorso a un file CSV.
    sep_fallback : str, default=';'
        Separatore alternativo da provare se la prima lettura fallisce.

    Ritorna
    -------
    pandas.DataFrame
    """
    if isinstance(data_or_path, pd.DataFrame):
        return data_or_path.copy()
    if isinstance(data_or_path, str):
        try:
            return pd.read_csv(data_or_path)
        except Exception:
            return pd.read_csv(data_or_path, sep=sep_fallback)
    raise TypeError("Input must be a pandas DataFrame or a path to a CSV file.")


def _ensure_columns(df, required):
    """
    Verifica che le colonne richieste siano presenti.
    """
    missing = set(required) - set(df.columns)
    if missing:
        raise ValueError(f"Missing required columns: {missing}. Available: {list(df.columns)}")


def _auto_color_lut(labels, cmap_name="tab10"):
    """
    Genera una mappa etichetta->colore usando una colormap matplotlib.
    """
    cmap = plt.get_cmap(cmap_name)
    colors = cmap(np.linspace(0, 1, max(1, len(labels))))
    return {lab: colors[i] for i, lab in enumerate(labels)}


def _normalize_key(s: str) -> str:
    """
    Normalizza una chiave per il confronto con style_map:
    - converte in stringa
    - rimuove tutti gli spazi
    """
    return str(s).replace(" ", "")


# ============================================================
# 1) plot_completion_rate_by_episode
# ============================================================
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


def plot_completion_rate_by_episode(
    data_or_path,
    by=("model",),
    episode_col="taskId",
    metric_col="solutionCheck",
    tax_col="tax",
    agg="mean",
    figsize=(14, 7),
    title="Completion rate × episode × group",
    *,
    # formattazione base
    annot=True,
    annot_fontsize=9,
    annot_fmt="{:.2f}",
    legend_loc="best",
    legend_ncol=1,
    legend_frame=False,
    xtick_rotation=45,
    xtick_ha="right",
    edgecolor="black",
    # etichette/naming
    by_name_map=None,       # es. {"objectIdentifier": "Labeling scheme"}
    group_sep=" | ",        # separatore tra i valori che compongono l'etichetta del gruppo
    # styling esplicito per gruppo
    style_map=None,         # dict: {"G4O": ("blue", None), "G4O|SEM": ("blue","//"), ...}
    cmap="tab10",           # fallback per gruppi non mappati
    # layout gruppi di barre
    group_width: float = 0.8,          # larghezza totale occupata dal gruppo
    intra_group_spacing: float = 0.0,  # spazio tra barre nello stesso gruppo (unità asse X)
    # std pre-calcolata
    show_std: bool = False,            # se True disegna i candelotti
    std_col: str | None = None,        # es. "solutionCheck_std"
    errorbar_capsize: float = 4.0,
    errorbar_linewidth: float = 1.0,
    errorbar_color: str | None = None,
    # font & colori
    font_color: str = "black",
    title_fontsize: int = 14,
    xlabel_fontsize: int = 12,
    ylabel_fontsize: int = 12,
    xtick_labelsize: int = 10,
    ytick_labelsize: int = 10,
    # offset annotazioni (in punti), alternati globalmente
    annot_offset_even: float = 0.0,    # usato per la 1a, 3a, 5a etichetta, ...
    annot_offset_odd: float = 0.0,     # usato per la 2a, 4a, 6a etichetta, ...
):
    """
    Disegna un grafico a barre raggruppate di `metric_col` per episodio; ogni serie corrisponde
    a una combinazione di valori nelle colonne `by`.

    Etichetta di gruppo = concatenazione dei soli valori delle colonne in `by`, separati da `group_sep`.
      Esempi:
        by=("model",)                   -> "G4O"
        by=("model","objectIdentifier") -> "G4O | OPAQ"

    Parametri principali
    --------------------
    data_or_path : pandas.DataFrame | str
        Dati in memoria o percorso di un CSV.
    by : tuple[str] | list[str], default=("model",)
        Colonne che definiscono i gruppi (ogni combinazione unica → una serie).
    episode_col : str, default="taskId"
        Colonna che identifica l'episodio (asse X).
    metric_col : str, default="solutionCheck"
        Metrica da plottare (altezza delle barre).
    tax_col : str, default="tax"
        Colonna per l’ordinamento degli episodi (livello C/D).
    agg : {"mean","median","sum"} | callable, default="mean"
        Funzione di aggregazione quando esistono più righe per episodio×gruppo.
    figsize : tuple[float, float], default=(14, 7)
        Dimensioni della figura matplotlib.
    title : str, default="Completion rate × episode × group"
        Titolo del grafico.

    Annotazioni
    -----------
    annot : bool, default=True
        Se True, stampa le annotazioni sopra le barre.
        Logica:
        - per ciascun episodio (posizione X), se più barre hanno valore 1.0, viene annotata
          solo la prima barra che ha 1.0, le altre con 1.0 vengono ignorate;
        - per gli altri valori vengono annotate tutte le barre.
        - le annotazioni visibili sono sfalsate globalmente: la 1a usa `annot_offset_even`,
          la 2a `annot_offset_odd`, la 3a di nuovo `annot_offset_even`, ecc.
    annot_fontsize : int, default=9
        Dimensione del testo delle annotazioni.
    annot_fmt : str, default="{:.2f}"
        Formato stringa per il valore annotato.
    annot_offset_even : float, default=0.0
        Offset verticale (in punti) applicato alle annotazioni in posizione globale pari
        (1a, 3a, 5a, ...). Positivo = più in alto, negativo = più in basso rispetto alla
        cima della barra.
    annot_offset_odd : float, default=0.0
        Offset verticale (in punti) applicato alle annotazioni in posizione globale dispari
        (2a, 4a, 6a, ...). Stessa semantica del precedente.

    (Il resto dei parametri è come nelle versioni precedenti.)
    """
    df = _load_df(data_or_path)
    _ensure_columns(df, {episode_col, metric_col, tax_col, *by})

    # Se vogliamo std, controlliamo che std_col esista
    if show_std:
        if std_col is None:
            print("[plot_completion_rate_by_episode] show_std=True ma std_col=None: nessuna error bar verrà plottata.")
            show_std = False
        elif std_col not in df.columns:
            raise ValueError(f"std_col='{std_col}' non trovato nel dataframe.")

    # Ordina episodi per (tax, episode)
    df[tax_col] = pd.to_numeric(df[tax_col], errors="coerce")
    df = df.sort_values([tax_col, episode_col], ascending=[True, True])

    # Etichetta episodio ordinata
    df["episode_label"] = df[episode_col].astype(str)
    ordered = df["episode_label"].unique().tolist()
    df["episode_label"] = pd.Categorical(df["episode_label"], categories=ordered, ordered=True)

    # Etichetta gruppo = concatenazione valori (con separatore configurabile)
    def _group_label(row):
        return group_sep.join([str(row[col]) for col in by])

    df["_group"] = df.apply(_group_label, axis=1)

    # ======================
    # Aggregazione (metriche)
    # ======================
    if isinstance(agg, str):
        pivot_df = (
            df.pivot_table(index="episode_label", columns="_group", values=metric_col, aggfunc=agg)
              .reindex(ordered)
        )
    else:
        pivot_df = (
            df.groupby(["episode_label", "_group"], sort=False)[metric_col]
              .apply(agg)
              .unstack("_group")
              .reindex(ordered)
        )
    pivot_df = pivot_df.fillna(0.0)

    # ======================
    # Pivot della std pre-calcolata (se richiesto)
    # ======================
    std_pivot = None
    if show_std:
        std_pivot = (
            df.pivot_table(index="episode_label", columns="_group", values=std_col, aggfunc="mean")
              .reindex(ordered)
        )
        std_pivot = std_pivot.fillna(0.0)

    # Stile per gruppi
    groups = pivot_df.columns.tolist()
    auto_colors = _auto_color_lut(groups, cmap_name=cmap)

    # Prepara style_map normalizzato (chiavi senza spazi)
    style_norm = { _normalize_key(k): v for k, v in (style_map or {}).items() }

    # Avviso per gruppi non mappati
    unmapped = [g for g in groups if _normalize_key(g) not in style_norm]
    if style_map and unmapped:
        print(
            "[plot_completion_rate_by_episode] Avviso: "
            f"{len(unmapped)} gruppi non presenti in style_map "
            f"(useranno colori automatici): {unmapped}"
        )

    def _style_for(label):
        key = _normalize_key(label)
        if key in style_norm:
            c, h = style_norm[key]
            return (c if c is not None else auto_colors[label]), (h or "")
        return auto_colors[label], ""

    # ======================
    # Layout gruppi
    # ======================
    x = np.arange(len(pivot_df.index))
    n_groups = max(1, len(groups))

    if n_groups == 1:
        bar_width = group_width
        offsets = [0.0]
    else:
        total_gap = intra_group_spacing * (n_groups - 1)
        if total_gap >= group_width:
            raise ValueError(
                f"intra_group_spacing troppo grande: "
                f"group_width={group_width}, spacing={intra_group_spacing}, n_groups={n_groups}"
            )
        bar_width = (group_width - total_gap) / n_groups
        offsets = [
            -group_width / 2 + bar_width / 2 + i * (bar_width + intra_group_spacing)
            for i in range(n_groups)
        ]

    fig, ax = plt.subplots(figsize=figsize)
    eb_color = errorbar_color if errorbar_color is not None else edgecolor

    # per ciascun episodio (posizione x), teniamo traccia se abbiamo già annotato un "1"
    one_already_labeled = [False] * len(x)

    # contatore globale di annotazioni effettivamente disegnate (per alternare even/odd)
    annot_counter = 0

    # ======================
    # Plot
    # ======================
    for i, g in enumerate(groups):
        color, hatch = _style_for(g)

        yerr = None
        if show_std and std_pivot is not None and g in std_pivot.columns:
            yerr = std_pivot[g].values

        values = pivot_df[g].values

        bars = ax.bar(
            x + offsets[i],
            values,
            bar_width,
            label=g,
            color=color,
            edgecolor=edgecolor,
            yerr=yerr,
            capsize=errorbar_capsize if yerr is not None else 0,
            ecolor=eb_color,
            linewidth=errorbar_linewidth,
        )

        if hatch:
            for b in bars:
                b.set_hatch(hatch)

        if annot:
            for idx, b in enumerate(bars):
                val = b.get_height()

                # se il valore è ~1.0 e abbiamo già etichettato un 1 in quel task, salta
                if np.isclose(val, 1.0, atol=1e-6):
                    if one_already_labeled[idx]:
                        continue
                    one_already_labeled[idx] = True  # prima volta che vediamo un 1 per questo episodio

                # scegli offset in base al numero globale di annotazioni già disegnate
                if annot_counter % 2 == 0:
                    dy = annot_offset_even
                else:
                    dy = annot_offset_odd

                ax.annotate(
                    annot_fmt.format(val),
                    xy=(b.get_x() + b.get_width() / 2, val),
                    xytext=(0, dy),
                    textcoords="offset points",
                    ha="center",
                    va="bottom",  # sempre "partenza" dalla cima barra, poi dy può essere ±
                    fontsize=annot_fontsize,
                    color=font_color,
                )

                annot_counter += 1

    # Titolo legenda con rename colonne
    legend_title = " × ".join([by_name_map.get(c, c) for c in by]) if by_name_map else " × ".join(by)

    ax.set_xlabel("Tasks (ordered by C/D Level)", fontsize=xlabel_fontsize, color=font_color)
    ax.set_ylabel("Completion Rate", fontsize=ylabel_fontsize, color=font_color)
    ax.set_title(title, fontsize=title_fontsize, color=font_color)

    # xticks centrati sul gruppo
    ax.set_xticks(x)
    ax.set_xticklabels(pivot_df.index, rotation=xtick_rotation, ha=xtick_ha)

    # applica font/color ai tick
    for label in ax.get_xticklabels():
        label.set_fontsize(xtick_labelsize)
        label.set_color(font_color)

    for label in ax.get_yticklabels():
        label.set_fontsize(ytick_labelsize)
        label.set_color(font_color)

    ax.legend(title=legend_title, loc=legend_loc, ncol=legend_ncol, frameon=legend_frame)
    ax.grid(axis="y", linestyle="--", alpha=0.7)

    plt.tight_layout()
    plt.show()

    return pivot_df






# =====================================================
# 2) plot_dimension_profile
# =====================================================
def plot_dimension_profile(
    data_or_path,
    by=("model",),
    task_col="taskId",
    metric_col="solutionCheck",
    tax_col="tax",
    tax_act="taxAct",
    tax_rel="taxRel",
    tax_ref="taxRef",
    figsize=(12, 6),
    title="Completion Rate × group × Dominant C/D dimension",
    *,
    bars_by="group",
    annot=True,
    annot_fontsize=9,
    annot_fmt="{:.2f}",
    legend_loc="best",
    legend_ncol=1,
    legend_frame=False,
    xtick_fontsize=9,
    edgecolor="black",
    by_name_map=None,
    group_sep=" | ",
    style_map=None,
    cmap="tab10",
):
    """
    Media di `metric_col` per gruppo (definito da `by`) e per dimensione dominante della tassonomia
    C/D (Action, Relation, Reference). Gestisce co-dominanza duplicando il contributo del task nelle
    dimensioni a pari massimo.

    L’etichetta di gruppo è la concatenazione dei valori nelle colonne `by`, separati da `group_sep`.

    Parametri
    ----------
    data_or_path : pandas.DataFrame | str
    by : tuple[str] | list[str], default=("model",)
    task_col : str, default="taskId"
    metric_col : str, default="solutionCheck"
    tax_col : str, default="tax"
    tax_act : str, default="taxAct"
    tax_rel : str, default="taxRel"
    tax_ref : str, default="taxRef"
    figsize : tuple[float, float], default=(12, 6)
    title : str, default="Completion Rate × group × Dominant C/D dimension"

    bars_by : {"group","dimension"}, default="group"
        - "group": X = dimensioni; per ogni dimensione disegna N barre (una per gruppo) e
          applica `style_map` per gruppo.
        - "dimension": X = gruppi; 3 barre per gruppo (A/R/Ref) con colori della dimensione (ignora `style_map`).

    annot : bool, default=True
    annot_fontsize : int, default=9
    annot_fmt : str, default="{:.2f}"
    legend_loc : str, default="best"
    legend_ncol : int, default=1
    legend_frame : bool, default=False
    xtick_fontsize : int, default=9
    edgecolor : str, default="black"

    by_name_map : dict[str,str] | None, default=None
        Rinomina le colonne in `by` nel titolo della legenda.
    group_sep : str, default=" | "
        Separatore tra i valori concatenati dell’etichetta di gruppo.
    style_map : dict[str, tuple[color, hatch]] | None, default=None
        Stile per gruppo (solo quando bars_by="group"); le chiavi sono confrontate ignorando gli spazi.
    cmap : str, default="tab10"
        Colormap di fallback per gruppi non mappati.

    Ritorna
    -------
    completion_summary : pandas.DataFrame
        Media di `metric_col` per gruppo × dimensione dominante (colonne in ordine A/Rel/Ref).
    dominance_df : pandas.DataFrame
        Statistiche aggregate di dominanza per dimensione.
    task_dominance : pandas.DataFrame
        Frazioni per task in percentuale con colonna `dominant_dim`.
    """
    df = _load_df(data_or_path)
    required = set(by) | {task_col, metric_col, tax_col, tax_act, tax_rel, tax_ref}
    _ensure_columns(df, required)

    for col in [tax_col, tax_act, tax_rel, tax_ref, metric_col]:
        df[col] = pd.to_numeric(df[col], errors="coerce")

    # Frazioni e dominanza per task
    df["fracAct"] = df[tax_act] / df[tax_col]
    df["fracRel"] = df[tax_rel] / df[tax_col]
    df["fracRef"] = df[tax_ref] / df[tax_col]

    task_frac = df.groupby(task_col)[["fracAct", "fracRel", "fracRef"]].mean()

    def _dominant(row):
        m = row.max()
        cols = [c for c, v in row.items() if np.isclose(v, m, atol=1e-9)]
        mapping = {"fracAct": "Action", "fracRel": "Relation", "fracRef": "Reference"}
        return [mapping[c] for c in cols]

    task_frac["dominant_dims"] = task_frac.apply(_dominant, axis=1)
    task_frac["dominant_dim"] = task_frac["dominant_dims"].apply(lambda x: "/".join(x))

    # Per reporting in %
    task_dominance = task_frac.copy()
    task_dominance[["fracAct", "fracRel", "fracRef"]] *= 100
    task_dominance = task_dominance.rename(columns={
        "fracAct": "Action (%)",
        "fracRel": "Relation (%)",
        "fracRef": "Reference (%)"
    })

    # Espansione co-dominanza
    exploded = task_frac.explode("dominant_dims")
    df = df.merge(exploded["dominant_dims"], left_on=task_col, right_index=True, how="left")

    # Etichetta gruppo
    df["_group"] = df.apply(lambda r: group_sep.join([str(r[c]) for c in by]), axis=1)

    # Aggregazione per gruppo × dimensione
    completion_summary = (
        df.groupby(["_group", "dominant_dims"])[metric_col]
          .mean()
          .unstack(fill_value=0)
          .reindex(columns=["Action", "Relation", "Reference"], fill_value=0)
          .sort_index()
    )

    # Statistiche dominanza
    dominance_stats = []
    for dim, frac_col in zip(["Action", "Relation", "Reference"], ["fracAct", "fracRel", "fracRef"]):
        sub = exploded[exploded["dominant_dims"] == dim]
        dominance_stats.append({
            "dimension": dim,
            "avg_dominance_pct": (sub[frac_col].mean() * 100) if len(sub) else 0.0,
            "task_count": len(sub.index.unique())
        })
    dominance_df = pd.DataFrame(dominance_stats).set_index("dimension")

    # ---------- Plot ----------
    dims = ["Action", "Relation", "Reference"]
    groups = completion_summary.index.tolist()
    legend_title = " × ".join([by_name_map.get(c, c) for c in by]) if by_name_map else " × ".join(by)

    if bars_by == "group":
        auto_colors = _auto_color_lut(groups, cmap_name=cmap)
        style_norm = { _normalize_key(k): v for k, v in (style_map or {}).items() }
        unmapped = [g for g in groups if _normalize_key(g) not in style_norm]
        if style_map and unmapped:
            print(f"[plot_dimension_profile] Avviso: {len(unmapped)} gruppi non presenti in style_map "
                  f"(useranno colori automatici): {unmapped}")

        def _style_for(label):
            key = _normalize_key(label)
            if key in style_norm:
                c, h = style_norm[key]
                return (c if c is not None else auto_colors[label]), (h or "")
            return auto_colors[label], ""

        x = np.arange(len(dims))
        width = 0.8 / max(1, len(groups))
        fig, ax = plt.subplots(figsize=figsize)

        for i, g in enumerate(groups):
            vals = completion_summary.loc[g, dims].values
            color, hatch = _style_for(g)
            bars = ax.bar(x + i * width, vals, width, label=g, color=color, edgecolor=edgecolor)
            if hatch:
                for b in bars:
                    b.set_hatch(hatch)
            if annot:
                for b in bars:
                    v = b.get_height()
                    ax.annotate(
                        annot_fmt.format(v),
                        xy=(b.get_x() + b.get_width()/2, v),
                        xytext=(0, 3),
                        textcoords="offset points",
                        ha="center", va="bottom",
                        fontsize=annot_fontsize
                    )

        ax.set_xticks(x + width * (len(groups) - 1) / 2)
        xticks = [
            f"{d}\nAvg Dominance: {dominance_df.loc[d, 'avg_dominance_pct']:.1f}%\n#Tasks: {dominance_df.loc[d, 'task_count']}"
            for d in dims
        ]
        ax.set_xticklabels(xticks, fontsize=xtick_fontsize, ha="center")

        ax.legend(title=legend_title, loc=legend_loc, ncol=legend_ncol, frameon=legend_frame)
        ax.set_xlabel("Dominant C/D dimensions")

    elif bars_by == "dimension":
        x = np.arange(len(groups))
        width = 0.8 / 3.0
        base_colors = plt.get_cmap(cmap)(np.linspace(0, 1, 3))
        fig, ax = plt.subplots(figsize=figsize)

        for i, dim in enumerate(dims):
            vals = completion_summary[dim].values
            bars = ax.bar(x + i * width, vals, width, label=dim,
                          color=base_colors[i], edgecolor=edgecolor)
            if annot:
                for b in bars:
                    v = b.get_height()
                    ax.annotate(
                        annot_fmt.format(v),
                        xy=(b.get_x() + b.get_width()/2, v),
                        xytext=(0, 3),
                        textcoords="offset points",
                        ha="center", va="bottom",
                        fontsize=annot_fontsize
                    )

        ax.set_xticks(x + width)
        ax.set_xticklabels(groups, fontsize=xtick_fontsize, ha="center")
        ax.legend(title="Dimension", loc=legend_loc, ncol=legend_ncol, frameon=legend_frame)
        ax.set_xlabel("Group")
    else:
        raise ValueError("bars_by must be either 'group' or 'dimension'.")

    ax.set_ylabel("Completion Rate")
    ax.set_title(title)
    ax.grid(axis="y", linestyle="--", alpha=0.7)
    plt.tight_layout()
    plt.show()

    return completion_summary, dominance_df, task_dominance



def merge_exp(exp1:Path,exp2:Path,dst:Path):
    RESULTS=Path("results.csv")
    RUNDATA=Path("runData.csv")

    #Prendo solo le cartelle
    exp1_ep_time=[s for s in os.listdir(exp1) if (exp1/Path(s)).is_dir()]
    exp2_ep_time=[s for s in os.listdir(exp2) if (exp2/Path(s)).is_dir()]

    #Rimuovo il time stamp rimane solo l episodio come chiave per matchar come valore il apth da usare
    exp1_ep={s.split("_")[0]:s for s in exp1_ep_time}
    exp2_ep={s.split("_")[0]:s for s in exp2_ep_time}


    os.mkdir(dst) #Cartella nuovo esperimento nonde ve esistere

    #Megiare results
    for ep1_key,ep1_path in exp1_ep.items():
        ep2_path=exp2_ep[ep1_key]
        print(ep1_path,ep2_path)

        f_ep1_path,f_ep2_path=exp1/ep1_path,exp2/ep2_path

        ep1_res_path=f_ep1_path/RESULTS
        ep2_res_path=f_ep2_path/RESULTS

    

        res1_df=pd.read_csv(ep1_res_path,delimiter=";")
        res2_df=pd.read_csv(ep2_res_path,delimiter=";")

        res_merged_df:pd.DataFrame=pd.concat([res1_df,res2_df])

        #Sistemo la colonna repIndex
        res_merged_df["repIndex"] = range(len(res_merged_df))



        


        #Sistemare runData
        ep1_runData_path=f_ep1_path/RUNDATA
        ep2_runData_path=f_ep2_path/RUNDATA
        
        runData1_df=pd.read_csv(ep1_runData_path,delimiter=";")
        runData2_df=pd.read_csv(ep2_runData_path,delimiter=";")

        #Sommo le ripetizioni
        runData1_df["repetitions"]=runData1_df["repetitions"]+runData2_df["repetitions"]

        #creo cartella e salvo le robe
        merged_ep_path=dst/Path(ep1_key) 
        os.mkdir(merged_ep_path)




        runData1_df.to_csv(merged_ep_path/RUNDATA,index=False,sep=";") #salvo il nuovo run data con il totale delle ripetizioni

        res_merged_df.to_csv(merged_ep_path/RESULTS,index=False,sep=";")


import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from typing import Union, Dict, Any, Tuple, Optional
from pathlib import Path
import yaml

# Se li hai già definiti, non ridefinirli.
def _load_episodes(episodes_or_yaml: Union[str, Path, Dict[str, Any]]) -> Dict[str, Any]:
    if isinstance(episodes_or_yaml, dict):
        return episodes_or_yaml
    p = Path(episodes_or_yaml)
    if p.exists():
        with open(p, "r", encoding="utf-8") as fp:
            return yaml.safe_load(fp)
    # fallback: trattalo come stringa YAML
    return yaml.safe_load(episodes_or_yaml)


def compute_token_completion_matrix(
    data_or_path,
    episodes_or_yaml: Union[str, Path, Dict[str, Any]],
    metric_col: str = "solutionCheck",
    *,
    sep_fallback: str = ";",
    drop_all_nan_tokens: bool = True,
) -> Tuple[pd.DataFrame, Dict[str, list[str]]]:
    """
    Costruisce una matrice (dimensione × token) dove ogni cella è il completion rate medio
    per quel token in quella dimensione, aggregato su TUTTI i task/scene che contengono il token.

    Parametri
    ----------
    data_or_path : pd.DataFrame | str | Path
        DataFrame aggregato (es. out_agg) oppure path al CSV.
        Deve contenere:
          - colonna metric_col (tipicamente 'solutionCheck')
          - una colonna per ciascun token definito nella tassonomia
            (come generato da compute_episode_result/get_token_levels_index).
    episodes_or_yaml : str | Path | dict
        YAML degli episodi (o dict già caricato) contenente taxonomy_spec/axonomy_spec.
    metric_col : str, default="solutionCheck"
        Colonna metrica da usare come completion rate.
    sep_fallback : str, default=";"
        Separatore alternativo da usare se il CSV non è separato da ','.
    drop_all_nan_tokens : bool, default=True
        Se True, elimina i token che sono NaN in tutte e tre le dimensioni
        (nessun task li usa).

    Ritorna
    -------
    df_matrix : pd.DataFrame
        DataFrame con index = ["Action", "Reference", "Relation"],
        columns = token (in ordine ACT + REF + REL),
        valori = completion rate medio (float) o NaN.
    dim_to_tokens : dict
        Dizionario { "act": [token...], "ref": [...], "rel": [...] } utilizzato.
    """
    # --- 1) Carica df ---
    if isinstance(data_or_path, pd.DataFrame):
        df = data_or_path.copy()
    else:
        try:
            df = pd.read_csv(data_or_path)
        except Exception:
            df = pd.read_csv(data_or_path, sep=sep_fallback)

    if metric_col not in df.columns:
        raise ValueError(f"Column '{metric_col}' not found in data. Available: {list(df.columns)}")

    df[metric_col] = pd.to_numeric(df[metric_col], errors="coerce")

    # --- 2) Carica episodi/tassonomia e recupera tokens per dimensione ---
    doc = _load_episodes(episodes_or_yaml)
    spec_root = doc.get("taxonomy_spec") or doc.get("axonomy_spec") or {}
    spec_tokens = (spec_root.get("levels") or {}).get("tokens") or {}

    dims = ("act", "ref", "rel")
    dim_to_tokens: Dict[str, list[str]] = {
        d: list((spec_tokens.get(d, {}) or {}).keys()) for d in dims
    }

    # Ordine globale: ACT + REF + REL
    all_tokens: list[str] = [tok for d in dims for tok in dim_to_tokens[d]]

    # --- 3) Verifica che le colonne token esistano nel df (best-effort) ---
    missing_cols = [t for t in all_tokens if t not in df.columns]
    if missing_cols:
        print(f"[compute_token_completion_matrix] WARNING: {len(missing_cols)} token columns "
              f"not found in data and will be ignored: {missing_cols}")
        # Rimuovi dai token quelli mancanti
        all_tokens = [t for t in all_tokens if t in df.columns]
        for d in dims:
            dim_to_tokens[d] = [t for t in dim_to_tokens[d] if t in all_tokens]

    if not all_tokens:
        raise ValueError("No token columns from taxonomy found in the data frame.")

    # --- 4) Costruisci matrice 3 × N ---
    dim_labels = ["Action", "Reference", "Relation"]
    mat = np.full((3, len(all_tokens)), np.nan, dtype=float)

    # Per ogni dimensione e token: media su tutte le righe che hanno il token (notna)
    dim_index = {"act": 0, "ref": 1, "rel": 2}
    for d in dims:
        row_idx = dim_index[d]
        for tok in dim_to_tokens[d]:
            col_idx = all_tokens.index(tok)
            mask = df[tok].notna()
            if not mask.any():
                # Nessun task con questo token
                continue
            val = df.loc[mask, metric_col].mean()
            mat[row_idx, col_idx] = float(val)

    df_matrix = pd.DataFrame(mat, index=dim_labels, columns=all_tokens)

    # opzionale: togli i token completamente NaN
    if drop_all_nan_tokens:
        df_matrix = df_matrix.loc[:, df_matrix.notna().any(axis=0)]

    return df_matrix, dim_to_tokens


from matplotlib.patches import Patch

def plot_token_completion_bars(
    data_or_path,
    episodes_or_yaml: Union[str, Path, Dict[str, Any]],
    metric_col: str = "solutionCheck",
    *,
    sep_fallback: str = ";",
    drop_all_nan_tokens: bool = True,
    figsize: Tuple[float, float] = (20, 5),
    sort_by: str = "dimension",  # "dimension" | "value" | "token"
    palette: Optional[Dict[str, str]] = None,
    ylim: Optional[Tuple[float, float]] = (0.0, 1.0),
    annot: bool = False,
    annot_fmt: str = "{:.2f}",
    annot_fontsize: int = 8,
):
    """
    Bar plot 1D: una barra per token, altezza = completion rate medio,
    colore = dimensione C/D (Action / Reference / Relation).

    Parametri chiave:
    - sort_by:
        "dimension": raggruppa per dimensione (A, poi Ref, poi Rel) e ordina per valore decrescente dentro il gruppo
        "value": ordina globalmente per completion rate
        "token": ordina alfabeticamente per nome token
    - palette: dict opzionale { "Action": "#...", "Reference": "#...", "Relation": "#..." }
    """
    # 1) Matrice dimensione×token
    df_matrix, dim_to_tokens = compute_token_completion_matrix(
        data_or_path=data_or_path,
        episodes_or_yaml=episodes_or_yaml,
        metric_col=metric_col,
        sep_fallback=sep_fallback,
        drop_all_nan_tokens=drop_all_nan_tokens,
    )

    # 2) Converti in formato "long" (token, dimension, value)
    rows = []
    for dim in df_matrix.index:
        for tok in df_matrix.columns:
            val = df_matrix.loc[dim, tok]
            if not np.isfinite(val):
                continue
            rows.append({"token": tok, "dimension": dim, metric_col: float(val)})

    if not rows:
        raise ValueError("No finite values to plot (all token completions are NaN).")

    df_long = pd.DataFrame(rows)

    # 3) Assicura unicità dimensione per token (caso patologico: token in più dimensioni)
    dims_per_token = df_long.groupby("token")["dimension"].nunique()
    multi_dim_tokens = dims_per_token[dims_per_token > 1].index.tolist()
    if multi_dim_tokens:
        print(
            "[plot_token_completion_bars] WARNING: some tokens appear in multiple dimensions, "
            "keeping the first occurrence only. Tokens: ",
            multi_dim_tokens,
        )
        df_long = (
            df_long.sort_values(["token", "dimension"])
                   .drop_duplicates(subset=["token"], keep="first")
        )

    # 4) Ordinamento token sull'asse X
    dim_order = ["Action", "Reference", "Relation"]
    dim_order_map = {d: i for i, d in enumerate(dim_order)}
    df_long["dim_order"] = df_long["dimension"].map(dim_order_map)

    if sort_by == "dimension":
        df_long = df_long.sort_values(
            ["dim_order", metric_col], ascending=[True, False]
        )
    elif sort_by == "value":
        df_long = df_long.sort_values(metric_col, ascending=False)
    elif sort_by == "token":
        df_long = df_long.sort_values("token")
    else:
        raise ValueError("sort_by must be one of {'dimension','value','token'}")

    tokens = df_long["token"].tolist()
    values = df_long[metric_col].values
    dims = df_long["dimension"].tolist()

    # 5) Palette colori
    if palette is None:
        palette = {
            "Action": "#1f77b4",    # blu
            "Reference": "#2ca02c", # verde
            "Relation": "#d62728",  # rosso
        }

    colors = [palette.get(d, "#7f7f7f") for d in dims]

    # 6) Plot
    fig, ax = plt.subplots(figsize=figsize)
    x = np.arange(len(tokens))

    bars = ax.bar(x, values, color=colors, edgecolor="black")

    if annot:
        for bar, v in zip(bars, values):
            ax.annotate(
                annot_fmt.format(v),
                xy=(bar.get_x() + bar.get_width()/2, v),
                xytext=(0, 3),
                textcoords="offset points",
                ha="center", va="bottom",
                fontsize=annot_fontsize,
            )

    ax.set_xticks(x)
    ax.set_xticklabels(tokens, rotation=90)
    ax.set_ylabel(metric_col)
    ax.set_xlabel("Tokens")
    ax.set_title(f"{metric_col} per token (colored by C/D dimension)")
    if ylim is not None:
        ax.set_ylim(*ylim)
    ax.grid(axis="y", linestyle="--", alpha=0.4)

    # legenda dimensioni
    legend_handles = []
    for dim_name in dim_order:
        if dim_name in df_long["dimension"].unique():
            legend_handles.append(Patch(facecolor=palette.get(dim_name, "#7f7f7f"),
                                        edgecolor="black",
                                        label=dim_name))
    ax.legend(handles=legend_handles, title="C/D dimension")

    fig.tight_layout()
    plt.show()

    return df_long

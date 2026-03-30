# config_loader.py
import os, argparse
from typing import get_origin, get_args, Optional, Type,Union
from pydantic import BaseModel
from enum import Enum
from data_classes import AdamConfig

def _is_optional(t) -> bool:
    return get_origin(t) is Optional or (get_origin(t) is Union and type(None) in get_args(t))  # py<3.10

def _strip_optional(t):
    return next((a for a in get_args(t) if a is not type(None)), t)  # py<3.10 compat

def _coerce(value: str, py_type):
    if issubclass(py_type, bool):
        return value.lower() in ("1", "true", "yes", "on")
    if issubclass(py_type, int):
        return int(value)
    if issubclass(py_type, float):
        return float(value)
    if issubclass(py_type, Enum):
        # accetta case-insensitive
        for m in py_type:
            if m.value.lower() == value.lower():
                return m
        raise ValueError(f"{value} not in {[m.value for m in py_type]}")
    return value  # str o altro

def build_arg_parser_from_model(model_cls: Type[BaseModel]) -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=f"{model_cls.__name__} runtime switches.")
    for name, field in model_cls.model_fields.items():
        meta = field.json_schema_extra or {}
        cli_flag = meta.get("cli")
        help_txt = meta.get("help", field.description or name)
        if not cli_flag:
            continue  # campo nascosto da CLI

        ann = field.annotation
        opt = _is_optional(ann)
        base_t = _strip_optional(ann) if opt else ann

        # bool con doppio flag
        if base_t is bool:
            parser.add_argument(cli_flag, dest=name, action="store_true", help=help_txt)
            parser.add_argument(f"--no-{cli_flag.lstrip('-')}", dest=name, action="store_false")
            parser.set_defaults(**{name: None})  # distinguere non-passato
            continue

        # Enum -> scelte
        if isinstance(base_t, type) and issubclass(base_t, Enum):
            choices = [m.value for m in base_t]
            parser.add_argument(cli_flag, dest=name, choices=choices, default=None, help=help_txt)
            continue

        # primitive
        py_type = str if base_t is None else base_t
        parser.add_argument(cli_flag, dest=name, type=py_type, default=None, help=help_txt)

    return parser

def load_config(model_cls: Type[BaseModel]) -> BaseModel:
    parser = build_arg_parser_from_model(model_cls)
    args = parser.parse_args()
    values = {}

    for name, field in model_cls.model_fields.items():
        meta = field.json_schema_extra or {}
        env_key = meta.get("env")  # se non definito, puoi fallback a NAME upper
        env_val = os.getenv(env_key) if env_key else None

        # CLI raw
        cli_val = getattr(args, name, None) if hasattr(args, name) else None

        # se CLI passato → vince
        if cli_val is not None:
            ann = field.annotation
            base_t = _strip_optional(ann) if _is_optional(ann) else ann
            if isinstance(base_t, type) and issubclass(base_t, Enum):
                values[name] = base_t(cli_val)  # da stringa a Enum
            else:
                values[name] = cli_val
            continue

        # altrimenti ENV se presente
        if env_val is not None:
            ann = field.annotation
            base_t = _strip_optional(ann) if _is_optional(ann) else ann
            if isinstance(base_t, type) and issubclass(base_t, Enum):
                values[name] = _coerce(env_val, base_t)
            elif isinstance(base_t, type):
                values[name] = _coerce(env_val, base_t)
            else:
                values[name] = env_val
            continue

        # default (Pydantic gestisce)
        # non mettere nulla in values -> userà il default del modello

    return model_cls(**values)


def set_env_from_config(config: AdamConfig) -> None:
    from enum import Enum
    for name, field in AdamConfig.model_fields.items():
        meta = field.json_schema_extra or {}
        env_key = meta.get("env")
        if not env_key:
            continue
        val = getattr(config, name)

        if val is None:
            # non lasciare variabili vuote in giro
            os.environ.pop(env_key, None)
            continue

        if isinstance(val, Enum):
            val = val.value
        os.environ[env_key] = str(val)


def read_config_from_env() -> AdamConfig:
    from enum import Enum
    def _coerce(value: str, typ):
        if isinstance(typ, type) and issubclass(typ, Enum):
            # match su value (case-insensitive)
            for m in typ:
                if m.value.lower() == value.lower():
                    return m
            raise ValueError(f"'{value}' not in {[m.value for m in typ]}")
        if typ is bool:
            return value.lower() in ("1", "true", "yes", "on")
        if typ is int:
            return int(value)
        if typ is float:
            return float(value)
        return value  # string o altro

    values = {}
    for name, field in AdamConfig.model_fields.items():
        meta = field.json_schema_extra or {}
        env_key = meta.get("env")
        raw = os.getenv(env_key) if env_key else None
        if raw is None:
            continue
        ann = field.annotation
        # Pydantic qui accetta direttamente Enum/primitive; coercizziamo solo se serve
        try:
            values[name] = _coerce(raw, ann)
        except Exception:
            # fallback: lascia stringa, poi Pydantic ci dirà se non va bene
            values[name] = raw

    return AdamConfig(**values)

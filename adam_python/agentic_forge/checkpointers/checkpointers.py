from enum import Enum
from ..configs.base_config import BaseConfig
from pydantic import  model_validator
from langgraph.checkpoint.memory import InMemorySaver
from langgraph.checkpoint.sqlite import SqliteSaver

from langgraph.checkpoint.base import BaseCheckpointSaver
import os
import sqlite3

class CheckpointerType(str, Enum):
    IN_MEMORY = "InMemorySaver"
    SQLITE = "SqliteSaver"
    NONE = "none"


from pathlib import Path
from typing import Optional

class InMemorySaverConfig(BaseConfig):
    pass  # Nessun parametro specifico

class SqliteSaverConfig(BaseConfig):
    path: Path
    check_same_thread: bool = True
    timeout: int = 5
    detect_types: int = 0
    isolation_level: Optional[str] = None

from typing import Union

class CheckpointerConfig(BaseConfig):
    type: Optional[CheckpointerType] = None
    config: Optional[Union[InMemorySaverConfig, SqliteSaverConfig]] = None

    @model_validator(mode="before")
    @classmethod
    def coerce_config(cls, data):
        # Se il campo è proprio null interamente, ritorna oggetto vuoto (default)
        if data is None:
            return {}

        # Se type è null, normalizza a stringa "none"
        if data.get("type") is None:
            data["type"] = "none"

        config_data = data.get("config")
        type_ = data.get("type")

        if type_ == CheckpointerType.SQLITE and isinstance(config_data, dict):
            data["config"] = SqliteSaverConfig(**config_data)
        elif type_ == CheckpointerType.IN_MEMORY and isinstance(config_data, dict):
            data["config"] = InMemorySaverConfig(**config_data)

        return data

    @model_validator(mode="after")
    def validate_checkpointer(self) -> "CheckpointerConfig":
        if self.type == CheckpointerType.SQLITE:
            if not isinstance(self.config, SqliteSaverConfig):
                raise ValueError("SqliteSaver requires a valid SqliteSaverConfig.")
        elif self.type == CheckpointerType.IN_MEMORY:
            if self.config is not None and not isinstance(self.config, InMemorySaverConfig):
                raise ValueError("InMemorySaver accepts only InMemorySaverConfig or None.")
        elif self.type == CheckpointerType.NONE:
            if self.config is not None:
                raise ValueError("No config must be provided when type is 'none'.")
        return self


def forge_checkpointer(cfg: Optional[CheckpointerConfig]) -> Optional[BaseCheckpointSaver]:
    if cfg is None or cfg.type==CheckpointerType.NONE:
        return None

    if cfg.type==CheckpointerType.IN_MEMORY:
        if cfg.config is None:
            return InMemorySaver()
        return InMemorySaver(**cfg.config.model_dump())
    
    elif cfg.type == CheckpointerType.SQLITE:
        if not isinstance(cfg.config, SqliteSaverConfig):
            raise ValueError("Expected SqliteSaverConfig for Sqlite checkpointer.")
        os.makedirs(cfg.config.path.parent, exist_ok=True)
        conn = sqlite3.connect(
            cfg.config.path,
            check_same_thread=cfg.config.check_same_thread,
            timeout=cfg.config.timeout,
            detect_types=cfg.config.detect_types,
            isolation_level=cfg.config.isolation_level,
        )
        return SqliteSaver(conn=conn)

    raise ValueError(f"Unsupported checkpointer type: {cfg.type}")

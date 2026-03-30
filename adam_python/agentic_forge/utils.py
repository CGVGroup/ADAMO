
from __future__ import annotations
from pathlib import Path
from enum import Enum


def get_project_root(project_name: str = "agentic_forge") -> Path:
    current = Path(__file__).resolve()
    for parent in current.parents:
        if parent.name == project_name:
            return parent
    raise RuntimeError(f"Could not find project root named '{project_name}'")



class ForwardEnum(Enum):
    """
    Forward degli attributi al contenuto del membro:
    - Durante l'init di Enum evitiamo di toccare `.value` (non ancora pronto).
    """
    def __getattr__(self, item):
        # 1) prova attributi "normali"
        try:
            return super().__getattribute__(item)
        except AttributeError:
            pass
        # 2) evita .value durante l'init: usa _value_ se presente
        try:
            target = object.__getattribute__(self, "_value_")
        except Exception:
            # il membro non è ancora inizializzato: lascia fallire normalmente
            raise AttributeError(item)
        # 3) forward
        try:
            return getattr(target, item)
        except AttributeError as e:
            tgt_name = getattr(target, "__name__", repr(target))
            raise AttributeError(
                f"{self.__class__.__name__}.{self.name} has no attribute {item!r} "
                f"(forward to {tgt_name} failed)"
            ) from e

    def __dir__(self):
        base = set(super().__dir__())
        try:
            target = object.__getattribute__(self, "_value_")
            base.update(dir(target))
        except Exception:
            pass
        return sorted(base)

from pathlib import Path
from typing import Union, Type, TypeVar, Optional, Dict, Any
from pydantic import BaseModel
import yaml
import warnings

T = TypeVar("T", bound="BaseConfig")


def deep_update(base: Dict[str, Any], overrides: Dict[str, Any]) -> Dict[str, Any]:
    """
    Ricorsivamente unisce `overrides` in `base`, sovrascrivendo solo i campi specificati.
    """
    for key, value in overrides.items():
        if isinstance(value, dict) and isinstance(base.get(key), dict):
            base[key] = deep_update(base.get(key, {}), value)
        else:
            base[key] = value
    return base


class BaseConfig(BaseModel):
    """
    Base class per tutti i config models, con supporto per:
    - Caricamento da YAML o dizionario
    - Override parziali annidati
    - Estrazione automatica se la config è annidata sotto una chiave
    - Avvisi sui valori di default usati
    """

    def __init__(self, **data):
        super().__init__(**data)
        for name, field in self.__fields__.items():
            if name not in data:
                default = getattr(self, name)
                warnings.warn(
                    f"[{self.__class__.__name__}] Field '{name}' not specified in input; using default: {default!r}",
                    UserWarning
                )

    @classmethod
    def _extract_nested_if_needed(cls, raw_data: dict) -> dict:
        """
        Se il dizionario ha una sola chiave top-level e il suo contenuto sembra una config,
        estrae automaticamente quel livello (utile per configurazioni “namespace”).
        """
        if isinstance(raw_data, dict) and len(raw_data) == 1:
            sole_key = next(iter(raw_data))
            maybe_inner = raw_data[sole_key]
            if isinstance(maybe_inner, dict):
                inner_keys = set(maybe_inner.keys())
                model_keys = set(cls.model_fields.keys())
                if inner_keys & model_keys:
                    warnings.warn(
                        f"[{cls.__name__}] Auto-extracted config from key '{sole_key}'",
                        UserWarning
                    )
                    return maybe_inner
        return raw_data

    @classmethod
    def from_yaml(cls: Type[T], path: Union[str, Path], override: Optional[dict] = None) -> T:
        """
        1) Legge il file YAML con encoding UTF-8 forzato (per evitare problemi su Windows)
        2) Estrae eventuale livello annidato se c’è un solo “namespace” top‐level
        3) Applica deep_update(raw_dict, override) *prima* di istanziare Pydantic
        4) Ritorna cls(**merged_dict), lasciando a Pydantic (e ai default_factory) il compito di
           popolare tutti i campi mancanti (ad es. i prompt di default in V2Config)
        """
        path = Path(path)
        # Apriamo sempre in UTF-8 per non incorrere in unicode‐decode errors su Windows
        with path.open("r", encoding="utf-8") as f:
            raw_data = yaml.safe_load(f) or {}

        # Se la radice è un singolo namespace che contiene i field di questo modello, lo estraiamo a forza
        data = cls._extract_nested_if_needed(raw_data)

        # Se c’è un override, applichiamolo ora al dict prima di creare l’oggetto
        if override:
            data = deep_update(data, override)

        # Creo finalmente l’istanza Pydantic, lasciando che i default_factory facciano il loro lavoro
        return cls(**data)

    @classmethod
    def from_dict(cls: Type[T], data: dict, override: Optional[dict] = None) -> T:
        """
        Come from_yaml, ma parte da un dict già caricato in memoria anziché da file.
        """
        extracted = cls._extract_nested_if_needed(data)

        if override:
            extracted = deep_update(extracted, override)

        return cls(**extracted)

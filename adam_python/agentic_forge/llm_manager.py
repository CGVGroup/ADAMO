import os
import yaml
from pathlib import Path
from typing import Dict, Literal, Optional, Union
from pydantic import model_validator, ValidationError

from langchain_openai.chat_models import ChatOpenAI
from langchain_openai.embeddings import OpenAIEmbeddings
from langchain_ollama.chat_models import ChatOllama
from langchain_ollama.embeddings import OllamaEmbeddings

from agentic_forge.configs.base_config import BaseConfig


# ------------------------------------------------------------
# ENUMS
# ------------------------------------------------------------
from enum import Enum

class ProviderType(str, Enum):
    OPENAI = "openai"
    OLLAMA = "ollama"
    OPENROUTER= "openrouter"

class ModelType(str, Enum):
    CHAT = "chat"
    EMBEDDING = "embedding"


# ------------------------------------------------------------
# MODEL CONFIGS
# ------------------------------------------------------------
class ChatModelConfig(BaseConfig):
    name: str
    max_tokens: int = 4000
    temperature: float = 0.0
    top_p: float = 1.0
    support_tools_parallel:bool=False
    support_tool_choice:bool=False
    tool_choice:Union[dict,str,bool]=False
    router:str=None #Per openrouter
    context:int=None #finestra di contesto del modello non viene usato manon si sa mai

class EmbeddingModelConfig(BaseConfig):
    name: str
    embedding_chunk_size: Optional[int] = 1

class ModelConfig(BaseConfig):
    """
    ModelConfig definisce un singolo modello, di tipo 'chat' o 'embedding',
    e incapsula la relativa config (ChatModelConfig o EmbeddingModelConfig).
    """
    type: ModelType
    config: Union[ChatModelConfig, EmbeddingModelConfig]

    @model_validator(mode="before")
    @classmethod
    def coerce_config(cls, data):
        type_ = data.get("type")
        config_data = data.get("config")
        if config_data is None:
            raise ValueError(f"Missing 'config' for model type '{type_}'.")

        constructor_map = {
            ModelType.CHAT.value: ChatModelConfig,
            ModelType.EMBEDDING.value: EmbeddingModelConfig,
        }
        constructor = constructor_map.get(type_)
        if constructor is None:
            raise ValueError(f"Unsupported model type '{type_}'.")

        try:
            data["config"] = constructor(**config_data)
        except ValidationError as e:
            details = "\n".join(f"- {'.'.join(map(str, err['loc']))}: {err['msg']}"
                                for err in e.errors())
            raise ValueError(
                f"Invalid fields in 'config' for model type '{type_}':\n{details}"
            ) from e

        return data


# ------------------------------------------------------------
# PROVIDER CONFIGS
# ------------------------------------------------------------
class OpenAIProviderConfig(BaseConfig):
    api_key_file: str
    models: Dict[str, ModelConfig]

class OllamaProviderConfig(BaseConfig):
    base_url: str
    keep_alive: Optional[str] = None
    models: Dict[str, ModelConfig]

class OpenRouterProviderConfig(BaseConfig):  # <-- NEW
    base_url: str = "https://openrouter.ai/api/v1"
    api_key_file: str
    # campi opzionali per header raccomandati da OpenRouter
    site_url: Optional[str] = None   # es. "https://example.com"
    app_name: Optional[str] = None   # es. "A.D.A.M.O."
    models: Dict[str, ModelConfig]

class ProviderConfig(BaseConfig):
    """
    ProviderConfig incapsula:
      - type: 'openai' o 'ollama' o 'openrouter'
      - config: a sua volta OpenAIProviderConfig o OllamaProviderConfig o OpenRouterProviderConfig
    """
    type: ProviderType
    config: Union[OpenAIProviderConfig, OllamaProviderConfig, OpenRouterProviderConfig]

    @model_validator(mode="before")
    @classmethod
    def coerce_config(cls, data):
        type_ = data.get("type")
        config_data = data.get("config")
        if config_data is None:
            raise ValueError(f"Missing 'config' for provider type '{type_}'.")

        constructor_map = {
            ProviderType.OPENAI.value: OpenAIProviderConfig,
            ProviderType.OLLAMA.value: OllamaProviderConfig,
            ProviderType.OPENROUTER.value: OpenRouterProviderConfig
        }
        constructor = constructor_map.get(type_)
        if constructor is None:
            raise ValueError(f"Unsupported provider type '{type_}'.")

        try:
            data["config"] = constructor(**config_data)
        except ValidationError as e:
            details = "\n".join(f"- {'.'.join(map(str, err['loc']))}: {err['msg']}"
                                for err in e.errors())
            raise ValueError(
                f"Invalid fields in 'config' for provider type '{type_}':\n{details}"
            ) from e

        return data


# ------------------------------------------------------------
# LLM MANAGER CONFIG
# ------------------------------------------------------------
class LlmManagerConfig(BaseConfig):


    providers: Dict[str, ProviderConfig]


# ------------------------------------------------------------
# LLM MANAGER (Main class)
# ------------------------------------------------------------
class LlmManager:
    """
    Manager unificato per modelli OpenAI e Ollama.
    Ora utilizza un dizionario di provider, ciascuno con chiave arbitraria.
    """

    def __init__(self, config: LlmManagerConfig):
        self.config = config

        # Invece di una lista, `providers` era già un dict: la chiave è un identificatore arbitrario
        # (es. "ollama_default", "openai_default"), e ciascun valore è un ProviderConfig.
        self.providers: Dict[str, ProviderConfig] = self.config.providers

        # Imposta le variabili d’ambiente per API key e Ollama keep-alive, se serve
        self._initialize_env()

    @classmethod
    def from_yaml(cls, path: Union[str, Path]) -> "LlmManager":
        config = LlmManagerConfig.from_yaml(path)
        return cls(config)

    def _initialize_env(self):
        for provider_key, provider in self.providers.items():

            if provider.type == ProviderType.OPENAI:
                # Se il percorso non è assoluto, lo trasformiamo
                api_path = provider.config.api_key_file
                if not os.path.isabs(api_path):
                    api_path = os.path.abspath(api_path)
                with open(api_path, 'r') as f:
                    api = yaml.safe_load(f)
                    os.environ["OPENAI_API_KEY"] = api["openai_api_key"]

            elif provider.type == ProviderType.OLLAMA:
                if provider.config.keep_alive:
                    os.environ["OLLAMA_KEEP_ALIVE"] = provider.config.keep_alive

            elif provider.type == ProviderType.OPENROUTER:
                api_path = provider.config.api_key_file
                if not os.path.isabs(api_path):
                    api_path = os.path.abspath(api_path)
                with open(api_path, 'r') as f:
                    api = yaml.safe_load(f)
                    # Manteniamo separata la key per chiarezza
                    os.environ["OPENROUTER_API_KEY"] = api.get("openrouter_api_key") or api.get("api_key") or ""
                # Nessuna variabile d'ambiente "obbligatoria" sugli header: li passiamo al build

    def get_model(self, provider_key: str, model_key: str, **kwargs):
        """
        Restituisce un modello (Chat o Embedding) per il provider identificato da `provider_key`
        (che è la chiave del dizionario `providers`), per il modello interno `model_key`.

        Esempio di chiamata:
            manager.get_model(provider_key="ollama_default", model_key="default_chat", stream=True)
        """
        if provider_key not in self.providers:
            raise ValueError(f"Provider `{provider_key}` non trovato fra: {list(self.providers.keys())}")

        provider_config = self.providers[provider_key]
        model_entry = provider_config.config.models.get(model_key)
        if model_entry is None:
            raise ValueError(
                f"Modello `{model_key}` non trovato sotto il provider `{provider_key}`. "
                f"Chiavi disponibili: {list(provider_config.config.models.keys())}"
            )

        model_type = model_entry.type
        model_cfg = model_entry.config

        if model_type == ModelType.CHAT:
            return self._build_chat(provider_config, model_cfg, **kwargs)
        elif model_type == ModelType.EMBEDDING:
            return self._build_embedding(provider_config, model_cfg, **kwargs)
        else:
            raise ValueError(f"Unsupported model type: {model_type}")

    def _build_chat(
        self,
        provider_config: ProviderConfig,
        model_cfg: ChatModelConfig,
        json_mode=False,
        stream=False,
        **kwargs
    ):
        """
        Costruisce un ChatOpenAI o ChatOllama a seconda di `provider_config.type`.
        I parametri standard (model, temperature, top_p, max_tokens) provengono da model_cfg.
        """
        params = {
            "model_name" if provider_config.type == ProviderType.OPENAI else "model": model_cfg.name,
            "max_tokens": model_cfg.max_tokens,
            **kwargs
        }

        if provider_config.type == ProviderType.OPENAI:
            #GPT-5 non accetta temperature e top-k
            if not (model_cfg.name.startswith("gpt-5")):
                params["temperature"] = model_cfg.temperature
                params["top_p"] = model_cfg.top_p

            return ChatOpenAI(
                **params,
                openai_api_key=os.getenv("OPENAI_API_KEY"),
                streaming=stream,
                model_kwargs={"response_format": {"type": "json_object"}} if json_mode else {}
            )
        elif provider_config.type == ProviderType.OLLAMA:  # ProviderType.OLLAMA
            return ChatOllama(
                **params,
                base_url=provider_config.config.base_url,
                format="json" if json_mode else "",
                stream=stream
            )
        elif provider_config.type == ProviderType.OPENROUTER:
            # Header consigliati da OpenRouter
            default_headers = {}
            site_url = getattr(provider_config.config, "site_url", None)
            app_name = getattr(provider_config.config, "app_name", None)
            if site_url:
                default_headers["HTTP-Referer"] = site_url
            if app_name:
                default_headers["X-Title"] = app_name

            # OpenRouter espone API compatibile OpenAI; usiamo ChatOpenAI con base_url custom.
            # N.B.: in langchain_openai, il parametro è 'base_url' e 'openai_api_key'.
            # Passiamo anche response_format se richiesto.
            model_kwargs = {}
            if json_mode:
                model_kwargs["response_format"] = {"type": "json_object"}

            # OpenRouter supporta tool-calling OpenAI-style; non servono branch speciali.
            return ChatOpenAI(
                **params,
                base_url=provider_config.config.base_url,
                openai_api_key=os.getenv("OPENROUTER_API_KEY"),
                streaming=stream,
                default_headers=default_headers,
                model_kwargs=model_kwargs
            )

        else:
            raise ValueError(f"Unsupported provider type: {provider_config.type}")


    def _build_embedding(
        self,
        provider_config: ProviderConfig,
        model_cfg: EmbeddingModelConfig,
        **kwargs
    ):
        """
        Costruisce un OpenAIEmbeddings o OllamaEmbeddings a seconda di `provider_config.type`.
        """
        if provider_config.type == ProviderType.OPENAI:
            return OpenAIEmbeddings(
                model=model_cfg.name,
                chunk_size=model_cfg.embedding_chunk_size or 1,
                openai_api_key=os.getenv("OPENAI_API_KEY"),
                **kwargs
            )
        elif provider_config.type == ProviderType.OLLAMA:
            return OllamaEmbeddings(
                model=model_cfg.name,
                base_url=provider_config.config.base_url,
                **kwargs
            )
        elif provider_config.type == ProviderType.OPENROUTER:
            # API compatibile OpenAI per embeddings
            return OpenAIEmbeddings(
                model=model_cfg.name,
                chunk_size=model_cfg.embedding_chunk_size or 1,
                openai_api_key=os.getenv("OPENROUTER_API_KEY"),
                base_url=provider_config.config.base_url,
                **kwargs
            )
        else:
            raise ValueError(f"Unsupported provider type: {provider_config.type}")


# ------------------------------------------------------------
# Factory
# ------------------------------------------------------------
def forge_llm_manager(config_or_path: Union[Path, str, dict, LlmManagerConfig]) -> LlmManager:
    r"""
    Se `config_or_path` è una stringa o Path, chiama `LlmManager.from_yaml`.
    Se è un dict, chiama `LlmManagerConfig.from_dict` e poi passa a LlmManager.
    Se è già un LlmManagerConfig, lo passa direttamente.
    """
    if isinstance(config_or_path, (str, Path)):
        return LlmManager.from_yaml(config_or_path)
    elif isinstance(config_or_path, dict):
        config = LlmManagerConfig.from_dict(config_or_path)
        return LlmManager(config)
    elif isinstance(config_or_path, LlmManagerConfig):
        return LlmManager(config_or_path)
    else:
        raise ValueError("Invalid LLM Manager configuration")

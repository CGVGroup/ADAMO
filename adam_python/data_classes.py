from pydantic import BaseModel
from typing import Any,Optional,Literal
from enum import Enum

# config.py
from enum import Enum
from typing import Optional,Union
from pydantic import BaseModel, Field

class ModelId(str, Enum):
    G4O = "G4O"
    MIS = "MIS"
    S35 = "S35"


class ObjectIdentifierId(str, Enum):
    SEM  = "SEM"
    OPAQ = "OPAQ"

class CoordinatesTypeId(str, Enum):
    ABS = "ABS"
    REL = "REL"

class AdamConfig(BaseModel):

    agent_server_app:str= Field(
        default="adam_agent_server",
        description="Name of the python script that run the uvicorn agent server",
        json_schema_extra={"env": "ADAM_AGENT_APP", "cli": "--agent-app", "help": "Name of the python script that run the uvicorn agent server"}
    )

    agent_host:str= Field(
        default="localhost",
        description="Host of the Agent server",
        json_schema_extra={"env": "ADAM_AGENT_HOST", "cli": "--agent-host", "help": "HTTP agent server host"}
    )

    agent_port:int= Field(
        default=50000,
        description="Port of the Agent server",
        json_schema_extra={"env": "ADAM_AGENT_PORT", "cli": "--agent-port", "help": "HTTP agent server port"}
    )

    agent_server_reload:bool= Field(
        default=False,
        description="Set the hot reload for the server",
        json_schema_extra={"env": "ADAM_AGENT_RELOAD", "cli": "--agent-reload", "help": "Set the Hot reload for the agent uvicorn server"}
    )

    model_id: ModelId = Field(
        default=ModelId.G4O,
        description="Model selector",
        json_schema_extra={"env": "MODEL_ID", "cli": "--model-id", "help": "Model selector"}
    )
    object_identifier_id: ObjectIdentifierId = Field(
        default=ObjectIdentifierId.SEM,
        description="Object identifier mode",
        json_schema_extra={"env": "OBJECT_IDENTIFIER_ID", "cli": "--object-identifier-id", "help": "Object identifier mode"}
    )
    coordinates_type_id: CoordinatesTypeId = Field(
        default=CoordinatesTypeId.ABS,
        description="Coordinates type",
        json_schema_extra={"env": "COORDINATES_TYPE_ID", "cli": "--coordinates-type-id", "help": "Coordinates type"}
    )

    tool_host: str = Field(
        default="http://localhost",
        description="Host of the tool server",
        json_schema_extra={"env": "ADAM_TOOL_HOST", "cli": "--tool-host", "help": "HTTP tool server host"}
    )
    tool_port: int = Field(
        default=60000,
        description="Port of the tool server",
        json_schema_extra={"env": "ADAM_TOOL_PORT", "cli": "--tool-port", "help": "HTTP tool server port"}
    )
    tool_timeout: Optional[Union[int,None]] = Field(
        default=None,
        json_schema_extra={"env": "ADAM_TIMEOUT", "cli": "--tool-timeout", "help": "HTTP timeout (s)"}
    )

    debug: bool = Field(
        default=False,
        json_schema_extra={"env": "ADAM_DEBUG", "cli": "--debug", "help": "Enable debug"}
    )
    verbose: bool = Field(
        default=True,
        json_schema_extra={"env": "ADAM_VERBOSE", "cli": "--verbose", "help": "Verbose logs"}
    )
    llm_config_path: Optional[str] = Field(
        default="./agentic_forge/configs/llm_manager_config.yaml",
        json_schema_extra={"env": "ADAM_LLM_CONFIG_PATH","cli": "--llm-config-path",  "help": "LLM config file",},
    )
    checkpointer_config_path: Optional[str] = Field(
        default="./agentic_forge/configs/checkpointer_config.yaml",
        json_schema_extra={"env": "ADAM_CHECKPOINTER_CONFIG_PATH", "cli": "--checkpointer-config-path","help": "Checkpointer config file",},
    )
    msg_window_size: int = Field(
        default=3, #default 50 #ablation 3
        json_schema_extra={"env": "ADAM_MSG_WINDOW_SIZE", "cli": "--msg-window-size", "help": "Message window size"}
    )
    recursion_limit: int = Field(
        default=10000,
        json_schema_extra={"env": "ADAM_RECURSION_LIMIT", "cli": "--recursion-limit", "help": "Graph recursion limit"}
    )
    tools_parallel: bool = Field(
        default=False,
        json_schema_extra={"env": "ADAM_TOOLS_PARALLEL", "cli": "--tools-parallel", "help": "Run tools in parallel"}
    )

    model_config = dict(use_enum_values=True)  # dump come stringhe

# --- API Models ---
class BaseResponse(BaseModel):
    code: int
    message: str

class TextInferenceInput(BaseModel):
    thread_id: str
    message: str

class VisionInferenceInput(BaseModel):
    thread_id: str
    message: str
    image_base64: str  # JPEG or PNG

class InferenceResponse(BaseModel):
    thread_id: str
    response: str

class Vector3(BaseModel):
    x:float
    y:float
    z:float

class UnityGameObject(BaseModel):
    id: int #instance id
    type: str #nome della classe
    position: Vector3 #posizione dell'instanza
    volume: float #volume approssimato dell'oggetto in m^3



class UnityAgent(BaseModel):
    position: Vector3
    #rotation ??

class AgentInput(VisionInferenceInput):
    game_objects: list[UnityGameObject]
    spatial_points: list[UnityGameObject]
    agent_location: Vector3


# ---Checkpointer/ Threads ---
class GetUserThreadsResponse(BaseModel):
    threads: list[Any]

class DeleteUserThreadRequest(BaseModel):
    thread_id: str

class GetCheckpointsRequest(BaseModel):
    thread_id: str
    limit: Optional[int] = None  # se None, restituisce tutti

class GetCheckpointsResponse(BaseModel):
    thread_id: str
    checkpoints: list[Any]
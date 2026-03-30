from typing import ClassVar


from typing import Any, List, Sequence, get_args, get_origin
from pydantic import BaseModel,Field

from langchain_core.tools import BaseTool  # solo per typing esplicito
from agentic_forge.tools.tools_http import HTTPPostTool
from typing import Dict,Any


# agentic_forge/tooling/walk_tool.py
from typing import Any, ClassVar, Dict, List, Optional



from agentic_forge.tools.tools_command import HTTPCommandToolBase, ExecuteOutput,VisionHumanMessage,InjectedArgs
from data_classes import UnityGameObject,Vector3  # same model you already use in AdamState

from adam_prompts import WALK_TOOL_PROMPT,DROP_TOOL_PROMPT,PICK_TOOL_PROMPT,LOOK_TOOL_PROMPT,fmt_agent_location_prompt


from agentic_forge.utils import ForwardEnum




class PAYLOAD(ForwardEnum):
    action_state="action_state"
    completion_code="completion_code"
    fail_code="fail_code"
    agent_location="agent_location"
    action_log="action_log"
    image_base64="image_base64"
    spatial_points="spatial_points"
    game_objects="game_objects"
    obj_id="obj_id"


def _handle_http_response(response):
    if "status" in response or "ok" in response:
        status = response.get("status")
        ok = response.get("ok")
    #HTTP error
    if status != 200 and ok is not True: #Caso errore htttp
        raise Exception(f"ERROR in calling Walk: HTTP error: status={status}, ok={ok}")
        
    payload = response.get("data") or {} #Qui ho i dati senza erroe http
    return payload


def _validate_data_model(raw_data: Any, data_model: Any):
    """
    Valida raw_data rispetto a data_model (Pydantic v2).
    - data_model = Model:           ritorna Model | None
    - data_model = List[Model]:     ritorna List[Model] (filtrata) | None
    """
    origin = get_origin(data_model)

    # Caso lista di modelli: List[Model]
    if origin in (list, List, Sequence):
        args = get_args(data_model)
        if not args:
            return None
        elem_model = args[0]
        if not isinstance(raw_data, list):
            return None

        parsed = []
        for item in raw_data:
            try:
                parsed.append(elem_model.model_validate(item))
            except Exception:
                # tollera elementi non validi
                continue
        return parsed or None

    # Caso modello singolo: Model
    try:
        return data_model.model_validate(raw_data)
    except Exception:
        return None




class WalkTool(HTTPCommandToolBase):

    class Args(InjectedArgs):
        x: float = Field(..., description=WALK_TOOL_PROMPT.ArgsDescriptions.x.value)
        y: float = Field(..., description=WALK_TOOL_PROMPT.ArgsDescriptions.y.value)
        z: float = Field(..., description=WALK_TOOL_PROMPT.ArgsDescriptions.z.value)

    # ---- Tool metadata (unchanged) ----
    name: ClassVar[str] = WALK_TOOL_PROMPT.tool_name.value
    description: ClassVar[str] = WALK_TOOL_PROMPT.description.value
    args_schema: ClassVar[type[BaseModel]] = Args
    tool_message_text: str = "Walk action processed"  # will be overridden per call
    debug:bool=False
    object_identifier_id:str

    # ---- Payload (unchanged) ----
    def build_payload(
        self,
        x: float, y: float, z: float,
    ) -> Dict[str, float]:
        payload={
            "x": x, "y": y, "z": z,
        }
        return payload

    # ---- Normalize HTTP response into a clean dict ----
    def process_response(self, response: Dict[str, Any],**tool_args: Any) -> Dict[str, Any]:

        #Analisi potenziali errori http
        payload=_handle_http_response(response)

        # ("action_state")
        # ("fail_code")
        # ("completion_code")
        # ("agent_location")        payload contiene questo

        #Valido agent_location
        agent_location: Optional[Vector3] = _validate_data_model(payload.get(PAYLOAD.agent_location.value),Vector3)
        if not agent_location: raise Exception("No valid agent_location")
        payload[PAYLOAD.agent_location.value]=agent_location

        return payload

    # ---- Decide how to update state and what tool-message to return ----
    def execute(self, data: Dict[str, Any],**tool_args: Any) -> ExecuteOutput:
        action_state=data.get(PAYLOAD.action_state.value)
        fail_code=data.get(PAYLOAD.fail_code.value)
        completion_code=data.get(PAYLOAD.completion_code.value)
        agent_location=data.get(PAYLOAD.agent_location.value)
            


        # Completed: update state (objects), append Vision message later
        if action_state == WALK_TOOL_PROMPT.FeedbackMessages.Completed.name:


            if completion_code==WALK_TOOL_PROMPT.FeedbackMessages.Completed.RequestedDestinationReached.name:
                msg = WALK_TOOL_PROMPT.FeedbackMessages.Completed.RequestedDestinationReached.value.format(agent_location=fmt_agent_location_prompt(agent_location))
                update: Dict[str, Any] = {"agent_location":agent_location}
            
            elif completion_code==WALK_TOOL_PROMPT.FeedbackMessages.Completed.SuggestedDestinationReached.name:
                
                msg = WALK_TOOL_PROMPT.FeedbackMessages.Completed.RequestedDestinationReached.value.format(agent_location=fmt_agent_location_prompt(agent_location))
                update: Dict[str, Any] = {"agent_location":agent_location}
            else:
                raise Exception(f"Walk tool return with {PAYLOAD.action_state.value} {action_state} but sends a unknown {PAYLOAD.completion_code.value} {completion_code}")

     
            return ExecuteOutput(
                update=update,
                override_tool_message_text=msg,
            )



        # Failed: suggest nearest reachable destination if present; no updates
        elif action_state == WALK_TOOL_PROMPT.FeedbackMessages.Failed.name:
       
            if fail_code==WALK_TOOL_PROMPT.FeedbackMessages.Failed.DestinationNotReachable.name:
                return ExecuteOutput(
                message_only=True,
                override_tool_message_text=WALK_TOOL_PROMPT.FeedbackMessages.Failed.DestinationNotReachable.value,
            )
            else:
                raise Exception(f"Walk tool return with {PAYLOAD.action_state.value} {action_state} and sends a unknown {PAYLOAD.fail_code.value} {fail_code}")
        
        else:

            raise Exception(f"Unknown {PAYLOAD.action_state.value} for Walk should be Completed or Failed: {action_state}")
        

class LookTool(HTTPCommandToolBase):
    # ---- Arguments (unchanged) ----
    class Args(InjectedArgs):
        # Gaze target (where to look)
        x: float = Field(..., description=LOOK_TOOL_PROMPT.ArgsDescriptions.x.value)
        y: float = Field(..., description=LOOK_TOOL_PROMPT.ArgsDescriptions.y.value)
        z: float = Field(..., description=LOOK_TOOL_PROMPT.ArgsDescriptions.z.value)

    # ---- Tool metadata (unchanged) ----
    name: ClassVar[str] = LOOK_TOOL_PROMPT.tool_name.value
    description: ClassVar[str] = LOOK_TOOL_PROMPT.description.value
    args_schema: ClassVar[type[BaseModel]] = Args
    tool_message_text: str = "Look action processed"  # will be overridden per call
    debug:bool=False
    object_identifier_id:str

    # ---- Payload (unchanged) ----
    def build_payload(
        self,
        x: float, y: float, z: float,
    ) -> Dict[str, float]:
        payload={
            "x": x, "y": y, "z": z,
        }
        return payload

    # ---- Normalize HTTP response into a clean dict ----
    def process_response(self, response: Dict[str, Any],**tool_args: Any) -> Dict[str, Any]:


        payload=_handle_http_response(response)

        # ("action_state")
        # ("fail_code")
        # ("completion_code")
        # ("agent_location")        payload contiene questo
        # ("image_base64")
        # ("game_objects")
        # ("spatial_points")
        

        #Preparo i dati nel modello dati coretto per lo state dell agente
        
        objects: Optional[List[UnityGameObject]] = _validate_data_model(payload.get(PAYLOAD.game_objects.value),List[UnityGameObject])
        payload[PAYLOAD.game_objects.value]=objects #Se lista vuota ritorna None


        points: Optional[List[UnityGameObject]] = _validate_data_model(payload.get(PAYLOAD.spatial_points.value),List[UnityGameObject])
        if not points: raise Exception("No valid spatial_points") #Caso impossibile
        payload[PAYLOAD.spatial_points.value]=points

        return payload

    # ---- Decide how to update state and what tool-message to return ----
    def execute(self, data: Dict[str, Any],**tool_args: Any) -> ExecuteOutput:
        action_state=data.get(PAYLOAD.action_state.value)
        #Non usati dalla look
        #fail_code=data.get(PAYLOAD.fail_code.value)
        #completion_code=data.get(PAYLOAD.completion_code.value)
        #agent_location=data.get(PAYLOAD.agent_location.value)
        game_objects=data.get(PAYLOAD.game_objects.value)
        spatial_points=data.get(PAYLOAD.spatial_points.value)
        image_base64=data.get(PAYLOAD.image_base64.value)
  

        # Completed: update state (objects), append Vision message later
        if action_state == LOOK_TOOL_PROMPT.FeedbackMessages.Completed.name:
            update: Dict[str, Any] = {}
            if not game_objects:
                msg=LOOK_TOOL_PROMPT.FeedbackMessages.Completed.noNewGameObjects.value
                update[PAYLOAD.spatial_points.name]=spatial_points
                return ExecuteOutput(
                    update=update,
                    override_tool_message_text=msg,
                    data={PAYLOAD.image_base64.name: image_base64}
                )

            msg = LOOK_TOOL_PROMPT.FeedbackMessages.Completed.feedback.value
            update[PAYLOAD.game_objects.name] = game_objects
            update[PAYLOAD.spatial_points.name]=spatial_points   
            return ExecuteOutput(
                update=update,
                data={PAYLOAD.image_base64.name: image_base64},
                override_tool_message_text=msg,
            )

        else:
            raise Exception(f"Unknown {PAYLOAD.action_state.name}: {action_state!r}")

    # ---- Attach extra messages (Vision) when we have a snapshot image ----
    def extra_messages(
        self,
        tool_call_id: str,
        data: Dict[str, Any],
        update: Dict[str, Any],
        tool_args: Dict[str, Any],
    ):
        #If this tool have state completed should have an image, in all other cases extra messages is not calles since we set message_only=True
        img_b64 = data.get(PAYLOAD.image_base64.value)
        if not img_b64:
            raise Exception(f"Response does not contain the {PAYLOAD.image_base64.value} field")

        caption = "[VISUAL_PERCEPTION]"
        return [VisionHumanMessage(text=caption, image=img_b64)]
    



class PickObjectTool(HTTPCommandToolBase):
    class Args(InjectedArgs):
        ObjTag: str = Field(
            ...,
            description=PICK_TOOL_PROMPT.ArgsDescriptions.ObjTag.value
        )

    # Tool metadata
    name: ClassVar[str] = PICK_TOOL_PROMPT.tool_name.value
    description: ClassVar[str] = PICK_TOOL_PROMPT.description.value
    args_schema: ClassVar[type[BaseModel]] = Args
    object_identifier_id:str

    def build_payload(self, ObjTag: str) -> Dict[str, Any]:
        return {"stringId": ObjTag}

    def process_response(self, response: Dict[str, Any],**tool_args: Any) -> str:
        payload=_handle_http_response(response)

        agent_location: Optional[List[UnityGameObject]] = _validate_data_model(payload.get(PAYLOAD.agent_location.value),Vector3)
        if not agent_location: raise Exception("No valid agent_location")
        payload[PAYLOAD.agent_location.value]=agent_location 

        return payload




   
    
    def execute(self, data: Dict[str, Any],**tool_args: Any) -> ExecuteOutput:
        #fmt_obj_id=forge_game_obj_prompt(self.object_identifier_id)
        action_state=data.get(PAYLOAD.action_state.value)
        fail_code=data.get(PAYLOAD.fail_code.value)
        completion_code=data.get(PAYLOAD.completion_code.value)
        agent_location=data.get(PAYLOAD.agent_location.value)
        obj_id=data.get(PAYLOAD.obj_id.value)
        #non usati dalla pick
        #game_objects=data.get(PAYLOAD.game_objects.value)
        #spatial_points=data.get(PAYLOAD.spatial_points.value)
        #image_base64=data.get(PAYLOAD.image_base64.value)


        update={}
        msg=""
        msg_only=True
        if action_state == PICK_TOOL_PROMPT.FeedbackMessages.Completed.name:

            if completion_code == PICK_TOOL_PROMPT.FeedbackMessages.Completed.WithMoving.name:
                update={PAYLOAD.agent_location.value:agent_location}
                msg= PICK_TOOL_PROMPT.FeedbackMessages.Completed.WithMoving.value.format(obj_id=obj_id,agent_location=fmt_agent_location_prompt(agent_location))
                msg_only=False
            
            elif completion_code==PICK_TOOL_PROMPT.FeedbackMessages.Completed.WithoutMoving.name:
                msg= PICK_TOOL_PROMPT.FeedbackMessages.Completed.WithoutMoving.value.format(obj_id=obj_id)
                msg_only=True

        elif action_state == PICK_TOOL_PROMPT.FeedbackMessages.Failed.name:

            if fail_code==PICK_TOOL_PROMPT.FeedbackMessages.Failed.LocationNotReachable.name:
                msg= PICK_TOOL_PROMPT.FeedbackMessages.Failed.LocationNotReachable.value.format(obj_id=obj_id)

            
            elif fail_code==PICK_TOOL_PROMPT.FeedbackMessages.Failed.ObjAlreadyHeld.name:
                msg= PICK_TOOL_PROMPT.FeedbackMessages.Failed.ObjAlreadyHeld.value.format(obj_id=obj_id)

            
            elif fail_code==PICK_TOOL_PROMPT.FeedbackMessages.Failed.ObjNotFound.name:
                msg=PICK_TOOL_PROMPT.FeedbackMessages.Failed.ObjNotFound.value.format(obj_id=obj_id)
            
            elif fail_code==PICK_TOOL_PROMPT.FeedbackMessages.Failed.ObjNotReachable.name:
                update={PAYLOAD.agent_location:agent_location}
                msg= PICK_TOOL_PROMPT.FeedbackMessages.Failed.ObjNotReachable.value.format(agent_location=fmt_agent_location_prompt(agent_location),obj_id=obj_id)
                msg_only=False
            
        return ExecuteOutput(
            update=update,
            override_tool_message_text=msg,
            msg_only=msg_only
        )
            


class DropObjectTool(HTTPCommandToolBase):

    class Args(InjectedArgs):
        ObjTag: str = Field(
            ...,
            description=DROP_TOOL_PROMPT.ArgsDescriptions.ObjTag.value
        )
        x: float = Field(..., description=DROP_TOOL_PROMPT.ArgsDescriptions.x.value)
        y: float = Field(..., description=DROP_TOOL_PROMPT.ArgsDescriptions.y.value)
        z: float = Field(..., description=DROP_TOOL_PROMPT.ArgsDescriptions.z.value)

    # Tool metadata
    name: ClassVar[str] = DROP_TOOL_PROMPT.tool_name.value
    description: ClassVar[str] = DROP_TOOL_PROMPT.description.value
    args_schema: ClassVar[type[BaseModel]] = Args
    object_identifier_id:str

    def build_payload(self, ObjTag: str, x: float, y: float, z: float) -> Dict[str, Any]:
        return {"stringId": ObjTag, "x": x, "y": y, "z": z}

    def process_response(self, response: Dict[str, Any],**tool_args: Any) -> Dict[str, Any]:
        payload = _handle_http_response(response)

        # valida e normalizza agent_location (Vector3)
        agent_location: Optional["Vector3"] = _validate_data_model(
            payload.get(PAYLOAD.agent_location.value), Vector3
        )
        if not agent_location:
            raise Exception("No valid agent_location")

        payload[PAYLOAD.agent_location.value] = agent_location
        return payload

    def execute(self, data: Dict[str, Any], **tool_args: Any) -> ExecuteOutput:
        #fmt_obj_id=forge_game_obj_prompt(self.object_identifier_id)
        action_state = data.get(PAYLOAD.action_state.value)
        fail_code = data.get(PAYLOAD.fail_code.value)
        completion_code = data.get(PAYLOAD.completion_code.value)
        agent_location = data.get(PAYLOAD.agent_location.value)
        obj_id = data.get(PAYLOAD.obj_id.value)

        update: Dict[Any, Any] = {}
        msg = ""
        msg_only = True

        if action_state == DROP_TOOL_PROMPT.FeedbackMessages.Completed.name:

            if completion_code == DROP_TOOL_PROMPT.FeedbackMessages.Completed.WithMoving.name:
                update = {PAYLOAD.agent_location: agent_location}
                msg = DROP_TOOL_PROMPT.FeedbackMessages.Completed.WithMoving.value.format(obj_id=obj_id, agent_location=fmt_agent_location_prompt(agent_location))
                msg_only = False

            elif completion_code == DROP_TOOL_PROMPT.FeedbackMessages.Completed.WithoutMoving.name:
                msg = DROP_TOOL_PROMPT.FeedbackMessages.Completed.WithoutMoving.value.format(obj_id=obj_id)

        elif action_state == DROP_TOOL_PROMPT.FeedbackMessages.Failed.name:

            if fail_code == DROP_TOOL_PROMPT.FeedbackMessages.Failed.LocationNotReachable.name:
                msg = DROP_TOOL_PROMPT.FeedbackMessages.Failed.LocationNotReachable.value.format(obj_id=obj_id)

            elif fail_code == DROP_TOOL_PROMPT.FeedbackMessages.Failed.ObjNotHeld.name:
                msg = DROP_TOOL_PROMPT.FeedbackMessages.Failed.ObjNotHeld.value.format(obj_id=obj_id)

            elif fail_code == DROP_TOOL_PROMPT.FeedbackMessages.Failed.ObjNotFound.name:
                msg = DROP_TOOL_PROMPT.FeedbackMessages.Failed.ObjNotFound.value.format(obj_id=obj_id)

            elif fail_code == DROP_TOOL_PROMPT.FeedbackMessages.Failed.PosNotReachable.name:
                update = {PAYLOAD.agent_location: agent_location}
                msg = DROP_TOOL_PROMPT.FeedbackMessages.Failed.PosNotReachable.value.format(obj_id=obj_id, agent_location=fmt_agent_location_prompt(agent_location))
                msg_only = False

        return ExecuteOutput(
            update=update,
            override_tool_message_text=msg,
            msg_only=msg_only
        )

    



def forge_adam_toolkit(host: str = "http://localhost", port: int = 8080, timeout: int = None,debug:bool=False,object_identifier_id:str="SEM"):


    # Istanziazione vera e propria
    toolkit = [
        WalkTool(
            endpoint=f"{host}:{port}/api/walk",
            timeout=timeout,
            object_identifier_id=object_identifier_id
        ),

        LookTool(
            endpoint=f"{host}:{port}/api/look",
            timeout=timeout,
            object_identifier_id=object_identifier_id
            # debug=debug unused
        ),

        PickObjectTool(
            endpoint=f"{host}:{port}/api/pick",
            timeout=timeout,
            object_identifier_id=object_identifier_id
        ),
        DropObjectTool(
            endpoint=f"{host}:{port}/api/drop",
            timeout=timeout,
            object_identifier_id=object_identifier_id
        )
    ]

    return toolkit




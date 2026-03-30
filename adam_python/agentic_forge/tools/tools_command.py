# agentic_forge/tooling/command_tools.py

from __future__ import annotations
from typing import Any, Dict, List, Optional, ClassVar, Union, Sequence

from typing_extensions import Annotated
from pydantic import BaseModel, Field
from langchain_core.tools import BaseTool, InjectedToolCallId
from langchain_core.messages import BaseMessage, ToolMessage
from langgraph.types import Command, Send

from agentic_forge.tools.tools_http import HTTPPostTool
from agentic_forge.vision.messages import VisionHumanMessage


# --------------------------------------------------------------------------------------
# Common primitives
# --------------------------------------------------------------------------------------

class InjectedArgs(BaseModel):
    """
    Common args schema injected by LangChain: always provides the call id.
    This field is NOT exposed to the model/tool schema.

    Attributes
    ----------
    tool_call_id : str
        Internal identifier tying your ToolMessage to the LLM tool call.
    """
    tool_call_id: Annotated[str, InjectedToolCallId]


class ExecuteOutput(BaseModel):
    """
    Result container returned by `execute(...)`.

    You decide the behavior with these fields:

    - update: dict
        State updates you want to merge into the graph state.
    - data: dict
        Auxiliary data you may want to use when building extra messages
        (e.g., paths/URLs for VisionHumanMessage).
    - message_only: bool
        If True, the tool will return ONLY the mandatory ToolMessage, you can still override it.
        No state update will be merged, and no extra messages will be appended.
        Typical for error/failure branches when you want to avoid mutating state.
    - override_tool_message_text: Optional[str]
        If set, overrides the `tool_message_text` configured on the instance for this call only.
        Useful to adapt the tool message based on inputs or results.
    - override_goto: Optional[Union[Send, Sequence[Union[Send, str]], str]]
        If set, overrides the `goto` configured on the instance for this call only.

    Notes
    -----
    * There is ALWAYS at least one ToolMessage in the final Command.
      If you set `message_only=True`, only that ToolMessage is returned.
    """

    model_config = dict(arbitrary_types_allowed=True)

    update: Dict[str, Any] = {}
    data: Dict[str, Any] = {}
    message_only: bool = False
    override_tool_message_text: Optional[str] = None
    override_goto: Optional[Union[Send, Sequence[Union[Send, str]], str]] = None


# --------------------------------------------------------------------------------------
# Non-HTTP Command Tool
# --------------------------------------------------------------------------------------

class CommandToolBase(BaseTool):
    """
    Minimal base for non-HTTP tools that return a Command with:
      - a mandatory ToolMessage (plain text, no templating),
      - optional state updates (`update`),
      - optional extra messages (e.g., VisionHumanMessage),
      - optional `goto` (plain node name or Send object).

    Contract
    --------
    Subclasses MUST implement:
      * `execute(**tool_args) -> ExecuteOutput`
          Return state updates in `update`. Put any convenience payload in `data`.
          To adapt the mandatory ToolMessage or the goto for this call, set
          `override_tool_message_text` and/or `override_goto` in the returned ExecuteOutput.
      * `extra_messages(tool_call_id, data, update, tool_args) -> list[BaseMessage]`
          Build any additional messages (e.g., VisionHumanMessage) using `data` and `update`.

    Initialization
    --------------
    Parameters
    ----------
    tool_message_text : str
        Mandatory plain text used for the mandatory ToolMessage (as-is).
    goto : Optional[Union[Send, Sequence[Union[Send, str]], str]]
        Optional Command.goto. Can be a node name, a Send, or a sequence.
        Note: if you set goto the flow is branched: first is executed the node defined in the static edge and then the goto node

    Behavior
    --------
    - A ToolMessage with `tool_message_text` (or the per-call override) is always added.
    - If `message_only=True`, NO state update is merged and NO extra messages are appended.
    - Otherwise, `update` is merged and `extra_messages(...)` messages are appended.

    Examples
    --------
    Success with state update and Vision message:

    >>> class CheckEmotionArgs(InjectedArgs):
    >>>     pass
    >>>
    >>> class CheckEmotion(CommandToolBase):
    >>>     name: ClassVar[str] = "check_emotions"
    >>>     description: ClassVar[str] = "Update current emotion; attach a visualization."
    >>>     args_schema: ClassVar[type[BaseModel]] = CheckEmotionArgs
    >>>
    >>>     def execute(self, **tool_args) -> ExecuteOutput:
    >>>         return ExecuteOutput(
    >>>             update={"emotion": "sad"},
    >>>             data={"viz_path": "./imgs/emotion.jpg"},
    >>>         )
    >>>
    >>>     def extra_messages(self, tool_call_id, data, update, tool_args):
    >>>         return [VisionHumanMessage(f"Visual: {update['emotion']}", data["viz_path"])]
    >>>
    >>> tools = [CheckEmotion(tool_message_text="Emotion updated")]

    Failure branch with message-only:

    >>> class FailingTool(CommandToolBase):
    >>>     name: ClassVar[str] = "failing_tool"
    >>>     description: ClassVar[str] = "Demonstrates message-only error."
    >>>     args_schema: ClassVar[type[BaseModel]] = CheckEmotionArgs
    >>>
    >>>     def execute(self, **tool_args) -> ExecuteOutput:
    >>>         return ExecuteOutput(
    >>>             message_only=True,
    >>>             override_tool_message_text="Operation failed: invalid input."
    >>>         )
    >>>
    >>> tools = [FailingTool(tool_message_text="placeholder")]
    """
    args_schema: ClassVar[type[BaseModel]] = InjectedArgs
    tool_message_text: str = Field(..., description="Mandatory tool message, used as-is.")
    goto: Union[Send, Sequence[Union[Send, str]], str] = Field(default_factory=tuple, description="Optional Command.goto")
    registered_name: str = None


    def execute(self, **tool_args: Any) -> ExecuteOutput:
        raise NotImplementedError

    def extra_messages(
        self,
        tool_call_id: str,
        data: Dict[str, Any],
        update: Dict[str, Any],
        tool_args: Dict[str, Any],
    ) -> List[BaseMessage]:
        return []

    def _run(self, **tool_args: Any) -> Command:
        tool_call_id = tool_args.pop("tool_call_id", None)
        if self.registered_name is None:
            # set once
            object.__setattr__(self, "registered_name", self.name)
        tool_msg_name = self.registered_name
        if not tool_call_id:
            raise RuntimeError("Missing tool_call_id (InjectedToolCallId is required).")

        out = self.execute(**tool_args)
        if not isinstance(out, ExecuteOutput):
            raise TypeError("`execute` must return ExecuteOutput.")

        tool_msg_text = out.override_tool_message_text or self.tool_message_text
        goto = out.override_goto if out.override_goto is not None else self.goto


        
        messages: List[BaseMessage] = [ToolMessage(tool_msg_text, tool_call_id=tool_call_id)]

        if out.message_only:
            return Command(update={"messages": messages}, goto=goto)

        update = out.update or {}
        data = out.data or {}
        messages.extend(self.extra_messages(tool_call_id, data, update, tool_args))

        return Command(update={"messages": messages, **update}, goto=goto)

    async def _arun(self, **tool_args: Any) -> Command:
        return self._run(**tool_args)


# --------------------------------------------------------------------------------------
# HTTP Command Tool
# --------------------------------------------------------------------------------------

class HTTPCommandToolBase(HTTPPostTool):
    """
    HTTP variant mirroring the same behavior and simplicity as `CommandToolBase`.

    Subclass responsibilities
    -------------------------
    You MUST implement:
      * `build_payload(**tool_args) -> dict`
          Build the HTTP request JSON payload.
      * `process_response(response: dict, **tool_args) -> dict`
          Normalize the raw HTTP envelope into a clean `data` dict.
      * `execute(data: dict, **tool_args) -> ExecuteOutput`
          Return state updates in `update`, optional `data`, and behavioral flags/overrides.
      * `extra_messages(tool_call_id, data, update, tool_args) -> list[BaseMessage]`
          Build additional messages (e.g., Vision) using `data` and `update`.

    Initialization
    --------------
    Parameters
    ----------
    tool_message_text : str
        Mandatory plain text used for the mandatory ToolMessage (as-is).
    goto : Optional[Union[Send, Sequence[Union[Send, str]], str]]
        Optional Command.goto.
        Note: if you set goto the flow is branched: first is executed the node defined in the static edge and then the goto node

    Behavior
    --------
    - A ToolMessage with `tool_message_text` (or per-call override) is always added.
    - If `message_only=True` in ExecuteOutput, NO state update is merged and NO extra messages are appended.
    - Otherwise, `update` is merged and `extra_messages(...)` messages are appended.

    Examples
    --------
    Success path with update + Vision:

    >>> class AnalyzeArgs(InjectedArgs):
    >>>     text: str = Field(..., description="Text to analyze")
    >>>     img:  str = Field(..., description="Image path/URL")
    >>>
    >>> class AnalyzeEmotion(HTTPCommandToolBase):
    >>>     name: ClassVar[str] = "analyze_emotion"
    >>>     description: ClassVar[str] = "Analyze emotion via HTTP; update state; attach visualization."
    >>>     args_schema: ClassVar[type[BaseModel]] = AnalyzeArgs
    >>>
    >>>     endpoint: str = "http://localhost:8080/analyze"
    >>>     timeout: float = 10.0
    >>>
    >>>     def build_payload(self, text: str, img: str, **_: Any) -> Dict[str, Any]:
    >>>         return {"text": text, "image": img}
    >>>
    >>>     def process_response(self, response: Dict[str, Any], **_: Any) -> Dict[str, Any]:
    >>>         data = response.get("data") or {}
    >>>         return {
    >>>             "ok": bool(response.get("ok")),
    >>>             "status": int(response.get("status") or 0),
    >>>             "emotion": data.get("emotion"),
    >>>             "viz": data.get("viz_path"),
    >>>         }
    >>>
    >>>     def execute(self, data: Dict[str, Any], **_: Any) -> ExecuteOutput:
    >>>         if data["ok"] and data["status"] == 200 and data.get("emotion"):
    >>>             return ExecuteOutput(
    >>>                 update={"emotion": data["emotion"]},
    >>>                 data={"viz": data.get("viz")},
    >>>                 override_tool_message_text="Analysis complete"
    >>>             )
    >>>         return ExecuteOutput(
    >>>             message_only=True,
    >>>             override_tool_message_text=f"Analysis failed (HTTP {data.get('status')})"
    >>>         )
    >>>
    >>>     def extra_messages(self, tool_call_id: str, data: Dict[str, Any], update: Dict[str, Any], tool_args: Dict[str, Any]):
    >>>         if "emotion" in update and data.get("viz"):
    >>>             return [VisionHumanMessage(f"Visualization: {update['emotion']}", data["viz"])]
    >>>         return []
    >>>
    >>> tools = [AnalyzeEmotion(tool_message_text="placeholder")]
    """
    args_schema: ClassVar[type[BaseModel]] = InjectedArgs
    tool_message_text: str = Field(default="Tool action completed.", description="Mandatory tool message, used as-is.")
    goto: Union[Send, Sequence[Union[Send, str]], str] = Field(default_factory=tuple, description="Optional Command.goto")
    registered_name: str=None

    def process_response(self, response: Dict[str, Any], **tool_args: Any) -> Dict[str, Any]:
        raise NotImplementedError

    def execute(self, data: Dict[str, Any], **tool_args: Any) -> ExecuteOutput:
        raise NotImplementedError

    def extra_messages(
        self,
        tool_call_id: str,
        data: Dict[str, Any],
        update: Dict[str, Any],
        tool_args: Dict[str, Any],
    ) -> List[BaseMessage]:
        return []

    def _run(self, **tool_args: Any) -> Command:
        tool_call_id = tool_args.pop("tool_call_id", None)
        if not tool_call_id:
            raise RuntimeError("Missing tool_call_id (InjectedToolCallId is required).")
        if self.registered_name is None:
            # set once
            object.__setattr__(self, "registered_name", self.name)
        tool_msg_name = self.registered_name

        payload = self.build_payload(**tool_args)
        raw = self._http_post_sync(
            self.endpoint,
            payload=payload,
            headers=self.default_headers,
            timeout=self.timeout,
        )

        data = self.process_response(raw, **tool_args)
        if not isinstance(data, dict):
            raise TypeError("`process_response` must return a dict (`data`).")

        out = self.execute(data, **tool_args)
        if not isinstance(out, ExecuteOutput):
            raise TypeError("`execute` must return ExecuteOutput.")

        tool_msg_text = out.override_tool_message_text or self.tool_message_text
        goto = out.override_goto if out.override_goto is not None else self.goto



        messages: List[BaseMessage] = [ToolMessage(tool_msg_text, tool_call_id=tool_call_id)]

        if out.message_only:
            return Command(update={"messages": messages}, goto=goto)

        update = out.update or {}
        messages.extend(self.extra_messages(tool_call_id, data, update, tool_args))

        return Command(update={"messages": messages, **update}, goto=goto)

    async def _arun(self, **tool_args: Any) -> Command:
        tool_call_id = tool_args.pop("tool_call_id", None)
        if not tool_call_id:
            raise RuntimeError("Missing tool_call_id (InjectedToolCallId is required).")
        

        if self.registered_name is None:
            # set once
            object.__setattr__(self, "registered_name", self.name)
        tool_msg_name = self.registered_name


        payload = self.build_payload(**tool_args)
        raw = await self._http_post(
            self.endpoint,
            payload=payload,
            headers=self.default_headers,
            timeout=self.timeout,
        )

        data = self.process_response(raw, **tool_args)
        if not isinstance(data, dict):
            raise TypeError("`process_response` must return a dict (`data`).")

        out = self.execute(data, **tool_args)
        if not isinstance(out, ExecuteOutput):
            raise TypeError("`execute` must return ExecuteOutput.")

        tool_msg_text = out.override_tool_message_text or self.tool_message_text
        goto = out.override_goto if out.override_goto is not None else self.goto

        messages: List[BaseMessage] = [ToolMessage(tool_msg_text, tool_call_id=tool_call_id)]

        if out.message_only:
            return Command(update={"messages": messages}, goto=goto)

        update = out.update or {}
        messages.extend(self.extra_messages(tool_call_id, data, update, tool_args))

        return Command(update={"messages": messages, **update}, goto=goto)

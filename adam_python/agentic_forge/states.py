from pydantic import BaseModel
from langchain_core.messages import RemoveMessage,BaseMessage
from typing_extensions import Annotated,Optional,Union

from langgraph.graph.message import add_messages


class ChatState(BaseModel):
    messages: Annotated[Union[list[BaseMessage],RemoveMessage],add_messages]

class ChatStateStm(BaseModel):
    messages: Annotated[list[BaseMessage],add_messages]
    summary: Optional[str] = None

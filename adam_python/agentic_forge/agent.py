from __future__ import annotations

from typing import Optional, List, Union, TypedDict, Annotated
from pathlib import Path
import copy

from langgraph.graph import StateGraph, END
from langgraph.graph.state import CompiledStateGraph
from langgraph.prebuilt import ToolNode
from langgraph.graph.message import add_messages

from langchain_core.messages import (
    BaseMessage,
    HumanMessage,
    AIMessage,
    SystemMessage,
    RemoveMessage,
)
from langchain_core.prompts import ChatPromptTemplate, MessagesPlaceholder,SystemMessagePromptTemplate
from langchain_core.runnables import Runnable

from agentic_forge.checkpointers.checkpointers import forge_checkpointer, CheckpointerConfig
from agentic_forge.llm_manager import LlmManagerConfig, LlmManager
from agentic_forge.tools.tools_mcp import load_mcp_tools_from_url_sync


# -------------------------
# Stato LangGraph (solo messaggi)
# -------------------------
class AgentState(TypedDict):
    messages: Annotated[list[BaseMessage], add_messages]
    emotion: str="Happy"


# -------------------------
# Prompt minimale (niente placeholder)
# -------------------------
DEFAULT_SYSTEM_PROMPT = """\
You are tool-using assistant. 
Coordinate reasoning is relative to the user’s current query context. 
Prefer precise, actionable answers. If using tools, clearly state intent.
You can simulate emotion your emotion is: {emotion}\
"""



# -------------------------
# Agente snello: messaggi + tools
# -------------------------
class Agent:
    def __init__(
        self,
        provider: str,
        model_name: str,
        llm_config_path: Union[str, Path],
        checkpointer_config_path: Union[str, Path],
        system_prompt: str = DEFAULT_SYSTEM_PROMPT,
        msg_window_size: int = 5,
        tools_local: Optional[List[Runnable]] = None,
        tools_mcp_url: Optional[str] = None,   # URL MCP
        tools_parallel: bool = False,
        recursion_limit: int = 10,
        verbose: bool = False,
    ):
        self.verbose = verbose
        self.msg_window_size = msg_window_size
        self.system_prompt = system_prompt
        self.tools_parallel = tools_parallel
        self.recursion_limit = recursion_limit

        # 1) LLM
        llm_config = LlmManagerConfig.from_yaml(llm_config_path)
        self.llm = LlmManager(llm_config).get_model(provider, model_name)

        # 2) Prompt (System fisso + finestra messaggi)
        self.prompt_template = ChatPromptTemplate.from_messages([
            SystemMessagePromptTemplate.from_template(self.system_prompt),
            MessagesPlaceholder(variable_name="messages"),
        ])

        # 3) Tools (locali + MCP) e bind
        self.tools_local = tools_local or []
        self.tools_mcp = load_mcp_tools_from_url_sync(tools_mcp_url) if tools_mcp_url else []
        self.tools = self.tools_local + self.tools_mcp

        if self.tools:
            self.llm = self.llm.bind_tools(self.tools, parallel_tool_calls=self.tools_parallel)

        self.chat_chain = self.prompt_template | self.llm

        # 4) Checkpointer + Graph
        ckpt_config = CheckpointerConfig.from_yaml(checkpointer_config_path)
        self.checkpointer = forge_checkpointer(ckpt_config)
        self.graph: CompiledStateGraph = self._init_graph()

        if self.verbose:
            print("🛠️ [Agent (lean) Configuration]")
            print(f"🔹 Provider          : {provider}")
            print(f"🔹 Model             : {model_name}")
            print(f"🔹 Msg Window Size   : {msg_window_size}")
            print(f"🔹 Tools Local       : {[t.name for t in self.tools_local]}")
            print(f"🔹 Tools MCP         : url: {tools_mcp_url} - tools: {[t.name for t in self.tools_mcp]}")
            print("🔹 LangGraph Topology:")
            print(self.graph.get_graph().draw_ascii())
            print("=" * 60)

    # -------------------------
    # Graph
    # -------------------------
    def _init_graph(self) -> CompiledStateGraph:
        builder = StateGraph(AgentState)
        builder.add_node("manage_messages", self._manage_messages_node)
        builder.add_node("chat_model", self._chat_model_node)
        builder.set_entry_point("manage_messages")
        builder.add_edge("manage_messages", "chat_model")

        if self.tools:
            self._tool_node = ToolNode(self.tools)
            builder.add_node("tools", self._tool_node)
            builder.add_conditional_edges("chat_model", self._should_continue_node, ["tools", END])
            builder.add_edge("tools", "chat_model")
        else:
            builder.add_edge("chat_model", END)

        return builder.compile(checkpointer=self.checkpointer)

    def _should_continue_node(self, state: AgentState):
        last_message = state["messages"][-1]
        if isinstance(last_message, AIMessage) and last_message.tool_calls:
            return "tools"
        return END

    # -------------------------
    # Nodes
    # -------------------------
    def _manage_messages_node(self, state: AgentState) -> AgentState:
        """Mantiene solo gli ultimi N messaggi (finestra scorrevole)."""
        if len(state["messages"]) > self.msg_window_size:
            to_remove = state["messages"][:-self.msg_window_size]
            remove_ops = [RemoveMessage(id=m.id) for m in to_remove]
            return AgentState(messages=remove_ops)
        return state

    def _chat_model_node(self, state: AgentState) -> AgentState:
        chat_input = {"messages": state["messages"],"emotion":state["emotion"]}
        print(chat_input)

        if self.verbose:
            print("=" * 30)
            print("🧠 [Compiled Prompt]")
            # Stampa safe della finestra messaggi (nessun media handling necessario qui)
            safe_msgs = copy.deepcopy(state["messages"])
            safe_input={"messages":safe_msgs,"emotion":state["emotion"]}
            print(self.prompt_template.format(**safe_input))

        res: AIMessage = self.chat_chain.invoke(input=chat_input)

        if self.verbose:
            print(f"AI: {res.content}")
            if res.tool_calls:
                for tc in res.tool_calls:
                    print(f"🔧 Tool call -> {tc['name']} | args={tc['args']}")
            print("=" * 30)

        return AgentState(messages=[res])

    # -------------------------
    # API pubblica
    # -------------------------
    def _build_config(self, thread_id: Optional[str] = None, recursion_limit: int = 20, **kwargs) -> dict:
        cfg = {"recursion_limit": recursion_limit, "configurable":{}}
        if thread_id is not None:
            cfg["configurable"]["thread_id"] = thread_id
        if kwargs:
            cfg["configurable"].update(kwargs)
        print(cfg)
        return cfg

    def chat(self, user_input: str, thread_id: str = "default") -> tuple[AgentState, AIMessage]:
        user_message = HumanMessage(content=user_input)
        input_state = AgentState(messages=[user_message],emotion="Happy")
        print(input_state)
        state = self.graph.invoke(input_state, self._build_config(thread_id, self.recursion_limit))
        last: AIMessage = state["messages"][-1]  # type: ignore
        return state, last

    async def achat(self, user_input: str, thread_id: str = "default") -> tuple[AgentState, AIMessage]:
        user_message = HumanMessage(content=user_input)
        input_state = AgentState(messages=[user_message])
        state = await self.graph.ainvoke(input_state, self._build_config(thread_id, self.recursion_limit))
        last: AIMessage = state["messages"][-1]  # type: ignore
        return state, last

    # -------------------------
    # Utility checkpointer
    # -------------------------
    def list_thread_ids(self) -> list[str]:
        checkpoints = self.checkpointer.list(config=None)
        return list({
            cp.config.get("configurable", {}).get("thread_id")
            for cp in checkpoints
            if cp.config.get("configurable", {}).get("thread_id") is not None
        })

    def delete_thread(self, thread_id: str) -> None:
        if not self.checkpointer:
            raise RuntimeError("Checkpointer non configurato.")
        self.checkpointer.delete_thread(thread_id)

    def get_checkpoints(self, thread_id: str, limit: Optional[int] = None):
        config = self._build_config(thread_id)
        return list(self.checkpointer.list(config=config, limit=limit))

    def get_state(self, thread_id: str = "default") -> AgentState:
        state = self.graph.get_state(config=self._build_config(thread_id))
        return AgentState(**state)


from __future__ import annotations

from datetime import datetime
from typing import Optional, List,Union, TypedDict, Annotated,Literal
from langgraph.graph import StateGraph, START, END
from langgraph.graph.state import CompiledStateGraph
from langgraph.graph.message import add_messages
from langgraph.prebuilt import ToolNode
from langchain_core.messages import ToolMessage,HumanMessage,AIMessage
import copy
from copy import deepcopy



import time, uuid

from langchain_core.messages import (
    BaseMessage,
    HumanMessage,
    AIMessage,
    SystemMessage,
    RemoveMessage,
)
from langchain_core.prompts import ChatPromptTemplate, SystemMessagePromptTemplate, MessagesPlaceholder
from langchain_core.runnables import Runnable

from agentic_forge.checkpointers.checkpointers import forge_checkpointer,CheckpointerConfig

from agentic_forge.llm_manager import LlmManagerConfig, LlmManager
from agentic_forge.vision.messages import VisionHumanMessage
from agentic_forge.tools.tools_mcp import load_mcp_tools_from_url_sync
from pathlib import Path
from PIL import Image
from data_classes import UnityGameObject,Vector3
from adam_prompts import forge_game_obj_prompt,fmt_game_obj_prompt_sem,fmt_spatial_points_prompt,fmt_agent_location_prompt

# -------------------------
# Stato LangGraph
# -------------------------

class AdamState(TypedDict):
    messages: Annotated[list[BaseMessage], add_messages]
    game_objects: Optional[List[UnityGameObject]]=None
    spatial_points:Optional[List[UnityGameObject]]=None
    agent_location:Vector3=Vector3(x=0,y=0,z=0) #Da sistemare
    ##task_plan_sketchpad: 


# -------------------------
# Agente LangGraph
# -------------------------

class AdamAgent:
    def __init__(
        self,
        provider: str,
        model_name: str,
        llm_config_path: Union[str, Path],
        checkpointer_config_path: Union[str, Path],
        system_prompt_template: str,
        fmt_game_obj_prompt: Runnable=fmt_game_obj_prompt_sem,
        fmt_spatial_points_prompt: Runnable=fmt_spatial_points_prompt,
        fmt_agent_location_prompt: Runnable=fmt_agent_location_prompt,
        msg_window_size: int = 5,
        tools_local: Optional[List[Runnable]] = None,
        tools_mcp_url: Optional[str] = None,  # str = URL MCP
        tools_parallel:bool=False,
        tool_choice:Union[str,bool]="auto",
        append_continue_message_after_tool:bool=False, #Test per controllare di piu il comportamento del modello, test fallito per ora
        recursion_limit:int=50, #25 tool message e 25 tool response (conta ogni nodo)
        verbose: bool = False,
        debug:bool = False
    ):
        self.verbose = verbose
        self.debug=debug
        self.msg_window_size = msg_window_size
        self.system_prompt_template = system_prompt_template
        self.recursion_limit=recursion_limit
        self.append_continue_message_after_tool=append_continue_message_after_tool

        #Function to format the perceptions
        self.fmt_game_obj_prompt=fmt_game_obj_prompt
        self.fmt_spatial_points_prompt=fmt_spatial_points_prompt
        self.fmt_agent_location_prompt=fmt_agent_location_prompt

        # 1) LLM
        llm_config = LlmManagerConfig.from_yaml(llm_config_path)
        self.llm = LlmManager(llm_config).get_model(provider, model_name)

        # --- Capabilities dal YAML ---
        model_cfg = llm_config.providers[provider].config.models[model_name].config
        cap_support_parallel = bool(getattr(model_cfg, "support_tools_parallel", False))
        cap_support_tool_choice = bool(getattr(model_cfg, "support_tool_choice", False))

        # --- Valori richiesti dallo sviluppatore (li manteniamo per logging) ---
        self.tools_parallel_requested = bool(tools_parallel)
        self.tool_choice_requested = tool_choice  # può essere bool o string ("none" | "auto" | "required")

        # --- Valori EFFETTIVI applicando le capability del modello ---
        self.tools_parallel = self.tools_parallel_requested and cap_support_parallel

        # tool_choice: se non supportato → False; altrimenti prendi esattamente il valore passato
        self.tool_choice = (False if not cap_support_tool_choice else self.tool_choice_requested)

        # 2) Prompt
        self.prompt_template = ChatPromptTemplate.from_messages([
            SystemMessagePromptTemplate.from_template(self.system_prompt_template),
            MessagesPlaceholder(variable_name="messages")
        ])

        # 3) Risolvi tools (URL MCP -> lista BaseTool)

        self.tools_local = tools_local if tools_local != None else []
        self.tools_mcp= load_mcp_tools_from_url_sync(tools_mcp_url) if tools_mcp_url != None else []
        self.tools=self.tools_local+self.tools_mcp
        # 4) bind_tools solo DOPO la risoluzione
        
        self.chat_chain=self._build_chat_chain()

        # 5) Checkpointer + Graph
        ckpt_config = CheckpointerConfig.from_yaml(checkpointer_config_path)
        self.checkpointer = forge_checkpointer(ckpt_config)
        self.graph: CompiledStateGraph = self._init_graph()

        #Instruemntations
        self._t0_by_ai_id={}

         # --- Print configurazione (solo se verbose) ---
        if self.verbose:
            self._print_adam_config(
                llm_config=llm_config,
                provider=provider,
                model_name=model_name,
                model_cfg=model_cfg,
                tools_mcp_url=tools_mcp_url,
                cap_support_parallel=cap_support_parallel,
                cap_support_tool_choice=cap_support_tool_choice,
            )


     # -------------------------
    # Private: pretty config log
    # -------------------------
    def _print_adam_config(
        self,
        llm_config,
        provider: str,
        model_name: str,
        model_cfg,
        tools_mcp_url: Optional[str],
        cap_support_parallel: bool,
        cap_support_tool_choice: bool,
    ) -> None:
        print("🛠️ [AdamAgent Configuration]")
        print(f"🔹 Provider          : {provider}-{llm_config.providers[provider].type}")
        print(f"🔹 Model             : {model_name}-{getattr(model_cfg, 'name', model_name)}")
        print(f"🔹 LLM Config Path   : {llm_config.source_path if hasattr(llm_config, 'source_path') else 'n/a'}")
        # Se hai la path come argomento, puoi passarlo e stamparlo qui:
        # print(f"🔹 Checkpointer Path : {checkpointer_config_path}")
        print(f"🔹 Msg Window Size   : {self.msg_window_size}")

        # Tools list
        print(f"🔹 Tools Local       : {[t.name for t in self.tools_local]}")
        print(f"🔹 Tools MCP         : url: {tools_mcp_url} - tools: {[t.name for t in self.tools_mcp]}")

        # Parallel — richiesto vs effettivo
        if self.tools_parallel_requested and not cap_support_parallel:
            print("🔹 Tools Parallel    : requested=ON → effective=OFF (model not supported)")
        else:
            print(f"🔹 Tools Parallel    : {'ON' if self.tools_parallel else 'OFF'}")

        # Tool choice — richiesto vs effettivo (OFF se non supportato)
        def _is_off(val) -> bool:
            return (val is False) or (val is None) or (isinstance(val, str) and val.strip().lower() == "none")

        if not cap_support_tool_choice:
            req_str = repr(self.tool_choice_requested)
            print(f"🔹 Tool Choice       : requested={req_str} → effective=OFF (model not supported)")
        else:
            if _is_off(self.tool_choice):
                print("🔹 Tool Choice       : OFF")
            else:
                print(f"🔹 Tool Choice       : {repr(self.tool_choice)}")

        print("🔹 Prompt Template   :")
        print([msg.pretty_print() for msg in self.prompt_template.messages])

        if self.tools:
            print("🔹 Available tools:")
            for t in self.tools:
                args_schema_name = getattr(t.args_schema, "__name__", str(t.args_schema))
                print(f"   • Name: {t.name}; Desc: {t.description}; Args: {args_schema_name}")

        print("🔹 LangGraph Topology:")
        print(self.graph.get_graph().draw_ascii())
        print("=" * 60)

    # 1) Usa perf_counter per tempi robusti
    def _now_ms(self) -> int:
        return time.perf_counter_ns() // 1_000_000



    def _build_chat_chain(self):
        if self.tools:
            #Models that not support tools_parallel should not use this arg
            if self.tools_parallel:
 
                self.llm = self.llm.bind_tools(self.tools,parallel_tool_calls=self.tools_parallel,tool_choice=self.tool_choice)
            else:
                self.llm = self.llm.bind_tools(self.tools,tool_choice=self.tool_choice)#,parallel_tool_calls=False)

        return self.prompt_template | self.llm


    def _build_config(self, thread_id: Optional[str] = None, recursion_limit: int = 20, **kwargs) -> dict:
        cfg = {"recursion_limit": recursion_limit, "configurable":{}}
        if thread_id is not None:
            cfg["configurable"]["thread_id"] = thread_id
        if kwargs:
            cfg["configurable"].update(kwargs)

        return cfg


    def _get_safe_state_for_printing(self, state: AdamState) -> AdamState:
        state_copy = copy.deepcopy(state)
        for msg in state_copy["messages"]:
            if isinstance(msg.content, list):
                for part in msg.content:
                    if isinstance(part, dict) and part.get("type") == "image_url":
                        if "image_url" in part and "url" in part["image_url"]:
                            part["image_url"]["url"] = "placeholder for base64"
                            msg.content[0]["text"]=f"[THIS MESSAGE CONTAINS AN IMAGE] {msg.content[0]['text']}"
            if isinstance(msg,AIMessage) and msg.tool_calls:
                msg.content=f"{msg.content}\n Tool calls:"
                for tc in msg.tool_calls:
                    msg.content+=f"\n\tName: {tc['name']}; Args: {tc['args']}"
        return state_copy

    def _init_graph(self) -> CompiledStateGraph:
        builder = StateGraph(AdamState)
        #Gestione della coda messagi (STM)
        #To do capire come gestire i messaggi dei tool
        builder.add_node("manage_messages", self._manage_messages_node)
        builder.add_node("chat_model", self._chat_model_node)
        builder.set_entry_point("manage_messages")

        builder.add_edge("manage_messages", "chat_model")

        
        if self.tools:
            #Se ci sono tool
            #Qui in base a cosa è tools si può gestire la logica mcp creando un qualche wrapper (vedremo poi)
            builder.add_node("tools",ToolNode(self.tools))

            builder.add_conditional_edges("chat_model",self._should_continue_node,["tools",END]) #il chat model decide se chiamare tool (tool call) o dare risposta finale
            builder.add_edge("tools","manage_messages") #i tool rispondono al chat model che li ha invocati (tool response)
        else:     
            #Se non ci sono tool il chat model dà sempre la risposta finale
            builder.add_edge("chat_model", END)

        return builder.compile(checkpointer=self.checkpointer)
    




    



    def _should_continue_node(self, state: AdamState):
        msgs = state["messages"]
        last = msgs[-1]
        if isinstance(last, AIMessage) and getattr(last, "tool_calls", None):
            self._t0_by_ai_id[last.id] = self._now_ms()
            return "tools"
        return END

        
    # 3) _instrument_tools: misura t1 SOLO quando tutte le tool responses ci sono
    def _instrument_tools(self, state: AdamState) -> list[BaseMessage]:
        msgs = state["messages"]

        # trova l’ultimo AI che ha invocato tool
        ai_idx = next((i for i in range(len(msgs)-1, -1, -1)
                    if isinstance(msgs[i], AIMessage)
                    and getattr(msgs[i], "tool_calls", None)), None)
        if ai_idx is None:
            return []

        ai_msg: AIMessage = msgs[ai_idx]
        t0 = self._t0_by_ai_id.get(ai_msg.id)
        if t0 is None:
            return []

        # raccogli tutti i tool_call_id di quell’AI
        tcalls = ai_msg.additional_kwargs.get("tool_calls", []) if isinstance(ai_msg.additional_kwargs, dict) else []
        call_ids = {tc.get("id") for tc in tcalls if isinstance(tc, dict) and tc.get("id")}

        if not call_ids:
            # niente tool calls reali → niente timing
            self._t0_by_ai_id.pop(ai_msg.id, None)
            return []

        # verifica che per OGNI call_id esista un ToolMessage corrispondente
        have_ids = set()
        for j in range(ai_idx + 1, len(msgs)):
            m = msgs[j]
            if isinstance(m, ToolMessage) and m.tool_call_id:
                have_ids.add(m.tool_call_id)

        if not call_ids.issubset(have_ids):
            # non sono ancora arrivati tutti i ToolMessage → rimanda la misurazione
            return []

        # ok: tutte le risposte ci sono → misura t1 adesso
        t1 = self._now_ms()
        elapsed = t1 - t0

        # aggiorna AIMessage: response_metadata + timings per ciascun tool_call
        edits: list[BaseMessage] = []
        if getattr(ai_msg, "id", None):
            edits.append(RemoveMessage(id=ai_msg.id))

        # clone deep e scrivi nei punti giusti
        rm = dict(ai_msg.response_metadata or {})
        rm["tools_timing_ms"] = {"t0": t0, "t1": t1, "elapsed": elapsed}

        ak = dict(ai_msg.additional_kwargs or {})
        tcalls_new = []
        for tc in tcalls:
            if not isinstance(tc, dict):
                tcalls_new.append(tc)
                continue
            tc = dict(tc)
            timings = dict(tc.get("timings_ms", {}))
            timings.update({"t0": t0, "t1": t1, "elapsed": elapsed})
            tc["timings_ms"] = timings
            tcalls_new.append(tc)
        ak["tool_calls"] = tcalls_new

        edits.append(ai_msg.copy(update={
            "response_metadata": rm,
            "additional_kwargs": ak
        }))

        # consumiamo t0: è one-shot per quel turno AI
        self._t0_by_ai_id.pop(ai_msg.id, None)
        return edits



    def _manage_messages_node(self, state: AdamState) -> AdamState:
        messages = state["messages"]

        if len(messages) <= self.msg_window_size:
            return state

        # 1) Trova il primo HUMAN
        first_human = None
        for m in messages:
            if getattr(m, "type", None) == "human":
                first_human = m
                break

        if first_human is None:
            to_remove = messages[:-self.msg_window_size]
            remove_ops = [RemoveMessage(id=m.id) for m in to_remove]
            return AdamState(messages=remove_ops)

        # 2) Blocchi. Usare ID, non oggetti.
        blocks = []
        used_ids = set()

        for m in messages:
            if m.id in used_ids:
                continue

            # -------- AI with tool_calls --------
            if m.type == "ai" and getattr(m, "tool_calls", None):
                block = [m]
                used_ids.add(m.id)

                for tc in m.tool_calls:
                    call_id = tc.get("id")
                    # cerca la risposta del tool
                    for m2 in messages:
                        if (
                            m2.id not in used_ids
                            and m2.type == "tool"
                            and getattr(m2, "tool_call_id", None) == call_id
                        ):
                            block.append(m2)
                            used_ids.add(m2.id)
                            break
                
                blocks.append(block)
                continue

            # -------- TOOL senza AI precedente --------
            if m.type == "tool" and getattr(m, "tool_call_id", None):
                block = [m]
                used_ids.add(m.id)
                blocks.append(block)
                continue

            # -------- Messaggio singolo --------
            block = [m]
            used_ids.add(m.id)
            blocks.append(block)

        # 3) Trova il blocco del primo HUMAN
        block_with_first_h = None
        for b in blocks:
            if first_human in b:
                block_with_first_h = b
                break

        # 4) Costruzione finestra
        keep_blocks = [block_with_first_h]

        other_blocks = [b for b in blocks if b is not block_with_first_h]

        if self.msg_window_size > 1:
            N = self.msg_window_size - 1
            keep_blocks.extend(other_blocks[-N:])

        # 5) ID da mantenere
        keep_ids = {m.id for block in keep_blocks for m in block}

        # 6) Rimozione
        to_remove = [m for m in messages if m.id not in keep_ids]
        remove_ops = [RemoveMessage(id=m.id) for m in to_remove]

        return AdamState(messages=remove_ops)





    def _chat_model_node(self, state: AdamState) -> AdamState:
        edits = self._instrument_tools(state)  # <-- ora ritorna una lista di messaggi


        now = datetime.now().isoformat(timespec='seconds') + " Format: YYYY-MM-DDTHH:MM:SS"
        game_objects = state.get("game_objects",None) #state["game_objects"] if state["game_objects"] else None
        game_objects_prompt = self.fmt_game_obj_prompt(game_objects)
        points=state.get("spatial_points",None)
        points_prompt=self.fmt_spatial_points_prompt(points)
        agent_location=state.get("agent_location",None)
        agent_location_prompt=fmt_agent_location_prompt(agent_location)
        

        if self.append_continue_message_after_tool:
            #if isinstance(state["messages"][-1],ToolMessage):
            state["messages"]+=[
                HumanMessage(content="Continue"),
                #AIMessage(content="Ok i will call the tool know using my tool calling feature")
                ]
        
        
       
        chat_input = {
            "messages": state["messages"],
            "game_objects": game_objects_prompt,
            "spatial_points": points_prompt,
            "agent_location": agent_location_prompt
        }

        if self.verbose:
            print("=" * 30)
            print("🧠 [Compiled Prompt]")
            safe_chat_input = copy.deepcopy(chat_input)
            safe_chat_input["messages"] = self._get_safe_state_for_printing(state)["messages"]
            print(self.prompt_template.format(**safe_chat_input))

        #self.chat_chain=self._build_chat_chain() Alcune combo di ollama + model richiedono di rebuildare i tool specialmente se si usa tool_choice

        res: AIMessage = self._instrumented_invoke(chat_input) #self.chat_chain.invoke(chat_input)

        if self.verbose:
            print(f"AI: {res.content}")
            print("=" * 30)

        return AdamState(messages=edits + [res]) # credo che non serve game_objects=state["game_objects"])
    
    def _instrumented_invoke(self,chat_input:dict):
        

        t0=time.time_ns() // 1_000_000
        res: AIMessage = self.chat_chain.invoke(chat_input)
        t1=time.time_ns() // 1_000_000

        res.response_metadata["timing_ms"]={"start":t0,"end":t1,"elapsed":t1-t0}
        return res

    def chat(
        self,
        user_input: Optional[str] = None,
        image: Optional[Union[str, Path, bytes, Image.Image]] = None,
        game_objects: Optional[List[UnityGameObject]] = None,
        spatial_points:Optional[List[UnityGameObject]] = None,
        agent_location:Vector3 = None,
        thread_id: str = "default"
    ) -> tuple[AdamState, AIMessage]:
        if image:
            user_message = VisionHumanMessage(text=user_input, image=image)
        elif user_input:
            user_message = HumanMessage(content=user_input)
        else:
            raise ValueError("Devi fornire almeno user_input o image.")

        input_state = AdamState(messages=[user_message], game_objects=game_objects,spatial_points=spatial_points,agent_location=agent_location)
        state = self.graph.invoke(input_state, config=self._build_config(thread_id,self.recursion_limit))
        last = state["messages"][-1]

        # if self.verbose:
        #     print("=" * 30)
        #     print("🧠 [Final State]")
        #     print(self._get_safe_state_for_printing(state))
        #     print("=" * 30)

        return state, last
    
    async def achat(
        self,
        user_input: Optional[str] = None,
        image: Optional[Union[str, Path, bytes, "Image.Image"]] = None,
        game_objects: Optional[List["UnityGameObject"]] = None,
        thread_id: str = "default"
    ) -> tuple["AdamState", AIMessage]:
        """
        Async chat con supporto immagini, game_objects e tool async.
        """
        if image:
            user_message = VisionHumanMessage(text=user_input, image=image)
        elif user_input:
            user_message = HumanMessage(content=user_input)
        else:
            raise ValueError("Devi fornire almeno user_input o image.")

        input_state = AdamState(messages=[user_message], game_objects=game_objects)
        state = await self.graph.ainvoke(input_state, config=self._build_config(thread_id,self.recursion_limit))
        last = state["messages"][-1]

        # if self.verbose:
        #     print("=" * 30)
        #     print("🧠 [Final State] (async)")
        #     print(self._get_safe_state_for_printing(state))
        #     print("=" * 30)

        return state, last


    def list_thread_ids(self) -> list[str]:
        checkpoints = self.checkpointer.list(config=None)
        return list(set([
            cp.config.get("configurable", {}).get("thread_id")
            for cp in checkpoints
            if cp.config.get("configurable", {}).get("thread_id") is not None
        ]))

    def delete_thread(self, thread_id: str) -> None:
        if not self.checkpointer:
            raise RuntimeError("Checkpointer non configurato, impossibile cancellare il thread.")
        self.checkpointer.delete_thread(thread_id)

    def get_checkpoints(self, thread_id: str, limit: Optional[int] = None):
        config = self._build_config(thread_id)
        return list(self.checkpointer.list(config=config, limit=limit))

    def get_state(self, thread_id: str = "default") -> AdamState:
        state = self.graph.get_state(config=self._build_config(thread_id))
        return AdamState(**state)
    

from adam_prompts import forge_adam_system_prompt
from adam_tools import forge_adam_toolkit
from data_classes import AdamConfig,ModelId,CoordinatesTypeId,ObjectIdentifierId
from enum import Enum


def forge_adam(conf: AdamConfig) -> AdamAgent:
    # Normalizza a stringa canonica (accetta Enum o str)
    model_id = conf.model_id.value if isinstance(conf.model_id, Enum) else str(conf.model_id)

    # Provider/Model mapping minimale
    if model_id == ModelId.MIS.value:
        provider, model_name = "hpc_ollama", "vision_tool_chat"
    elif model_id == ModelId.G4O.value:
        provider, model_name = "default_openrouter", "vision_tool_chat_gpt"
    elif model_id == ModelId.S35.value:
        provider, model_name = "default_openrouter", "vision_tool_chat_claude"

    else:
        raise ValueError(f"ModelId '{model_id}' non valido. Possibili: {[e.value for e in ModelId]}")

    # Prompt in base al tipo coordinate
    system_prompt_template = forge_adam_system_prompt(model_id)

    # Tools
    tools = forge_adam_toolkit(conf.tool_host, conf.tool_port, conf.tool_timeout, debug=conf.debug,object_identifier_id=conf.object_identifier_id)


    #ObjectIdentifier

    game_obj_prompt=forge_game_obj_prompt(conf.object_identifier_id)


    # Agent
    return AdamAgent(
        provider,
        model_name,
        llm_config_path=conf.llm_config_path,
        checkpointer_config_path=conf.checkpointer_config_path,
        msg_window_size=conf.msg_window_size,
        system_prompt_template=system_prompt_template,
        fmt_game_obj_prompt=game_obj_prompt,
        fmt_spatial_points_prompt=fmt_spatial_points_prompt,
        tools_local=tools,
        tools_parallel=conf.tools_parallel,
        recursion_limit=conf.recursion_limit,
        verbose=conf.verbose,
    )



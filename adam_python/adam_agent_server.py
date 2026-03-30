from fastapi import FastAPI, HTTPException, Request, Response
from fastapi.responses import JSONResponse
from contextlib import asynccontextmanager
from pydantic import BaseModel
from typing import Any, Optional, Tuple, List
import secrets
import base64
import sys
import os
from datetime import datetime

# --- Local imports ---
from data_classes import *
from adam_agent import AdamAgent,forge_adam

from typing import Literal 

import argparse
import uvicorn

# Setup path
current_dir = os.path.dirname(__file__)
sys.path.insert(0, os.path.join(current_dir, "agentic_forge"))

from adam_utils import load_config,set_env_from_config,read_config_from_env  # importa il tuo loader generico

# ---------------------------------------------------------------------
# Runtime config + globals C:\Users\Alessandro Pecora\.conda\envs\langgchain\python.exe
# ---------------------------------------------------------------------


# Import-time: leggi la CONFIG dalle env (funziona anche in worker uvicorn)
CONFIG = read_config_from_env()
adam: Optional[AdamAgent] = None


# --- FastAPI lifecycle ---
@asynccontextmanager
async def lifespan(app: FastAPI):
    global adam
    print("🚀 Starting server...")

    # Se l'app è avviata con: uvicorn adam_agent_server:app
    # non passa da __main__, quindi costruiamo l'agent con i default.
    if adam is None:
        adam = forge_adam(CONFIG)

    try:
        yield
    finally:
        print("🔌 Shutting down...")


app = FastAPI(lifespan=lifespan)

@app.get("/health")
def health():
    return {"status": "ready"}

# --- Session Handling ---
def get_or_create_user_session(request: Request, response: Response) -> str:
    user_id = request.cookies.get("user_id")
    if not user_id:
        user_id = secrets.token_hex(16)
        response.set_cookie(key="user_id", value=user_id, httponly=True)
    return user_id


# --- Text-only Inference ---
@app.post("/text_inference", response_model=InferenceResponse)
def text_inference(request: Request, response: Response, input_data: TextInferenceInput):
    try:
        user_id = get_or_create_user_session(request, response)
        print(f"[TEXT INFERENCE] user_id={user_id}, thread_id={input_data.thread_id}, message={input_data.message}")

        state, ai_message = adam.chat(
            user_input=input_data.message,
            image=None,
            game_objects=None,
            thread_id=input_data.thread_id
        )

        return InferenceResponse(thread_id=input_data.thread_id, response=str(ai_message.content))

    except Exception as e:
        import traceback; traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))


# --- Vision Inference (image + text) ---
@app.post("/vision_inference", response_model=InferenceResponse)
def vision_inference(request: Request, response: Response, input_data: VisionInferenceInput):
    try:
        user_id = get_or_create_user_session(request, response)
        print(f"[VISION INFERENCE] user_id={user_id}, thread_id={input_data.thread_id}")

        state, ai_message = adam.chat(
            user_input=input_data.message,
            image=input_data.image_base64,
            game_objects=None,
            thread_id=input_data.thread_id
        )

        return InferenceResponse(thread_id=input_data.thread_id, response=str(ai_message.content))

    except Exception as e:
        import traceback; traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/agent_inference", response_model=InferenceResponse)
def agent_inference(request: Request, response: Response, input_data: AgentInput):
    try:
        user_id = get_or_create_user_session(request, response)
        print(f"[AGENT INFERENCE] user_id={user_id}, thread_id={input_data.thread_id}, message={input_data.message}, agent_location={input_data.agent_location}")

        state, ai_message = adam.chat(
            user_input="[HUMAN] "+ input_data.message,
            image=input_data.image_base64,
            game_objects=input_data.game_objects,
            agent_location=input_data.agent_location,
            spatial_points=input_data.spatial_points,
            thread_id=input_data.thread_id

        )

        return InferenceResponse(thread_id=input_data.thread_id, response=str(ai_message.content))

    except Exception as e:
        import traceback; traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/get_threads", response_model=GetUserThreadsResponse)
def get_threads(request: Request, response: Response):
    try:
        user_id = get_or_create_user_session(request, response)
        print(f"[GET THREADS] user_id={user_id}")

        thread_ids = adam.list_thread_ids()

        print(f"[GET THREADS] threads={thread_ids}")
        return GetUserThreadsResponse(threads=thread_ids)

    except Exception as e:
        import traceback; traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/delete_thread", response_model=BaseResponse)
def delete_thread(request: Request, response: Response, delete_input: DeleteUserThreadRequest):
    try:
        user_id = get_or_create_user_session(request, response)
        print(f"[DELETE THREAD] user_id={user_id}, thread_id={delete_input.thread_id}")
        adam.delete_thread(delete_input.thread_id)
        return BaseResponse(code=200, message=f"Deleted thread {delete_input.thread_id}")
    except Exception as e:
        import traceback; traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/get_checkpoints", response_model=GetCheckpointsResponse)
def get_checkpoints(request: Request, response: Response, input_data: GetCheckpointsRequest):
    try:
        user_id = get_or_create_user_session(request, response)

        checkpoints = adam.get_checkpoints(input_data.thread_id, limit=input_data.limit)
        print(checkpoints)

        return GetCheckpointsResponse(
            thread_id=input_data.thread_id,
            checkpoints=checkpoints
        )

    except Exception as e:
        import traceback; traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/logout", response_model=BaseResponse)
def logout(request: Request, response: Response):
    user_id = request.cookies.get("user_id")
    if user_id:
        response.delete_cookie("user_id")
        print(f"[LOGOUT] user_id={user_id}")
    else:
        print("[LOGOUT] No session found.")
    return BaseResponse(code=200, message="Logout effettuato con successo. [mocked]")





# --- Main ---
if __name__ == "__main__":
    # 1) Carica la config dal modello (CLI > ENV > default)

    cfg: AdamConfig = load_config(AdamConfig)

    # 2) Esporta TUTTI i campi della config in ENV usando le chiavi `env` dei campi Pydantic


    set_env_from_config(cfg)

    # 3) Log di lancio
    print("[LAUNCH CONFIG]", cfg.model_dump())

    # 4) Avvio server (i worker uvicorn rileggeranno le ENV e ricostruiranno la config all'import)
    uvicorn.run(f"{cfg.agent_server_app}:app", host=cfg.agent_host, port=cfg.agent_port, reload=cfg.agent_server_reload)


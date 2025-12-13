from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import List, Dict, Any

from rl_core import (
    initialize_model,
    get_action as rl_get_action,
    store_transition as rl_store_transition,
    training_stats,
    status,
)

app = FastAPI()


class GameState(BaseModel):
    state: List[float]


class StepData(BaseModel):
    state: List[float]
    action: List[int]
    reward: float
    next_state: List[float]
    done: bool


class InitConfig(BaseModel):
    boss_name: str
    observation_size: int


@app.post("/initialize")
def initialize(config: InitConfig) -> Dict[str, Any]:
    """Initialize the model with boss-specific configuration."""
    print(f"[API] Initializing for boss: {config.boss_name} with observation size: {config.observation_size}")
    return initialize_model(config.observation_size, config.boss_name)


@app.post("/get_action")
def get_action(gs: GameState) -> Dict[str, List[int]]:
    try:
        return {"action": rl_get_action(gs.state)}
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))


@app.post("/store_transition")
def store_transition(sd: StepData) -> Dict[str, bool]:
    try:
        rl_store_transition(sd.state, sd.action, sd.reward, sd.next_state, sd.done)
        return {"stored": True}
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))


@app.get("/training_stats")
def get_training_stats() -> Dict[str, Any]:
    """Get current training statistics"""
    return training_stats()


@app.get("/status")
def get_status() -> Dict[str, Any]:
    """Get API and model status"""
    return status()



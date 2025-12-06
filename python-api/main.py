from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import List, Optional, Dict, Any, Tuple
import numpy as np
from gymnasium import spaces
import uvicorn
import gymnasium as gym
import os

from sb3_ppo_override import CustomPPO

app = FastAPI()

obs_dim: Optional[int] = None
model: Optional[CustomPPO] = None
current_boss: Optional[str] = None


# Dummy env to keep SB3 happy
class DummyEnv(gym.Env):
    def __init__(self, obs_size: int) -> None:
        super().__init__()
        self.obs_size = obs_size
        self.observation_space = spaces.Box(0.0, 1.0, shape=(obs_size,), dtype=np.float32)
        self.action_space = spaces.MultiDiscrete([3, 3, 2, 2])

    def reset(
        self,
        *,
        seed: Optional[int] = None,
        options: Optional[Dict[str, Any]] = None
    ) -> Tuple[np.ndarray, Dict[str, Any]]:
        return np.zeros(self.obs_size, dtype=np.float32), {}

    def step(
        self,
        action: np.ndarray
    ) -> Tuple[np.ndarray, float, bool, bool, Dict[str, Any]]:
        obs = np.zeros(self.obs_size, dtype=np.float32)
        reward = 0.0
        done = True
        info = {}
        return obs, reward, done, False, info


def initialize_model(obs_size: int, boss_name: str) -> CustomPPO:
    """Initialize the PPO model with the given observation size and boss name."""
    global model, obs_dim, current_boss
    
    obs_dim = obs_size
    current_boss = boss_name
    
    # Normalize boss name for file paths (lowercase with underscores)
    normalized_boss_name = boss_name.replace(" ", "_").lower()
    
    checkpoint_path = f"models/{normalized_boss_name}/checkpoint.zip"
    if os.path.exists(checkpoint_path):
        print(f"[API] Loading checkpoint for {boss_name}: {checkpoint_path}")
        model = CustomPPO.load(
            checkpoint_path,
            env = DummyEnv(obs_size),
            device="cpu",
        )
        print(f"[API] Loaded checkpoint for {boss_name} with observation size: {obs_size}")
    else:
        print(f"[API] No checkpoint found for {boss_name}, initializing fresh model")
        model = CustomPPO(
            "MlpPolicy", 
            DummyEnv(obs_size),
            boss_name=normalized_boss_name,
            verbose=1,
            n_steps=4096,
            batch_size=1024,
            learning_rate=3e-4,
            ent_coef=0.01,
            clip_range=0.2,
            n_epochs=10,
            gamma=0.99,
            gae_lambda=0.95,
            max_grad_norm=0.5,
            policy_kwargs=dict(
                net_arch=[256, 256, 128]
            )
        )
        print(f"[API] Model initialized for {boss_name} with observation size: {obs_size}")
    
    return model


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
    global model, obs_dim, current_boss
    
    print(f"[API] Initializing for boss: {config.boss_name} with observation size: {config.observation_size}")
    initialize_model(config.observation_size, config.boss_name)
    
    # Normalize boss name for path check
    normalized_boss_name = config.boss_name.replace(" ", "_").lower()

    return {
        "initialized": True,
        "boss_name": config.boss_name,
        "observation_size": obs_dim,
        "checkpoint_loaded": os.path.exists(f"models/{normalized_boss_name}/checkpoint.zip")
    }


@app.post("/get_action")
def get_action(gs: GameState) -> Dict[str, List[int]]:
    global model, obs_dim
    
    if model is None:
        raise HTTPException(
            status_code=400,
            detail="Model not initialized. Call /initialize endpoint first with boss_name and observation_size."
        )
    
    if len(gs.state) != obs_dim:
        raise HTTPException(
            status_code=400,
            detail=f"Expected observation size {obs_dim}, got {len(gs.state)}"
        )
    
    obs = np.array(gs.state, dtype=np.float32)
    action, _ = model.predict(obs, deterministic=False)
    return {"action": action.tolist()}


@app.post("/store_transition")
def store_transition(sd: StepData) -> Dict[str, bool]:
    global model, obs_dim
    
    if model is None:
        raise HTTPException(
            status_code=400,
            detail="Model not initialized. Call /initialize endpoint first."
        )
    
    if len(sd.state) != obs_dim or len(sd.next_state) != obs_dim:
        raise HTTPException(
            status_code=400,
            detail=f"Expected observation size {obs_dim}, got state={len(sd.state)}, next_state={len(sd.next_state)}"
        )
    
    model.store_transition(sd.state, sd.action, sd.reward, sd.next_state, sd.done)
    return {"stored": True}


@app.get("/training_stats")
def get_training_stats() -> Dict[str, Any]:
    """Get current training statistics"""
    return {
        "times_trained": model.times_trained if model and hasattr(model, 'times_trained') else 0,
        "num_timesteps": model.num_timesteps if model and hasattr(model, 'num_timesteps') else 0,
        "observation_size": obs_dim,
        "initialized": model is not None,
    }

@app.get("/status")
def get_status() -> Dict[str, Any]:
    """Get API and model status"""
    return {
        "initialized": model is not None,
        "observation_size": obs_dim,
        "current_boss": current_boss,
        "ready": model is not None and obs_dim is not None,
    }



if __name__ == "__main__":
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
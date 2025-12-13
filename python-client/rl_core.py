import os
from typing import Optional, Dict, Any, List, Tuple

import gymnasium as gym
import numpy as np
from gymnasium import spaces

from sb3_ppo_override import CustomPPO

model: Optional[CustomPPO] = None
obs_dim: Optional[int] = None
current_boss: Optional[str] = None


class DummyEnv(gym.Env):
    """Minimal env to satisfy SB3; matches previous definitions."""

    def __init__(self, obs_size: int) -> None:
        super().__init__()
        self.obs_size = obs_size
        self.observation_space = spaces.Box(0.0, 1.0, shape=(obs_size,), dtype=np.float32)
        self.action_space = spaces.MultiDiscrete([3, 3, 2, 2])

    def reset(
        self,
        *,
        seed: Optional[int] = None,
        options: Optional[Dict[str, Any]] = None,
    ) -> Tuple[np.ndarray, Dict[str, Any]]:
        return np.zeros(self.obs_size, dtype=np.float32), {}

    def step(self, action: np.ndarray) -> Tuple[np.ndarray, float, bool, bool, Dict[str, Any]]:
        obs = np.zeros(self.obs_size, dtype=np.float32)
        reward = 0.0
        done = True
        info = {}
        return obs, reward, done, False, info


def normalize_boss_name(boss_name: str) -> str:
    return boss_name.replace(" ", "_").lower()


def initialize_model(obs_size: int, boss_name: str) -> Dict[str, Any]:
    """Initialize or load model; returns metadata about the initialization."""
    global model, obs_dim, current_boss

    obs_dim = obs_size
    current_boss = boss_name

    normalized_boss_name = normalize_boss_name(boss_name)
    checkpoint_path = f"models/{normalized_boss_name}/checkpoint.zip"

    if os.path.exists(checkpoint_path):
        print(f"[RLCore] Loading checkpoint: {checkpoint_path}")
        model = CustomPPO.load(
            checkpoint_path,
            env=DummyEnv(obs_size),
            device="cpu",
        )
        checkpoint_loaded = True
    else:
        print(f"[RLCore] No checkpoint found, initializing fresh model")
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
            policy_kwargs=dict(net_arch=[256, 256, 128]),
        )
        checkpoint_loaded = False

    return {
        "initialized": True,
        "boss_name": boss_name,
        "observation_size": obs_dim,
        "checkpoint_loaded": checkpoint_loaded,
    }


def get_action(state: List[float]) -> List[int]:
    if model is None:
        raise ValueError("Model not initialized")
    if len(state) != obs_dim:
        raise ValueError(f"Expected obs size {obs_dim}, got {len(state)}")

    obs = np.array(state, dtype=np.float32)
    action, _ = model.predict(obs, deterministic=False)
    return action.tolist()


def store_transition(state: List[float], action: List[int], reward: float, next_state: List[float], done: bool) -> None:
    if model is None:
        raise ValueError("Model not initialized")
    if len(state) != obs_dim or len(next_state) != obs_dim:
        raise ValueError("Observation size mismatch")

    model.store_transition(state, action, reward, next_state, done)


def training_stats() -> Dict[str, Any]:
    return {
        "times_trained": model.times_trained if model and hasattr(model, "times_trained") else 0,
        "num_timesteps": model.num_timesteps if model and hasattr(model, "num_timesteps") else 0,
        "observation_size": obs_dim,
        "initialized": model is not None,
    }


def status() -> Dict[str, Any]:
    return {
        "initialized": model is not None,
        "observation_size": obs_dim,
        "current_boss": current_boss,
        "ready": model is not None and obs_dim is not None,
    }


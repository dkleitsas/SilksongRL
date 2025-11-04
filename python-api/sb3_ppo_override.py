import os
import numpy as np
import torch
from stable_baselines3 import PPO




class CustomPPO(PPO):

    def __init__(self, *args, boss_name=None, **kwargs):
        super().__init__(*args, **kwargs)

        self.last_done = False
        self.times_trained = 0
        self.boss_name = boss_name

        # Initilalize logger or SB3 complains
        if not hasattr(self, '_logger') or self._logger is None:
            from stable_baselines3.common.logger import configure
            self._logger = configure()

        # Only reset buffer if it exists (it won't exist during .load())
        if hasattr(self, 'rollout_buffer') and self.rollout_buffer is not None:
            self.rollout_buffer.reset()
    
    @property
    def logger(self):
        return self._logger

    def start_new_rollout(self):
        self.rollout_buffer.reset()

    def store_transition(self, obs, action, reward, next_obs, done):

        obs = np.array(obs, dtype=np.float32)
        action = np.array(action, dtype=np.int32)
        next_obs = np.array(next_obs, dtype=np.float32)
        
        obs_t = torch.as_tensor(obs).float().unsqueeze(0).to(self.device)
        action_t = torch.as_tensor(action).unsqueeze(0).to(self.device)

        with torch.no_grad():
            dist = self.policy.get_distribution(obs_t)
            value = self.policy.predict_values(obs_t)
            log_prob = dist.log_prob(action_t)
            if log_prob.dim() > 1:
                log_prob = log_prob.sum(-1)

        self.rollout_buffer.add(
            obs=obs,
            action=action,
            reward=reward,
            episode_start=self.last_done,
            value=value.squeeze(),
            log_prob=log_prob.squeeze(),
        )
        
        self.last_done = done
        
        if self.rollout_buffer.pos >= self.n_steps:
            self.finish_rollout_and_train(next_obs)

    def finish_rollout_and_train(self, next_obs):
        with torch.no_grad():
            last_values = self.policy.predict_values(
                torch.as_tensor(next_obs).float().unsqueeze(0).to(self.device)
            )

        self.rollout_buffer.compute_returns_and_advantage(
            last_values=last_values, dones=np.array([False])
        )

        self.train()
        self.rollout_buffer.reset()
        
        self.times_trained += 1
        
        if self.times_trained % 100 == 0:
            save_dir = f"models/{self.boss_name}"
            os.makedirs(save_dir, exist_ok=True)
            save_path = f"{save_dir}/checkpoint"
            self.save(save_path)
            print(f"[CustomPPO] Checkpoint saved to {save_path}.zip at iteration {self.times_trained}")


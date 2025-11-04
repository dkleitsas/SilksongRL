# SilksongRL

Reinforcement learning system for training AI agents to play Hollow Knight: Silksong boss encounters.

## Overview

This project combines a Unity mod with a Python-based RL training pipeline to teach agents how to fight bosses using PPO (Proximal Policy Optimization). Still working on extending this to other RL algorithms.

**Components:**
- **unity-mod/** - BepInEx mod that hooks into Silksong, captures game state, and executes agent actions
- **python-api/** - FastAPI server that runs training for models and provides action predictions

## Architecture

The Unity mod communicates with the Python API via HTTP:
1. Game state (observations) is sent from Unity to the Python API
2. The trained model predicts actions based on the current state
3. Actions are executed in-game and rewards are calculated
4. Training data is collected for model improvement

## TO DO

This is very much so still a work in progress and there is a lot to be done. Will be extending this readme asap with more details on just about everything. Will also add a running/building step by step.


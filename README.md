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

## Set up Instructions

### Prerequisites

- **Hollow Knight: Silksong** (game installation)
- **BepInEx 5.4.x** in your Silksong directory (https://www.nexusmods.com/hollowknightsilksong/mods/26)
- **Debug Mod** in your BepInEx plugins folder (https://www.nexusmods.com/hollowknightsilksong/mods/2)
- **.NET Framework 4.7.2** 
- **Build system that supports MSBuild projects** (e.g. Visual Studio)
- **Python 3.11**

### Building the Unity Mod

1. **Configure your game directory:**
   ```bash
   cd unity-mod/SilksongRL

   # PowerShell/Unix:
   cp SilksongRL.csproj.user.example SilksongRL.csproj.user
   # CMD:
   copy SilksongRL.csproj.user.example SilksongRL.csproj.user
   ```

2. **Edit `SilksongRL.csproj.user`** and set your game installation path:
   ```xml
   <GameDir>YOUR_PATH_HERE\Hollow Knight Silksong</GameDir>
   ```
   Path Examples:
   - Steam (Windows): `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong`
   - Steam (custom drive): `D:\Steam\steamapps\common\Hollow Knight Silksong`
   - GOG: `C:\GOG Games\Hollow Knight Silksong`
   - Epic Games: `C:\Program Files\Epic Games\Hollow Knight Silksong`

3. **Build the project:**
   
   In Visual Studio:
   - Open `unity-mod/SilksongRL.sln`
   - Build Solution (Ctrl+Shift+B)


4. **Install the mod:**
   - Copy the built `SilksongRL.dll` from `unity-mod/SilksongRL/bin/Debug/` (or `bin/Release/` if you built in Release configuration) to your game's `BepInEx/plugins/` directory

### Setting Up the Python API

1. **Navigate to the Python API directory:**
   ```bash
   cd python-api
   ```

2. **Create a virtual environment (recommended):**
   ```bash
   python -m venv venv
   venv\Scripts\activate # On Unix: source venv/bin/activate
   ```

3. **Install dependencies:**
   ```bash
   pip install -r requirements.txt
   ```

4. **Run the API server:**
   ```bash
   python main.py
   ```

### Running the System

1. Start the Python API server (as described above)
2. Launch Hollow Knight: Silksong with BepInEx and the SilksongRL mod installed
3. The mod will automatically connect to the Python API at `http://localhost:8000`
4. Navigate to a supported boss encounter in-game and set your save state in the arena through the Debug mod
5. Press P to hand over control to the agent.

Note:
Boss fight triggers are different from boss to boss so you might need to check {Boss}Encounter.cs to figure out how to actually begin the fight. For now you may need to manually begin the first episode and then hand over control. After that the training should continue on its own. This is a limitation of relying on the Debug mode for resetting. Removing this dependency entirely and setting up a custom reset system is in future plans.

## TO DO

- Set up periodic testing rounds or at least a testing option/endpoint (unsure about this one)
- Add dedicated README files to the python API and unity mod components for more detailed information
- Set up custon reset system to remove dependency on Debug mod and for more flexibility
- Set up custom battle trigger system to streamline training (not sure if possible)
- Add more boss encounters (Lace 2, Second Sentinel might be good ideas to consider)
- Add more RL algorithms



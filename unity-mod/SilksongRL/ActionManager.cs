using HarmonyLib;
using UnityEngine;

namespace SilksongRL
{
    public enum MoveDirection { None = 0, Left = 1, Right = 2 }
    public enum LookDirection { None = 0, Up = 1, Down = 2 }

    public class Action
    {
        public MoveDirection move;
        public LookDirection look;
        public bool jump;
        public bool attack;

        public Action()
        {
            move = MoveDirection.None;
            look = LookDirection.None;
            jump = false;
            attack = false;
        }
    }

    // Just a basic manager class to convert between Action and int[]
    // Will probably have to change to be more flexible in the future
    // for encounters where more abilities are required
    public class ActionManager
    {
    
        public static Action ArrayToAction(ActionResponse response)
        {
            if (response.action == null || response.action.Length != 4)
            {
                Debug.LogError("[ActionManager] Invalid action array received from API");
                return new Action();
            }
            
            return new Action
            {
                move = (MoveDirection)response.action[0],
                look = (LookDirection)response.action[1],
                jump = response.action[2] == 1,
                attack = response.action[3] == 1
            };
        }

        public static int[] ActionToArray(Action action)
        {
            return new int[]
            {
            (int)action.move,
            (int)action.look,
            action.jump ? 1 : 0,
            action.attack ? 1 : 0
            };
        }


    }


    // A Harmony patch to get the key pressed by the agent
    // and inject it into the game
    [HarmonyPatch(typeof(Input), "GetKey", typeof(KeyCode))]
    public static class GetKeyPatch
    {
        public static bool Prefix(KeyCode key, ref bool __result)
        {
            if (!RLManager.isAgentControlEnabled)
                return true;

            var action = RLManager.currentAction;
            if (action == null)
                return true;

            switch (key)
            {
                case KeyCode.LeftArrow:
                    __result = action.move == MoveDirection.Left;
                    return false;
                case KeyCode.RightArrow:
                    __result = action.move == MoveDirection.Right;
                    return false;
                case KeyCode.UpArrow:
                    __result = action.look == LookDirection.Up;
                    return false;
                case KeyCode.DownArrow:
                    __result = action.look == LookDirection.Down;
                    return false;
                case KeyCode.Z:
                    __result = action.jump;
                    return false;
                case KeyCode.X:
                    __result = action.attack;
                    return false;
            }

            return true;
        }
    }

    // F5 key down patch, for some reason this would not work with the GetKey patch
    // I assume it has to do with how the Debug Mod handles resetting
    [HarmonyPatch(typeof(Input), "GetKeyDown", typeof(KeyCode))]
    public static class GetKeyDownPatch
    {
        public static bool Prefix(KeyCode key, ref bool __result)
        {
            if (!RLManager.isAgentControlEnabled)
                return true;

            if (key == KeyCode.F5)
            {
                if (RLManager.simulateF5Press)
                {
                    __result = true;
                    return false;
                }
            }

            return true;
        }
    }

}
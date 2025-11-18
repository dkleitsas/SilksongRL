using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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
                RLManager.StaticLogger?.LogError("[ActionManager] Invalid action array received from API");
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


    // Harmony patch for the new Unity Input System
    // Patches ButtonControl.isPressed to intercept key presses
    [HarmonyPatch(typeof(ButtonControl), "get_isPressed")]
    public static class ButtonControlPatch
    {
        public static bool Prefix(ButtonControl __instance, ref bool __result)
        {
            if (!RLManager.isAgentControlEnabled)
                return true;

            var action = RLManager.currentAction;
            if (action == null)
                return true;

            // Get the key name from the control path
            string keyName = __instance.name;

            switch (keyName)
            {
                case "leftArrow":
                    __result = action.move == MoveDirection.Left;
                    return false;
                case "rightArrow":
                    __result = action.move == MoveDirection.Right;
                    return false;
                case "upArrow":
                    __result = action.look == LookDirection.Up;
                    return false;
                case "downArrow":
                    __result = action.look == LookDirection.Down;
                    return false;
                case "z":
                    __result = action.jump;
                    return false;
                case "x":
                    __result = action.attack;
                    return false;
                /*
                case "f5":
                    if (RLManager.simulateF5Press)
                    {
                        __result = true;
                        return false;
                    }
                    break;
                */
            }

            return true;
        }
    }

    /* Debug still uses old input system, so we use the patch below
    keeping this just in case it gets an update later

    [HarmonyPatch(typeof(ButtonControl), "get_wasPressedThisFrame")]
    public static class ButtonControlWasPressedPatch
    {
        public static bool Prefix(ButtonControl __instance, ref bool __result)
        {
            if (!RLManager.isAgentControlEnabled)
                return true;

            string keyName = __instance.name;

            // Handle F5 key down for reset functionality
            if (keyName == "f5")
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
    */

    // Patch over the legacy input system because that's what the
    // Debug Mod uses
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
                    RLManager.simulateF5Press = false;
                    return false;
                }
            }
            
            return true;
        }
    }

}
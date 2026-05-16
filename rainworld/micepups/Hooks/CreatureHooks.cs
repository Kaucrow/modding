using HarmonyLib;
using MicePups.Data;
using System;
using System.Reflection;
using UnityEngine;

namespace MicePups.Hooks
{
    internal class CreatureHooks
    {
        internal static void Apply()
        {
        }

        internal static void Remove()
        {
        }

        [HarmonyPatch]
        private static class Mouse_IVars_Patch
        {
            static MethodBase TargetMethod()
            {
                return typeof(LanternMouse).GetMethod("GenerateIVars",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            [HarmonyPostfix]
            static void Postfix(LanternMouse __instance)
            {
                Console.WriteLine("Called Harmony LanternMouse GenerateIVars");
                Console.WriteLine($"MousePups:{MicePupsManager._pupData}");

                if (/*UnityEngine.Random.value*/ 1.0f > 0.5f)
                {
                    __instance.SetPupData();
                    Console.WriteLine("Mouse is pup");
                }
                else
                {
                    Console.WriteLine("Mouse is not pup");
                }
            }
        }

        [HarmonyPatch(typeof(LanternMouse), nameof(LanternMouse.Update))]
        private static class LanternMouse_Update_Patch
        {   
            [HarmonyPostfix]
            static void Postfix(LanternMouse __instance, bool eu)
            {
                if (__instance.grasps[0] != null)
                {
                    LanternMouseCarryObject(__instance, eu);
                }
            }
        }

        private static void LanternMouseCarryObject(LanternMouse __instance, bool eu)
        {
            if (__instance.graphicsModule != null)
            {
                MouseGraphics mouseGraphics = __instance.graphicsModule as MouseGraphics;
                __instance.grasps[0].grabbedChunk.MoveFromOutsideMyUpdate(eu, __instance.bodyChunks[1].pos);
            }
            else
            {
                __instance.grasps[0].grabbedChunk.MoveFromOutsideMyUpdate(eu, __instance.bodyChunks[1].pos);
            }
            __instance.grasps[0].grabbedChunk.vel = __instance.mainBodyChunk.vel;
            if (Vector2.Distance(__instance.grasps[0].grabbedChunk.pos, __instance.bodyChunks[1].pos) > 100f)
            {
                __instance.grasps[0].Release();
            }
        }
    }
}

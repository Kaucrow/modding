using HarmonyLib;
using MicePups.AI;
using MicePups.Data;
using MicePups.Extensions;
using System;
using System.Reflection;
using UnityEngine;

namespace MicePups.Hooks
{
    internal class CreatureHooks
    {
        internal static void Apply()
        {
            // Hook into InitiateAI instead of the physical creature constructor
            On.AbstractCreature.InitiateAI += AbstractCreature_InitiateAI;
            On.LanternMouse.Update += LanternMouse_Update;
        }

        internal static void Remove()
        {
            On.AbstractCreature.InitiateAI -= AbstractCreature_InitiateAI;
            On.LanternMouse.Update -= LanternMouse_Update;
        }

        private static void AbstractCreature_InitiateAI(
            On.AbstractCreature.orig_InitiateAI orig,
            AbstractCreature self
        )
        {
            // Call the original AI creation method
            orig(self);

            // Check if this abstract creature is a Lantern Mouse
            if (self.creatureTemplate.type == CreatureTemplate.Type.LanternMouse)
            {
                if (self.GetPupData() != null)
                {
                    UnityEngine.Debug.Log("Replacing Vanilla MouseAI with MousePupAI inside InitiateAI");

                    self.abstractAI.RealAI = new MousePupAI(self, self.world);
                }
            }
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
                // Force it to be a pup for testing
                __instance.SetPupData();
                Console.WriteLine("Mouse is pup");
            }
        }

        private static void LanternMouse_Update(
            On.LanternMouse.orig_Update orig,
            LanternMouse self,
            bool eu
        )
        {
            // Call the original method
            orig(self, eu);

            // Check if the mouse has a grasps array and is actually holding something
            if (self.grasps != null && self.grasps.Length > 0 && self.grasps[0] != null)
            {
                // --- PHYSICS CARRY LOGIC ---
                self.grasps[0].grabbedChunk.MoveFromOutsideMyUpdate(eu, self.bodyChunks[0].pos);
                self.grasps[0].grabbedChunk.vel = self.mainBodyChunk.vel;

                // Failsafe: Drop the item if it gets stuck on geometry
                if (Vector2.Distance(self.grasps[0].grabbedChunk.pos, self.bodyChunks[0].pos) > 100f)
                {
                    self.grasps[0].Release();
                }
                else
                {
                    // --- EATING LOGIC ---
                    // In an else block because we don't want to try to eat if the item is stuck on geometry and we're trying to drop it

                    // Check if the held item is edible, and if so, take a bite every 40 frames 
                    if (self.grasps[0].grabbed is IPlayerEdible foodItem)
                    {
                        if (self.room != null && self.room.game.clock % 40 == 0)
                        {
                            // Remember how many bites were left before we take a bite
                            int bitesBefore = foodItem.BitesLeft;

                            try
                            {
                                // If the mouse has a graphics module, trigger the bite animation.
                                if (self.graphicsModule != null)
                                {
                                    (self.graphicsModule as MouseGraphics)?.BiteFly(0);
                                }

                                // Call the BitByPlayer method on the food item.
                                // It will play its proper custom sound and decrement its own bites.
                                // On the final bite, it will throw a NullReferenceException when looking for a Player.
                                foodItem.BitByPlayer(self.grasps[0], eu);
                            }
                            catch
                            {
                                // Silently swallow the exception
                            }

                            // Because the exception aborted the very end of the BitByPlayer method, 
                            // the item never got to run grasp.Release() or this.Destroy(). 
                            // We finish the job manually if it was the last bite.
                            if (bitesBefore == 1)
                            {
                                if (self.grasps[0] != null)
                                {
                                    self.grasps[0].Release();
                                }

                                if (foodItem is PhysicalObject physObj)
                                {
                                    physObj.Destroy();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
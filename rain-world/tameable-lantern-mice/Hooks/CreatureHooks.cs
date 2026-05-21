using HarmonyLib;
using MouseFriends.AI;
using MouseFriends.Data;
using MouseFriends.Extensions;
using System;
using System.Reflection;
using UnityEngine;

namespace MouseFriends.Hooks
{
    internal class CreatureHooks
    {
        internal static void Apply()
        {
            // Hook into InitiateAI instead of the physical creature constructor
            On.AbstractCreature.InitiateAI += AbstractCreature_InitiateAI;
            On.LanternMouse.Update += LanternMouse_Update;
            //On.BodyChunk.MoveFromOutsideMyUpdate += BodyChunk_MoveFromOutsideMyUpdate;
        }

        internal static void Remove()
        {
            On.AbstractCreature.InitiateAI -= AbstractCreature_InitiateAI;
            On.LanternMouse.Update -= LanternMouse_Update;
            //On.BodyChunk.MoveFromOutsideMyUpdate -= BodyChunk_MoveFromOutsideMyUpdate;
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
                if (self.IsMouseFriend())
                {
                    UnityEngine.Debug.Log("Replacing Vanilla MouseAI with MousePupAI inside InitiateAI");

                    self.abstractAI.RealAI = new MouseFriendAI(self, self.world);
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
                // Force it to be a friend for testing
                __instance.SetFriendData();
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
            
            if (self.GetFriendData() is not MouseFriendData friendData) return;

            // Check if the mouse has a grasps array and is actually holding something
            if (self.grasps != null && self.grasps.Length > 0 && self.grasps[0] != null)
            {
                // --- PHYSICS CARRY LOGIC ---
                self.grasps[0].grabbedChunk.MoveFromOutsideMyUpdate(eu, self.bodyChunks[0].pos);
                self.grasps[0].grabbedChunk.vel = self.mainBodyChunk.vel;

                // --- EATING LOGIC ---
                // In an else block because we don't want to try to eat if the item is stuck on geometry and we're trying to drop it

                // Check if the held item is edible, and if so, take a bite every 40 frames 
                if (
                    self.grasps[0].grabbed is IPlayerEdible foodItem && self.room.game.clock % 40 == 0 &&
                    foodItem is PhysicalObject physFood && self.room != null
                )
                {
                    int bitesBefore = foodItem.BitesLeft;

                    // Trigger graphics
                    if (self.graphicsModule != null)
                    {
                        (self.graphicsModule as MouseGraphics)?.BiteFly(0);
                    }

                    bool hitPlayerCastCrash = false;

                    try
                    {
                        // Execute the vanilla method (Plays sounds, changes sprite, decrements bites)
                        foodItem.BitByPlayer(self.grasps[0], eu);
                    }
                    catch (NullReferenceException)
                    {
                        hitPlayerCastCrash = true;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.Log($"Unexpected eating error: {ex}");
                    }

                    // If the item is fully eaten, tame the mouse and destroy the food item
                    if (bitesBefore <= 1 || hitPlayerCastCrash)
                    {
                        self.ObjectEaten(foodItem);

                        if (self.grasps[0] != null)
                        {
                            self.grasps[0].Release();
                        }

                        physFood.Destroy();
                    }
                }
            }
        }
    }
}
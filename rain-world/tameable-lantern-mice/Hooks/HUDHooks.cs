using HUD;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using MouseFriends.Data;
using MouseFriends.Extensions;
using RWCustom;
using System.Collections.Generic;
using UnityEngine;

namespace MouseFriends.Hooks
{
    internal static class HUDHooks
    {
        //private static Hook isPupHook;
        //public delegate bool orig_IsPupFoodMeter(FoodMeter self);

        public static void Apply()
        {
            //MethodInfo isPupGetter = typeof(FoodMeter).GetProperty("IsPupFoodMeter", BindingFlags.Public | BindingFlags.Instance).GetGetMethod();
            //MethodInfo ourDetour = typeof(HUDHooks).GetMethod(nameof(FoodMeter_IsPupFoodMeter_Detour), BindingFlags.NonPublic | BindingFlags.Static);
            //isPupHook = new Hook(isPupGetter, ourDetour);
            
            On.HUD.FoodMeter.CircleDistance += FoodMeter_CircleDistance;
            On.HUD.FoodMeter.TrySpawnPupBars += FoodMeter_TrySpawnPupBars;
            On.HUD.FoodMeter.Update += FoodMeter_Update;
            On.HUD.FoodMeter.Draw += FoodMeter_Draw;
            On.HUD.FoodMeter.MeterCircle.Draw += FoodMeter_MeterCircle_Draw;
            On.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHud;
        }

        public static void Remove()
        {
            //if (isPupHook != null)
            //{
            //    isPupHook.Dispose();
            //    isPupHook = null;
            //}

            On.HUD.FoodMeter.CircleDistance -= FoodMeter_CircleDistance;
            On.HUD.FoodMeter.TrySpawnPupBars -= FoodMeter_TrySpawnPupBars;
            On.HUD.FoodMeter.Update -= FoodMeter_Update;
            On.HUD.FoodMeter.Draw -= FoodMeter_Draw;
            On.HUD.FoodMeter.MeterCircle.Draw -= FoodMeter_MeterCircle_Draw;
            On.HUD.HUD.InitSinglePlayerHud -= HUD_InitSinglePlayerHud;
        }
        
        /*
        private static bool FoodMeter_IsPupFoodMeter_Detour(orig_IsPupFoodMeter orig, FoodMeter self)
        {
            if (MouseFriendMeters.TryGetValue(self, out _))
            {
                return true;
            }
            return orig(self);
        }
        */
    
        private static float FoodMeter_CircleDistance(
            On.HUD.FoodMeter.orig_CircleDistance orig,
            FoodMeter self,
            float timeStacker
        )
        {
            if (self.GetMouseData(out var data))
            {
                return Mathf.Lerp(20f, 15f, data.DeathFade);
            }

            return orig(self, timeStacker);
        }

        private static void FoodMeter_TrySpawnPupBars(On.HUD.FoodMeter.orig_TrySpawnPupBars orig, FoodMeter self)
        {
            // This is only done for the player's food meter
            if (ModManager.MSC && !self.IsMouseFoodMeter() && !self.IsPupFoodMeter && self.pupBars == null)
            {
                if (self.hud.owner is Player mainPlayer && mainPlayer.room != null && mainPlayer.room.game.spawnedPendingObjects)
                {
                    int stackIndex = 1;
                    self.pupBars = new List<FoodMeter>();

                    var roomCreatures = mainPlayer.abstractCreature.Room.creatures;
                    for (int i = 0; i < roomCreatures.Count; i++)
                    {
                        var abstractCrit = roomCreatures[i];

                        // Keep the creature if it is alive
                        if (abstractCrit.state != null && abstractCrit.state.alive)
                        {
                            if (abstractCrit.creatureTemplate.type == MoreSlugcatsEnums.CreatureTemplateType.SlugNPC)
                            {
                                if (abstractCrit.realizedCreature is Player pup)
                                {
                                    FoodMeter pupMeter = new FoodMeter(self.hud, 0, 0, pup, stackIndex);
                                    self.hud.AddPart(pupMeter);
                                    self.pupBars.Add(pupMeter);
                                    stackIndex++;
                                }
                                else if (abstractCrit.realizedCreature is LanternMouse mouse && mouse.IsBefriended())
                                {
                                    // Just increment the stack index for befriended mice, since they will be handled
                                    // by their own separate bars that we add in InitSinglePlayerHud and Update.
                                    stackIndex++;
                                }
                            }
                        }
                    }
                    return; // Skip the original method since we've already added the Slugpup bars
                }
            }

            // Fallback to the original method if conditions aren't met
            orig(self);
        }

        private static void FoodMeter_Update(
            On.HUD.FoodMeter.orig_Update orig,
            FoodMeter self
        )
        {
            // If this is the main player's food meter, monitor the room for newly tamed mice
            if (!self.IsMouseFoodMeter() && self.hud.owner is Player mainPlayer && mainPlayer.room != null)
            {
                foreach (var updateable in mainPlayer.room.updateList)
                {
                    if (updateable is LanternMouse mouse && mouse.IsBefriended())
                    {
                        // This helper handles checking if a bar already exists
                        AddMouseBar(self.hud, mainPlayer, mouse);
                    }
                }
            }

            // State-Swap trick to display the mouse's food by temporarily swapping
            // the player's food values with the mouse's food values.
            if (self.GetMouseData(out var meterData) && meterData.DummyPlayer?.playerState != null)
            {
                if (meterData.Mouse.GetFriendData() is not MouseFriendData friendData) return;

                int currentFood = friendData.FoodInStomach;

                int realPlayerFood = meterData.DummyPlayer.playerState.foodInStomach;
                meterData.DummyPlayer.playerState.foodInStomach = currentFood;

                orig(self);

                meterData.DummyPlayer.playerState.foodInStomach = realPlayerFood;
                
                // Update the death fade based on whether the mouse is currently dead or alive
                if (friendData.Creature.state.dead)
                {
                    meterData.DeathFade = Mathf.Lerp(meterData.DeathFade, 1f, 0.05f);
                    if (meterData.DeathFade > 0.98f)
                    {
                        meterData.DeathFade = 1f;
                    }
                }
                else
                {
                    meterData.DeathFade = Mathf.Lerp(meterData.DeathFade, 0f, 0.1f);
                    if (meterData.DeathFade < 0.02f)
                    {
                        meterData.DeathFade = 0f;
                    }
                }

                // Smoothly move the mouse meter's survival limit and position to match the current food and stack index
                self.MoveSurvivalLimit(Mathf.Lerp((float)self.survivalLimit, (float)currentFood, self.forceSleep), false);
                float num2 = (float)meterData.StackIndex;
                if (self.hud.gourmandmeter != null)
                {
                    num2 += (float)self.hud.gourmandmeter.visibleRows;
                }
                self.pos = Vector2.Lerp(
                    new Vector2(
                        Mathf.Max(50f, self.hud.rainWorld.options.SafeScreenOffset.x + 5.5f),
                        Mathf.Max(25f, self.hud.rainWorld.options.SafeScreenOffset.y + 17.25f) + 5f + 25f * num2
                    ),
                    (
                        self.hud.karmaMeter.pos +
                        new Vector2(0f, 25f * num2) +
                        Custom.DegToVec(Mathf.Lerp(90f, 135f, self.downInCorner)) *
                        (self.hud.karmaMeter.Radius + 22f + Custom.SCurve(Mathf.Pow(self.hud.rainMeter.fade, 0.4f), 0.5f) * 8f)
                    ),
                    Custom.SCurve(1f - self.downInCorner, 0.5f)
                );
            }
            else
            {
                orig(self);
            }
        }

        private static void FoodMeter_Draw(
            On.HUD.FoodMeter.orig_Draw orig,
            FoodMeter self,
            float timeStacker
        )
        {
            // If this is a mouse meter, prevent the dark fade from applying to it
            if (self.IsMouseFoodMeter())
            {
                var darkFade = self.darkFade;

                orig(self, timeStacker);

                self.darkFade = darkFade;
            }
            else
            {
                orig(self, timeStacker);
            }
        }

        private static void FoodMeter_MeterCircle_Draw(
            On.HUD.FoodMeter.MeterCircle.orig_Draw orig,
            FoodMeter.MeterCircle self,
            float timeStacker
        )
        {
            orig(self, timeStacker);

            if (self.meter.GetMouseData(out var data))
            {
                for (int i = 0; i < self.circles.Length; i++)
                {
                    self.circles[i].sprite.element = Futile.atlasManager.GetElementWithName(self.circles[i].snapGraphic.ToString());
                    self.circles[i].sprite.scale = self.circles[i].rad / self.circles[i].snapRad;
                    self.circles[i].sprite.alpha = 1f;
                    self.circles[i].sprite.shader = self.circles[i].basicShader;
                    self.circles[i].sprite.alpha = Mathf.Lerp(self.circles[i].lastFade, self.circles[i].fade, timeStacker);
                    self.circles[i].sprite.color = Custom.FadableVectorCircleColors[self.circles[i].color];
                    self.circles[i].sprite.scale /= 2f;

                    HSLColor color = data.Mouse.iVars.color;
                    self.circles[i].sprite.color = Color.Lerp(
                        Color.Lerp(
                            self.circles[i].sprite.color,
                            Custom.HSL2RGB(
                                color.hue,
                                Mathf.Lerp(color.saturation, 1f, 0.8f),
                                0.7f
                            ),
                            0.5f - (float)self.circles[i].color * 0.5f
                        ),
                        new Color (0.6f, 0.6f, 0.6f),
                        data.DeathFade
                    );
                }
            }
        }

        private static void HUD_InitSinglePlayerHud(
            On.HUD.HUD.orig_InitSinglePlayerHud orig,
            HUD.HUD self,
            RoomCamera cam
        )
        {
            orig(self, cam);
            if (self.owner is not Player mainPlayer || cam.room == null) return;

            // Look for any mice already in the room that are already befriended
            foreach (var updateable in cam.room.updateList)
            {
                if (updateable is LanternMouse mouse && mouse.IsBefriended())
                {
                    AddMouseBar(self, mainPlayer, mouse);
                }
            }
        }

        private static void AddMouseBar(
            HUD.HUD hud,
            Player mainPlayer,
            LanternMouse mouseFriend
        )
        {
            // Prevent duplicate bars for the same mouse
            foreach (var part in hud.parts)
            {
                if (part is FoodMeter existingMeter &&
                    existingMeter.GetMouseData(out var conn) &&
                    conn.Mouse == mouseFriend)
                {
                    return;
                }
            }

            UnityEngine.Debug.Log("Adding new mouse bar");

            int stackIndex = 1;

            // Count how many Slugpup & Mouse meters are already in the HUD to determine the correct stacking position
            foreach (var part in hud.parts)
            {
                if (part is FoodMeter meter && (meter.IsPupFoodMeter || meter.IsMouseFoodMeter()))
                {
                    stackIndex++;
                }
            }

            if (mouseFriend.GetFriendData() is not MouseFriendData friendData) return;

            // Initialize and register the new UI element
            FoodMeter mouseMeter = ConstructMouseFoodMeter(hud, stackIndex);
            mouseMeter.RegisterAsMouseMeter(mouseFriend, mainPlayer, stackIndex);

            {
                mouseMeter.lastCount = friendData.FoodInStomach;
                mouseMeter.NewShowCount(friendData.FoodInStomach);
            }

            hud.AddPart(mouseMeter);
        }

        private static FoodMeter ConstructMouseFoodMeter(HUD.HUD hud, int stackIndex)
        {
            FoodMeter mouseMeter = new FoodMeter(hud, 3, 2, null, stackIndex);
            mouseMeter.lineSprite.scaleX = 1.5f;
            mouseMeter.lineSprite.scaleY = 18.5f;

            return mouseMeter;
        }
    }
}
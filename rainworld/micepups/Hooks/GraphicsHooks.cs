using UnityEngine;
using System;
using HarmonyLib;
using System.Reflection;

namespace MicePups.Hooks
{
    internal static class GraphicsHooks
    {
        internal static void Apply()
        {
            On.MouseGraphics.InitiateSprites += MouseAddMark;
            On.MouseGraphics.DrawSprites += OnDraw;
        }

        internal static void Remove()
        {
            On.MouseGraphics.InitiateSprites -= MouseAddMark;
            On.MouseGraphics.DrawSprites -= OnDraw;
        }

        private static void MouseAddMark(
            On.MouseGraphics.orig_InitiateSprites orig,
            MouseGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam
        )
        {
            //MicePups.Logger.Log("Adding mouse pup mark");
            UnityEngine.Debug.Log("Adding mouse pup mark");

            orig(self, sLeaser, rCam); // Call original first

            if (self.owner is not LanternMouse) return;

            // Resize the sprites array to add space for the mark
            Array.Resize(ref sLeaser.sprites, sLeaser.sprites.Length + 2);
            int markSprite = sLeaser.sprites.Length - 1;        // New index
            int markGlowSprite = sLeaser.sprites.Length - 2;    // New index

            // Create the mark sprite
            sLeaser.sprites[markSprite] = new FSprite("pixel", true)
            {
                color = self.BodyColor,
                alpha = 1f,
                scale = 5f,
            };
            rCam.ReturnFContainer("Foreground").AddChild(sLeaser.sprites[markSprite]);

            // Create the mark glow sprite
            sLeaser.sprites[markGlowSprite] = new FSprite("Futile_White", true)
            {
                shader = rCam.game.rainWorld.Shaders["FlatLight"],
                color = self.BodyColor,
                alpha = 0.5f,
                scale = 1f,
            };
            rCam.ReturnFContainer("Foreground").AddChild(sLeaser.sprites[markGlowSprite]);
        }

        private static void OnDraw(
            On.MouseGraphics.orig_DrawSprites orig,
            MouseGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            float timeStacker,
            Vector2 camPos
        )
        {
            int markSprite = sLeaser.sprites.Length - 1;        // New index
            int markGlowSprite = sLeaser.sprites.Length - 2;    // New index

            int mouseHead = 4;

            // Get interpolated head position (smooth between frames)
            Vector2 headPos = Vector2.Lerp(
                self.bodyParts[mouseHead].lastPos,
                self.bodyParts[mouseHead].pos,
                timeStacker
            );

            // Convert to camera space and add offsets
            sLeaser.sprites[markSprite].x = headPos.x - camPos.x;
            sLeaser.sprites[markSprite].y = (headPos.y - camPos.y) + 50f;

            // Convert to camera space and add offsets
            sLeaser.sprites[markGlowSprite].x = headPos.x - camPos.x;
            sLeaser.sprites[markGlowSprite].y = (headPos.y - camPos.y) + 50f;

            // THEN call the original method
            orig(self, sLeaser, rCam, timeStacker, camPos);

            // Optional post-processing
            if (self.owner is LanternMouse)
            {
                sLeaser.sprites[markSprite].MoveToFront();
            }
        }
    }
}
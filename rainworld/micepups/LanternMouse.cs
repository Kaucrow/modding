using UnityEngine;
using System;
using IL.Stove.Sample.Ownership;
using System.ComponentModel;

namespace MicePupsMod;

public partial class MicePupsMod
{
    private void MouseAddMark(
        On.MouseGraphics.orig_InitiateSprites orig,
        MouseGraphics self,
        RoomCamera.SpriteLeaser sLeaser,
        RoomCamera rCam
    )
    {
        orig(self, sLeaser, rCam); // Call original first

        if (self.owner is not LanternMouse) return;

        // Resize the sprites array to add space for our mark
        Array.Resize(ref sLeaser.sprites, sLeaser.sprites.Length + 2);
        int markSprite = sLeaser.sprites.Length - 1; // New index
        int markGlowSprite = sLeaser.sprites.Length - 2; // New index

        // Create the mark sprite
        sLeaser.sprites[markSprite] = new FSprite("pixel", true) {
            //shader = rCam.game.rainWorld.Shaders["FlatLight"],
            color = self.BodyColor,
            alpha = 1f,
            scale = 5f,
        };
        rCam.ReturnFContainer("Foreground").AddChild(sLeaser.sprites[markSprite]); 

        // Create the mark sprite
        sLeaser.sprites[markGlowSprite] = new FSprite("Futile_White", true) {
            shader = rCam.game.rainWorld.Shaders["FlatLight"],
            color = self.BodyColor,
            alpha = 0.5f,
            scale = 1f,
        };
        rCam.ReturnFContainer("Foreground").AddChild(sLeaser.sprites[markGlowSprite]); 

        Debug.Log($"New LanternMouse leaser created with {sLeaser.sprites.Length} sprites");
        Debug.Log($"The mark's position is: {sLeaser.sprites[markSprite].GetPosition()}");
        Logger.LogInfo($"New LanternMouse leaser created with {sLeaser.sprites.Length} sprites");
        Logger.LogInfo($"The mark's position is: {sLeaser.sprites[markSprite].GetPosition()}");
    }

    private void OnDraw(
        On.MouseGraphics.orig_DrawSprites orig,
        MouseGraphics self,
        RoomCamera.SpriteLeaser sLeaser,
        RoomCamera rCam,
        float timeStacker,
        Vector2 camPos
    )
    {
        int markSprite = sLeaser.sprites.Length - 1; // New index
        int markGlowSprite = sLeaser.sprites.Length - 2; // New index

        int mouseHead = 4;

        // Get interpolated head position (smooth between frames)
        Vector2 headPos = Vector2.Lerp(
            self.bodyParts[mouseHead].lastPos, 
            self.bodyParts[mouseHead].pos, 
            timeStacker
        );

        // Convert to camera space and add offsets
        sLeaser.sprites[markSprite].x = headPos.x - camPos.x; // <- CRUCIAL: Subtract camera position
        sLeaser.sprites[markSprite].y = (headPos.y - camPos.y) + 50f; // 15 pixels above head

        // Convert to camera space and add offsets
        sLeaser.sprites[markGlowSprite].x = headPos.x - camPos.x; // <- CRUCIAL: Subtract camera position
        sLeaser.sprites[markGlowSprite].y = (headPos.y - camPos.y) + 50f; // 15 pixels above head

        // 2. THEN call the original method (including base.DrawSprites)
        orig(self, sLeaser, rCam, timeStacker, camPos);

        // 3. Optional post-processing
        if (self.owner is LanternMouse)
        {
            sLeaser.sprites[markSprite].MoveToFront();
        }

        Logger.LogInfo($"The mark's new position is: {sLeaser.sprites[markSprite].GetPosition()}");
    }
}
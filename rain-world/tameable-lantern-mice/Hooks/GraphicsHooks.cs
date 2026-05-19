using UnityEngine;
using System;
using MouseFriends.AI;

namespace MouseFriends.Hooks
{
    internal static class GraphicsHooks
    {
        private static bool isHooked = false;

        internal static void Apply()
        {
            if (isHooked) return;
            isHooked = true;

            On.MouseGraphics.InitiateSprites += MouseGraphics_InitiateSprites;
            On.MouseGraphics.Update += MouseGraphics_Update;
            On.MouseGraphics.DrawSprites += MouseGraphics_DrawSprites;
            On.RoomCamera.SpriteLeaser.Update += SpriteLeaser_Update;
        }

        internal static void Remove()
        {
            if (!isHooked) return;
            isHooked = false;

            On.MouseGraphics.InitiateSprites -= MouseGraphics_InitiateSprites;
            On.MouseGraphics.Update -= MouseGraphics_Update;
            On.MouseGraphics.DrawSprites -= MouseGraphics_DrawSprites;
            On.RoomCamera.SpriteLeaser.Update -= SpriteLeaser_Update;
        }

        private static void MouseGraphics_InitiateSprites(
            On.MouseGraphics.orig_InitiateSprites orig,
            MouseGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam
        )
        {
            orig(self, sLeaser, rCam);

            if (self.owner is not LanternMouse) return;

            Array.Resize(ref sLeaser.sprites, sLeaser.sprites.Length + 2);
            int markSprite = sLeaser.sprites.Length - 1;
            int markGlowSprite = sLeaser.sprites.Length - 2;

            sLeaser.sprites[markSprite] = new FSprite("pixel", true)
            {
                color = self.BodyColor,
                alpha = 1f,
                scale = 5f,
            };
            rCam.ReturnFContainer("Foreground").AddChild(sLeaser.sprites[markSprite]);

            sLeaser.sprites[markGlowSprite] = new FSprite("Futile_White", true)
            {
                shader = rCam.game.rainWorld.Shaders["FlatLight"],
                color = self.BodyColor,
                alpha = 0.5f,
                scale = 1f,
            };
            rCam.ReturnFContainer("Foreground").AddChild(sLeaser.sprites[markGlowSprite]);
        }

        // Moves the mouse limbs to visually follow the held item
        private static void MouseGraphics_Update(On.MouseGraphics.orig_Update orig, MouseGraphics self)
        {
            orig(self);

            if (self.mouse is LanternMouse mouse && mouse.grasps != null)
            {
                for (int i = 0; i < mouse.grasps.Length; i++)
                {
                    if (mouse.grasps[i] != null && mouse.grasps[i].grabbed != null)
                    {
                        float faceDir = 1f;
                        bool isMoving = Mathf.Abs(mouse.mainBodyChunk.vel.x) > 1f;

                        if (isMoving)
                        {
                            faceDir = Mathf.Sign(mouse.mainBodyChunk.vel.x);
                        }
                        else if (mouse.AI != null && mouse.AI.pathFinder != null)
                        {
                            Vector2 targetTilePos = mouse.room.MiddleOfTile(mouse.AI.pathFinder.GetDestination);
                            if (targetTilePos.x != mouse.mainBodyChunk.pos.x)
                            {
                                faceDir = Mathf.Sign(targetTilePos.x - mouse.mainBodyChunk.pos.x);
                            }
                        }

                        // --- State Checks ---
                        bool isEating = mouse.AI.behavior == MouseFriendAI.Behavior.Eat;
                        bool isDangling = mouse.ropeAttatchedPos != null;
                        bool isClimbing = mouse.room != null && mouse.room.aimap.getAItile(mouse.mainBodyChunk.pos).acc == AItile.Accessibility.Climb;

                        Vector2 offset;

                        if (isEating)
                        {
                            // Snout position
                            offset = new Vector2(faceDir * 9f, -1f);
                        }
                        else if (isDangling)
                        {
                            offset = new Vector2(0f, -12f);     // Hanging
                        }
                        else if (isMoving)
                        {
                            offset = new Vector2(faceDir * 12f, -5f);   // Walking
                        }
                        else
                        {
                            offset = new Vector2(faceDir * 7f, -2f);    // Sitting
                        }

                        Vector2 targetPawPos = mouse.bodyChunks[0].pos + offset;

                        if (isDangling || !isMoving || isEating)
                        {
                            if (self.bodyParts != null && self.bodyParts.Length > 1)
                            {
                                Limb frontPaw = self.bodyParts[0] as Limb;
                                Limb secondPaw = self.bodyParts[2] as Limb;

                                // Always grab with the primary hand
                                if (frontPaw != null)
                                {
                                    frontPaw.mode = Limb.Mode.HuntAbsolutePosition;
                                    frontPaw.absoluteHuntPos = targetPawPos;
                                    frontPaw.pos = targetPawPos;
                                    frontPaw.vel = Vector2.zero;
                                }

                                // If eating AND (on the ground OR dangling), use both hands
                                if (isEating && (!isClimbing || isDangling) && secondPaw != null)
                                {
                                    // Offset the second hand slightly so they don't z-fight
                                    Vector2 twoHandPos = targetPawPos + new Vector2(faceDir * 2f, -2f);

                                    secondPaw.mode = Limb.Mode.HuntAbsolutePosition;
                                    secondPaw.absoluteHuntPos = twoHandPos;
                                    secondPaw.pos = twoHandPos;
                                    secondPaw.vel = Vector2.zero;
                                }
                                // If we are climbing or stopped eating, release the second hand back to normal physics
                                else if (secondPaw != null && secondPaw.mode == Limb.Mode.HuntAbsolutePosition)
                                {
                                    secondPaw.mode = Limb.Mode.Dangle;
                                }
                            }
                        }

                        break;
                    }
                }
            }
        }

        private static void MouseGraphics_DrawSprites(
            On.MouseGraphics.orig_DrawSprites orig,
            MouseGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            float timeStacker,
            Vector2 camPos
        )
        {
            int markSprite = sLeaser.sprites.Length - 1;
            int markGlowSprite = sLeaser.sprites.Length - 2;
            int mouseHead = 4;

            Vector2 headPos = Vector2.Lerp(
                self.bodyParts[mouseHead].lastPos,
                self.bodyParts[mouseHead].pos,
                timeStacker
            );

            sLeaser.sprites[markSprite].x = headPos.x - camPos.x;
            sLeaser.sprites[markSprite].y = (headPos.y - camPos.y) + 50f;

            sLeaser.sprites[markGlowSprite].x = headPos.x - camPos.x;
            sLeaser.sprites[markGlowSprite].y = (headPos.y - camPos.y) + 50f;

            orig(self, sLeaser, rCam, timeStacker, camPos);

            if (self.owner is LanternMouse)
            {
                sLeaser.sprites[markSprite].MoveToFront();
            }
        }

        // Moves the held item to visually follow the mouse's head position
        private static void SpriteLeaser_Update(
            On.RoomCamera.SpriteLeaser.orig_Update orig,
            RoomCamera.SpriteLeaser self,
            float timeStacker,
            RoomCamera rCam,
            Vector2 camPos
        )
        {
            orig(self, timeStacker, rCam, camPos);

            if (self.drawableObject is PhysicalObject obj && obj.grabbedBy != null && obj.grabbedBy.Count > 0)
            {
                if (obj.grabbedBy[0].grabber is LanternMouse mouse)
                {
                    Vector2 bodyPos = Vector2.Lerp(mouse.bodyChunks[0].lastPos, mouse.bodyChunks[0].pos, timeStacker);

                    float faceDir = 1f;
                    if (Mathf.Abs(mouse.mainBodyChunk.vel.x) > 0.1f)
                    {
                        faceDir = Mathf.Sign(mouse.mainBodyChunk.vel.x);
                    }
                    else if (mouse.AI != null && mouse.AI.pathFinder != null)
                    {
                        Vector2 targetTilePos = rCam.room.MiddleOfTile(mouse.AI.pathFinder.GetDestination);
                        if (targetTilePos.x != mouse.mainBodyChunk.pos.x)
                        {
                            faceDir = Mathf.Sign(targetTilePos.x - mouse.mainBodyChunk.pos.x);
                        }
                    }

                    // --- State Checks ---
                    bool isEating = mouse.AI.behavior == MouseFriendAI.Behavior.Eat;
                    bool isDangling = mouse.ropeAttatchedPos != null;

                    Vector2 offset = Vector2.zero;

                    if (isEating)
                    {
                        offset = new Vector2(faceDir * 9f, -1f);    // Held at the snout
                    }
                    else if (isDangling)
                    {
                        offset = new Vector2(0f, -12f);     // Hanging down
                    }
                    else
                    {
                        offset = new Vector2(faceDir * 12f, -5f);   // Held forward
                    }

                    Vector2 targetVisualPos = bodyPos + offset - camPos;

                    if (self.sprites != null)
                    {
                        for (int i = 0; i < self.sprites.Length; i++)
                        {
                            self.sprites[i].x = targetVisualPos.x;
                            self.sprites[i].y = targetVisualPos.y;
                        }
                    }
                }
            }
        }
    }
}
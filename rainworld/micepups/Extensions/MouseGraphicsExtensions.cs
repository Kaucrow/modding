using RWCustom;
using System.Reflection;
using UnityEngine;

namespace MicePups.Extensions
{
    internal static class MouseGraphicsExtensions
    {
        private static readonly FieldInfo blinkField = typeof(MouseGraphics).GetField("blink", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static void BiteFly(this MouseGraphics graphics, int hand)
        {
            LanternMouse mouse = graphics.owner as LanternMouse;
            if (mouse == null) return;

            BodyPart activeHand = graphics.bodyParts[hand];
            PhysicalObject grabbedObj = mouse.grasps[hand]?.grabbed;

            if (grabbedObj != null)
            {
                // We calculate the direction to the food and jerk the visual head aggressively towards it
                Vector2 dirToFood = Custom.DirVec(graphics.head.pos, grabbedObj.firstChunk.pos);
                graphics.head.vel += dirToFood * 3f;

                // Give the visual head a slight bump up, and the chest a tiny push to simulate heaving
                graphics.head.pos.y += 1f;
                mouse.bodyChunks[0].vel.y += 0.5f;

                for (int i = 0; i < Random.Range(0, 3); i++)
                {
                    graphics.mouse.room.AddObject(new WaterDrip(graphics.head.pos, Custom.DegToVec(Random.value * 360f) * (Random.value * 4f), false));
                }

                // Access the 'blink' field using reflection and set it to 5 if it's currently less than 5
                if (blinkField != null)
                {
                    int currentBlink = (int)blinkField.GetValue(graphics);

                    if (currentBlink < 5)
                    {
                        blinkField.SetValue(graphics, 5);
                    }
                }
            }
        }
    }
}

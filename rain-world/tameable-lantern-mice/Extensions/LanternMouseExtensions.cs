using MouseFriends.AI;
using MouseFriends.Data;
using RWCustom;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MouseFriends.Extensions
{
    internal static class LanternMouseExtensions
    {
        private static readonly ConditionalWeakTable<AbstractCreature, MouseFriendData> friendData = new();

        internal static bool IsBefriended(this LanternMouse mouse)
        {
            MouseFriendData data = mouse.GetFriendData();
            return data.abstractAI.IsTamed;
        }

        internal static void GrabItem(this LanternMouse mouse, PhysicalObject obj)
        {
            for (int i = 0; i < mouse.grasps.Length; i++)
            {
                if (mouse.grasps[i] == null)
                {
                    mouse.Grab(obj, i, 0, Creature.Grasp.Shareability.CanNotShare, 0, false, false);
                    break;
                }
            }
        }

        internal static void Sit(this LanternMouse mouse)
        {
            // Use Reflection to invoke the private "Sit" method on this.mouse
            typeof(LanternMouse).GetMethod("Sit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(mouse, null);
            mouse.GoThroughFloors = false;
        }

        internal static void Throw(this LanternMouse mouse)
        {
            // Ensure we are actually holding something to throw
            if (mouse.grasps[0]?.grabbed is not PhysicalObject grabbedObject) return;

            if (mouse.abstractCreature.abstractAI?.RealAI is not MouseFriendAI mouseFriendAI) return;

            int throwDirRawInt = mouseFriendAI.data.ThrowAtTarget;

            // Convert the throw direction -1 / 1 integer data into a physics direction vector
            Vector2 throwDir = new Vector2((float)throwDirRawInt, 0f);
            IntVector2 throwDirInt = new IntVector2(throwDirRawInt, 0);

            // Define spawning positions relative to the mouse's body
            Vector2 spawnPos = mouse.mainBodyChunk.pos + throwDir * 30f;    // Slightly shorter offset for a small mouse
            Vector2 trailingPos = mouse.mainBodyChunk.pos - throwDir * 5f;

            if (grabbedObject is Weapon weapon)
            {
                // Start the Rain World lethal weapon state
                // The float multiplier (0.75f) scales the weapon's generic stun/physics weight
                weapon.Thrown(mouse, spawnPos, trailingPos, throwDirInt, 0.75f, mouse.evenUpdate);
            }
            else
            {
                // Handling for non-weapon objects
                if (grabbedObject.bodyChunks.Length == 1)
                {
                    grabbedObject.firstChunk.pos = spawnPos;
                }
                grabbedObject.firstChunk.vel = throwDir * 15f;  // Tossing items has less force

                // Special DLC cases for interactive non-weapons
                if (ModManager.MSC && grabbedObject is MoreSlugcats.FireEgg fireEgg)
                {
                    fireEgg.Tossed(mouse);
                }
            }

            // Drop the item out of our hands
            mouse.ReleaseGrasp(0);

            // Reset the throw property so we don't spam throws every frame
            mouseFriendAI.data.ThrowAtTarget = 0;
        }

        internal static void ObjectEaten(this LanternMouse mouse, IPlayerEdible _)
        {
            mouse.AddFood(1);
        }

        internal static void AddFood(this LanternMouse mouse, int amount)
        {
            MouseFriendData data = mouse.GetFriendData();

            // Ensure that we don't exceed the maximum food in stomach
            if (data.FoodInStomach + amount > data.MaxFoodInStomach)
            {
                data.FoodInStomach = data.MaxFoodInStomach;
                return;
            }

            data.FoodInStomach += amount;
        }

        internal static bool IsFull(this LanternMouse mouse)
        {
            MouseFriendData data = mouse.GetFriendData();
            return data.FoodInStomach >= data.MaxFoodInStomach;
        }

        internal static MouseFriendData GetFriendData(this AbstractCreature absCrit)
        {
            friendData.TryGetValue(absCrit, out var data);
            return data;
        }

        internal static MouseFriendData GetFriendData(this LanternMouse mouse)
        {
            return mouse.abstractCreature?.GetFriendData();
        }

        internal static bool GetFriendData(this AbstractCreature absCrit, out MouseFriendData data)
        {
            return friendData.TryGetValue(absCrit, out data);
        }

        internal static void SetFriendData(this LanternMouse mouse)
        {
            friendData.Remove(mouse.abstractCreature);
            friendData.Add(mouse.abstractCreature, new MouseFriendData(mouse.abstractCreature));
        }
    }
}

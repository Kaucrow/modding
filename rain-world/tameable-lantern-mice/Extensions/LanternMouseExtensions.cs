using System.Runtime.CompilerServices;
using MouseFriends.Data;

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

using System.Runtime.CompilerServices;
using HUD;
using MouseFriends.Data;

namespace MouseFriends.Extensions
{
    internal static class FoodMeterExtensions
    {
        private static readonly ConditionalWeakTable<FoodMeter, MouseFoodMeterData> mouseFriendMeters = new();

        // Associates a LanternMouse and a dummy Player with the given FoodMeter, effectively marking it as a MouseMeter
        public static void RegisterAsMouseMeter(
            this FoodMeter foodMeter,
            LanternMouse mouse,
            Player dummyPlayer,
            int stackIndex
        )
        {
            mouseFriendMeters.Add(foodMeter, new MouseFoodMeterData(mouse, dummyPlayer, stackIndex));
        }

        // Returns the MouseMeterData associated with the given FoodMeter, or null if no data is associated
        public static bool GetMouseData(this FoodMeter foodMeter, out MouseFoodMeterData data)
        {
            return mouseFriendMeters.TryGetValue(foodMeter, out data);
        }

        // Returns true if the given FoodMeter is a MouseMeter
        public static bool IsMouseFoodMeter(this FoodMeter foodMeter)
        {
            return mouseFriendMeters.TryGetValue(foodMeter, out _);
        }
    }
}

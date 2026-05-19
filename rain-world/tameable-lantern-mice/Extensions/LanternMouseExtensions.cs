using System.Runtime.CompilerServices;
using MouseFriends.Data;

namespace MouseFriends.Extensions
{
    internal static class LanternMouseExtensions
    {
        private static readonly ConditionalWeakTable<AbstractCreature, MouseFriendData> friendData = new();

        internal static bool IsBefriended(this LanternMouse mouse)
        {
            var data = mouse.GetFriendData();
            return data != null && data.IsTamed;
        }

        internal static void Sit(this LanternMouse mouse)
        {
            // Use Reflection to invoke the private "Sit" method on this.mouse
            typeof(LanternMouse).GetMethod("Sit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(mouse, null);
            mouse.GoThroughFloors = false;
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

        internal static void SetPupData(this LanternMouse mouse)
        {
            friendData.Remove(mouse.abstractCreature);
            friendData.Add(mouse.abstractCreature, new MouseFriendData(mouse.abstractCreature));
        }
    }
}

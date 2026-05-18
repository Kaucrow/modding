namespace MicePups.Extensions
{
    internal static class LanternMouseExtensions
    {
        internal static void Sit(this LanternMouse mouse)
        {
            // Use Reflection to invoke the private "Sit" method on this.mouse
            typeof(LanternMouse).GetMethod("Sit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(mouse, null);
            mouse.GoThroughFloors = false;
        }
    }
}

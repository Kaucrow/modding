using System.Runtime.CompilerServices;

namespace MicePups.Data
{
    internal static class MicePupsManager
    {
        public static ConditionalWeakTable<AbstractCreature, MousePupData> _pupData = new();

        public static MousePupData GetPupData(this AbstractCreature absCrit)
        {
            _pupData.TryGetValue(absCrit, out var data);
            return data;
        }

        public static MousePupData GetPupData(this LanternMouse mouse)
        {
            return mouse.abstractCreature?.GetPupData();
        }

        public static void SetPupData(this LanternMouse mouse)
        {
            _pupData.Remove(mouse.abstractCreature);
            _pupData.Add(mouse.abstractCreature, new MousePupData(mouse.abstractCreature));
        }
    }
}
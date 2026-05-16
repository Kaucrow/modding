using MicePupsMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MicePups.Data
{
    internal static class MicePupsManager
    {
        public static ConditionalWeakTable<LanternMouse, PupData> _pupData = new();

        public static PupData GetPupData(this LanternMouse mouse)
        {
            _pupData.TryGetValue(mouse, out var data);
            return data;
        }

        public static void SetPupData(this LanternMouse mouse)
            => _pupData.Add(mouse, new PupData(mouse.abstractCreature));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicePups.AI
{
    internal class MousePupBehaviors
    {
        public static readonly MouseAI.Behavior GrabItem = new MouseAI.Behavior("GrabItem", true);

        public static void Register()
        {
            // Dummy method to force static initialization
        }
    }
}

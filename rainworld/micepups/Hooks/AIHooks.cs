using MicePups.AI;

namespace MicePups.Hooks
{
    internal static class AIHooks
    {
        internal static void Apply()
        {
            On.AbstractCreature.InitiateAI += OnAbstractCreatureInitiateAI;
        }

        internal static void Remove()
        {
            On.AbstractCreature.InitiateAI -= OnAbstractCreatureInitiateAI;
        }

        private static void OnAbstractCreatureInitiateAI(
            On.AbstractCreature.orig_InitiateAI orig,
            AbstractCreature self
        )
        {
            orig(self);

            if (self.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.LanternMouse)
            {
                // Injects all of the code you wrote in MouseAIExtended into the creature!
                self.abstractAI.RealAI = new MouseAIExtended(self, self.world);
            }
        }
    }
}
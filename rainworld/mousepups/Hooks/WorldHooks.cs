using UnityEngine;
using System;
using HarmonyLib;
using System.Reflection;
using MicePups.AI;

namespace MicePups.Hooks
{
    internal class WorldHooks
    {
        internal static void Apply()
        {
            On.StaticWorld.InitStaticWorldRelationships += OnInitWorldRelationships;
        }

        internal static void Remove()
        {
            On.StaticWorld.InitStaticWorldRelationships -= OnInitWorldRelationships;
        }

        private static void OnInitWorldRelationships(
            On.StaticWorld.orig_InitStaticWorldRelationships orig
        )
        {
            orig();

            var mouseTemplate = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.LanternMouse);
            if (mouseTemplate != null)
            {
                // Modify relationship with Slugcat
                //mouseTemplate.relationships[(int)CreatureTemplate.Type.Slugcat] =
                //    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0.5f);

            }
        }

        [HarmonyPatch(typeof(AbstractCreature))]
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] {
            typeof(World),
            typeof(CreatureTemplate),
            typeof(Creature),
            typeof(WorldCoordinate),
            typeof(EntityID)
        })]
        public static class AbstractCreature_Constructor_Patch
        {
            public static void Postfix(AbstractCreature __instance, CreatureTemplate creatureTemplate)
            {
                if (creatureTemplate.type == CreatureTemplate.Type.LanternMouse)    // Replace with the creature type check
                {
                    __instance.abstractAI = new MousePupAbstractAI(__instance.world, __instance);
                }
            }
        }
    }
}

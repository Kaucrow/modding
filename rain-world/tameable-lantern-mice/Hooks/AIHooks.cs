using MouseFriends.AI;

namespace MouseFriends.Hooks
{
    internal static class AIHooks
    {
        internal static void Apply()
        {
            On.ArtificialIntelligence.DynamicRelationship_CreatureRepresentation_AbstractCreature += ArtificialIntelligence_DynamicRelationship;
            On.ArtificialIntelligence.StaticRelationship += ArtificialIntelligence_StaticRelationship;
            On.MouseAI.ReconsiderDanglePos += MouseAI_ReconsiderDanglePos;
        }

        internal static void Remove()
        {
            On.ArtificialIntelligence.DynamicRelationship_CreatureRepresentation_AbstractCreature -= ArtificialIntelligence_DynamicRelationship;
            On.ArtificialIntelligence.StaticRelationship -= ArtificialIntelligence_StaticRelationship;
            On.MouseAI.ReconsiderDanglePos -= MouseAI_ReconsiderDanglePos;
        }

        private static CreatureTemplate.Relationship ArtificialIntelligence_DynamicRelationship(
            On.ArtificialIntelligence.orig_DynamicRelationship_CreatureRepresentation_AbstractCreature orig,
            ArtificialIntelligence self,
            Tracker.CreatureRepresentation rep,
            AbstractCreature absCrit
        )
        {
            // DEBUG
            /*
            if (self is MousePupAI)
            {
                // Replicate the first step of the vanilla method to see what data we have
                Tracker.CreatureRepresentation debugRep = rep ?? self.tracker?.RepresentationForCreature(absCrit, false);

                if (debugRep == null)
                {
                    UnityEngine.Debug.Log($"[MousePupAI] No representation found for {absCrit?.creatureTemplate.type}. Vanilla will use StaticRelationship.");
                }
                else if (debugRep.dynamicRelationship != null)
                {
                    UnityEngine.Debug.Log($"[MousePupAI] SUCCESS! Using dynamic relationship for {debugRep.representedCreature.creatureTemplate.type}. Current Type: {debugRep.dynamicRelationship.currentRelationship.type}");
                }
                else
                {
                    UnityEngine.Debug.Log($"[MousePupAI] WARNING: Representation exists for {debugRep.representedCreature.creatureTemplate.type}, but dynamicRelationship is NULL! Vanilla will use StaticRelationship.");
                }
            }
            */

            // Call the original method
            CreatureTemplate.Relationship rel = orig(self, rep, absCrit);

            if (self is MouseFriendAI pupAI && absCrit != null && absCrit.creatureTemplate.type == CreatureTemplate.Type.Slugcat)
            {
                // Check if the pup has developed a strong liking to this player in its social memory
                if (pupAI.creature.state.socialMemory != null)
                {
                    SocialMemory.Relationship playerRel = pupAI.creature.state.socialMemory.GetRelationship(absCrit.ID);

                    // If the pup's relationship level crosses the 0.5f threshold
                    if (playerRel != null && playerRel.like > 0.5f)
                    {
                        // Dynamic upgrade to the relationship status
                        rel.type = CreatureTemplate.Relationship.Type.SocialDependent;
                        rel.intensity = 1f;
                        return rel;
                    }
                }

                // If no friendship exists yet, fall back to the static relationship
                rel.type = CreatureTemplate.Relationship.Type.Ignores;
                rel.intensity = 0f;
            }

            return rel;
        }

        private static CreatureTemplate.Relationship ArtificialIntelligence_StaticRelationship(
            On.ArtificialIntelligence.orig_StaticRelationship orig,
            ArtificialIntelligence self,
            AbstractCreature absCrit
        )
        {
            CreatureTemplate.Relationship rel = orig(self, absCrit);

            if (self is MouseFriendAI && absCrit != null && absCrit.creatureTemplate.type == CreatureTemplate.Type.Slugcat)
            {
                rel.type = CreatureTemplate.Relationship.Type.Ignores;
                rel.intensity = 0f;
            }

            return rel;
        }

        private static void MouseAI_ReconsiderDanglePos(
            On.MouseAI.orig_ReconsiderDanglePos orig,
            MouseAI self
        )
        {
            if (self is not MouseFriendAI)
            {
                orig(self);
                return;
            }

            // If holding food or moving towards an item, skip checking for ceilings
            if (self.behavior == MouseFriendAI.Behavior.GrabItem)
            {
                self.dangle = null;
                return;
            }
            else if (
                self.mouse.grasps != null && self.mouse.grasps.Length > 0 &&
                self.mouse.grasps[0] != null && self.mouse.grasps[0].grabbed is IPlayerEdible
            )
            {
                self.dangle = null;
                return;
            }

            // Otherwise, look for ceilings normally
            orig(self);
        }
    }
}
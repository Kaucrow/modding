using System.Linq;
using MoreSlugcats;

namespace MouseFriends.AI
{
    internal partial class MouseFriendAI : MouseAI, IUseItemTracker, FriendTracker.IHaveFriendTracker, IReactToSocialEvents,
        IUseARelationshipTracker
    {
        AIModule IUseARelationshipTracker.ModuleToTrackRelationship(CreatureTemplate.Relationship relationship)
        {
            CreatureTemplate.Relationship.Type type = relationship.type;
            if (type == CreatureTemplate.Relationship.Type.Eats || type == CreatureTemplate.Relationship.Type.Attacks)
            {
                return base.preyTracker;
            }
            if (type == CreatureTemplate.Relationship.Type.Afraid)
            {
                return base.threatTracker;
            }
            return base.tracker;
        }

        RelationshipTracker.TrackedCreatureState IUseARelationshipTracker.CreateTrackedCreatureState(RelationshipTracker.DynamicRelationship rel)
        {
            return new MouseFriendTrackState();
        }

        CreatureTemplate.Relationship IUseARelationshipTracker.UpdateDynamicRelationship(RelationshipTracker.DynamicRelationship dRelation)
        {
            var representedCreature = dRelation.trackerRep.representedCreature;
            var creatureType = representedCreature.creatureTemplate.type;

            // If our friend is Arti, return an Attacks relationship
            if (base.friendTracker.friend?.abstractCreature.state is PlayerState playerState &&
                playerState.slugcatCharacter == MoreSlugcatsEnums.SlugcatStatsName.Artificer)
            {
                return new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Attacks, 1f);
            }

            // Important predators
            var scaryMonsters /* and nice sprites */ = new[]
            {
                CreatureTemplate.Type.RedLizard,
                CreatureTemplate.Type.RedCentipede,
                CreatureTemplate.Type.DaddyLongLegs,
                CreatureTemplate.Type.BrotherLongLegs,
                DLCSharedEnums.CreatureTemplateType.TerrorLongLegs,
                MoreSlugcatsEnums.CreatureTemplateType.HunterDaddy
            };

            // If the creature is not an important predator but is still a threat, increase the annoyingThreat counter
            if (base.threatTracker.GetThreatCreature(representedCreature) != null && !scaryMonsters.Contains(creatureType))
            {
                (dRelation.state as MouseFriendTrackState).annoyingThreat++;
            }

            // If the creature is an important predator, return the creature's static relationship
            Creature realizedCreature = dRelation.trackerRep.representedCreature.realizedCreature;
            if (realizedCreature == null)
            {
                return base.StaticRelationship(dRelation.trackerRep.representedCreature);
            }

            // If the creature is in visual contact and it's holding a player, set the holdingAFriend flag to true
            if (dRelation.trackerRep.VisualContact)
            {
                bool holdingAFriend = false;
                if (!realizedCreature.abstractCreature.creatureTemplate.smallCreature &&
                    !(realizedCreature is Player) &&
                    realizedCreature.grasps != null &&
                    realizedCreature.grasps.Length != 0)
                {
                    for (int i = 0; i < realizedCreature.grasps.Length; i++)
                    {
                        if (realizedCreature.grasps[i] != null && realizedCreature.grasps[i].grabbed is Player)
                        {
                            holdingAFriend = true;
                        }
                    }
                }
                (dRelation.state as MouseFriendTrackState).holdingAFriend = holdingAFriend;
            }

            // Check if the creature shares a friend with us
            var commonFriend = realizedCreature.abstractCreature?.abstractAI?.RealAI?.friendTracker?.friend;

            // If we share a friend with the creature, ignore it
            if (commonFriend != null && commonFriend == base.friendTracker?.friend)
            {
                return new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0.5f);
            }

            // If we want to eat the creature, return an Eats relationship with a higher
            // weight if the creature is alive and a lower weight if it's dead.
            if (this.WantsToEatThis(realizedCreature))
            {
                return new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Eats, dRelation.state.alive ? 0.65f : 1f);
            }

            // If the creature is dead and it's not a player, ignore it
            if (realizedCreature.dead && !(realizedCreature is Player))
            {
                return new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0f);
            }

            // Fall back to the creature's static relationship if none of the above conditions are met
            return base.StaticRelationship(dRelation.trackerRep.representedCreature);
        }
    }
}

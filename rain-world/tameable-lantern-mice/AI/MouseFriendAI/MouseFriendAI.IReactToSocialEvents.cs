using System;

namespace MouseFriends.AI
{
    internal partial class MouseFriendAI : MouseAI, IUseItemTracker, FriendTracker.IHaveFriendTracker, IReactToSocialEvents,
        IUseARelationshipTracker
    {
        public void SocialEvent(SocialEventRecognizer.EventID ID, Creature subjectCrit, Creature objectCrit, PhysicalObject involvedItem)
        {
            if (
                subjectCrit != null && objectCrit != null &&
                subjectCrit.abstractCreature.rippleLayer != objectCrit.abstractCreature.rippleLayer &&
                !subjectCrit.abstractCreature.rippleBothSides && !objectCrit.abstractCreature.rippleBothSides
            )
            {
                return;
            }

            Tracker.CreatureRepresentation creatureRepresentation = base.tracker.RepresentationForObject(subjectCrit, false);
            if (creatureRepresentation == null)
            {
                return;
            }

            Tracker.CreatureRepresentation creatureRepresentation2 = null;
            bool flag = objectCrit == this.mouse;
            if (!flag)
            {
                creatureRepresentation2 = base.tracker.RepresentationForObject(objectCrit, false);
                if (creatureRepresentation2 == null)
                {
                    return;
                }
            }

            if (!flag && this.mouse.dead)
            {
                return;
            }

            if (creatureRepresentation2 != null && creatureRepresentation.TicksSinceSeen > 40 && creatureRepresentation2.TicksSinceSeen > 40)
            {
                return;
            }

            if (
                (ID == SocialEventRecognizer.EventID.LethalAttack || ID == SocialEventRecognizer.EventID.Killing)
                && objectCrit is Player && subjectCrit.abstractCreature.creatureTemplate.type != CreatureTemplate.Type.LanternMouse
            )
            {
                for (int i = 0; i < base.relationshipTracker.relationships.Count; i++)
                {
                    if (base.relationshipTracker.relationships[i].trackerRep != null && base.relationshipTracker.relationships[i].trackerRep.representedCreature != null && base.relationshipTracker.relationships[i].trackerRep.representedCreature.realizedCreature == subjectCrit)
                    {
                        // TODO: Implement hurtAFriend
                        //(base.relationshipTracker.relationships[i].state as SlugNPCAI.SlugNPCTrackState).hurtAFriend = true;
                    }
                }
            }

            if (
                ID == SocialEventRecognizer.EventID.ItemOffering &&
                subjectCrit.abstractCreature.creatureTemplate.type == CreatureTemplate.Type.Slugcat &&
                !(involvedItem is Player) && objectCrit == this.mouse
            )
            {
                if (involvedItem.room.abstractRoom.name == "SL_AI" && involvedItem.firstChunk.pos.x > 1150f)
                {
                    Console.WriteLine(new string[]
                    {
                        "Reject gift due to moon proximity"
                    });
                    return;
                }
                else
                {
                    base.friendTracker.giftOfferedToMe = involvedItem.room.socialEventRecognizer.ItemOwnership(involvedItem);
                }
            }
        }
    }
}

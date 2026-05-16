using MicePups.Data;
using System;
using UnityEngine;

namespace MicePups.AI
{
    internal class MouseAIExtended : MouseAI, IUseItemTracker, FriendTracker.IHaveFriendTracker, IReactToSocialEvents
    {
        public MouseAIExtended(AbstractCreature creature, World world)
            : base(creature, world)
        {
            if (this.mouse.GetPupData() != null)
            {
                if (this.mouse.State.socialMemory == null)
                {
                    this.mouse.State.socialMemory = new SocialMemory();
                }

                if (this.utilityComparer == null)
                {
                    this.AddModule(new UtilityComparer(this));
                }

                this.AddModule(new FriendTracker(this));
                this.AddModule(new ItemTracker(this, 10, 10, -1, -1, true));
            }
        }

        public override void Update()
        {
            base.Update();

            var data = this.mouse.GetPupData();
            if (data == null) return;

            // Friend Initiation Logic (Shelter Check)
            if (this.friendTracker != null && this.friendTracker.friend == null && this.mouse.room != null)
            {
                for (int i = 0; i < this.mouse.room.game.Players.Count; i++)
                {
                    var player = this.mouse.room.game.Players[i].realizedCreature as Player;
                    if (player != null && player.room == this.mouse.room)
                    {
                        SocialMemory.Relationship rel = this.mouse.State.socialMemory.GetOrInitiateRelationship(player.abstractCreature.ID);
                        rel.InfluenceLike(1f);
                        rel.InfluenceTempLike(1f);
                    }
                }
            }

            // Communication & Taming
            if (this.friendTracker?.friend is Player friendPlayer)
            {
                data.abstractAI.isTamed = true;

                if (this.VisualContact(friendPlayer.firstChunk) && this.mouse.room == friendPlayer.room)
                {
                    this.Communicate(friendPlayer);
                }
            }

            // Clear grab target if already holding it
            if (this.HoldingThis(data.grabTarget))
            {
                data.grabTarget = null;
            }

            // Accept Gifts
            if (this.friendTracker?.giftOfferedToMe?.item != null)
            {
                this.mouse.ReleaseGrasp(0);
                data.grabTarget = this.friendTracker.giftOfferedToMe.item;
            }

            // Behavior State Switching
            if (this.currentUtility < 0.2f && data.grabTarget != null)
            {
                this.behavior = MousePupBehaviors.GrabItem;
            }

            // Execute GrabItem Behavior
            if (this.behavior == MousePupBehaviors.GrabItem && data.grabTarget != null)
            {
                this.creature.abstractAI.SetDestination(data.grabTarget.abstractPhysicalObject.pos);
                this.NPCForceGrab(data.grabTarget);
            }
        }

        // Helper methods
        private void NPCForceGrab(PhysicalObject obj)
        {
            for (int i = 0; i < this.mouse.grasps.Length; i++)
            {
                if (this.mouse.grasps[i] == null)
                {
                    this.mouse.Grab(obj, i, 0, Creature.Grasp.Shareability.CanNotShare, 0, false, false);
                    break;
                }
            }
        }

        private void Communicate(Player player)
        {
            var data = this.mouse.GetPupData();
            if (data == null) return;

            Player.InputPackage[] input = player.input;
            if (input[0].jmp && !input[1].jmp && player.bodyMode != Player.BodyModeIndex.Default)
            {
                if (input[0].y == -1 && input[0].x == 0) return;

                if (input[0].y == 1 && input[0].x == 0 && player.bodyMode != Player.BodyModeIndex.ClimbingOnBeam)
                {
                    this.mouse.DetatchRope();
                    return;
                }
            }
        }

        private bool HoldingThis(PhysicalObject obj)
        {
            if (obj == null) return false;

            for (int i = 0; i < this.mouse.grasps.Length; i++)
            {
                if (this.mouse.grasps[i] != null && this.mouse.grasps[i].grabbed == obj)
                {
                    return true;
                }
            }
            return false;
        }

        public bool TrackItem(AbstractPhysicalObject obj)
        {
            return obj.realizedObject == null || !(obj.realizedObject is Weapon) || (obj.realizedObject as Weapon).mode != Weapon.Mode.StuckInWall;
        }

        public void SeeThrownWeapon(PhysicalObject obj, Creature thrower)
        {
            if (base.tracker.RepresentationForObject(thrower, false) == null)
            {
                base.noiseTracker.mysteriousNoises += 20f;
                base.noiseTracker.mysteriousNoiseCounter = 200;
            }
        }

        public void GiftRecieved(SocialEventRecognizer.OwnedItemOnGround giftOfferedToMe)
        {
            SocialMemory.Relationship orInitiateRelationship = this.creature.realizedCreature.State.socialMemory.GetOrInitiateRelationship(giftOfferedToMe.owner.abstractCreature.ID);
            if (giftOfferedToMe.owner is Player)
            {
                orInitiateRelationship.InfluenceLike(1f);
                orInitiateRelationship.InfluenceTempLike(1f);
            }
            Console.WriteLine("Relationship:");
            Console.WriteLine(new string[]
            {
                orInitiateRelationship.ToString()
            });
        }

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

                    if (this.dangle != null)
                    {
                        this.mouse.DetatchRope();
                    }
                }
            }
        }
    }
}
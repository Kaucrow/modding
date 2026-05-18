using MicePups.Data;
using MicePups.Extensions;
using System;
using UnityEngine;

namespace MicePups.AI
{
    internal class MousePupAI : MouseAI, IUseItemTracker, FriendTracker.IHaveFriendTracker, IReactToSocialEvents
    {
        // DEBUG
        /*
        private WorldCoordinate? travelTo = null;
        */

        public new class Behavior : MouseAI.Behavior
        {
            public static readonly MouseAI.Behavior GrabItem = new MouseAI.Behavior("GrabItem", register: true);
            public static readonly MouseAI.Behavior Eat = new MouseAI.Behavior("Eat", register: true);
            public Behavior(string value, bool register = false) : base(value, register) {}
        }

        public MousePupAI(AbstractCreature creature, World world) : base(creature, world)
        {
            if (this.mouse.State.socialMemory == null)
            {
                this.mouse.State.socialMemory = new SocialMemory();
            }

            base.AddModule(new FriendTracker(this));
            base.AddModule(new ItemTracker(this, 10, 10, -1, -1, true));

            FloatTweener.FloatTween smoother = new FloatTweener.FloatTweenUpAndDown(
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Lerp, 0.5f),
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.0025f)
            );

            // Fetch the existing threat profile created by the base constructor
            UtilityComparer.UtilityTracker existingThreatProfile = base.utilityComparer.GetUtilityTracker(base.threatTracker);

            // Overwrite its properties
            if (existingThreatProfile != null)
            {
                existingThreatProfile.smoother = smoother;
                existingThreatProfile.weight = 1f;
                existingThreatProfile.continuationBonus = 1f;
            }

            // Add the friend tracker
            base.utilityComparer.AddComparedModule(base.friendTracker, null, 0.9f, 1.2f);
        }

        public override void Update()
        {
            // DEBUG
            /*
            this.dangle = null;
            */

            var data = this.mouse.GetPupData();

            if (data.grabTarget != null)
            {
                // Force an immediate detachment if it's currently on a rope
                if (this.mouse.ropeAttatchedPos != null && this.IsSafeToDrop())
                {
                    this.mouse.DetatchRope();
                }
            }

            base.Update();
            
            // DEBUG
            /*
            AIModule topModule = this.utilityComparer.HighestUtilityModule();

            if (topModule != null)
            {
                UnityEngine.Debug.Log("--- PUP AI BRAIN SCAN ---");
                UnityEngine.Debug.Log("Winning Module: " + topModule.GetType().Name);
                UnityEngine.Debug.Log("Module Utility: " + topModule.Utility());
                UnityEngine.Debug.Log("Total currentUtility: " + this.currentUtility);
                UnityEngine.Debug.Log("-------------------------");
            }
            */
            
            // DEBUG
            /*
            if (this.travelTo != null)
            {
                this.creature.abstractAI.SetDestination(this.travelTo.Value);
            }
            */

            // Friend Initiation Logic
            /*if (this.friendTracker != null && this.friendTracker.friend == null && this.mouse.room != null)
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
            }*/

            // Communication & Taming
            if (this.friendTracker?.friend is Player friendPlayer)
            {
                data.abstractAI.isTamed = true;

                if (this.VisualContact(friendPlayer.firstChunk) && this.mouse.room == friendPlayer.room)
                {
                    this.Communicate(friendPlayer);
                }
            }

            // If the grabbed object is food, sit so we can eat it
            if (
                this.mouse.grasps != null && this.mouse.grasps.Length > 0 &&
                this.mouse.grasps[0] != null && this.mouse.grasps[0].grabbed is IPlayerEdible
            )
            {
                this.behavior = Behavior.Eat;

                this.walkWithMouse = null;
                this.mouse.runSpeed = 0f;

                LanternMouseExtensions.Sit(this.mouse);

                // Stop the AI from calculating new paths
                if (this.creature.abstractAI.destination.Tile != this.creature.pos.Tile)
                {
                    this.creature.abstractAI.SetDestination(this.creature.pos);
                }
            }
            // Accept Gifts
            if (this.friendTracker?.giftOfferedToMe?.item != null)
            {
                UnityEngine.Debug.Log("Gift offered: " + this.friendTracker.giftOfferedToMe.item);
                UnityEngine.Debug.Log("Current utility: " + this.currentUtility);
                this.mouse.ReleaseGrasp(0);
                data.grabTarget = this.friendTracker.giftOfferedToMe.item;
            }

            // Behavior State Switching
            if (data.grabTarget != null && base.threatTracker.Utility() < 0.2f && base.rainTracker.Utility() < 0.2f)
            {
                // First, check if the gift reachable
                if (base.pathFinder.CoordinateReachableAndGetbackable(data.grabTarget.abstractPhysicalObject.pos))
                {
                    // Switch behavior so the pathfinder starts moving us to the gift
                    UnityEngine.Debug.Log("Switching to GrabItem behavior. Utility: " + this.currentUtility);
                    base.behavior = Behavior.GrabItem;
                }
                else
                {
                    if (data.grabTarget.firstChunk.vel.magnitude < 1f)
                    {
                        UnityEngine.Debug.Log("Gift has landed but is unreachable! Ignoring it.");
                        data.grabTarget = null;
                        if (this.friendTracker != null) this.friendTracker.giftOfferedToMe = null;
                    }
                    else
                    {
                        UnityEngine.Debug.Log("Gift is mid-air... waiting for it to land.");
                    }
                }
            }

            // Execute GrabItem Behavior
            if (this.behavior == Behavior.GrabItem && data.grabTarget != null)
            {
                UnityEngine.Debug.Log("Attempting to grab: " + data.grabTarget);

                // Walk towards the item
                this.creature.abstractAI.SetDestination(data.grabTarget.abstractPhysicalObject.pos);

                // Check if the mouse pup is physically close enough to grab it
                if (Vector2.Distance(this.mouse.mainBodyChunk.pos, data.grabTarget.firstChunk.pos) < 40f)
                {
                    UnityEngine.Debug.Log("Close enough! Grabbing: " + data.grabTarget);
                    this.NPCForceGrab(data.grabTarget);

                    // Tell the tracker we successfully took the gift
                    if (this.friendTracker?.giftOfferedToMe != null)
                    {
                        this.GiftRecieved(this.friendTracker.giftOfferedToMe);
                        this.friendTracker.giftOfferedToMe = null;  // Clear the offer
                    }

                    // Reset the target and go back to normal behavior
                    this.behavior = Behavior.Idle;
                    data.grabbed = data.grabTarget;
                    data.grabTarget = null;
                }
            }
        }

        // Helper methods
        private bool IsSafeToDrop()
        {
            if (this.mouse.room == null) return false;

            // Get the pup's current tile coordinates
            RWCustom.IntVector2 mouseTile = this.mouse.room.GetTilePosition(this.mouse.mainBodyChunk.pos);

            // Scan downwards from the pup to the very bottom of the room
            for (int y = mouseTile.y; y >= 0; y--)
            {
                Room.Tile tile = this.mouse.room.GetTile(mouseTile.x, y);

                // If we hit solid terrain, a floor, or a slope, it's safe
                if (tile.Solid || tile.Terrain == Room.Tile.TerrainType.Floor || tile.Terrain == Room.Tile.TerrainType.Slope)
                {
                    return true;
                }
            }

            // If the loop reaches y = 0 and never hit solid ground, it's a bottomless death pit
            return false;
        }

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

            // DEBUG
            /*
            this.travelTo = giftOfferedToMe.item.abstractPhysicalObject.pos;
            */
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
                }
            }
        }
    }
}
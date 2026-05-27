using MoreSlugcats;
using MouseFriends.Data;
using MouseFriends.Extensions;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MouseFriends.AI
{
    internal partial class MouseFriendAI : MouseAI, IUseItemTracker, FriendTracker.IHaveFriendTracker, IReactToSocialEvents,
        IUseARelationshipTracker
    {
        // DEBUG
        /*
        private WorldCoordinate? travelTo = null;
        */

        public readonly MouseFriendData data;

        public MouseFriendAI(AbstractCreature creature, World world, MouseFriendData data) : base(creature, world)
        {
            this.data = data;

            if (this.mouse.State.socialMemory == null)
            {
                this.mouse.State.socialMemory = new SocialMemory();
            }

            // Add the AI modules
            base.AddModule(new FriendTracker(this));
            base.AddModule(new ItemTracker(this, 10, 10, -1, -1, true));
            base.AddModule(new PreyTracker(this, 10, 1f, 5f, -1f, 0.5f));

            // Customize the threat tracker
            FloatTweener.FloatTween threatSmoother = new FloatTweener.FloatTweenUpAndDown(
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Lerp, 0.5f),
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.0025f)
            );

            // Fetch the existing threat profile created by the base constructor
            UtilityComparer.UtilityTracker existingThreatProfile = base.utilityComparer.GetUtilityTracker(base.threatTracker);

            // Overwrite its properties
            if (existingThreatProfile != null)
            {
                existingThreatProfile.smoother = threatSmoother;
                existingThreatProfile.weight = 1f;
                existingThreatProfile.continuationBonus = 1f;
            }

            // Add the compared modules
            base.utilityComparer.AddComparedModule(base.friendTracker, null, 0.9f, 1.2f);
            base.utilityComparer.AddComparedModule(
                base.preyTracker,
                new FloatTweener.FloatTweenBasic(
                    FloatTweener.TweenType.Tick,
                    0.033333335f
                ),
                0.5f,
                1.1f
            );
        }

        public override void Update()
        {
            var prevRunSpeed = this.mouse.runSpeed;

            base.Update();

            // DEBUG
            /*
            this.dangle = null;
            */

            if (data.GrabTarget != null)
            {
                // Force an immediate detachment if it's currently on a rope
                if (this.mouse.ropeAttatchedPos != null && this.IsSafeToDrop())
                {
                    this.mouse.DetatchRope();
                }
            }

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

            // Communication
            if (this.friendTracker?.friend is Player friendPlayer)
            {
                if (this.VisualContact(friendPlayer.firstChunk) && this.mouse.room == friendPlayer.room)
                {
                    this.Communicate(friendPlayer);
                }
            }

            // If the grabbed object is food, sit so we can eat it
            if (this.mouse.grasps?.Length > 0 && this.mouse.grasps[0]?.grabbed is IPlayerEdible)
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

            // Accept gifts
            if (this.friendTracker?.giftOfferedToMe?.item != null)
            {
                UnityEngine.Debug.Log("Gift offered: " + this.friendTracker.giftOfferedToMe.item);
                UnityEngine.Debug.Log("Current utility: " + this.currentUtility);
                this.mouse.ReleaseGrasp(0);
                data.GrabTarget = this.friendTracker.giftOfferedToMe.item;
            }

            // If we have a friend and it's a player, we're tamed
            if (base.friendTracker.friend != null && base.friendTracker.friend is Player)
            {
                this.data.abstractAI.IsTamed = true;
            }

            // Define follow closeness based on our relationship with our friend, if we have one
            if (base.friendTracker.friend != null)
            {
                this.DefineFollowCloseness();
            }

            // If our grab target is currently being held by us, unset the grab target
            if (this.HoldingThis(this.data.GrabTarget))
            {
                this.data.GrabTarget = null;
            }

            // If we're currently on the player's head or being held by the player,
            // drop any held item, unset ToldToStay, unset giftOfferedToMe, and update ToldToPlay.
            if (this.behavior == Behavior.OnHead || this.behavior == Behavior.HeldByPlayer)
            {
                this.mouse.ReleaseGrasp(0);
                this.data.abstractAI.ToldToStay = null;
                base.friendTracker.giftOfferedToMe = null;
                this.data.ToldToPlay = Mathf.Min(this.data.ToldToPlay, -2000);
            }

            // If we have a gift offer, accept the gift by setting it as our grab target
            // and releasing any held item.
            if (base.friendTracker.giftOfferedToMe?.item != null)
            {
                this.mouse.ReleaseGrasp(0);
                this.data.GrabTarget = base.friendTracker.giftOfferedToMe.item;
            }

            // Behavior state switching
            this.DecideBehavior();

            // If we're not in any of the vanilla behaviors, undo the modifications those behaviors apply in the vanilla update
            if (this.behavior != Behavior.Idle && this.behavior != Behavior.Flee && this.behavior != Behavior.EscapeRain)
            {
                this.mouse.runSpeed = prevRunSpeed;
                this.dangle = null;
                this.walkWithMouse = null;
                this.wantToSleep = false;
            }

            // If we have a grab target and it's something we can grab, grab it
            if (this.data.GrabTarget != null && this.CanGrabItem(this.data.GrabTarget))
            {
                this.mouse.GrabItem(this.data.GrabTarget);
                this.data.GrabTarget = null;

                // If the item was a gift offered by our friend, tell the tracker we successfully took the gift
                if (this.friendTracker?.giftOfferedToMe != null)
                {
                    this.GiftRecieved(this.friendTracker.giftOfferedToMe);
                    this.friendTracker.giftOfferedToMe = null;  // Clear the offer
                }
            }

            UnityEngine.Debug.Log("Current behavior: " + this.behavior);

            // If we're being held by the player and are tamed, keep the current position
            // TODO: Handle held but not tamed
            if (this.behavior == Behavior.HeldByPlayer && this.mouse.IsBefriended())
            {
                this.creature.abstractAI.SetDestination(this.mouse.abstractCreature.pos);
            }
            else
            {
                WorldCoordinate destination = this.creature.abstractAI.parent.pos;

                if (this.behavior == Behavior.Flee)
                {
                    destination = base.threatTracker.FleeTo(this.mouse.abstractCreature.pos, 10, 30, true);

                    // Scan the room to see if it can turn and retaliate with a spear throw while fleeing
                    for (int j = 0; j < base.tracker.CreaturesCount; j++)
                    {
                        Tracker.CreatureRepresentation threatRep = base.tracker.GetRep(j);
                        AbstractCreature absThreat = threatRep?.representedCreature;

                        if (absThreat?.realizedCreature is not Creature threat || base.threatTracker.GetThreatCreature(absThreat) == null)
                        {
                            continue;
                        }

                        if (threat.bodyChunks.Length == 0) continue;

                        // Pick a random body chunk target on the threat to aim at
                        int targetChunkIndex = UnityEngine.Random.Range(0, threat.bodyChunks.Length);

                        // Calculate the mouse's counter-attack chance based on its personality
                        float attackChance = Mathf.Lerp(0.035f, 0.1f, this.creature.personality.aggression);

                        if (this.HasLethal(threat) &&
                            this.GoodAttackPos(threatRep, targetChunkIndex) &&
                            UnityEngine.Random.value < attackChance)
                        {
                            BodyChunk targetChunk = threat.bodyChunks[targetChunkIndex];
                            float horizontalDelta = targetChunk.pos.x - this.mouse.firstChunk.pos.x;

                            // Mark a directional flag (-1 for left, 1 for right) to throw at the target
                            this.data.ThrowAtTarget = (int)Mathf.Sign(horizontalDelta);
                        }
                    }
                }
                else if (this.behavior == Behavior.Follow)
                {
                    destination = base.friendTracker.friendDest;
                }
                else if (this.behavior == Behavior.GrabItem)
                {
                    if (this.data.GrabTarget != null)
                    {
                        destination = this.data.GrabTarget.abstractPhysicalObject.pos;
                    }
                }
                else if (this.behavior == Behavior.Attack)
                {
                    UnityEngine.Debug.Log("Attacking target: " + (
                        this.AttackingThreat() ?
                            base.threatTracker.mostThreateningCreature?.representedCreature?.realizedCreature?.GetType().Name :
                            base.preyTracker.MostAttractivePrey?.representedCreature?.realizedCreature?.GetType().Name
                    ));

                    this.AttackUpdate(
                        ref destination,
                        this.AttackingThreat() ?
                            base.threatTracker.mostThreateningCreature :
                            base.preyTracker.MostAttractivePrey
                    );
                }
                else if (this.behavior == Behavior.Idle)
                {
                    if (this.data.abstractAI.ToldToStay != null)
                    {
                        destination = this.data.abstractAI.ToldToStay.Value;
                    }
                    else
                    {
                        /*
                        TODO: Idle behavior logic
                        WorldCoordinate? worldCoordinate = this.IdleBehavior();
                        if (worldCoordinate != null)
                        {
                            this.lastIdleSpot = new WorldCoordinate?(worldCoordinate.Value);
                        }
                        if (this.lastIdleSpot != null)
                        {
                            destination = this.lastIdleSpot.Value;
                        }
                        */
                    }
                }

                this.creature.abstractAI.SetDestination(destination);
            }
        }

        public bool WantsToEatThis(PhysicalObject obj)
        {
            // The mouse won't want to eat anything if it's already full
            if (this.data.IsFull) return false;

            // The mouse will eat anything that is explicitly edible
            if (obj is IPlayerEdible edibleItem && edibleItem.Edible) return true;

            // The mouse will consider "edible" any corpse with meat left on it that's edible by a Slugcat player in the room
            if (obj is Creature critter && critter.dead && this.TheoreticallyEatMeat(critter, false)) return true;

            // Return false if no condition was met
            return false;
        }

        private bool CanGrabItem(PhysicalObject obj)
        {
            bool hasFreeGrasp = this.mouse.grasps == null || this.mouse.grasps.Any(g => g == null);
            bool isBeingHeld = obj.grabbedBy.Count > 0;
            bool isCloseEnough = Vector2.Distance(this.mouse.mainBodyChunk.pos, obj.firstChunk.pos) < 40f;

            return hasFreeGrasp && isCloseEnough && !isBeingHeld;
        }

        private void DefineFollowCloseness()
        {
            // If our companion is in a completely different room, make following them a priority
            if (base.friendTracker.friend.room != this.mouse.room)
            {
                this.data.FollowCloseness = 1f;
                return;
            }

            // How distracted is the mouse? Scales up to 2000 frames
            float playFactor = (float)this.data.ToldToPlay / 2000f;

            // How long has the mouse been in the current room? Scales up to 6000 frames
            float roomComfortFactor = (float)Mathf.Clamp(this.timeInRoom, 0, 6000) / 6000f;

            // Is there an active threat nearby? Threat level is heavily amplified
            float fearFactor = base.threatTracker.ThreatOfArea(this.creature.pos, false) * 3f;

            // Combine the modifiers into a single distraction score
            float distractionScore = playFactor + roomComfortFactor + fearFactor;

            // Clamp the total distraction between -1 and 1
            float clampedDistraction = Mathf.Clamp(distractionScore, -1f, 1f);

            // Invert the value. High distraction means low follow closeness.
            // High fear means a negative distraction score, which turns into maximum (1f) closeness.
            this.data.FollowCloseness = Mathf.Clamp01(1f - clampedDistraction);
        }

        private void DecideBehavior()
        {
            if (this.mouse.grabbedBy.Count > 0 && this.mouse.grabbedBy[0].grabber is Player)
            {
                this.behavior = Behavior.HeldByPlayer;
                return;
            }

            /*
            TODO: Handle mouse on back
            if (this.mouse.onBack != null)
            {
                this.behavior = Behavior.OnHead;
                return;
            }
            */

            /*
            TODO: Handle thrown behavior
            if (this.behavior == Behavior.Thrown && this.mouse.bodyMode == Player.BodyModeIndex.Default)
            {
                return;
            }
            */

            // Update the utility tracker weights based on our current situation and personality,
            // so that the utility comparer can make informed decisions about which behavior to pick.
            this.UpdateUtilityTrackerWeights();

            // Check which AI module has the highest utility and switch behavior based on that
            AIModule aiModule = base.utilityComparer.HighestUtilityModule();
            float highestUtility = base.utilityComparer.HighestUtility();

            if (aiModule != null && highestUtility > 0.2f)
            {
                if (aiModule is ThreatTracker)
                {
                    if (this.AttackingThreat())
                    {
                        this.behavior = Behavior.Attack;
                    }
                    else
                    {
                        this.behavior = Behavior.Flee;
                    }
                }
                else if (aiModule is FriendTracker && this.data.abstractAI.ToldToStay == null)
                {
                    this.behavior = Behavior.Follow;
                }
                else if (aiModule is PreyTracker && (aiModule as PreyTracker).MostAttractivePrey != null)
                {
                    this.behavior = Behavior.Attack;
                }
            }
            else
            {
                this.behavior = (
                    (
                        base.friendTracker.friend != null &&
                        this.data.FollowCloseness > 0f &&
                        this.data.abstractAI.ToldToStay == null
                    ) ? Behavior.Follow : Behavior.Idle
                );
            }

            // If we have a grab target and our utility is low or we're currently following, switch to the GrabItem behavior to prioritize picking up the item
            if (this.data.GrabTarget != null && (highestUtility <= 0.2f || this.behavior == Behavior.Follow))
            {
                this.behavior = Behavior.GrabItem;
            }

            /*
            DELETE
            if (data.GrabTarget != null && base.threatTracker.Utility() < 0.2f && base.rainTracker.Utility() < 0.2f)
            {
                // First, check if the gift reachable
                if (base.pathFinder.CoordinateReachableAndGetbackable(data.GrabTarget.abstractPhysicalObject.pos))
                {
                    // Switch behavior so the pathfinder starts moving us to the gift
                    UnityEngine.Debug.Log("Switching to GrabItem behavior. Utility: " + this.currentUtility);
                    this.behavior = Behavior.GrabItem;
                }
                else
                {
                    if (data.GrabTarget.firstChunk.vel.magnitude < 1f)
                    {
                        UnityEngine.Debug.Log("Gift has landed but is unreachable! Ignoring it.");
                        data.GrabTarget = null;
                        if (this.friendTracker != null) this.friendTracker.giftOfferedToMe = null;
                    }
                    else
                    {
                        UnityEngine.Debug.Log("Gift is mid-air... waiting for it to land.");
                    }
                }
            }
            */
        }

        // Updates the weights of the utility trackers based on the mouse's current situation and personality traits
        private void UpdateUtilityTrackerWeights()
        {
            var preyTracker = base.utilityComparer.GetUtilityTracker(base.preyTracker);
            var friendTracker = base.utilityComparer.GetUtilityTracker(base.friendTracker);
            var threatTracker = base.utilityComparer.GetUtilityTracker(base.threatTracker);

            var personality = this.mouse.abstractCreature.personality;
            bool isToldToStay = data.abstractAI.ToldToStay != null;

            float rawPreyWeight = 0f;

            if (isToldToStay)
            {
                rawPreyWeight = 0f; // Won't hunt if told to wait
            }
            else if (this.data.IsFull)
            {
                // When full, more aggressive creatures might still hunt
                float aggressionFactor = Mathf.Lerp(0.6f, 1f, personality.aggression);
                rawPreyWeight = Mathf.Lerp(0f, 0.4f, aggressionFactor);
            }
            else
            {
                // If hungry, baseline is 1f, reduced if we have a friend to follow
                float socialDistraction = (base.friendTracker != null) ? Mathf.Clamp01(1f - this.data.FollowCloseness) : 1f;
                rawPreyWeight = socialDistraction - (base.friendTracker.Urgency * 0.5f);
            }

            float sympathyDampener = Mathf.InverseLerp(0.95f, 0.65f, personality.sympathy);
            preyTracker.weight = rawPreyWeight * sympathyDampener;

            // Calculate friend social urgency
            friendTracker.weight = isToldToStay ? 0f : this.data.FollowCloseness;

            // Calculate threat safety urgency
            bool threatIsUnreachable = false;
            var mostThreatening = base.threatTracker.mostThreateningCreature;

            if (mostThreatening?.representedCreature?.abstractAI?.RealAI?.pathFinder is PathFinder threatPath)
            {
                if (!threatPath.CoordinateReachable(this.data.abstractAI.parent.pos))
                {
                    threatIsUnreachable = true;
                }
            }

            if (threatIsUnreachable)
            {
                // If the threat can't reach us, calculate the fear weight based on bravery
                threatTracker.weight = 0.75f - 0.7f * Mathf.Pow(personality.bravery, 0.5f);
            }
            else
            {
                // The threat can reach us
                threatTracker.weight = 1f;
            }
        }

        private WorldCoordinate AttackUpdate(ref WorldCoordinate coord, Tracker.CreatureRepresentation target)
        {
            UnityEngine.Debug.Log("AttackUpdate called. Evaluating target: " + target?.representedCreature?.realizedCreature?.GetType().Name);

            if (target?.representedCreature?.realizedCreature is not Creature targetCrit)
            {
                return coord;
            }

            UnityEngine.Debug.Log("Target is a valid creature: " + targetCrit.GetType().Name);

            // If we want to eat this creature, go towards it provided we can pathfind to it,
            // or if there are no weapons nearby to grab first.
            if (this.WantsToEatThis(targetCrit) &&
               (base.pathFinder.CoordinateReachable(target.BestGuessForPosition()) || this.NearestLethalWeapon(targetCrit) == null))
            {
                coord = target.BestGuessForPosition();
                return coord;
            }

            // If we doesn't possess a lethal weapon, search for the nearest weapon
            if (!this.HasLethal(targetCrit))
            {
                data.GrabTarget = this.NearestLethalWeapon(targetCrit);

                // Head towards the weapon if found, otherwise hold current position
                coord = (data.GrabTarget != null) ? data.GrabTarget.abstractPhysicalObject.pos : coord;
                return coord;
            }

            // We have a weapon, so evaluate a throwing position
            this.FindAttackPosition(target);
            coord = this.data.AttackPos;

            if (targetCrit.bodyChunks.Length > 0)
            {
                int randomChunkIndex = UnityEngine.Random.Range(0, targetCrit.bodyChunks.Length);

                if (this.GoodAttackPos(target, randomChunkIndex))
                {
                    BodyChunk targetChunk = targetCrit.bodyChunks[randomChunkIndex];
                    float horizontalDelta = targetChunk.pos.x - this.mouse.firstChunk.pos.x;
                }
            }

            return coord;
        }

        private bool AttackingThreat()
        {
            for (int i = 0; i < base.relationshipTracker.relationships.Count; i++)
            {
                // TODO: IMPORTANT: FIX MOUSE FRIEND TRACK STATE BEING NULL
                MouseFriendAI.MouseFriendTrackState mouseFriendTrackState =
                    base.relationshipTracker.relationships[i].state as MouseFriendAI.MouseFriendTrackState;

                Tracker.CreatureRepresentation trackerRep = base.relationshipTracker.relationships[i].trackerRep;

                UnityEngine.Debug.Log("TrackerRep creature: " + trackerRep?.representedCreature?.realizedCreature?.GetType().Name);

                if (
                    base.threatTracker.mostThreateningCreature == trackerRep &&
                    (
                        (float)mouseFriendTrackState.annoyingThreat > Mathf.Lerp(
                            600f,
                            6000f,
                            0.25f * this.creature.personality.aggression + 0.75f * (1f - this.creature.personality.sympathy)
                        ) ||
                        mouseFriendTrackState.holdingAFriend ||
                        mouseFriendTrackState.hurtAFriend
                    ) &&
                    trackerRep.representedCreature != null &&
                    trackerRep.representedCreature.realizedCreature != null &&
                    (
                        this.HasLethal(trackerRep.representedCreature.realizedCreature, true) ||
                        (
                            this.NearestLethalWeapon(trackerRep.representedCreature.realizedCreature) != null &&
                            this.LethalWeaponScore(
                                this.NearestLethalWeapon(trackerRep.representedCreature.realizedCreature),
                                trackerRep.representedCreature.realizedCreature
                            ) >= 1f
                        )
                    )
                )
                {
                    return true;
                }
            }
            return false;
        }

        private PhysicalObject NearestLethalWeapon(Creature target)
        {
            float highestScore = 0f;
            PhysicalObject bestWeapon = null;

            for (int i = 0; i < base.itemTracker.ItemCount; i++)
            {
                ItemTracker.ItemRepresentation rep = base.itemTracker.GetRep(i);
                PhysicalObject obj = rep.representedItem.realizedObject;

                // Skip the item if the object isn't physically spawned yet
                if (obj == null) continue;

                // Skip the item if we are already holding it or if someone else has it
                if (this.HoldingThis(obj) || obj.grabbedBy.Count > 0) continue;

                // Skip the item if the pathfinder says we can't physically reach it
                if (!base.pathFinder.CoordinateReachable(rep.representedItem.pos)) continue;

                // Calculate Euclidean distance penalty
                float magnitude = (obj.firstChunk.pos - this.mouse.firstChunk.pos).magnitude;
                float distanceFactor = Mathf.Clamp(1f - magnitude / 2000f, 0f, 1f);

                // Run weapon scoring calculations
                float currentScore = this.LethalWeaponScore(obj, target) * distanceFactor;

                // Track the highest scoring weapon found so far
                if (currentScore > highestScore)
                {
                    highestScore = currentScore;
                    bestWeapon = obj;
                }
            }

            return bestWeapon;
        }

        private bool HasLethal(Creature creature)
        {
            return this.HasLethal(creature, false);
        }

        private bool HasLethal(Creature creature, bool actuallyLethal)
        {
            for (int i = 0; i < this.mouse.grasps.Length; i++)
            {
                if (this.mouse.grasps[i] != null && (
                    actuallyLethal ?
                        (this.LethalWeaponScore(this.mouse.grasps[i].grabbed, creature) >= 1f) :
                        (this.LethalWeaponScore(this.mouse.grasps[i].grabbed, creature) > 0f)
                    )
                )
                {
                    return true;
                }
            }
            return false;
        }

        private float LethalWeaponScore(PhysicalObject obj, Creature target)
        {
            if (obj is PuffBall && target is InsectoidCreature)
            {
                return 5f;
            }
            if (obj is Spear)
            {
                if ((obj as Spear).stuckInWall != null)
                {
                    return 0f;
                }
                if ((obj.abstractPhysicalObject as AbstractSpear).electric)
                {
                    return 2f;
                }
                if (!(obj.abstractPhysicalObject as AbstractSpear).explosive)
                {
                    return 1f;
                }
                if (!(obj as ExplosiveSpear).Ignited)
                {
                    return 0f;
                }
                return 3f;
            }
            else if (obj is Rock)
            {
                if (!this.WantsToEatThis(target))
                {
                    return 0.3f;
                }
                return 10f;
            }
            else
            {
                if (obj is ScavengerBomb || obj is JokeRifle)
                {
                    return 2f;
                }
                if (obj is SingularityBomb)
                {
                    return 0.1f;
                }
                if (
                    obj is FlareBomb && (
                        target is Spider || target.abstractCreature.creatureTemplate.type == CreatureTemplate.Type.BigSpider
                    )
                )
                {
                    return 4f;
                }
                if (obj is LillyPuck)
                {
                    return 0.75f;
                }
                if (obj is JellyFish)
                {
                    return 0.5f;
                }
                return 0f;
            }
        }

        private bool TheoreticallyEatMeat(Creature crit, bool excludeCentipedes)
        {
            // If the room isn't loaded, we default to only allowing universally edible itemsand disallowing all meat
            if (this.mouse.room == null || this.mouse.room.game == null)
            {
                return crit is IPlayerEdible;
            }

            // Loop through active players
            foreach (AbstractCreature abstractPlayer in this.mouse.room.game.Players)
            {
                if (abstractPlayer?.realizedCreature is Player player && player.room == this.mouse.room)
                {
                    SlugcatStats.Name campaign = player.SlugCatClass;

                    // Saint and Spearmaster don't eat meat
                    if (campaign == MoreSlugcatsEnums.SlugcatStatsName.Saint ||
                        campaign == MoreSlugcatsEnums.SlugcatStatsName.Spear)
                    {
                        continue;
                    }

                    // Universal food is checked first
                    if (crit is IPlayerEdible) return true;

                    // Check for meat
                    if (crit.dead && crit.State.meatLeft > 0)
                    {
                        // Centipede check only runs if there is valid meat and we aren't excluding them
                        if (!excludeCentipedes &&
                           (crit.Template.type == CreatureTemplate.Type.Centipede ||
                            crit.Template.type == CreatureTemplate.Type.Centiwing ||
                            crit.Template.type == DLCSharedEnums.CreatureTemplateType.AquaCenti ||
                            crit.Template.type == CreatureTemplate.Type.RedCentipede))
                        {
                            return true;
                        }

                        // These Slugcats do eat meat
                        if (campaign == SlugcatStats.Name.Red ||
                            campaign == MoreSlugcatsEnums.SlugcatStatsName.Artificer ||
                            campaign == MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel ||
                            campaign == MoreSlugcatsEnums.SlugcatStatsName.Gourmand)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool GoodAttackPos(Tracker.CreatureRepresentation target, int chunk)
        {
            // Ensure the target actually exists in the world physically
            if (target?.representedCreature?.realizedCreature is not Creature targetCrit) return false;

            BodyChunk targetChunk = targetCrit.bodyChunks[chunk];
            BodyChunk mouseChunk = this.mouse.mainBodyChunk;

            // Check line-of-sight and the vertical throwing arc window
            Vector2 throwDirection = Custom.DirVec(mouseChunk.pos, targetChunk.pos);

            if (throwDirection.y > 0.05f || throwDirection.y < -0.2f || !base.VisualContact(targetChunk))
            {
                return false;   // Angled too steeply up/down, or blocked by terrain
            }

            // Scan the room for bystanders to prevent accidental friendly-fire or wasted spears
            for (int i = 0; i < base.tracker.CreaturesCount; i++)
            {
                Tracker.CreatureRepresentation bystanderRep = base.tracker.GetRep(i);

                // Unpack and validate the bystander
                if (bystanderRep?.representedCreature?.realizedCreature is not Creature bystander) continue;
                if (bystanderRep == target || bystander.room != this.mouse.room) continue;

                // Skip if this isn't a creature we care about hitting or if they can't be killed
                if (!this.CareAboutHitting(bystander, targetCrit) || !this.HasLethal(bystander))
                {
                    continue;
                }

                // Calculate the spear's flight line segment
                Vector2 bystanderPos = bystander.mainBodyChunk.pos;
                Vector2 closestPointOnFireLine = Custom.ClosestPointOnLineSegment(mouseChunk.pos, targetChunk.pos, bystanderPos);

                // If the bystander is within 40 pixels of the spear's flight path, this isn't a good attack position
                if (Vector2.Distance(bystanderPos, closestPointOnFireLine) < 40f)
                {
                    return false;
                }
            }

            // The shot is clear of any immediate friendly-fire risks and has an
            // appropriate throwing angle, so it's a good attack position.
            return true;
        }

        private bool CareAboutHitting(Creature crit, Creature intendedTarget)
        {
            // The mouse only cares about hitting creatures that it wants to eat
            return
            crit is SmallNeedleWorm ||
            (!crit.abstractCreature.creatureTemplate.smallCreature && crit.canBeHitByWeapons) ||
            (this.AttackingPrey() && this.TheoreticallyEatMeat(crit, true));
        }

        private bool AttackingPrey()
        {
            // The mouse is considered to be "attacking prey" if its prey tracker has a valid most attractive prey
            // that it wants to eat.
            return
            base.preyTracker.MostAttractivePrey != null &&
            base.preyTracker.MostAttractivePrey.representedCreature.realizedCreature != null &&
            this.TheoreticallyEatMeat(base.preyTracker.MostAttractivePrey.representedCreature.realizedCreature, true);
        }

        private void FindAttackPosition(Tracker.CreatureRepresentation target)
        {
            Room currentRoom = this.mouse.room;
            IntVector2 targetTile = target.BestGuessForPosition().Tile;

            // Populate the target position list
            if (target.representedCreature.creatureTemplate.PreBakedPathingIndex < 0)
            {
                this.data.List = new List<IntVector2>
                {
                    target.BestGuessForPosition().Tile
                };
            }
            else
            {
                QuickConnectivity.FloodFill(
                    this.mouse.room,
                    StaticWorld.GetCreatureTemplate(MoreSlugcatsEnums.CreatureTemplateType.SlugNPC),
                    target.BestGuessForPosition().Tile,
                    20,
                    500,
                    this.data.List
                );
            }

            // Select a target tile coordinate
            IntVector2 pos;
            bool processFloodFillList = UnityEngine.Random.value < 0.5f && data.List.Count > 0;

            if (processFloodFillList)
            {
                IntVector2 randomListTile = this.data.List[UnityEngine.Random.Range(0, this.data.List.Count)];
                pos = randomListTile;

                int stepDirection = (UnityEngine.Random.value >= 0.5f) ? 1 : -1;

                // Cast a horizontal ray to find a clean standing tile
                for (int i = 0; i < 40; i++)
                {
                    IntVector2 checkTile = randomListTile + new IntVector2(stepDirection * i, 0);

                    if (currentRoom.HasAnySolid(checkTile))
                    {
                        break;  // Hit a wall
                    }

                    if (i > 5 && base.pathFinder.CoordinateViable(currentRoom.GetWorldCoordinate(checkTile)))
                    {
                        pos = checkTile;
                        break;
                    }
                }
            }
            else
            {
                // Pick a fallback random offset coordinate around the mouse
                int randomDirectionX = (UnityEngine.Random.value >= 0.5f) ? 1 : -1;
                int randomDirectionY = (UnityEngine.Random.value >= 0.5f) ? 1 : -1;

                int offsetX = UnityEngine.Random.Range(1, 10) * randomDirectionX;
                int offsetY = UnityEngine.Random.Range(1, 10) * randomDirectionY;

                pos = this.creature.pos.Tile + new IntVector2(offsetX, offsetY);
            }

            WorldCoordinate checkCoord = currentRoom.GetWorldCoordinate(pos);

            // Evaluate the candidate coordinate and switch to it if it's better than the current test position
            if (base.pathFinder.CoordinateViable(checkCoord))
            {
                float candidateScore = this.SpearThrowPositionScore(checkCoord, targetTile, this.data.List);
                float currentTestScore = this.SpearThrowPositionScore(this.data.TestThrowPos, targetTile, this.data.List);

                if (candidateScore > currentTestScore)
                {
                    data.TestThrowPos = checkCoord;
                }
            }

            // Evaluate whether we should switch our active attack position to the test position
            float testPosScore = this.SpearThrowPositionScore(this.data.TestThrowPos, targetTile, this.data.List);
            float activePosScore = this.SpearThrowPositionScore(this.data.AttackPos, targetTile, this.data.List);

            bool isStuckAtCurrent = Custom.ManhattanDistance(this.creature.pos, data.AttackPos) < 3;
            bool isTestPosSignificantlyBetter = testPosScore > activePosScore + (float)data.ChangeAttackPositionDelay;

            // Only switch if the test position is different and either we're stuck at the current position
            // or the testposition is significantly better than the current position.
            if (data.TestThrowPos != data.AttackPos && (isStuckAtCurrent || isTestPosSignificantlyBetter))
            {
                data.AttackPos = data.TestThrowPos;
                data.ChangeAttackPositionDelay = 400;

                // Record history
                data.PreviousAttackPositions.Insert(0, data.TestThrowPos.Tile);
                if (data.PreviousAttackPositions.Count > 20)
                {
                    data.PreviousAttackPositions.RemoveAt(20);
                }
            }

            // Tick down the position re-evaluation buffer timer
            if (data.ChangeAttackPositionDelay > 0)
            {
                data.ChangeAttackPositionDelay--;
            }
        }

        private float SpearThrowPositionScore(
            WorldCoordinate candidatePos,
            IntVector2 targetPosition,
            List<IntVector2> targetMovementArea
        )
        {
            // IF the AI can't pathfind to the candidate position, it's a useless tile
            if (!base.pathFinder.CoordinateViable(candidatePos))
            {
                return float.MinValue;
            }

            Room currentRoom = this.mouse.room;

            // IMPORTANT: Ensure that this creature template works
            QuickConnectivity.FloodFill(
                currentRoom,
                this.creature.creatureTemplate,
                candidatePos.Tile,
                40,
                500,
                this.data._CachedFloodFillList
            );

            // Find the vertical bounds of the target's movement area
            int targetMaxY = int.MinValue;
            int targetMinY = int.MaxValue;

            foreach (IntVector2 tile in targetMovementArea)
            {
                if (tile.y > targetMaxY) targetMaxY = tile.y;
                if (tile.y < targetMinY) targetMinY = tile.y;
            }

            float positionScore = 0f;

            // Calculate the initial line-of-sight and spear trajectory score
            foreach (IntVector2 fillTile in data._CachedFloodFillList)
            {
                // Check if this flood-filled neighbor tile is vertically level with the target's movement range
                bool withinTargetVerticalRange = fillTile.y >= targetMinY && fillTile.y <= targetMaxY;

                if (withinTargetVerticalRange && currentRoom.VisualContact(fillTile, targetPosition))
                {
                    // Find how many horizontal lanes have completely clear sight-lines for throwing a spear
                    foreach (IntVector2 moveTile in targetMovementArea)
                    {
                        if (fillTile.y == moveTile.y && this.NoSolidTilesBetween(fillTile.x, moveTile.x, fillTile.y))
                        {
                           positionScore += 1f;
                        }
                    }
                }
            }

            // Set baseline score weights based on initial tactical advantage
            if (positionScore == 0f)
            {
                positionScore = 1f;     // Minimum score for having line of sight but no clear horizontal lanes
            }
            else
            {
                positionScore += 100f;  // Massive bonus for having clear horizontal firing lanes
            }

            // Double the score if this tile is near our current body
            foreach (IntVector2 fillTile in data._CachedFloodFillList)
            {
                if (fillTile.FloatDist(this.creature.pos.Tile) < 3f)
                {
                    positionScore *= 2f;
                    break;
                }
            }

            // Double the score if it moves us toward our pathfinder destination
            if (base.pathFinder.GetDestination.room == this.creature.pos.room)
            {
                IntVector2 destinationTile = base.pathFinder.GetDestination.Tile;

                foreach (IntVector2 fillTile in data._CachedFloodFillList)
                {
                    if (fillTile.FloatDist(destinationTile) < 3f)
                    {
                        positionScore *= 2f;
                        break;
                    }
                }
            }

            // Distance & safety dampening factors
            float currentDistance = candidatePos.Tile.FloatDist(targetPosition);

            // Apply distance penalty maps (prefers mid-range positioning over being
            // really far or really close).
            positionScore *= Custom.LerpMap(currentDistance, 30f, 60f, 1f, 0f);
            positionScore *= Custom.LerpMap(currentDistance, 5f, 0f, 1f, 0.1f);

            // Reduce score based on environmental danger / active threats on that tile
            positionScore *= 1f - base.threatTracker.ThreatOfArea(candidatePos, true);

            // If there's major height difference, check if distance justifies a throw angle
            if (Math.Abs(candidatePos.Tile.y - targetPosition.y) >= 3)
            {
                positionScore *= Mathf.InverseLerp(5f, 10f, currentDistance);
            }

            // Heavy penalty if we are currently evaluating the exact location we are already heading to
            if (base.pathFinder.GetDestination.Tile == candidatePos.Tile)
            {
                positionScore *= 0.1f;
            }

            // Deduct points if this position overlaps with where the mouse recently stood
            for (int n = 1; n < this.data.PreviousAttackPositions.Count; n++)
            {
                float historyDistance = candidatePos.Tile.FloatDist(this.data.PreviousAttackPositions[n]);
                positionScore -= Custom.LerpMap(historyDistance, 0f, 5f, 50f, 0f);
            }

            return positionScore;
        }

        private bool NoSolidTilesBetween(int xA, int xB, int y)
        {
            // Ensure xA is always the starting point (left) and xB is the ending point (right)
            if (xB < xA)
            {
                (xA, xB) = (xB, xA);
            }

            // Scan every tile horizontally along the row 'y'
            for (int x = xA; x <= xB; x++)
            {
                // If even any tile contains solid terrain, the firing lane is blocked
                if (this.mouse.room.HasAnySolid(x, y))
                {
                    return false;
                }
            }

            // The lane is completely clear
            return true;
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

        private void Communicate(Player player)
        {
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
                if (this.mouse.grasps[i]?.grabbed == obj)
                {
                    return true;
                }
            }
            return false;
        }

        internal new class Behavior : MouseAI.Behavior
        {
            internal static readonly MouseAI.Behavior Follow = new MouseAI.Behavior("Follow", register: true);
            internal static readonly MouseAI.Behavior GrabItem = new MouseAI.Behavior("GrabItem", register: true);
            internal static readonly MouseAI.Behavior Eat = new MouseAI.Behavior("Eat", register: true);
            internal static readonly MouseAI.Behavior HeldByPlayer = new MouseAI.Behavior("HeldByPlayer", register: true);
            internal static readonly MouseAI.Behavior OnHead = new MouseAI.Behavior("OnHead", register: true);
            internal static readonly MouseAI.Behavior Attack = new MouseAI.Behavior("Attack", register: true);

            internal Behavior(string value, bool register = false) : base(value, register) {}
        }

        internal class MouseFriendTrackState : RelationshipTracker.TrackedCreatureState
        {
            internal bool holdingAFriend;
            internal bool jawsOccupied;
            internal bool hurtAFriend;
            internal int annoyingThreat;
        }
    }
}
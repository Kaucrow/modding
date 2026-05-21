using System;
using System.Collections.Generic;
using UnityEngine;
using RWCustom;
using MoreSlugcats;
using MouseFriends.Data;
using MouseFriends.Extensions;

namespace MouseFriends.AI
{
    internal class MouseFriendAI : MouseAI, IUseItemTracker, FriendTracker.IHaveFriendTracker, IReactToSocialEvents
    {
        // DEBUG
        /*
        private WorldCoordinate? travelTo = null;
        */

        public MouseFriendAI(AbstractCreature creature, World world) : base(creature, world)
        {
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

            if (this.mouse.GetFriendData() is not MouseFriendData data) return;

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

            // Accept gifts
            if (this.friendTracker?.giftOfferedToMe?.item != null)
            {
                UnityEngine.Debug.Log("Gift offered: " + this.friendTracker.giftOfferedToMe.item);
                UnityEngine.Debug.Log("Current utility: " + this.currentUtility);
                this.mouse.ReleaseGrasp(0);
                data.GrabTarget = this.friendTracker.giftOfferedToMe.item;
            }

            // Behavior state switching
            this.DecideBehavior(ref data);

            // If we're not in any of the vanilla behaviors, undo the modifications those behaviors apply in the vanilla update
            if (this.behavior != Behavior.Idle && this.behavior != Behavior.Flee && this.behavior != Behavior.EscapeRain)
            {
                this.mouse.runSpeed = prevRunSpeed;
                this.dangle = null;
                this.walkWithMouse = null;
                this.wantToSleep = false;
            }

            this.behavior = Behavior.Attacking;
            // Update according to the current behavior
            if (this.behavior == Behavior.OnHead)
            {
                // TODO
            }
            else if (this.behavior == Behavior.BeingHeld && this.mouse.IsBefriended())
            {
                // TODO
            }
            else if (this.behavior == Behavior.GrabItem && data.GrabTarget != null)
            {
                UnityEngine.Debug.Log("Attempting to grab: " + data.GrabTarget);

                // Walk towards the item
                data.Destination = data.GrabTarget.abstractPhysicalObject.pos;

                // Check if the mouse pup is physically close enough to grab it
                if (Vector2.Distance(this.mouse.mainBodyChunk.pos, data.GrabTarget.firstChunk.pos) < 40f)
                {
                    UnityEngine.Debug.Log("Close enough! Grabbing: " + data.GrabTarget);
                    this.NPCForceGrab(data.GrabTarget);

                    // Tell the tracker we successfully took the gift
                    if (this.friendTracker?.giftOfferedToMe != null)
                    {
                        this.GiftRecieved(this.friendTracker.giftOfferedToMe);
                        this.friendTracker.giftOfferedToMe = null;  // Clear the offer
                    }

                    // Reset the target and go back to normal behavior
                    this.behavior = Behavior.Idle;
                    data.GrabTarget = null;
                }
            }
            else if (this.behavior == Behavior.Attacking)
            {
                this.AttackUpdate(
                    ref data,
                    this.AttackingThreat() ?
                        base.threatTracker.mostThreateningCreature :
                        base.preyTracker.MostAttractivePrey
                );
            }

            this.creature.abstractAI.SetDestination(data.Destination);
        }

        private void DecideBehavior(ref MouseFriendData data)
        {
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
        }

        private WorldCoordinate AttackUpdate(ref MouseFriendData data, Tracker.CreatureRepresentation target)
        {
            if (target != null && target.representedCreature != null && target.representedCreature.realizedCreature != null)
            {
                if (
                    this.WantsToEatThis(target.representedCreature.realizedCreature) &&
                    (
                        base.pathFinder.CoordinateReachable(target.BestGuessForPosition()) ||
                        this.NearestLethalWeapon(target.representedCreature.realizedCreature) == null
                    )
                )
                {
                    data.Destination = target.BestGuessForPosition();
                }
                else if (!this.HasLethal(target.representedCreature.realizedCreature))
                {
                    data.GrabTarget = this.NearestLethalWeapon(target.representedCreature.realizedCreature);
                    data.Destination = ((data.GrabTarget != null) ? data.GrabTarget.abstractPhysicalObject.pos : data.Destination);
                }
                else
                {
                    this.FindAttackPosition(ref data, target);
                    data.Destination = data.AttackPos;
                    int num = UnityEngine.Random.Range(0, target.representedCreature.realizedCreature.bodyChunks.Length - 1);
                    if (this.GoodAttackPos(target, num))
                    {
                        BodyChunk bodyChunk = target.representedCreature.realizedCreature.bodyChunks[num];
                        data.ThrowAtTarget = (int)Mathf.Sign(bodyChunk.pos.x - this.mouse.firstChunk.pos.x);
                    }
                }
            }

            return data.Destination;
        }

        private bool AttackingThreat()
        {
            for (int i = 0; i < base.relationshipTracker.relationships.Count; i++)
            {
                MouseFriendAI.MouseFriendTrackState mouseFriendTrackState =
                    base.relationshipTracker.relationships[i].state as MouseFriendAI.MouseFriendTrackState;

                Tracker.CreatureRepresentation trackerRep = base.relationshipTracker.relationships[i].trackerRep;

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

        public bool WantsToEatThis(PhysicalObject obj)
        {
            // The mouse won't want to eat anything if it's already full
            if (this.mouse.IsFull()) return false;

            // The mouse will eat anything that is explicitly edible
            if (obj is IPlayerEdible edibleItem && edibleItem.Edible) return true;
            
            // The mouse will consider "edible" any corpse with meat left on it that's edible by a Slugcat player in the room
            if (obj is Creature critter && critter.dead && this.TheoreticallyEatMeat(critter, false)) return true;

            // Return false if no condition was met
            return false;
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

        private void FindAttackPosition(ref MouseFriendData data, Tracker.CreatureRepresentation target)
        {
            Room currentRoom = this.mouse.room;
            IntVector2 targetTile = target.BestGuessForPosition().Tile;
            
            // Populate the target position list
            if (target.representedCreature.creatureTemplate.PreBakedPathingIndex < 0)
            {
                data.List = new List<IntVector2>
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
                    data.List
                );
            }
            
            // Select a target tile coordinate
            IntVector2 pos;
            bool processFloodFillList = UnityEngine.Random.value < 0.5f && data.List.Count > 0;

            if (processFloodFillList)
            {
                IntVector2 randomListTile = data.List[UnityEngine.Random.Range(0, data.List.Count)];
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
                float candidateScore = this.SpearThrowPositionScore(ref data, checkCoord, targetTile, data.List);
                float currentTestScore = this.SpearThrowPositionScore(ref data, data.TestThrowPos, targetTile, data.List);

                if (candidateScore > currentTestScore)
                {
                    data.TestThrowPos = checkCoord;
                }
            }

            // Evaluate whether we should switch our active attack position to the test position
            float testPosScore = this.SpearThrowPositionScore(ref data, data.TestThrowPos, targetTile, data.List);
            float activePosScore = this.SpearThrowPositionScore(ref data, data.AttackPos, targetTile, data.List);

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
            ref MouseFriendData data,
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
                data._CachedFloodFillList
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
            for (int n = 1; n < data.PreviousAttackPositions.Count; n++)
            {
                float historyDistance = candidatePos.Tile.FloatDist(data.PreviousAttackPositions[n]);
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
            if (this.mouse.GetFriendData() is not MouseFriendData data) return;

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

        internal new class Behavior : MouseAI.Behavior
        {
            internal static readonly MouseAI.Behavior GrabItem = new MouseAI.Behavior("GrabItem", register: true);
            internal static readonly MouseAI.Behavior Eat = new MouseAI.Behavior("Eat", register: true);
            internal static readonly MouseAI.Behavior OnHead = new MouseAI.Behavior("OnHead", register: true);
            internal static readonly MouseAI.Behavior BeingHeld = new MouseAI.Behavior("BeingHeld", register: true);
            internal static readonly MouseAI.Behavior Attacking = new MouseAI.Behavior("Attacking", register: true);

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
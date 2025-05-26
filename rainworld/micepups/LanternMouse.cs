using UnityEngine;
using System;
using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace MicePupsMod;

public static class MicePupsManager
{
    public static ConditionalWeakTable<LanternMouse, PupData> _pupData = new();

    public static PupData GetPupData(this LanternMouse mouse)
    {
        _pupData.TryGetValue(mouse, out var data);
        return data;
    }

    public static void SetPupData(this LanternMouse mouse)
        => _pupData.Add(mouse, new PupData(mouse.abstractCreature));

    public class PupData {
        public PupData(AbstractCreature creature)
        {
            this.creature = creature;
        }

        public AbstractCreature creature;
        public PhysicalObject grabTarget;

        public MousePupAbstractAI abstractAI
		{
			get
			{
				return this.creature.abstractAI as MousePupAbstractAI;
			}
		}    
    }
}

public class MousePupAbstractAI : AbstractCreatureAI
{
    public MousePupAbstractAI(World world, AbstractCreature parent) : base(world, parent)
    {
    }

    public bool isTamed;
}

public partial class MicePupsMod
{
    private void MouseAddMark(
        On.MouseGraphics.orig_InitiateSprites orig,
        MouseGraphics self,
        RoomCamera.SpriteLeaser sLeaser,
        RoomCamera rCam
    )
    {
        orig(self, sLeaser, rCam); // Call original first

        if (self.owner is not LanternMouse) return;

        // Resize the sprites array to add space for our mark
        Array.Resize(ref sLeaser.sprites, sLeaser.sprites.Length + 2);
        int markSprite = sLeaser.sprites.Length - 1; // New index
        int markGlowSprite = sLeaser.sprites.Length - 2; // New index

        // Create the mark sprite
        sLeaser.sprites[markSprite] = new FSprite("pixel", true)
        {
            //shader = rCam.game.rainWorld.Shaders["FlatLight"],
            color = self.BodyColor,
            alpha = 1f,
            scale = 5f,
        };
        rCam.ReturnFContainer("Foreground").AddChild(sLeaser.sprites[markSprite]);

        // Create the mark sprite
        sLeaser.sprites[markGlowSprite] = new FSprite("Futile_White", true)
        {
            shader = rCam.game.rainWorld.Shaders["FlatLight"],
            color = self.BodyColor,
            alpha = 0.5f,
            scale = 1f,
        };
        rCam.ReturnFContainer("Foreground").AddChild(sLeaser.sprites[markGlowSprite]);
    }

    private void OnDraw(
        On.MouseGraphics.orig_DrawSprites orig,
        MouseGraphics self,
        RoomCamera.SpriteLeaser sLeaser,
        RoomCamera rCam,
        float timeStacker,
        Vector2 camPos
    )
    {
        int markSprite = sLeaser.sprites.Length - 1; // New index
        int markGlowSprite = sLeaser.sprites.Length - 2; // New index

        int mouseHead = 4;

        // Get interpolated head position (smooth between frames)
        Vector2 headPos = Vector2.Lerp(
            self.bodyParts[mouseHead].lastPos,
            self.bodyParts[mouseHead].pos,
            timeStacker
        );

        // Convert to camera space and add offsets
        sLeaser.sprites[markSprite].x = headPos.x - camPos.x; // <- CRUCIAL: Subtract camera position
        sLeaser.sprites[markSprite].y = (headPos.y - camPos.y) + 50f; // 15 pixels above head

        // Convert to camera space and add offsets
        sLeaser.sprites[markGlowSprite].x = headPos.x - camPos.x; // <- CRUCIAL: Subtract camera position
        sLeaser.sprites[markGlowSprite].y = (headPos.y - camPos.y) + 50f; // 15 pixels above head

        // THEN call the original method (including __instance.DrawSprites)
        orig(self, sLeaser, rCam, timeStacker, camPos);

        //  Optional post-processing
        if (self.owner is LanternMouse)
        {
            sLeaser.sprites[markSprite].MoveToFront();
        }
    }

    private void OnInitWorldRelationships(On.StaticWorld.orig_InitStaticWorldRelationships orig)
    {
        orig();

        var mouseTemplate = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.LanternMouse);
        if (mouseTemplate != null)
        {
            // Modify relationship with Slugcat
            mouseTemplate.relationships[(int)CreatureTemplate.Type.Slugcat] =
                new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0.5f);

        }
    }

    private void OnItemTrackerUpdate(On.ItemTracker.orig_Update orig, ItemTracker self)
    {
        orig(self);

        Logger.LogDebug($"Item tracker updated for creature: {self.AI.creature}");
    }

    private void OnAbstractCreatureInitiateAI(On.AbstractCreature.orig_InitiateAI orig, AbstractCreature self)
    {
        orig(self);

        if (self.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.LanternMouse)
        {
            self.abstractAI.RealAI = new MouseAIExtended(self, self.world);
        }
    }

    public static void LanternMouseCarryObject(LanternMouse __instance, bool eu)
    {
        if (__instance.graphicsModule != null)
        {
            MouseGraphics mouseGraphics = __instance.graphicsModule as MouseGraphics;
            __instance.grasps[0].grabbedChunk.MoveFromOutsideMyUpdate(eu, __instance.bodyChunks[1].pos);
        }
        else
        {
            __instance.grasps[0].grabbedChunk.MoveFromOutsideMyUpdate(eu, __instance.bodyChunks[1].pos);
        }
        __instance.grasps[0].grabbedChunk.vel = __instance.mainBodyChunk.vel;
        if (Vector2.Distance(__instance.grasps[0].grabbedChunk.pos, __instance.bodyChunks[1].pos) > 100f)
        {
            __instance.grasps[0].Release();
        }
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
        if (creatureTemplate.type == CreatureTemplate.Type.LanternMouse) // Replace with your creature type check
        {
            __instance.abstractAI = new MousePupAbstractAI(__instance.world, __instance);
        }
    }
}

public static class MousePupBehaviors
{
    public static readonly MouseAI.Behavior GrabItem = new MouseAI.Behavior("GrabItem", true);
    
    public static void Register() 
    {
        // Dummy method to force static initialization
    }
}

/*
[HarmonyPatch(typeof(StaticWorld), nameof(StaticWorld.InitStaticWorld))]
public static class StaticWorld_InitStaticWorld_Patch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        // Access the static CreatureTemplate list via reflection
        var templates = typeof(StaticWorld)
            .GetField("creatureTemplates", BindingFlags.Static | BindingFlags.NonPublic)?
            .GetValue(null) as List<CreatureTemplate>;
        

        if (templates != null)
        {
            var mouseTemplate = templates.Find(t => t.type == CreatureTemplate.Type.LanternMouse);
            if (mouseTemplate != null)
            {
                mouseTemplate.socialMemory = true;
                Console.WriteLine("Enabled LanternMouse social memory");
            }
        }
    }
}
*/

[HarmonyPatch(typeof(StaticWorld), nameof(StaticWorld.InitStaticWorld))]
public static class StaticWorld_InitStaticWorld_Patch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        try 
        {
            // Get the LanternMouse template from the array
            var mouseIndex = CreatureTemplate.Type.LanternMouse.Index;
            if (mouseIndex >= 0 && mouseIndex < StaticWorld.creatureTemplates.Length)
            {
                var mouseTemplate = StaticWorld.creatureTemplates[mouseIndex];
                if (mouseTemplate != null)
                {
                    mouseTemplate.socialMemory = true;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Template modification failed: {e}");
        }
    }
}

[HarmonyPatch(typeof(LanternMouse), nameof(LanternMouse.Update))]
public static class LanternMouse_Update_Patch
{   
    [HarmonyPostfix]
    static void Postfix(LanternMouse __instance, bool eu)
    {
        if (__instance.grasps[0] != null)
        {
            MicePupsMod.LanternMouseCarryObject(__instance, eu);
        }
    }
}

[HarmonyPatch(typeof(MouseAI), nameof(MouseAI.Update))]
class MouseAI_Update_Patch
{
    [HarmonyPostfix]
    static void Postfix(MouseAI __instance)
    {
        Console.WriteLine("Called Harmony MouseAI update");

        var data = __instance.mouse.GetPupData();

        if (data == null) return;

        // If the mouse is in a shelter and doesn't already have a friend
        if (__instance.friendTracker.friend == null && __instance.mouse.room != null /*&& __instance.mouse.room.abstractRoom.shelter*/)
        {
            for (int i = 0; i < __instance.mouse.room.game.Players.Count; i++)
            {
                if (__instance.mouse.room.game.Players[i].realizedCreature != null && __instance.mouse.room.game.Players[i].realizedCreature.room == __instance.mouse.room)
                {
                    SocialMemory.Relationship orInitiateRelationship = __instance.mouse.State.socialMemory.GetOrInitiateRelationship(__instance.mouse.room.game.Players[i].ID);
                    orInitiateRelationship.InfluenceLike(1f);
                    orInitiateRelationship.InfluenceTempLike(1f);
                }
            }
        }
        if (__instance.friendTracker.friend != null && __instance.friendTracker.friend is Player &&
        __instance.VisualContact(__instance.friendTracker.friend.firstChunk) &&
        __instance.mouse.room == __instance.friendTracker.friend.room)
        {
            Communicate(__instance, __instance.friendTracker.friend as Player);
        }
        if (__instance.friendTracker.friend != null && __instance.friendTracker.friend is Player)
        {
            data.abstractAI.isTamed = true;
        }
        if (HoldingThis(__instance, data.grabTarget))
        {
            data.grabTarget = null;
        }
        if (__instance.friendTracker.giftOfferedToMe != null && __instance.friendTracker.giftOfferedToMe.item != null)
        {
            __instance.mouse.ReleaseGrasp(0);
            data.grabTarget = __instance.friendTracker.giftOfferedToMe.item;
            Console.WriteLine("GIFT OFFERED!: " + data.grabTarget);
        }

        if (__instance.currentUtility < 0.2f && data.grabTarget != null)
        {
            Console.WriteLine("Set behavior to GrabItem");
            __instance.behavior = MousePupBehaviors.GrabItem;
        }

        if (__instance.behavior == MousePupBehaviors.GrabItem)
        {
            if (data.grabTarget != null)
            {
                __instance.creature.abstractAI.SetDestination(data.grabTarget.abstractPhysicalObject.pos);
                Console.WriteLine("Set destination to: " + __instance.creature.abstractAI.destination);
            }
        }

        if (data.grabTarget != null /*&& __instance.CanGrabItem(data.grabTarget)*/)
        {
            // Create a properly positioned debug line

            /*
            Vector2 startPos = __instance.mouse.mainBodyChunk.pos;
            Vector2 endPos = data.grabTarget.firstChunk.pos;
            float distance = Vector2.Distance(startPos, endPos);
            float angle = Mathf.Atan2(endPos.y - startPos.y, endPos.x - startPos.x) * Mathf.Rad2Deg;

            FSprite lineSprite = new FSprite("pixel")
            {
                scaleX = distance,
                rotation = angle,
                color = new Color(0, 1, 0),
                anchorX = 0 // Critical for proper positioning
            };

            DebugSprite line = new DebugSprite(
                startPos, // Starting position
                lineSprite,
                __instance.mouse.room
            )
            {
            };

            __instance.mouse.room.AddObject(line);
            */

            NPCForceGrab(__instance, data.grabTarget);
        }
    }

    /*private bool CanGrabItem(MouseAI __instance, PhysicalObject obj)
        {
            return __instance.mouse.CanIPickThisUp(obj) && this.cat.NPCGrabCheck(obj);
        }*/

    static private void NPCForceGrab(MouseAI __instance, PhysicalObject obj)
    {
        //if (this.dontGrabStuff == 0)
        {
            for (int i = 0; i < __instance.mouse.grasps.Length; i++)
            {
                if (__instance.mouse.grasps[i] == null)
                {
                    //this.SlugcatGrab(obj, i);
                    int chunkGrabbed = 0;
                    __instance.mouse.Grab(obj, i, chunkGrabbed, Creature.Grasp.Shareability.CanNotShare, 0, false, false);
                }
            }
        }
    }

    private static void Communicate(MouseAI __instance, Player player)
    {
        var data = __instance.mouse.GetPupData();

        if (data == null) return;

        Player.InputPackage[] input = player.input;
        if (input[0].jmp && !input[1].jmp && player.bodyMode != Player.BodyModeIndex.Default)
        {
            // Jump while crouched
            if (input[0].y == -1 && input[0].x == 0)
            {
                Console.WriteLine("Jump while crouched!");
                return;
            }
            // Jump with up direction held
            if (input[0].y == 1 && input[0].x == 0 && player.bodyMode != Player.BodyModeIndex.ClimbingOnBeam)
            {
                Console.WriteLine("Jump with up direction held!");
                __instance.mouse.DetatchRope();
                return;
            }
        }
    }

    private static bool HoldingThis(MouseAI __instance, PhysicalObject obj)
    {
        for (int i = 0; i < __instance.mouse.grasps.Length; i++)
        {
            if (__instance.mouse.grasps[i] != null && __instance.mouse.grasps[i].grabbed == obj)
            {
                return true;
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(ArtificialIntelligence), "VisualContact", new[] { typeof(BodyChunk) })]
class VisualContact_BodyChunk_Patch
{
    [HarmonyPostfix]
    static void Postfix(ArtificialIntelligence __instance, BodyChunk chunk, bool __result)
    {
        Console.WriteLine($"[VisualContact] Creature: {__instance.creature}, " +
            $"Target: {chunk.owner}, " +
            $"Result: {__result}");
    }
}

public class MouseAIExtended : MouseAI, IUseItemTracker, FriendTracker.IHaveFriendTracker, IReactToSocialEvents
{
    public MouseAIExtended(AbstractCreature creature, World world)
        : base(creature, world) // Pass parameters to base class
    {
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

[HarmonyPatch(typeof(MouseAI))]
[HarmonyPatch(MethodType.Constructor)]
[HarmonyPatch(new Type[] { typeof(AbstractCreature), typeof(World) })]
public static class MouseAI_Constructor_Patch
{
    [HarmonyPrefix]
    static bool Prefix(MouseAI __instance, AbstractCreature creature, World world)
    {
        Console.WriteLine("Called Harmony MouseAI constructor");

        if (!(creature.realizedCreature is LanternMouse)) return true;

        // Properly initialize base class
        typeof(ArtificialIntelligence)
            .GetConstructor(new[] { typeof(AbstractCreature), typeof(World) })
            .Invoke(__instance, new object[] { creature, world });

        __instance.mouse = creature.realizedCreature as LanternMouse;
		__instance.mouse.AI = __instance;
		__instance.AddModule(new StandardPather(__instance, world, creature));
		__instance.AddModule(new Tracker(__instance, 10, 10, 450, 0.5f, 5, 5, 10, false));
		__instance.AddModule(new ThreatTracker(__instance, 10));
		__instance.AddModule(new RainTracker(__instance));
		__instance.AddModule(new DenFinder(__instance, creature));
		__instance.AddModule(new UtilityComparer(__instance));
		__instance.AddModule(new RelationshipTracker(__instance, __instance.tracker));
        if (__instance.mouse.GetPupData() != null) {
            __instance.AddModule(new FriendTracker(__instance));
            __instance.AddModule(new ItemTracker(__instance, 10, 10, -1, -1, true));
        }
		__instance.utilityComparer.AddComparedModule(__instance.threatTracker, null, 1f, 1.1f);
		__instance.utilityComparer.AddComparedModule(__instance.rainTracker, null, 1f, 1.1f);
		__instance.behavior = MouseAI.Behavior.Idle;

        return false;
    }
}

[HarmonyPatch]
public static class Mouse_IVars_Patch
{
    static MethodBase TargetMethod()
    {
        return typeof(LanternMouse).GetMethod("GenerateIVars",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    [HarmonyPostfix]
    static void Postfix(LanternMouse __instance)
    {
        Console.WriteLine("Called Harmony LanternMouse GenerateIVars");
        Console.WriteLine($"MousePups:{MicePupsManager._pupData}");

        if (/*UnityEngine.Random.value*/ 1.0f > 0.5f)
        {
            __instance.SetPupData();
            Console.WriteLine("Mouse is pup");
        } else {
            Console.WriteLine("Mouse is not pup");
        }
    }
}
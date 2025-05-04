using UnityEngine;
using System;
using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MicePupsMod;

public static class MicePupsManager
{
    public static ConditionalWeakTable<LanternMouse, PupData> _pupData = new();

    public static bool IsPup(this LanternMouse mouse) 
        => _pupData.TryGetValue(mouse, out _);

    public static void SetPup(this LanternMouse mouse)
        => _pupData.Add(mouse, new PupData());

    public class PupData {} // Dummy data holder
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
        sLeaser.sprites[markSprite] = new FSprite("pixel", true) {
            //shader = rCam.game.rainWorld.Shaders["FlatLight"],
            color = self.BodyColor,
            alpha = 1f,
            scale = 5f,
        };
        rCam.ReturnFContainer("Foreground").AddChild(sLeaser.sprites[markSprite]); 

        // Create the mark sprite
        sLeaser.sprites[markGlowSprite] = new FSprite("Futile_White", true) {
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

        // 2. THEN call the original method (including __instance.DrawSprites)
        orig(self, sLeaser, rCam, timeStacker, camPos);

        // 3. Optional post-processing
        if (self.owner is LanternMouse)
        {
            sLeaser.sprites[markSprite].MoveToFront();
        }
    }

    private void OnInitWorldRelationships(On.StaticWorld.orig_InitStaticWorldRelationships orig) {
        orig();

         var mouseTemplate = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.LanternMouse);
        if (mouseTemplate != null)
        {
            // Modify relationship with Slugcat
            mouseTemplate.relationships[(int)CreatureTemplate.Type.Slugcat] = 
                new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0.0f);

        }
    }

    private void OnItemTrackerUpdate(On.ItemTracker.orig_Update orig, ItemTracker self) {
        orig(self);

        Logger.LogDebug($"Item tracker updated for creature: {self.AI.creature}");
        Logger.LogDebug($"Item count: {self.ItemCount}");
    }

    private void OnAbstractCreatureInitiateAI(On.AbstractCreature.orig_InitiateAI orig, AbstractCreature self) {
        orig(self);

        if (self.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.LanternMouse)
        {
            self.abstractAI.RealAI = new MouseAIWithItemTracker(self, self.world);
        }
    }
}

[HarmonyPatch(typeof(ArtificialIntelligence))]
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

public class MouseAIWithItemTracker : MouseAI, IUseItemTracker
{
    public MouseAIWithItemTracker(AbstractCreature creature, World world) 
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
        if (__instance.mouse.IsPup()) {
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

        if (UnityEngine.Random.value > 0.5f)
        {
            __instance.SetPup();
            Console.WriteLine("Mouse is pup");
        } else {
            Console.WriteLine("Mouse is not pup");
        }
    }
}
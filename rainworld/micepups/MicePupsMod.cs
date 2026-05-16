using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using HarmonyLib;
using MicePups.Hooks;
using MicePups.AI;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace MicePupsMod;

[BepInPlugin("kaucrow.micepups", "Mice Pups", "1.0.0")]
public partial class MicePupsMod : BaseUnityPlugin
{
    public static MicePupsMod Instance { get; private set; }
    private Harmony _harmony;
    private bool IsInit;

    private void OnEnable()
    {
        Instance = this;
        _harmony = new Harmony("com.kaucrow.micepups");

        MicePups.Logs.UnityLogger.Initialize();

        // Apply Harmony patches immediately on load
        try
        {
            _harmony.PatchAll();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Harmony patching failed: {ex}");
        }

        // Subscribe to the initialization hook
        On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
    }

    private void OnDisable()
    {
        // Clean up everything safely if the mod is disabled
        _harmony?.UnpatchAll();

        if (IsInit)
        {
            UnregisterHooks();
            IsInit = false;
        }
    }

    private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (IsInit) return;

        try
        {
            IsInit = true;

            // Initialize custom behavior structures
            MousePupBehaviors.Register();

            // Register all standard On hooks
            RegisterHooks();

            Logger.LogInfo("Mice Pups successfully initialized!");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Mice Pups initialization failed: {ex}");
        }
    }

    private void RegisterHooks()
    {
        On.Player.Update += PlayerOnUpdate;
        On.Player.Jump += PlayerActivateMark;
        GraphicsHooks.Apply();
        AIHooks.Apply();
        CreatureHooks.Apply();
        WorldHooks.Apply();
    }

    private void UnregisterHooks()
    {
        On.RainWorld.OnModsInit -= RainWorldOnOnModsInit;
        On.Player.Update -= PlayerOnUpdate;
        On.Player.Jump -= PlayerActivateMark;
        GraphicsHooks.Remove();
        AIHooks.Remove();
        CreatureHooks.Remove();
        WorldHooks.Remove();
    }
}
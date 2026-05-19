using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using HarmonyLib;
using MouseFriends.Hooks;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace MouseFriendsMod;

[BepInPlugin("kaucrow.tameablelanternmice", "Tameable Lantern Mice", "1.0.0")]
public partial class MouseFriendsMod : BaseUnityPlugin
{
    public static MouseFriendsMod Instance { get; private set; }
    private Harmony _harmony;
    private static bool IsInit;

    private void OnEnable()
    {
        Instance = this;
        _harmony = new Harmony("com.kaucrow.tameablelanternmice");

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

    private void RainWorldOnOnModsInit(
        On.RainWorld.orig_OnModsInit orig,
        RainWorld self
    )
    {
        orig(self);

        if (IsInit) return;

        try
        {
            IsInit = true;

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
        HUDHooks.Apply();
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
        HUDHooks.Remove();
    }
}
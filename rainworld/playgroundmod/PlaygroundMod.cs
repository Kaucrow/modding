using System;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using RWCustom;
using BepInEx;
using Debug = UnityEngine.Debug;
using MonoMod.RuntimeDetour;
using System.Data.SqlClient;  // Critical for 'On' hooks

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace TemplateMod;

[BepInPlugin("kaucrow.playgroundmod", "Playground Mod", "1.0.0")]
public partial class TemplateMod : BaseUnityPlugin
{
    private void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
    }

    private bool IsInit;
    private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (IsInit) return;

        try
        {
            IsInit = true;

            // Hooks go here
            On.Player.Update += PlayerOnUpdate;
            On.Player.Die += PlayerOnDie;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

}

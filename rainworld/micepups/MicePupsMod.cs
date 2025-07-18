﻿using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using HarmonyLib;
using System.Xml;  // Critical for 'On' hooks

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace MicePupsMod;

[BepInPlugin("kaucrow.micepups", "Mice Pups", "1.0.0")]
public partial class MicePupsMod : BaseUnityPlugin
{
    public static MicePupsMod Instance { get; private set; }
    private Harmony _harmony;

    private void OnEnable()
    {
        Instance = this;

        _harmony = new Harmony("com.kaucrow.micepups");

        On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
    }

    private void OnDisable()
    {
        _harmony?.UnpatchAll();
    }

    private bool IsInit;
    private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (IsInit) return;

        try
        {
            IsInit = true;

            // Apply Harmony patches
            _harmony.PatchAll();

            MousePupBehaviors.Register();

            // Hooks go here
            On.Player.Update += PlayerOnUpdate;
            //On.Player.Die += PlayerOnDie;
            On.Player.Jump += PlayerActivateMark;

            On.MouseGraphics.InitiateSprites += MouseAddMark;
            On.MouseGraphics.DrawSprites += OnDraw;

            On.StaticWorld.InitStaticWorldRelationships += OnInitWorldRelationships;

            On.ItemTracker.Update += OnItemTrackerUpdate;

            On.AbstractCreature.InitiateAI += OnAbstractCreatureInitiateAI;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

}
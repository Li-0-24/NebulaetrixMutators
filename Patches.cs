using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NebulaetrixMutators;

[HarmonyPatch(typeof(WorldLoader))]
internal static class WorldLoaderHook
{
    internal static Action OnWorldLoaded;

    [HarmonyPatch("Initialize")]
    [HarmonyPostfix]
    private static void InitializePostfix()
    {
        if (WindTunnelMode.Enabled) WindTunnelMode.Add();
    }

    [HarmonyPatch("GenerateLevels")]
    [HarmonyPostfix]
    private static void GenerateLevelsPostfix()
    {
        OnWorldLoaded?.Invoke();
    }
}

[HarmonyPatch(typeof(Event_Cold))]
internal static class EventColdPatch
{
    [HarmonyPatch("LateUpdate")]
    [HarmonyPostfix]
    private static void LateUpdatePostfix(Event_Cold __instance)
    {
        if (WindTunnelMode.Enabled) WindTunnelMode.Tick();
    }
}

internal static class InventoryHelpers
{
    public static void CheckItem(Item item)
    {
        if (item?.itemName == "Note") BabyMode.IsHoldingNote = false;
    }
}

[HarmonyPatch(typeof(Inventory), "ShowInventory")]
internal static class InventoryShowBlockPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => !MarkMode.Enabled;
}

[HarmonyPatch(typeof(ENT_Player), nameof(ENT_Player.Jump))]
internal static class JumpBlockPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => !LeglessMode.Enabled;
}

[HarmonyPatch(typeof(Inventory), "AddItemToHand", typeof(Item), typeof(ENT_Player.Hand))]
internal static class InventoryAddItemToHandPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref Item i)
    {
        if (i != null && i.itemName == "Note") BabyMode.IsHoldingNote = true;
    }
}

[HarmonyPatch(typeof(Inventory), "ClearItemFromHand")]
internal static class InventoryClearItemPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref Item i) => InventoryHelpers.CheckItem(i);
}

[HarmonyPatch(typeof(Inventory), "DropItemFromHand")]
internal static class InventoryDropItemFromHandPatch
{
    [HarmonyPrefix]
    private static void Prefix(int h, Inventory __instance)
    {
        var item = __instance.itemHands[h].currentItem;
        if (item != null) InventoryHelpers.CheckItem(item);
    }
}

[HarmonyPatch(typeof(Inventory), "DestroyItemInHand")]
internal static class InventoryDestroyItemInHandPatch
{
    [HarmonyPrefix]
    private static void Prefix(int h, Inventory __instance)
    {
        var item = __instance.itemHands[h].currentItem;
        if (item != null) InventoryHelpers.CheckItem(item);
    }
}

[HarmonyPatch(typeof(Inventory), "AddItemToInventoryScreen")]
internal static class InventoryAddToScreenPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref Item item) => InventoryHelpers.CheckItem(item);
}

[HarmonyPatch(typeof(Inventory), "DropItemIntoWorld")]
internal static class InventoryDropIntoWorldPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref Item item) => InventoryHelpers.CheckItem(item);
}

[HarmonyPatch(typeof(M_Level))]
internal static class MLevelPatch
{
    [HarmonyPatch("OnLevelActivate")]
    [HarmonyPostfix]
    private static void OnLevelActivatePostfix(M_Level __instance)
    {
        if (MarathonMode.Enabled) CoroutineRunner.Run(MarathonMode.SetGiftRoomParams(__instance));
    }
}

[HarmonyPatch(typeof(UT_TriggerZone))]
internal static class TriggerZonePatch
{
    [HarmonyPatch("OnTriggerEnter")]
    [HarmonyPrefix]
    private static void OnTriggerEnterPrefix(UT_TriggerZone __instance)
    {
        if (__instance.gameObject.name != "Event-MovingSecurityZone") return;
        if (ZenMode.Enabled || BackwardMode.Enabled)
            GOHelper.Disable(__instance.gameObject);
    }
}

[HarmonyPatch(typeof(UT_PlaySoundOnStart), "PlaySound")]
internal static class SafePlaySoundOnStartPatch
{
    [HarmonyPrefix]
    private static bool Prefix(UT_PlaySoundOnStart __instance)
    {
        if (__instance == null) return false;
        if (__instance.clips == null || __instance.clips.Count == 0) return false;
        return true;
    }
}

[HarmonyPatch(typeof(DEN_Bloodbug), "Start")]
internal static class DenBloodbugPatch
{
    [HarmonyPostfix]
    private static void Postfix(DEN_Bloodbug __instance) { if (ZenMode.Enabled) ZenMode.Bloodbug(__instance); }
}

[HarmonyPatch(typeof(DEN_DeathFloor), "Start")]
internal static class DenDeathFloorPatch
{
    [HarmonyPostfix]
    private static void Postfix(DEN_DeathFloor __instance)
    {
        if (ZenMode.Enabled || BackwardMode.Enabled)
            GOHelper.Disable(__instance.gameObject);
    }
}

[HarmonyPatch(typeof(DEN_Teeth), "Awake")]
internal static class DenTeethPatch
{
    [HarmonyPrefix]
    private static void Prefix(DEN_Teeth __instance) { if (ZenMode.Enabled) ZenMode.Teeth(__instance); }
}

[HarmonyPatch(typeof(DEN_Turret), "Start")]
internal static class DenTurretPatch
{
    [HarmonyPostfix]
    private static void Postfix(DEN_Turret __instance) { if (ZenMode.Enabled) ZenMode.Turret(__instance); }
}

[HarmonyPatch(typeof(DEN_VentThing), "Awake")]
internal static class DenVentThingPatch
{
    [HarmonyPrefix]
    private static void Prefix(DEN_VentThing __instance) { if (ZenMode.Enabled) ZenMode.VentThing(__instance); }
}

[HarmonyPatch(typeof(CL_GameManager), "EndGameSequence")]
internal static class EndGamePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool win, CL_GameManager __instance)
    {
        bool anyActive = false;
        foreach (var kv in TrinketAndBindingFramework.CustomModeRegistry.All)
        {
            if (TrinketAndBindingFramework.CustomModeRegistry.IsActive(kv.Key)) { anyActive = true; break; }
        }
        if (!anyActive) return;

        float score = __instance.GetPlayerAscent() * __instance.GetPlayerAscentRate();
        SaveRun(win, score, __instance.GetGameTime(), __instance.GetPlayerAscentRate());
    }

    private static void SaveRun(bool win, float score, float time, float ascentRate)
    {
        var activeIds = new List<string>();
        foreach (var kv in TrinketAndBindingFramework.CustomModeRegistry.All)
        {
            if (TrinketAndBindingFramework.CustomModeRegistry.IsActive(kv.Key)) activeIds.Add(kv.Key);
        }
        float final = win ? score * 5f : score;
        var run = new MutatorRunData
        {
            ModeName = string.Join("+", activeIds),
            Completed = win,
            TimeTaken = time,
            Score = Mathf.RoundToInt(final),
            AscentRate = ascentRate,
        };
        MutatorSaveManager.SaveRun(run);
    }
}

[HarmonyPatch(typeof(UI_GamemodeScreen), "LoadGamemode")]
internal static class LoadGamemodePatch
{
    private static readonly System.Reflection.FieldInfo _baseGamemodeField =
        AccessTools.Field(typeof(UI_GamemodeScreen), "baseGamemode");

    [HarmonyPrefix]
    private static bool Prefix(UI_GamemodeScreen __instance)
    {
        if (!MarathonMode.IsPlaylistNeeded) return true;
        var gm = _baseGamemodeField?.GetValue(__instance) as M_Gamemode;
        var name = gm?.gamemodeName ?? "";
        if (name.IndexOf("Campaign", StringComparison.OrdinalIgnoreCase) < 0) return true;
        return !MarathonMode.LoadLibraryLevels();
    }
}

internal static class MarathonPauseGate
{
    public const string EndingLevelName = "M4_Abyss_Outro_01";

    public static bool Decide(M_Level inst, ref bool __result)
    {
        if (!MarathonMode.IsPlaylistNeeded || inst == null) return true;
        if (inst.levelName == EndingLevelName) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(M_Level), nameof(M_Level.DoesPauseGeneration))]
internal static class MarathonDoesPauseGenerationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(M_Level __instance, ref bool __result) => MarathonPauseGate.Decide(__instance, ref __result);
}

[HarmonyPatch(typeof(M_Level), nameof(M_Level.IsPausingGeneration))]
internal static class MarathonIsPausingGenerationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(M_Level __instance, ref bool __result) => MarathonPauseGate.Decide(__instance, ref __result);
}

[HarmonyPatch(typeof(M_Level), nameof(M_Level.IsLastLevel))]
internal static class MarathonIsLastLevelPatch
{
    [HarmonyPrefix]
    private static bool Prefix(M_Level __instance, ref bool __result) => MarathonPauseGate.Decide(__instance, ref __result);
}

[HarmonyPatch(typeof(UT_Door), nameof(UT_Door.Close))]
internal static class BackwardSuppressDoorClosePatch
{
    [HarmonyPrefix]
    private static bool Prefix() => !BackwardMode.Enabled;
}

[HarmonyPatch(typeof(WorldLoader), "UnloadLevelRange")]
internal static class BackwardSuppressUnloadRangePatch
{
    [HarmonyPrefix]
    private static bool Prefix() => !BackwardMode.Enabled;
}

[HarmonyPatch(typeof(WorldLoader), nameof(WorldLoader.UnloadPreviousLevels))]
internal static class BackwardSuppressUnloadPreviousPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => !BackwardMode.Enabled;
}

[HarmonyPatch(typeof(ENT_Player))]
internal static class PlayerPatch
{
    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    private static void AwakePostfix(ENT_Player __instance)
    {
        if (DisorientedMode.Enabled) DisorientedMode.Apply(__instance);
        if (ArmlessMode.Enabled) ArmlessMode.Amputate(__instance);
        if (ZeroGravMode.Enabled) ZeroGravMode.Apply(__instance);
        if (ZenMode.Enabled) CoroutineRunner.Run(ZenMode.KillMassWhenAvailable());
    }

    [HarmonyPatch("Movement")]
    [HarmonyPrefix]
    private static void MovementPrefix(ENT_Player __instance)
    {
        if (LeglessMode.Enabled) LeglessMode.Apply(__instance);
    }

    [HarmonyPatch("Movement")]
    [HarmonyPostfix]
    private static void MovementPostfix(ENT_Player __instance)
    {
        if (ZenMode.Enabled) ZenMode.Apply(__instance);
        if (GlassMode.Enabled) GlassMode.Apply(__instance);
        if (BabyMode.Enabled) BabyMode.Apply(__instance);
    }

    [HarmonyPatch("FixedUpdate")]
    [HarmonyPostfix]
    private static void FixedUpdatePostfix(ENT_Player __instance)
    {
        if (CombustMode.Enabled) CombustMode.Roll(__instance);
    }

    [HarmonyPatch("InteractCheck")]
    [HarmonyPrefix]
    private static void InteractCheckPrefix(ref int hand, ENT_Player __instance)
    {
        if (DevilDaggerMode.Enabled) DevilDaggerMode.SpawnRebar(hand, __instance);
    }
}

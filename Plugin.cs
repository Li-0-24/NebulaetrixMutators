using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TrinketAndBindingFramework;

namespace NebulaetrixMutators;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency(TrinketAndBindingFramework.Plugin.GUID)]
public class Plugin : BaseUnityPlugin
{
    public const string GUID = "com.nebulaetrix.moremutators";
    public const string NAME = "Nebulaetrix Mutators";
    public const string VERSION = "1.0.0";

    private const string Credit = "Mode by Nebulaetrix";

    public static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;

        var harmony = new Harmony(GUID);
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes())
        {
            try { harmony.CreateClassProcessor(type).Patch(); }
            catch (System.Exception ex) { Log.LogError($"[Harmony] {type.FullName}: {ex.Message}"); }
        }

        if (!MutatorAssets.Load())
        {
            Log.LogError("AssetBundle load failed, modes will not function");
            return;
        }

        RegisterModes();
        RegisterCombos();

        CustomModeRegistry.RegisterDisabledLock("BackwardMode", "WIP");
        CustomModeRegistry.RegisterMutualLink("MarathonMode", "BackwardMode");
        CustomModeRegistry.RegisterGamemodeAlias("GM_Level_Tester", "Campaign");
        CustomModeRegistry.InjectIntoGamemode("Campaign");

        Log.LogInfo($"{NAME} {VERSION} loaded");
    }

    private static void RegisterModes()
    {
        var d = CustomModeRegistry.Difficulty.Easy;
        var m = CustomModeRegistry.Difficulty.Medium;
        var h = CustomModeRegistry.Difficulty.Hard;
        var x = CustomModeRegistry.Difficulty.Extreme;

        CustomModeRegistry.Register("DevilDaggerMode", "Devil Daggers", "Shoot rebar upon opening your hands", d, DevilDaggerMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("ZenMode", "Zen Mode", "No Mass, Bloodbugs, Teeth etc", d, ZenMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("ZeroGravMode", "Zero-G", "Zero gravity", d, ZeroGravMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("BackwardMode", "elkcunK etihW", "Start at the top and descend", m, BackwardMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("CombustMode", "Volatile", "Randomly explode", m, CombustMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("MarkMode", "Markiplier%", "No inventory", m, MarkMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("ArmlessMode", "Amputated", "Missing an arm", h, ArmlessMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("BabyMode", "Baby Knuckle", "66% smaller", h, BabyMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("DisorientedMode", "Disoriented", "Inverted camera", h, DisorientedMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("GlassMode", "Glass Knuckle", "One shot to everything", h, GlassMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("LeglessMode", "Paraplegic", "No jumping", h, LeglessMode.Set, hoverCreditText: Credit);
        CustomModeRegistry.Register("MarathonMode", "Marathon Mode", "Every level in existence", x, MarathonMode.Set,
            exclusiveSettingIds: new[] { "BackwardMode" }, hoverCreditText: Credit);
        CustomModeRegistry.Register("WindTunnelMode", "Wind Tunnel", "Constant Downward Blizzard", x, WindTunnelMode.Set, hoverCreditText: Credit);
    }

    private static void RegisterCombos()
    {
        CustomModeRegistry.RegisterCombo(new[] { "DevilDaggerMode", "CombustMode" }, "<color=#ff9500>Detonating Daggers</color>");
        CustomModeRegistry.RegisterCombo(new[] { "ZenMode", "BackwardMode" }, "<color=#ff9500>Backwards Buddha</color>");
        CustomModeRegistry.RegisterCombo(new[] { "DevilDaggerMode", "BabyMode" }, "<color=#ff4da6>Pocket Rockets</color>");
        CustomModeRegistry.RegisterCombo(new[] { "ZenMode", "WindTunnelMode" }, "<color=#00ffff>Tranquil Tempest</color>");
        CustomModeRegistry.RegisterCombo(new[] { "CombustMode", "BabyMode" }, "<color=#ff490d>Coughing Bomb vs Hydrogen Baby</color>");
        CustomModeRegistry.RegisterCombo(new[] { "CombustMode", "GlassMode" }, "<color=#ffe6cc>Glass Cannon</color>");
        CustomModeRegistry.RegisterCombo(new[] { "MarkMode", "BabyMode" }, "<color=#ffe28c>Man Child</color>");
        CustomModeRegistry.RegisterCombo(new[] { "BackwardMode", "MarathonMode" }, "<color=#cc00ff>The Backathon</color>");
        CustomModeRegistry.RegisterCombo(new[] { "DisorientedMode", "MarathonMode" }, "<color=#c0c0c0>The Lost Pilgrimage</color>");
        CustomModeRegistry.RegisterCombo(new[] { "ArmlessMode", "LeglessMode" }, "<color=#ffff00>One Knuckle to rule them all</color>");
        CustomModeRegistry.RegisterCombo(new[] { "ArmlessMode", "BabyMode" }, "<color=#ff4d4d>Birth Defect</color>");
        CustomModeRegistry.RegisterCombo(new[] { "BabyMode", "WindTunnelMode" }, "<color=#8cffff>Freezer Baby</color>");
        CustomModeRegistry.RegisterCombo(new[] { "GlassMode", "MarathonMode" }, "<color=#ff00ff>The Frail Endurance</color>");
        CustomModeRegistry.RegisterCombo(new[] { "MarathonMode", "WindTunnelMode" }, "<color=#ff0000>Masochism</color>");
        CustomModeRegistry.RegisterCombo(
            new[] { "DevilDaggerMode", "ZenMode", "ZeroGravMode", "CombustMode", "MarkMode", "ArmlessMode", "BabyMode", "DisorientedMode", "GlassMode", "LeglessMode", "MarathonMode", "WindTunnelMode" },
            "<color=#000000>Why</color>");
    }
}

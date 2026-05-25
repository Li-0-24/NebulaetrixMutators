using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NebulaetrixMutators;

internal static class MutatorAssets
{
    private static AssetBundle _bundle;

    internal static GameObject EventCold;
    internal static GameObject ChimneyStartReplacement;
    internal static GameObject EndObj;
    internal static GameObject RebarExplosion;
    internal static GameObject Rebar;
    internal static GameObject RebarExplosive;

    internal static bool Loaded { get; private set; }

    internal static bool Load()
    {
        if (Loaded) return true;
        var path = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
            "Assets", "more_mutators_assets");
        _bundle = AssetBundle.LoadFromFile(path);
        if (_bundle == null)
        {
            Plugin.Log?.LogError($"AssetBundle missing at {path}");
            return false;
        }

        bool ok = true;
        ok &= TryLoad("Assets/Neb.Assets/More Mutators/Event_Cold.prefab", out EventCold);
        ok &= TryLoad("Assets/Neb.Assets/More Mutators/Level_Hull.prefab", out ChimneyStartReplacement);
        ok &= TryLoad("Assets/Neb.Assets/More Mutators/EndObj.prefab", out EndObj);
        ok &= TryLoad("Assets/Neb.Assets/More Mutators/Item_Rebar_Hit_Explosion.prefab", out RebarExplosion);
        ok &= TryLoad("Assets/GameObject/Projectile_Rebar.prefab", out Rebar);
        ok &= TryLoad("Assets/GameObject/Projectile_Rebar_Explosive.prefab", out RebarExplosive);

        Loaded = ok;
        return ok;
    }

    private static bool TryLoad(string path, out GameObject go)
    {
        go = _bundle.LoadAsset<GameObject>(path);
        if (go == null) { Plugin.Log?.LogError($"Missing bundle asset: {path}"); return false; }
        return true;
    }
}

[Serializable]
public class MutatorRunData
{
    public string ModeName;
    public bool Completed;
    public int Score;
    public float AscentRate;
    public float TimeTaken;
}

[Serializable]
public class MutatorSaveData
{
    public Dictionary<string, MutatorRunData> ModeRuns = new Dictionary<string, MutatorRunData>();
}

internal static class MutatorSaveManager
{
    private static MutatorSaveData _cached;
    private static string SaveDir =>
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "MutatorSaves");
    private static string SavePath => Path.Combine(SaveDir, "save.json");

    internal static MutatorSaveData LoadData()
    {
        if (_cached != null) return _cached;
        try
        {
            if (!Directory.Exists(SaveDir)) Directory.CreateDirectory(SaveDir);
            if (!File.Exists(SavePath))
            {
                _cached = new MutatorSaveData();
                Write(_cached);
                return _cached;
            }
            _cached = JsonConvert.DeserializeObject<MutatorSaveData>(File.ReadAllText(SavePath))
                      ?? new MutatorSaveData();
            return _cached;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"Load save failed: {ex.Message}");
            _cached = new MutatorSaveData();
            return _cached;
        }
    }

    internal static void SaveRun(MutatorRunData run)
    {
        try
        {
            var data = LoadData();
            if (data.ModeRuns.TryGetValue(run.ModeName, out var prev) && run.Score <= prev.Score) return;
            data.ModeRuns[run.ModeName] = run;
            Write(data);
            _cached = data;
        }
        catch (Exception ex) { Plugin.Log?.LogError($"SaveRun failed: {ex.Message}"); }
    }

    private static void Write(MutatorSaveData data)
    {
        try { File.WriteAllText(SavePath, JsonConvert.SerializeObject(data, Formatting.Indented)); }
        catch (Exception ex) { Plugin.Log?.LogError($"Write failed: {ex.Message}"); }
    }
}

public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner _instance;
    public static void Run(IEnumerator co)
    {
        if (_instance == null)
        {
            _instance = new GameObject("NebMutators_CoroutineRunner").AddComponent<CoroutineRunner>();
            DontDestroyOnLoad(_instance);
        }
        _instance.StartCoroutine(co);
    }
}

internal static class BlizzardFactory
{
    public static IEnumerator CreateBlizzard(Action<Event_Cold> onComplete, float windSpeed = 0.14f, float windChangeRate = 0.1f, float coldMultiplier = 0f)
    {
        var go = UnityEngine.Object.Instantiate(MutatorAssets.EventCold);
        yield return null;
        var blizz = go.GetComponent<Event_Cold>();
        var buff = Traverse.Create(blizz).Field("coldDebuff").GetValue<BuffContainer>();
        buff.SetMultiplier(coldMultiplier);
        blizz.blizzardWindSpeed = windSpeed;
        blizz.blizzardWindChangeRate = windChangeRate;
        onComplete?.Invoke(blizz);
    }
}

internal static class GOHelper
{
    public static void Disable(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
    }
}

internal static class TooltipHelper
{
    public static void UpdateLevelTooltip(string objectPath, string levelTip, string subRegionTip = null, string regionTip = null)
    {
        var go = GameObject.Find(objectPath);
        if (go == null) return;
        var level = go.GetComponent<M_Level>();
        if (level == null) return;
        if (levelTip != null) level.introText = levelTip;
        if (subRegionTip != null && level.subRegion != null) level.subRegion.introText = subRegionTip;
        if (regionTip != null && level.region != null) level.region.introText = regionTip;
    }
}

internal static class Toggle
{
    public static void Set(ref bool field, bool value) { field = value; }
}

public static class ArmlessMode
{
    public static bool Enabled;
    public static void Set(bool v) => Toggle.Set(ref Enabled, v);

    public static void Amputate(ENT_Player player)
    {
        if (player == null) return;
        player.hands[1].SetLocked(true);
        var iR = GameObject.Find("Interact_R");
        if (iR != null) iR.SetActive(false);
        var rt = player.transform.Find("Main Cam Root/Main Camera Shake Root/Main Camera/Inventory Camera/Inventory-Root/Right_Hand_Target");
        if (rt != null) rt.gameObject.SetActive(false);

        var rightHand = player.hands.Length > 1 ? player.hands[1] : null;
        if (rightHand?.handModel != null) rightHand.handModel.gameObject.SetActive(false);

        CoroutineRunner.Run(KillUpgradeConsoles());
    }

    private static IEnumerator KillUpgradeConsoles()
    {
        yield return new WaitForSeconds(3f);
        var root = GameObject.Find("World_Root(Clone)");
        if (root == null) yield break;
        foreach (var obj in AllChildren(root).Where(o => o.name.Contains("Prop_UpgradeConsole")))
            UnityEngine.Object.Destroy(obj);
    }

    private static List<GameObject> AllChildren(GameObject parent)
    {
        var list = new List<GameObject>();
        foreach (Transform t in parent.transform)
        {
            list.Add(t.gameObject);
            list.AddRange(AllChildren(t.gameObject));
        }
        return list;
    }
}

public static class BabyMode
{
    private const float PlayerScale = 0.5f;
    private const float HoldDistance = 0.6f;
    private const float InteractDistance = 1.75f;
    private const float JumpHeight = 0.3f;
    private const float Speed = 1f;
    private const float SprintSpeed = 1.5f;

    public static bool Enabled;
    internal static bool IsHoldingNote;

    public static void Set(bool v) => Toggle.Set(ref Enabled, v);

    public static void Apply(ENT_Player player)
    {
        ApplyAttributes(player);
        AdjustWeapons();
        if (!IsHoldingNote)
            player.gravity = -0.2f;
        else
            player.gravity = (bool)Traverse.Create(player).Field("falling").GetValue() ? 0.1f : -0.2f;
    }

    private static void ApplyAttributes(ENT_Player player)
    {
        player.transform.localScale = new Vector3(PlayerScale, PlayerScale, PlayerScale);
        Traverse.Create(player).Field("holdDistance").SetValue(HoldDistance);
        player.interactDistance = InteractDistance;
        player.jumpHeight = JumpHeight;
        player.speed = Speed;
        player.sprintSpeed = SprintSpeed;
    }

    private static void AdjustWeapons()
    {
        string[] names = { "Item_Hands_Rebar(Clone)", "Item_Hands_Rebar_Explosive(Clone)",
            "Item_Hands_RebarRope(Clone)", "Item_Hands_Rubble(Clone)", "Item_Hands_Flaregun(Clone)" };
        foreach (var n in names)
        {
            try
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var shoot = go.GetComponent<HandItem_Shoot>();
                if (shoot == null) continue;
                shoot.maxRecoilMagnitude = n.Contains("Flaregun") ? 1.5f : 0.513f;
                shoot.recoil = n.Contains("Flaregun") ? 4.5f : 3.3f;
            }
            catch { }
        }
    }
}

public static class BackwardMode
{
    public static bool Enabled;
    private const float PollTimeoutSeconds = 8f;
    private static readonly Vector3 SpawnOffset = new Vector3(10.47f, 41.24f, 5.30f);

    internal static M_Level ResolveTopRealLevel(WorldLoader.BranchInfo branch)
    {
        if (branch?.levelTracker == null) return null;
        for (int i = branch.levelTracker.Count - 1; i >= 0; i--)
        {
            var lvl = branch.levelTracker[i]?.GetLevel();
            if (lvl == null) continue;
            var n = lvl.levelName ?? lvl.name ?? "";
            if (n.IndexOf("Level_Null", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            return lvl;
        }
        return null;
    }

    private static readonly Action _worldLoadedHandler = () => CoroutineRunner.Run(Setup());

    public static void Set(bool v)
    {
        Toggle.Set(ref Enabled, v);
        if (v)
        {
            WorldLoaderHook.OnWorldLoaded += _worldLoadedHandler;
            BackwardDebugRunner.Ensure();
            BackwardDoorKeeper.Ensure();
        }
        else
        {
            WorldLoaderHook.OnWorldLoaded -= _worldLoadedHandler;
        }
    }

    public static IEnumerator Setup()
    {
        yield return null;

        float deadline = Time.unscaledTime + PollTimeoutSeconds;
        while (Time.unscaledTime < deadline)
        {
            var branch = WorldLoader.instance?.GetCurrentBranch();
            var player = ENT_Player.playerObject;
            if (WorldLoader.isLoaded
                && branch?.levelTracker != null && branch.levelTracker.Count > 0
                && player != null)
                break;
            yield return null;
        }

        var b = WorldLoader.instance?.GetCurrentBranch();
        var p = ENT_Player.playerObject;
        if (b?.levelTracker == null || b.levelTracker.Count == 0 || p == null) yield break;

        var topLevel = ResolveTopRealLevel(b);
        if (topLevel == null) yield break;

        var pos = topLevel.transform.position + SpawnOffset;
        p.Teleport(pos, p.transform.rotation);

        var bottomLevel = b.levelTracker[0]?.GetLevel();
        if (bottomLevel != null && MutatorAssets.EndObj != null)
            UnityEngine.Object.Instantiate(MutatorAssets.EndObj, Vector3.zero, Quaternion.identity, bottomLevel.transform);
    }
}

public class BackwardDoorKeeper : MonoBehaviour
{
    private static BackwardDoorKeeper _instance;
    private const int PollFrames = 30;
    private int _frame;

    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("NebMutators_BackwardDoorKeeper");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<BackwardDoorKeeper>();
    }

    private void Update()
    {
        if (!BackwardMode.Enabled) return;
        if ((_frame++ % PollFrames) != 0) return;

        var doors = UnityEngine.Object.FindObjectsOfType<UT_Door>();
        for (int i = 0; i < doors.Length; i++)
        {
            var door = doors[i];
            if (door == null || door.IsOpen()) continue;
            try { door.Open(); } catch { }
        }
    }
}

public class BackwardDebugRunner : MonoBehaviour
{
    private static BackwardDebugRunner _instance;

    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("NebMutators_BackwardDebug");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<BackwardDebugRunner>();
    }

    private void Update()
    {
        if (!BackwardMode.Enabled) return;
        if (!Input.GetKeyDown(KeyCode.F9)) return;

        var p = ENT_Player.playerObject;
        if (p == null) return;

        var branch = WorldLoader.instance?.GetCurrentBranch();
        var top = BackwardMode.ResolveTopRealLevel(branch);
        if (top == null) return;

        Vector3 offset = p.transform.position - top.transform.position;
        Plugin.Log?.LogInfo($"[F9] top='{top.levelName}' player={p.transform.position} top.pos={top.transform.position} offset={offset}");
    }
}

public static class CombustMode
{
    public static bool Enabled;
    private static float _accumulated;
    private const float Threshold = 1f;

    public static void Set(bool v) => Toggle.Set(ref Enabled, v);

    public static void Roll(ENT_Player player)
    {
        _accumulated += Time.deltaTime;
        if (_accumulated < Threshold) return;
        _accumulated = 0f;
        if (UnityEngine.Random.Range(0, 10) != 0) return;
        var pos = player.transform.position + UnityEngine.Random.insideUnitSphere * 1f;
        var go = UnityEngine.Object.Instantiate(MutatorAssets.RebarExplosion, pos, Quaternion.identity);
        UnityEngine.Object.Destroy(go, 5f);
    }
}

public static class DevilDaggerMode
{
    public static bool Enabled;
    private const float ShootTimer = 0.1f;
    private static float _accumulatedTime;

    private static bool _resolved;
    private static GameObject _rebarProjectilePrefab;
    private static GameObject _rebarExplosiveProjectilePrefab;
    private static float _shootSpeed;
    private static LayerMask _aimMask;

    public static void Set(bool v) => Toggle.Set(ref Enabled, v);

    private static bool ResolveShoot()
    {
        if (_resolved && _rebarProjectilePrefab != null) return true;
        _resolved = true;
        try
        {
            var rebarItemGo = CL_AssetManager.GetAssetGameObject("Item_Rebar");
            var io = rebarItemGo != null ? rebarItemGo.GetComponent<Item_Object>() : null;
            var hi = io?.itemData?.handItemAsset as HandItem_Shoot;
            if (hi == null) return false;
            _rebarProjectilePrefab = hi.projectile;
            _shootSpeed = hi.shootSpeed;
            _aimMask = hi.aimMask;
            if (_rebarProjectilePrefab == null) return false;

            try
            {
                var explosiveGo = CL_AssetManager.GetAssetGameObject("Item_Rebar_Explosive");
                var eio = explosiveGo != null ? explosiveGo.GetComponent<Item_Object>() : null;
                var ehi = eio?.itemData?.handItemAsset as HandItem_Shoot;
                _rebarExplosiveProjectilePrefab = ehi?.projectile;
            }
            catch { _rebarExplosiveProjectilePrefab = null; }

            return true;
        }
        catch { return false; }
    }

    public static void SpawnRebar(int hand, ENT_Player player)
    {
        if (player == null || Camera.main == null) return;

        _accumulatedTime += Time.deltaTime;
        if (_accumulatedTime < ShootTimer) return;
        _accumulatedTime = 0f;

        if (hand < 0 || hand >= player.hands.Length) return;
        var playerHand = player.hands[hand];
        if (playerHand == null) return;

        if (!InputManager.GetButton(playerHand.fireButton).Pressed) return;
        if (!ResolveShoot()) return;

        var prefab = (CombustMode.Enabled && _rebarExplosiveProjectilePrefab != null)
            ? _rebarExplosiveProjectilePrefab
            : _rebarProjectilePrefab;

        var cam = Camera.main.transform;
        Vector3 spawnPos = Vector3.Lerp(player.transform.position, cam.position, 0.5f)
                         + -player.transform.forward * 0.25f;
        var go = UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.LookRotation(cam.forward));

        var projComp = go.GetComponent<Projectile>();
        if (projComp == null) { UnityEngine.Object.Destroy(go); return; }
        projComp.Initialize(cam.forward * 50f, player);

        CL_CameraControl.Shake(0.01f);
        if (player.cCon != null && player.cCon.isGrounded)
            player.AddForce(-cam.forward * 0.05f);
        else
            player.AddForce(-cam.forward * 0.2f);
        player.DropHang();
    }
}

public static class DisorientedMode
{
    public static bool Enabled;
    public static void Set(bool v) => Toggle.Set(ref Enabled, v);
    public static void Apply(ENT_Player player) =>
        player.transform.Find("Main Cam Root")?.Rotate(new Vector3(0f, 0f, 180f));
}

public static class GlassMode
{
    public static bool Enabled;
    public static void Set(bool v) => Toggle.Set(ref Enabled, v);
    public static void Apply(ENT_Player player) => player.maxHealth = 0.1f;
}

public static class LeglessMode
{
    public static bool Enabled;
    public static void Set(bool v) => Toggle.Set(ref Enabled, v);

    public static void Apply(ENT_Player player)
    {
        if (player == null) return;
        Traverse.Create(player).Field("hasJumped").SetValue(true);
        player.jumpHeight = 0f;
    }
}

public static class MarathonMode
{
    public static bool Enabled;
    public static bool IsPlaylistNeeded => Enabled || BackwardMode.Enabled;

    private const string MainMenuScene = "Main-Menu";
    private const float PreloadTimer = 0.25f;
    private const string ChimneyStartPath = "Level Tester-root/World_Root(Clone)/MX_Chimney_Start_01(Clone)";
    private const float BlizzardBlendValue = 0.95f;

    private static readonly Action _worldLoadedHandler = () => CoroutineRunner.Run(ModifyLevelsAsync());

    public static void Set(bool v)
    {
        Toggle.Set(ref Enabled, v);
        if (v) WorldLoaderHook.OnWorldLoaded += _worldLoadedHandler;
        else WorldLoaderHook.OnWorldLoaded -= _worldLoadedHandler;
    }

    public static bool LoadLibraryLevels()
    {
        if (SceneManager.GetActiveScene().name != MainMenuScene) return false;
        var gm = GameObject.Find("GameManager")?.GetComponent<CL_GameManager>();
        if (gm == null) return false;

        var requested = MutatorLevelsList.OrderedLevels;
        var valid = new List<string>(requested.Count);
        foreach (var name in requested)
        {
            bool ok = false;
            try { ok = CL_AssetManager.GetLevelAsset(name) != null; } catch { ok = false; }
            if (ok) valid.Add(name);
        }
        if (valid.Count == 0) return false;

        var levelTester = CL_AssetManager.GetGamemodeAsset("GM_Level_Tester");
        if (levelTester == null) return false;
        if (levelTester.modeType != M_Gamemode.GameType.playlist)
            levelTester.modeType = M_Gamemode.GameType.playlist;

        try
        {
            var gamemodeArgsField = AccessTools.Field(typeof(CL_GameManager), "gamemodeArgs");
            var gamemodeArgs = gamemodeArgsField?.GetValue(null) as List<string>;
            if (gamemodeArgs == null) return false;

            if (!BackwardMode.Enabled) ShuffleNumberedGroups(valid);

            gamemodeArgs.Clear();
            gamemodeArgs.AddRange(valid);

            gm.SetGamemode(levelTester);
            var baseGamemodeField = AccessTools.Field(typeof(CL_GameManager), "baseGamemode");
            baseGamemodeField?.SetValue(gm, levelTester);

            SceneManager.LoadScene("Game-Main");
            return true;
        }
        catch (Exception ex) { Plugin.Log?.LogError($"LoadLibraryLevels failed: {ex}"); return false; }
    }

    private static void ShuffleNumberedGroups(List<string> levels)
    {
        int i = 0;
        while (i < levels.Count)
        {
            string prefix = StripTrailingNumber(levels[i]);
            if (prefix == null) { i++; continue; }
            int groupEnd = i + 1;
            while (groupEnd < levels.Count && StripTrailingNumber(levels[groupEnd]) == prefix)
                groupEnd++;
            int groupLen = groupEnd - i;
            if (groupLen >= 2)
            {
                for (int k = groupEnd - 1; k > i; k--)
                {
                    int j = UnityEngine.Random.Range(i, k + 1);
                    var tmp = levels[k]; levels[k] = levels[j]; levels[j] = tmp;
                }
            }
            i = groupEnd;
        }
    }

    private static string StripTrailingNumber(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        int us = name.LastIndexOf('_');
        if (us < 0 || us == name.Length - 1) return null;
        for (int k = us + 1; k < name.Length; k++)
            if (!char.IsDigit(name[k])) return null;
        return name.Substring(0, us + 1);
    }

    private static IEnumerator ModifyLevelsAsync()
    {
        yield return new WaitForSeconds(PreloadTimer);
        ModifyOtherLevels();
        ModifyEnding();
        ClearOtherLevelObjects();
    }

    private static void ModifyOtherLevels()
    {
        var chimneyStart = GameObject.Find(ChimneyStartPath);
        if (chimneyStart == null) return;
        ReplaceChimneyLevel(chimneyStart);
        TooltipHelper.UpdateLevelTooltip("Level Tester-root/World_Root(Clone)/MX_Chimney_Crystal_01(Clone)", null, "The Final Stretch");
        TooltipHelper.UpdateLevelTooltip("Level Tester-root/World_Root(Clone)/M3_Habitation_LadderWarp_Loop(Clone)", "<size=40>???</size>\n<color=grey>I lied sorgy</color>");
    }

    private static void ModifyEnding()
    {
        var ending = GameObject.Find("Level Tester-root/World_Root(Clone)/M3_Habitation_Lab_Ending(Clone)");
        var voidEnd = GameObject.Find("Level Tester-root/World_Root(Clone)/M3_Habitation_LadderWarp_Void_06(Clone)");
        if (ending == null || voidEnd == null) return;
        var entrance = ending.transform.Find("M3_Lab_Ending/Level_Entrance");
        var exit = voidEnd.transform.Find("M3_LadderWarp_Void_06/Level_GEO_Root/LU_Transition_Exit_01");
        if (entrance == null || exit == null) return;
        exit.localPosition += new Vector3(-4.5f, 0f, 0f);
        var diff = exit.position - entrance.position;
        ending.transform.position += diff;
    }

    private static void ReplaceChimneyLevel(GameObject chimneyStart)
    {
        var replacement = MutatorAssets.ChimneyStartReplacement;
        if (replacement == null) return;
        replacement.transform.localScale = Vector3.one * 0.01f;
        var fxZone = chimneyStart.transform.Find("FX_Zone.03")?.GetComponent<FX_Zone>();
        if (fxZone != null) fxZone.blend = BlizzardBlendValue;
        DestroyObjectsInChimney(chimneyStart);
        var target = chimneyStart.transform.Find("MX_Chimney_Start_01");
        if (target != null) UnityEngine.Object.Instantiate(replacement, target.transform);
    }

    private static void DestroyObjectsInChimney(GameObject chimneyStart)
    {
        string[] paths = {
            "MX_Chimney_Start_01/L_Snow.001",
            "MX_Chimney_Start_01/Lighting/Light-Yellow.004",
            "MX_Chimney_Start_01/Lighting/Light-Ambience.009",
            "MX_Chimney_Start_01/Level_Hull",
        };
        foreach (var p in paths)
        {
            var t = chimneyStart.transform.Find(p);
            if (t != null) UnityEngine.Object.Destroy(t.gameObject);
        }
    }

    private static void ClearOtherLevelObjects()
    {
        GameObject.Find("Level Tester-root/World_Root(Clone)/M3_Campaign_Transition_Pipeworks_To_Habitation_01(Clone)/Level Scripting/Goo Controllers")?.SetActive(false);
        GameObject.Find("Level Tester-root/World_Root(Clone)/M3_Habitation_LadderWarp_Loop(Clone)/WinTrigger")?.SetActive(false);
        var enter = GameObject.Find("Level Tester-root/World_Root(Clone)/M3_Habitation_Shaft_Intro(Clone)/Entities/Trigger-RoomEnter");
        if (enter != null) foreach (var g in enter.GetComponents<UT_GooController>()) UnityEngine.Object.Destroy(g);
    }

    internal static IEnumerator SetGiftRoomParams(M_Level level)
    {
        yield return new WaitForSeconds(0.5f);
        if (!level.levelName.Contains("MX_Chimney_Gift")) yield break;
        var door1 = level.transform.Find("Doors/Door_Reinforced_01")?.GetComponent<UT_Door>();
        var door2 = level.transform.Find("Doors/Door_Reinforced_01.01")?.GetComponent<UT_Door>();
        if (door1 != null)
        {
            var pos = Traverse.Create(door1).Field("startPos").GetValue<Vector3>() + new Vector3(0f, 0f, -5f);
            Traverse.Create(door1).Field("endPos").SetValue(pos);
        }
        if (door2 != null)
        {
            var pos = Traverse.Create(door2).Field("startPos").GetValue<Vector3>() + new Vector3(0f, 0f, -5f);
            Traverse.Create(door2).Field("endPos").SetValue(pos);
        }
        if (BackwardMode.Enabled)
        {
            var btn = level.transform.Find("Doors/Prop_Button_Console_01/Prop_Button_02_Switch")?.GetComponent<CL_ToggleButton>();
            btn?.Interact();
        }
    }
}

public static class MarkMode
{
    public static bool Enabled;
    public static void Set(bool v) => Toggle.Set(ref Enabled, v);
}

public static class WindTunnelMode
{
    public static bool Enabled;
    private static Event_Cold _blizzard;

    public static void Set(bool v) => Toggle.Set(ref Enabled, v);

    internal static void Add()
    {
        CoroutineRunner.Run(BlizzardFactory.CreateBlizzard(b => { _blizzard = b; _blizzard.StartBlizzard(); }, 1f, 0f));
    }

    internal static void Tick()
    {
        if (_blizzard == null) return;
        Traverse.Create(_blizzard).Field("blizzardWindDirection").SetValue(Vector3.down);
        if (!Traverse.Create(_blizzard).Field<bool>("hasBlizzard").Value) _blizzard.StartBlizzard();
    }
}

public static class ZenMode
{
    public static bool Enabled;
    public static void Set(bool v) => Toggle.Set(ref Enabled, v);

    public static void Teeth(DEN_Teeth teeth) { teeth.Despawn(); }
    public static void Turret(DEN_Turret turret) => GOHelper.Disable(turret.gameObject);
    internal static void Bloodbug(DEN_Bloodbug b) => GOHelper.Disable(b.gameObject);
    internal static void VentThing(DEN_VentThing v) => GOHelper.Disable(v.gameObject);
    public static void Apply(ENT_Player player) => player.health = 5f;

    public static IEnumerator KillMassWhenAvailable()
    {
        float deadline = Time.unscaledTime + 5f;
        while (Time.unscaledTime < deadline)
        {
            if (DEN_DeathFloor.instance != null)
            {
                DEN_DeathFloor.instance.gameObject.SetActive(false);
                yield break;
            }
            yield return null;
        }
    }
}

public static class ZeroGravMode
{
    public static bool Enabled;
    public static void Set(bool v) => Toggle.Set(ref Enabled, v);

    public static void Apply(ENT_Player player)
    {
        Traverse.Create(player).Field("hasGravity").SetValue(false);
        player.dragCoefficient = 0.05f;
    }
}

internal static class MutatorLevelsList
{
    internal static readonly List<string> OrderedLevels = new List<string>(173)
    {
        "M1_Intro_01","M1_Silos_Air_01","M1_Silos_Air_02","M1_Silos_Air_03","M1_Silos_Air_04","M1_Silos_Air_05","M1_Silos_Air_06","M1_Silos_Air_07","M1_Silos_Air_08","M1_Silos_Air_09",
        "M1_Silos_Air_10","M1_Silos_SafeArea_01","M1_Silos_Broken_01","M1_Silos_Broken_02","M1_Silos_Broken_03","M1_Silos_Broken_04","M1_Silos_Broken_05","M1_Silos_Broken_06","M1_Silos_Broken_07","M1_Silos_Broken_08",
        "M1_Silos_Broken_09","M1_Silos_Broken_10","M1_Silos_SafeArea_Endless_01","M1_Silos_Storage_01","M1_Silos_Storage_02","M1_Silos_Storage_03","M1_Silos_Storage_04","M1_Silos_Storage_05","M1_Silos_Storage_06","M1_Silos_Storage_07",
        "M1_Silos_Storage_08","M1_Silos_Storage_09","M1_Silos_Storage_10","M1_Silos_Storage_11","M1_Silos_Storage_12","M1_Silos_Storage_13","M1_Silos_Storage_14","M1_Silos_Storage_15","M1_Silos_Storage_16","Campaign_Interlude_Silo_To_Pipeworks_01",
        "M2_Pipeworks_Drainage_01","M2_Pipeworks_Drainage_02","M2_Pipeworks_Drainage_03","M2_Pipeworks_Drainage_04","M2_Pipeworks_Drainage_05","M2_Pipeworks_Drainage_06","M2_Pipeworks_Drainage_07","M2_Pipeworks_Waste_01","M2_Pipeworks_Waste_02","M2_Pipeworks_Waste_03",
        "M2_Pipeworks_Waste_04","M2_Pipeworks_Break_01","M2_Pipeworks_Organ_01","M2_Pipeworks_Organ_02","M2_Pipeworks_Organ_03","M2_Pipeworks_Organ_04","M2_Pipeworks_Organ_05","M2_Pipeworks_Organ_06","M2_Pipeworks_Organ_07","M2_Pipeworks_Organ_08",
        "Campaign_Interlude_Pipeworks_To_Habitation_03","M3_Habitation_Entryway_01","M3_Habitation_Shaft_Intro","M3_Habitation_Shaft_01","M3_Habitation_Shaft_02","M3_Habitation_Endless_Shaft_End","M3_Delta_Pier_Intro_01","M3_Delta_Pier_01","M3_Delta_Pier_02","M3_Delta_Pier_03","M3_Delta_Pier_Outro_01",
        "M3_Habitation_Endless_Shaft_Start","M3_Habitation_Shaft_03","M3_Habitation_Shaft_04","M3_Habitation_Shaft_To_Pier","M3_Habitation_Pier_Entrance_01","M3_Habitation_Pier_01",
        "M3_Habitation_Lab_Lobby","M3_Habitation_Lab_01","M3_Habitation_Lab_02","M3_Habitation_Lab_03","M3_Habitation_Lab_04","M3_Habitation_Endless_Shaft_Start","M3_Habitation_Shaft_05","M3_Habitation_Shaft_06","M3_Habitation_Shaft_To_Pier","M3_Habitation_Pier_Entrance_01",
        "M3_Habitation_Pier_02","M3_Habitation_Lab_Lobby","M3_Habitation_Lab_05","M3_Habitation_Lab_06","M3_Habitation_Lab_07","M3_Habitation_Lab_08","M3_Habitation_Endless_Breakroom_01","M3_Habitation_Endless_Shaft_Start","M3_Habitation_Shaft_07","M3_Habitation_Shaft_01",
        "M3_Habitation_Shaft_To_Pier","M3_Habitation_Pier_Entrance_01","M3_Habitation_Pier_03","M3_Habitation_Lab_Lobby","M3_Habitation_Endless_Shaft_Start","M3_Habitation_Shaft_02","M3_Habitation_Shaft_03","M3_Habitation_Shaft_To_Pier","M3_Habitation_Pier_Entrance_01","M3_Habitation_Pier_04",
        "M3_Habitation_Lab_Lobby","M3_Habitation_Endless_Breakroom_01","M3_Habitation_Lab_Ending",
        "Campaign_Interlude_Habitation_To_Abyss_01",
        "M4_Abyss_Transit_01","M4_Abyss_Transit_02","M4_Abyss_Transit_03","M4_Abyss_Transit_05","M4_Abyss_Transit_06",
        "M4_Abyss_Handle_01",
        "M4_Abyss_Garden_01","M4_Abyss_Garden_02","M4_Abyss_Garden_03","M4_Abyss_Garden_04",
        "M4_Abyss_Outro_01",
    };
}

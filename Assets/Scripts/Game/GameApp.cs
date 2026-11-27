using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>一局的结算数据。</summary>
public sealed class RaidResult
{
    public bool Extracted;
    public int Kills;
    public int LootValue;
    public float Seconds;
    public int RoublesBefore;
    public int RoublesAfter;
    public string Summary = string.Empty;
    public List<LootEntry> Loot = new List<LootEntry>();
}

/// <summary>
/// 整个游戏的状态机：主菜单 → 突袭 → 结算 → 回主菜单。
/// 单场景运行，一局结束就把 Raid 根节点整个销毁重建，不需要场景切换。
/// </summary>
public sealed class GameApp : MonoBehaviour
{
    public static GameApp Instance { get; private set; }

    public MetaProfile Profile { get; private set; } = new MetaProfile();
    public PlayerRig Player { get; private set; }
    public Hud Hud { get; private set; }

    public bool InRaid { get; private set; }
    public int Kills { get; private set; }
    public int EnemiesTotal { get; private set; }
    public int EnemiesAlive { get; private set; }
    public int ContainerTotal { get; private set; }

    public float RaidSeconds => InRaid ? Time.time - _raidStart : 0f;
    public int LootValue => Player != null ? Player.Inventory.TotalValue : 0;
    public bool PlayerAlive => Player != null && Player.IsAlive;
    public Vector3 PlayerPosition => Player != null ? Player.transform.position : Vector3.zero;
    public IReadOnlyList<ExtractionZone> Extractions => _extractions;

    private readonly List<ExtractionZone> _extractions = new List<ExtractionZone>();
    private LoadoutKit _currentKit;

    private GameObject _raidRoot;
    private GameObject _controlsRoot;
    private float _raidStart;
    private bool _raidEnding;
    private int _raidCounter;

    private void Awake()
    {
        Instance = this;
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // 先读档再设帧率，否则读档里的设置要等下一局才生效
        Profile = MetaProfile.Load();
        Application.targetFrameRate = Mathf.Clamp(Profile.TargetFps, 30, 165);

        Fx.EnsureLoaded();
    }

    private IEnumerator Start()
    {
        // 等 JSON 数据加载完（GameDataBootstrap 是协程加载）
        var deadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < deadline &&
               (GameDataBootstrap.Instance == null || GameDataBootstrap.Instance.Catalog == null))
        {
            yield return null;
        }

        ShowMenu();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- 菜单 ----------

    public void ShowMenu()
    {
        MenuUi.Close();
        BackpackUi.Close();
        Time.timeScale = 1f;
        DestroyRaid();

        InRaid = false;
        _raidEnding = false;
        Player = null;

        MenuUi.ShowMainMenu(this);
    }

    // ---------- 一局 ----------

    public void StartRaid()
    {
        MenuUi.Close();
        BackpackUi.Close();
        Time.timeScale = 1f;
        Application.targetFrameRate = Mathf.Clamp(Profile.TargetFps, 30, 165);
        DestroyRaid();

        Kills = 0;
        EnemiesTotal = 0;
        EnemiesAlive = 0;
        ContainerTotal = 0;
        _raidEnding = false;

        _raidRoot = new GameObject("Raid");

        var seed = unchecked(System.Environment.TickCount + (++_raidCounter) * 7919);
        var builder = new RaidMapBuilder();
        builder.Build(this, _raidRoot.transform, seed);

        _extractions.AddRange(builder.Extractions);
        ContainerTotal = builder.LootCount;

        // 静态几何合批：200 多个方块否则就是一个方块一个 draw call
        if (builder.StaticRoot != null)
        {
            StaticBatchingUtility.Combine(builder.StaticRoot.gameObject);
        }

        Physics.SyncTransforms();

        // 进场先扣装备钱：这就是"带装备下本"的风险
        _currentKit = Loadouts.Resolve(Profile);
        if (_currentKit.Cost > 0)
        {
            Profile.Roubles = Mathf.Max(0, Profile.Roubles - _currentKit.Cost);
            Profile.Save();
        }

        SpawnPlayer(builder.PlayerSpawn, builder.PlayerYaw);
        SpawnEnemies(builder.EnemySpawns);

        Hud = gameObject.AddComponent<Hud>();
        Hud.Initialize(this);

        InRaid = true;
        _raidStart = Time.time;

        Fx.Play2D(Fx.Beep, 0.35f);
        Debug.Log("[Raid] start seed=" + seed + " enemies=" + EnemiesTotal + " loot=" + ContainerTotal);
    }

    private void SpawnPlayer(Vector3 spawn, float yaw)
    {
        var go = new GameObject("Player");
        go.transform.SetParent(_raidRoot.transform, false);
        go.transform.position = spawn;

        var controller = go.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.slopeLimit = 50f;
        controller.stepOffset = 0.4f;

        var cameraGo = new GameObject("PlayerCamera");
        cameraGo.transform.SetParent(go.transform, false);
        cameraGo.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        cameraGo.tag = "MainCamera";

        var camera = cameraGo.AddComponent<Camera>();
        camera.fieldOfView = 75f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 240f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
        cameraGo.AddComponent<AudioListener>();

        var controls = TouchControls.Create();
        _controlsRoot = controls.gameObject;

        var rig = go.AddComponent<PlayerRig>();
        rig.Message += Toast;
        rig.Initialize(controls, this, spawn, yaw, Profile.Sensitivity, _currentKit);
        Player = rig;

        Physics.SyncTransforms();
    }

    private void SpawnEnemies(List<Vector3> spawns)
    {
        EnemiesTotal = spawns.Count;
        EnemiesAlive = spawns.Count;

        for (var i = 0; i < spawns.Count; i++)
        {
            // 出生点按地面重新对齐：之前直接用 SampleGroundY + 0.15，
            // 敌人一落地就悬在半空，加上阴影看起来像飘着
            var ground = Art.GroundY(spawns[i]);
            var go = new GameObject("Scav" + i);
            go.transform.SetParent(_raidRoot.transform, false);
            go.transform.position = new Vector3(spawns[i].x, ground, spawns[i].z);

            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.38f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;

            var color = Color.Lerp(new Color(0.55f, 0.44f, 0.4f), new Color(0.42f, 0.47f, 0.36f), Random.value);

            var scav = go.AddComponent<EnemyScav>();
            scav.Initialize(this, 100, 12f, color);
        }
    }

    // ---------- 事件 ----------

    public void OnShotFired(Vector3 position)
    {
        for (var i = 0; i < EnemyScav.Active.Count; i++)
        {
            var scav = EnemyScav.Active[i];
            if (scav != null) scav.HearShot(position);
        }
    }

    public void OnEnemyKilled(EnemyScav enemy)
    {
        Kills++;
        EnemiesAlive = Mathf.Max(0, EnemiesAlive - 1);
        Profile.Kills++;
        Toast("击杀 Scav   +1");
    }

    public void OnPlayerDied()
    {
        EndRaid(false, "你在突袭中阵亡，背包里的东西全部丢失");
    }

    public void OnExtractionComplete(ExtractionZone zone)
    {
        EndRaid(true, "成功从「" + zone.ZoneName + "」撤离");
    }

    public void Toast(string text)
    {
        if (Hud != null) Hud.Toast(text);
    }

    public static void ToastStatic(string text)
    {
        if (Instance != null) Instance.Toast(text);
    }

    // ---------- 结算 ----------

    private void EndRaid(bool extracted, string summary)
    {
        if (_raidEnding) return;
        _raidEnding = true;

        var loot = Player != null ? Player.Inventory.Snapshot() : new List<LootEntry>();
        var lootValue = 0;
        for (var i = 0; i < loot.Count; i++) lootValue += loot[i].TotalValue;

        var before = Profile.Roubles;

        Profile.Raids++;

        if (extracted)
        {
            Profile.Extracts++;
            Profile.Roubles += lootValue;
            Profile.TotalLootValue += lootValue;
            if (lootValue > Profile.BestLootValue) Profile.BestLootValue = lootValue;
        }
        else
        {
            Profile.Deaths++;
        }

        Profile.Save();

        var result = new RaidResult
        {
            Extracted = extracted,
            Kills = Kills,
            LootValue = lootValue,
            Seconds = RaidSeconds,
            RoublesBefore = before,
            RoublesAfter = Profile.Roubles,
            Summary = summary,
            Loot = loot
        };

        InRaid = false;
        Debug.Log("[Raid] end extracted=" + extracted + " kills=" + Kills + " loot=" + lootValue);

        StartCoroutine(ShowResultAfter(extracted ? 1.2f : 2.2f, result));
    }

    private IEnumerator ShowResultAfter(float delay, RaidResult result)
    {
        yield return new WaitForSecondsRealtime(delay);

        DestroyRaid();
        MenuUi.ShowResult(this, result);
    }

    // ---------- 清理 ----------

    private void DestroyRaid()
    {
        BackpackUi.Close();

        if (Hud != null)
        {
            Destroy(Hud);
            Hud = null;
        }

        if (_controlsRoot != null)
        {
            _controlsRoot.SetActive(false);
            Destroy(_controlsRoot);
            _controlsRoot = null;
        }

        if (_raidRoot != null)
        {
            _raidRoot.SetActive(false);
            Destroy(_raidRoot);
            _raidRoot = null;
        }

        EnemyScav.Active.Clear();
        LootContainer.Active.Clear();
        _extractions.Clear();
        Player = null;
    }
}
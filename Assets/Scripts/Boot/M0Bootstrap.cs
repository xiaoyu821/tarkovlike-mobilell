using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// M0 运行时白盒训练场：不依赖手工场景资源，构建地面、掩体、靶子、掉落箱、
/// 玩家相机与触屏 UI。CI 生成的空场景里挂上本组件即可运行。
/// </summary>
public sealed class M0Bootstrap : MonoBehaviour
{
    private sealed class Toast
    {
        public string Text;
        public float ExpireAt;
    }

    private readonly List<Toast> _toasts = new List<Toast>();

    private PlayerRig _player;
    private Camera _playerCamera;
    private Transform _ground;
    private string _diag = "";
    private float _nextDiag;
    private GUIStyle _hudStyle;
    private GUIStyle _toastStyle;
    private float _startTime;

    private void Awake()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        _startTime = Time.time;

        // 关键：工程里 ProjectSettings/DynamicsManager 的 m_AutoSyncTransforms = 0。
        // 这种情况下用 transform.position 摆好物体后，物理世界里的碰撞体还停在旧位置，
        // 运行时生成的墙/地面就会“看起来在那、实际不在那”，CharacterController 直接穿地。
        Physics.autoSyncTransforms = true;

        BuildWorld();
        Physics.SyncTransforms();
    }

    public void ShowToast(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _toasts.Add(new Toast { Text = text, ExpireAt = Time.time + 4f });
        if (_toasts.Count > 3)
        {
            _toasts.RemoveAt(0);
        }
    }

    private void BuildWorld()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.35f, 0.37f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.fogColor = new Color(0.12f, 0.13f, 0.15f);

        var root = new GameObject("M0World").transform;

        var ground = CreateMaterial(new Color(0.2f, 0.21f, 0.22f));
        var wall = CreateMaterial(new Color(0.3f, 0.3f, 0.31f));
        var cover = CreateMaterial(new Color(0.36f, 0.34f, 0.3f));
        var crateMaterial = CreateMaterial(new Color(0.5f, 0.38f, 0.2f));
        var dummyMaterial = CreateMaterial(new Color(0.65f, 0.18f, 0.18f));

        // 地面用厚 Box，不用 Plane：
        // 1) Plane 是零厚度、单面的 MeshCollider，首帧 dt 抖动时 CharacterController 容易直接穿过去；
        // 2) 从下方看 Plane 完全不可见，掉下去就是一片纯黑，根本看不出发生了什么。
        var groundGo = CreateCube("Ground", new Vector3(0f, -1f, 0f), new Vector3(90f, 2f, 90f), ground, root);
        _ground = groundGo.transform;

        CreateCube("WallNorth", new Vector3(0f, 3f, 45f), new Vector3(90f, 6f, 0.6f), wall, root);
        CreateCube("WallSouth", new Vector3(0f, 3f, -45f), new Vector3(90f, 6f, 0.6f), wall, root);
        CreateCube("WallEast", new Vector3(45f, 3f, 0f), new Vector3(0.6f, 6f, 90f), wall, root);
        CreateCube("WallWest", new Vector3(-45f, 3f, 0f), new Vector3(0.6f, 6f, 90f), wall, root);

        CreateCube("CoverA", new Vector3(-6f, 1f, 2f), new Vector3(3f, 2f, 1.2f), cover, root);
        CreateCube("CoverB", new Vector3(6f, 1f, 8f), new Vector3(1.2f, 2f, 4f), cover, root);
        CreateCube("CoverC", new Vector3(-10f, 1f, 16f), new Vector3(4f, 2f, 1.2f), cover, root);
        CreateCube("CoverD", new Vector3(12f, 1f, 20f), new Vector3(2f, 2f, 2f), cover, root);

        CreateDummy(new Vector3(0f, 1.1f, 12f), dummyMaterial, root);
        CreateDummy(new Vector3(8f, 1.1f, 18f), dummyMaterial, root);
        CreateDummy(new Vector3(-7f, 1.1f, 24f), dummyMaterial, root);
        CreateDummy(new Vector3(3f, 1.1f, 32f), dummyMaterial, root);

        CreateCrate(new Vector3(4f, 0.6f, 5f), crateMaterial, "loot_jacket", root);
        CreateCrate(new Vector3(-5f, 0.6f, 10f), crateMaterial, "loot_jacket", root);
        CreateCrate(new Vector3(9f, 0.6f, 14f), crateMaterial, "loot_weapon_crate", root);
        CreateCrate(new Vector3(-3f, 0.6f, 20f), crateMaterial, null, root);

        CreateLight(root);
        CreatePlayer(root);
    }

    private void CreatePlayer(Transform root)
    {
        var playerGo = new GameObject("Player");
        playerGo.transform.SetParent(root, false);

        // 出生点不要硬编码 y：先向下打射线找地面。
        // 之前写死 0.05，一旦地面没生成或位置不对，开场就是自由落体。
        var spawn = new Vector3(0f, 2f, -6f);
        Physics.SyncTransforms();
        if (Physics.Raycast(spawn + Vector3.up * 5f, Vector3.down, out var groundHit, 30f, ~0, QueryTriggerInteraction.Ignore))
        {
            spawn.y = groundHit.point.y + 0.1f;
        }
        else
        {
            Debug.LogWarning("[M0] 出生点下方没有地面，玩家会掉落：检查 Ground 是否生成");
        }
        playerGo.transform.position = spawn;
        Physics.SyncTransforms(); // 让 CC 的胶囊体真的落在这个坐标上

        var controller = playerGo.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.slopeLimit = 50f;
        controller.stepOffset = 0.4f;

        var cameraGo = new GameObject("PlayerCamera");
        cameraGo.transform.SetParent(playerGo.transform, false);
        cameraGo.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        cameraGo.tag = "MainCamera";

        var cameraComponent = cameraGo.AddComponent<Camera>();
        cameraComponent.fieldOfView = 75f;
        cameraComponent.nearClipPlane = 0.05f;
        cameraComponent.farClipPlane = 200f;
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
        cameraGo.AddComponent<AudioListener>();
        _playerCamera = cameraComponent;

        _player = playerGo.AddComponent<PlayerRig>();
        _player.Message += ShowToast;

        var controls = TouchControls.Create();
        _player.Initialize(controls);
    }

    private static void CreateLight(Transform root)
    {
        var lightGo = new GameObject("Sun");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.95f, 0.86f);
        light.shadows = LightShadows.Soft;
    }

    private static void CreateDummy(Vector3 position, Material material, Transform root)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Target";
        go.transform.SetParent(root, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
        go.GetComponent<Renderer>().sharedMaterial = material;

        EnsureCollider<CapsuleCollider>(go);

        var dummy = go.AddComponent<TargetDummy>();
        dummy.Initialize(100);
    }

    private static void CreateCrate(Vector3 position, Material material, string tableId, Transform root)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "LootCrate";
        go.transform.SetParent(root, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        go.GetComponent<Renderer>().sharedMaterial = material;

        EnsureCollider<BoxCollider>(go);
        go.AddComponent<LootCrate>().Initialize(tableId);
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, Transform root)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;

        // CreatePrimitive 的 Collider 是引擎内部 AddComponent 的，
        // IL2CPP 链接器可能把 BoxCollider 裁掉（实测：HUD 显示 col=none，然后角色穿地）。
        // 这里显式再要一次，托管代码里出现类型名，链接器就不会删。
        EnsureCollider<BoxCollider>(go);
        return go;
    }

    private static T EnsureCollider<T>(GameObject go) where T : Collider
    {
        var collider = go.GetComponent<T>();
        if (collider == null)
        {
            collider = go.AddComponent<T>();
        }
        return collider;
    }

    private static Material CreateMaterial(Color color)
    {
        var shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return new Material(shader) { color = color };
    }

    private void Update()
    {
        if (Time.time < _nextDiag) return;
        _nextDiag = Time.time + 0.5f;
        _diag = BuildDiagnostics();
    }

    private string BuildDiagnostics()
    {
        if (_player == null) return "player=null";

        var p = _player.transform.position;
        var c = _playerCamera != null ? _playerCamera.transform.position : Vector3.zero;
        var ray = "ray=none";
        if (Physics.Raycast(c + Vector3.up * 0.2f, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            ray = string.Format("ray={0}@{1:0.00}m", hit.collider.name, hit.distance);
        }

        var groundInfo = "ground=null";
        if (_ground != null)
        {
            var col = _ground.GetComponent<Collider>();
            groundInfo = string.Format("ground y={0:0.0} col={1} trig={2} layer={3}",
                _ground.position.y,
                col != null ? col.GetType().Name : "none",
                col != null && col.isTrigger ? "Y" : "N",
                _ground.gameObject.layer);
        }

        return string.Format("P=({0:0.0},{1:0.0},{2:0.0}) C=({3:0.0},{4:0.0},{5:0.0})\n" +
                             "grounded={6} dt={7:0.000} fps={8:0} cams={9} autoSync={10}\n" +
                             "{11}\n{12}",
            p.x, p.y, p.z, c.x, c.y, c.z,
            _player.Grounded ? "Y" : "N", Time.deltaTime,
            1f / Mathf.Max(0.0001f, Time.smoothDeltaTime),
            Camera.allCamerasCount, Physics.autoSyncTransforms,
            groundInfo, ray);
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawCrosshair();
        DrawWeaponFeedback();
        DrawHud();
        DrawToasts();
    }

    private void EnsureStyles()
    {
        if (_hudStyle == null)
        {
            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(24, Screen.height / 32),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _hudStyle.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
        }

        if (_toastStyle == null)
        {
            _toastStyle = new GUIStyle(_hudStyle)
            {
                fontSize = Mathf.Max(22, Screen.height / 40),
                alignment = TextAnchor.MiddleCenter
            };
            _toastStyle.normal.textColor = new Color(1f, 0.9f, 0.6f, 0.95f);
        }
    }

    private void DrawWeaponFeedback()
    {
        if (_player == null) return;

        // 枪口闪光：屏幕下半部一团暖色，0.045s
        if (_player.MuzzleFlashActive)
        {
            var w = Screen.width * 0.22f;
            var h = Screen.height * 0.16f;
            GUI.color = new Color(1f, 0.85f, 0.45f, 0.35f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - h * 1.1f, w, h),
                Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.95f, 0.75f, 0.5f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - w * 0.2f, Screen.height - h * 0.85f, w * 0.4f, h * 0.45f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // 命中标记：中心一个 X
        if (_player.HitMarkerActive)
        {
            var cx = Screen.width * 0.5f;
            var cy = Screen.height * 0.5f;
            var len = 14f;
            var thickness = 2f;
            GUI.color = new Color(1f, 0.3f, 0.25f, 0.95f);

            // 用几段小矩形拼一个 X（避免依赖贴图）
            for (var i = 0; i < 8; i++)
            {
                var t = i / 8f;
                var x = cx - len + t * len * 2f;
                GUI.DrawTexture(new Rect(x, cy - len + t * len * 2f, thickness, thickness), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x, cy + len - t * len * 2f, thickness, thickness), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }

    private static void DrawCrosshair()
    {
        var width = Screen.width;
        var height = Screen.height;
        var centerX = width * 0.5f;
        var centerY = height * 0.5f;

        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(new Rect(centerX - 1f, centerY - 16f, 2f, 9f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - 1f, centerY + 7f, 2f, 9f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - 16f, centerY - 1f, 9f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX + 7f, centerY - 1f, 9f, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawHud()
    {
        if (_player == null)
        {
            return;
        }

        var ammoText = _player.Reloading
            ? "换弹中…"
            : _player.Magazine + " / " + _player.MagazineSize;

        GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height - 120f, 440f, 56f), ammoText, _hudStyle);
        GUI.Label(new Rect(Screen.width - 520f, 16f, 500f, 48f),
            "M0 训练场 · " + _player.WeaponName, _hudStyle);

        // 诊断块：掉下去的时候这几行能直接告诉我们发生了什么
        GUI.Label(new Rect(16f, Screen.height - 300f, 900f, 150f), _diag, _toastStyle);

        if (Time.time - _startTime < 15f)
        {
            GUI.Label(new Rect(Screen.width * 0.5f - 480f, 90f, 960f, 52f),
                "左半屏拖动移动 · 右半屏滑动视角 · 右下角开火 / 拿", _toastStyle);
        }
    }

    private void DrawToasts()
    {
        var active = 0;
        for (var i = _toasts.Count - 1; i >= 0; i--)
        {
            if (Time.time >= _toasts[i].ExpireAt)
            {
                _toasts.RemoveAt(i);
            }
        }

        for (var i = _toasts.Count - 1; i >= 0; i--)
        {
            GUI.Label(new Rect(Screen.width * 0.2f, Screen.height - 210f - active * 56f,
                Screen.width * 0.6f, 52f), _toasts[i].Text, _toastStyle);
            active++;
        }
    }
}

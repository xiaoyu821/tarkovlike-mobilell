using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 程序化白盒地图：外围高墙 + 若干带门洞的建筑 + 户外掩体 + 可搜刮容器 + 两个撤离点。
/// 全部用 Cube 拼，不依赖任何手工场景资源，CI 里跑起来是什么样就是什么样。
/// </summary>
public sealed class RaidMapBuilder
{
    public const float MapHalf = 46f;
    private const float WallHeight = 3.4f;
    private const float WallThickness = 0.45f;

    public Vector3 PlayerSpawn { get; private set; }
    public float PlayerYaw { get; private set; }
    public int LootCount { get; private set; }
    public Transform StaticRoot { get; private set; }

    public readonly List<Vector3> EnemySpawns = new List<Vector3>();
    public readonly List<ExtractionZone> Extractions = new List<ExtractionZone>();

    private readonly List<Rect> _buildings = new List<Rect>();
    private readonly List<Rect> _reserved = new List<Rect>();

    // 出生点固定在 (0, -MapHalf + 6)，物资和掩体都要给它留出一圈空地
    private static readonly Vector2 SpawnXZ = new Vector2(0f, -(MapHalf - 6f));

    private Transform _root;
    private GameApp _app;
    private System.Random _rng;

    private Material _groundMaterial;
    private Material _wallMaterial;
    private Material _coverMaterial;
    private Material _crateMaterial;
    private Material _extractMaterial;

    public void Build(GameApp app, Transform root, int seed)
    {
        _app = app;
        _root = root;
        _rng = new System.Random(seed);

        _buildings.Clear();
        _reserved.Clear();
        EnemySpawns.Clear();
        Extractions.Clear();
        LootCount = 0;

        var staticGo = new GameObject("Static");
        staticGo.transform.SetParent(root, false);
        StaticRoot = staticGo.transform;

        CreateMaterials();

        BuildGroundAndWalls();
        PlaceExtractions();
        BuildBuildings();
        ScatterCrates();
        PlaceLoot();
        PlacePlayerSpawn();
        PlaceEnemySpawns();
    }

    // ---------- 基础结构 ----------

    private void CreateMaterials()
    {
        // CC0 贴图材质（Resources/Textures），拿不到贴图时自动退化成纯色
        _groundMaterial = Art.Ground;
        _wallMaterial = Art.Wall;
        _coverMaterial = Art.MetalDark;
        _crateMaterial = Art.Wood;
        _extractMaterial = Art.Metal;
    }

    private void BuildGroundAndWalls()
    {
        var size = MapHalf * 2f + 16f;
        AddBox(new Vector3(0f, -1f, 0f), new Vector3(size, 2f, size), _groundMaterial, "Ground");

        AddBox(new Vector3(0f, WallHeight * 0.5f, MapHalf), new Vector3(size, WallHeight, WallThickness), _wallMaterial, "WallN");
        AddBox(new Vector3(0f, WallHeight * 0.5f, -MapHalf), new Vector3(size, WallHeight, WallThickness), _wallMaterial, "WallS");
        AddBox(new Vector3(MapHalf, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, size), _wallMaterial, "WallE");
        AddBox(new Vector3(-MapHalf, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, size), _wallMaterial, "WallW");
    }

    private void PlaceExtractions()
    {
        AddExtraction("北侧撤离点", new Vector3(30f, 0f, 34f));
        AddExtraction("西侧撤离点", new Vector3(-32f, 0f, -28f));
    }

    private void AddExtraction(string name, Vector3 center)
    {
        _reserved.Add(new Rect(center.x - 9f, center.z - 9f, 18f, 18f));

        var go = new GameObject("Extraction_" + name);
        go.transform.SetParent(_root, false);
        go.transform.position = center;

        var zone = go.AddComponent<ExtractionZone>();
        zone.Initialize(_app, name, center, 4.5f);
        Extractions.Add(zone);

        // 地面上的绿盘 + 四根柱子，远远就能认出来
        AddBox(center + new Vector3(0f, 0.04f, 0f), new Vector3(9f, 0.08f, 9f), Art.Beacon, "ExtractPad");
        Art.BuildBeacon(StaticRoot, center);
        var corners = new[]
        {
            new Vector3(-4.2f, 0f, -4.2f), new Vector3(4.2f, 0f, -4.2f),
            new Vector3(-4.2f, 0f, 4.2f), new Vector3(4.2f, 0f, 4.2f)
        };

        for (var i = 0; i < corners.Length; i++)
        {
            AddBox(center + corners[i] + new Vector3(0f, 1.3f, 0f),
                new Vector3(0.28f, 2.6f, 0.28f), _extractMaterial, "ExtractPost");
        }
    }

    private void BuildBuildings()
    {
        var target = 14;
        var placed = 0;

        for (var attempt = 0; attempt < 400 && placed < target; attempt++)
        {
            var width = _rng.Next(7, 16);
            var depth = _rng.Next(7, 16);
            var x = RandomRange(-MapHalf + 9f, MapHalf - 9f);
            var z = RandomRange(-MapHalf + 9f, MapHalf - 9f);
            var rect = new Rect(x - width * 0.5f, z - depth * 0.5f, width, depth);

            // 出生点周围 18 米留空，别一进场就贴脸
            if (rect.Overlaps(new Rect(-18f, -MapHalf, 36f, 26f))) continue;
            if (OverlapsAny(rect, _buildings, 4.5f)) continue;
            if (OverlapsAny(rect, _reserved, 3f)) continue;

            _buildings.Add(rect);
            BuildBuildingShell(rect);
            placed++;
        }
    }

    private void BuildBuildingShell(Rect rect)
    {
        var doorSide = _rng.Next(0, 4);

        for (var side = 0; side < 4; side++)
        {
            var hasDoor = side == doorSide;

            if (side == 0 || side == 1)
            {
                var z = side == 0 ? rect.yMax : rect.yMin;
                BuildHorizontalWall(rect.xMin, rect.xMax, z, hasDoor);
            }
            else
            {
                var x = side == 2 ? rect.xMax : rect.xMin;
                BuildVerticalWall(rect.yMin, rect.yMax, x, hasDoor);
            }
        }

        // 室内掩体
        var coverCount = _rng.Next(1, 3);
        for (var i = 0; i < coverCount; i++)
        {
            var cx = RandomRange(rect.xMin + 1.8f, rect.xMax - 1.8f);
            var cz = RandomRange(rect.yMin + 1.8f, rect.yMax - 1.8f);
            AddBox(new Vector3(cx, 0.55f, cz),
                new Vector3(RandomRange(1.2f, 2.4f), 1.1f, RandomRange(1.2f, 2.4f)),
                _coverMaterial, "Cover");
        }
    }

    private void BuildHorizontalWall(float x0, float x1, float z, bool door)
    {
        if (!door || x1 - x0 < 6f)
        {
            AddBox(new Vector3((x0 + x1) * 0.5f, WallHeight * 0.5f, z),
                new Vector3(x1 - x0, WallHeight, WallThickness), _wallMaterial, "Wall");
            return;
        }

        var gap = RandomRange(x0 + 2.2f, x1 - 2.2f);
        const float halfGap = 1.3f;

        var leftLength = gap - halfGap - x0;
        var rightLength = x1 - (gap + halfGap);

        if (leftLength > 0.5f)
        {
            AddBox(new Vector3(x0 + leftLength * 0.5f, WallHeight * 0.5f, z),
                new Vector3(leftLength, WallHeight, WallThickness), _wallMaterial, "Wall");
        }

        if (rightLength > 0.5f)
        {
            AddBox(new Vector3(gap + halfGap + rightLength * 0.5f, WallHeight * 0.5f, z),
                new Vector3(rightLength, WallHeight, WallThickness), _wallMaterial, "Wall");
        }
    }

    private void BuildVerticalWall(float z0, float z1, float x, bool door)
    {
        if (!door || z1 - z0 < 6f)
        {
            AddBox(new Vector3(x, WallHeight * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(WallThickness, WallHeight, z1 - z0), _wallMaterial, "Wall");
            return;
        }

        var gap = RandomRange(z0 + 2.2f, z1 - 2.2f);
        const float halfGap = 1.3f;

        var belowLength = gap - halfGap - z0;
        var aboveLength = z1 - (gap + halfGap);

        if (belowLength > 0.5f)
        {
            AddBox(new Vector3(x, WallHeight * 0.5f, z0 + belowLength * 0.5f),
                new Vector3(WallThickness, WallHeight, belowLength), _wallMaterial, "Wall");
        }

        if (aboveLength > 0.5f)
        {
            AddBox(new Vector3(x, WallHeight * 0.5f, gap + halfGap + aboveLength * 0.5f),
                new Vector3(WallThickness, WallHeight, aboveLength), _wallMaterial, "Wall");
        }
    }

    private void ScatterCrates()
    {
        for (var i = 0; i < 14; i++)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var x = RandomRange(-MapHalf + 6f, MapHalf - 6f);
                var z = RandomRange(-MapHalf + 6f, MapHalf - 6f);
                var size = RandomRange(1.2f, 2.2f);
                var rect = new Rect(x - size, z - size, size * 2f, size * 2f);

                if (OverlapsAny(rect, _buildings, 1f)) continue;
                if (OverlapsAny(rect, _reserved, 1f)) continue;
                if (x * x + z * z < 400f) continue;
                if (Vector2.Distance(new Vector2(x, z), SpawnXZ) < 13f) continue;

                AddBox(new Vector3(x, size * 0.5f, z), new Vector3(size, size, size), _crateMaterial, "Crate");
                break;
            }
        }
    }

    private void PlaceLoot()
    {
        // 建筑里放 9 个外套箱（屋里是主要搜刮点）
        for (var i = 0; i < 9 && _buildings.Count > 0; i++)
        {
            var rect = _buildings[_rng.Next(0, _buildings.Count)];
            var x = RandomRange(rect.xMin + 1.6f, rect.xMax - 1.6f);
            var z = RandomRange(rect.yMin + 1.6f, rect.yMax - 1.6f);
            LootContainer.Spawn(_root, new Vector3(x, 0.45f, z), "loot_jacket", "外套箱",
                new Color(0.55f, 0.45f, 0.26f), new Vector3(0.9f, 0.9f, 0.9f));
            LootCount++;
        }

        // 户外放 5 个武器箱
        for (var i = 0; i < 5; i++)
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var x = RandomRange(-MapHalf + 6f, MapHalf - 6f);
                var z = RandomRange(-MapHalf + 6f, MapHalf - 6f);
                var rect = new Rect(x - 2f, z - 2f, 4f, 4f);

                if (OverlapsAny(rect, _buildings, 1f)) continue;
                if (OverlapsAny(rect, _reserved, 1f)) continue;
                if (x * x + z * z < 100f) continue;
                if (Vector2.Distance(new Vector2(x, z), SpawnXZ) < 13f) continue;

                LootContainer.Spawn(_root, new Vector3(x, 0.5f, z), "loot_weapon_crate", "武器箱",
                    new Color(0.42f, 0.32f, 0.16f), new Vector3(1.1f, 1f, 1.1f));
                LootCount++;
                break;
            }
        }

        // 再补 6 个外套箱
        for (var i = 0; i < 6; i++)
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var x = RandomRange(-MapHalf + 6f, MapHalf - 6f);
                var z = RandomRange(-MapHalf + 6f, MapHalf - 6f);
                var rect = new Rect(x - 2f, z - 2f, 4f, 4f);

                if (OverlapsAny(rect, _buildings, 1f)) continue;
                if (OverlapsAny(rect, _reserved, 1f)) continue;
                if (x * x + z * z < 64f) continue;
                if (Vector2.Distance(new Vector2(x, z), SpawnXZ) < 13f) continue;

                LootContainer.Spawn(_root, new Vector3(x, 0.45f, z), "loot_jacket", "外套箱",
                    new Color(0.55f, 0.45f, 0.26f), new Vector3(0.9f, 0.9f, 0.9f));
                LootCount++;
                break;
            }
        }
    }

    private void PlacePlayerSpawn()
    {
        var spawn = new Vector3(0f, 0.2f, -MapHalf + 6f);
        spawn.y = SampleGroundY(spawn.x, spawn.z) + 0.03f;
        PlayerSpawn = spawn;
        PlayerYaw = 0f;
    }

    private void PlaceEnemySpawns()
    {
        var target = 10;

        for (var attempt = 0; attempt < 500 && EnemySpawns.Count < target; attempt++)
        {
            var x = RandomRange(-MapHalf + 8f, MapHalf - 8f);
            var z = RandomRange(-MapHalf + 8f, MapHalf - 8f);

            if (Vector3.Distance(new Vector3(x, 0f, z), new Vector3(PlayerSpawn.x, 0f, PlayerSpawn.z)) < 26f) continue;

            var rect = new Rect(x - 2f, z - 2f, 4f, 4f);
            if (OverlapsAny(rect, _buildings, 1.5f)) continue;
            if (OverlapsAny(rect, _reserved, 1f)) continue;

            var point = new Vector3(x, SampleGroundY(x, z) + 0.05f, z);

            // 别把敌人生成在箱子里/墙缝里（否则一开局就卡住打不到）
            if (Physics.CheckCapsule(point + Vector3.up * 0.45f, point + Vector3.up * 1.35f, 0.4f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            EnemySpawns.Add(point);
        }

        for (var i = 0; i < EnemySpawns.Count; i++)
        {
            Debug.Log("[Map] scav spawn " + i + " at " + EnemySpawns[i]);
        }
    }

    // ---------- 工具 ----------

    private float SampleGroundY(float x, float z)
    {
        var origin = new Vector3(x, 4f, z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return 0f;
    }

    private GameObject AddBox(Vector3 center, Vector3 size, Material material, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(StaticRoot, false);
        go.transform.position = center;
        go.transform.localScale = size;

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;

        // IL2CPP 会把引擎内部 AddComponent 的碰撞体裁掉，这里显式引用一次类型
        if (go.GetComponent<BoxCollider>() == null)
        {
            go.AddComponent<BoxCollider>();
        }

        return go;
    }

    private float RandomRange(float min, float max)
    {
        return min + (float)_rng.NextDouble() * (max - min);
    }

    private static bool OverlapsAny(Rect rect, List<Rect> others, float padding)
    {
        var padded = new Rect(rect.xMin - padding, rect.yMin - padding,
            rect.width + padding * 2f, rect.height + padding * 2f);

        for (var i = 0; i < others.Count; i++)
        {
            if (padded.Overlaps(others[i])) return true;
        }

        return false;
    }

    private static Material Solid(Color color)
    {
        var shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        return new Material(shader) { color = color };
    }
}
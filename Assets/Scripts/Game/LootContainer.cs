using System.Collections.Generic;
using Game.Data;
using UnityEngine;

/// <summary>
/// 可搜刮容器：站在旁边点"搜"，1 秒读完，掉落表结果直接进背包。
/// 搜过的容器变暗且永久为空，所以一局里的物资是有限的。
/// </summary>
public sealed class LootContainer : MonoBehaviour
{
    public const float SearchSeconds = 1.0f;
    public const float ReachDistance = 3.2f;

    public static readonly List<LootContainer> Active = new List<LootContainer>();

    private static int _rollSeed;

    private string _tableId;
    private PlayerRig _searcher;
    private float _searchStart;
    private bool _searching;
    private Renderer[] _renderers;

    public static LootContainer CurrentSearch { get; private set; }

    public string Label { get; private set; } = "容器";
    public bool IsEmpty { get; private set; }
    public bool Searching => _searching;
    public float Progress01 => _searching
        ? Mathf.Clamp01((Time.time - _searchStart) / SearchSeconds)
        : 0f;

    public static LootContainer Spawn(Transform parent, Vector3 position, string tableId, string label,
        Color color, Vector3 size)
    {
        var go = new GameObject("Loot_" + label);
        go.transform.SetParent(parent, false);
        // 不管调用方传什么 y，一律按地面重新贴一次，避免箱子/尸体悬空
        var ground = Art.GroundY(position);
        go.transform.position = new Vector3(position.x, ground + size.y * 0.5f, position.z);
        go.transform.localScale = size;

        // IL2CPP 会裁掉引擎内部 AddComponent 的碰撞体，这里显式要一次
        var collider = go.AddComponent<BoxCollider>();
        collider.size = Vector3.one;

        // 木箱 / 军械箱模型（边长 1，靠 localScale 缩放）
        Art.BuildCrate(go.transform, tableId == "loot_weapon_crate");

        var container = go.AddComponent<LootContainer>();
        container._tableId = tableId;
        container.Label = label;
        container._renderers = go.GetComponentsInChildren<Renderer>();

        return container;
    }

    private void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
        if (CurrentSearch == this) CurrentSearch = null;
    }

    private void Update()
    {
        if (!IsEmpty)
        {
            transform.Rotate(0f, 22f * Time.deltaTime, 0f, Space.World);
        }

        if (!_searching) return;

        if (_searcher == null || !_searcher.IsAlive)
        {
            CancelSearch();
            return;
        }

        var distance = Vector3.Distance(_searcher.transform.position, transform.position);
        if (distance > ReachDistance + 0.8f)
        {
            CancelSearch();
            GameApp.ToastStatic("离得太远，搜索中断");
            return;
        }

        if (Time.time - _searchStart >= SearchSeconds)
        {
            CompleteSearch();
        }
    }

    public bool BeginSearch(PlayerRig player)
    {
        if (_searching || IsEmpty || player == null || !player.IsAlive) return false;
        if (Vector3.Distance(player.transform.position, transform.position) > ReachDistance) return false;

        _searching = true;
        _searchStart = Time.time;
        _searcher = player;
        CurrentSearch = this;
        Fx.PlayAt(transform.position, Fx.Search, 0.6f);
        return true;
    }

    public void CancelSearch()
    {
        _searching = false;
        _searcher = null;
        if (CurrentSearch == this) CurrentSearch = null;
    }

    private void CompleteSearch()
    {
        var player = _searcher;
        _searching = false;
        _searcher = null;
        if (CurrentSearch == this) CurrentSearch = null;

        if (player == null) return;

        IsEmpty = true;

        // 搜过的箱子整体变暗（材质实例化一份，别影响别的箱子）
        if (_renderers != null)
        {
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null) continue;
                var material = renderer.material;
                material.color = material.color * 0.4f;
            }
        }

        var catalog = GameDataBootstrap.Instance != null ? GameDataBootstrap.Instance.Catalog : null;
        if (catalog == null)
        {
            GameApp.ToastStatic("数据未就绪，什么都没搜到");
            return;
        }

        LootTableData table = null;
        if (!string.IsNullOrEmpty(_tableId))
        {
            catalog.LootTables.TryGetValue(_tableId, out table);
        }

        if (table == null && catalog.LootTables.Count > 0)
        {
            foreach (var candidate in catalog.LootTables.Values)
            {
                table = candidate;
                break;
            }
        }

        if (table == null)
        {
            GameApp.ToastStatic("这张地图没有配置掉落表");
            return;
        }

        _rollSeed = unchecked(_rollSeed * 7919 + System.Environment.TickCount);
        var results = LootRoller.Roll(table, _rollSeed);

        var summary = "搜到: ";
        var added = 0;
        var lost = 0;

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var displayName = result.ItemId;
            var value = 0;

            if (catalog.Items.TryGetValue(result.ItemId, out var item))
            {
                displayName = item.DisplayName;
                value = item.Value;
            }

            if (player.Inventory.TryAdd(result.ItemId, displayName, result.Quantity, value))
            {
                if (added > 0) summary += "，";
                summary += displayName + " x" + result.Quantity;
                added++;
            }
            else
            {
                lost += result.Quantity;
            }
        }

        if (added == 0)
        {
            GameApp.ToastStatic(Label + ": 背包满了");
            return;
        }

        if (lost > 0) summary += "（背包满，" + lost + " 件没拿到）";
        GameApp.ToastStatic(summary);
    }

    /// <summary>地图生成和敌人尸体共用同一套外观创建逻辑。</summary>
    private static Material Solid(Color color)
    {
        var shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var material = new Material(shader) { color = color };

        // 加一点自发光：白盒地图没有烘焙光照，纯靠反射光根本看不见箱子在哪
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 0.45f);

        return material;
    }
}
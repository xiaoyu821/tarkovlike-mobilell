using System.Text;
using Game.Data;
using UnityEngine;

/// <summary>
/// M0 可拾取箱子：调用 LootRoller 按掉落表生成结果，返回给 HUD 显示。
/// 表格 ID 可指定；数据未加载或表不存在时回退到任意一张表。
/// 拾取后有冷却，箱子会变暗，避免站在箱子前面无限刷物资。
/// </summary>
public sealed class LootCrate : MonoBehaviour
{
    private const float CooldownSeconds = 6f;

    private static int _rollCounter;

    private string _tableId;
    private float _cooldownUntil;
    private Renderer _renderer;
    private Material _material;
    private Color _baseColor = Color.white;

    public bool IsOnCooldown => Time.time < _cooldownUntil;

    public void Initialize(string tableId)
    {
        _tableId = tableId;

        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            _material = _renderer.material;
            _baseColor = _material.color;
        }
    }

    private void Update()
    {
        // 冷却中慢慢自转，冷却结束转快一点，远看就能知道能不能搜
        var speed = IsOnCooldown ? 8f : 28f;
        transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.World);
    }

    public string Interact()
    {
        if (IsOnCooldown)
        {
            return "这个箱子刚搜过，还要 " + Mathf.CeilToInt(_cooldownUntil - Time.time) + " 秒";
        }

        var catalog = GameDataBootstrap.Instance != null ? GameDataBootstrap.Instance.Catalog : null;
        if (catalog == null)
        {
            return "数据还没加载完，稍后再试";
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
            return "这张地图没有配置掉落表";
        }

        // 之前只用 TickCount 当种子，同一毫秒内连按两次会掉出一模一样的东西
        _rollCounter++;
        var seed = unchecked(System.Environment.TickCount * 31 + _rollCounter * 7919);

        var results = LootRoller.Roll(table, seed);
        var builder = new StringBuilder("拾取: ");

        if (results.Count == 0)
        {
            builder.Append("什么都没搜到");
        }

        for (var i = 0; i < results.Count; i++)
        {
            if (i > 0)
            {
                builder.Append("，");
            }

            var result = results[i];
            var displayName = result.ItemId;
            if (catalog.Items.TryGetValue(result.ItemId, out var item))
            {
                displayName = item.DisplayName;
            }

            builder.Append(displayName).Append(" x").Append(result.Quantity);
        }

        _cooldownUntil = Time.time + CooldownSeconds;
        if (_material != null)
        {
            _material.color = _baseColor * 0.45f;
        }

        return builder.ToString();
    }

    private void LateUpdate()
    {
        if (_material != null && !IsOnCooldown && _material.color != _baseColor)
        {
            _material.color = _baseColor;
        }
    }
}
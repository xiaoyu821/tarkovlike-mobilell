using System.Text;
using Game.Data;
using UnityEngine;

/// <summary>
/// M0 可拾取箱子：调用 LootRoller 按掉落表生成结果，返回给 HUD 显示。
/// 表格 ID 可指定；数据未加载或表不存在时回退到任意一张表。
/// </summary>
public sealed class LootCrate : MonoBehaviour
{
    private string _tableId;

    public void Initialize(string tableId)
    {
        _tableId = tableId;
    }

    private void Update()
    {
        transform.Rotate(0f, 25f * Time.deltaTime, 0f, Space.World);
    }

    public string Interact()
    {
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

        var results = LootRoller.Roll(table, System.Environment.TickCount);
        var builder = new StringBuilder("拾取: ");

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

        return builder.ToString();
    }
}

using System.Collections.Generic;

/// <summary>背包里的一条战利品。</summary>
public sealed class LootEntry
{
    public string ItemId;
    public string DisplayName;
    public int Count;
    public int UnitValue;

    public int TotalValue => UnitValue * Count;

    public LootEntry(string itemId, string displayName, int count, int unitValue)
    {
        ItemId = itemId;
        DisplayName = displayName;
        Count = count;
        UnitValue = unitValue;
    }
}

/// <summary>
/// 一局内的背包：按条目去重合并，格子数上限控制容量。
/// 撤离成功才结算成卢布，阵亡全丢 —— 这是塔科夫式循环的核心风险。
/// </summary>
public sealed class PlayerInventory
{
    public const int Capacity = 18;

    private readonly List<LootEntry> _items = new List<LootEntry>();

    public IReadOnlyList<LootEntry> Items => _items;
    public int Count => _items.Count;
    public bool IsFull => _items.Count >= Capacity;
    public int TotalValue { get; private set; }

    public bool Contains(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].ItemId == itemId) return true;
        }
        return false;
    }

    /// <summary>加物品。背包满且是新品时返回 false。</summary>
    public bool TryAdd(string itemId, string displayName, int count, int unitValue)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].ItemId != itemId) continue;

            _items[i].Count += count;
            TotalValue += unitValue * count;
            return true;
        }

        if (IsFull) return false;

        var entry = new LootEntry(itemId, string.IsNullOrEmpty(displayName) ? itemId : displayName, count, unitValue);
        _items.Add(entry);
        TotalValue += entry.TotalValue;
        return true;
    }

    public void Clear()
    {
        _items.Clear();
        TotalValue = 0;
    }

    /// <summary>结果界面用的快照，撤离后背包会随场景销毁。</summary>
    public List<LootEntry> Snapshot()
    {
        var copy = new List<LootEntry>(_items.Count);
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            copy.Add(new LootEntry(item.ItemId, item.DisplayName, item.Count, item.UnitValue));
        }
        return copy;
    }
}
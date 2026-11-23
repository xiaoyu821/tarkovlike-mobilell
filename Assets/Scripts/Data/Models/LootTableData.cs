using System;
using System.Collections.Generic;

namespace Game.Data
{
    [Serializable]
    public sealed class IntRange
    {
        public int Min { get; set; }
        public int Max { get; set; }
    }

    [Serializable]
    public sealed class LootTableEntry
    {
        public string ItemId { get; set; }
        public int Weight { get; set; }
        public IntRange Qty { get; set; } = new IntRange { Min = 1, Max = 1 };
    }

    /// <summary>容器掉落表。对应 schemas/loot_table.schema.json。</summary>
    [Serializable]
    public sealed class LootTableData
    {
        public string Id { get; set; }
        public string ContainerType { get; set; }
        public IntRange Rolls { get; set; } = new IntRange { Min = 1, Max = 1 };
        public List<LootTableEntry> Entries { get; set; } = new List<LootTableEntry>();
    }
}

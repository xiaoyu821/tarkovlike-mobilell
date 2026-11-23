using System;
using System.Collections.Generic;

namespace Game.Data
{
    public enum ItemCategory
    {
        Weapon,
        Ammo,
        Armor,
        Helmet,
        Med,
        Loot,
        Key,
        Container,
        Mod
    }

    [Serializable]
    public sealed class GridSize
    {
        public int W { get; set; }
        public int H { get; set; }
    }

    /// <summary>背包中的物品定义。对应 schemas/item.schema.json。</summary>
    [Serializable]
    public sealed class ItemData
    {
        public string Id { get; set; }
        public string NameKey { get; set; }
        public string DisplayName { get; set; }
        public ItemCategory Category { get; set; }
        public GridSize Grid { get; set; } = new GridSize();
        public int WeightG { get; set; }
        public int Value { get; set; }
        public int StackMax { get; set; }
        public string Icon { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
}

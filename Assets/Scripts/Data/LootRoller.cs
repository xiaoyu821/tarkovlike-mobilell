using System;
using System.Collections.Generic;

namespace Game.Data
{
    public sealed class LootResult
    {
        public string ItemId { get; }
        public int Quantity { get; }

        public LootResult(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public override string ToString()
        {
            return ItemId + " x" + Quantity;
        }
    }

    /// <summary>
    /// 确定性掉落：同一 seed 必然得到同一结果，便于复现与单测。
    /// 语义见 docs/02-数据结构.md §3.5。
    /// </summary>
    public static class LootRoller
    {
        public static IReadOnlyList<LootResult> Roll(LootTableData table, int seed)
        {
            return Roll(table, new Random(seed));
        }

        public static IReadOnlyList<LootResult> Roll(LootTableData table, Random rng)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (table.Rolls == null || table.Rolls.Min < 1 || table.Rolls.Max < table.Rolls.Min)
            {
                throw new ArgumentException("掉落表 '" + table.Id + "' 的 rolls 区间非法", nameof(table));
            }

            if (table.Entries == null || table.Entries.Count == 0)
            {
                throw new ArgumentException("掉落表 '" + table.Id + "' 没有条目", nameof(table));
            }

            var totalWeight = 0;
            foreach (var entry in table.Entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("掉落表 '" + table.Id + "' 存在 null 条目", nameof(table));
                }

                if (entry.Weight <= 0)
                {
                    throw new ArgumentException(
                        "掉落表 '" + table.Id + "' 条目 '" + entry.ItemId + "' 的 weight 必须为正", nameof(table));
                }

                if (entry.Qty == null || entry.Qty.Min < 1 || entry.Qty.Max < entry.Qty.Min)
                {
                    throw new ArgumentException(
                        "掉落表 '" + table.Id + "' 条目 '" + entry.ItemId + "' 的 qty 区间非法", nameof(table));
                }

                totalWeight += entry.Weight;
            }

            var rollCount = rng.Next(table.Rolls.Min, table.Rolls.Max + 1);
            var results = new List<LootResult>(rollCount);

            for (var i = 0; i < rollCount; i++)
            {
                var entry = PickEntry(table.Entries, totalWeight, rng);
                var quantity = rng.Next(entry.Qty.Min, entry.Qty.Max + 1);
                results.Add(new LootResult(entry.ItemId, quantity));
            }

            return results;
        }

        private static LootTableEntry PickEntry(IReadOnlyList<LootTableEntry> entries, int totalWeight, Random rng)
        {
            var roll = rng.Next(totalWeight);

            foreach (var entry in entries)
            {
                if (roll < entry.Weight)
                {
                    return entry;
                }

                roll -= entry.Weight;
            }

            throw new InvalidOperationException("掉落权重计算错误");
        }
    }
}

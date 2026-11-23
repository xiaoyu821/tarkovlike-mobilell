using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Data
{
    public sealed class DataValidationException : Exception
    {
        public IReadOnlyList<string> Issues { get; }

        public DataValidationException(IReadOnlyList<string> issues)
            : base("数据校验失败，共 " + issues.Count + " 个问题：" +
                   Environment.NewLine + string.Join(Environment.NewLine, issues))
        {
            Issues = issues;
        }
    }

    /// <summary>
    /// 运行期数据目录：索引 + 交叉引用校验（对应 docs/02-数据结构.md §4 的 R1~R9）。
    /// 不依赖 UnityEngine，可脱离引擎单测。
    /// </summary>
    public sealed class DataCatalog
    {
        private readonly List<string> _loadIssues;

        public IReadOnlyDictionary<string, ItemData> Items { get; }
        public IReadOnlyDictionary<string, WeaponData> Weapons { get; }
        public IReadOnlyDictionary<string, AmmoData> Ammo { get; }
        public IReadOnlyDictionary<string, ArmorData> Armor { get; }
        public IReadOnlyDictionary<string, LootTableData> LootTables { get; }

        private DataCatalog(
            Dictionary<string, ItemData> items,
            Dictionary<string, WeaponData> weapons,
            Dictionary<string, AmmoData> ammo,
            Dictionary<string, ArmorData> armor,
            Dictionary<string, LootTableData> lootTables,
            List<string> loadIssues)
        {
            Items = items;
            Weapons = weapons;
            Ammo = ammo;
            Armor = armor;
            LootTables = lootTables;
            _loadIssues = loadIssues;
        }

        public static DataCatalog Create(
            IEnumerable<ItemData> items,
            IEnumerable<WeaponData> weapons,
            IEnumerable<AmmoData> ammo,
            IEnumerable<ArmorData> armor,
            IEnumerable<LootTableData> lootTables)
        {
            var issues = new List<string>();

            var itemMap = BuildMap(items, x => x.Id, "item", issues);
            var weaponMap = BuildMap(weapons, x => x.Id, "weapon", issues);
            var ammoMap = BuildMap(ammo, x => x.Id, "ammo", issues);
            var armorMap = BuildMap(armor, x => x.Id, "armor", issues);
            var lootMap = BuildMap(lootTables, x => x.Id, "loot", issues);

            CheckGlobalIdUniqueness(issues,
                ("item", itemMap.Keys),
                ("weapon", weaponMap.Keys),
                ("ammo", ammoMap.Keys),
                ("armor", armorMap.Keys),
                ("loot", lootMap.Keys));

            return new DataCatalog(itemMap, weaponMap, ammoMap, armorMap, lootMap, issues);
        }

        /// <summary>返回全部问题；空列表表示通过。</summary>
        public List<string> Validate()
        {
            var issues = new List<string>(_loadIssues);

            CheckNameKeyUniqueness(issues);

            foreach (var weapon in Weapons.Values)
            {
                ValidateWeapon(weapon, issues);
            }

            foreach (var ammo in Ammo.Values)
            {
                ValidateAmmo(ammo, issues);
            }

            foreach (var armor in Armor.Values)
            {
                ValidateArmor(armor, issues);
            }

            foreach (var table in LootTables.Values)
            {
                ValidateLootTable(table, issues);
            }

            return issues;
        }

        public void ThrowIfInvalid()
        {
            var issues = Validate();
            if (issues.Count > 0)
            {
                throw new DataValidationException(issues);
            }
        }

        private void ValidateWeapon(WeaponData weapon, List<string> issues)
        {
            var id = weapon.Id ?? "<null>";

            if (!Items.TryGetValue(weapon.ItemId ?? string.Empty, out var item))
            {
                issues.Add("武器 '" + id + "' 引用了不存在的物品 '" + weapon.ItemId + "'");
            }
            else if (item.Category != ItemCategory.Weapon)
            {
                issues.Add("武器 '" + id + "' 的物品 '" + weapon.ItemId + "' 类别不是 weapon");
            }

            if (weapon.AllowedAmmo == null || weapon.AllowedAmmo.Count == 0)
            {
                issues.Add("武器 '" + id + "' 没有配置 allowedAmmo");
            }
            else
            {
                foreach (var ammoId in weapon.AllowedAmmo)
                {
                    if (!Ammo.TryGetValue(ammoId ?? string.Empty, out var ammo))
                    {
                        issues.Add("武器 '" + id + "' 引用了不存在的弹药 '" + ammoId + "'");
                    }
                    else if (!string.Equals(ammo.Caliber, weapon.Caliber, StringComparison.Ordinal))
                    {
                        issues.Add("武器 '" + id + "' 的口径 '" + weapon.Caliber +
                                   "' 与弹药 '" + ammoId + "' 的口径 '" + ammo.Caliber + "' 不一致");
                    }
                }
            }

            if (weapon.DamageMultiplier < 0.5f || weapon.DamageMultiplier > 1.5f)
            {
                issues.Add("武器 '" + id + "' 的 damageMultiplier 超出 [0.5, 1.5]");
            }

            if (weapon.Rpm < 30 || weapon.Rpm > 1200)
            {
                issues.Add("武器 '" + id + "' 的 rpm 超出 [30, 1200]");
            }

            if (weapon.RecoilCurve == null ||
                weapon.RecoilCurve.Vertical == null || weapon.RecoilCurve.Vertical.Count == 0 ||
                weapon.RecoilCurve.Horizontal == null || weapon.RecoilCurve.Horizontal.Count == 0)
            {
                issues.Add("武器 '" + id + "' 的 recoilCurve 不能为空");
            }
        }

        private void ValidateAmmo(AmmoData ammo, List<string> issues)
        {
            var id = ammo.Id ?? "<null>";

            if (!Items.TryGetValue(ammo.ItemId ?? string.Empty, out var item))
            {
                issues.Add("弹药 '" + id + "' 引用了不存在的物品 '" + ammo.ItemId + "'");
            }
            else
            {
                if (item.Category != ItemCategory.Ammo)
                {
                    issues.Add("弹药 '" + id + "' 的物品 '" + ammo.ItemId + "' 类别不是 ammo");
                }

                if (item.StackMax <= 1)
                {
                    issues.Add("弹药 '" + id + "' 的物品 '" + ammo.ItemId + "' 必须可堆叠（stackMax > 1）");
                }
            }

            if (ammo.Pellets < 1 || ammo.Pellets > 16)
            {
                issues.Add("弹药 '" + id + "' 的 pellets 超出 [1, 16]");
            }
        }

        private void ValidateArmor(ArmorData armor, List<string> issues)
        {
            var id = armor.Id ?? "<null>";

            if (!Items.TryGetValue(armor.ItemId ?? string.Empty, out var item))
            {
                issues.Add("护甲 '" + id + "' 引用了不存在的物品 '" + armor.ItemId + "'");
            }
            else if (item.Category != ItemCategory.Armor && item.Category != ItemCategory.Helmet)
            {
                issues.Add("护甲 '" + id + "' 的物品 '" + armor.ItemId + "' 类别必须是 armor 或 helmet");
            }

            if (armor.Coverage == null || armor.Coverage.Count == 0)
            {
                issues.Add("护甲 '" + id + "' 的 coverage 不能为空");
            }
        }

        private void ValidateLootTable(LootTableData table, List<string> issues)
        {
            var id = table.Id ?? "<null>";

            if (table.Rolls == null || table.Rolls.Min < 1 || table.Rolls.Max < table.Rolls.Min)
            {
                issues.Add("掉落表 '" + id + "' 的 rolls 区间非法");
            }

            if (table.Entries == null || table.Entries.Count == 0)
            {
                issues.Add("掉落表 '" + id + "' 没有条目");
                return;
            }

            foreach (var entry in table.Entries)
            {
                if (entry == null)
                {
                    issues.Add("掉落表 '" + id + "' 存在 null 条目");
                    continue;
                }

                if (!Items.TryGetValue(entry.ItemId ?? string.Empty, out var item))
                {
                    issues.Add("掉落表 '" + id + "' 引用了不存在的物品 '" + entry.ItemId + "'");
                    continue;
                }

                if (entry.Weight <= 0)
                {
                    issues.Add("掉落表 '" + id + "' 条目 '" + entry.ItemId + "' 的 weight 必须为正");
                }

                if (entry.Qty == null || entry.Qty.Min < 1 || entry.Qty.Max < entry.Qty.Min)
                {
                    issues.Add("掉落表 '" + id + "' 条目 '" + entry.ItemId + "' 的 qty 区间非法");
                    continue;
                }

                if (entry.Qty.Max > item.StackMax)
                {
                    issues.Add("掉落表 '" + id + "' 条目 '" + entry.ItemId + "' 的 qty.max=" +
                               entry.Qty.Max + " 超过 stackMax=" + item.StackMax);
                }
            }
        }

        private void CheckNameKeyUniqueness(List<string> issues)
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var item in Items.Values)
            {
                var nameKey = item.NameKey ?? string.Empty;
                if (seen.TryGetValue(nameKey, out var otherId))
                {
                    issues.Add("物品 '" + item.Id + "' 与 '" + otherId + "' 的 nameKey 重复: '" + nameKey + "'");
                }
                else
                {
                    seen.Add(nameKey, item.Id ?? "<null>");
                }
            }
        }

        private static Dictionary<string, T> BuildMap<T>(
            IEnumerable<T> source,
            Func<T, string> idSelector,
            string kind,
            List<string> issues) where T : class
        {
            var map = new Dictionary<string, T>(StringComparer.Ordinal);

            if (source == null)
            {
                issues.Add(kind + ": 数据集合为 null");
                return map;
            }

            foreach (var entry in source)
            {
                if (entry == null)
                {
                    issues.Add(kind + ": 存在 null 条目");
                    continue;
                }

                var id = idSelector(entry);
                if (string.IsNullOrEmpty(id))
                {
                    issues.Add(kind + ": 存在空 ID");
                    continue;
                }

                if (map.ContainsKey(id))
                {
                    issues.Add(kind + ": 重复 ID '" + id + "'");
                    continue;
                }

                map.Add(id, entry);
            }

            return map;
        }

        private static void CheckGlobalIdUniqueness(
            List<string> issues,
            params (string Kind, IEnumerable<string> Ids)[] groups)
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var group in groups)
            {
                foreach (var id in group.Ids)
                {
                    if (seen.TryGetValue(id, out var otherKind))
                    {
                        issues.Add("ID '" + id + "' 在 " + otherKind + " 与 " + group.Kind + " 中重复");
                    }
                    else
                    {
                        seen.Add(id, group.Kind);
                    }
                }
            }
        }
    }
}

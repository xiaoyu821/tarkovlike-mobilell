using System.Collections.Generic;
using System.IO;
using Game.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Data.Tests
{
    public class DataCatalogTests
    {
        private static string GameDataDirectory
        {
            get { return Path.Combine(Application.dataPath, "StreamingAssets", "GameData"); }
        }

        [Test]
        public void LoadRepositoryData_HasExpectedCounts()
        {
            var catalog = JsonCatalogLoader.LoadFromDirectory(GameDataDirectory);

            Assert.AreEqual(18, catalog.Items.Count, "物品数量");
            Assert.AreEqual(5, catalog.Weapons.Count, "武器数量");
            Assert.AreEqual(5, catalog.Ammo.Count, "弹药数量");
            Assert.AreEqual(3, catalog.Armor.Count, "护甲数量");
            Assert.AreEqual(3, catalog.LootTables.Count, "掉落表数量");
        }

        [Test]
        public void RepositoryData_PassesCrossReferenceValidation()
        {
            var catalog = JsonCatalogLoader.LoadFromDirectory(GameDataDirectory);

            var issues = catalog.Validate();

            Assert.IsEmpty(issues, string.Join("\n", issues));
        }

        [Test]
        public void WeaponWithUnknownAmmo_IsReported()
        {
            var item = MakeWeaponItem("item_wpn_test", "l10n.item.wpn_test");
            var weapon = MakeWeapon(item.Id, "ammo_missing");

            var catalog = DataCatalog.Create(
                new[] { item },
                new[] { weapon },
                new AmmoData[0],
                new ArmorData[0],
                new LootTableData[0]);

            var issues = catalog.Validate();

            Assert.That(issues, Has.Some.Matches<string>(s => s.Contains("ammo_missing")));
        }

        [Test]
        public void WeaponWithMismatchedCaliber_IsReported()
        {
            var item = MakeWeaponItem("item_wpn_test", "l10n.item.wpn_test");
            var weapon = MakeWeapon(item.Id, "ammo_9x19");
            weapon.Caliber = "5.45x39";

            var ammoItem = new ItemData
            {
                Id = "item_ammo_test",
                NameKey = "l10n.item.ammo_test",
                Category = ItemCategory.Ammo,
                StackMax = 60
            };
            var ammo = new AmmoData
            {
                Id = "ammo_9x19",
                ItemId = ammoItem.Id,
                Caliber = "9x19",
                Damage = 30,
                Penetration = 10,
                MuzzleVelocity = 380,
                Pellets = 1
            };

            var catalog = DataCatalog.Create(
                new[] { item, ammoItem },
                new[] { weapon },
                new[] { ammo },
                new ArmorData[0],
                new LootTableData[0]);

            var issues = catalog.Validate();

            Assert.That(issues, Has.Some.Matches<string>(s => s.Contains("口径")));
        }

        [Test]
        public void LootQuantityAboveStackMax_IsReported()
        {
            var item = new ItemData
            {
                Id = "item_loot_test",
                NameKey = "l10n.item.loot_test",
                Category = ItemCategory.Loot,
                StackMax = 2
            };

            var table = new LootTableData
            {
                Id = "loot_test",
                ContainerType = "test",
                Rolls = new IntRange { Min = 1, Max = 1 },
                Entries = new List<LootTableEntry>
                {
                    new LootTableEntry
                    {
                        ItemId = item.Id,
                        Weight = 10,
                        Qty = new IntRange { Min = 1, Max = 5 }
                    }
                }
            };

            var catalog = DataCatalog.Create(
                new[] { item },
                new WeaponData[0],
                new AmmoData[0],
                new ArmorData[0],
                new[] { table });

            var issues = catalog.Validate();

            Assert.That(issues, Has.Some.Matches<string>(s => s.Contains("stackMax")));
        }

        private static ItemData MakeWeaponItem(string id, string nameKey)
        {
            return new ItemData
            {
                Id = id,
                NameKey = nameKey,
                Category = ItemCategory.Weapon,
                StackMax = 1
            };
        }

        private static WeaponData MakeWeapon(string itemId, string ammoId)
        {
            var weapon = new WeaponData
            {
                Id = "wpn_test",
                ItemId = itemId,
                Caliber = "9x19",
                BaseDamage = 30,
                DamageMultiplier = 1f,
                Rpm = 300,
                MagazineCapacity = 10,
                AllowedAmmo = new List<string> { ammoId }
            };

            weapon.RecoilCurve.Vertical.Add(1f);
            weapon.RecoilCurve.Horizontal.Add(1f);
            weapon.AttachmentSlots.Add(AttachmentSlot.Optic);

            return weapon;
        }
    }
}

using System.Collections.Generic;
using Game.Data;
using NUnit.Framework;

namespace Game.Data.Tests
{
    public class LootRollerTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalResults()
        {
            var table = MakeTable(
                Entry("item_a", 50, 1, 3),
                Entry("item_b", 30, 1, 2),
                Entry("item_c", 20, 1, 1));

            var first = LootRoller.Roll(table, 12345);
            var second = LootRoller.Roll(table, 12345);

            Assert.AreEqual(first.Count, second.Count);
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].ItemId, second[i].ItemId);
                Assert.AreEqual(first[i].Quantity, second[i].Quantity);
            }
        }

        [Test]
        public void Quantities_AreWithinConfiguredRange()
        {
            var table = MakeTable(Entry("item_a", 100, 3, 7));

            for (var seed = 0; seed < 50; seed++)
            {
                var results = LootRoller.Roll(table, seed);
                Assert.AreEqual(1, results.Count);
                Assert.That(results[0].Quantity, Is.InRange(3, 7));
            }
        }

        [Test]
        public void ZeroWeight_Throws()
        {
            var table = MakeTable(Entry("item_a", 0));

            Assert.Throws<System.ArgumentException>(() => LootRoller.Roll(table, 1));
        }

        [Test]
        public void Distribution_ApproximatesConfiguredWeights()
        {
            var table = MakeTable(
                Entry("item_common", 90),
                Entry("item_rare", 10));

            var rng = new System.Random(777);
            const int iterations = 10000;
            var commonCount = 0;

            for (var i = 0; i < iterations; i++)
            {
                var results = LootRoller.Roll(table, rng);
                if (results[0].ItemId == "item_common")
                {
                    commonCount++;
                }
            }

            var ratio = (double)commonCount / iterations;
            Assert.That(ratio, Is.InRange(0.85, 0.95), "实际比例 " + ratio);
        }

        private static LootTableData MakeTable(params LootTableEntry[] entries)
        {
            return new LootTableData
            {
                Id = "loot_test",
                ContainerType = "test",
                Rolls = new IntRange { Min = 1, Max = 1 },
                Entries = new List<LootTableEntry>(entries)
            };
        }

        private static LootTableEntry Entry(string itemId, int weight, int min = 1, int max = 1)
        {
            return new LootTableEntry
            {
                ItemId = itemId,
                Weight = weight,
                Qty = new IntRange { Min = min, Max = max }
            };
        }
    }
}

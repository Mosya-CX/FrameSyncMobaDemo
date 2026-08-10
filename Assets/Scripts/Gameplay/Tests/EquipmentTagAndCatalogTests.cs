using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Design v12 2.9-2.12: tags are ScriptableObjects with stable Uids;
    /// only tags listed in UniqueEquipmentTagTable enforce exclusivity.
    /// </summary>
    public sealed class EquipmentTagAndCatalogTests
    {
        private SimulationTickContextController tickController;

        [SetUp]
        public void SetUp()
        {
            tickController =
                new SimulationTickContextController();
            tickController.BeginTick(
                10,
                ExecutionMode.ServerAuthority);
        }

        [TearDown]
        public void TearDown()
        {
            tickController.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void UniqueTagInTable_ConflictingPurchase_Rejected()
        {
            EquipmentTagDefinition bootsTag =
                EquipmentTagDefinition.Create(
                    "Boots",
                    1001);
            EquipmentDefinition bootsA =
                MakeDefinition(1, "BootsA", 300, bootsTag);
            EquipmentDefinition bootsB =
                MakeDefinition(2, "BootsB", 400, bootsTag);

            TestContext context =
                CreateContext(
                    bootsA,
                    bootsB,
                    bootsTag);
            Assert.IsTrue(
                context.Handler.Add(bootsA, 0));

            Assert.IsFalse(
                context.Shop.TryBuildPurchasePlan(
                    0,
                    bootsB.Id,
                    1000,
                    context.Handler,
                    out _,
                    out EquipmentShopFailureReason failure));
            Assert.That(
                failure,
                Is.EqualTo(
                    EquipmentShopFailureReason
                        .UniqueTagConflict));
        }

        [Test]
        public void NonUniqueTag_NotInTable_AllowsCoexistence()
        {
            EquipmentTagDefinition attackTag =
                EquipmentTagDefinition.Create(
                    "Attack",
                    1002);
            EquipmentDefinition swordA =
                MakeDefinition(1, "SwordA", 200, attackTag);
            EquipmentDefinition swordB =
                MakeDefinition(2, "SwordB", 250, attackTag);

            TestContext context =
                CreateContext(
                    swordA,
                    swordB,
                    null);
            Assert.IsTrue(
                context.Handler.Add(swordA, 0));

            Assert.IsTrue(
                context.Shop.TryBuildPurchasePlan(
                    0,
                    swordB.Id,
                    1000,
                    context.Handler,
                    out _,
                    out EquipmentShopFailureReason failure),
                failure.ToString());
        }

        [Test]
        public void Catalog_Bake_SealsDefinitionsAndUniqueTags()
        {
            EquipmentTagDefinition bootsTag =
                EquipmentTagDefinition.Create(
                    "Boots",
                    1001);
            EquipmentDefinition boots =
                MakeDefinition(1, "Boots", 300, bootsTag);
            EquipmentDefinition dagger =
                MakeDefinition(2, "Dagger", 150, null);

            var catalog =
                ScriptableObject
                    .CreateInstance<EquipmentCatalogAsset>();
            catalog.Definitions = new[]
            {
                boots,
                dagger,
            };
            catalog.UniqueTags =
                new UniqueEquipmentTagTable
                {
                    UniqueTags = new[]
                    {
                        bootsTag,
                    },
                };

            EquipmentDatabase database =
                catalog.BakeOrThrow();

            Assert.That(database.Count, Is.EqualTo(2));
            Assert.That(
                database.UniqueTagTable
                    .IsUnique(bootsTag),
                Is.True);
            Assert.IsTrue(
                database.TryGetDefinitionsByTag(
                    bootsTag.Uid,
                    out var tagged));
            Assert.That(tagged.Count, Is.EqualTo(1));
        }

        [Test]
        public void Catalog_Bake_DuplicateId_Throws()
        {
            EquipmentDefinition first =
                MakeDefinition(7, "First", 100, null);
            EquipmentDefinition duplicate =
                MakeDefinition(7, "Duplicate", 100, null);

            var catalog =
                ScriptableObject
                    .CreateInstance<EquipmentCatalogAsset>();
            catalog.Definitions = new[]
            {
                first,
                duplicate,
            };

            Assert.Throws<System.InvalidOperationException>(
                () => catalog.BakeOrThrow());
        }

        private static EquipmentDefinition MakeDefinition(
            int id,
            string name,
            int value,
            EquipmentTagDefinition tag)
        {
            var def =
                ScriptableObject
                    .CreateInstance<EquipmentDefinition>();
            def.Id = id;
            def.Name = name;
            def.Description = "Tag test fixture";
            def.Tier =
                EquipmentTier.Finished;
            def.Value = value;
            def.MaxStack = 1;
            def.Tags =
                tag != null
                    ? new[] { tag }
                    : null;
            return def;
        }

        private static TestContext CreateContext(
            EquipmentDefinition first,
            EquipmentDefinition second,
            EquipmentTagDefinition uniqueTag)
        {
            var database = new EquipmentDatabase();
            database.Register(first);
            database.Register(second);
            if (uniqueTag != null)
            {
                database.SetUniqueTagTable(
                    new UniqueEquipmentTagTable
                    {
                        UniqueTags = new[]
                        {
                            uniqueTag,
                        },
                    });
            }
            database.Seal();

            var world = new UnitWorld
            {
                EquipmentDatabase = database,
            };
            Unit unit = UnitTestFactory.CreateUnit(
                new UnitUid(0, 3, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            unit.World = world;
            unit.EquipmentHandler.DefinitionDatabase =
                database;
            world.RegisterUnit(unit);

            var shop = new EquipmentShopRuntime();
            shop.Initialize(
                1,
                database,
                (fp)7 / (fp)10,
                world);
            shop.GetOrCreateTrader(0, unit.UnitUid);
            shop.ConfigureIncomeView(
                new FixedIncomeView());

            return new TestContext(
                shop,
                unit.EquipmentHandler);
        }

        private sealed class FixedIncomeView :
            IConfirmedGoldIncomeView
        {
            public int GetConfirmedEarnedGoldTotal(
                int playerSlot)
            {
                return 1000;
            }
        }

        private readonly struct TestContext
        {
            public readonly EquipmentShopRuntime Shop;
            public readonly EquipmentHandler Handler;

            public TestContext(
                EquipmentShopRuntime shop,
                EquipmentHandler handler)
            {
                Shop = shop;
                Handler = handler;
            }
        }
    }
}

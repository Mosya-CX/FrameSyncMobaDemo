using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class EquipmentShopTransactionTests
    {
        private SimulationTickContextController tickController;

        [SetUp]
        public void SetUp()
        {
            tickController = new SimulationTickContextController();
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
        public void Purchase_UsesOwnedRecipePartAndFreedLowestSlot()
        {
            EquipmentDefinition component =
                Definition(1, 100, EquipmentTier.Basic);
            EquipmentDefinition target =
                Definition(2, 500, EquipmentTier.Finished);
            target.Recipe = new EquipmentRecipe
            {
                Components = new[]
                {
                    new EquipmentRecipePart
                    {
                        Item = component,
                        Count = 2,
                    },
                },
            };
            EquipmentDefinition filler =
                Definition(3, 10, EquipmentTier.Basic);
            TestContext context =
                CreateContext(component, target, filler);
            Assert.IsTrue(
                context.Handler.Add(component, 0));
            for (int slot = 1;
                 slot < EquipmentHandler.SlotCount;
                 slot++)
                Assert.IsTrue(
                    context.Handler.Add(filler, slot));

            Assert.AreEqual(
                400,
                context.Shop.CalculatePurchasePrice(0, target.Id));
            Assert.IsTrue(
                context.Shop.TryBuildPurchasePlan(
                    0,
                    target.Id,
                    400,
                    context.Handler,
                    out EquipmentPurchasePlan plan,
                    out EquipmentShopFailureReason failure),
                failure.ToString());
            CollectionAssert.AreEqual(
                new[] { 0 },
                plan.ConsumedComponentSlots);
            Assert.AreEqual(0, plan.DestinationSlot);
            Assert.IsTrue(
                context.Shop.ProcessPurchase(
                    0,
                    plan,
                    context.Handler,
                    out ShopOperationRecord record));

            Assert.AreSame(
                target,
                context.Handler.GetSlotDef(0));
            Assert.AreEqual(-400, record.GoldDelta);
            Assert.AreEqual(
                -400,
                context.Shop.ComputeEffectiveShopGoldDelta(0));

            Assert.IsTrue(
                context.Shop.CanUndo(0, 0, out failure),
                failure.ToString());
            Assert.IsTrue(
                context.Shop.ProcessUndo(
                    0,
                    0,
                    context.Handler,
                    out ShopOperationRecord reverted));
            Assert.AreSame(
                component,
                context.Handler.GetSlotDef(0));
            Assert.IsTrue(reverted.Reverted);
            Assert.AreEqual(
                1,
                context.Shop.GetTrader(0).OperationLog.Count);
            Assert.AreEqual(
                0,
                context.Shop.ComputeEffectiveShopGoldDelta(0));
        }

        [Test]
        public void SellUndo_RequiresReturnedGoldAndRestoresSlot()
        {
            EquipmentDefinition item =
                Definition(10, 100, EquipmentTier.Basic);
            TestContext context = CreateContext(item);
            Assert.IsTrue(context.Handler.Add(item, 2));
            Assert.IsTrue(
                context.Shop.TrySell(
                    0,
                    2,
                    context.Handler,
                    out int sellValue,
                    out _));
            Assert.AreEqual(70, sellValue);
            Assert.IsTrue(
                context.Shop.ProcessSell(
                    0,
                    2,
                    context.Handler,
                    sellValue,
                    out _));

            Assert.IsFalse(
                context.Shop.CanUndo(
                    0,
                    sellValue - 1,
                    out EquipmentShopFailureReason failure));
            Assert.AreEqual(
                EquipmentShopFailureReason.InsufficientGold,
                failure);
            Assert.IsTrue(
                context.Shop.CanUndo(0, sellValue, out failure),
                failure.ToString());
            Assert.IsTrue(
                context.Shop.ProcessUndo(
                    0,
                    sellValue,
                    context.Handler,
                    out _));
            Assert.AreSame(item, context.Handler.GetSlotDef(2));
            Assert.AreEqual(
                0,
                context.Shop.ComputeEffectiveShopGoldDelta(0));
        }

        private static TestContext CreateContext(
            params EquipmentDefinition[] definitions)
        {
            var database = new EquipmentDatabase();
            for (int i = 0; i < definitions.Length; i++)
                database.Register(definitions[i]);
            database.Seal();
            var world = new UnitWorld
            {
                EquipmentDatabase = database,
            };
            Unit unit = UnitTestFactory.CreateUnit(
                new UnitUid(0, 1, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            unit.World = world;
            unit.EquipmentHandler.DefinitionDatabase = database;
            world.RegisterUnit(unit);
            var shop = new EquipmentShopRuntime();
            shop.Initialize(
                1,
                database,
                (fp)7 / (fp)10,
                world);
            shop.GetOrCreateTrader(0, unit.UnitUid);
            return new TestContext(
                shop,
                unit.EquipmentHandler);
        }

        private static EquipmentDefinition Definition(
            int id,
            int value,
            EquipmentTier tier)
        {
            return new EquipmentDefinition
            {
                Id = id,
                Name = $"TestEquipment{id}",
                Description = "Transaction test fixture",
                Tier = tier,
                Value = value,
                MaxStack =
                    tier == EquipmentTier.Consumable ? 3 : 1,
            };
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

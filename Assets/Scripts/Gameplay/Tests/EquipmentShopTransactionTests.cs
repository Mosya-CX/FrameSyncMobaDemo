using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

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

        [Test]
        public void DuplicateRule_AllowsSmallItemsAndRejectsFinishedItems()
        {
            EquipmentDefinition small =
                Definition(20, 100, EquipmentTier.Basic);
            TestContext smallContext =
                CreateContext(small);
            Assert.IsTrue(
                smallContext.Handler.Add(small, 0));
            Assert.IsTrue(
                smallContext.Shop.TryBuildPurchasePlan(
                    0,
                    small.Id,
                    100,
                    smallContext.Handler,
                    out EquipmentPurchasePlan smallPlan,
                    out EquipmentShopFailureReason smallFailure),
                smallFailure.ToString());
            Assert.IsTrue(
                smallContext.Shop.ProcessPurchase(
                    0,
                    smallPlan,
                    smallContext.Handler,
                    out _));
            Assert.AreSame(
                small,
                smallContext.Handler.GetSlotDef(0));
            Assert.AreSame(
                small,
                smallContext.Handler.GetSlotDef(1));

            EquipmentDefinition finished =
                Definition(21, 1000, EquipmentTier.Finished);
            TestContext finishedContext =
                CreateContext(finished);
            Assert.IsTrue(
                finishedContext.Handler.Add(finished, 0));
            Assert.IsFalse(
                finishedContext.Shop.TryBuildPurchasePlan(
                    0,
                    finished.Id,
                    1000,
                    finishedContext.Handler,
                    out _,
                    out EquipmentShopFailureReason finishedFailure));
            Assert.AreEqual(
                EquipmentShopFailureReason.DuplicateFinishedItem,
                finishedFailure);
        }

        [Test]
        public void SequentialPurchases_AfterSnapshotRestore_MatchContinuousState()
        {
            EquipmentDefinition first = Definition(
                31009,
                1150,
                EquipmentTier.Basic,
                new EquipmentFixedStatAuthoring
                {
                    Stat = StatId.AttackDamage,
                    Value = 15f,
                },
                new EquipmentFixedStatAuthoring
                {
                    Stat = StatId.MaxHealth,
                    Value = 250f,
                });
            EquipmentDefinition second = Definition(
                31010,
                1050,
                EquipmentTier.Basic,
                new EquipmentFixedStatAuthoring
                {
                    Stat = StatId.AttackDamage,
                    Value = 20f,
                },
                new EquipmentFixedStatAuthoring
                {
                    Stat = StatId.CooldownReduction,
                    Value = 10f,
                });
            TestContext context = CreateContext(first, second);

            Assert.IsTrue(context.Shop.TryBuildPurchasePlan(
                0,
                first.Id,
                10000,
                context.Handler,
                out EquipmentPurchasePlan firstPlan,
                out EquipmentShopFailureReason firstFailure),
                firstFailure.ToString());
            Assert.IsTrue(context.Shop.ProcessPurchase(
                0,
                firstPlan,
                context.Handler,
                out _));
            context.Handler.Owner.StatHandler.FinalizeTick();

            StatHandlerSnapshot anchorStats = default;
            EquipmentHandlerSnapshot anchorEquipment = default;
            EquipmentShopRuntimeSnapshot anchorShop = default;
            context.Handler.Owner.StatHandler.Capture(ref anchorStats);
            context.Handler.Capture(ref anchorEquipment);
            context.Shop.Capture(ref anchorShop);

            tickController.EndTick();
            tickController.BeginTick(11, ExecutionMode.ServerAuthority);
            PurchaseSecond(context, second.Id);
            context.Handler.Owner.StatHandler.FinalizeTick();
            StatHandlerSnapshot continuousStats = default;
            EquipmentHandlerSnapshot continuousEquipment = default;
            EquipmentShopRuntimeSnapshot continuousShop = default;
            context.Handler.Owner.StatHandler.Capture(ref continuousStats);
            context.Handler.Capture(ref continuousEquipment);
            context.Shop.Capture(ref continuousShop);

            context.Handler.Owner.StatHandler.Restore(anchorStats);
            context.Handler.Restore(anchorEquipment);
            context.Shop.Restore(anchorShop);
            var rollback = new RollbackContext(
                11,
                ExecutionMode.ClientReplay);
            context.Handler.Owner.StatHandler.Resolve(rollback);
            context.Handler.Resolve(rollback);
            context.Shop.Resolve(rollback);
            context.Handler.Owner.StatHandler.Rebuild(rollback);
            context.Handler.Rebuild(rollback);
            context.Shop.Rebuild(rollback);

            PurchaseSecond(context, second.Id);
            context.Handler.Owner.StatHandler.FinalizeTick();
            StatHandlerSnapshot replayStats = default;
            EquipmentHandlerSnapshot replayEquipment = default;
            EquipmentShopRuntimeSnapshot replayShop = default;
            context.Handler.Owner.StatHandler.Capture(ref replayStats);
            context.Handler.Capture(ref replayEquipment);
            context.Shop.Capture(ref replayShop);

            AssertStatSnapshotsEqual(continuousStats, replayStats);
            AssertEquipmentSnapshotsEqual(
                continuousEquipment,
                replayEquipment);
            AssertShopSnapshotsEqual(continuousShop, replayShop);
        }

        private static void PurchaseSecond(
            TestContext context,
            int equipmentId)
        {
            Assert.IsTrue(context.Shop.TryBuildPurchasePlan(
                0,
                equipmentId,
                10000,
                context.Handler,
                out EquipmentPurchasePlan plan,
                out EquipmentShopFailureReason failure),
                failure.ToString());
            Assert.IsTrue(context.Shop.ProcessPurchase(
                0,
                plan,
                context.Handler,
                out _));
        }

        private static void AssertStatSnapshotsEqual(
            in StatHandlerSnapshot expected,
            in StatHandlerSnapshot actual)
        {
            Assert.AreEqual(expected.Level, actual.Level);
            Assert.AreEqual(expected.CurrentHealth, actual.CurrentHealth);
            Assert.AreEqual(expected.CurrentCastResource, actual.CurrentCastResource);
            Assert.AreEqual(expected.CurrentExperience, actual.CurrentExperience);
            Assert.AreEqual(expected.NextStatSeq, actual.NextStatSeq);
            Assert.AreEqual(expected.Entries.Length, actual.Entries.Length);
            for (int i = 0; i < expected.Entries.Length; i++)
            {
                StatRuntimeEntrySnapshot left = expected.Entries[i];
                StatRuntimeEntrySnapshot right = actual.Entries[i];
                Assert.AreEqual(left.StatId, right.StatId, $"entry {i} stat");
                Assert.AreEqual(left.LevelBaseValue, right.LevelBaseValue, $"entry {i} base");
                Assert.AreEqual(left.FinalValue, right.FinalValue, $"entry {i} final");
                Assert.AreEqual(
                    left.PreviousLogicTickFinalValue,
                    right.PreviousLogicTickFinalValue,
                    $"entry {i} previous");
                Assert.AreEqual(left.Dirty, right.Dirty, $"entry {i} dirty");
                CollectionAssert.AreEqual(
                    left.Modifiers,
                    right.Modifiers,
                    $"entry {i} modifiers");
            }
        }

        private static void AssertEquipmentSnapshotsEqual(
            in EquipmentHandlerSnapshot expected,
            in EquipmentHandlerSnapshot actual)
        {
            Assert.AreEqual(expected.RuntimeRevision, actual.RuntimeRevision);
            Assert.AreEqual(expected.Slots.Count, actual.Slots.Count);
            for (int i = 0; i < expected.Slots.Count; i++)
            {
                EquipmentSlotSnapshot left = expected.Slots[i];
                EquipmentSlotSnapshot right = actual.Slots[i];
                Assert.AreEqual(left.Occupied, right.Occupied, $"slot {i} occupied");
                Assert.AreEqual(left.EquipmentId, right.EquipmentId, $"slot {i} item");
                Assert.AreEqual(left.StackCount, right.StackCount, $"slot {i} stack");
                Assert.AreEqual(left.ChargeCount, right.ChargeCount, $"slot {i} charge");
                Assert.AreEqual(left.ReadyTick, right.ReadyTick, $"slot {i} ready");
                CollectionAssert.AreEqual(
                    left.FixedStatHandles,
                    right.FixedStatHandles,
                    $"slot {i} handles");
            }
        }

        private static void AssertShopSnapshotsEqual(
            in EquipmentShopRuntimeSnapshot expected,
            in EquipmentShopRuntimeSnapshot actual)
        {
            Assert.AreEqual(
                expected.CreatedTraders.Count,
                actual.CreatedTraders.Count);
            for (int i = 0; i < expected.CreatedTraders.Count; i++)
            {
                ShopTraderRuntimeSnapshot left = expected.CreatedTraders[i];
                ShopTraderRuntimeSnapshot right = actual.CreatedTraders[i];
                Assert.AreEqual(left.Player, right.Player);
                Assert.AreEqual(left.ControlledUnitUid, right.ControlledUnitUid);
                Assert.AreEqual(left.NextOperationSequence, right.NextOperationSequence);
                Assert.AreEqual(left.OperationLog.Count, right.OperationLog.Count);
                CollectionAssert.AreEqual(
                    left.UndoableOperationStack,
                    right.UndoableOperationStack);
                for (int operation = 0;
                     operation < left.OperationLog.Count;
                     operation++)
                {
                    ShopOperationRecord a = left.OperationLog[operation];
                    ShopOperationRecord b = right.OperationLog[operation];
                    Assert.AreEqual(a.OperationSequence, b.OperationSequence);
                    Assert.AreEqual(a.OperationType, b.OperationType);
                    Assert.AreEqual(a.LogicTick, b.LogicTick);
                    Assert.AreEqual(a.GoldDelta, b.GoldDelta);
                    Assert.AreEqual(a.EquipmentRevisionBefore, b.EquipmentRevisionBefore);
                    Assert.AreEqual(a.EquipmentRevisionAfter, b.EquipmentRevisionAfter);
                }
            }
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
            EquipmentTier tier,
            params EquipmentFixedStatAuthoring[] fixedStats)
        {
            var def =
                ScriptableObject
                    .CreateInstance<EquipmentDefinition>();
            def.Id = id;
            def.Name = $"TestEquipment{id}";
            def.Description = "Transaction test fixture";
            def.Tier = tier;
            def.Value = value;
            def.MaxStack =
                tier == EquipmentTier.Consumable
                    ? 3
                    : 1;
            def.FixedStats = fixedStats;
            return def;
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

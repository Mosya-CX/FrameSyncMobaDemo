using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// Minimal targeted tests for the Shop UI Foundation (ExecPlan 0055).
    /// Validates EquipmentShopRuntime gold delta, snapshot round trip,
    /// and database operations.
    /// </summary>
    public class ShopFoundationEditModeTests
    {
        private EquipmentDatabase _database;
        private EquipmentShopRuntime _shop;
        private SimulationTickContextController _tickController;

        [SetUp]
        public void SetUp()
        {
            _database = new EquipmentDatabase();
            var unitWorld = new UnitWorld { EquipmentDatabase = _database };
            _shop = new EquipmentShopRuntime();
            _shop.Initialize(2, _database, (fp)0.7f, unitWorld);
            _tickController = new SimulationTickContextController();
            _tickController.BeginTick(0, ExecutionMode.ServerAuthority);
        }

        [TearDown]
        public void TearDown()
        {
            _tickController.EndTick();
            _tickController = null;
            _shop = null;
            _database = null;
        }

        [Test]
        public void Database_RegisterAndRetrieve()
        {
            var def = MakeEquipment(1001, "Test Sword", 500, EquipmentTier.Basic);
            _database.Register(def);
            _database.Seal();
            var retrieved = _database.GetDefinition(1001);
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Name, Is.EqualTo("Test Sword"));
            Assert.That(retrieved.Value, Is.EqualTo(500));
            Assert.That(retrieved.IsBaked, Is.True);
        }

        [Test]
        public void Database_AllDefinitions_Sorted()
        {
            _database.Register(MakeEquipment(3002, "Item B", 200, EquipmentTier.Basic));
            _database.Register(MakeEquipment(1001, "Item A", 100, EquipmentTier.Basic));
            _database.Register(MakeEquipment(2003, "Item C", 300, EquipmentTier.Consumable));
            _database.Seal();
            var all = _database.AllDefinitions;
            Assert.That(all.Count, Is.EqualTo(3));
            Assert.That(all[0].Id, Is.EqualTo(1001));
            Assert.That(all[1].Id, Is.EqualTo(2003));
            Assert.That(all[2].Id, Is.EqualTo(3002));
        }

        [Test]
        public void ShopTrader_CreateAndRetrieve()
        {
            var uid = new UnitUid(0, 42, 1);
            var trader = _shop.GetOrCreateTrader(0, uid);
            Assert.That(trader, Is.Not.Null);
            Assert.That(trader.Player, Is.EqualTo(0));
            var retrieved = _shop.GetTrader(0);
            Assert.That(retrieved, Is.SameAs(trader));
        }

        [Test]
        public void GoldDelta_InitialZero()
        {
            int delta = _shop.ComputeEffectiveShopGoldDelta(0);
            Assert.That(delta, Is.EqualTo(0));
        }

        [Test]
        public void GoldDelta_AfterManualLog()
        {
            var uid = new UnitUid(0, 42, 1);
            var trader = _shop.GetOrCreateTrader(0, uid);
            trader.OperationLog.Add(new ShopOperationRecord
            {
                OperationSequence = 0,
                OperationType = EquipmentShopOperationType.Purchase,
                Player = 0,
                ControlledUnitUid = uid,
                LogicTick = 0,
                GoldDelta = -300,
                Reverted = false,
            });
            trader.NextOperationSequence = 1;
            int delta = _shop.ComputeEffectiveShopGoldDelta(0);
            Assert.That(delta, Is.EqualTo(-300));
        }

        [Test]
        public void Snapshot_RoundTrip_PreservesTrader()
        {
            var uid = new UnitUid(0, 42, 1);
            _shop.GetOrCreateTrader(0, uid);
            var snapshot = EquipmentShopRuntimeSnapshot.Empty;
            _shop.Capture(ref snapshot);

            var unitWorld2 = new UnitWorld { EquipmentDatabase = _database };
            var shop2 = new EquipmentShopRuntime();
            shop2.Initialize(2, _database, (fp)0.7f, unitWorld2);
            shop2.Restore(snapshot);

            var restored = shop2.GetTrader(0);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Player, Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_RoundTrip_PreservesOperationLog()
        {
            var uid = new UnitUid(0, 42, 1);
            var trader = _shop.GetOrCreateTrader(0, uid);
            trader.OperationLog.Add(new ShopOperationRecord
            {
                OperationSequence = 0,
                OperationType = EquipmentShopOperationType.Purchase,
                Player = 0,
                ControlledUnitUid = uid,
                LogicTick = 0,
                GoldDelta = -500,
                SlotChanges = new[]
                {
                    new EquipmentSlotChange
                    {
                        Slot = 0,
                        Before = EquipmentTransactionSlotState.Empty,
                        After = new EquipmentTransactionSlotState
                        {
                            Occupied = true,
                            EquipmentId = 1001,
                            StackCount = 1,
                        },
                    },
                },
                Reverted = false,
            });
            trader.NextOperationSequence = 1;
            trader.UndoableOperationStack.Add(0);

            var snapshot = EquipmentShopRuntimeSnapshot.Empty;
            _shop.Capture(ref snapshot);

            var unitWorld2 = new UnitWorld { EquipmentDatabase = _database };
            var shop2 = new EquipmentShopRuntime();
            shop2.Initialize(2, _database, (fp)0.7f, unitWorld2);
            shop2.Restore(snapshot);

            int delta = shop2.ComputeEffectiveShopGoldDelta(0);
            Assert.That(delta, Is.EqualTo(-500));
        }

        private static EquipmentDefinition MakeEquipment(
            int id, string name, int value, EquipmentTier tier)
        {
            var def =
                ScriptableObject
                    .CreateInstance<EquipmentDefinition>();
            def.Id = id;
            def.Name = name;
            def.Description = "Test equipment " + id;
            def.Tier = tier;
            def.Value = value;
            def.MaxStack =
                tier == EquipmentTier.Consumable
                    ? 3
                    : 1;
            return def;
        }
    }
}

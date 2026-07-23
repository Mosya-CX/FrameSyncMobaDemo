using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class EquipmentShopRuntimeSnapshotTests
    {
        [TearDown]
        public void TearDown() => UnitTestFactory.DestroyCreatedObjects();

        [Test]
        public void RestoreResolve_PreservesValidControlledUnitReference()
        {
            UnitWorld world = CreateWorldWithUnit(out Unit unit);
            EquipmentShopRuntime source = CreateRuntime(world);
            source.GetOrCreateTrader(0, unit.UnitUid);
            EquipmentShopRuntimeSnapshot snapshot = default;
            source.Capture(ref snapshot);

            EquipmentShopRuntime restored = CreateRuntime(world);
            restored.Restore(snapshot);
            restored.Resolve(default);

            Assert.AreEqual(
                unit.UnitUid,
                restored.GetTrader(0).ControlledUnitUid);
        }

        [Test]
        public void Restore_RejectsNoncanonicalPlayerOrder()
        {
            UnitWorld world = CreateWorldWithUnit(out Unit unit);
            EquipmentShopRuntime runtime = CreateRuntime(world);
            var snapshot = new EquipmentShopRuntimeSnapshot
            {
                CreatedTraders = new List<ShopTraderRuntimeSnapshot>
                {
                    new ShopTraderRuntimeSnapshot
                    {
                        Player = 1,
                        ControlledUnitUid = unit.UnitUid,
                    },
                    new ShopTraderRuntimeSnapshot
                    {
                        Player = 0,
                        ControlledUnitUid = unit.UnitUid,
                    },
                },
            };

            Assert.Throws<DeterministicSimulationException>(
                () => runtime.Restore(snapshot));
        }

        private static EquipmentShopRuntime CreateRuntime(UnitWorld world)
        {
            var runtime = new EquipmentShopRuntime();
            runtime.Initialize(
                2,
                new EquipmentDatabase(),
                (fp)7 / (fp)10,
                world);
            return runtime;
        }

        private static UnitWorld CreateWorldWithUnit(out Unit unit)
        {
            var world = new UnitWorld();
            unit = UnitTestFactory.CreateUnit(
                new UnitUid(1, 10, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            unit.World = world;
            world.RegisterUnit(unit);
            return world;
        }
    }
}

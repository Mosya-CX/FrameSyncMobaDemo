using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class UnitMonoBehaviourCompositionTests
    {
        [Test]
        public void FactoryUnit_BindsEveryHandlerToSameComponentOwner()
        {
            Unit unit = UnitTestFactory.CreateUnit(
                new UnitUid(12, 7, 3),
                UnitKind.Hero,
                0,
                new TeamId(2));

            Assert.That(unit, Is.InstanceOf<MonoBehaviour>());
            Assert.AreSame(unit, unit.StatHandler.Owner);
            Assert.AreSame(unit, unit.MovementHandler.Owner);
            Assert.AreSame(unit, unit.AttackHandler.Owner);
            Assert.AreSame(unit, unit.AbilityHandler.Owner);
            Assert.AreSame(unit, unit.BuffHandler.Owner);
            Assert.AreSame(unit, unit.CrowdControl.Owner);
            Assert.AreSame(unit, unit.EquipmentHandler.Owner);
            Assert.AreSame(unit.gameObject, unit.PhysicsEntity.gameObject);
        }

        [Test]
        public void Death_PreservesGlobalStatAndCombatModifierOwnership()
        {
            Unit unit = UnitTestFactory.CreateUnit(
                new UnitUid(12, 7, 3),
                UnitKind.Hero,
                0,
                TeamId.Neutral);
            unit.StatHandler.AddModifier(
                StatId.MaxHealth,
                StatModifierOperation.FlatAdd,
                25);
            unit.CombatModifiers.Attach(new CombatModifierRecord
            {
                Id = CombatModifierId.Create(4, "Test.Owner"),
            });

            unit.ClearForDeath();

            Assert.AreEqual(1, unit.CombatModifiers.Count);
            Assert.AreEqual((Unity.Mathematics.FixedPoint.fp)125, unit.StatHandler.GetStat(StatId.MaxHealth));
        }

        [Test]
        public void PoolReset_PreservesPrefabTopologyAndClearsRuntimeIdentity()
        {
            Unit unit = UnitTestFactory.CreateUnit(
                new UnitUid(12, 7, 3),
                UnitKind.Minion,
                5,
                new TeamId(2));

            unit.ResetForPool();

            Assert.IsFalse(unit.UnitUid.IsValid());
            Assert.IsNotNull(unit.StatHandler);
            Assert.IsNotNull(unit.MovementHandler);
            Assert.IsNotNull(unit.AttackHandler);
            Assert.IsNotNull(unit.PhysicsEntity);
        }
    }
}

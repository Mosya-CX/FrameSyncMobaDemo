using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class MinionInitialBuffTests
    {
        private UnitWorld world;
        private CombatSystem combat;
        private SimulationTickContextController controller;

        private static readonly BuffConfigId Muncher =
            new BuffConfigId(9101);
        private static readonly BuffConfigId Pincushion =
            new BuffConfigId(9102);
        private static readonly BuffConfigId TowerPillow =
            new BuffConfigId(9103);

        [SetUp]
        public void SetUp()
        {
            controller =
                new SimulationTickContextController();
            world = new UnitWorld
            {
                StatDefinitionTable =
                    new StatDefinitionTable(),
                BuffDefinitions =
                    new BuffDefinitionRegistry(),
            };
            RegisterBuffs();
            combat = new CombatSystem(
                world, 300, 60);
        }

        [TearDown]
        public void TearDown()
        {
            if (controller.IsTickActive)
            {
                controller.EndTick();
            }
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void InitialBuffs_AppliedAutomaticallyFromPrototype()
        {
            controller.BeginTick(
                1,
                ExecutionMode.ServerAuthority);
            Unit minion = Spawn(
                2001,
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new[] { 9101, 9103 });

            Assert.That(
                minion.BuffHandler.HasBuff(
                    Muncher),
                Is.True);
            Assert.That(
                minion.BuffHandler.HasBuff(
                    TowerPillow),
                Is.True);
        }

        [Test]
        public void MeleeMinion_AttacksMinion_AddsTwoPercentCurrentHealth()
        {
            controller.BeginTick(
                1,
                ExecutionMode.ServerAuthority);
            Unit attacker = Spawn(
                2001,
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new[] { 9101, 9103 });
            Unit target = Spawn(
                2002,
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new int[0]);

            fp damage = DealAttackDamage(
                attacker,
                target,
                (fp)11);

            // 11 + 430 * 2% (target current health) = 19.6
            Assert.That(
                fpmath.abs(
                    damage - (fp)19.6m) <
                    (fp)0.001m,
                Is.True);
        }

        [Test]
        public void RangedMinion_AttacksMinion_AddsThreePointFivePercentCurrentHealth()
        {
            controller.BeginTick(
                1,
                ExecutionMode.ServerAuthority);
            Unit attacker = Spawn(
                2101,
                UnitKind.Minion,
                NonHeroUnitSubKindId.RangedMinion,
                new[] { 9102, 9103 });
            Unit target = Spawn(
                2002,
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new int[0]);

            fp damage = DealAttackDamage(
                attacker,
                target,
                (fp)21);

            // 21 + 430 * 3.5% = 36.05
            Assert.That(
                fpmath.abs(
                    damage - (fp)36.05m) <
                    (fp)0.001m,
                Is.True);
        }

        [Test]
        public void Minion_AttacksStructure_DealsSixtyPercent()
        {
            controller.BeginTick(
                1,
                ExecutionMode.ServerAuthority);
            Unit attacker = Spawn(
                2001,
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new[] { 9101, 9103 });
            Unit tower = Spawn(
                3001,
                UnitKind.Structure,
                0,
                new int[0]);

            fp damage = DealAttackDamage(
                attacker,
                tower,
                (fp)11);

            // 11 * 60% = 6.6 (armor 0 structure)
            Assert.That(
                fpmath.abs(
                    damage - (fp)6.6m) <
                    (fp)0.001m,
                Is.True);
        }

        private Unit Spawn(
            int prototypeId,
            UnitKind kind,
            ushort subKindId,
            int[] initialBuffs)
        {
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = prototypeId,
                Name = "Test_" + prototypeId,
                RuntimeEntityPrefabId =
                    prototypeId,
                UnitKind = kind,
                UnitSubKindId = subKindId,
                BaseStats = CreateStats(),
                InitialBuffConfigIds =
                    ToBuffConfigIds(initialBuffs),
            };
            return world.SpawnUnit(
                prototype,
                TeamId.Neutral,
                1,
                fp.zero,
                fp.zero);
        }

        private static BuffConfigId[]
            ToBuffConfigIds(int[] values)
        {
            var result =
                new BuffConfigId[values.Length];
            for (int i = 0;
                 i < values.Length;
                 i++)
            {
                result[i] =
                    new BuffConfigId(values[i]);
            }
            return result;
        }

        private static StatPreset CreateStats()
        {
            var preset = new StatPreset();
            preset.Stats.Add(
                new StatPresetEntry
                {
                    StatId = StatId.MaxHealth,
                    BaseValue = (fp)430,
                });
            preset.Stats.Add(
                new StatPresetEntry
                {
                    StatId = StatId.AttackDamage,
                    BaseValue = (fp)11,
                });
            return preset;
        }

        private fp DealAttackDamage(
            Unit attacker,
            Unit target,
            fp baseDamage)
        {
            fp initial =
                target.StatHandler.CurrentHealth;
            combat.BeginTick();
            combat.SubmitDamage(
                UnitTestFactory.CreateDamageRequest(
                    attacker.UnitUid,
                    target.UnitUid,
                    baseDamage,
                    DamageType.Physical,
                    CombatSourceType.Attack,
                    sourceId: 1,
                    recipeId: 1));
            combat.SettleActiveRequests();
            combat.EndTick();
            return initial -
                target.StatHandler.CurrentHealth;
        }

        private void RegisterBuffs()
        {
            RegisterBuff(
                Muncher,
                new CombatModifierMatch(
                    SourceTypeMask.Attack,
                    0,
                    0,
                    DamageTypeMask.Physical,
                    1UL << (int)UnitKind.Minion),
                new CombatFormulaPatch(
                    CombatFormulaSlot.FinalValue,
                    CombatModifierOperation.Add,
                    new CombatOperand(
                        fp.zero,
                        new[]
                        {
                            new CombatOperandTerm(
                                new CombatValueRef(
                                    CombatValueRefKind
                                        .TargetCurrentHealth),
                                (fp)0.02m),
                        })));
            RegisterBuff(
                Pincushion,
                new CombatModifierMatch(
                    SourceTypeMask.Attack,
                    0,
                    0,
                    DamageTypeMask.Physical,
                    1UL << (int)UnitKind.Minion),
                new CombatFormulaPatch(
                    CombatFormulaSlot.FinalValue,
                    CombatModifierOperation.Add,
                    new CombatOperand(
                        fp.zero,
                        new[]
                        {
                            new CombatOperandTerm(
                                new CombatValueRef(
                                    CombatValueRefKind
                                        .TargetCurrentHealth),
                                (fp)0.035m),
                        })));
            RegisterBuff(
                TowerPillow,
                new CombatModifierMatch(
                    SourceTypeMask.Attack,
                    0,
                    0,
                    DamageTypeMask.Physical,
                    1UL << (int)UnitKind.Structure),
                new CombatFormulaPatch(
                    CombatFormulaSlot.FinalValue,
                    CombatModifierOperation.Multiply,
                    new CombatOperand((fp)0.6m)));
        }

        private void RegisterBuff(
            BuffConfigId id,
            CombatModifierMatch match,
            CombatFormulaPatch patch)
        {
            var definition =
                ScriptableObject.CreateInstance<
                    BuffDefinition>();
            definition.ConfigId = id;
            definition.Life =
                new BuffLifeRuleConfig
                {
                    Infinite = true,
                };
            definition.Stack =
                new BuffStackRuleConfig
                {
                    MaxStacks = 1,
                };
            definition.Effects =
                new[]
                {
                    new BuffEffectConfig
                    {
                        Effect =
                            new CombatModifierBuffEffect
                            {
                                Record =
                                    new CombatModifierRecord
                                    {
                                        Id =
                                            (ulong)id.Value,
                                        Domain =
                                            CombatDomain
                                                .Damage,
                                        Scope =
                                            CombatModifierScope
                                                .Outgoing,
                                        Match = match,
                                        ValuePatches =
                                            new[]
                                            {
                                                patch,
                                            },
                                    },
                                HandleSlot =
                                    new BuffStateSlotId(
                                        1),
                            },
                    },
                };
            world.BuffDefinitions.Register(
                definition);
        }
    }
}

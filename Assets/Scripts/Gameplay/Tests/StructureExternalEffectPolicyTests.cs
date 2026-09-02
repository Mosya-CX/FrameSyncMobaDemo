using System;
using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class StructureExternalEffectPolicyTests
    {
        private SimulationTickContextController controller;
        private UnitWorld world;
        private CombatSystem combat;
        private UnitType attacker;
        private UnitType structure;
        private CrowdControlDefinition crowdControlDefinition;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
            controller.BeginTick(10, ExecutionMode.ServerAuthority);
            world = new UnitWorld
            {
                CrowdControlDefinitions =
                    new CrowdControlDefinitionRegistry(),
            };
            crowdControlDefinition =
                UnityEngine.ScriptableObject.CreateInstance<
                    CrowdControlDefinition>();
            crowdControlDefinition.Configure(
                new CrowdControlId(9902),
                CrowdControlIntensity.Low,
                CrowdControlDefinition.ControlTagBits.Control,
                CrowdControlDurationRule.DefaultTenacity,
                Array.Empty<CrowdControlParamAuthoring>(),
                Array.Empty<CrowdControlModuleAuthoring>());
            world.CrowdControlDefinitions.Register(
                crowdControlDefinition);
            attacker = Spawn(UnitKind.Hero, 1, (fp)1000);
            structure = Spawn(UnitKind.Structure, 2, (fp)1000);
            combat = new CombatSystem(world, 0, 0);
            world.CombatSystem = combat;
            combat.BeginTick();
        }

        [TearDown]
        public void TearDown()
        {
            controller.EndTick();
            UnityEngine.Object.DestroyImmediate(
                crowdControlDefinition);
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void ExternalOrdinaryAttackDamage_IsAccepted()
        {
            Assert.IsTrue(combat.SubmitDamage(
                CreateDamage(CombatSourceType.Attack, (fp)100)));

            combat.SettleActiveRequests();

            Assert.AreEqual(
                (double)((fp)900),
                (double)structure.StatHandler.CurrentHealth,
                0.001);
        }

        [TestCase(CombatSourceType.Ability)]
        [TestCase(CombatSourceType.Buff)]
        [TestCase(CombatSourceType.Equipment)]
        [TestCase(CombatSourceType.AttackEffect)]
        [TestCase(CombatSourceType.System)]
        public void ExternalNonAttackDamage_IsRejected(
            CombatSourceType sourceType)
        {
            Assert.IsTrue(combat.SubmitDamage(
                CreateDamage(sourceType, (fp)100)));

            combat.SettleActiveRequests();

            Assert.AreEqual(
                (double)((fp)1000),
                (double)structure.StatHandler.CurrentHealth,
                0.001);
            Assert.AreEqual(0, combat.DamageProcessed);
        }

        [Test]
        public void AttackTypedNonBasicSource_IsRejected()
        {
            DamageRequest request = CreateDamage(
                CombatSourceType.Attack,
                (fp)100);
            request.Header.SourceDescriptor.SourceId = 10011;

            Assert.IsTrue(combat.SubmitDamage(request));
            combat.SettleActiveRequests();

            Assert.AreEqual(
                (double)((fp)1000),
                (double)structure.StatHandler.CurrentHealth,
                0.001);
        }

        [Test]
        public void ExternalHealAndShield_AreRejected()
        {
            combat.SubmitHeal(new HealRequest
            {
                Header = CreateHeader(CombatSourceType.Ability),
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = structure.UnitUid,
                BaseValue = (fp)50,
            });
            combat.SubmitShield(new ShieldRequest
            {
                Header = CreateHeader(CombatSourceType.Ability),
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = structure.UnitUid,
                BaseValue = (fp)50,
                ShieldType = ShieldType.White,
                DurationTicks = 10,
            });

            combat.SettleActiveRequests();

            Assert.AreEqual(0, combat.HealProcessed);
            Assert.AreEqual(0, combat.ShieldProcessed);
        }

        [Test]
        public void SelfOwnedStructureBuff_IsAllowedButExternalBuffIsRejected()
        {
            BuffDefinition definition =
                UnityEngine.ScriptableObject
                    .CreateInstance<BuffDefinition>();
            definition.ConfigId = new BuffConfigId(9901);
            definition.Display = new BuffDisplayInfo
            {
                Name = "StructureSelfBuff",
            };
            definition.Life = new BuffLifeRuleConfig
            {
                DurationSeconds = 1f,
            };
            definition.Stack = new BuffStackRuleConfig
            {
                MaxStacks = 1,
                AddMode = BuffAddMode.Add,
                ReduceMode = BuffReduceMode.Reduce,
            };

            Assert.IsFalse(structure.BuffHandler.Apply(
                definition.ConfigId,
                definition,
                attacker.UnitUid));
            Assert.IsTrue(structure.BuffHandler.Apply(
                definition.ConfigId,
                definition,
                structure.UnitUid));
            Assert.IsTrue(structure.BuffHandler.HasBuff(
                definition.ConfigId));
        }

        [Test]
        public void ExternalCrowdControl_IsRejectedButSelfOwnedIsAllowed()
        {
            CrowdControlAddResult external =
                StructureEffectPolicy.TryApplyCrowdControl(
                    structure,
                    attacker.UnitUid,
                    new CrowdControlId(9902),
                    10,
                    default);
            Assert.AreEqual(
                CrowdControlAddStatus.OwnerRejected,
                external.Status);
            Assert.AreEqual(0, structure.CrowdControl.Count);

            CrowdControlAddResult selfOwned =
                StructureEffectPolicy.TryApplyCrowdControl(
                    structure,
                    structure.UnitUid,
                    new CrowdControlId(9902),
                    10,
                    default);
            Assert.IsTrue(selfOwned.Added);
            Assert.AreEqual(1, structure.CrowdControl.Count);
        }

        [Test]
        public void ExternalBuffToHandlerlessTower_IsConsumedNoOp()
        {
            UnitType handlerlessTower = Spawn(
                UnitKind.Structure,
                3,
                (fp)1000);
            UnityEngine.Object.DestroyImmediate(
                handlerlessTower.BuffHandler);
            BuffDefinition definition =
                UnityEngine.ScriptableObject
                    .CreateInstance<BuffDefinition>();
            definition.ConfigId = new BuffConfigId(9903);
            definition.Display = new BuffDisplayInfo
            {
                Name = "ExternalTowerProbe",
            };
            definition.Life = new BuffLifeRuleConfig
            {
                DurationSeconds = 1f,
            };
            definition.Stack = new BuffStackRuleConfig
            {
                MaxStacks = 1,
                AddMode = BuffAddMode.Add,
                ReduceMode = BuffReduceMode.Reduce,
            };

            try
            {
                Assert.IsTrue(
                    handlerlessTower.BuffHandler == null);
                bool applied = true;
                Assert.DoesNotThrow(() =>
                    applied = StructureEffectPolicy.TryApplyBuff(
                        handlerlessTower,
                        definition.ConfigId,
                        definition,
                        BuffSource.Create(
                            attacker.UnitUid,
                            BuffSourceType.Ability,
                            10011)));
                Assert.IsFalse(applied);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ExternalCrowdControlToHandlerlessTower_IsConsumedNoOp()
        {
            UnitType handlerlessTower = Spawn(
                UnitKind.Structure,
                4,
                (fp)1000);
            UnityEngine.Object.DestroyImmediate(
                handlerlessTower.CrowdControl);

            Assert.IsTrue(
                handlerlessTower.CrowdControl == null);
            CrowdControlAddResult result = default;
            Assert.DoesNotThrow(() =>
                result = StructureEffectPolicy.TryApplyCrowdControl(
                    handlerlessTower,
                    attacker.UnitUid,
                    new CrowdControlId(9902),
                    10,
                    default));
            Assert.AreEqual(
                CrowdControlAddStatus.OwnerRejected,
                result.Status);
        }

        [Test]
        public void InvalidExternalCrowdControl_IsNotHiddenByStructureFilter()
        {
            CrowdControlAddResult missingDefinition =
                StructureEffectPolicy.TryApplyCrowdControl(
                    structure,
                    attacker.UnitUid,
                    new CrowdControlId(9999),
                    10,
                    default);
            CrowdControlAddResult invalidDuration =
                StructureEffectPolicy.TryApplyCrowdControl(
                    structure,
                    attacker.UnitUid,
                    new CrowdControlId(9902),
                    0,
                    default);

            Assert.AreEqual(
                CrowdControlAddStatus.InvalidDefinition,
                missingDefinition.Status);
            Assert.AreEqual(
                CrowdControlAddStatus.InvalidDuration,
                invalidDuration.Status);
        }

        [Test]
        public void DeferredExternalEffects_AreFilteredBeforeSequenceAndSnapshot()
        {
            DamageRequest rejectedDamage = CreateDamage(
                CombatSourceType.Ability,
                (fp)100);
            var rejectedHeal = new HealRequest
            {
                Header = CreateHeader(CombatSourceType.Ability),
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = structure.UnitUid,
                BaseValue = (fp)50,
            };
            var rejectedShield = new ShieldRequest
            {
                Header = CreateHeader(CombatSourceType.Ability),
                SourceUnitUid = attacker.UnitUid,
                TargetUnitUid = structure.UnitUid,
                BaseValue = (fp)50,
                ShieldType = ShieldType.White,
                DurationTicks = 10,
            };

            combat.DeferRequest(
                CombatRequestKind.Damage,
                null,
                rejectedDamage,
                null,
                11,
                10);
            combat.DeferRequest(
                CombatRequestKind.Heal,
                null,
                null,
                rejectedHeal,
                11,
                10);
            combat.DeferRequest(
                CombatRequestKind.Shield,
                rejectedShield,
                null,
                null,
                11,
                10);
            combat.DeferRequest(
                CombatRequestKind.Damage,
                null,
                CreateDamage(CombatSourceType.Attack, (fp)100),
                null,
                11,
                10);

            CombatSnapshot snapshot = CombatSnapshot.Default;
            combat.Capture(ref snapshot);

            Assert.AreEqual(1, snapshot.DeferredRequests.Length);
            Assert.AreEqual(
                CombatRequestKind.Damage,
                snapshot.DeferredRequests[0].RequestKind);
            Assert.AreEqual(
                0,
                snapshot.DeferredRequests[0]
                    .DeferredSequenceInSourceTick);
        }

        private DamageRequest CreateDamage(
            CombatSourceType sourceType,
            fp amount)
        {
            return new DamageRequest
            {
                Header = CreateHeader(sourceType),
                BaseDamage = amount,
                DamageType = DamageType.Physical,
            };
        }

        private CombatRequestHeader CreateHeader(
            CombatSourceType sourceType)
        {
            return CombatRequestHeader.Create(
                attacker.UnitUid,
                structure.UnitUid,
                sourceType,
                1,
                1);
        }

        private UnitType Spawn(
            UnitKind kind,
            int prototypeId,
            fp maxHealth)
        {
            var stats = new StatPreset();
            stats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = maxHealth,
            });
            return UnitTestFactory.SpawnUnit(
                world,
                new UnitPrototype
                {
                    UnitPrototypeId = prototypeId,
                    Name = kind.ToString(),
                    RuntimeEntityPrefabId = 9000 + prototypeId,
                    UnitKind = kind,
                    BaseStats = stats,
                    Loadout = HandlerLoadout.DefaultHero,
                },
                kind == UnitKind.Structure
                    ? new TeamId(2)
                    : new TeamId(1),
                10,
                fp.zero,
                fp.zero);
        }
    }
}

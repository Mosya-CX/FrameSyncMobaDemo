using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Verifies the deterministic ability-stage aim contract: point-target
    /// area damage resolves from AbilitySession.Aim.TargetPoint and
    /// direction-target projectile spawn resolves from
    /// AbilitySession.Aim.Direction (Ability v15.2 sections 3.9 and 7.1).
    /// </summary>
    [TestFixture]
    public sealed class AbilityAimStageTests
    {
        private SimulationTickContextController controller;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
            controller.BeginTick(
                20,
                ExecutionMode.ServerAuthority);
        }

        [TearDown]
        public void TearDown()
        {
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void AreaDamageStage_CentersOnAimTargetPoint()
        {
            UnitWorld world = new UnitWorld();
            UnitPrototype prototype = CreatePrototype();
            UnitType caster = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                20,
                fp.zero,
                fp.zero);
            UnitType inside = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(2),
                20,
                fp.zero,
                fp.zero);
            UnitType outside = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(2),
                20,
                fp.zero,
                fp.zero);

            SetPose(inside, new fp2((fp)3, fp.zero));
            SetPose(outside, new fp2((fp)6, fp.zero));

            var combat = new CombatSystem(world, 0, 0);
            world.CombatSystem = combat;
            combat.BeginTick();
            world.RangeQuery =
                new RangeQueryService(world.PhysicsWorld);
            world.PhysicsWorld.BuildUnitFinalGrid();

            var model = new CommitCastModelDef
            {
                Cast = new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 1,
                    Def = new AreaDamageStageDef
                    {
                        Radius = (fp)1.5m,
                        BaseDamage = (fp)30,
                        DamageType = DamageType.Physical,
                        TargetFilter = UnitTargetFilter.Default,
                    },
                },
            };
            Install(
                world,
                caster,
                new AbilityDef
                {
                    AbilityId = 200,
                    Name = "TestAreaPoint",
                    CastModel = model,
                    AimKind = AimKind.Point,
                    CastRange = (fp)5,
                    CostPlan = default,
                    CooldownByLevel = default,
                });

            fp2 aimPoint = new fp2((fp)3, fp.zero);
            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(AimSnapshot.ForPoint(aimPoint))));
            combat.SettleActiveRequests();

            Assert.AreEqual(
                (fp)70,
                inside.StatHandler.CurrentHealth,
                "Unit at the aim point must take area damage.");
            Assert.AreEqual(
                (fp)100,
                outside.StatHandler.CurrentHealth,
                "Unit outside the aim radius must not take damage.");
        }

        [Test]
        public void AreaDamageStage_DoesNotDamageEnemyStructure()
        {
            UnitWorld world = new UnitWorld();
            UnitType caster = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(),
                new TeamId(1),
                20,
                fp.zero,
                fp.zero);
            UnitType structure = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(UnitKind.Structure, 2, 1002),
                new TeamId(2),
                20,
                fp.zero,
                fp.zero);
            SetPose(structure, new fp2((fp)3, fp.zero));

            var combat = new CombatSystem(world, 0, 0);
            world.CombatSystem = combat;
            combat.BeginTick();
            world.RangeQuery =
                new RangeQueryService(world.PhysicsWorld);
            world.PhysicsWorld.BuildUnitFinalGrid();

            Install(
                world,
                caster,
                new AbilityDef
                {
                    AbilityId = 202,
                    Name = "TestAreaStructure",
                    CastModel = new CommitCastModelDef
                    {
                        Cast = new CastStage
                        {
                            StageKey = 1,
                            DurationTicks = 1,
                            Def = new AreaDamageStageDef
                            {
                                Radius = (fp)1.5m,
                                BaseDamage = (fp)30,
                                DamageType = DamageType.Physical,
                                TargetFilter = new UnitTargetFilter
                                {
                                    TeamRule = TeamQueryRule.EnemyOnly,
                                    UnitKindMask = UnitKindMask.All,
                                    LifeStateMask =
                                        UnitLifeStateMask.AliveOnly,
                                    RequireTargetable = true,
                                },
                            },
                        },
                    },
                    AimKind = AimKind.Point,
                    CastRange = (fp)5,
                    CostPlan = default,
                    CooldownByLevel = default,
                });

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(AimSnapshot.ForPoint(
                    new fp2((fp)3, fp.zero)))));
            combat.SettleActiveRequests();

            Assert.AreEqual(
                (fp)100,
                structure.StatHandler.CurrentHealth);
        }

        [Test]
        public void SpawnProjectileStage_FiresTowardAimDirection()
        {
            UnitWorld world = new UnitWorld();
            UnitPrototype prototype = CreatePrototype();
            UnitType caster = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                20,
                fp.zero,
                fp.zero);
            UnitTestFactory.AddProjectilePrefab(world, 2001);

            var projectileWorld = new ProjectileWorld
            {
                UnitWorld = world,
                PhysicsWorld = world.PhysicsWorld,
                PrefabTable = world.GlobalPrefabTable,
                DefRegistry = new ProjectileDefRegistry(),
            };
            projectileWorld.DefRegistry.Register(
                new ProjectileDef
                {
                    DefId = 2001,
                    RuntimeEntityPrefabId = 2001,
                    Speed = (fp)1,
                    MaxLifetimeTicks = 10,
                    HitRadius = (fp)1 / (fp)10,
                    TargetFilter =
                        ProjectileTargetFilter.DefaultEnemy,
                    HitPolicy = new ProjectileHitPolicy
                    {
                        Enabled = false,
                    },
                });
            world.ProjectileWorld = projectileWorld;

            var model = new CommitCastModelDef
            {
                Cast = new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 1,
                    Def = new SpawnProjectileStageDef
                    {
                        ProjectileDefId = 2001,
                        SpawnOffsetDistance = (fp)1,
                    },
                },
            };
            Install(
                world,
                caster,
                new AbilityDef
                {
                    AbilityId = 201,
                    Name = "TestProjectileDirection",
                    CastModel = model,
                    AimKind = AimKind.Direction,
                    CastRange = (fp)5,
                    CostPlan = default,
                    CooldownByLevel = default,
                });

            // A non-unit direction must be normalized by the aim snapshot.
            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(AimSnapshot.ForDirection(
                    new fp2((fp)2, fp.zero)))));

            Assert.AreEqual(1, projectileWorld.PendingCount);
            projectileWorld.CommitSpawns();
            Assert.AreEqual(1, projectileWorld.Count);

            ProjectileUid uid =
                projectileWorld.GetAllOrdered()[0].Uid;
            Assert.IsTrue(projectileWorld.TryGet(
                uid,
                out ProjectileRuntime runtime));
            Assert.AreEqual(
                new fp2(fp.one, fp.zero),
                runtime.Velocity,
                "Projectile must fly along the normalized aim direction.");
            Assert.AreEqual(
                new fp2(fp.one, fp.zero),
                runtime.Position,
                "Projectile must spawn at caster + offset * direction.");
        }

        private static void Install(
            UnitWorld world,
            UnitType caster,
            AbilityDef definition)
        {
            var runtime = new AbilityRuntime
            {
                Definition = definition,
                Level = 1,
            };
            var slot = new AbilitySlotRuntime
            {
                SlotIndex = 0,
                ActiveAbilityId = definition.AbilityId,
                AllocatedPoints = 1,
            };
            slot.AddAbility(runtime);
            caster.AbilityHandler.AddSlot(slot);
        }

        private static AbilitySignal CommitSignal(
            AimSnapshot aim)
        {
            return new AbilitySignal
            {
                Slot = 0,
                Verb = AbilitySignalVerb.Commit,
                Aim = aim,
            };
        }

        private static void SetPose(
            UnitType unit,
            fp2 position)
        {
            unit.PhysicsEntity.SetLogicShape(
                PhysicsShape2D.CreateCircle(
                    fp2.zero,
                    (fp)1 / (fp)4));
            unit.PhysicsEntity.SetLogicPose(
                position,
                new fp2(fp.one, fp.zero));
        }

        private static UnitPrototype CreatePrototype(
            UnitKind kind = UnitKind.Hero,
            int prototypeId = 1,
            int prefabId = 1001)
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = (fp)100,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxCastResource,
                BaseValue = (fp)100,
            });
            return new UnitPrototype
            {
                UnitPrototypeId = prototypeId,
                RuntimeEntityPrefabId = prefabId,
                UnitKind = kind,
                BaseStats = preset,
                Loadout = HandlerLoadout.DefaultHero,
            };
        }
    }
}

using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using GameplayUnit = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync.Tests
{
    /// <summary>
    /// Verifies projectile piercing falloff end-to-end: each extra unit hit
    /// reduces the on-hit damage (15% per extra hit, 33% floor), including
    /// the per-instance on-hit damage override path used by charged abilities.
    /// </summary>
    [TestFixture]
    public sealed class ProjectileFalloffTests
    {
        private SimulationTickContextController controller;
        private UnitWorld unitWorld;
        private CombatSystem combat;
        private ProjectileWorld projectileWorld;
        private ProjectileHitResolver resolver;
        private GameplayUnit owner;
        private GameplayUnit firstTarget;
        private GameplayUnit secondTarget;
        private GameplayUnit thirdTarget;
        private GameplayUnit fourthTarget;
        private GameplayUnit fifthTarget;
        private GameplayUnit sixthTarget;
        private GameplayUnit seventhTarget;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
            controller.BeginTick(
                10,
                ExecutionMode.ServerAuthority);
            unitWorld = new UnitWorld();
            UnitPrototype prototype = CreatePrototype();
            owner = unitWorld.SpawnUnit(
                prototype,
                new TeamId(1),
                10,
                fp.zero,
                fp.zero);
            firstTarget = unitWorld.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            secondTarget = unitWorld.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            thirdTarget = unitWorld.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            fourthTarget = unitWorld.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            fifthTarget = unitWorld.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            sixthTarget = unitWorld.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            seventhTarget = unitWorld.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            UnitTestFactory.AddProjectilePrefab(
                unitWorld,
                2001);

            combat = new CombatSystem(unitWorld, 0, 0);
            unitWorld.CombatSystem = combat;
            combat.BeginTick();
            projectileWorld = new ProjectileWorld
            {
                UnitWorld = unitWorld,
                PhysicsWorld = unitWorld.PhysicsWorld,
                PrefabTable = unitWorld.GlobalPrefabTable,
                DefRegistry = new ProjectileDefRegistry(),
            };
            unitWorld.ProjectileWorld = projectileWorld;
            resolver = new ProjectileHitResolver(
                unitWorld.PhysicsWorld,
                unitWorld);

            SetTargetPose(firstTarget, (fp)1);
            SetTargetPose(secondTarget, (fp)2);
            SetTargetPose(thirdTarget, (fp)3);
            SetTargetPose(fourthTarget, (fp)4);
            SetTargetPose(fifthTarget, (fp)5);
            SetTargetPose(sixthTarget, (fp)6);
            SetTargetPose(seventhTarget, (fp)7);
            unitWorld.PhysicsWorld.BuildUnitFinalGrid();
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Clear();
            projectileWorld?.Dispose();
            if (controller.IsTickActive)
                controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void PiercingFalloff_ReducesDamagePerExtraHit()
        {
            RegisterDefinition(
                maxHits: 4,
                endOnFirst: false,
                pierceCount: 3,
                falloff: (fp)0.15m,
                minRatio: (fp)0.33m,
                useOverride: false);
            SpawnChargedProjectile();

            AdvanceThroughTargets();

            Assert.AreEqual(
                (double)((fp)1000 - (fp)100),
                (double)firstTarget.StatHandler.CurrentHealth,
                0.01);
            Assert.AreEqual(
                (double)((fp)1000 - (fp)85),
                (double)secondTarget.StatHandler.CurrentHealth,
                0.01);
            Assert.AreEqual(
                (double)((fp)1000 - (fp)70),
                (double)thirdTarget.StatHandler.CurrentHealth,
                0.01);
        }

        [Test]
        public void PiercingFalloff_OverridePath_MatchesStaticConfig()
        {
            RegisterDefinition(
                maxHits: 4,
                endOnFirst: false,
                pierceCount: 3,
                falloff: (fp)0.15m,
                minRatio: (fp)0.33m,
                useOverride: true);
            SpawnChargedProjectile();

            AdvanceThroughTargets();

            Assert.AreEqual(
                (double)((fp)1000 - (fp)100),
                (double)firstTarget.StatHandler.CurrentHealth,
                0.01);
            Assert.AreEqual(
                (double)((fp)1000 - (fp)85),
                (double)secondTarget.StatHandler.CurrentHealth,
                0.01);
            Assert.AreEqual(
                (double)((fp)1000 - (fp)70),
                (double)thirdTarget.StatHandler.CurrentHealth,
                0.01);
        }

        [Test]
        public void PiercingFalloff_ClampsAtMinDamageRatio()
        {
            RegisterDefinition(
                maxHits: 10,
                endOnFirst: false,
                pierceCount: 10,
                falloff: (fp)0.15m,
                minRatio: (fp)0.33m,
                useOverride: false);
            SpawnChargedProjectile();

            AdvanceThroughTargets();

            // 7th hit would be 1 - 6*0.15 = 0.10, clamped to 0.33.
            Assert.AreEqual(
                (double)((fp)1000 - (fp)33),
                (double)seventhTarget.StatHandler.CurrentHealth,
                0.01,
                "Falloff must never drop below the configured floor.");
        }

        private void RegisterDefinition(
            int maxHits,
            bool endOnFirst,
            int pierceCount,
            fp falloff,
            fp minRatio,
            bool useOverride)
        {
            var effects = new ProjectileOnHitEffects
            {
                DamageEffects = useOverride
                    ? null
                    : new[]
                    {
                        new ProjectileOnHitDamage
                        {
                            Amount = (fp)100,
                            DamageType = DamageType.Physical,
                            FalloffPerHitPercent = falloff,
                            MinDamageRatio = minRatio,
                            RecipeId = 1,
                        },
                    },
            };
            projectileWorld.DefRegistry.Register(
                new ProjectileDef
                {
                    DefId = 1,
                    RuntimeEntityPrefabId = 2001,
                    Speed = (fp)1,
                    MaxLifetimeTicks = 30,
                    HitRadius = (fp)1 / (fp)10,
                    TargetFilter =
                        ProjectileTargetFilter.DefaultEnemy,
                    HitPolicy = new ProjectileHitPolicy
                    {
                        Enabled = true,
                        QueryIntervalTicks = 1,
                        SameTargetPolicy =
                            HitSameTargetPolicy.Once,
                        MaxTotalHitCount = maxHits,
                        InitialPierceCount = pierceCount,
                        EndOnFirstValidHit = endOnFirst,
                        StopResolvingAfterEndRequested = true,
                    },
                    OnHitEffects = effects,
                });
        }

        private void SpawnChargedProjectile()
        {
            var source = new SourceDescriptor
            {
                SourceType = CombatSourceType.Ability,
                SourceId = 10011,
                OwnerUnitUid = owner.UnitUid,
                EmitterUnitUid = owner.UnitUid,
            };
            ProjectileOnHitDamage[] damageOverride =
                null;
            ProjectileDef def =
                projectileWorld.DefRegistry.FindById(1);
            if (def != null &&
                def.OnHitEffects.DamageEffects == null)
            {
                damageOverride = new[]
                {
                    new ProjectileOnHitDamage
                    {
                        Amount = (fp)100,
                        DamageType = DamageType.Physical,
                        FalloffPerHitPercent =
                            (fp)0.15m,
                        MinDamageRatio = (fp)0.33m,
                        RecipeId = 1,
                    },
                };
            }
            ProjectileUid uid =
                projectileWorld.RequestSpawn(
                    new ProjectileSpawnRequest(
                        1,
                        owner.UnitUid,
                        owner.TeamId,
                        source,
                        new OriginActionId(
                            owner.GameplayParticipantId,
                            source.SourceType,
                            source.SourceId,
                            10,
                            0),
                        fp2.zero,
                        new fp2(fp.one, fp.zero),
                        damageOverride,
                        30));
            Assert.IsTrue(uid.IsValid);
            projectileWorld.CommitSpawns();
        }

        private void AdvanceThroughTargets()
        {
            for (int tick = 11;
                 tick <= 20;
                 tick++)
            {
                controller.EndTick();
                controller.BeginTick(
                    tick,
                    ExecutionMode.ServerAuthority);
                combat.BeginTick();
                projectileWorld.AdvanceMotion();
                projectileWorld.UpdateLifecycle();
                resolver.ResolveAllHits(projectileWorld);
                resolver.EmitEffects(projectileWorld);
                projectileWorld.FlushDestroy();
                combat.SettleActiveRequests();
                combat.EndTick();
            }
        }

        private static void SetTargetPose(
            GameplayUnit unit,
            fp x)
        {
            unit.PhysicsEntity.SetLogicShape(
                FrameSyncMoba.Physics.PhysicsShape2D
                    .CreateCircle(
                    fp2.zero,
                    (fp)1 / (fp)4));
            unit.PhysicsEntity.SetLogicPose(
                new fp2(x, fp.zero),
                new fp2(fp.one, fp.zero));
        }

        private static UnitPrototype CreatePrototype()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = (fp)1000,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxCastResource,
                BaseValue = (fp)500,
            });
            return new UnitPrototype
            {
                UnitPrototypeId = 1,
                RuntimeEntityPrefabId = 1001,
                UnitKind = UnitKind.Hero,
                BaseStats = preset,
                Loadout = HandlerLoadout.DefaultHero,
            };
        }
    }
}

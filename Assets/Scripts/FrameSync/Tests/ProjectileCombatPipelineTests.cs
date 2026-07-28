using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using GameplayUnit = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class ProjectileCombatPipelineTests
    {
        private SimulationTickContextController controller;
        private UnitWorld unitWorld;
        private PhysicsWorld physicsWorld;
        private CombatSystem combat;
        private ProjectileWorld projectileWorld;
        private ProjectileHitResolver resolver;
        private GameplayUnit owner;
        private GameplayUnit firstTarget;
        private GameplayUnit secondTarget;

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
            UnitTestFactory.AddProjectilePrefab(
                unitWorld,
                2001);

            physicsWorld = unitWorld.PhysicsWorld;
            combat = new CombatSystem(unitWorld, 0, 0);
            unitWorld.CombatSystem = combat;
            combat.BeginTick();
            projectileWorld = new ProjectileWorld
            {
                UnitWorld = unitWorld,
                PhysicsWorld = physicsWorld,
                PrefabTable = unitWorld.GlobalPrefabTable,
                DefRegistry = new ProjectileDefRegistry(),
            };
            unitWorld.ProjectileWorld = projectileWorld;
            resolver = new ProjectileHitResolver(
                physicsWorld,
                unitWorld);

            SetTargetPose(firstTarget, (fp)1);
            SetTargetPose(secondTarget, (fp)1);
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Clear();
            projectileWorld?.Dispose();
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void EqualDistanceHits_AreUidOrderedAndUseCombat()
        {
            RegisterDefinition(
                maxHits: 2,
                endOnFirst: false);
            SpawnAndAdvance();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);

            Assert.AreEqual(2, resolver.PendingHits.Count);
            Assert.Less(
                resolver.PendingHits[0].TargetUnitUid.CompareTo(
                    resolver.PendingHits[1].TargetUnitUid),
                0);

            int onHitCount = 0;
            CombatEvents.OnHitDealt += _ => onHitCount++;
            resolver.EmitEffects(projectileWorld);
            projectileWorld.FlushDestroy();
            combat.SettleActiveRequests();

            Assert.AreEqual(2, combat.DamageProcessed);
            Assert.AreEqual(2, onHitCount);
            Assert.AreEqual(0, projectileWorld.Count);
        }

        [Test]
        public void EndOnFirstHit_RejectsLaterSameTickCandidate()
        {
            RegisterDefinition(
                maxHits: 4,
                endOnFirst: true);
            SpawnAndAdvance();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);
            Assert.AreEqual(2, resolver.PendingHits.Count);
            resolver.EmitEffects(projectileWorld);
            projectileWorld.FlushDestroy();
            combat.SettleActiveRequests();

            Assert.AreEqual(1, combat.DamageProcessed);
            Assert.AreEqual(0, projectileWorld.Count);
        }

        [Test]
        public void PierceBudget_EndsAtConfiguredHitCount()
        {
            RegisterDefinition(
                maxHits: 4,
                endOnFirst: false,
                pierceCount: 1);
            SpawnAndAdvance();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);
            resolver.EmitEffects(projectileWorld);
            projectileWorld.FlushDestroy();
            combat.SettleActiveRequests();

            Assert.AreEqual(1, combat.DamageProcessed);
            Assert.AreEqual(0, projectileWorld.Count);
        }

        [Test]
        public void ProjectileDamage_UsesCombatShieldPipeline()
        {
            firstTarget.StatHandler.AddShield(
                ShieldType.White,
                (fp)10,
                0,
                owner.UnitUid);
            RegisterDefinition(
                maxHits: 1,
                endOnFirst: false);
            SpawnAndAdvance();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);
            resolver.EmitEffects(projectileWorld);
            projectileWorld.FlushDestroy();
            combat.SettleActiveRequests();

            Assert.AreEqual(fp.zero, firstTarget.StatHandler.CurrentShield);
            Assert.AreEqual((fp)85, firstTarget.StatHandler.CurrentHealth);
        }

        [Test]
        public void EnemyFilter_ExcludesFriendlyTarget()
        {
            GameplayUnit friendly = unitWorld.SpawnUnit(
                CreatePrototype(),
                new TeamId(1),
                10,
                fp.zero,
                fp.zero);
            UnitTestFactory.AddProjectilePrefab(
                unitWorld,
                2001);
            SetTargetPose(friendly, (fp)1);
            RegisterDefinition(
                maxHits: 4,
                endOnFirst: false);
            SpawnAndAdvance();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);

            Assert.AreEqual(2, resolver.PendingHits.Count);
            for (int i = 0; i < resolver.PendingHits.Count; i++)
                Assert.AreNotEqual(
                    friendly.UnitUid,
                    resolver.PendingHits[i].TargetUnitUid);
        }

        [Test]
        public void SnapshotRoundTrip_PreservesSourceAndHitMemory()
        {
            RegisterDefinition(
                maxHits: 2,
                endOnFirst: false);
            SpawnAndAdvance();
            Assert.IsTrue(projectileWorld.TryGet(
                projectileWorld.GetAllOrdered()[0].Uid,
                out ProjectileRuntime runtime));
            Assert.IsTrue(runtime.RegisterHit(
                firstTarget.UnitUid,
                10));

            ProjectileWorldSnapshot captured =
                ProjectileWorldSnapshot.Empty;
            projectileWorld.Capture(ref captured);
            projectileWorld.Restore(captured);
            projectileWorld.Resolve(unitWorld);
            projectileWorld.Rebuild(default);
            ProjectileWorldSnapshot roundTrip =
                ProjectileWorldSnapshot.Empty;
            projectileWorld.Capture(ref roundTrip);

            Assert.AreEqual(1, roundTrip.ActiveProjectiles.Length);
            Assert.AreEqual(
                CombatSourceType.Attack,
                roundTrip.ActiveProjectiles[0]
                    .Source.SourceType);
            Assert.AreEqual(
                firstTarget.UnitUid,
                roundTrip.ActiveProjectiles[0]
                    .HitRecords[0].TargetUid);
            Assert.AreEqual(
                captured.ActiveProjectiles[0].Position,
                roundTrip.ActiveProjectiles[0].Position);
        }

        private void RegisterDefinition(
            int maxHits,
            bool endOnFirst,
            int pierceCount = 0)
        {
            projectileWorld.DefRegistry.Register(
                new ProjectileDef
                {
                    DefId = 1,
                    RuntimeEntityPrefabId = 2001,
                    Speed = (fp)1,
                    MaxLifetimeTicks = 10,
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
                    OnHitEffects =
                        new ProjectileOnHitEffects
                        {
                            DamageEffects =
                                new[]
                                {
                                    new ProjectileOnHitDamage
                                    {
                                        Amount = (fp)25,
                                        DamageType =
                                            DamageType.Physical,
                                        RecipeId = 1,
                                    },
                                },
                        },
                });
        }

        private void SpawnAndAdvance()
        {
            var source = new SourceDescriptor
            {
                SourceType = CombatSourceType.Attack,
                SourceId =
                    CombatBuiltinSourceId.BasicAttack,
                OwnerUnitUid = owner.UnitUid,
                EmitterUnitUid = owner.UnitUid,
            };
            ProjectileUid uid =
                projectileWorld.RequestSpawn(
                    new ProjectileSpawnRequest(
                        1,
                        owner.UnitUid,
                        owner.TeamId,
                        source,
                        fp2.zero,
                        new fp2(fp.one, fp.zero)));
            Assert.IsTrue(uid.IsValid);
            projectileWorld.CommitSpawns();
            projectileWorld.AdvanceMotion();
            projectileWorld.UpdateLifecycle();
        }

        private static void SetTargetPose(
            GameplayUnit unit,
            fp x)
        {
            unit.PhysicsEntity.SetLogicShape(
                PhysicsShape2D.CreateCircle(
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
                BaseValue = (fp)100,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.Armor,
                BaseValue = fp.zero,
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

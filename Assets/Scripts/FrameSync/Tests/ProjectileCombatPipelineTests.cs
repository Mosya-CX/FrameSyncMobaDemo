using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
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
        private CrowdControlDefinition testCrowdControlDefinition;

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
            if (testCrowdControlDefinition != null)
                UnityEngine.Object.DestroyImmediate(
                    testCrowdControlDefinition);
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat()
        {
            RegisterDefinition(
                maxHits: 2,
                endOnFirst: false);
            SpawnAndAdvance();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);

            Assert.AreEqual(2, resolver.PendingHits.Count);
            Assert.Less(
                resolver.PendingHits[0].EqualDistanceTieScore,
                resolver.PendingHits[1].EqualDistanceTieScore);

            int onHitCount = 0;
            int observedParentEffectOrdinal = -1;
            CombatEvents.OnHitDealt += data =>
            {
                onHitCount++;
                observedParentEffectOrdinal = data.EffectOrdinal;
            };
            resolver.EmitEffects(projectileWorld);
            projectileWorld.FlushDestroy();
            combat.SettleActiveRequests();

            Assert.AreEqual(2, combat.DamageProcessed);
            Assert.AreEqual(2, onHitCount);
            Assert.AreEqual(
                CombatFairnessKey.ComposeEffectOrdinal(1, 0),
                observedParentEffectOrdinal);
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
            UnitUid expectedWinner =
                resolver.PendingHits[0].TargetUnitUid;
            resolver.EmitEffects(projectileWorld);
            projectileWorld.FlushDestroy();
            combat.SettleActiveRequests();

            Assert.AreEqual(1, combat.DamageProcessed);
            Assert.AreEqual(
                (fp)75,
                expectedWinner == firstTarget.UnitUid
                    ? firstTarget.StatHandler.CurrentHealth
                    : secondTarget.StatHandler.CurrentHealth);
            Assert.AreEqual(0, projectileWorld.Count);
        }

        [Test]
        public void EqualDistanceArbitration_UidRelabelKeepsParticipantWinner()
        {
            var action = new OriginActionId(
                GameplayParticipantId.Explicit(10),
                CombatSourceType.Ability,
                1001,
                20,
                3);
            GameplayParticipantId firstParticipant =
                GameplayParticipantId.Explicit(20);
            GameplayParticipantId secondParticipant =
                GameplayParticipantId.Explicit(30);
            ulong firstScore = CombatFairnessKey.ProjectileTieScore(
                991u,
                action,
                firstParticipant);
            ulong secondScore = CombatFairnessKey.ProjectileTieScore(
                991u,
                action,
                secondParticipant);
            Assert.AreNotEqual(firstScore, secondScore);

            ProjectileHitResult[] original =
            {
                CreateEqualDistanceHit(
                    new UnitUid(20, 1001, 0),
                    firstParticipant,
                    firstScore),
                CreateEqualDistanceHit(
                    new UnitUid(20, 1001, 1),
                    secondParticipant,
                    secondScore),
            };
            ProjectileHitResult[] relabeled =
            {
                CreateEqualDistanceHit(
                    new UnitUid(20, 9001, 1),
                    firstParticipant,
                    firstScore),
                CreateEqualDistanceHit(
                    new UnitUid(20, 9001, 0),
                    secondParticipant,
                    secondScore),
            };

            Array.Sort(
                original,
                ProjectileHitResolver.CompareHitResults);
            Array.Sort(
                relabeled,
                ProjectileHitResolver.CompareHitResults);

            Assert.AreEqual(
                original[0].TargetParticipantId,
                relabeled[0].TargetParticipantId);
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
        public void EnemyFilter_IncludesStructureTarget()
        {
            GameplayUnit structure = unitWorld.SpawnUnit(
                CreatePrototype(UnitKind.Structure, 2, 1002),
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            UnitTestFactory.AddProjectilePrefab(
                unitWorld,
                2001);
            SetTargetPose(structure, (fp)1);
            RegisterDefinition(
                maxHits: 4,
                endOnFirst: false);
            SpawnAndAdvance();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);

            bool found = false;
            for (int i = 0; i < resolver.PendingHits.Count; i++)
            {
                if (resolver.PendingHits[i].TargetUnitUid ==
                    structure.UnitUid)
                {
                    found = true;
                    break;
                }
            }
            Assert.That(found, Is.True);
        }

        [Test]
        public void MisconfiguredAbilityProjectile_StructureHitIsConsumedNoOp()
        {
            unitWorld.CrowdControlDefinitions =
                new CrowdControlDefinitionRegistry();
            testCrowdControlDefinition =
                UnityEngine.ScriptableObject.CreateInstance<
                    CrowdControlDefinition>();
            testCrowdControlDefinition.Configure(
                CrowdControlIds.Stun,
                CrowdControlIntensity.Low,
                CrowdControlDefinition.ControlTagBits.Control,
                CrowdControlDurationRule.DefaultTenacity,
                Array.Empty<CrowdControlParamAuthoring>(),
                Array.Empty<CrowdControlModuleAuthoring>());
            unitWorld.CrowdControlDefinitions.Register(
                testCrowdControlDefinition);
            GameplayUnit structure = unitWorld.SpawnUnit(
                CreatePrototype(UnitKind.Structure, 2, 1002),
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            UnitTestFactory.AddProjectilePrefab(
                unitWorld,
                2001);
            SetTargetPose(structure, (fp)1);
            RegisterDefinition(
                maxHits: 4,
                endOnFirst: false,
                includeCrowdControl: true);
            SpawnAndAdvance(
                CombatSourceType.Ability,
                10011);
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);
            Assert.DoesNotThrow(
                () => resolver.EmitEffects(projectileWorld));
            combat.SettleActiveRequests();

            Assert.AreEqual(
                (fp)100,
                structure.StatHandler.CurrentHealth);
            Assert.AreEqual(0, structure.CrowdControl.Count);
        }

        [Test]
        public void MisconfiguredProjectileBuff_StructureHitStillFailsVisibly()
        {
            unitWorld.BuffDefinitions =
                new BuffDefinitionRegistry();
            GameplayUnit structure = unitWorld.SpawnUnit(
                CreatePrototype(UnitKind.Structure, 2, 1002),
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            UnitTestFactory.AddProjectilePrefab(
                unitWorld,
                2001);
            RegisterDefinition(
                maxHits: 1,
                endOnFirst: true,
                buffEffects: new[]
                {
                    new ProjectileOnHitBuff
                    {
                        BuffId = new BuffConfigId(9909),
                        DurationTicks = 10,
                        TargetKinds = UnitKindMask.All,
                    },
                });
            SpawnAndAdvance(
                CombatSourceType.Ability,
                10011);
            ProjectileRuntime projectile =
                projectileWorld.GetAllOrdered()[0];

            Assert.Throws<DeterministicSimulationException>(
                () => ProjectileEffectDispatcher.DispatchOnHit(
                    projectile,
                    structure.UnitUid,
                    unitWorld));
        }

        [Test]
        public void MisconfiguredProjectileCC_StructureHitStillFailsVisibly()
        {
            GameplayUnit structure = unitWorld.SpawnUnit(
                CreatePrototype(UnitKind.Structure, 2, 1002),
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            UnitTestFactory.AddProjectilePrefab(
                unitWorld,
                2001);
            RegisterDefinition(
                maxHits: 1,
                endOnFirst: true,
                includeCrowdControl: true);
            SpawnAndAdvance(
                CombatSourceType.Ability,
                10011);
            ProjectileRuntime projectile =
                projectileWorld.GetAllOrdered()[0];

            Assert.Throws<DeterministicSimulationException>(
                () => ProjectileEffectDispatcher.DispatchOnHit(
                    projectile,
                    structure.UnitUid,
                    unitWorld));
        }

        [Test]
        public void
            RestrictToTrackedTarget_IgnoresUnitsBetweenProjectileAndTarget()
        {
            // Both targets sit on the +x flight path. The projectile is
            // locked to secondTarget; firstTarget stands between them and
            // must NOT be hit even though the swept path overlaps it.
            SetTargetPose(firstTarget, (fp)1);
            SetTargetPose(secondTarget, (fp)2);
            RegisterDefinition(
                maxHits: 1,
                endOnFirst: true,
                restrictToTracked: true);
            SpawnAndAdvance(
                lockSecondTarget: true);
            projectileWorld.AdvanceMotion();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);

            Assert.AreEqual(
                1,
                resolver.PendingHits.Count);
            Assert.AreEqual(
                secondTarget.UnitUid,
                resolver.PendingHits[0]
                    .TargetUnitUid);

            resolver.EmitEffects(projectileWorld);
            projectileWorld.FlushDestroy();
            combat.SettleActiveRequests();

            Assert.AreEqual(
                (fp)100,
                firstTarget.StatHandler
                    .CurrentHealth);
            Assert.AreEqual(
                (fp)75,
                secondTarget.StatHandler
                    .CurrentHealth);
        }

        [Test]
        public void PendingTrackedTarget_SnapshotRoundTripPreservesLock()
        {
            RegisterDefinition(
                maxHits: 1,
                endOnFirst: true,
                restrictToTracked: true);
            var source = new SourceDescriptor
            {
                SourceType = CombatSourceType.Attack,
                SourceId = CombatBuiltinSourceId.BasicAttack,
                OwnerUnitUid = owner.UnitUid,
                EmitterUnitUid = owner.UnitUid,
            };
            ProjectileUid uid = projectileWorld.RequestSpawn(
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
                        1),
                    fp2.zero,
                    new fp2(fp.one, fp.zero),
                    targetUnitUid: secondTarget.UnitUid));

            ProjectileWorldSnapshot captured =
                ProjectileWorldSnapshot.Empty;
            projectileWorld.Capture(ref captured);
            projectileWorld.Restore(captured);
            ProjectileWorldSnapshot roundTrip =
                ProjectileWorldSnapshot.Empty;
            projectileWorld.Capture(ref roundTrip);

            Assert.AreEqual(
                secondTarget.UnitUid,
                roundTrip.PendingSpawns[0].TargetUnitUid);
            projectileWorld.CommitSpawns();
            Assert.IsTrue(
                projectileWorld.TryGet(
                    uid,
                    out ProjectileRuntime runtime));
            Assert.AreEqual(
                secondTarget.UnitUid,
                runtime.TargetUnitUid);
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
                captured.ActiveProjectiles[0]
                    .OriginActionId,
                roundTrip.ActiveProjectiles[0]
                    .OriginActionId);
            Assert.AreEqual(
                firstTarget.UnitUid,
                roundTrip.ActiveProjectiles[0]
                    .HitRecords[0].TargetUid);
            Assert.AreEqual(
                captured.ActiveProjectiles[0].Position,
                roundTrip.ActiveProjectiles[0].Position);
        }

        [Test]
        public void
            Capture_PrunesHitMemoryForDisposedTargets()
        {
            // A projectile may still be alive after a unit it already hit
            // has been disposed (death despawn). The snapshot must prune such
            // hit-memory entries; otherwise ValidateUnitReferences throws on
            // the next rollback through this tick.
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

            unitWorld.DespawnUnit(
                new UnitDespawnRequest(
                    firstTarget.UnitUid,
                    UnitDespawnReason
                        .ScriptedCleanup,
                    UnitDespawnMode.Destroy));

            ProjectileWorldSnapshot captured =
                ProjectileWorldSnapshot.Empty;
            projectileWorld.Capture(ref captured);

            Assert.AreEqual(
                1,
                captured.ActiveProjectiles.Length);
            Assert.AreEqual(
                0,
                captured.ActiveProjectiles[0]
                    .HitRecords.Length);

            projectileWorld.Restore(captured);
            Assert.DoesNotThrow(
                () => projectileWorld.Resolve(
                    unitWorld));
        }

        [Test]
        public void Restore_PreservesLogicSecondsPerTick_SoMotionKeepsAuthoredSpeed()
        {
            RegisterDefinition(
                maxHits: 2,
                endOnFirst: false);
            // The runtime ctor defaults LogicSecondsPerTick to 1; the world
            // configures 1/TickRate. A restored projectile must keep the
            // world value or it flies TickRate x too fast after a rollback.
            projectileWorld.LogicSecondsPerTick =
                (fp)1 / (fp)30;
            SpawnAndAdvance();
            fp afterFirst =
                projectileWorld.GetAllOrdered()[0]
                    .Position.x;

            ProjectileWorldSnapshot captured =
                ProjectileWorldSnapshot.Empty;
            projectileWorld.Capture(ref captured);
            projectileWorld.Restore(captured);
            projectileWorld.Resolve(unitWorld);
            projectileWorld.Rebuild(default);
            projectileWorld.AdvanceMotion();
            fp afterRestore =
                projectileWorld.GetAllOrdered()[0]
                    .Position.x;

            fp deltaAfterRestore =
                afterRestore - afterFirst;
            fp deltaBefore =
                afterFirst;
            Assert.That(
                (double)deltaAfterRestore,
                Is.EqualTo((double)deltaBefore)
                    .Within(0.0001));
            Assert.That(
                (double)deltaAfterRestore,
                Is.LessThan((double)0.1),
                "Restored projectile must advance by " +
                "Speed * (1/TickRate), not Speed per Tick.");
        }

        [Test]
        public void InfernalChainsHit_SpawnsStationaryContainmentProjectile()
        {
            BuffDefinition tether =
                AssetDatabase.LoadAssetAtPath<BuffDefinition>(
                    "Assets/Config/Formal/Buffs/Aatrox/AatroxWTether.asset");
            Assert.That(tether, Is.Not.Null);
            unitWorld.BuffDefinitions = new BuffDefinitionRegistry();
            unitWorld.BuffDefinitions.Register(tether);

            projectileWorld.DefRegistry.Register(
                new ProjectileDef
                {
                    DefId = 110,
                    RuntimeEntityPrefabId = 2001,
                    Speed = fp.zero,
                    MaxLifetimeTicks = 45,
                    HitRadius = fp.zero,
                    TargetFilter =
                        ProjectileTargetFilter.DefaultEnemy,
                    HitPolicy =
                        new ProjectileHitPolicy { Enabled = false },
                    ContainmentZone =
                        new ProjectileContainmentZone(
                            (fp)(-1.5f),
                            (fp)6f,
                            fp.one,
                            (fp)3f),
                });
            projectileWorld.DefRegistry.Register(
                new ProjectileDef
                {
                    DefId = 109,
                    RuntimeEntityPrefabId = 2001,
                    Speed = (fp)1,
                    MaxLifetimeTicks = 10,
                    HitRadius = (fp)0.1f,
                    TargetFilter =
                        ProjectileTargetFilter.DefaultEnemy,
                    HitPolicy =
                        ProjectileHitPolicy.DefaultSingleHit,
                    OnHitEffects =
                        new ProjectileOnHitEffects
                        {
                            BuffEffects =
                                new[]
                                {
                                    new ProjectileOnHitBuff
                                    {
                                        BuffId =
                                            new BuffConfigId(12022),
                                        DurationTicks = 45,
                                        TargetKinds =
                                            UnitKindMask.Hero,
                                    },
                                },
                        },
                });

            ProjectileUid missile = projectileWorld.RequestSpawn(
                new ProjectileSpawnRequest(
                    109,
                    owner.UnitUid,
                    owner.TeamId,
                    new SourceDescriptor
                    {
                        SourceType = CombatSourceType.Ability,
                        SourceId = 10022,
                        OwnerUnitUid = owner.UnitUid,
                        EmitterUnitUid = owner.UnitUid,
                    },
                    new OriginActionId(
                        owner.GameplayParticipantId,
                        CombatSourceType.Ability,
                        10022,
                        10,
                        0),
                    fp2.zero,
                    new fp2(fp.one, fp.zero)));
            Assert.That(missile.IsValid, Is.True);
            projectileWorld.CommitSpawns();
            projectileWorld.AdvanceMotion();
            projectileWorld.UpdateLifecycle();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);
            Assert.That(resolver.PendingHits, Is.Not.Empty);
            GameplayUnit impactedTarget =
                resolver.PendingHits[0].TargetUnitUid ==
                    firstTarget.UnitUid
                    ? firstTarget
                    : secondTarget;
            resolver.EmitEffects(projectileWorld);

            Assert.That(
                impactedTarget.BuffHandler.HasBuff(
                    new BuffConfigId(12022)),
                Is.True);
            Assert.That(
                projectileWorld.PendingCount,
                Is.EqualTo(1),
                "The W hit must queue its stationary containment projectile.");
            ProjectileWorldSnapshot snapshot =
                ProjectileWorldSnapshot.Empty;
            projectileWorld.Capture(ref snapshot);
            Assert.That(snapshot.PendingSpawns, Has.Length.EqualTo(1));
            Assert.That(snapshot.PendingSpawns[0].DefId, Is.EqualTo(110));

            SetTargetPose(impactedTarget, (fp)20);
            impactedTarget.BuffHandler.Advance();
            Assert.That(
                impactedTarget.BuffHandler.HasBuff(
                    new BuffConfigId(12022)),
                Is.False,
                "Leaving the authored zone must remove the tether.");
            Assert.That(
                projectileWorld.PendingCount,
                Is.Zero,
                "Removing the tether must cancel its area projectile even before spawn commit.");
        }

        private void RegisterDefinition(
            int maxHits,
            bool endOnFirst,
            int pierceCount = 0,
            bool restrictToTracked = false,
            bool includeCrowdControl = false,
            ProjectileOnHitBuff[] buffEffects = null)
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
                        RestrictToTrackedTarget =
                            restrictToTracked,
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
                            CCEffects = includeCrowdControl
                                ? new[]
                                {
                                    new ProjectileOnHitCC
                                    {
                                        ControlId =
                                            CrowdControlIds.Stun,
                                        DurationTicks = 10,
                                    },
                                }
                                : Array.Empty<ProjectileOnHitCC>(),
                            BuffEffects = buffEffects ??
                                Array.Empty<ProjectileOnHitBuff>(),
                        },
                });
        }

        private void SpawnAndAdvance(
            bool lockSecondTarget = false) =>
            SpawnAndAdvance(
                CombatSourceType.Attack,
                CombatBuiltinSourceId.BasicAttack,
                lockSecondTarget);

        private void SpawnAndAdvance(
            CombatSourceType sourceType,
            int sourceId,
            bool lockSecondTarget = false)
        {
            var source = new SourceDescriptor
            {
                SourceType = sourceType,
                SourceId = sourceId,
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
                    new OriginActionId(
                        owner.GameplayParticipantId,
                        source.SourceType,
                        source.SourceId,
                        10,
                        0),
                    fp2.zero,
                        new fp2(fp.one, fp.zero),
                        targetUnitUid:
                            lockSecondTarget
                                ? secondTarget.UnitUid
                                : default));
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

        private static ProjectileHitResult CreateEqualDistanceHit(
            UnitUid unitUid,
            GameplayParticipantId participantId,
            ulong score) =>
            new ProjectileHitResult
            {
                TargetUnitUid = unitUid,
                TargetParticipantId = participantId,
                HitDistance = fp.one,
                EqualDistanceTieScore = score,
            };

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
                StatId = StatId.Armor,
                BaseValue = fp.zero,
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

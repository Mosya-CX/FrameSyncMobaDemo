using System;
using System.Collections.Generic;
using System.Linq;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class AatroxFormalContentTests
    {
        private const string Root = "Assets/Config/Formal/";

        [Test]
        public void CombinedAbilityCatalog_BakesAatroxAndVarus()
        {
            AbilityRuntimeCatalogAsset catalog = Load<AbilityRuntimeCatalogAsset>(
                Root + "Abilities/FormalHeroAbilityRuntimeCatalog.asset");
            AbilityDefinitionRegistry registry = catalog.BakeOrThrow();

            for (int id = 10021; id <= 10024; id++)
                Assert.That(registry.TryGet(id, out _), Is.True, $"Ability {id}");
            Assert.That(registry.TryGetPassive(10020, out _), Is.True);
            Assert.That(registry.TryGet(10011, out _), Is.True, "Varus Q remains registered");
            Assert.That(registry.TryGetSlot(0, out AbilitySlotDef qSlot), Is.True);
            Assert.That(qSlot.AbilityIds, Does.Contain(10011));
            Assert.That(qSlot.AbilityIds, Does.Contain(10021));
        }

        [Test]
        public void DarkinBlade_UsesExactThreeZonesAndRecastDelay()
        {
            AbilityAsset asset = Load<AbilityAsset>(
                Root + "Abilities/Aatrox/AatroxQ.asset");
            AbilityDef baked = asset.Bake();
            var model = baked.CastModel as SequentialRecastCastModelDef;
            Assert.That(model, Is.Not.Null);
            var commit = new AbilitySignal { Verb = AbilitySignalVerb.Commit };
            Assert.That(model.CanHandleSignal(
                commit, model.FirstRecastWindow.StageKey, 29), Is.False);
            Assert.That(model.CanHandleSignal(
                commit, model.FirstRecastWindow.StageKey, 30), Is.True);
            Assert.That(model.FirstRecastWindow.DurationTicks, Is.EqualTo(120));
            Assert.That(model.FirstImpact.DurationTicks, Is.EqualTo(30));
            Assert.That(model.SecondImpact.DurationTicks, Is.EqualTo(30));
            Assert.That(model.FinalImpact.DurationTicks, Is.EqualTo(30));
            Assert.That(model.SecondImpact.IconAddressOverride, Is.Not.Empty);
            Assert.That(model.FinalImpact.IconAddressOverride, Is.Not.Empty);
            Assert.That(model.FirstRecastWindow.IconAddressOverride,
                Is.EqualTo(model.SecondImpact.IconAddressOverride),
                "After Q1, the current recast-window Stage must show Q2.");
            Assert.That(model.SecondRecastWindow.IconAddressOverride,
                Is.EqualTo(model.FinalImpact.IconAddressOverride),
                "After Q2, the current recast-window Stage must show Q3.");

            DirectionalMultiZoneDamageStageDefAuthoring[] zones = asset.Stages
                .OfType<DirectionalMultiZoneDamageStageDefAuthoring>()
                .ToArray();
            Assert.That(zones, Has.Length.EqualTo(3));
            Assert.That(zones[0].Shape, Is.EqualTo(DirectionalZoneShape.Rectangle));
            Assert.That(zones[0].ForwardLength, Is.EqualTo(5.5f).Within(.001f));
            Assert.That(zones[0].SweetForwardStart, Is.EqualTo(3.75f).Within(.001f));
            Assert.That(zones[1].Shape, Is.EqualTo(DirectionalZoneShape.Trapezoid));
            Assert.That(zones[1].ForwardStart, Is.EqualTo(-1f).Within(.001f));
            Assert.That(zones[2].Shape, Is.EqualTo(DirectionalZoneShape.OffsetCircle));
            Assert.That(zones[2].CircleRadius, Is.EqualTo(3f).Within(.001f));
            Assert.That(zones[2].SweetCircleRadius, Is.EqualTo(1.8f).Within(.001f));
            Assert.That(zones.Select(item =>
                    ((DirectionalMultiZoneDamageStageDef)item.Bake())
                        .ImpactDelayTicks),
                Is.All.EqualTo(30));
            DirectionalMultiZoneDamageStageDef q1 =
                zones[0].Bake() as DirectionalMultiZoneDamageStageDef;
            Assert.That(q1.BaseDamageByAbilityLevel.Resolve(1),
                Is.EqualTo((fp)10));
            Assert.That(q1.AttackDamageRatioByAbilityLevel.Resolve(1),
                Is.EqualTo((fp).6f));
            Assert.That(q1.TargetFilter.UnitKindMask.Contains(UnitKind.Structure), Is.False);

            AbilityAsset w = Load<AbilityAsset>(
                Root + "Abilities/Aatrox/AatroxW.asset");
            CommitCastModelDef wCastModel =
                w.Bake().CastModel as CommitCastModelDef;
            Assert.That(wCastModel, Is.Not.Null);
            Assert.That(
                wCastModel.Cast.DurationTicks,
                Is.EqualTo(14));
            var wStage = (ScaledProjectileStageDef)
                ((ScaledProjectileStageDefAuthoring)w.Stages[0]).Bake();
            Assert.That(wStage.SpawnDelayTicks, Is.EqualTo(14));
            Assert.That(wStage.BaseDamageByLevel.Resolve(1),
                Is.EqualTo((fp)30));

            AbilityAsset e = Load<AbilityAsset>(
                Root + "Abilities/Aatrox/AatroxE.asset");
            var ePassive = (AbilityRankStatModifierPassiveEffectDef)
                e.PassiveEffect.Bake();
            Assert.That(ePassive.ValueByAbilityLevel.Resolve(1),
                Is.EqualTo((fp).2f));
        }

        [Test]
        public void DarkinBlade_VfxDurationUsesConfiguredGameplayTickRate()
        {
            VisualEventOutput.Clear();
            UnitWorld world = new UnitWorld();
            UnitType caster = UnitTestFactory.SpawnUnit(
                world,
                CreateTestPrototype(),
                new TeamId(1),
                50,
                fp.zero,
                fp.zero);
            // UnitTestFactory supplies its canonical 30 Hz fixture defaults;
            // override the runtime rate after fixture setup for this
            // variable-Tick presentation assertion.
            world.TickRate = 50;
            var runtime = new AbilityRuntime
            {
                Definition = new AbilityDef { AbilityId = 10021 },
                Level = 1,
                World = world,
                CasterUnitUid = caster.UnitUid,
            };
            var session = new AbilitySession
            {
                Runtime = runtime,
                Aim = AimSnapshot.ForDirection(
                    new fp2(fp.one, fp.zero)),
            };
            var stage = new DirectionalMultiZoneDamageStageDef
            {
                StageDefId = 1,
                VfxDefId = 2101,
                ImpactDelayTicks = 50,
            };

            try
            {
                Assert.That(
                    stage.OnEnter(session, runtime),
                    Is.EqualTo(StageResult.Running));
                IReadOnlyList<VfxEvent> events =
                    VisualEventOutput.ConsumeVfxEvents();
                Assert.That(events.Count, Is.EqualTo(1));
                Assert.That(
                    events[0].DurationScale,
                    Is.EqualTo(fp.one),
                    "A one-second impact delay at 50 Tick/s must keep a " +
                    "one-second presentation duration.");
            }
            finally
            {
                VisualEventOutput.Clear();
                UnitTestFactory.DestroyCreatedObjects();
            }
        }

        [Test]
        public void
            WProjectile_FliesStraightAlongCastDirection_IgnoringTargetPosition()
        {
            UnitWorld world = new UnitWorld();
            UnitPrototype prototype = CreateTestPrototype();
            UnitType caster = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                20,
                fp.zero,
                fp.zero);
            // Target placed PERPENDICULAR to the cast direction; a homing
            // projectile would bend toward it, a straight chain must not.
            UnitType target = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(2),
                20,
                fp.zero,
                fp.zero);
            SetPose(
                target,
                new fp2(
                    fp.zero,
                    (fp)3));
            UnitTestFactory.AddProjectilePrefab(
                world,
                2105);

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
                    DefId = 109,
                    RuntimeEntityPrefabId = 2105,
                    Speed = (fp)18,
                    Homing = false,
                    MaxLifetimeTicks = 15,
                    HitRadius = (fp)0.2m,
                    TargetFilter =
                        ProjectileTargetFilter
                            .DefaultEnemy,
                    HitPolicy =
                        new ProjectileHitPolicy
                        {
                            Enabled = true,
                            QueryIntervalTicks = 1,
                            MaxTotalHitCount = 1,
                            EndOnFirstValidHit = true,
                        },
                });
            world.ProjectileWorld = projectileWorld;

            AbilityAsset wAsset = Load<AbilityAsset>(
                Root + "Abilities/Aatrox/AatroxW.asset");
            AbilityDef wDef = wAsset.Bake();
            var wStage =
                (ScaledProjectileStageDef)
                ((ScaledProjectileStageDefAuthoring)
                    wAsset.Stages[0]).Bake();
            var model = new CommitCastModelDef
            {
                Cast = new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 14,
                    NotifyAbilityCastOnEnter = true,
                    Def = wStage,
                },
            };
            Install(
                world,
                caster,
                new AbilityDef
                {
                    AbilityId = wDef.AbilityId,
                    Name = wDef.Name,
                    CastModel = model,
                    AimKind = AimKind.Direction,
                    CastRange = (fp)8.25m,
                    CostPlan = default,
                    CooldownByLevel = default,
                });

            // Cast the chain straight along +X; the target sits on +Y.
            Assert.IsTrue(
                caster.AbilityHandler.HandleSignal(
                    CommitSignal(
                        AimSnapshot.ForDirection(
                            new fp2(
                                fp.one,
                                fp.zero)))));

            // Advance the cast until the projectile spawns (delay 14 ticks).
            for (int tick = 0;
                 tick < 15;
                 tick++)
            {
                caster.AbilityHandler.TickUpdate();
            }
            projectileWorld.CommitSpawns();
            Assert.That(
                projectileWorld.Count,
                Is.EqualTo(1));

            ProjectileRuntime projectile =
                projectileWorld.GetAllOrdered()[0];
            AssertFpClose(
                projectile.Velocity.x,
                (fp)18);
            AssertFpClose(
                projectile.Velocity.y,
                fp.zero);

            // Let it fly a few Ticks, then move the target far beyond the
            // cast line mid-flight: the chain must still fly straight along
            // +X and never bend toward the target's new direction.
            for (int tick = 0;
                 tick < 3;
                 tick++)
            {
                projectileWorld.AdvanceMotion();
            }
            SetPose(
                target,
                new fp2(
                    (fp)10,
                    (fp)8));
            for (int tick = 0;
                 tick < 6;
                 tick++)
            {
                projectileWorld.AdvanceMotion();
            }
            Assert.That(
                projectile.Position.x,
                Is.GreaterThan(
                    (fp)5));
            AssertFpClose(
                projectile.Position.y,
                fp.zero,
                (fp)0.001m);
        }

        private static void AssertFpClose(
            fp actual,
            fp expected) =>
            AssertFpClose(
                actual,
                expected,
                (fp)0.01m);

        private static void AssertFpClose(
            fp actual,
            fp expected,
            fp tolerance)
        {
            Assert.That(
                fpmath.abs(actual - expected) <=
                    tolerance,
                Is.True,
                $"Expected {expected} but was {actual}.");
        }

        private static void SetPose(
            UnitType unit,
            fp2 position)
        {
            unit.PhysicsEntity.SetLogicShape(
                Physics.PhysicsShape2D.CreateCircle(
                    fp2.zero,
                    (fp)1 / (fp)4));
            unit.PhysicsEntity.SetLogicPose(
                position,
                new fp2(fp.one, fp.zero));
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
                SlotIndex = 1,
                ActiveAbilityId = definition.AbilityId,
                AllocatedPoints = 1,
            };
            slot.AddAbility(runtime);
            caster.AbilityHandler.AddSlot(slot);
        }

        private static AbilitySignal CommitSignal(
            AimSnapshot aim) =>
            new AbilitySignal
            {
                Slot = 1,
                Verb = AbilitySignalVerb.Commit,
                Aim = aim,
            };

        private static UnitPrototype CreateTestPrototype()
        {
            var preset = new StatPreset();
            preset.Stats.Add(
                new StatPresetEntry
                {
                    StatId = StatId.MaxHealth,
                    BaseValue = (fp)500,
                    GrowthValue = fp.zero,
                });
            preset.Stats.Add(
                new StatPresetEntry
                {
                    StatId = StatId.MaxCastResource,
                    BaseValue = (fp)300,
                    GrowthValue = fp.zero,
                });
            return new UnitPrototype
            {
                UnitPrototypeId = 1002,
                Name = "AatroxWTest",
                RuntimeEntityPrefabId = 1102,
                UnitKind = UnitKind.Hero,
                BaseStats = preset,
                Loadout = HandlerLoadout.DefaultHero,
            };
        }

        [Test]
        public void SequentialRecastSession_SnapshotRoundTripPreservesWindowState()
        {
            AbilityDef definition = Load<AbilityAsset>(
                Root + "Abilities/Aatrox/AatroxQ.asset").Bake();
            var source = new AbilityRuntime
            {
                Definition = definition,
                Level = 3,
            };
            AbilitySession session = source.BeginSession(
                77,
                123,
                AimSnapshot.ForDirection(
                    new Unity.Mathematics.FixedPoint.fp2(
                        Unity.Mathematics.FixedPoint.fp.one,
                        Unity.Mathematics.FixedPoint.fp.zero)));
            session.CurrentStageKey = 2;
            session.StageElapsedTicks = 29;
            var snapshot = new AbilityRuntimeSnapshot();
            source.Capture(ref snapshot);

            var restored = new AbilityRuntime { Definition = definition };
            restored.Restore(snapshot);

            Assert.That(restored.ActiveSession.SessionUid, Is.EqualTo(77));
            Assert.That(restored.ActiveSession.CurrentStageKey, Is.EqualTo(2));
            Assert.That(restored.ActiveSession.StageElapsedTicks, Is.EqualTo(29));
            var model = (SequentialRecastCastModelDef)definition.CastModel;
            var commit = new AbilitySignal { Verb = AbilitySignalVerb.Commit };
            Assert.That(model.CanHandleSignal(
                commit,
                restored.ActiveSession.CurrentStageKey,
                restored.ActiveSession.StageElapsedTicks), Is.False);
        }

        [Test]
        public void SequentialRecastWindow_ExposesShortHudCooldown()
        {
            GameObject holder = new GameObject(
                "AatroxRecastHudCooldownFixture");
            try
            {
                AbilityHandler handler =
                    holder.AddComponent<AbilityHandler>();
                AbilityDef definition = Load<AbilityAsset>(
                    Root + "Abilities/Aatrox/AatroxQ.asset")
                    .Bake();
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
                handler.AddSlot(slot);
                AbilitySession session = runtime.BeginSession(
                    1,
                    10,
                    AimSnapshot.ForDirection(
                        new fp2(fp.zero, fp.one)));
                SequentialRecastCastModelDef model =
                    (SequentialRecastCastModelDef)
                    definition.CastModel;
                session.CurrentStageKey =
                    model.FirstRecastWindow.StageKey;
                session.StageElapsedTicks = 12;

                Assert.That(
                    handler.GetDisplayCooldownRemainingTicks(
                        0,
                        100),
                    Is.EqualTo(18));
                Assert.That(
                    handler.GetDisplayCooldownTotalTicks(0),
                    Is.EqualTo(30));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void FormalCatalogs_RegisterAatroxRuntimeContent()
        {
            GlobalPrefabTable root = Load<GlobalPrefabTable>(
                Root + "GlobalPrefabTable.asset");
            GlobalPrefabTable prefabs = BuildResolvedFormalPrefabTable(root);
            try
            {
            Assert.That(prefabs.GetRequiredPrefab(PrefabKind.Unit, 1102), Is.Not.Null);
            Assert.That(prefabs.GetRequiredPrefab(PrefabKind.Projectile, 2105), Is.Not.Null);
            Assert.That(prefabs.GetRequiredPrefab(PrefabKind.Projectile, 2106), Is.Not.Null);
            for (int id = 3101; id <= 3103; id++)
            {
                Assert.That(
                    prefabs.TryGetEntry(
                        PrefabKind.ParticleVfx,
                        id,
                        out PrefabEntry entry),
                    Is.True);
                Assert.That(entry.UnityPrefab, Is.Null);
                Assert.That(entry.ClientViewAddress, Is.Not.Empty);
            }

            UnitRuntimeCatalogAsset units = Load<UnitRuntimeCatalogAsset>(
                Root + "FullMatchUnitRuntimeCatalog.asset");
            UnitPrototypeAuthoring aatrox = units.UnitPrototypes.Single(
                item => item.UnitPrototypeId == 1002);
            Assert.That(aatrox.RuntimeEntityPrefabId, Is.EqualTo(1102));
            Assert.That(BaseStat(aatrox, StatId.MaxHealth), Is.EqualTo(650f));
            Assert.That(BaseStat(aatrox, StatId.AttackRange), Is.EqualTo(175f));
            units.BakeOrThrow(prefabs);

            ProjectileRuntimeCatalogAsset projectiles =
                Load<ProjectileRuntimeCatalogAsset>(
                    Root + "FullMatchProjectileRuntimeCatalog.asset");
            ProjectileDef wProjectile =
                projectiles.BakeOrThrow(prefabs).FindById(109);
            Assert.That(wProjectile, Is.Not.Null);
            Assert.That(wProjectile.TargetFilter.UnitKindMask,
                Is.EqualTo(ProjectileUnitKindMask.Hero |
                    ProjectileUnitKindMask.Minion |
                    ProjectileUnitKindMask.Monster));
            Assert.That(wProjectile.OnHitEffects.BuffEffects.Single(
                item => item.BuffId.Value == 12022).TargetKinds.Contains(
                    UnitKind.Hero), Is.True);
            Assert.That(wProjectile.OnHitEffects.BuffEffects.Single(
                item => item.BuffId.Value == 12022).TargetKinds.Contains(
                    UnitKind.Minion), Is.False);
            ProjectileDef tetherArea =
                projectiles.BakeOrThrow(prefabs).FindById(110);
            Assert.That(tetherArea, Is.Not.Null);
            Assert.That(tetherArea.RuntimeEntityPrefabId, Is.EqualTo(2106));
            Assert.That(tetherArea.Speed, Is.EqualTo(fp.zero));
            Assert.That(tetherArea.HitPolicy.Enabled, Is.False);
            Assert.That(tetherArea.ContainmentZone.IsValid, Is.True);
            Assert.That(tetherArea.ContainmentZone.ForwardStart,
                Is.EqualTo((fp)(-1.5f)));
            Assert.That(tetherArea.ContainmentZone.ForwardLength,
                Is.EqualTo((fp)6f));

            BuffCatalogAsset buffs = Load<BuffCatalogAsset>(
                Root + "Buffs/FullMatchTestBuffCatalog.asset");
            for (int id = 12021; id <= 12022; id++)
                Assert.That(buffs.Definitions.Any(
                    item => item.ConfigId.Value == id), Is.True);
            Assert.That(buffs.Definitions.Any(
                item => item.ConfigId.Value == 12024), Is.True);
            BuffDefinition slow = buffs.Definitions.Single(
                item => item.ConfigId.Value == 12021);
            Assert.That(slow.GetEffects()
                    .OfType<AbilityRankStatModifierBuffEffect>()
                    .Single().ValueByAbilityLevel.Resolve(1),
                Is.EqualTo((fp).25f));
            BuffDefinition tether = buffs.Definitions.Single(
                item => item.ConfigId.Value == 12022);
            Assert.That(tether.GetEffects()
                    .OfType<TetherZoneBuffEffect>()
                    .Single().BaseDamageByAbilityLevel.Resolve(1),
                Is.EqualTo((fp)30));
            TetherZoneBuffEffect tetherEffect = tether.GetEffects()
                .OfType<TetherZoneBuffEffect>()
                .Single();
            Assert.That(tetherEffect.AreaProjectileDefId, Is.EqualTo(110));
            Assert.That(tetherEffect.ProjectileSpawnTickSlot.IsValid, Is.True);
            Assert.That(tetherEffect.ProjectilePrefabIdSlot.IsValid, Is.True);
            Assert.That(tetherEffect.ProjectileSequenceSlot.IsValid, Is.True);
            BuffDefinition worldEnder = buffs.Definitions.Single(
                item => item.ConfigId.Value == 12024);
            Assert.That(worldEnder.GetEffects()
                    .OfType<AbilityRankStatModifierBuffEffect>()
                    .All(item => item.ValueByAbilityLevel.Resolve(1) > fp.zero),
                Is.True);

            CrowdControlCatalogAsset controls = Load<CrowdControlCatalogAsset>(
                Root + "CrowdControl/CrowdControlCatalog.asset");
            Assert.That(controls.Definitions.Any(
                item => item.ControlId.Value == 113), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabs);
            }
        }

        [Test]
        public void BasicAttackProjectiles_MeleeContentUsesDirectDamage()
        {
            Assert.That(
                Load<GameObject>(
                        Root + "Prefabs/Logic/Unit/AatroxHeroRuntime.prefab")
                    .GetComponent<AttackHandler>().ProjectileDefId,
                Is.Zero,
                "Aatrox basic attacks must settle directly without a projectile.");
            Assert.That(
                Load<GameObject>(
                        Root + "Prefabs/Logic/Unit/TestMeleeMinionBlueRuntime.prefab")
                    .GetComponent<AttackHandler>().ProjectileDefId,
                Is.Zero,
                "Blue melee minions must settle directly without a projectile.");
            Assert.That(
                Load<GameObject>(
                        Root + "Prefabs/Logic/Unit/TestMeleeMinionRedRuntime.prefab")
                    .GetComponent<AttackHandler>().ProjectileDefId,
                Is.Zero,
                "Red melee minions must settle directly without a projectile.");

            Assert.That(
                Load<GameObject>(
                        Root + "Prefabs/Logic/Unit/VarusRuntime.prefab")
                    .GetComponent<AttackHandler>().ProjectileDefId,
                Is.EqualTo(101),
                "Varus remains a projectile basic attacker.");
            Assert.That(
                Load<GameObject>(
                        Root + "Prefabs/Logic/Unit/TestCasterMinionBlueRuntime.prefab")
                    .GetComponent<AttackHandler>().ProjectileDefId,
                Is.EqualTo(102),
                "Blue caster minions remain projectile basic attackers.");
            Assert.That(
                Load<GameObject>(
                        Root + "Prefabs/Logic/Unit/TestCasterMinionRedRuntime.prefab")
                    .GetComponent<AttackHandler>().ProjectileDefId,
                Is.EqualTo(103),
                "Red caster minions remain projectile basic attackers.");
        }

        [Test]
        public void AatroxPrefabs_SeparateGameplayFromPresentationAndRetainEditorGizmo()
        {
            GameObject logicPrefab = Load<GameObject>(
                "Assets/Config/Formal/Prefabs/Logic/Unit/" +
                "AatroxHeroRuntime.prefab");
            Assert.That(logicPrefab.GetComponent<Unit>(), Is.Not.Null);
            Assert.That(logicPrefab.GetComponent<AbilityHandler>(), Is.Not.Null);
            AatroxAbilityZoneAuthoringGizmo gizmo =
                logicPrefab.GetComponent<AatroxAbilityZoneAuthoringGizmo>();
            Assert.That(gizmo, Is.Not.Null);
            SerializedObject serializedGizmo = new SerializedObject(gizmo);
            Assert.That(serializedGizmo.FindProperty("qAbility")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(serializedGizmo.FindProperty("wTetherZone")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                logicPrefab.GetComponentInChildren<Animator>(true),
                Is.Null,
                "The deterministic logic prefab must not retain an Animator.");
            Assert.That(logicPrefab.transform.Find("Model"), Is.Null);

            GameObject viewPrefab = Load<GameObject>(
                "Assets/ClientContent/Views/Unit/" +
                "AatroxHeroRuntimeView.prefab");
            Assert.That(viewPrefab.GetComponent<Unit>(), Is.Null);
            Assert.That(viewPrefab.GetComponent<AbilityHandler>(), Is.Null);
            Animator animator = viewPrefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            AnimatorController controller =
                animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "IsMoving", "MoveSpeed", "IsAttacking",
                    "IsAttackRecovering", "IsEmpoweredAttack",
                    "AttackSequenceIndex", "AttackMotionTime",
                    "AttackStart", "IsCasting",
                    "AbilityStageProgress", "LifeState", "IsControlled",
                    "IsPassiveReady", "IsAnimationVariantActive",
                    "AnimationVariantExit",
                },
                controller.parameters.Select(item => item.name).ToArray());
            AnimatorStateMachine machine =
                controller.layers[0].stateMachine;
            Assert.That(machine.defaultState.name, Is.EqualTo("AatroxIdle"));
            Assert.That(machine.anyStateTransitions.Length,
                Is.EqualTo(8));
            string[] locomotionStates =
            {
                "AatroxIdle", "AatroxWalk",
                "AatroxIdle_Passive", "AatroxWalk_Passive",
                "AatroxIdle_ULT", "AatroxWalk_ULT",
            };
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "AatroxIdle", "AatroxWalk",
                    "AatroxIdle_Passive", "AatroxWalk_Passive",
                    "AatroxIdle_ULT", "AatroxWalk_ULT",
                    "AatroxAttack1", "AatroxAttack2",
                    "AatroxAttack_Passive",
                    "AatroxAttack1_ULT", "AatroxAttack2_ULT",
                    "AatroxAttack_Passive_ULT",
                    "AatroxDash", "AatroxDash_Passive",
                    "AatroxDash_ULT",
                    "AatroxQ1", "AatroxQ2", "AatroxQ3",
                    "AatroxQ1_ULT", "AatroxQ2_ULT", "AatroxQ3_ULT",
                    "AatroxSpellW", "AatroxSpellW_ULT",
                    "AatroxSpellR", "AatroxDeath",
                    "AatroxULTOut",
                },
                machine.states.Select(item => item.state.name).ToArray());
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "AatroxAttack1", "AatroxAttack2",
                    "AatroxAttack_Passive",
                    "AatroxAttack1_ULT", "AatroxAttack2_ULT",
                    "AatroxAttack_Passive_ULT",
                },
                machine.anyStateTransitions
                    .Where(item =>
                        item.destinationState.name.StartsWith(
                            "AatroxAttack",
                            StringComparison.Ordinal))
                    .Select(item => item.destinationState.name)
                    .ToArray());
            Assert.That(machine.anyStateTransitions.Where(
                    item => item.destinationState != null &&
                        locomotionStates.Contains(
                            item.destinationState.name)),
                Is.Empty,
                "Looping locomotion must not be entered from AnyState.");
            foreach (string sourceName in locomotionStates)
            {
                AnimatorState source = machine.states.Single(
                    item => item.state.name == sourceName).state;
                AnimatorStateTransition[] transitions = source.transitions
                    .Where(item => item.destinationState != null &&
                        locomotionStates.Contains(
                            item.destinationState.name))
                    .ToArray();
                Assert.That(transitions,
                    Has.Length.EqualTo(locomotionStates.Length - 1),
                    sourceName);
                Assert.That(transitions.Any(item =>
                        item.destinationState == source ||
                        item.canTransitionToSelf),
                    Is.False,
                    sourceName);
            }
            AnimatorState passiveIdle = machine.states.Single(
                item => item.state.name ==
                    "AatroxIdle_Passive").state;
            Assert.That(passiveIdle.transitions.Any(
                    item => item.destinationState != null &&
                        item.destinationState.name ==
                            "AatroxWalk_Passive" &&
                        item.conditions.Any(condition =>
                            condition.parameter == "IsMoving" &&
                            condition.mode ==
                                AnimatorConditionMode.If)),
                Is.True);
            Assert.That(machine.states.Single(
                    item => item.state.name == "AatroxQ3_ULT")
                    .state.transitions.Any(
                        item => item.destinationState != null &&
                            item.destinationState.name ==
                                "AatroxIdle"),
                Is.True);
            Assert.That(machine.anyStateTransitions.Any(
                    item => item.destinationState != null &&
                        item.destinationState.name == "AatroxDeath" &&
                        item.conditions.Any(condition =>
                            condition.parameter == "LifeState" &&
                            condition.mode ==
                                AnimatorConditionMode.Equals &&
                            condition.threshold == 2f)),
                Is.True);

            FrameSyncMoba.Presentation.UnitAnimationProfile profile =
                Load<FrameSyncMoba.Presentation.UnitAnimationProfile>(
                    "Assets/ClientContent/Animation/Profiles/" +
                    "AatroxAnimationProfile.asset");
            Assert.That(profile.PassiveReadyAbilityId,
                Is.EqualTo(10020));
            Assert.That(profile.AnimationVariantBuffConfigId,
                Is.EqualTo(12024));
            Assert.That(profile.AnimationVariantExitHash,
                Is.EqualTo(Animator.StringToHash(
                    "AnimationVariantExit")));
            Assert.That(profile.DeathStateHash,
                Is.EqualTo(Animator.StringToHash(
                    "Base Layer.AatroxDeath")));
            FrameSyncMoba.Presentation.StageAnimationBinding dash =
                profile.StageBindings.Single(
                    item => item.AbilityId == 10023 &&
                        item.StageIndex == 1);
            Assert.That(dash.PassiveReadyStateNameHash,
                Is.EqualTo(Animator.StringToHash(
                    "Base Layer.AatroxDash_Passive")));
            Assert.That(dash.AnimationVariantStateNameHash,
                Is.EqualTo(Animator.StringToHash(
                    "Base Layer.AatroxDash_ULT")));
            Assert.That(profile.StageBindings
                    .Where(item => item.AbilityId == 10021)
                    .All(item =>
                        item.AnimationVariantStateNameHash != 0),
                Is.True);
            Assert.That(viewPrefab.transform.Find("Model"), Is.Not.Null);

            foreach (string clipName in new[]
                {
                    "AatroxQ1", "AatroxQ2", "AatroxQ3",
                    "AatroxQ1_ULT", "AatroxQ2_ULT", "AatroxQ3_ULT",
                })
            {
                AnimationClip clip = Load<AnimationClip>(
                    "Assets/ClientContent/Animation/Aatrox/" +
                    clipName + ".anim");
                Assert.That(clip.length,
                    Is.EqualTo(1f).Within(.001f),
                    clipName);
            }

            foreach (string clipName in new[]
                {
                    "AatroxSpellW", "AatroxSpellW_ULT",
                })
            {
                AnimationClip clip = Load<AnimationClip>(
                    "Assets/ClientContent/Animation/Aatrox/" +
                    clipName + ".anim");
                Assert.That(clip.length,
                    Is.EqualTo(14f / 30f).Within(.001f),
                    clipName);
            }

            foreach (string stateName in new[]
                {
                    "AatroxSpellW", "AatroxSpellW_ULT",
                })
            {
                AnimatorState state = machine.states.Single(
                    item => item.state.name == stateName).state;
                Assert.That(state.transitions,
                    Is.Not.Empty,
                    stateName);
                Assert.That(state.transitions,
                    Is.All.Matches<AnimatorStateTransition>(
                        transition =>
                            transition.hasFixedDuration &&
                            Mathf.Approximately(
                                transition.duration,
                                .05f)),
                    stateName);
            }

            GameObject wMissile = Load<GameObject>(
                "Assets/Config/Formal/Prefabs/Logic/Projectile/AatroxSpellWMissle.prefab");
            Assert.That(wMissile.GetComponent<Physics.PhysicsEntity2D>(),
                Is.Not.Null);
            Assert.That(wMissile.GetComponent<Physics.PhysicsEntity2DShapeAuthoring>(),
                Is.Not.Null);

            GameObject tetherArea = Load<GameObject>(
                "Assets/Config/Formal/Prefabs/Logic/Projectile/InfernalChainsArea.prefab");
            Assert.That(tetherArea.GetComponent<Physics.PhysicsEntity2D>(),
                Is.Not.Null);
            Assert.That(tetherArea.GetComponent<Physics.PhysicsEntity2DShapeAuthoring>(),
                Is.Not.Null);
            Assert.That(tetherArea.GetComponent<ProjectileContainmentZoneAuthoring>(),
                Is.Not.Null);
            Assert.That(tetherArea.GetComponent<LineRenderer>(), Is.Null);
            GameObject tetherAreaView = Load<GameObject>(
                "Assets/ClientContent/Views/Projectile/" +
                "InfernalChainsAreaView.prefab");
            Assert.That(tetherAreaView.GetComponent<LineRenderer>(), Is.Not.Null);
            Assert.That(logicPrefab.GetComponent<AatroxAbilityZoneAuthoringGizmo>(),
                Is.Not.Null);
        }

        [Test]
        public void InfernalChainsArea_ContainmentUsesAuthoredTrapezoid()
        {
            GameObject prefab = Load<GameObject>(
                "Assets/Config/Formal/Prefabs/Logic/Projectile/InfernalChainsArea.prefab");
            ProjectileContainmentZone zone = prefab
                .GetComponent<ProjectileContainmentZoneAuthoring>()
                .BakeOrThrow();
            fp2 origin = fp2.zero;
            fp2 forward = new fp2(fp.zero, fp.one);

            Assert.That(zone.Contains(
                origin, forward, new fp2(fp.zero, (fp)(-1.5f)), fp.zero),
                Is.True);
            Assert.That(zone.Contains(
                origin, forward, new fp2((fp)2.9f, (fp)4.4f), fp.zero),
                Is.True);
            Assert.That(zone.Contains(
                origin, forward, new fp2((fp)2f, (fp)(-1f)), fp.zero),
                Is.False);
            Assert.That(zone.Contains(
                origin, forward, new fp2(fp.zero, (fp)4.6f), fp.zero),
                Is.False);
        }

        [Test]
        public void ProjectileWorld_RequestEnd_CancelsPendingAreaProjectile()
        {
            var registry = new ProjectileDefRegistry();
            registry.Register(new ProjectileDef
            {
                DefId = 110,
                RuntimeEntityPrefabId = 2106,
                Speed = fp.zero,
                MaxLifetimeTicks = 45,
                HitRadius = fp.zero,
                TargetFilter = ProjectileTargetFilter.DefaultEnemy,
                HitPolicy = new ProjectileHitPolicy { Enabled = false },
                ContainmentZone = new ProjectileContainmentZone(
                    (fp)(-1.5f), (fp)6f, fp.one, (fp)3f),
            });
            var world = new ProjectileWorld { DefRegistry = registry };
            var controller = new SimulationTickContextController();
            controller.BeginTick(42, ExecutionMode.ServerAuthority);
            try
            {
                UnitUid owner = new UnitUid(1, 1102, 0);
                ProjectileUid uid = world.RequestSpawn(
                    new ProjectileSpawnRequest(
                        110,
                        owner,
                        new TeamId(1),
                        new SourceDescriptor
                        {
                            SourceType = CombatSourceType.Ability,
                            SourceId = 10022,
                            OwnerUnitUid = owner,
                            EmitterUnitUid = owner,
                        },
                        new OriginActionId(
                            GameplayParticipantId.Explicit(1),
                            CombatSourceType.Ability,
                            10022,
                            42,
                            0),
                        fp2.zero,
                        new fp2(fp.zero, fp.one)));

                Assert.That(uid.IsValid, Is.True);
                Assert.That(world.PendingCount, Is.EqualTo(1));
                Assert.That(world.RequestEnd(uid), Is.True);
                Assert.That(world.PendingCount, Is.Zero);
                Assert.That(world.RequestEnd(uid), Is.False);
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void EmpoweredFixedPassive_RollbackRebuild_DoesNotDuplicateReadyBuffModifier()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(
                40,
                ExecutionMode.ServerAuthority);
            try
            {
                var world = new UnitWorld();
                world.BuffDefinitions =
                    new BuffDefinitionRegistry();
                world.BuffDefinitions.Register(
                    CreateDeathbringerStanceBuffDefinition());

                Unit hero = UnitTestFactory.SpawnUnit(
                    world,
                    CreateEmpoweredHeroPrototype(),
                    new TeamId(1),
                    40,
                    fp.zero,
                    fp.zero);
                hero.AbilityHandler.SetFixedPassive(
                    CreateEmpoweredPassiveDef());

                // The empowered passive is ready from spawn
                // (NextReadyLogicTick == 0), so the first tick applies the
                // ready Buff, which owns the AttackRange modifier exactly
                // once (rollback-safe by construction).
                hero.AbilityHandler.FixedPassive.EffectRuntime.Tick(hero);

                var statBefore = new StatHandlerSnapshot();
                hero.StatHandler.Capture(ref statBefore);
                var abilityBefore = new AbilityHandlerSnapshot();
                hero.AbilityHandler.Capture(ref abilityBefore);
                Assert.That(
                    CountModifiers(
                        statBefore,
                        StatId.AttackRange),
                    Is.EqualTo(1));
                Assert.That(
                    hero.BuffHandler.HasBuff(
                        new BuffConfigId(10025)),
                    Is.True);

                // Rollback restore + rebuild: the exact sequence executed by
                // PredictionRollbackCoordinator.CorrectAndReplay before it
                // replays the authoritative tick.
                hero.StatHandler.Restore(statBefore);
                hero.AbilityHandler.Restore(abilityBefore);
                hero.AbilityHandler.Rebuild(
                    new RollbackContext(
                        41,
                        ExecutionMode.ClientReplay));

                var statAfter = new StatHandlerSnapshot();
                hero.StatHandler.Capture(ref statAfter);
                Assert.That(
                    CountModifiers(
                        statAfter,
                        StatId.AttackRange),
                    Is.EqualTo(1),
                    "Rebuild must not duplicate the ready Buff's " +
                    "StatModifier: Restore already brought it back " +
                    "(Unit v27.3 7.15).");
                Assert.That(
                    statAfter.NextStatSeq,
                    Is.EqualTo(statBefore.NextStatSeq));
                Assert.That(
                    hero.BuffHandler.HasBuff(
                        new BuffConfigId(10025)),
                    Is.True);
                Assert.That(
                    hero.StatHandler.GetStat(StatId.AttackRange),
                    Is.EqualTo((fp)175 + (fp)0.5m));
            }
            finally
            {
                controller.EndTick();
                UnitTestFactory.DestroyCreatedObjects();
            }
        }

        private static UnitPrototype CreateEmpoweredHeroPrototype()
        {
            StatPreset preset = UnitTestFactory.CreateDefaultPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackRange,
                BaseValue = (fp)175,
                GrowthValue = fp.zero,
            });
            return new UnitPrototype
            {
                UnitPrototypeId = 1002,
                RuntimeEntityPrefabId = 1102,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                Loadout = HandlerLoadout.DefaultHero,
                BaseStats = preset,
                BaseGoldValue = 0,
                BaseExperienceValue = 0,
            };
        }

        private static PassiveAbilityDef CreateEmpoweredPassiveDef()
        {
            return new PassiveAbilityDef
            {
                AbilityId = 10020,
                Name = "TestEmpowered",
                PassiveEffect =
                    new EmpoweredAttackMaxHealthPassiveEffectDef
                    {
                        ListenerMask =
                            AbilityPassiveListenerMask.OnHitDealt |
                            AbilityPassiveListenerMask.DamageDealt,
                        SourceAbilityId = 10020,
                        RecipeId = 100200,
                        ReadyBuffConfigId =
                            new BuffConfigId(10025),
                        HeroHealRatioBasisPoints = 8000,
                        NonHeroHealRatioBasisPoints = 2500,
                    },
            };
        }

        private static BuffDefinition
            CreateDeathbringerStanceBuffDefinition()
        {
            var definition =
                UnityEngine.ScriptableObject
                    .CreateInstance<BuffDefinition>();
            definition.ConfigId =
                new BuffConfigId(10025);
            definition.Display =
                new BuffDisplayInfo
                {
                    Name = "Deathbringer Stance",
                };
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
                        Effect = new StatModifierBuffEffect
                        {
                            StatId = StatId.AttackRange,
                            Operation =
                                StatModifierOperation.FlatAdd,
                            BaseValue = (fp)0.5m,
                            ValuePerStack = fp.zero,
                            HandleSlot =
                                new BuffStateSlotId(1002501),
                        },
                    },
                    new BuffEffectConfig
                    {
                        Effect = new MaxHealthOnHitBuffEffect
                        {
                            SourceAbilityId = 10020,
                            RecipeId = 100200,
                            MaxHealthRatioBasisPointsByUnitLevel =
                                new[] { 400 },
                            MonsterDamageCapByUnitLevel =
                                new[] { 100 },
                        },
                    },
                };
            return definition;
        }

        private static int CountModifiers(
            StatHandlerSnapshot snapshot,
            StatId statId)
        {
            StatRuntimeEntrySnapshot[] entries =
                snapshot.Entries ??
                Array.Empty<StatRuntimeEntrySnapshot>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].StatId == statId)
                {
                    return entries[i].Modifiers?.Length ?? 0;
                }
            }
            return 0;
        }

        private static float BaseStat(UnitPrototypeAuthoring unit, StatId stat)
        {
            return unit.BaseStats.Single(item => item.StatId == stat).BaseValue;
        }

        private static GlobalPrefabTable BuildResolvedFormalPrefabTable(
            GlobalPrefabTable root)
        {
            string[] paths =
            {
                Root + "MatchContent/CoreGlobalPrefabSubTable.asset",
                Root + "MatchContent/VarusGlobalPrefabSubTable.asset",
                Root + "MatchContent/AatroxGlobalPrefabSubTable.asset",
            };
            var children = new List<GlobalPrefabSubTableAsset>();
            var resolved = new Dictionary<string, GameObject>(
                StringComparer.Ordinal);
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                GlobalPrefabSubTableAsset child =
                    Load<GlobalPrefabSubTableAsset>(paths[pathIndex]);
                child.ValidateOrThrow();
                children.Add(child);
                for (int groupIndex = 0;
                     groupIndex < child.PrefabGroups.Count;
                     groupIndex++)
                {
                    PrefabGroup group = child.PrefabGroups[groupIndex];
                    for (int entryIndex = 0;
                         entryIndex < group.Entries.Count;
                         entryIndex++)
                    {
                        PrefabEntry entry = group.Entries[entryIndex];
                        if (string.IsNullOrEmpty(entry.LogicAssetAddress))
                            continue;
                        resolved.Add(
                            entry.LogicAssetAddress,
                            Load<GameObject>(entry.LogicAssetAddress));
                    }
                }
            }
            return root.CreateResolvedRuntimeTable(children, resolved);
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }
    }
}

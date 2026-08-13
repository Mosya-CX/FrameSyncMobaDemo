using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class StageDefBakeTests
    {
        [Test]
        public void AreaDamageStageDef_OnEnter_WithoutWorld_ReturnsFailed()
        {
            var def = new AreaDamageStageDef
            {
                StageDefId = 1,
                DebugName = "TestArea",
                Radius = (fp)3,
                BaseDamage = (fp)50,
                DamageType = DamageType.Physical,
            };
            var runtime = new AbilityRuntime();
            var session = runtime.BeginSession(1, 0, AimSnapshot.None);

            StageResult result = def.OnEnter(session, runtime);

            Assert.That(result, Is.EqualTo(StageResult.Failed));
        }

        [Test]
        public void SpawnProjectileStageDef_OnEnter_WithoutWorld_ReturnsFailed()
        {
            var def = new SpawnProjectileStageDef
            {
                StageDefId = 2,
                DebugName = "TestSpawn",
                ProjectileDefId = 1,
            };
            var runtime = new AbilityRuntime();
            var session = runtime.BeginSession(1, 0, AimSnapshot.None);

            StageResult result = def.OnEnter(session, runtime);

            Assert.That(result, Is.EqualTo(StageResult.Failed));
        }

        [Test]
        public void ApplyBuffStageDef_OnEnter_WithoutWorld_ReturnsFailed()
        {
            var def = new ApplyBuffStageDef
            {
                StageDefId = 3,
                DebugName = "TestBuff",
                BuffConfigId = new BuffConfigId(1),
                TargetRule = BuffTargetRule.Self,
            };
            var runtime = new AbilityRuntime();
            var session = runtime.BeginSession(1, 0, AimSnapshot.None);

            StageResult result = def.OnEnter(session, runtime);

            Assert.That(result, Is.EqualTo(StageResult.Failed));
        }

        [Test]
        public void DashStageDef_OnEnter_WithValidConfig_ReturnsRunning()
        {
            var def = new DashStageDef
            {
                StageDefId = 4,
                DebugName = "TestDash",
                SpeedPerTick = (fp)1,
                TotalDistance = (fp)8,
            };
            UnitWorld world = new UnitWorld();
            UnitPrototype prototype = CreateTestPrototype();
            Unit caster = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                20,
                fp.zero,
                fp.zero);
            var runtime = new AbilityRuntime
            {
                Definition = new AbilityDef { AbilityId = 1 },
            };
            runtime.World = world;
            runtime.CasterUnitUid = caster.UnitUid;
            var aim = AimSnapshot.ForDirection(new fp2(fp.one, fp.zero));
            var session = runtime.BeginSession(1, 0, aim);

            StageResult result = def.OnEnter(session, runtime);

            Assert.That(result, Is.EqualTo(StageResult.Running));
        }

        [Test]
        public void DashStageDef_OnTick_WithoutWorld_ReturnsFailed()
        {
            var def = new DashStageDef
            {
                StageDefId = 4,
                DebugName = "TestDash",
                SpeedPerTick = (fp)1,
                TotalDistance = (fp)8,
            };
            var runtime = new AbilityRuntime
            {
                Definition = new AbilityDef { AbilityId = 1 },
            };
            var aim = AimSnapshot.ForDirection(new fp2(fp.one, fp.zero));
            var session = runtime.BeginSession(1, 0, aim);
            def.OnEnter(session, runtime);

            StageResult result = def.OnTick(session, runtime);

            Assert.That(result, Is.EqualTo(StageResult.Failed));
        }

        [Test]
        public void AreaDamageAuthoring_Bake_ProducesValidStageDef()
        {
            var authoring = new AreaDamageStageDefAuthoring();

            var def = authoring.Bake();

            Assert.That(def, Is.InstanceOf<AreaDamageStageDef>());
            var areaDef = (AreaDamageStageDef)def;
            Assert.That(areaDef.Radius, Is.GreaterThan(fp.zero));
            Assert.That(areaDef.BaseDamage, Is.GreaterThan(fp.zero));
        }

        [Test]
        public void AreaDamageAuthoring_Bake_RejectsEmptyTargetMasks()
        {
            var authoring = new AreaDamageStageDefAuthoring();
            FieldInfo filterField =
                typeof(AreaDamageStageDefAuthoring).GetField(
                    "targetFilter",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(filterField, Is.Not.Null);
            filterField.SetValue(authoring, default(UnitTargetFilter));

            Assert.Throws<InvalidOperationException>(
                () => authoring.Bake());
        }

        [Test]
        public void UnitTargetFilter_JsonRoundTrip_PreservesMasks()
        {
            var holder = new TargetFilterHolder
            {
                Filter = new UnitTargetFilter
                {
                    TeamRule = TeamQueryRule.EnemyOnly,
                    UnitKindMask = UnitKindMask.All,
                    LifeStateMask = UnitLifeStateMask.AliveOnly,
                    RequireTargetable = true,
                },
            };

            string json = JsonUtility.ToJson(holder);
            TargetFilterHolder restored =
                JsonUtility.FromJson<TargetFilterHolder>(json);

            Assert.That(
                restored.Filter.UnitKindMask.Contains(
                    UnitKind.Structure),
                Is.True);
            Assert.That(
                restored.Filter.LifeStateMask.Contains(
                    LifeState.Alive),
                Is.True);
            Assert.That(
                restored.Filter.LifeStateMask.Contains(
                    LifeState.Dead),
                Is.False);
        }

        [Test]
        public void SpawnProjectileAuthoring_Bake_ProducesValidStageDef()
        {
            var authoring = new SpawnProjectileStageDefAuthoring();

            var def = authoring.Bake();

            Assert.That(def, Is.InstanceOf<SpawnProjectileStageDef>());
            var projDef = (SpawnProjectileStageDef)def;
            Assert.That(projDef.ProjectileDefId, Is.EqualTo(1));
        }

        [Test]
        public void ApplyBuffAuthoring_Bake_ProducesValidStageDef()
        {
            var authoring = new ApplyBuffStageDefAuthoring();

            var def = authoring.Bake();

            Assert.That(def, Is.InstanceOf<ApplyBuffStageDef>());
            var buffDef = (ApplyBuffStageDef)def;
            Assert.That(buffDef.BuffConfigId.Value, Is.EqualTo(1));
            Assert.That(buffDef.TargetRule, Is.EqualTo(BuffTargetRule.Self));
        }

        [Test]
        public void DashAuthoring_Bake_ProducesValidStageDef()
        {
            var authoring = new DashStageDefAuthoring();
            FieldInfo stageKeyField =
                typeof(StageDefAuthoring).GetField(
                    "stageKey",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(stageKeyField, Is.Not.Null);
            stageKeyField.SetValue(
                authoring,
                (byte)4);

            var def = authoring.Bake();

            Assert.That(def, Is.InstanceOf<DashStageDef>());
            var dashDef = (DashStageDef)def;
            Assert.That(dashDef.SpeedPerTick, Is.GreaterThan(fp.zero));
            Assert.That(dashDef.TotalDistance, Is.GreaterThan(fp.zero));
        }

        private static UnitPrototype CreateTestPrototype()
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
                UnitPrototypeId = 7,
                RuntimeEntityPrefabId = 1007,
                UnitKind = UnitKind.Hero,
                BaseStats = preset,
                Loadout = HandlerLoadout.DefaultHero,
            };
        }

        [Test]
        public void AreaDamageStageDef_OnExit_DoesNotThrow()
        {
            var def = new AreaDamageStageDef();
            var runtime = new AbilityRuntime();
            var session = runtime.BeginSession(1, 0, AimSnapshot.None);

            Assert.DoesNotThrow(() => def.OnExit(session, runtime));
        }

        [Test]
        public void SpawnProjectileStageDef_OnExit_DoesNotThrow()
        {
            var def = new SpawnProjectileStageDef();
            var runtime = new AbilityRuntime();
            var session = runtime.BeginSession(1, 0, AimSnapshot.None);

            Assert.DoesNotThrow(() => def.OnExit(session, runtime));
        }

        [Test]
        public void ApplyBuffStageDef_OnExit_DoesNotThrow()
        {
            var def = new ApplyBuffStageDef();
            var runtime = new AbilityRuntime();
            var session = runtime.BeginSession(1, 0, AimSnapshot.None);

            Assert.DoesNotThrow(() => def.OnExit(session, runtime));
        }

        [Test]
        public void DashStageDef_OnExit_DoesNotThrow()
        {
            var def = new DashStageDef();
            var runtime = new AbilityRuntime();
            var session = runtime.BeginSession(1, 0, AimSnapshot.None);

            Assert.DoesNotThrow(() => def.OnExit(session, runtime));
        }

        [Serializable]
        private sealed class TargetFilterHolder
        {
            public UnitTargetFilter Filter;
        }
    }
}

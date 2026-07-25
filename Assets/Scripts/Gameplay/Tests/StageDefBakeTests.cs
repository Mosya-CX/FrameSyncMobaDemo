using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

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
            var runtime = new AbilityRuntime
            {
                Definition = new AbilityDef { AbilityId = 1 },
            };
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

            var def = authoring.Bake();

            Assert.That(def, Is.InstanceOf<DashStageDef>());
            var dashDef = (DashStageDef)def;
            Assert.That(dashDef.SpeedPerTick, Is.GreaterThan(fp.zero));
            Assert.That(dashDef.TotalDistance, Is.GreaterThan(fp.zero));
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
    }
}

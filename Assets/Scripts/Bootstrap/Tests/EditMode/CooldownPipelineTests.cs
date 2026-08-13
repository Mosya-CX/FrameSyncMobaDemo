using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.LuaBridge;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// Tests verifying the cooldown data pipeline from AbilityHandler
    /// through UiSnapshotDto to LuaDataCache (ExecPlan 0089).
    /// </summary>
    public class CooldownPipelineTests
    {
        private SimulationTickContextController _tickController;

        [SetUp]
        public void SetUp()
        {
            _tickController = new SimulationTickContextController();
            _tickController.BeginTick(0, ExecutionMode.ServerAuthority);
        }

        [TearDown]
        public void TearDown()
        {
            _tickController.EndTick();
            _tickController = null;
        }

        [Test]
        public void UiSnapshotDto_Default_AllCooldownsZero()
        {
            var dto = UiSnapshotDto.Empty;
            Assert.That(dto.CooldownRemaining0, Is.EqualTo(0));
            Assert.That(dto.CooldownRemaining1, Is.EqualTo(0));
            Assert.That(dto.CooldownRemaining2, Is.EqualTo(0));
            Assert.That(dto.CooldownRemaining3, Is.EqualTo(0));
        }

        [Test]
        public void LuaDataCache_CooldownRemaining_ReturnsZeroForInvalidSlot()
        {
            Assert.That(LuaDataCache.CooldownRemaining(-1), Is.EqualTo(0));
            Assert.That(LuaDataCache.CooldownRemaining(4), Is.EqualTo(0));
        }

        [Test]
        public void LuaDataCache_CooldownTotal_ReturnsOneForInvalidSlot()
        {
            Assert.That(LuaDataCache.CooldownTotal(-1), Is.EqualTo(1));
            Assert.That(LuaDataCache.CooldownTotal(4), Is.EqualTo(1));
        }

        [Test]
        public void LuaDataCache_SetsAndReadsCooldown()
        {
            var dto = new UiSnapshotDto
            {
                CooldownRemaining0 = 100,
                CooldownTotal0 = 200,
                CooldownRemaining1 = 50,
                CooldownTotal1 = 120,
                CooldownRemaining2 = 0,
                CooldownTotal2 = 80,
                CooldownRemaining3 = 30,
                CooldownTotal3 = 60,
                MaxHealth = fp.one,
            };
            LuaDataCache.Latest = dto;

            Assert.That(LuaDataCache.CooldownRemaining(0), Is.EqualTo(100));
            Assert.That(LuaDataCache.CooldownTotal(0), Is.EqualTo(200));
            Assert.That(LuaDataCache.CooldownRemaining(1), Is.EqualTo(50));
            Assert.That(LuaDataCache.CooldownTotal(1), Is.EqualTo(120));
            Assert.That(LuaDataCache.CooldownRemaining(2), Is.EqualTo(0));
            Assert.That(LuaDataCache.CooldownTotal(2), Is.EqualTo(80));
            Assert.That(LuaDataCache.CooldownRemaining(3), Is.EqualTo(30));
            Assert.That(LuaDataCache.CooldownTotal(3), Is.EqualTo(60));
        }

        [Test]
        public void LuaDataCache_HasValidData_TrueWhenMaxHealthPositive()
        {
            var dto = new UiSnapshotDto { MaxHealth = fp.zero };
            LuaDataCache.Latest = dto;
            Assert.That(LuaDataCache.HasValidData, Is.False);

            dto.MaxHealth = fp.one;
            LuaDataCache.Latest = dto;
            Assert.That(LuaDataCache.HasValidData, Is.True);
        }

        [Test]
        public void KnockBackArc_IsHalfKnockUpArc()
        {
            float knockUp =
                CrowdControlVerticalMotionPresenter
                    .EvaluateArcHeight(
                        10,
                        40,
                        25,
                        30f,
                        false);
            float knockBack =
                CrowdControlVerticalMotionPresenter
                    .EvaluateArcHeight(
                        10,
                        40,
                        25,
                        30f,
                        true);
            Assert.That(knockUp, Is.GreaterThan(0f));
            Assert.That(knockBack,
                Is.EqualTo(knockUp * 0.5f).Within(.0001f));
        }
    }
}

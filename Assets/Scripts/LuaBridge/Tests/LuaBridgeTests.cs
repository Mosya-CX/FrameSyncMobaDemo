using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.LuaBridge.Tests
{
    [TestFixture]
    public class LuaRuntimeTests
    {
        [Test]
        public void SetGlobal_Int_CanRetrieve()
        {
            var rt = new LuaRuntime();
            rt.SetGlobal("TestValue", 42);
            Assert.That(rt.TryGetGlobal<int>("TestValue", out int val), Is.True);
            Assert.That(val, Is.EqualTo(42));
        }

        [Test]
        public void SetGlobal_Fp_CanRetrieve()
        {
            var rt = new LuaRuntime();
            fp value = (fp)3.14f;
            rt.SetGlobal("Pi", value);
            Assert.That(rt.TryGetGlobal<fp>("Pi", out fp val), Is.True);
            Assert.That(val, Is.EqualTo(value));
        }

        [Test]
        public void SetTableField_CanRetrieve()
        {
            var rt = new LuaRuntime();
            rt.SetTableField("HUD", "CurrentHealth", (fp)100);
            Assert.That(rt.TryGetTableField<fp>("HUD", "CurrentHealth", out fp val), Is.True);
            Assert.That(val, Is.EqualTo((fp)100));
        }

        [Test]
        public void Clear_RemovesAllData()
        {
            var rt = new LuaRuntime();
            rt.SetGlobal("X", 1);
            rt.SetTableField("HUD", "Gold", 500);
            rt.Clear();
            Assert.That(rt.TryGetGlobal<int>("X", out _), Is.False);
            Assert.That(rt.TryGetTableField<int>("HUD", "Gold", out _), Is.False);
        }

        [Test]
        public void MissingKey_ReturnsFalse()
        {
            var rt = new LuaRuntime();
            Assert.That(rt.TryGetGlobal<int>("NonExistent", out _), Is.False);
            Assert.That(rt.TryGetTableField<int>("HUD", "Missing", out _), Is.False);
        }
    }

    [TestFixture]
    public class UiSnapshotPopulationTests
    {
        [Test]
        public void Empty_IsDefault()
        {
            var dto = UiSnapshotDto.Empty;
            Assert.That(dto.CurrentHealth, Is.EqualTo(fp.zero));
            Assert.That(dto.MaxHealth, Is.EqualTo(fp.zero));
            Assert.That(dto.CurrentGold, Is.EqualTo(0));
        }

        [Test]
        public void Populated_FieldsAreSet()
        {
            var dto = new UiSnapshotDto
            {
                CurrentHealth = (fp)100,
                MaxHealth = (fp)200,
                CurrentGold = 500,
                CooldownRemaining0 = 30,
                CooldownTotal0 = 60,
                UnitLevel = 5,
            };

            Assert.That(dto.CurrentHealth, Is.EqualTo((fp)100));
            Assert.That(dto.MaxHealth, Is.EqualTo((fp)200));
            Assert.That(dto.CurrentGold, Is.EqualTo(500));
            Assert.That(dto.CooldownRemaining0, Is.EqualTo(30));
            Assert.That(dto.CooldownTotal0, Is.EqualTo(60));
            Assert.That(dto.UnitLevel, Is.EqualTo(5));
        }
    }

    [TestFixture]
    public class LuaBridgePushTests
    {
        [Test]
        public void PushTickData_WritesHudTable()
        {
            var bridge = new GameObject("TestBridge").AddComponent<LuaBridge>();
            var dto = new UiSnapshotDto
            {
                CurrentHealth = (fp)80,
                MaxHealth = (fp)100,
                CurrentGold = 350,
                CooldownRemaining0 = 15,
                CooldownTotal0 = 30,
            };

            bridge.PushTickData(42, dto, null);

            var rt = bridge.Runtime;
            Assert.That(rt.TryGetTableField<fp>("HUD", "CurrentHealth", out fp hp), Is.True);
            Assert.That(hp, Is.EqualTo((fp)80));
            Assert.That(rt.TryGetTableField<fp>("HUD", "MaxHealth", out fp maxHp), Is.True);
            Assert.That(maxHp, Is.EqualTo((fp)100));
            Assert.That(rt.TryGetTableField<int>("HUD", "CurrentGold", out int gold), Is.True);
            Assert.That(gold, Is.EqualTo(350));
            Assert.That(rt.TryGetTableField<int>("HUD", "CooldownRemaining0", out int cdRem), Is.True);
            Assert.That(cdRem, Is.EqualTo(15));
            Assert.That(rt.TryGetTableField<int>("HUD", "CooldownTotal0", out int cdTot), Is.True);
            Assert.That(cdTot, Is.EqualTo(30));
            Assert.That(rt.TryGetGlobal<int>("CurrentTick", out int tick), Is.True);
            Assert.That(tick, Is.EqualTo(42));
        }

        [Test]
        public void PushTickData_ClearsPreviousData()
        {
            var bridge = new GameObject("TestBridge").AddComponent<LuaBridge>();
            var dto1 = new UiSnapshotDto { CurrentHealth = (fp)100 };
            bridge.PushTickData(1, dto1, null);
            Assert.That(bridge.Runtime.TryGetTableField<fp>("HUD", "CurrentHealth", out fp v1), Is.True);

            var dto2 = new UiSnapshotDto { CurrentHealth = (fp)50 };
            bridge.PushTickData(2, dto2, null);
            Assert.That(bridge.Runtime.TryGetTableField<fp>("HUD", "CurrentHealth", out fp v2), Is.True);
            Assert.That(v2, Is.EqualTo((fp)50));
        }

        [Test]
        public void PushTickDataWithBindings_AppliesOverrides()
        {
            var bridge = new GameObject("TestBridge").AddComponent<LuaBridge>();
            var dto = new UiSnapshotDto
            {
                CurrentHealth = (fp)75,
                CurrentGold = 200,
            };

            var bindings = new System.Collections.Generic.Dictionary<string, string>
            {
                { "CurrentHealth", "CustomHUD.Health" },
                { "CurrentGold", "CustomHUD.Money" },
            };

            bridge.PushTickDataWithBindings(5, dto, null, bindings);

            var rt = bridge.Runtime;
            // Health is stored as float after ResolveGameplayField conversion
            Assert.That(rt.TryGetTableField<float>("CustomHUD", "Health", out float hp), Is.True);
            Assert.That(hp, Is.EqualTo(75f).Within(0.01f));
            Assert.That(rt.TryGetTableField<int>("CustomHUD", "Money", out int gold), Is.True);
            Assert.That(gold, Is.EqualTo(200));
        }
    }
}

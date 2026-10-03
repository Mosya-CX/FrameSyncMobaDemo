# 单位生命状态权威状态机

## 本次执行范围

本计划对应原编码 0011 的一次执行：单位生命状态权威状态机。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [同步生成死亡复活与回池](../../requirements/units/REQ-FEAT-028_unit-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [AI 注册调度与多态快照](../../requirements/non-heroes/REQ-FEAT-068_ai-scheduling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/LifeState.cs`：`LifeState`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/Unit/Core/CapabilityState.cs`：`CapabilityState`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/LifeState.cs`：

```csharp
namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit lifecycle state frozen by Unit v27.3 section 1.8.
    /// Unit stores LifeState; UnitWorld is the sole writer and transition validator.
    /// </summary>
    public enum LifeState : byte
    {
        Alive = 0,
        Dying = 1,
        Dead = 2,
        Respawning = 3,
    }
}
```

`Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：

```csharp
        internal void ApplyLifeStateFromUnitWorld(LifeState newState)
        {
            LifeState = newState;
        }
```

### 输入输出与边界

**同步生成死亡复活与回池**

UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。

ClearForDeath、ClearForRespawn、ClearForDespawn 按固定顺序；永久 Buff 与装备跨死亡保留所属状态；回池新生命周期不沿用旧身份。

**单位根与能力装配**

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

**AI 注册调度与多态快照**

UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。

不增加通用模拟按键层；AI Runtime 与管理者状态区分；死亡注销不能在恢复时静默漏建。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/CapabilityStateTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `NewlyConstructedUnit_HasAllCapabilitiesTrue`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void NewlyConstructedUnit_HasAllCapabilitiesTrue()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);

            Assert.That(unit.CapabilityState.CanMove, Is.True);
            Assert.That(unit.CapabilityState.CanAttack, Is.True);
            Assert.That(unit.CapabilityState.CanCast, Is.True);
            Assert.That(unit.CapabilityState.CanTurn, Is.True);
            Assert.That(unit.CapabilityState.IsTargetable, Is.True);
        }
```
- `Assets/Scripts/Gameplay/Tests/LifeStateMachineTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `NewlyConstructedUnit_HasLifeStateAlive`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void NewlyConstructedUnit_HasLifeStateAlive()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);

            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Alive));
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitWorldIntegrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SpawnMultipleKinds_GetByKind`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SpawnMultipleKinds_GetByKind()
        {
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(minionProto, TeamId.Neutral, 1, 0m, 0m);

            var heroes = world.GetUnitsByKind(UnitKind.Hero);
            var minions = world.GetUnitsByKind(UnitKind.Minion);
            var all = world.GetAllUnits();

            Assert.AreEqual(2, heroes.Count);
            Assert.AreEqual(1, minions.Count);
            Assert.AreEqual(3, all.Count);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

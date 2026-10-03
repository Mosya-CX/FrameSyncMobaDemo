# 逻辑 Tick 上下文与随机状态

## 本次执行范围

本计划对应原编码 0001 的一次执行：逻辑 Tick 上下文与随机状态。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [确定性随机与定点计算](../../requirements/determinism/REQ-FEAT-014_deterministic-random.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [逻辑时钟与 Tick 推进](../../requirements/frame-sync/REQ-FEAT-005_simulation-tick.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

唯一随机服务维护显式 Snapshot 状态，集合采样定义稳定顺序；权威类型为 Unity.Mathematics.FixedPoint.fp，作者 float 仅在验证/Bake 边界转换一次。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Deterministic/Core/ExecutionMode.cs`：`ExecutionMode`。
- `Assets/Scripts/Deterministic/Core/SimulationTickContext.cs`：`SimulationTickContext`。
- `Assets/Scripts/Deterministic/Random/DeterministicRandomSnapshot.cs`：`DeterministicRandomSnapshot`。
- `Assets/Scripts/Deterministic/Random/DeterministicRandomService.cs`：`DeterministicRandomService`。
- `Assets/Scripts/Deterministic/Core/SimulationTickContextController.cs`：`SimulationTickContextController`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Deterministic/Core/ExecutionMode.cs`：

```csharp
namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Identifies why the current deterministic Gameplay Tick is executing.
    /// </summary>
    public enum ExecutionMode
    {
        ServerAuthority = 0,
        ClientPrediction = 1,
        ClientReplay = 2,
    }
}
```

`Assets/Scripts/Deterministic/Core/SimulationTickContext.cs`：

```csharp
using System;

namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Single immutable simulation-time view published by the FrameSync pipeline.
    /// </summary>
    public readonly struct SimulationTickContext : IEquatable<SimulationTickContext>
    {
        private static SimulationTickContext current =
            new SimulationTickContext(
                0,
                ExecutionMode.ServerAuthority);
        private static bool isTickActive;

        internal SimulationTickContext(int tick, ExecutionMode executionMode)
        {
            Tick = tick;
            DeltaTick = 1;
            ExecutionMode = executionMode;
        }

        public static SimulationTickContext Current => current;

        public int Tick { get; }

        public int DeltaTick { get; }

        public ExecutionMode ExecutionMode { get; }

        internal static bool IsTickActive => isTickActive;

        internal static void SetCurrent(SimulationTickContext value)
        {
            if (isTickActive)
            {
                throw new InvalidOperationException(
                    "A Gameplay Tick is already active. Nested Tick execution is not allowed.");
            }

            current = value;
            isTickActive = true;
        }

        internal static void CompleteCurrent()
        {
            if (!isTickActive)
            {
                throw new InvalidOperationException("No Gameplay Tick is active.");
            }

            isTickActive = false;
        }

        public bool Equals(SimulationTickContext other)
        {
            return Tick == other.Tick
                && DeltaTick == other.DeltaTick
                && ExecutionMode == other.ExecutionMode;
        }

        public override bool Equals(object obj)
        {
            return obj is SimulationTickContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**确定性随机与定点计算**

唯一随机服务维护显式 Snapshot 状态，集合采样定义稳定顺序；权威类型为 Unity.Mathematics.FixedPoint.fp，作者 float 仅在验证/Bake 边界转换一次。

不新增定点类型；动作暴击使用动作键纯哈希，不消耗此共享随机流；禁止 UnityEngine.Random。

**逻辑时钟与 Tick 推进**

ServerTick、LocalSimulationTick 都是下一待执行 Tick；LatestAuthorityFrameTick 是最近连续接受权威帧，SnapshotTick 是恢复后下一 Tick。SimulationTickContext 提供只读上下文。

预测领先上限和每 Unity 帧执行上限明确；重演不读取渲染耗时或输入设备。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Deterministic/Tests/DeterministicAssemblyBoundaryTests.cs`：EditMode，程序集 `FrameSyncMoba.Deterministic.Tests`，函数 `RuntimeAssembly_HasNoForbiddenDirectDependency`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RuntimeAssembly_HasNoForbiddenDirectDependency()
        {
            string[] references = typeof(DeterministicRandomService)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            foreach (string forbiddenPrefix in ForbiddenDirectReferences)
            {
                Assert.That(
                    references.Any(reference => reference.StartsWith(forbiddenPrefix, StringComparison.Ordinal)),
                    Is.False,
                    $"FrameSyncMoba.Deterministic directly references forbidden assembly prefix '{forbiddenPrefix}'.");
            }
        }
```
- `Assets/Scripts/Deterministic/Tests/DeterministicRandomServiceTests.cs`：EditMode，程序集 `FrameSyncMoba.Deterministic.Tests`，函数 `SameSeedAndCalls_ProduceIdenticalSequence`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SameSeedAndCalls_ProduceIdenticalSequence()
        {
            var first = new DeterministicRandomService(0x12345678u);
            var second = new DeterministicRandomService(0x12345678u);

            for (int index = 0; index < 64; index++)
            {
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            }
        }
```
- `Assets/Scripts/Deterministic/Tests/SimulationTickContextTests.cs`：EditMode，程序集 `FrameSyncMoba.Deterministic.Tests`，函数 `ExecutionMode_ValuesAreStable`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ExecutionMode_ValuesAreStable()
        {
            Assert.That((int)ExecutionMode.ServerAuthority, Is.EqualTo(0));
            Assert.That((int)ExecutionMode.ClientPrediction, Is.EqualTo(1));
            Assert.That((int)ExecutionMode.ClientReplay, Is.EqualTo(2));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

# 确定性随机几何操作

## 本次执行范围

本计划对应原编码 0007 的一次执行：确定性随机几何操作。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [定点形状与范围查询](../../requirements/spatial-physics/REQ-FEAT-059_spatial-geometry.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [确定性随机与定点计算](../../requirements/determinism/REQ-FEAT-014_deterministic-random.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PhysicsShape2D 及 geometry 定点窄相位；RangeQueryService 使用 TeamQueryRule、分类和完整目标过滤，再按指定正式键排序。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Deterministic/Random/DeterministicRandomService.cs`：`DeterministicRandomService`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。
- `Assets/Scripts/Deterministic/Random/DeterministicRandomSnapshot.cs`：`DeterministicRandomSnapshot`。
- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：`PhysicsEntity2D`。
- `Assets/Scripts/Physics/Core/PhysicsWorld.cs`：`PhysicsWorld`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Deterministic/Random/DeterministicRandomService.cs`：

```csharp
        public DeterministicRandomSnapshot Capture()
        {
            return new DeterministicRandomSnapshot(random.state);
        }
```

`Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：

```csharp
using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct AbilitySignal
    {
        public byte Slot;
        public AbilitySignalVerb Verb;
        public AimSnapshot Aim;
        public static readonly AbilitySignal None = default;
    }

    public enum AbilitySignalVerb : byte
    {
        Focus = 0,
        Commit = 1,
        Cancel = 2,
    }

    public enum AimKind : byte
    {
        None = 0,
        Self = 1,
        Point = 2,
        Unit = 3,
        Direction = 4,
    }

    public readonly struct AimSnapshot : IEquatable<AimSnapshot>
    {
        public readonly AimKind Kind;
        public readonly UnitUid TargetUnitUid;
        public readonly fp2 TargetPoint;
        public readonly fp2 Direction;

        private AimSnapshot(
            AimKind kind,
            UnitUid targetUnitUid,
            fp2 targetPoint,
            fp2 direction)
        {
            Kind = kind;
            TargetUnitUid = targetUnitUid;
            TargetPoint = targetPoint;
            Direction = direction;
        }

        public static AimSnapshot Self => new AimSnapshot(
            AimKind.Self, default, default, default);

        public static AimSnapshot ForPoint(fp2 targetPoint) => new AimSnapshot(
            AimKind.Point, default, targetPoint, default);

        public static AimSnapshot ForUnit(UnitUid targetUnitUid)
        {
            if (!targetUnitUid.IsValid())
            {
                throw new ArgumentException("Unit aim requires a valid UnitUid.", nameof(targetUnitUid));
            }

            return new AimSnapshot(AimKind.Unit, targetUnitUid, default, default);
        }

        public static AimSnapshot ForDirection(fp2 direction)
        {
            if (!Physics.PhysicsGeometry2D.TryCreateFacing(
                    direction, out fp2 normalized, out _))
            {
                throw new ArgumentException(
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**定点形状与范围查询**

PhysicsShape2D 及 geometry 定点窄相位；RangeQueryService 使用 TeamQueryRule、分类和完整目标过滤，再按指定正式键排序。

不是 Unity 物理权威；边缘接触、零半径、退化线段、旋转矩形和候选去重都有明确结果。

**确定性随机与定点计算**

唯一随机服务维护显式 Snapshot 状态，集合采样定义稳定顺序；权威类型为 Unity.Mathematics.FixedPoint.fp，作者 float 仅在验证/Bake 边界转换一次。

不新增定点类型；动作暴击使用动作键纯哈希，不消耗此共享随机流；禁止 UnityEngine.Random。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

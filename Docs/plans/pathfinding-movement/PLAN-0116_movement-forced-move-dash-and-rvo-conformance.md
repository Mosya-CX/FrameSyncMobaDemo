# 强制移动冲刺与 RVO

## 本次执行范围

本计划对应原编码 0116 的一次执行：强制移动冲刺与 RVO。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [普通移动冲刺与强制位移](../../requirements/pathfinding-movement/REQ-FEAT-067_movement-pipeline.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [路线选择与跟随状态](../../requirements/pathfinding-movement/REQ-FEAT-063_path-following.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [确定性局部避让](../../requirements/pathfinding-movement/REQ-FEAT-066_local-avoidance.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

MovementHandler 依正式优先级执行 Route、Dash 和 ResolvedForcedMove；ForcedMove 胜者仅由控制系统选出。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Movement/MovementSnapshot.cs`：`MovementSnapshot`。
- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：`PhysicsEntity2D`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。
- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlHandler.cs`：`CrowdControlHandler`。
- `Assets/Scripts/Gameplay/Movement/MovementHandler.cs`：`MovementHandler`、`MovementMode`。
- `Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs`：`UnitLocomotionAgent`、`value`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Physics/Core/PhysicsWorld.cs`：`PhysicsWorld`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Movement/MovementSnapshot.cs`：

```csharp
namespace FrameSyncMoba.Unit
{
    public struct MovementSnapshot
    {
        public DashRuntime Dash;
        public ForcedMoveRuntime ForcedMove;

        public bool IsDashing => Dash.IsActive;

        public static readonly MovementSnapshot Default = new MovementSnapshot
        {
            Dash = default,
            ForcedMove = default,
        };
    }
}
```

`Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：

```csharp
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// MonoBehaviour component owning the authoritative 2D logical transform.
    /// Pathfinding Design v13.1 v13.1 patch note:
    /// "冻结所有帧同步 GameObject 的 Unity Transform 唯一写入点为 PhysicsEntity2D.LateUpdate"
    /// </summary>
    public sealed class PhysicsEntity2D : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("When enabled, LateUpdate syncs the logical Transform2D to the Unity Transform.")]
        private bool syncTransform = true;

        private Vector3 presentationStartPosition;
        private Vector3 presentationTargetPosition;
        private Quaternion presentationStartRotation = Quaternion.identity;
        private Quaternion presentationTargetRotation = Quaternion.identity;
        private float presentationPositionElapsed;
        private float presentationRotationElapsed;
        private bool presentationInitialized;
        private bool presentationSnapRequested = true;

        public PhysicsTransform2D Transform2D { get; private set; }

        public PhysicsShape2D Shape { get; private set; }

        public PhysicsBounds2D Bounds { get; private set; }

        /// <summary>
        /// Query identity metadata (Physics v13.1 section 2.3).
        /// Set once at registration via <see cref="SetQueryInfo"/>; read-only after.
        /// </summary>
        public PhysicsEntityQueryInfo QueryInfo { get; private set; }

        public void SetQueryInfo(in PhysicsEntityQueryInfo queryInfo)
        {
            QueryInfo = queryInfo;
        }

        public void SetLogicPosition(fp2 position)
        {
            var transform = new PhysicsTransform2D(
                position,
                Transform2D.Position,
                Transform2D.Forward,
                Transform2D.Right);
            CommitTransform(transform);
        }

        public void SetLogicPose(fp2 position, fp2 forward)
        {
            fp2 nextForward = Transform2D.Forward;
            fp2 nextRight = Transform2D.Right;
            if (PhysicsGeometry2D.TryCreateFacing(forward, out fp2 normalized, out fp2 right))
            {
                nextForward = normalized;
                nextRight = right;
            }

            var transform = new PhysicsTransform2D(
                position,
                Transform2D.Position,
                nextForward,
                nextRight);
            CommitTransform(transform);
        }

// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**普通移动冲刺与强制位移**

MovementHandler 依正式优先级执行 Route、Dash 和 ResolvedForcedMove；ForcedMove 胜者仅由控制系统选出。

死亡清理移动任务；仲裁决定占用而不让技能控制直接写 Transform；墙体约束与异常挤出分开。

**路线选择与跟随状态**

UnitLocomotionAgent/RouteResolver 按 MovePurpose、目标变化和路径偏离决策；PathFollower2D 输出 LocomotionResult，不直接写空间。

控制打断不重写 Order；恢复保留正式路线状态或明确重建可派生结果；追踪目标失效按正式边界返回。

**确定性局部避让**

DeterministicRVOSystem 读取移动前 RvoGrid，以固定候选速度和稳定约束求解；全部结果计算后再应用。

技术遍历不形成先移动优势；零速度、重叠、狭窄通道和速度上限有可判定边界。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/FrameSync/Tests/SnapshotChecksumCompletenessTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `AggregateSnapshot_RestoresIntentDashAndLocomotion`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void AggregateSnapshot_RestoresIntentDashAndLocomotion()
        {
            UnitWorld world = CreateWorld(withPathGrid: true);
            UnitType source = Spawn(world, 100, 0);
            UnitType target = Spawn(world, 101, 0);
            source.Planner.SetIntent(new UnitIntent
            {
                Kind = IntentKind.AttackTarget,
                TargetUnit = target.UnitUid,
                AllowChase = true,
                AllowReplan = true,
            });

            var tick = new SimulationTickContextController();
            tick.BeginTick(2, ExecutionMode.ServerAuthority);
            try
            {
                source.MovementHandler.ApplyDash(
                    new fp2(fp.one, fp.zero), (fp)8, (fp)4);
                Assert.That(
                    source.Locomotion.AcceptRouteRequest(
                        RouteMoveRequest.ToPosition(new fp2(9, 3), (fp)0.5m)),
                    Is.EqualTo(MoveAcceptResult.Accepted));
                var spec = new ActionStartSpec(
                    ActionSlot.Base,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionInterruptLevel.Ordinary,
                    true,
                    false);
                source.ActionRuntimes.Start(ActionKind.Move, spec);
            }
            finally
            {
                tick.EndTick();
            }

            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld);
            GameplaySnapshot snapshot = pipeline.CaptureAggregateSnapshot();

            source.Planner.ClearIntent();
            source.MovementHandler.Restore(MovementSnapshot.Default);
            source.Locomotion.CancelRoute(MoveCancelReason.UserCommand);
            source.ActionRuntimes.ClearWithoutCancel();

            pipeline.RestoreFromSnapshot(snapshot, 3);
// 方法后续请阅读上述真实源码；这里是节选。
```
- `Assets/Scripts/Gameplay/Tests/MovementConformanceTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `ForcedMove_OverridesRouteMove`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ForcedMove_OverridesRouteMove()
        {
            Unit unit = CreateUnit(100, fp2.zero);
            RegisterKnockBack(unit);
            CrowdControlAddResult added =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    CreateForcedMoveParams(
                        new fp2(fp.one, fp.zero),
                        2,
                        (short)5));
            Assert.That(added.Added, Is.True);

            unit.MovementHandler.ApplyRouteMovement(
                new LocomotionResult
                {
                    UnitUid = unit.UnitUid,
                    HasMovement = true,
                    DesiredDirection =
                        new fp2(fp.zero, fp.one),
                    DesiredSpeed = (fp)4,
                });
            unit.MovementHandler.TickUpdate();

            Assert.That(
                unit.PhysicsEntity.Transform2D.Position,
                Is.EqualTo(
                    new fp2(fp.one, fp.zero)));
        }
```
- `Assets/Scripts/Gameplay/Tests/MovementHandlerTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Constructor_SetsInitialPosition`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Constructor_SetsInitialPosition()
        {
            var h = UnitTestFactory.CreateMovementHandler(new fp2(10m, 20m), 3m);

            Assert.AreEqual(new fp2(10m, 20m), h.Position);
            Assert.AreEqual((fp)3m, h.MoveSpeed);
            Assert.AreEqual(fp2.zero, h.Velocity);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

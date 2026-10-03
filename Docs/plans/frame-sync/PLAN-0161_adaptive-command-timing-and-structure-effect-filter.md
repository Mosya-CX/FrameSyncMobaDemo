# 自适应命令时间与结构效果过滤

## 本次执行范围

本计划对应原编码 0161 的一次执行：自适应命令时间与结构效果过滤。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [命令序列化与类型化派发](../../requirements/frame-sync/REQ-FEAT-007_command-dispatch.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命令合并转发与幂等重发](../../requirements/frame-sync/REQ-FEAT-008_command-forwarding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [自适应命令目标 Tick](../../requirements/frame-sync/REQ-FEAT-009_command-timing.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GameplayCommand 采用唯一 CommandHeader 与强类型负载、规范字节序；CommandDispatcher 按正式 Command 类型进入所属系统。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/FrameSync/CommandTargetTickResolver.cs`：`CommandTargetTickResolver`。
- `Assets/Scripts/Bootstrap/FrameSyncNetworkBridge.cs`：`FrameSyncNetworkBridge`、`PresentationPingTracker`、`FrameSyncWireCodec`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：`BuffHandler`、`BuffReactionKind`。
- `Assets/Scripts/Gameplay/Unit/Kind/UnitKind.cs`：`UnitKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [DisallowMultipleComponent]
    public sealed class Unit : MonoBehaviour, IUnitCollisionParticipant
    {
        [Header("Deterministic composition")]
        [Tooltip("Authoritative 2D physics component owned by this Unit prefab.")]
        [SerializeField] private PhysicsEntity2D physicsEntity;
        [SerializeField] private StatHandler statHandler;
        [SerializeField] private MovementHandler movementHandler;
        [SerializeField] private AttackHandler attackHandler;
        [SerializeField] private AbilityHandler abilityHandler;
        [SerializeField] private BuffHandler buffHandler;
        [SerializeField] private CrowdControlHandler crowdControlHandler;
        [SerializeField] private EquipmentHandler equipmentHandler;

        private CapabilityState capabilityState;
        private UnitAbilityMask abilityMask;
        private readonly List<UnitTag> tags =
            new List<UnitTag>();

        /// <summary>Deterministic runtime identity (SpawnLogicTick /
        /// prefab id / spawn sequence). Displayed in the Inspector for
        /// debugging spawned unit instances.</summary>
        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-120)]
        public UnitUid UnitUid { get; private set; }
        public GameplayParticipantId GameplayParticipantId { get; private set; }
        public UnitWorld World { get; internal set; }
        public UnitUid OwnerUid { get; private set; }
        public UnitKind UnitKind { get; private set; }
        public ushort UnitSubKindId { get; private set; }
        public TeamId TeamId { get; private set; }
        public int UnitPrototypeId { get; private set; }
        public int BaseGoldValue { get; private set; }
        public int BaseExperienceValue { get; private set; }
        public int BaseCreepScoreValue { get; private set; }
        public LifeState LifeState { get; private set; }
        public ref readonly CapabilityState CapabilityState => ref capabilityState;
        public UnitAbilityMask AbilityMask => abilityMask;

        public PhysicsEntity2D PhysicsEntity => physicsEntity;
        public StatHandler StatHandler => statHandler;
        public CombatModifierSet CombatModifiers { get; private set; }
        public MovementHandler MovementHandler => movementHandler;
        public AttackHandler AttackHandler => attackHandler;
        public AbilityHandler AbilityHandler => abilityHandler;
        public BuffHandler BuffHandler => buffHandler;
        public CrowdControlHandler CrowdControl => crowdControlHandler;
        public EquipmentHandler EquipmentHandler => equipmentHandler;
        public UnitEventBus EventBus { get; private set; }

        public UnitIntent Intent { get => Planner?.CurrentIntent ?? UnitIntent.None; internal set => Planner?.SetIntent(value); }
        public BehaviorPlanner Planner { get; private set; }
        public ActionArbiter Arbiter { get; private set; }
        public ActionRuntimeSet ActionRuntimes { get; private set; }

        public UnitLocomotionAgent Locomotion { get; internal set; }
        public int Level => statHandler?.Level ?? 1;
        /// <summary>
        /// The deterministic home spawn position captured when this runtime
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/FrameSync/CommandTargetTickResolver.cs`：

```csharp
using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Owns the formal local Command target-Tick formula. PlayerInput supplies
    /// intent only and never chooses a Tick itself. Network timing is an
    /// optional client-side lower bound; the static formula remains the
    /// cold-start and stale-sample fallback.
    /// </summary>
    public sealed class CommandTargetTickResolver
    {
        private readonly Func<int> localSimulationTickProvider;
        private readonly Func<int> latestSynchronizedServerTickProvider;
        private readonly int minCommandLeadTicks;
        private readonly int maxFutureCommandTicks;
        private readonly ICommandNetworkTimingProvider networkTimingProvider;
        private bool hasCachedTargetTick;
        private int cachedBuildLocalTick = -1;
        private int cachedTargetTick = -1;

        public CommandTargetTickResolver(
            Func<int> localSimulationTickProvider,
            Func<int> latestSynchronizedServerTickProvider,
            int minCommandLeadTicks,
            int maxFutureCommandTicks,
            ICommandNetworkTimingProvider networkTimingProvider = null)
        {
            this.localSimulationTickProvider = localSimulationTickProvider
                ?? throw new ArgumentNullException(
                    nameof(localSimulationTickProvider));
            this.latestSynchronizedServerTickProvider =
                latestSynchronizedServerTickProvider
                ?? throw new ArgumentNullException(
                    nameof(latestSynchronizedServerTickProvider));
            if (minCommandLeadTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(minCommandLeadTicks));
            if (maxFutureCommandTicks <= 0 ||
                minCommandLeadTicks > maxFutureCommandTicks)
                throw new ArgumentOutOfRangeException(
                    nameof(maxFutureCommandTicks));
            this.minCommandLeadTicks = minCommandLeadTicks;
            this.maxFutureCommandTicks = maxFutureCommandTicks;
            this.networkTimingProvider = networkTimingProvider;
        }

        public int ResolveTargetTick(out int buildLocalTick)
        {
            buildLocalTick = localSimulationTickProvider();
            if (hasCachedTargetTick &&
                buildLocalTick == cachedBuildLocalTick)
                return cachedTargetTick;

            int latestSynchronizedServerTick =
                latestSynchronizedServerTickProvider();
            if (buildLocalTick < 0 || latestSynchronizedServerTick < -1)
            {
                throw new DeterministicSimulationException(
                    "Command Tick sources must be non-negative, except the initial synchronized server Tick may be -1.");
            }

            int nextLocalTick;
            int leadTick;
            int latestAllowedTick;
            try
            {
                nextLocalTick = checked(buildLocalTick + 1);
                leadTick = checked(
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**命令序列化与类型化派发**

GameplayCommand 采用唯一 CommandHeader 与强类型负载、规范字节序；CommandDispatcher 按正式 Command 类型进入所属系统。

完整 canonical 字节参与对账；无效单位或槽位不能按示例静默修复；输入事件仅翻译一次。

**命令合并转发与幂等重发**

CommandCollector 保持 TargetTick、CommandSeq 与来源身份，GameplayCommandBundle 和 AcceptedCommandRelay 保留规范顺序。

已执行预测 Tick 收到 Relay 时走正常脏 Tick 纠错；相同标识但不同字节必须报冲突。

**自适应命令目标 Tick**

静态下界=max(LocalSimulationTick+1, LatestSynchronizedServerTick+MinCommandLeadTicks)。RTT 使用整数 SRTT 与 RTTVar，按半 RTT、抖动预算、处理预算估计服务器 Tick，再以本地和估计服务器未来窗口封顶。

冷启动、样本过少或陈旧时返回静态下界；同一模拟 Tick 的命令复用一个 TargetTick；开局前样本年龄不能冒充 Gameplay 已推进。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/EditMode/PresentationPingTrackerTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `TryBegin_UsesConfiguredHalfSecondCadence`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void TryBegin_UsesConfiguredHalfSecondCadence()
        {
            var tracker = new PresentationPingTracker(500);

            Assert.IsTrue(tracker.TryBegin(10_000, out uint first));
            Assert.AreEqual(1u, first);
            Assert.IsFalse(tracker.TryBegin(10_499, out _));
            Assert.IsTrue(tracker.TryBegin(10_500, out uint second));
            Assert.AreEqual(2u, second);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

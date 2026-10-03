# 快照与共享校验完整性

## 本次执行范围

本计划对应原编码 0111 的一次执行：快照与共享校验完整性。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [预测回滚与逐帧重演](../../requirements/frame-sync/REQ-FEAT-011_rollback-replay.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [共享校验与分段诊断](../../requirements/frame-sync/REQ-FEAT-013_shared-checksum.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：`SharedGameplayChecksum`、`ChecksumSegment`、`StatEntryField`。
- `Assets/Scripts/Gameplay/Movement/MovementSnapshot.cs`：`MovementSnapshot`。
- `Assets/Scripts/Gameplay/Unit/Core/ActionRuntimeSet.cs`：`ActionRuntimeSet`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitIntent.cs`：`IntentKind`、`UnitIntent`。
- `Assets/Scripts/Bootstrap/ClientBootstrap.cs`：`ClientBootstrap`。
- `Assets/Scripts/Gameplay/Modifiers/CombatModifierSet.cs`：`CombatModifierSet`、`RecordComparer`、`CombatFormulaAccumulator`、`CombatPolicyResolution`。

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

`Assets/Scripts/FrameSync/GameplaySnapshot.cs`：

```csharp
using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    public struct UnitSnapshot
    {
        public UnitUid UnitUid;
        public GameplayParticipantId GameplayParticipantId;
        public UnitUid OwnerUid;
        public UnitKind UnitKind;
        public ushort UnitSubKindId;
        public TeamId TeamId;
        public int UnitPrototypeId;
        /// <summary>Home spawn position used when this unit respawns.</summary>
        public fp2 RespawnPosition;
        public LifeState LifeState;
        public CapabilityState CapabilityState;
        public HitReactionState HitReactionState;
        public UnitIntent IntentState;
        public ActionRuntimeSetSnapshot ActionRuntimeState;
        public PhysicsTransform2D PhysicsTransform;
        public PhysicsShape2D PhysicsShape;
        public StatHandlerSnapshot StatState;
        public CombatModifierSetSnapshot CombatModifierState;
        public AttackSnapshot AttackState;
        public MovementSnapshot MovementState;
        public AbilityHandlerSnapshot AbilityState;
        public BuffHandlerSnapshot BuffState;
        public CrowdControlHandlerSnapshot CCState;
        public LocomotionAgentSnapshot LocomotionState;
        public EquipmentHandlerSnapshot EquipmentState;
        public UnitTag[] Tags;
    }

    /// <summary>
    /// Snapshot of the entire UnitWorld for rollback.
    /// Uses T[] arrays per Snapshot Appendix v7.2 section 5.
    /// </summary>
    public struct UnitWorldSnapshot
    {
        public UnitSnapshot[] Units;
        public MinionSystemSnapshot MinionSystemState;
        public RespawnTimerSnapshot PendingUnitLifecycleState;
        public JungleCampSnapshot[] JungleCampStates;
        public UnitAIControllerSnapshot[] AIControllerStates;
        public int RuntimeRevision;

        public static UnitWorldSnapshot CreateEmpty() => new UnitWorldSnapshot
        {
            Units = Array.Empty<UnitSnapshot>(),
            JungleCampStates = Array.Empty<JungleCampSnapshot>(),
            AIControllerStates = Array.Empty<UnitAIControllerSnapshot>(),
        };
    }

    public struct GameplaySnapshot
    {
        public const int CurrentSchemaVersion = 25;
        public int SchemaVersion;

        public DeterministicRandomSnapshot RandomState;
        public MatchRuleRuntimeSnapshot MatchRuleState;
        public UnitWorldSnapshot UnitWorldState;
        public CombatSnapshot CombatState;
        public ProjectileWorldSnapshot ProjectileState;
        public EquipmentShopRuntimeSnapshot EquipmentShopState;
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**快照树与字段归属**

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

Tick 内工作缓存不进入快照；恢复不能猜测缺字段；版本与 GameplayDataVersion 同步推进。

**预测回滚与逐帧重演**

PredictionRollbackCoordinator 每 Tick 保存快照，普通回滚起点不早于 LatestAuthorityFrameTick+1；先 Restore，再 Resolve，最后 Rebuild。

不倒退已接受金币基线、不重读设备输入；失效确定性引用明确失败；每个 AuthorityFrame 形成单 Tick 接受屏障。

**共享校验与分段诊断**

SharedGameplayChecksum 规范序列化并按稳定键排序；-checksumDetail 输出分段和逐单位 Handler 摘要。

诊断不改变 Gameplay，不依赖集合插入顺序；StatSeq、配置 ID 与正式序列规则一致。

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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

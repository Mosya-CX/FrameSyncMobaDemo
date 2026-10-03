# 完整合同恢复

## 本次执行范围

本计划对应原编码 0046 的一次执行：完整合同恢复。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [命令序列化与类型化派发](../../requirements/frame-sync/REQ-FEAT-007_command-dispatch.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命令合并转发与幂等重发](../../requirements/frame-sync/REQ-FEAT-008_command-forwarding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [权威帧校验与恢复](../../requirements/frame-sync/REQ-FEAT-010_authoritative-frame.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [共享校验与分段诊断](../../requirements/frame-sync/REQ-FEAT-013_shared-checksum.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [稳定 UID 与参与者身份](../../requirements/determinism/REQ-FEAT-015_stable-unit-identity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [全局配置与离线校验](../../requirements/configuration-content/REQ-FEAT-017_configuration-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [属性公式与 Modifier 所有权](../../requirements/unit-stats/REQ-FEAT-023_stat-formulas.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [生命资源护盾与自然恢复](../../requirements/unit-stats/REQ-FEAT-024_health-shields-regeneration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [属性脏标记与只读刷新](../../requirements/unit-stats/REQ-FEAT-026_stat-refresh.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能信号与会话状态](../../requirements/abilities/REQ-FEAT-042_ability-signal-session.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [施法模型与阶段推进](../../requirements/abilities/REQ-FEAT-043_cast-stages.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [世界鼠标 Aim 与类型化 Request](../../requirements/player-input/REQ-FEAT-080_aim-requests.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GameplayCommand 采用唯一 CommandHeader 与强类型负载、规范字节序；CommandDispatcher 按正式 Command 类型进入所属系统。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Stats/StatHandler.cs`：`StatHandler`、`StatConfig`、`ExperienceGainResult`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/FrameSync/AuthorityFrame.cs`：`AuthorityFrameFlags`、`AuthorityFrame`、`CanonicalCommandCodec`、`CanonicalReader`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。
- `Assets/Scripts/RuntimeConfig/GlobalGameplayData.cs`：`FrameSyncSettingsAuthoring`、`CriticalDataVersionsAuthoring`、`GameModeConfigAuthoring`、`PhysicsSettingsAuthoring`、`UnitSettingsAuthoring`、`BakedGlobalGameplayData`、`GlobalGameplayData`。
- `Assets/Scripts/FrameSync/GameplayCommand.cs`：`CommandHeader`、`GameplayCommandIdentity`、`AbilityCancelReason`、`EquipmentShopCommandOperationType`、`GameplayCommand`。
- `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：`SharedGameplayChecksum`、`ChecksumSegment`、`StatEntryField`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Stats/StatHandler.cs`：

```csharp
        public void Rebuild(in RollbackContext context)
        {
            foreach (var kvp in entries)
            {
                kvp.Value.Dirty = true;
            }
        }
```

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

### 输入输出与边界

**命令序列化与类型化派发**

GameplayCommand 采用唯一 CommandHeader 与强类型负载、规范字节序；CommandDispatcher 按正式 Command 类型进入所属系统。

完整 canonical 字节参与对账；无效单位或槽位不能按示例静默修复；输入事件仅翻译一次。

**命令合并转发与幂等重发**

CommandCollector 保持 TargetTick、CommandSeq 与来源身份，GameplayCommandBundle 和 AcceptedCommandRelay 保留规范顺序。

已执行预测 Tick 收到 Relay 时走正常脏 Tick 纠错；相同标识但不同字节必须报冲突。

**权威帧校验与恢复**

AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。

不提供进程重启恢复、局中加入或 BaseSnapshot；本地恢复锚点丢失即终止当前对局连接。

**快照树与字段归属**

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

Tick 内工作缓存不进入快照；恢复不能猜测缺字段；版本与 GameplayDataVersion 同步推进。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/StatHandlerCalculationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `GetStat_NoModifiers_ReturnsLevelBaseValue`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void GetStat_NoModifiers_ReturnsLevelBaseValue()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            Assert.AreEqual((fp)100m, h.GetStat(StatId.AttackDamage));
        }
```
- `Assets/Scripts/Gameplay/Tests/StatHandlerSnapshotTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `CaptureRestore_RoundTrip_PreservesAllState`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CaptureRestore_RoundTrip_PreservesAllState()
        {
            StatHandler h = CreateHandler();
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.AddModifier(StatId.MaxHealth, StatModifierOperation.FlatAdd, (fp)100m);
            h.FinalizeTick();

            StatHandlerSnapshot snapshot = default;
            h.Capture(ref snapshot);

            // Modify after capture
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)999m);
            h.Level = 5;
            h.ClearModifiers();

            // Restore
            h.Restore(in snapshot);

            Assert.AreEqual(1, h.Level);
            Assert.AreEqual((fp)150m, h.GetStat(StatId.AttackDamage));
            Assert.AreEqual((fp)600m, h.GetStat(StatId.MaxHealth));
        }
```
- `Assets/Scripts/FrameSync/Tests/GameplayCommandContractTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `AimFactories_ClearEveryUnusedPayloadField`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void AimFactories_ClearEveryUnusedPayloadField()
        {
            UnitUid target = new UnitUid(4, 8, 2);
            AimSnapshot unitAim = AimSnapshot.ForUnit(target);
            AimSnapshot pointAim = AimSnapshot.ForPoint(new fp2(3, 6));
            AimSnapshot directionAim = AimSnapshot.ForDirection(new fp2(10, 0));

            Assert.AreEqual(AimKind.Unit, unitAim.Kind);
            Assert.AreEqual(target, unitAim.TargetUnitUid);
            Assert.AreEqual(fp2.zero, unitAim.TargetPoint);
            Assert.AreEqual(fp2.zero, unitAim.Direction);

            Assert.AreEqual(AimKind.Point, pointAim.Kind);
            Assert.AreEqual(default(UnitUid), pointAim.TargetUnitUid);
            Assert.AreEqual(fp2.zero, pointAim.Direction);

            Assert.AreEqual(AimKind.Direction, directionAim.Kind);
            Assert.AreEqual(default(UnitUid), directionAim.TargetUnitUid);
            Assert.AreEqual(fp2.zero, directionAim.TargetPoint);
            Assert.LessOrEqual(
                fpmath.abs(directionAim.Direction.x - fp.one),
                fp.FromRaw(8));
            Assert.AreEqual(fp.zero, directionAim.Direction.y);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

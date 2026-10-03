# 设计合同恢复总计划

## 本次执行范围

本计划对应原编码 0109 的一次执行：设计合同恢复总计划。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [加载确认与单调时钟开局屏障](../../requirements/match-flow/REQ-FEAT-003_startup-barrier.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [逻辑时钟与 Tick 推进](../../requirements/frame-sync/REQ-FEAT-005_simulation-tick.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [全局阶段与同步 Tick 管线](../../requirements/frame-sync/REQ-FEAT-006_simulation-pipeline.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命令合并转发与幂等重发](../../requirements/frame-sync/REQ-FEAT-008_command-forwarding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [权威帧校验与恢复](../../requirements/frame-sync/REQ-FEAT-010_authoritative-frame.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [预测回滚与逐帧重演](../../requirements/frame-sync/REQ-FEAT-011_rollback-replay.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [共享校验与分段诊断](../../requirements/frame-sync/REQ-FEAT-013_shared-checksum.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [稳定 UID 与参与者身份](../../requirements/determinism/REQ-FEAT-015_stable-unit-identity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [比赛结束与全端统计](../../requirements/match-flow/REQ-FEAT-016_match-statistics.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [全局配置与离线校验](../../requirements/configuration-content/REQ-FEAT-017_configuration-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [意图规划与输入 Order](../../requirements/units/REQ-FEAT-021_intent-planning.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [属性公式与 Modifier 所有权](../../requirements/unit-stats/REQ-FEAT-023_stat-formulas.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [同步生成死亡复活与回池](../../requirements/units/REQ-FEAT-028_unit-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [战斗请求封存与因果波次](../../requirements/combat/REQ-FEAT-029_combat-causal-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [战斗公式修正与动态 Operand](../../requirements/combat/REQ-FEAT-033_combat-operands.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击周期规划与 Commit](../../requirements/basic-attacks/REQ-FEAT-037_attack-cycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [近战远程与攻击特效输出](../../requirements/basic-attacks/REQ-FEAT-038_attack-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [定义与强类型生成黑板](../../requirements/projectiles/REQ-FEAT-039_projectile-definitions.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [提交运动寿命与回收](../../requirements/projectiles/REQ-FEAT-040_projectile-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能信号与会话状态](../../requirements/abilities/REQ-FEAT-042_ability-signal-session.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [施法模型与阶段推进](../../requirements/abilities/REQ-FEAT-043_cast-stages.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [阶段效果与确定性黑板](../../requirements/abilities/REQ-FEAT-044_stage-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能目录消耗冷却与升级](../../requirements/abilities/REQ-FEAT-045_ability-catalog.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [金币批次确认与可用余额](../../requirements/equipment-shop/REQ-FEAT-057_gold-accounting.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [空间实体注册与写入](../../requirements/spatial-physics/REQ-FEAT-058_spatial-registration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [移动前后网格与碰撞事实](../../requirements/spatial-physics/REQ-FEAT-060_collision-facts.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [墙体异常挤出与表现同步](../../requirements/spatial-physics/REQ-FEAT-061_wall-recovery.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [确定性局部避让](../../requirements/pathfinding-movement/REQ-FEAT-066_local-avoidance.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [普通移动冲刺与强制位移](../../requirements/pathfinding-movement/REQ-FEAT-067_movement-pipeline.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [AI 注册调度与多态快照](../../requirements/non-heroes/REQ-FEAT-068_ai-scheduling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [野怪营地刷新与共享仇恨](../../requirements/non-heroes/REQ-FEAT-070_jungle-camps.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [特效音效与回滚账本](../../requirements/presentation-ui/REQ-FEAT-074_presentation-ledger.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [商店商品详情与余额刷新](../../requirements/presentation-ui/REQ-FEAT-077_shop-view.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Combat/DamageRequest.cs`：`DamageRequest`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`：`GoldIncomeBatchDigest`、`GoldIncomeReason`、`GoldIncomeRecord`、`GoldIncomeRecordBatch`、`GoldIncomeSnapshot`、`GoldIncomeRuntime`、`BuildState`。
- `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：`SharedGameplayChecksum`、`ChecksumSegment`、`StatEntryField`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/Movement/MovementHandler.cs`：`MovementHandler`、`MovementMode`。
- `Assets/Scripts/Gameplay/Unit/Core/ActionRuntimeSet.cs`：`ActionRuntimeSet`。

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

`Assets/Scripts/Gameplay/Combat/DamageRequest.cs`：

```csharp
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct DamageRequest
    {
        public CombatRequestHeader Header;

        public UnitUid SourceUnitUid
        {
            get => Header.SourceUnitUid;
            set => Header.SourceUnitUid = value;
        }

        public UnitUid TargetUnitUid
        {
            get => Header.TargetUnitUid;
            set => Header.TargetUnitUid = value;
        }

        public DamageType DamageType;
        public fp BaseDamage;
        public ProjectileUid? ProjectileSourceUid;

        public bool IsValid =>
            SourceUnitUid.IsValid() &&
            TargetUnitUid.IsValid() &&
            Header.SourceDescriptor.IsValid &&
            Header.RecipeId > 0 &&
            BaseDamage > fp.zero;

        public static readonly DamageRequest None = default;
    }
}
```

### 输入输出与边界

**加载确认与单调时钟开局屏障**

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

不得继续执行旧的载荷携带 UTC 授权方案；Ready 不执行 Gameplay；延迟、重复确认与过期授权不启动第二次对局。

**逻辑时钟与 Tick 推进**

ServerTick、LocalSimulationTick 都是下一待执行 Tick；LatestAuthorityFrameTick 是最近连续接受权威帧，SnapshotTick 是恢复后下一 Tick。SimulationTickContext 提供只读上下文。

预测领先上限和每 Unity 帧执行上限明确；重演不读取渲染耗时或输入设备。

**全局阶段与同步 Tick 管线**

SimulationTickPipeline 按全局 Handler 子阶段推进 Tag、Buff、Equipment、HitReaction、Ability、Movement、Attack，然后封存并结算战斗波次。

UnitUid 只用于稳定遍历，不能通过先处理整只单位制造跨 Handler 优势；捕获前瞬态队列必须清空。

**命令合并转发与幂等重发**

CommandCollector 保持 TargetTick、CommandSeq 与来源身份，GameplayCommandBundle 和 AcceptedCommandRelay 保留规范顺序。

已执行预测 Tick 收到 Relay 时走正常脏 Tick 纠错；相同标识但不同字节必须报冲突。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/FrameSync/Tests/GoldIncomeRuntimeContractTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously()
        {
            var runtime = new GoldIncomeRuntime();
            runtime.Initialize(2, 500);

            runtime.BeginTick(0);
            GoldIncomeRecordBatch empty = runtime.SealTick(0);
            Assert.AreNotEqual(0UL, empty.Digest.Value);
            Assert.AreEqual(0, empty.Records.Length);
            runtime.ConfirmAcceptedTick(0);

            runtime.BeginTick(1);
            runtime.RequestGoldIncome(1, 25, GoldIncomeReason.UnitKill);
            GoldIncomeRecordBatch income = runtime.SealTick(1);
            Assert.AreEqual(0, income.Records[0].IncomeSequenceInTick);
            Assert.Throws<FrameSyncMoba.Deterministic.DeterministicSimulationException>(
                () => runtime.ConfirmAcceptedTick(2));
            runtime.ConfirmAcceptedTick(1);
            Assert.AreEqual(525, runtime.GetConfirmedAvailableGold(1));
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitWorldTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `InternalRegistration_PublicLookupReturnsSameRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void InternalRegistration_PublicLookupReturnsSameRuntime()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(300, 9, 1), UnitKind.Hero, 0, TeamId.Neutral);

            world.RegisterUnit(unit);

            Assert.That(world.TryGetUnit(unit.UnitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(unit));
            Assert.That(world.TryGetUnit(new UnitUid(300, 9, 2), out _), Is.False);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

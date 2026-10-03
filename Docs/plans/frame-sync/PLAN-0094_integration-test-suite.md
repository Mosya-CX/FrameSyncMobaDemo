# 完整 Gameplay 链路集成测试

## 本次执行范围

本计划对应原编码 0094 的一次执行：完整 Gameplay 链路集成测试。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [全局阶段与同步 Tick 管线](../../requirements/frame-sync/REQ-FEAT-006_simulation-pipeline.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

SimulationTickPipeline 按全局 Handler 子阶段推进 Tag、Buff、Equipment、HitReaction、Ability、Movement、Attack，然后封存并结算战斗波次。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Deterministic/Core/SimulationTickContext.cs`：`SimulationTickContext`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    public sealed class SimulationTickPipeline
    {
        private readonly UnitWorld _unitWorld;
        private readonly PhysicsWorld _physicsWorld;
        private readonly CommandCollector _collector;
        private readonly CanonicalByteWriter _checksumWriter;
        private readonly List<UnitSnapshot> _unitStateBuffer = new List<UnitSnapshot>();
        private readonly List<UnitAIControllerSnapshot> _aiStateBuffer = new List<UnitAIControllerSnapshot>();
        private readonly List<JungleCampSnapshot> _campStateBuffer = new List<JungleCampSnapshot>();
        private readonly List<LocomotionResult> _locomotionBuffer = new List<LocomotionResult>();
        private readonly List<InitialSpawnEntry> _initialSpawnRequests =
            new List<InitialSpawnEntry>();

        // RVO system instance (created once, reused per Tick)
        private DeterministicRVOSystem _rvoSystem;
        private readonly RvoOrchestrator
            _rvoOrchestrator =
                new RvoOrchestrator();

        public CombatSystem CombatSystem { get; set; }
        public GoldIncomeRuntime GoldIncome { get; set; }
        public ProjectileWorld ProjectileWorld { get; set; }
        public EquipmentShopRuntime EquipmentShop { get; set; }
        public NaturalGoldIncomeSystem NaturalGoldIncome { get; set; }
        public NonHeroRestoreHelper NonHeroHelper { get; set; }
        public ProjectileHitResolver ProjectileHitResolver { get; set; }
        public DeterministicRandomService RandomService { get; set; }
        public MatchRuleRuntime MatchRule { get; set; }
        public FrameSyncMoba.Unit.MatchEventTracker MatchEventTracker { get; set; }
        public CommandCollector CommandCollector => _collector;
        internal int AuthorityReplayTick { get; set; } = -1;
        public int MaxFutureCommandTicks { get; set; } = 12;

        public int LocalSimulationTick { get; private set; }
        public uint LastChecksum { get; private set; }
        public event Action<int, IReadOnlyList<GameplayCommand>, uint> TickCompleted;
        public Action RestoreStaticBindings { get; set; }

        public bool HasPredictedMatchEndCandidate()
        {
            if (MatchRule == null ||
                MatchRule.CurrentPhase != MatchPhase.Running ||
                !MatchRule.BlueBaseUnitUid.IsValid() ||
                !MatchRule.RedBaseUnitUid.IsValid())
                return false;
            return IsFormallyDead(MatchRule.BlueBaseUnitUid) ||
                IsFormallyDead(MatchRule.RedBaseUnitUid);
        }

        public SimulationTickPipeline(UnitWorld unitWorld, PhysicsWorld physicsWorld = null)
        {
            _unitWorld = unitWorld;
            _physicsWorld = physicsWorld;
            _collector = new CommandCollector();
            _checksumWriter = new CanonicalByteWriter(new byte[262144]);
            LocalSimulationTick = 0;
            _rvoSystem = new DeterministicRVOSystem(RVOConfig.Default);
        }

        public void SubmitCommand(GameplayCommand command)
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：

```csharp
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    public sealed class CombatSystem : IRollback<CombatSnapshot>
    {
        public MatchEventTracker MatchEventTracker { get; set; }
        private readonly UnitWorld _unitWorld;
        private CombatSnapshot _snapshot;

        public DeathEffectDispatcher DeathEffectDispatcher { get; set; }
        public RespawnTimer RespawnTimer { get; set; }
        public int HeroRespawnBaseTicks { get; }
        public int HeroRespawnPerMinuteTicks { get; }

        private readonly List<ShieldRequest> _shieldQueue = new List<ShieldRequest>();
        private readonly List<DamageRequest> _damageQueue = new List<DamageRequest>();
        private readonly List<HealRequest> _healQueue = new List<HealRequest>();
        private readonly List<DeferredCombatRequest> _deferredBuffer = new List<DeferredCombatRequest>();
        private ushort _nextDeferredSeq;
        private bool _deferredSeqExhausted;
        private ushort _nextSequenceInTick;
        private bool _sequenceExhausted;
        private int _currentSequenceLogicTick = -1;
        private readonly Dictionary<UnitUid, CombatContributionEventLog> _eventLogs = new Dictionary<UnitUid, CombatContributionEventLog>();
        private readonly List<UnitUid> _eventLogVictimScratch = new List<UnitUid>();
        private readonly List<UnitUid> _pendingDying = new List<UnitUid>();
        private readonly List<DeathResult> _deathResults = new List<DeathResult>();
        private readonly List<EvaluatedDamage> _damageBatchScratch =
            new List<EvaluatedDamage>();
        private readonly List<EvaluatedHeal> _healBatchScratch =
            new List<EvaluatedHeal>();
        private readonly List<HeroDamageContribution> _heroDamageScratch =
            new List<HeroDamageContribution>();
        private readonly List<ShieldResultEmission> _shieldEmissionScratch =
            new List<ShieldResultEmission>();
        private readonly List<HealResultEmission> _healEmissionScratch =
            new List<HealResultEmission>();
        private readonly List<DamageResultEmission> _damageEmissionScratch =
            new List<DamageResultEmission>();
        private readonly List<UnitUid> _dyingEmissionScratch =
            new List<UnitUid>();
        private readonly Dictionary<UnitUid, UnitUid> _lethalBatchKillers =
            new Dictionary<UnitUid, UnitUid>();
        private readonly Dictionary<UnitUid, fp> _waveStartHealth =
            new Dictionary<UnitUid, fp>();
        private ushort _nextDeathSeq;
        private bool _deathSeqExhausted;
        private const int MaxSettlementWavesPerTick = 256;
        private const ulong KillerTieDomain = 0x434F4D4241544B49UL;
        private uint _initialMatchSeed;
        private bool _hasConfiguredInitialMatchSeed;
        private bool _isCombatTickActive;

        public int ShieldProcessed { get; private set; }
        public int DamageProcessed { get; private set; }
        public int HealProcessed { get; private set; }
        public uint InitialMatchSeed => _initialMatchSeed;
        /// <summary>
        /// Wall-clock seconds over which the unit's HealthRegeneration /
        /// CastResourceRegeneration stats are fully restored (design v13.2
        /// 5: natural regen, LoL-style per-5s values). Configured from
        /// GlobalGameplayData; defaults to 5.
        /// </summary>
        public int NaturalRegenIntervalMilliseconds { get; set; } =
            5000;

// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**全局阶段与同步 Tick 管线**

SimulationTickPipeline 按全局 Handler 子阶段推进 Tag、Buff、Equipment、HitReaction、Ability、Movement、Attack，然后封存并结算战斗波次。

UnitUid 只用于稳定遍历，不能通过先处理整只单位制造跨 Handler 优势；捕获前瞬态队列必须清空。

## 执行结果

现有 GameplayIntegrationTests 主要验证 Tick/随机/UID/路径和配置值，没有旧计划要求的生成→AI→寻路→战斗→死亡→表现完整链路测试。人工游玩正常不等价于这项测试系统已经实现。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Deterministic/Tests/SimulationTickContextTests.cs`：EditMode，程序集 `FrameSyncMoba.Deterministic.Tests`，函数 `ExecutionMode_ValuesAreStable`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ExecutionMode_ValuesAreStable()
        {
            Assert.That((int)ExecutionMode.ServerAuthority, Is.EqualTo(0));
            Assert.That((int)ExecutionMode.ClientPrediction, Is.EqualTo(1));
            Assert.That((int)ExecutionMode.ClientReplay, Is.EqualTo(2));
        }
```
- `Assets/Scripts/Gameplay/Tests/CombatSystemTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SubmitDamage_ValidRequest_ReducesHealth`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SubmitDamage_ValidRequest_ReducesHealth()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp finalHealth = target.StatHandler.CurrentHealth;
            Assert.Less(finalHealth, initialHealth);
            Assert.Greater(finalHealth, fp.zero);
        }
```
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

## 当前关闭结论

用户本轮明确确认该计划应已关闭，按用户确认结束原执行批次。本轮仅核对记录，没有重跑 Unity 测试、资源构建或配套运行，不把本轮关闭登记当作新机器通过证据。

此前源码核查与历史测试范围保留为证据，不再把原批次列为待确认事项；后续补充测试/资源改造应另建计划。

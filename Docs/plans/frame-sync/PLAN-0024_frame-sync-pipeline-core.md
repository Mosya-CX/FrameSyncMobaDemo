# 帧同步 Tick 管线基础

## 本次执行范围

本计划对应原编码 0024 的一次执行：帧同步 Tick 管线基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [全局阶段与同步 Tick 管线](../../requirements/frame-sync/REQ-FEAT-006_simulation-pipeline.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [逻辑时钟与 Tick 推进](../../requirements/frame-sync/REQ-FEAT-005_simulation-tick.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

SimulationTickPipeline 按全局 Handler 子阶段推进 Tag、Buff、Equipment、HitReaction、Ability、Movement、Attack，然后封存并结算战斗波次。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`：`GoldIncomeBatchDigest`、`GoldIncomeReason`、`GoldIncomeRecord`、`GoldIncomeRecordBatch`、`GoldIncomeSnapshot`、`GoldIncomeRuntime`、`BuildState`。
- `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：`SharedGameplayChecksum`、`ChecksumSegment`、`StatEntryField`。
- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。
- `Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs`：`PredictionPauseReason`、`LocalFrameVerificationRecord`、`MissingAuthorityFrameRange`、`AuthorityRecoveryRequest`、`AuthorityRecoveryResponse`、`PredictionRollbackCoordinator`、`CommandHistoryRecord`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/FrameSync/SnapshotStore.cs`：`SnapshotStore`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

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

`Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`：

```csharp
        public void Capture(ref GoldIncomeSnapshot state)
        {
            if (buildState != BuildState.Idle)
                throw new DeterministicSimulationException(
                    "Gold runtime cannot be captured while accepting requests.");
            state.ConfirmedIncomeThroughTick = confirmedIncomeThroughTick;
            state.ConfirmedEarnedGoldTotals = new System.Collections.Generic.List<int>(confirmedEarnedGoldTotals);
            state.UnconfirmedBatches = new System.Collections.Generic.List<GoldIncomeRecordBatch>(unconfirmedBatches.Count);
            for (int i = 0; i < unconfirmedBatches.Count; i++)
                state.UnconfirmedBatches.Add(CloneBatch(unconfirmedBatches[i]));
        }
```

### 输入输出与边界

**全局阶段与同步 Tick 管线**

SimulationTickPipeline 按全局 Handler 子阶段推进 Tag、Buff、Equipment、HitReaction、Ability、Movement、Attack，然后封存并结算战斗波次。

UnitUid 只用于稳定遍历，不能通过先处理整只单位制造跨 Handler 优势；捕获前瞬态队列必须清空。

**逻辑时钟与 Tick 推进**

ServerTick、LocalSimulationTick 都是下一待执行 Tick；LatestAuthorityFrameTick 是最近连续接受权威帧，SnapshotTick 是恢复后下一 Tick。SimulationTickContext 提供只读上下文。

预测领先上限和每 Unity 帧执行上限明确；重演不读取渲染耗时或输入设备。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/FrameSync/Tests/SnapshotStoreStressTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `RingMapping_SurvivesAdvancesDiscardsAndRestores`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RingMapping_SurvivesAdvancesDiscardsAndRestores()
        {
            var store = new SnapshotStore(512);

            // Phase 1: long prediction run.
            for (int t = 0; t < 260; t++)
                store.Store(t, GameplaySnapshot.CreateEmpty());

            // Phase 2: authority frames accepted in batches.
            for (int b = 10; b <= 170; b += 10)
                store.AdvanceBase(b);

            // Phase 3: rollback at 171, anchor is key 170.
            store.DiscardFromTick(171);
            Assert.That(
                store.TryGet(170, out var anchor),
                Is.True);
            Assert.That(anchor.SnapshotTick, Is.EqualTo(171));

            // Phase 4: replay 171..175, accept 171, rollback at 174.
            for (int t = 171; t <= 175; t++)
                store.Store(t, GameplaySnapshot.CreateEmpty());
            store.AdvanceBase(172);
            store.DiscardFromTick(174);
            Assert.That(
                store.TryGet(173, out var a173),
                Is.True);
            Assert.That(a173.SnapshotTick, Is.EqualTo(174));

            // Phase 5: replay forward and verify every key after accept.
            for (int t = 174; t <= 200; t++)
                store.Store(t, GameplaySnapshot.CreateEmpty());
            store.AdvanceBase(180);
            for (int t = 180; t <= 200; t++)
            {
                Assert.That(
                    store.TryGet(t, out var snapshot),
                    Is.True,
                    $"missing key {t}");
                Assert.That(
                    snapshot.SnapshotTick,
                    Is.EqualTo(t + 1),
                    $"mapping mismatch at key {t}");
            }
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

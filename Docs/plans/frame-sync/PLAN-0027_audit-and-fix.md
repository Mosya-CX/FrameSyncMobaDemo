# 框架合同审计与修复

## 本次执行范围

本计划对应原编码 0027 的一次执行：框架合同审计与修复。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [全局阶段与同步 Tick 管线](../../requirements/frame-sync/REQ-FEAT-006_simulation-pipeline.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [比赛结束与全端统计](../../requirements/match-flow/REQ-FEAT-016_match-statistics.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [生命资源护盾与自然恢复](../../requirements/unit-stats/REQ-FEAT-024_health-shields-regeneration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [强类型单位事件与反应](../../requirements/units/REQ-FEAT-027_unit-events.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [战斗请求封存与因果波次](../../requirements/combat/REQ-FEAT-029_combat-causal-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [治疗护盾与再生结算](../../requirements/combat/REQ-FEAT-032_healing-settlement.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [定义与强类型生成黑板](../../requirements/projectiles/REQ-FEAT-039_projectile-definitions.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [提交运动寿命与回收](../../requirements/projectiles/REQ-FEAT-040_projectile-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [金币批次确认与可用余额](../../requirements/equipment-shop/REQ-FEAT-057_gold-accounting.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [路线选择与跟随状态](../../requirements/pathfinding-movement/REQ-FEAT-063_path-following.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [商店商品详情与余额刷新](../../requirements/presentation-ui/REQ-FEAT-077_shop-view.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

SimulationTickPipeline 按全局 Handler 子阶段推进 Tag、Buff、Equipment、HitReaction、Ability、Movement、Attack，然后封存并结算战斗波次。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileSnapshot.cs`：`ProjectileRuntimeSnapshot`、`PendingSpawnRecordSnapshot`、`ProjectileWorldSnapshot`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileWorld.cs`：`PendingSpawnEntry`、`ProjectileWorld`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Combat/CombatSnapshot.cs`：`CombatSnapshot`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileRuntime.cs`：`ProjectileHitRecord`、`ProjectileRuntime`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Physics/Core/PhysicsRuntimeSnapshot.cs`：`UnitContactPair`、`UnitCollisionEventBufferSnapshot`、`PhysicsRuntimeSnapshot`、`IUnitCollisionParticipant`。

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

`Assets/Scripts/Gameplay/Projectile/ProjectileSnapshot.cs`：

```csharp
using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct ProjectileRuntimeSnapshot
    {
        public ProjectileUid Uid;
        public int DefId;
        public UnitUid OwnerUnitUid;
        public TeamId TeamSnapshot;
        public SourceDescriptor Source;
        public OriginActionId OriginActionId;
        public fp2 PreviousPosition;
        public fp2 Position;
        public fp2 Velocity;
        public int RemainingLifetimeTicks;
        public bool IsActive;
        public bool EndRequested;
        public ProjectileEndReason EndReason;
        public int TotalHitCount;
        public int RemainingPierceCount;
        public int RemainingBounceCount;
        public int NextQueryLogicTick;
        public ProjectileHitRecord[] HitRecords;
        public ProjectileOnHitDamage[] OnHitDamageOverride;
        public UnitUid TargetUnitUid;
    }

    public struct PendingSpawnRecordSnapshot
    {
        public ProjectileUid Uid;
        public int DefId;
        public UnitUid OwnerUnitUid;
        public TeamId TeamSnapshot;
        public SourceDescriptor Source;
        public OriginActionId OriginActionId;
        public fp2 StartPosition;
        public fp2 Direction;
        public ProjectileOnHitDamage[] OnHitDamageOverride;
        public int MaxLifetimeTicksOverride;
        public UnitUid TargetUnitUid;
    }

    public struct ProjectileWorldSnapshot
    {
        public PendingSpawnRecordSnapshot[] PendingSpawns;
        public ProjectileRuntimeSnapshot[] ActiveProjectiles;

        public static readonly ProjectileWorldSnapshot Empty =
            new ProjectileWorldSnapshot
            {
                PendingSpawns =
                    Array.Empty<PendingSpawnRecordSnapshot>(),
                ActiveProjectiles =
                    Array.Empty<ProjectileRuntimeSnapshot>(),
            };
    }
}
```

### 输入输出与边界

**全局阶段与同步 Tick 管线**

SimulationTickPipeline 按全局 Handler 子阶段推进 Tag、Buff、Equipment、HitReaction、Ability、Movement、Attack，然后封存并结算战斗波次。

UnitUid 只用于稳定遍历，不能通过先处理整只单位制造跨 Handler 优势；捕获前瞬态队列必须清空。

**快照树与字段归属**

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

Tick 内工作缓存不进入快照；恢复不能猜测缺字段；版本与 GameplayDataVersion 同步推进。

**比赛结束与全端统计**

MatchRuleRuntime 管理阶段与预测结束候选；MatchStatisticsRuntime 在所有模拟端消费 FormalDeathResult。

预测结果不先落为最终结果；统计不只在 Dedicated Server 执行；账户持久化不反写 Gameplay。

**单位根与能力装配**

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

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
- `Assets/Scripts/FrameSync/Tests/AuthorityReplicationTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `CommandBundle_ProducesStablePerTickReplacementRelays`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CommandBundle_ProducesStablePerTickReplacementRelays()
        {
            UnitUid unitUid = new UnitUid(0, 10, 1);
            GameplayCommand first = GameplayCommand.CreateMove(
                Header(unitUid, 3, 1),
                new fp2(fp.one, fp.zero));
            GameplayCommand replacement = GameplayCommand.CreateMove(
                Header(unitUid, 3, 2),
                new fp2((fp)2, fp.zero));
            var buffer = new CommandRelayBuffer();

            AcceptedCommandRelay[] relays = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { replacement, first }),
                0,
                12,
                command => command.ControlledUnitUid == unitUid);

            Assert.AreEqual(1, relays.Length);
            Assert.AreEqual(3, relays[0].TargetTick);
            Assert.AreEqual(1u, relays[0].RelayRevision);
            GameplayCommand[] canonical = relays[0].DecodeCommands();
            Assert.AreEqual(1, canonical.Length);
            Assert.AreEqual(2u, canonical[0].CommandSeq);
            Assert.AreEqual(new fp2((fp)2, fp.zero),
                canonical[0].MoveTargetPoint);

            AcceptedCommandRelay[] duplicate = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { replacement, first }),
                0,
                12,
                null);
            Assert.AreEqual(0, duplicate.Length);
        }
```
- `Assets/Scripts/FrameSync/Tests/ProjectileCombatPipelineTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat()
        {
            RegisterDefinition(
                maxHits: 2,
                endOnFirst: false);
            SpawnAndAdvance();
            physicsWorld.BuildUnitFinalGrid();

            resolver.ResolveAllHits(projectileWorld);

            Assert.AreEqual(2, resolver.PendingHits.Count);
            Assert.Less(
                resolver.PendingHits[0].EqualDistanceTieScore,
                resolver.PendingHits[1].EqualDistanceTieScore);

            int onHitCount = 0;
            int observedParentEffectOrdinal = -1;
            CombatEvents.OnHitDealt += data =>
            {
                onHitCount++;
                observedParentEffectOrdinal = data.EffectOrdinal;
            };
            resolver.EmitEffects(projectileWorld);
            projectileWorld.FlushDestroy();
            combat.SettleActiveRequests();

            Assert.AreEqual(2, combat.DamageProcessed);
            Assert.AreEqual(2, onHitCount);
            Assert.AreEqual(
                CombatFairnessKey.ComposeEffectOrdinal(1, 0),
                observedParentEffectOrdinal);
            Assert.AreEqual(0, projectileWorld.Count);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

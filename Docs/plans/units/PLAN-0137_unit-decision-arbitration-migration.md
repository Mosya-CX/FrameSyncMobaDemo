# 单位动作仲裁迁移

## 本次执行范围

本计划对应原编码 0137 的一次执行：单位动作仲裁迁移。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [动作仲裁与固定执行器](../../requirements/units/REQ-FEAT-022_action-arbitration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

ActionArbiter 输出类型化 ActionSubmitResult；固定 Main/Base 槽位承载资源占用，Runtime 拥有执行状态。移除旧 action 列表与反射式申请。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/BehaviorPlanner.cs`：`BehaviorPlanner`。
- `Assets/Scripts/Gameplay/Unit/Core/ActionRuntimeSet.cs`：`ActionRuntimeSet`。
- `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：`SharedGameplayChecksum`、`ChecksumSegment`、`StatEntryField`。
- `Assets/Scripts/Gameplay/Unit/Core/ActionRequest.cs`：`ActionKind`、`ActionRequest`、`MoveActionRequest`、`AttackActionRequest`、`CastActionRequest`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitActionStateView.cs`：`ActionMainKind`、`ActionBaseKind`、`UnitActionStateView`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：`AttackPlanStatus`、`AttackTimerResetReason`、`AttackHandler`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：

```csharp
        public void ReplaceIntent(in UnitIntent intent)
        {
            if (Planner == null)
                throw new InvalidOperationException(
                    $"Unit {UnitUid} has no BehaviorPlanner.");
            UnitIntent previous = Planner.CurrentIntent;
            Arbiter?.OnIntentReplaced(previous, intent);
            Planner.ReplaceIntent(intent);
        }
```

`Assets/Scripts/Gameplay/Unit/Core/BehaviorPlanner.cs`：

```csharp
        public void Tick(out ActionRequest primaryRequest)
        {
            primaryRequest = null;
            if (!_owner.CanRunActiveGameplayThisTick) return;
            if (_owner.LifeState != LifeState.Alive && _owner.LifeState != LifeState.Dying) return;

            int currentTick = SimulationTickContext.Current.Tick;

            // Unit Framework v27.3 3.3: the control system's stable forced
            // behavior winner is the highest-priority planning input.
            if (_owner.CrowdControl != null &&
                _owner.CrowdControl.TryGetBehaviorOverride(
                    out CrowdControlBehaviorOverride behavior))
            {
                primaryRequest =
                    PlanForcedBehavior(behavior);
                SuppressSatisfiedAction(ref primaryRequest);
                return;
            }
            if (!_currentIntent.IsActive) return;

            switch (_currentIntent.Kind)
            {
                case IntentKind.AttackTarget: primaryRequest = PlanAttackIntent(currentTick); break;
                case IntentKind.MoveToPosition: primaryRequest = PlanMoveIntent(); break;
                case IntentKind.CastAbility: primaryRequest = PlanCastIntent(currentTick); break;
                case IntentKind.LaneAdvance: primaryRequest = PlanLaneAdvance(); break;
                case IntentKind.ReturnToCamp: primaryRequest = PlanReturnToCamp(); break;
            }
            SuppressSatisfiedAction(ref primaryRequest);
        }
```

### 输入输出与边界

**动作仲裁与固定执行器**

ActionArbiter 输出类型化 ActionSubmitResult；固定 Main/Base 槽位承载资源占用，Runtime 拥有执行状态。移除旧 action 列表与反射式申请。

按仲裁资源矩阵判断并发；Snapshot 保存 Main/Base 状态，reservation 由槽位派生；运行时不保存“被接受申请”的第二份权威。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/CrowdControlHandlerTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Add_CreatesIndependentInstances_NoMerge`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Add_CreatesIndependentInstances_NoMerge()
        {
            CrowdControlAddResult first =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    30,
                    default);
            CrowdControlAddResult second =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    30,
                    default);

            Assert.That(first.Added, Is.True);
            Assert.That(second.Added, Is.True);
            Assert.That(unit.CrowdControl.Count,
                Is.EqualTo(2));
            Assert.That(
                first.Handle.InstanceId,
                Is.Not.EqualTo(
                    second.Handle.InstanceId));
            Assert.That(
                unit.CrowdControl.State.BlockedActions,
                Is.EqualTo(
                    UnitActionBlockMask.VoluntaryMove |
                    UnitActionBlockMask.Turn |
                    UnitActionBlockMask.VoluntaryAttack |
                    UnitActionBlockMask.AbilityCast |
                    UnitActionBlockMask.Mobility |
                    UnitActionBlockMask.ControlMove |
                    UnitActionBlockMask.ControlAttack));
        }
```
- `Assets/Scripts/FrameSync/Tests/AggregateSnapshotContractTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `Restore_UsesStableUnitUidAndRestoresRandomAndPhysicsState`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Restore_UsesStableUnitUidAndRestoresRandomAndPhysicsState()
        {
            UnitWorld world = CreateWorld();
            UnitType first = Spawn(world, 10, 0);
            UnitType second = Spawn(world, 11, 1);
            first.MovementHandler.ForceSetPosition(new fp2(3, 4));
            second.MovementHandler.ForceSetPosition(new fp2(7, 8));
            first.PhysicsEntity.SetLogicPose(new fp2(3, 4), new fp2(fp.one, fp.zero));
            second.PhysicsEntity.SetLogicPose(new fp2(7, 8), new fp2(fp.zero, fp.one));

            var random = new DeterministicRandomService(123u);
            _ = random.NextUInt();
            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld)
            {
                RandomService = random,
            };
            GameplaySnapshot snapshot = pipeline.CaptureAggregateSnapshot();
            var expectedRandom = new DeterministicRandomService(1u);
            expectedRandom.Restore(snapshot.RandomState);

            first.MovementHandler.ForceSetPosition(new fp2(99, 99));
            first.PhysicsEntity.TeleportLogicPosition(new fp2(99, 99));
            _ = random.NextUInt();

            pipeline.RestoreFromSnapshot(snapshot, 12);

            Assert.That(first.MovementHandler.Position, Is.EqualTo(new fp2(3, 4)));
            Assert.That(first.PhysicsEntity.Transform2D.Position, Is.EqualTo(new fp2(3, 4)));
            Assert.That(second.PhysicsEntity.Transform2D.Forward, Is.EqualTo(new fp2(fp.zero, fp.one)));
            Assert.That(random.NextUInt(), Is.EqualTo(expectedRandom.NextUInt()));
            Assert.That(pipeline.LocalSimulationTick, Is.EqualTo(12));
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
- `Assets/Scripts/Gameplay/Tests/AbilityCostAndCastTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SessionStartCost_UsesLevelResourceAndHealth`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SessionStartCost_UsesLevelResourceAndHealth()
        {
            AbilityRuntime runtime = InstallCommitAbility(
                CreateCostPlan(
                    20,
                    10,
                    AbilityCostTiming.OnSessionStart),
                new TestStageDef());

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(AimSnapshot.None)));

            Assert.AreEqual(
                (fp)80,
                caster.StatHandler.CurrentCastResource);
            Assert.AreEqual(
                (fp)90,
                caster.StatHandler.CurrentHealth);
            Assert.IsTrue(runtime.ActiveSession.CostPaid);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

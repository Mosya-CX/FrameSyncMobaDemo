# 命中反应集成

## 本次执行范围

本计划对应原编码 0040 的一次执行：命中反应集成。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [强类型单位事件与反应](../../requirements/units/REQ-FEAT-027_unit-events.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitEventBus 按正式事件数据强类型路由 Handler；支持列表和即时顺序明确，Reaction 只生成所允许的后续请求。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Movement/MovementHandler.cs`：`MovementHandler`、`MovementMode`。
- `Assets/Scripts/Gameplay/Ability/AbilityHandler.cs`：`AbilityHandler`、`PassiveEventKind`、`AbilityHandlerSnapshot`、`AbilityBook`、`AbilitySlotRuntime`、`AbilitySlotSnapshot`、`AbilityBookSnapshot`。
- `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：`AttackPlanStatus`、`AttackTimerResetReason`、`AttackHandler`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Combat/HitReactionState.cs`：`HitReactionKind`、`HitReactionState`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Movement/MovementHandler.cs`：

```csharp
        public void TickUpdate()
        {
            MovementMode mode = ResolveMovementMode();
            switch (mode)
            {
                case MovementMode.ForcedMove:
                    AdvanceForcedMove();
                    break;
                case MovementMode.Dash:
                    AdvanceDash();
                    break;
                case MovementMode.RouteMove:
                    AdvanceRouteMove();
                    break;
                default:
                    ApplyStationaryPose();
                    break;
            }

            _currentIntent = MoveIntent.None;
            ClearTickInputs();
        }
```

`Assets/Scripts/Gameplay/Ability/AbilityHandler.cs`：

```csharp
        public void TickUpdate()
        {
            TickPassiveRuntimes();
            // Capture the pre-advance cast state first so instant stages
            // (DurationTicks == 0) are still observable by the presentation
            // layer on the Tick they are cast; otherwise the session would
            // end before ActiveCasts is populated and no cast animation
            // would ever play for them.
            CaptureActiveCasts();

            foreach (var slot in _book.Slots)
            {
                var runtime = slot.GetActiveAbility();
                if (runtime != null) { runtime.World = Owner.World; runtime.CasterUnitUid = Owner.UnitUid; }
                if (Owner.HitReaction.InterruptsAbility && runtime?.ActiveSession != null)
                {
                    runtime.ActiveSession.Interrupted = true;
                    runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                    continue;
                }
                if (runtime?.ActiveSession == null) continue;
                var session = runtime.ActiveSession;
                var model = runtime.Definition.CastModel;
                var stage = GetCastStage(model, session.CurrentStageKey);
                session.StageElapsedTicks++;

                // CC-interrupt check (Ability Design v15.2 section 5.3):
                // any cast session (channel, hold/charge, toggle) is
                // interrupted when the owner is blocked from casting.
                if (ChannelStageHelper.ShouldInterrupt(Owner))
                {
                    session.Interrupted = true;
                    runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                    continue;
                }
                if (model.Kind == CastModelKind.Channel)
                {
                    var (isActive, progress) = ChannelStageHelper.EvaluateChannel(
                        Owner, SimulationTickContext.Current.Tick, session.StartLogicTick, stage.DurationTicks);
                    if (!isActive) { runtime.EndSession(SimulationTickContext.Current.Tick, 0); continue; }
                }

                // Toggle resource-drain check (Ability Design v15.2 section 5.4)
                if (model is ToggleCastModelDef toggleModel)
                {
                    var resource = Owner.StatHandler?.CurrentCastResource ?? Unity.Mathematics.FixedPoint.fp.zero;
                    var (canContinue, _) = ToggleStageHelper.EvaluateToggle(
                        Owner, isToggledOn: true, toggleModel.ResourcePerTick, ref resource);
                    if (Owner.StatHandler != null) Owner.StatHandler.SetCurrentCastResource(resource);
                    if (!canContinue) { runtime.EndSession(SimulationTickContext.Current.Tick, 0); continue; }
                }

                StageResult tickResult = StageResult.Running;
                if (stage.Def != null) tickResult = stage.Def.OnTick(session, runtime);
                if (tickResult == StageResult.Failed)
                { runtime.EndSession(SimulationTickContext.Current.Tick, 0); continue; }

                bool timedOut = session.IsStageTimedOut(stage);
                HoldReleaseCastModelDef holdTimeoutModel =
                    model as HoldReleaseCastModelDef;
                bool holdTimeoutCancel =
                    timedOut &&
                    holdTimeoutModel != null &&
                    holdTimeoutModel.HoldTimeoutPolicy ==
                        HoldTimeoutPolicy.Cancel &&
                    session.CurrentStageKey ==
                        holdTimeoutModel.Hold.StageKey;
                if (tickResult == StageResult.Completed ||
                    timedOut)
                {
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**强类型单位事件与反应**

UnitEventBus 按正式事件数据强类型路由 Handler；支持列表和即时顺序明确，Reaction 只生成所允许的后续请求。

死亡/击杀事件回调在 T 立即发生，但新普通 Shield、Damage、Heal 延迟到 T+1，合法序列缺口不重编号。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/AttackHandlerTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime()
        {
            ActionSubmitResult started = attacker.Arbiter.Submit(
                new AttackActionRequest(target.UnitUid));
            Assert.That(started.IsGranted, Is.True);
            Assert.That(attacker.AttackHandler.CurrentTargetUid,
                Is.EqualTo(target.UnitUid));
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.True);
            Assert.That(attacker.ActionRuntimes.Main.Kind,
                Is.EqualTo(ActionKind.Attack));

            world.RequestEnterDying(target);
            world.ConfirmUnitDeath(target);
            world.ApplyFormalDeathActionInvalidations(new[]
            {
                new DeathResult
                {
                    VictimUid = target.UnitUid,
                    DeathSequenceInTick = 0,
                    DeathLogicTick = 10,
                },
            });

            Assert.That(attacker.AttackHandler.CurrentTargetUid.IsValid(),
                Is.False);
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.False);

            var attackSnapshot = default(AttackSnapshot);
            var runtimeSnapshot = default(ActionRuntimeSetSnapshot);
            attacker.AttackHandler.Capture(ref attackSnapshot);
            attacker.ActionRuntimes.Capture(ref runtimeSnapshot);
            attacker.AttackHandler.Restore(attackSnapshot);
            attacker.ActionRuntimes.Restore(runtimeSnapshot);

            Assert.DoesNotThrow(() =>
            {
                attacker.AttackHandler.Resolve(new RollbackContext(
                    10,
                    ExecutionMode.ClientReplay));
                attacker.ActionRuntimes.Resolve();
            });
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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

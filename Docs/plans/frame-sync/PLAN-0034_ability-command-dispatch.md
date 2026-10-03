# 技能命令与类型化派发

## 本次执行范围

本计划对应原编码 0034 的一次执行：技能命令与类型化派发。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [命令序列化与类型化派发](../../requirements/frame-sync/REQ-FEAT-007_command-dispatch.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命令合并转发与幂等重发](../../requirements/frame-sync/REQ-FEAT-008_command-forwarding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [自适应命令目标 Tick](../../requirements/frame-sync/REQ-FEAT-009_command-timing.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能信号与会话状态](../../requirements/abilities/REQ-FEAT-042_ability-signal-session.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [施法模型与阶段推进](../../requirements/abilities/REQ-FEAT-043_cast-stages.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [阶段效果与确定性黑板](../../requirements/abilities/REQ-FEAT-044_stage-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能目录消耗冷却与升级](../../requirements/abilities/REQ-FEAT-045_ability-catalog.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [主动附带被动与固定被动](../../requirements/abilities/REQ-FEAT-046_ability-passives.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GameplayCommand 采用唯一 CommandHeader 与强类型负载、规范字节序；CommandDispatcher 按正式 Command 类型进入所属系统。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Ability/AbilityHandler.cs`：`AbilityHandler`、`PassiveEventKind`、`AbilityHandlerSnapshot`、`AbilityBook`、`AbilitySlotRuntime`、`AbilitySlotSnapshot`、`AbilityBookSnapshot`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Ability/AbilityRuntime.cs`：`AbilitySession`、`AbilityRuntime`、`AbilityRuntimeSnapshot`、`AbilitySessionSnapshot`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/FrameSync/GameplayCommand.cs`：`CommandHeader`、`GameplayCommandIdentity`、`AbilityCancelReason`、`EquipmentShopCommandOperationType`、`GameplayCommand`。
- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlHandler.cs`：`CrowdControlHandler`。
- `Assets/Scripts/PlayerInput/PlayerCommandRequester.cs`：`IPlayerGameplayCommandRequester`、`IPlayerShopCommandRequester`、`IPlayerAbilityInputProfileProvider`、`IPlayerAbilityAimProfileProvider`、`ILocalAbilityRuntimeView`、`GameplayCommandRequestReceipt`、`LocalAbilityInputStateKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Ability/AbilityHandler.cs`：

```csharp
        public bool HandleSignal(AbilitySignal signal)
        {
            ulong toggleMaskBefore = BuildActiveToggleMask();
            AbilityRuntime beforeRuntime =
                _book.GetSlot(signal.Slot)?.GetActiveAbility();
            int beforeSessionUid =
                beforeRuntime?.ActiveSession?.SessionUid ?? 0;
            int beforeStage =
                beforeRuntime?.ActiveSession?.CurrentStageKey ?? -1;
            int beforeStartTick =
                beforeRuntime?.ActiveSession?.StartLogicTick ?? -1;
            int beforeElapsedTicks =
                beforeRuntime?.ActiveSession?.StageElapsedTicks ?? -1;
            bool handled = HandleSignalCore(signal);
            AbilityRuntime afterRuntime =
                _book.GetSlot(signal.Slot)?.GetActiveAbility();
            int afterSessionUid =
                afterRuntime?.ActiveSession?.SessionUid ?? 0;
            int afterStage =
                afterRuntime?.ActiveSession?.CurrentStageKey ?? -1;
            int afterStartTick =
                afterRuntime?.ActiveSession?.StartLogicTick ?? -1;
            int afterElapsedTicks =
                afterRuntime?.ActiveSession?.StageElapsedTicks ?? -1;
            ulong toggleMaskAfter = BuildActiveToggleMask();
            Debug.Log(
                $"[AbilitySignal] tick={SimulationTickContext.Current.Tick} " +
                $"mode={SimulationTickContext.Current.ExecutionMode} " +
                $"unit={Owner.UnitUid} slot={signal.Slot} " +
                $"ability={afterRuntime?.Definition?.AbilityId ?? beforeRuntime?.Definition?.AbilityId ?? 0} " +
                $"model={afterRuntime?.Definition?.CastModel?.Kind.ToString() ?? beforeRuntime?.Definition?.CastModel?.Kind.ToString() ?? "<none>"} " +
                $"verb={signal.Verb} handled={handled} " +
                $"session={beforeSessionUid}->{afterSessionUid} " +
                $"stage={beforeStage}->{afterStage} aim={signal.Aim.Kind}");
            Debug.Log(
                $"[AbilitySignalTrace] tick={SimulationTickContext.Current.Tick} " +
                $"mode={SimulationTickContext.Current.ExecutionMode} " +
                $"unit={Owner.UnitUid} slot={signal.Slot} verb={signal.Verb} " +
                $"handled={handled} activeMask=0x{toggleMaskBefore:X}->0x{toggleMaskAfter:X} " +
                $"session={beforeSessionUid}->{afterSessionUid} " +
                $"stage={beforeStage}->{afterStage} " +
                $"startTick={beforeStartTick}->{afterStartTick} " +
                $"elapsed={beforeElapsedTicks}->{afterElapsedTicks} " +
                $"nextSessionUid={_nextSessionUid}");
            return handled;
        }
```

`Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：

```csharp
        private void DispatchCommand(GameplayCommand command)
        {
            if (!_unitWorld.TryGetUnit(command.UnitUid, out UnitType unit)) return;

            if (command.Kind ==
                GameplayCommandKind.AllocateAbilitySkillPoint)
            {
                unit.AbilityHandler?.TryAllocateSkillPoint(
                    command.AbilitySlot);
                return;
            }
            if (command.Kind ==
                GameplayCommandKind.Debug)
            {
                DispatchDebugCommand(
                    command,
                    unit);
                return;
            }
            if (command.Kind ==
                GameplayCommandKind.EquipmentShop)
            {
                DispatchEquipmentShopCommand(command, unit);
                return;
            }
            if (command.Kind ==
                GameplayCommandKind.SwapEquipmentSlot)
            {
                unit.EquipmentHandler?.SwapSlots(
                    command.SourceSlot,
                    command.TargetSlot);
                return;
            }
            if (command.Kind == GameplayCommandKind.UseItem)
            {
                if (unit.EquipmentHandler != null &&
                    unit.EquipmentHandler.Use(
                        command.SourceSlot,
                        command.Aim))
                    EquipmentShop?.InvalidateUndoByEquipmentUse(
                        command.PlayerSlot,
                        command.SourceSlot);
                return;
            }

            if (unit.Planner == null || unit.Arbiter == null)
                throw new DeterministicSimulationException(
                    $"Unit {unit.UnitUid} received an action Command without " +
                    "Planner/Arbiter composition.");

            if (command.Kind == GameplayCommandKind.Move)
            {
                unit.ReplaceIntent(new UnitIntent
                {
                    Kind = IntentKind.MoveToPosition,
                    TargetPosition = command.MoveTargetPoint,
                    AllowChase = false,
                    AllowReplan = true,
                });
            }
            else if (command.Kind == GameplayCommandKind.Attack)
            {
                unit.ReplaceIntent(new UnitIntent
                {
                    Kind = IntentKind.AttackTarget,
                    TargetUnit = command.AttackTargetUid,
                    AllowChase = true,
                    AllowReplan = false,
                });
            }
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

**技能信号与会话状态**

AbilityHandler 接受 AbilitySignal，AbilityRuntime 常驻，AbilitySession 只承载本次施法；Session 结束回传执行器，外部只读 AbilityCastView。

HandleSignal 返回是否接受，不让 Planner 私自推进 Session；同 Tick Focus 和 Commit 需正式 CommandSeq 顺序。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/PlayerInput/Tests/PlayerCommandRequesterTests.cs`：EditMode，程序集 `FrameSyncMoba.PlayerInput.Tests`，函数 `EventBuffer_AssignsStableSequenceAndRejectsOverflow`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void EventBuffer_AssignsStableSequenceAndRejectsOverflow()
        {
            var buffer = new LocalInputEventBuffer();
            for (int i = 0; i < LocalInputEventBuffer.MaxLocalInputEventsPerUnityFrame; i++)
            {
                Assert.IsTrue(buffer.Push(
                    LocalGameplayInputEventKind.AbilityKeyPressed,
                    (byte)(i % 4),
                    new Vector2(i, i)));
            }
            Assert.IsFalse(buffer.Push(
                LocalGameplayInputEventKind.PrimaryClick, 0, Vector2.zero));

            ulong previous = 0;
            while (buffer.TryDequeue(out LocalGameplayInputEvent inputEvent))
            {
                Assert.Greater(inputEvent.LocalEventSequence, previous);
                previous = inputEvent.LocalEventSequence;
            }
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
- `Assets/Scripts/Bootstrap/Tests/EditMode/GameplayCommandSendLedgerTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `UnchangedCollector_BuildsOnlyOneReliableBundleCandidate`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void UnchangedCollector_BuildsOnlyOneReliableBundleCandidate()
        {
            var collector = new CommandCollector();
            var ledger = new GameplayCommandSendLedger();
            collector.Collect(CreateToggle(10, 1));

            Assert.That(
                ledger.TryBuildUnsentCommands(
                    collector,
                    out ulong revision,
                    out var first),
                Is.True);
            Assert.That(first, Has.Count.EqualTo(1));
            ledger.CommitSuccessfulSend(revision, first);

            Assert.That(
                ledger.TryBuildUnsentCommands(
                    collector,
                    out _,
                    out _),
                Is.False,
                "Repeated Unity Updates must not wrap unchanged commands " +
                "in new reliable Bundles.");
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

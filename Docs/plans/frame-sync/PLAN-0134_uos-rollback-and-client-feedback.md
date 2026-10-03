# UOS 回滚与客户端反馈

## 本次执行范围

本计划对应原编码 0134 的一次执行：UOS 回滚与客户端反馈。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [预测回滚与逐帧重演](../../requirements/frame-sync/REQ-FEAT-011_rollback-replay.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [权威帧校验与恢复](../../requirements/frame-sync/REQ-FEAT-010_authoritative-frame.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [UOS 配置与连接模式](../../requirements/match-flow/REQ-FEAT-004_uos-configuration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [大厅槽位与开局配置](../../requirements/match-flow/REQ-FEAT-002_lobby-slots.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [加载确认与单调时钟开局屏障](../../requirements/match-flow/REQ-FEAT-003_startup-barrier.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PredictionRollbackCoordinator 每 Tick 保存快照，普通回滚起点不早于 LatestAuthorityFrameTick+1；先 Restore，再 Resolve，最后 Rebuild。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/NonHeroRestoreHelper.cs`：`NonHeroRestoreHelper`。
- `Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs`：`PredictionPauseReason`、`LocalFrameVerificationRecord`、`MissingAuthorityFrameRange`、`AuthorityRecoveryRequest`、`AuthorityRecoveryResponse`、`PredictionRollbackCoordinator`、`CommandHistoryRecord`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/NonHeroRestoreHelper.cs`：

```csharp
        public void RebuildNonHero(in RollbackContext context)
        {
            _minionSystem?.Rebuild(context);
            var camps = _unitWorld.JungleCamps;
            for (int i = 0; i < camps.Count; i++)
                camps[i].Rebuild(context);

            var aiControllers = _unitWorld.AIControllers;
            foreach (var ai in aiControllers)
            {
                ai.Rebuild(context);
            }
        }
```

`Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs`：

```csharp
        private void CorrectAndReplay(in AuthorityFrame frame)
        {
            int predictedEndTick = pipeline.LocalSimulationTick;
            if (frame.Tick >= predictedEndTick)
                throw new DeterministicSimulationException(
                    $"No local execution record exists for AuthorityFrame Tick {frame.Tick}.");
            if (frame.Tick < LatestAuthorityFrameTick + 1)
                throw new DeterministicSimulationException(
                    "Ordinary rollback cannot cross LatestAuthorityFrameTick + 1.");
            if (!store.TryGet(frame.Tick - 1, out RollbackFrameSnapshot anchor))
                throw new DeterministicSimulationException(
                    $"Missing local rollback anchor at SnapshotTick {frame.Tick}.");

            RemoveVerificationFrom(frame.Tick);
            pipeline.GoldIncome?.DiscardUnconfirmedFromTick(frame.Tick);
            store.DiscardFromTick(frame.Tick);
            bool authorityEndsMatch =
                (frame.FrameFlags &
                    AuthorityFrameFlags.MatchEndCandidate) != 0;
            int replayEndTick = authorityEndsMatch
                ? checked(frame.Tick + 1)
                : predictedEndTick;

            // Ordinary rollback must not drop the player's already-created
            // Commands that target ticks beyond the replay window:
            // ReplaceCommandsForNextTick clears the pipeline collector during
            // the replay, and losing them would permanently desync the
            // client's future prediction against the server's accepted
            // Commands.
            var pendingCommands = new List<GameplayCommand>();
            if (!authorityEndsMatch)
            {
                List<GameplayCommand> current =
                    pipeline.CommandCollector
                        .GetCanonicalCommands();
                for (int i = 0;
                     i < current.Count;
                     i++)
                {
                    if (current[i].TargetTick >=
                        replayEndTick)
                    {
                        pendingCommands.Add(
                            current[i]);
                    }
                }
            }
            UnityEngine.Debug.Log(
                $"[Rollback] tick={frame.Tick} " +
                $"anchorUnits={anchor.Gameplay.UnitWorldState.Units?.Length ?? -1}");
            UnityEngine.Debug.Log(
                $"[Rollback] tick={frame.Tick} " +
                $"anchor={anchor.SnapshotTick} " +
                $"replayEnd={replayEndTick} " +
                $"predictedEnd={predictedEndTick} " +
                $"pendingPreserved={pendingCommands.Count} " +
                $"authorityEndsMatch={authorityEndsMatch}");

            if (authorityEndsMatch)
                RemoveCommandHistoryAfter(frame.Tick);
            pipeline.RestoreFromSnapshot(
                anchor.Gameplay, anchor.SnapshotTick, ExecutionMode.ClientReplay);
            for (int i = 0; i < restoreRegistrations.Count; i++) restoreRegistrations[i](anchor.Gameplay);
            var context = new RollbackContext(frame.Tick, ExecutionMode.ClientReplay);
            for (int i = 0; i < resolveRegistrations.Count; i++) resolveRegistrations[i](context);
            for (int i = 0; i < rebuildRegistrations.Count; i++) rebuildRegistrations[i](context);

            GameplayCommand[] authoritativeCommands = frame.DecodeCommands();
            commandHistory[frame.Tick] = new CommandHistoryRecord(
                frame.FinalCommandRevision,
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**预测回滚与逐帧重演**

PredictionRollbackCoordinator 每 Tick 保存快照，普通回滚起点不早于 LatestAuthorityFrameTick+1；先 Restore，再 Resolve，最后 Rebuild。

不倒退已接受金币基线、不重读设备输入；失效确定性引用明确失败；每个 AuthorityFrame 形成单 Tick 接受屏障。

**权威帧校验与恢复**

AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。

不提供进程重启恢复、局中加入或 BaseSnapshot；本地恢复锚点丢失即终止当前对局连接。

**UOS 配置与连接模式**

UosApplicationConfig 读取 Unity.UOS.Common.Settings.MatchmakingConfigID；命令行仅作显式覆盖。本地模式由 LocalNgoEndpointDriver 拥有，UOS 模式由 LobbyFlowController 拥有。

匹配配置 ID 与启动 Profile ID 不可混用；令牌和服务器密钥不进源码或共享日志。

**大厅槽位与开局配置**

LobbySessionFlowNetwork 和 LobbyNetworkBridge 管理玩家槽位与屏障；GameStartConfig 固定 PlayerSlotConfig 列表。英雄可重复选择，正 HeroConfigId 由内容闭包校验。

断线、重复 Ready、人数不一致、内容或版本不一致必须有明确失败状态；大厅消息不进入 GameplayCommand。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `GameBootstrapPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。
- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameSyncDiagnosticBuildOptionsTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `ComposeScriptingDefines_CompilesDiagnosticsOnlyWhenEnabled`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ComposeScriptingDefines_CompilesDiagnosticsOnlyWhenEnabled()
        {
            bool original = EditorTools
                .FrameSyncDiagnosticBuildOptions
                .IncludeAsyncDiagnostics;
            try
            {
                EditorTools.FrameSyncDiagnosticBuildOptions
                    .IncludeAsyncDiagnostics = false;
                CollectionAssert.AreEqual(
                    new[] { "FRAME_SYNC_MOBA_UOS_ONLINE" },
                    EditorTools.FrameSyncDiagnosticBuildOptions
                        .ComposeScriptingDefines(
                            true,
                            "FRAME_SYNC_MOBA_UOS_ONLINE"));
                CollectionAssert.IsEmpty(
                    EditorTools.FrameSyncDiagnosticBuildOptions
                        .ComposeScriptingDefines(
                            false,
                            "FRAME_SYNC_MOBA_UOS_ONLINE"));

                EditorTools.FrameSyncDiagnosticBuildOptions
                    .IncludeAsyncDiagnostics = true;
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "FRAME_SYNC_MOBA_UOS_ONLINE",
                        Unit.FrameSyncDiagnostics.BuildDefine,
                    },
                    EditorTools.FrameSyncDiagnosticBuildOptions
                        .ComposeScriptingDefines(
                            true,
                            "FRAME_SYNC_MOBA_UOS_ONLINE"));
                CollectionAssert.AreEqual(
                    new[]
                    {
                        Unit.FrameSyncDiagnostics.BuildDefine,
                    },
                    EditorTools.FrameSyncDiagnosticBuildOptions
                        .ComposeScriptingDefines(
                            false,
                            "FRAME_SYNC_MOBA_UOS_ONLINE"));
            }
            finally
            {
                EditorTools.FrameSyncDiagnosticBuildOptions
                    .IncludeAsyncDiagnostics = original;
            }
        }
```
- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameSyncLaunchScheduleTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `ClientWait_SubtractsTransitAndPredictionLead`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ClientWait_SubtractsTransitAndPredictionLead()
        {
            const int tickRate = 30;
            const int leadTicks = 5;
            const long sentServerMs = 10_000L;
            const long serverLaunchMs =
                sentServerMs + 5_000L;
            const long receivedServerMs =
                sentServerMs + 1_200L;

            long clientLaunchMs = FrameSyncLaunchSchedule
                .GetClientPredictionLaunchServerTimeMilliseconds(
                    serverLaunchMs,
                    tickRate,
                    leadTicks);

            Assert.That(
                clientLaunchMs - receivedServerMs,
                Is.EqualTo(
                    5_000L -
                    1_200L -
                    leadTicks * 1_000L /
                    tickRate));
        }
```
- `Assets/Scripts/Bootstrap/Tests/EditMode/MatchLaunchProtocolTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `Messages_RoundTripCanonically`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Messages_RoundTripCanonically()
        {
            var applied =
                new BootstrapAppliedConfirmation(
                    "match-two-phase",
                    3);
            byte[] appliedBytes =
                MatchLaunchWireCodec
                    .WriteBootstrapApplied(applied);
            BootstrapAppliedConfirmation restoredApplied =
                MatchLaunchWireCodec
                    .ReadBootstrapApplied(appliedBytes);
            Assert.That(restoredApplied.MatchId,
                Is.EqualTo(applied.MatchId));
            Assert.That(restoredApplied.StartTick,
                Is.EqualTo(applied.StartTick));
            Assert.That(
                MatchLaunchWireCodec.WriteBootstrapApplied(
                    restoredApplied),
                Is.EqualTo(appliedBytes));

            var commit =
                new MatchLaunchCommit(
                    "match-two-phase",
                    3,
                    15_000L);
            byte[] commitBytes =
                MatchLaunchWireCodec
                    .WriteLaunchCommit(commit);
            MatchLaunchCommit restoredCommit =
                MatchLaunchWireCodec
                    .ReadLaunchCommit(commitBytes);
            Assert.That(restoredCommit.MatchId,
                Is.EqualTo(commit.MatchId));
            Assert.That(restoredCommit.StartTick,
                Is.EqualTo(commit.StartTick));
            Assert.That(
                restoredCommit.LaunchServerTimeMilliseconds,
                Is.EqualTo(
                    commit.LaunchServerTimeMilliseconds));
            Assert.That(
                MatchLaunchWireCodec.WriteLaunchCommit(
                    restoredCommit),
                Is.EqualTo(commitBytes));

            byte[] legacyVersionBytes =
                (byte[])commitBytes.Clone();
            legacyVersionBytes[4] = 1;
            legacyVersionBytes[5] = 0;
            Assert.Throws<DeterministicSimulationException>(
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

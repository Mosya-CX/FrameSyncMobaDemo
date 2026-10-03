# 命令 Bundle 幂等与发送门禁

## 本次执行范围

本计划对应原编码 0152 的一次执行：命令 Bundle 幂等与发送门禁。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [命令序列化与类型化派发](../../requirements/frame-sync/REQ-FEAT-007_command-dispatch.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命令合并转发与幂等重发](../../requirements/frame-sync/REQ-FEAT-008_command-forwarding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [自适应命令目标 Tick](../../requirements/frame-sync/REQ-FEAT-009_command-timing.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GameplayCommand 采用唯一 CommandHeader 与强类型负载、规范字节序；CommandDispatcher 按正式 Command 类型进入所属系统。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/CommandCollector.cs`：`CommandCollector`、`CommandMergeKey`、`UseItemMergeKey`、`GameplayCommandCanonicalComparer`。
- `Assets/Scripts/Bootstrap/FrameSyncNetworkBridge.cs`：`FrameSyncNetworkBridge`、`PresentationPingTracker`、`FrameSyncWireCodec`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Bootstrap/GameplayCommandSendLedger.cs`：`GameplayCommandSendLedger`。
- `Assets/Scripts/FrameSync/AuthorityFrame.cs`：`AuthorityFrameFlags`、`AuthorityFrame`、`CanonicalCommandCodec`、`CanonicalReader`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/CommandCollector.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    public sealed class CommandCollector
    {
        private readonly Dictionary<CommandMergeKey, GameplayCommand> moveCommands =
            new Dictionary<CommandMergeKey, GameplayCommand>();
        private readonly Dictionary<CommandMergeKey, GameplayCommand> attackCommands =
            new Dictionary<CommandMergeKey, GameplayCommand>();
        private readonly Dictionary<UseItemMergeKey, GameplayCommand> useItemCommands =
            new Dictionary<UseItemMergeKey, GameplayCommand>();
        private readonly List<GameplayCommand> nonMergedCommands = new List<GameplayCommand>();

        public int CommandCount =>
            moveCommands.Count + attackCommands.Count +
            useItemCommands.Count + nonMergedCommands.Count;
        public ulong ContentRevision { get; private set; }

        public void BeginTick(int targetTick)
        {
            bool hadCommands = CommandCount > 0;
            moveCommands.Clear();
            attackCommands.Clear();
            useItemCommands.Clear();
            nonMergedCommands.Clear();
            if (hadCommands)
                MarkContentChanged();
        }

        public void Collect(GameplayCommand command)
        {
            if (command.IsNone) return;
            ValidateHeader(command);

            var key = new CommandMergeKey(
                command.PlayerSlot,
                command.ControlledUnitUid,
                command.TargetTick);

            switch (command.Kind)
            {
                case GameplayCommandKind.Move:
                    if (CollectLastBySequence(
                            moveCommands,
                            key,
                            command))
                        MarkContentChanged();
                    break;

                case GameplayCommandKind.Attack:
                    if (CollectLastBySequence(
                            attackCommands,
                            key,
                            command))
                        MarkContentChanged();
                    break;

                case GameplayCommandKind.UseItem:
                    if (CollectLastBySequence(
                            useItemCommands,
                            new UseItemMergeKey(
                                command.PlayerSlot,
                                command.ControlledUnitUid,
                                command.TargetTick,
                                command.SourceSlot),
                            command))
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Bootstrap/FrameSyncNetworkBridge.cs`：

```csharp
        private void Send(
            string messageName,
            ulong clientId,
            byte[] payload)
        {
            using (var writer = new FastBufferWriter(
                payload.Length,
                Allocator.Temp))
            {
                writer.WriteBytesSafe(payload);
                networkManager.CustomMessagingManager
                    .SendNamedMessage(
                        messageName,
                        clientId,
                        writer,
                        NetworkDelivery.ReliableSequenced);
            }
        }
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

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

# 权威帧恢复与预测限制

## 本次执行范围

本计划对应原编码 0119 的一次执行：权威帧恢复与预测限制。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [权威帧校验与恢复](../../requirements/frame-sync/REQ-FEAT-010_authoritative-frame.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/RuntimeConfig/GlobalGameplayData.cs`：`FrameSyncSettingsAuthoring`、`CriticalDataVersionsAuthoring`、`GameModeConfigAuthoring`、`PhysicsSettingsAuthoring`、`UnitSettingsAuthoring`、`BakedGlobalGameplayData`、`GlobalGameplayData`。
- `Assets/Scripts/FrameSync/AuthorityFrame.cs`：`AuthorityFrameFlags`、`AuthorityFrame`、`CanonicalCommandCodec`、`CanonicalReader`。
- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。
- `Assets/Scripts/FrameSync/GameplayCommand.cs`：`CommandHeader`、`GameplayCommandIdentity`、`AbilityCancelReason`、`EquipmentShopCommandOperationType`、`GameplayCommand`。
- `Assets/Scripts/FrameSync/CommandCollector.cs`：`CommandCollector`、`CommandMergeKey`、`UseItemMergeKey`、`GameplayCommandCanonicalComparer`。
- `Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs`：`PredictionPauseReason`、`LocalFrameVerificationRecord`、`MissingAuthorityFrameRange`、`AuthorityRecoveryRequest`、`AuthorityRecoveryResponse`、`PredictionRollbackCoordinator`、`CommandHistoryRecord`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/FrameSync/SnapshotStore.cs`：`SnapshotStore`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/RuntimeConfig/GlobalGameplayData.cs`：

```csharp
using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    [Serializable]
    public sealed class FrameSyncSettingsAuthoring
    {
        [Min(1)] public int TickRate = 30;
        [Min(0)] public int MinCommandLeadTicks = 1;
        [Min(1)] public int MaxFutureCommandTicks = 12;
        [Min(2)] public int SnapshotWindowTicks = 180;
        [Min(0)] public int MaxPredictionLeadTicks = 6;
        [Min(1)] public int MaxLogicTicksPerUnityFrame = 4;
        [Min(1)] public int AuthorityRecoveryRetryTicks = 15;
        [Min(1)] public int MaxAuthorityRecoveryAttemptsBeforeDisconnect = 4;
        [Min(0)] public int StartLeadTicks = 3;
    }

    [Serializable]
    public sealed class CriticalDataVersionsAuthoring
    {
        [Min(1)] public uint GameplayDataVersion = 1;
        [Min(1)] public uint MapDataVersion = 1;
        [Min(1)] public uint GlobalPrefabTableVersion = 1;
        [Min(1)] public uint CommandSchemaVersion = 1;
    }

    [Serializable]
    public sealed class GameModeConfigAuthoring
    {
        [Min(1)] public int GameModeId = 1;
        [Min(1)] public int MaxPlayers = 10;
        public DurationAuthoring Countdown;
        [HideInInspector]
        [Min(0)] public float CountdownSeconds = 3f;
        public DurationAuthoring LaunchDelay;
        [HideInInspector]
        [Min(0f)] public float LaunchDelaySeconds = 5f;
        public DurationAuthoring EndingDuration;
        [HideInInspector]
        [Min(0)] public float EndingSeconds = 6f;
        [Min(0)] public int InitialEarnedGold = 1500;
        public DurationAuthoring HeroRespawnBase;
        [HideInInspector]
        [Min(0)] public float HeroRespawnBaseSeconds = 5f;
        public DurationAuthoring HeroRespawnPerMinute;
        [HideInInspector]
        [Min(0)] public float HeroRespawnPerMinuteSeconds = 0.5f;
        public DurationAuthoring MinionWaveInterval;
        [HideInInspector]
        [Min(0.01f)] public float MinionWaveIntervalSeconds = 30f;
        public DurationAuthoring JungleResetTimeout;
        [HideInInspector]
        [Min(0)] public float JungleResetTimeoutSeconds = 5f;
        public DurationAuthoring JungleResetDuration;
        [HideInInspector]
        [Min(0)] public float JungleResetDurationSeconds = 3f;
        public DurationAuthoring JungleRespawnDelay;
        [HideInInspector]
        [Min(0)] public float JungleRespawnDelaySeconds = 60f;
        [Range(0f, 1f)] public float EquipmentSellRate = 0.7f;
        /// <summary>Natural health/cast-resource regen cadence. The unit
        /// stats HealthRegeneration / CastResourceRegeneration express the
        /// amount restored over this many wall-clock seconds (LoL-style
        /// per-5s values).</summary>
        public DurationAuthoring NaturalRegenInterval;
        [HideInInspector, Min(0.1f)]
        public float NaturalRegenIntervalSeconds = 5f;
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/FrameSync/AuthorityFrame.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    [Flags]
    public enum AuthorityFrameFlags : byte
    {
        None = 0,
        MatchEndCandidate = 1 << 0,
    }

    /// <summary>
    /// Final authoritative input and deterministic output proof for one Tick.
    /// </summary>
    public readonly struct AuthorityFrame
    {
        public readonly int Tick;
        public readonly uint FrameSequence;
        public readonly uint FinalCommandRevision;
        public readonly AuthorityFrameFlags FrameFlags;
        public readonly uint SharedGameplayChecksum;
        private readonly byte[] canonicalCommandBytes;

        public byte[] CanonicalCommandBytes =>
            canonicalCommandBytes == null
                ? Array.Empty<byte>()
                : (byte[])canonicalCommandBytes.Clone();

        internal byte[] CanonicalCommandBytesUnsafe =>
            canonicalCommandBytes ?? Array.Empty<byte>();

        public AuthorityFrame(
            int tick,
            uint frameSequence,
            uint finalCommandRevision,
            byte[] canonicalCommandBytes,
            AuthorityFrameFlags frameFlags,
            uint sharedGameplayChecksum)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            Tick = tick;
            FrameSequence = frameSequence;
            FinalCommandRevision = finalCommandRevision;
            this.canonicalCommandBytes = canonicalCommandBytes == null
                ? throw new ArgumentNullException(nameof(canonicalCommandBytes))
                : (byte[])canonicalCommandBytes.Clone();
            FrameFlags = frameFlags;
            SharedGameplayChecksum = sharedGameplayChecksum;
        }

        public static AuthorityFrame Create(
            int tick,
            uint frameSequence,
            uint finalCommandRevision,
            IReadOnlyList<GameplayCommand> commands,
            AuthorityFrameFlags frameFlags,
            uint sharedGameplayChecksum)
        {
            return new AuthorityFrame(
                tick,
                frameSequence,
                finalCommandRevision,
                CanonicalCommandCodec.Encode(commands),
                frameFlags,
                sharedGameplayChecksum);
        }
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**权威帧校验与恢复**

AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。

不提供进程重启恢复、局中加入或 BaseSnapshot；本地恢复锚点丢失即终止当前对局连接。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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

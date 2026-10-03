# 规范基础类型字节写入

## 本次执行范围

本计划对应原编码 0004 的一次执行：规范基础类型字节写入。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [命令序列化与类型化派发](../../requirements/frame-sync/REQ-FEAT-007_command-dispatch.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [共享校验与分段诊断](../../requirements/frame-sync/REQ-FEAT-013_shared-checksum.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GameplayCommand 采用唯一 CommandHeader 与强类型负载、规范字节序；CommandDispatcher 按正式 Command 类型进入所属系统。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Deterministic/Serialization/CanonicalByteWriter.cs`：`CanonicalByteWriter`。
- `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：`SharedGameplayChecksum`、`ChecksumSegment`、`StatEntryField`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：`PhysicsEntity2D`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Deterministic/Serialization/CanonicalByteWriter.cs`：

```csharp
        public ArraySegment<byte> GetWrittenSegment()
        {
            return new ArraySegment<byte>(buffer, 0, writtenCount);
        }
```

`Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`：

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
    public static class SharedGameplayChecksum
    {
        /// <summary>
        /// When true, authority/server ticks and client replay mismatches log
        /// per-segment (and per-unit handler) checksum hashes so a divergence
        /// can be localized to the exact subsystem. Diagnostics only; never
        /// affects simulation.
        /// </summary>
        public static bool DetailedLoggingEnabled;

        public readonly struct ChecksumSegment
        {
            public readonly string Label;
            public readonly uint Hash;

            public ChecksumSegment(string label, uint hash)
            {
                Label = label;
                Hash = hash;
            }
        }

        public static uint Compute(
            in GameplaySnapshot snapshot,
            GoldIncomeBatchDigest goldDigest,
            CanonicalByteWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.Reset();
            writer.WriteInt32(snapshot.SchemaVersion);
            writer.WriteUInt32(snapshot.RandomState.State);
            WriteMatchRule(writer, snapshot.MatchRuleState);
            WriteUnitWorld(writer, snapshot.UnitWorldState);
            WriteCombat(writer, snapshot.CombatState);
            WriteProjectiles(writer, snapshot.ProjectileState);
            WriteEquipmentShop(writer, snapshot.EquipmentShopState);
            WritePhysics(writer, snapshot.PhysicsState);
            writer.WriteUInt64(goldDigest.Value);
            ArraySegment<byte> bytes = writer.GetWrittenSegment();
            return DeterministicHash32.Compute(bytes.Array, bytes.Offset, bytes.Count);
        }

        private static void WriteMatchRule(
            CanonicalByteWriter writer,
            in MatchRuleRuntimeSnapshot state)
        {
            writer.WriteByte((byte)state.CurrentPhase); writer.WriteInt32(state.PhaseEnterTick);
            writer.WriteInt32(state.RunningStartTick); WriteUnitUid(writer, state.BlueBaseUnitUid);
            WriteUnitUid(writer, state.RedBaseUnitUid); writer.WriteInt32(state.GameOverTick);
            writer.WriteInt32(state.FinishTick); writer.WriteByte(state.WinningTeamId.Value);
            writer.WriteByte((byte)state.EndReason);
            var entries = state.Statistics.Entries ?? new List<MatchStatisticsEntry>();
            writer.WriteInt32(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                WriteUnitUid(writer, entries[i].HeroUnitUid); writer.WriteInt32(entries[i].Kills);
                writer.WriteInt32(entries[i].Deaths); writer.WriteInt32(entries[i].Assists);
                writer.WriteInt32(entries[i].CreepKills);
            }
        }
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**命令序列化与类型化派发**

GameplayCommand 采用唯一 CommandHeader 与强类型负载、规范字节序；CommandDispatcher 按正式 Command 类型进入所属系统。

完整 canonical 字节参与对账；无效单位或槽位不能按示例静默修复；输入事件仅翻译一次。

**共享校验与分段诊断**

SharedGameplayChecksum 规范序列化并按稳定键排序；-checksumDetail 输出分段和逐单位 Handler 摘要。

诊断不改变 Gameplay，不依赖集合插入顺序；StatSeq、配置 ID 与正式序列规则一致。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Deterministic/Tests/CanonicalByteWriterTests.cs`：EditMode，程序集 `FrameSyncMoba.Deterministic.Tests`，函数 `MixedPrimitives_MatchCanonicalLittleEndianGoldenBytes`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void MixedPrimitives_MatchCanonicalLittleEndianGoldenBytes()
        {
            var buffer = new byte[35];
            var writer = new CanonicalByteWriter(buffer);

            writer.WriteByte(0xAB);
            writer.WriteBoolean(false);
            writer.WriteBoolean(true);
            writer.WriteInt32(unchecked((int)0x89ABCDEFu));
            writer.WriteUInt32(0x01234567u);
            writer.WriteInt64(unchecked((long)0xFEDCBA9876543210UL));
            writer.WriteUInt64(0x0123456789ABCDEFUL);
            writer.WriteFp(fp.FromRaw(unchecked((long)0x8877665544332211UL)));

            byte[] expected =
            {
                0xAB,
                0x00,
                0x01,
                0xEF, 0xCD, 0xAB, 0x89,
                0x67, 0x45, 0x23, 0x01,
                0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE,
                0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01,
                0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
            };

            Assert.That(writer.WrittenCount, Is.EqualTo(expected.Length));
            Assert.That(writer.RemainingCapacity, Is.Zero);
            AssertWrittenBytes(writer, expected);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

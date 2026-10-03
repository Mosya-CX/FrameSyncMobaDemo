# 非英雄快照与回滚

## 本次执行范围

本计划对应原编码 0036 的一次执行：非英雄快照与回滚。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [预测回滚与逐帧重演](../../requirements/frame-sync/REQ-FEAT-011_rollback-replay.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [权威帧校验与恢复](../../requirements/frame-sync/REQ-FEAT-010_authoritative-frame.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [AI 注册调度与多态快照](../../requirements/non-heroes/REQ-FEAT-068_ai-scheduling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [小兵波次与兵线 AI](../../requirements/non-heroes/REQ-FEAT-069_minion-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [野怪营地刷新与共享仇恨](../../requirements/non-heroes/REQ-FEAT-070_jungle-camps.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [防御塔目标优先级与攻击红线](../../requirements/non-heroes/REQ-FEAT-071_tower-targeting.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/NonHero/MinionSystem.cs`：`MinionSystem`。
- `Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs`：`PredictionPauseReason`、`LocalFrameVerificationRecord`、`MissingAuthorityFrameRange`、`AuthorityRecoveryRequest`、`AuthorityRecoveryResponse`、`PredictionRollbackCoordinator`、`CommandHistoryRecord`。
- `Assets/Scripts/FrameSync/NonHeroRestoreHelper.cs`：`NonHeroRestoreHelper`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/NonHero/UnitAIController.cs`：`UnitAIController`、`MinionAIController`、`ThreatEntry`、`MonsterAIController`、`TowerAIController`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/NonHero/MinionSystem.cs`：

```csharp
        public void Rebuild(
            in RollbackContext context)
        {
        }
```

`Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.FrameSync
{
    [Flags]
    public enum PredictionPauseReason : byte
    {
        None = 0,
        MissingAuthorityFrame = 1 << 0,
        PredictionLeadLimit = 1 << 1,
        MatchEndCandidate = 1 << 2,
    }

    public readonly struct LocalFrameVerificationRecord
    {
        public readonly int LogicTick;
        public readonly uint SharedGameplayChecksum;
        public LocalFrameVerificationRecord(int logicTick, uint checksum)
        { LogicTick = logicTick; SharedGameplayChecksum = checksum; }
    }

    public readonly struct MissingAuthorityFrameRange
    {
        public readonly int FromTick;
        public readonly int ToTick;
        public MissingAuthorityFrameRange(int fromTick, int toTick)
        {
            if (fromTick < 0 || toTick < fromTick)
                throw new ArgumentOutOfRangeException(nameof(fromTick));
            FromTick = fromTick;
            ToTick = toTick;
        }
    }

    public readonly struct AuthorityRecoveryRequest
    {
        private readonly MissingAuthorityFrameRange[] missingRanges;
        public readonly uint RequestSequence;
        public MissingAuthorityFrameRange[] MissingRanges =>
            missingRanges == null
                ? Array.Empty<MissingAuthorityFrameRange>()
                : (MissingAuthorityFrameRange[])missingRanges.Clone();
        public AuthorityRecoveryRequest(uint sequence, MissingAuthorityFrameRange[] ranges)
        {
            RequestSequence = sequence;
            missingRanges = ranges == null
                ? Array.Empty<MissingAuthorityFrameRange>()
                : (MissingAuthorityFrameRange[])ranges.Clone();
        }
    }

    public readonly struct AuthorityRecoveryResponse
    {
        private readonly AuthorityFrame[] authorityFrames;
        public readonly uint RequestSequence;
        public AuthorityFrame[] AuthorityFrames =>
            authorityFrames == null
                ? Array.Empty<AuthorityFrame>()
                : (AuthorityFrame[])authorityFrames.Clone();
        public AuthorityRecoveryResponse(uint sequence, AuthorityFrame[] frames)
        {
            RequestSequence = sequence;
            authorityFrames = frames == null
                ? Array.Empty<AuthorityFrame>()
                : (AuthorityFrame[])frames.Clone();
        }
    }

// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**快照树与字段归属**

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

Tick 内工作缓存不进入快照；恢复不能猜测缺字段；版本与 GameplayDataVersion 同步推进。

**预测回滚与逐帧重演**

PredictionRollbackCoordinator 每 Tick 保存快照，普通回滚起点不早于 LatestAuthorityFrameTick+1；先 Restore，再 Resolve，最后 Rebuild。

不倒退已接受金币基线、不重读设备输入；失效确定性引用明确失败；每个 AuthorityFrame 形成单 Tick 接受屏障。

**权威帧校验与恢复**

AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。

不提供进程重启恢复、局中加入或 BaseSnapshot；本地恢复锚点丢失即终止当前对局连接。

**AI 注册调度与多态快照**

UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。

不增加通用模拟按键层；AI Runtime 与管理者状态区分；死亡注销不能在恢复时静默漏建。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/Gameplay/Tests/UnitWorldTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `InternalRegistration_PublicLookupReturnsSameRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void InternalRegistration_PublicLookupReturnsSameRuntime()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(300, 9, 1), UnitKind.Hero, 0, TeamId.Neutral);

            world.RegisterUnit(unit);

            Assert.That(world.TryGetUnit(unit.UnitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(unit));
            Assert.That(world.TryGetUnit(new UnitUid(300, 9, 2), out _), Is.False);
        }
```
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `MinionWave_ExpandsCanonicalTeamLaneMemberOrder`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void MinionWave_ExpandsCanonicalTeamLaneMemberOrder()
        {
            var schedule = new BakedMinionWaveConfig(
                30,
                0,
                new[]
                {
                    new MinionWavePhase
                    {
                        StartWaveIndex = 0,
                        CompositionCycle = new[]
                        {
                            new MinionWaveComposition
                            {
                                Members = new[]
                                {
                                    new MinionWaveMember
                                    {
                                        UnitPrototypeId = 20,
                                        Count = 2,
                                        FirstSpawnOffsetTicks = 5,
                                        SpawnStepTicks = 1,
                                    },
                                },
                            },
                        },
                    },
                });
            var lane = new LaneRuntimeData(
                3,
                new[]
                {
                    new LaneTeamSpawnData(
                        new TeamId(1),
                        new fp2(1, 2),
                        new fp2(1, 0)),
                    new LaneTeamSpawnData(
                        new TeamId(2),
                        new fp2(9, 2),
                        new fp2(-1, 0)),
                },
                new[] { fp2.zero, new fp2(10, 0) },
                (fp)2m);
            var system = new MinionSystem(
                new UnitWorld(),
                schedule,
                new[] { lane });
            BeginTick(0);

            system.TickLogic();
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

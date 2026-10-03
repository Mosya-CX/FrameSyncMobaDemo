# 回滚合同与属性快照

## 本次执行范围

本计划对应原编码 0020 的一次执行：回滚合同与属性快照。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [预测回滚与逐帧重演](../../requirements/frame-sync/REQ-FEAT-011_rollback-replay.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [权威帧校验与恢复](../../requirements/frame-sync/REQ-FEAT-010_authoritative-frame.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Deterministic/Core/IRollback.cs`：`IRollback`。
- `Assets/Scripts/Gameplay/Stats/StatHandlerSnapshot.cs`：`StatHandlerSnapshot`、`StatRuntimeEntrySnapshot`。
- `Assets/Scripts/Gameplay/Stats/StatHandler.cs`：`StatHandler`、`StatConfig`、`ExperienceGainResult`。
- `Assets/Scripts/Deterministic/Core/RollbackContext.cs`：`RollbackContext`。
- `Assets/Scripts/Deterministic/Core/ExecutionMode.cs`：`ExecutionMode`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Deterministic/Core/IRollback.cs`：

```csharp
namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Unified rollback contract for authoritative state owners (Unit v27.3 §7.15).
    /// Only systems that own state affecting future simulation need to implement this.
    /// </summary>
    /// <typeparam name="TState">The snapshot state type owned by the implementor.</typeparam>
    public interface IRollback<TState>
    {
        void Capture(ref TState state);
        void Restore(in TState state);
        void Resolve(in RollbackContext context);
        void Rebuild(in RollbackContext context);
    }
}
```

`Assets/Scripts/Gameplay/Stats/StatHandlerSnapshot.cs`：

```csharp
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Serializable snapshot of all StatHandler cross-Tick state (Unit v27.3 ��5.9.1).
    /// </summary>
    public struct StatHandlerSnapshot
    {
        public int Level;
        public fp CurrentHealth;
        public fp CurrentCastResource;
        public int CurrentExperience;
        public uint NextStatSeq;

        public int NextShieldInstanceId;
        public ShieldInstance[] ShieldInstances;

        public StatRuntimeEntrySnapshot[] Entries;
    }

    /// <summary>
    /// Snapshot of one StatRuntimeEntry (Unit v27.3 ��5.9.1).
    /// </summary>
    public struct StatRuntimeEntrySnapshot
    {
        public StatId StatId;
        public fp LevelBaseValue;
        public fp FinalValue;
        public fp PreviousLogicTickFinalValue;
        public bool Dirty;
        public StatModifier[] Modifiers;
    }
}
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

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/StatHandlerSnapshotTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `CaptureRestore_RoundTrip_PreservesAllState`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CaptureRestore_RoundTrip_PreservesAllState()
        {
            StatHandler h = CreateHandler();
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.AddModifier(StatId.MaxHealth, StatModifierOperation.FlatAdd, (fp)100m);
            h.FinalizeTick();

            StatHandlerSnapshot snapshot = default;
            h.Capture(ref snapshot);

            // Modify after capture
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)999m);
            h.Level = 5;
            h.ClearModifiers();

            // Restore
            h.Restore(in snapshot);

            Assert.AreEqual(1, h.Level);
            Assert.AreEqual((fp)150m, h.GetStat(StatId.AttackDamage));
            Assert.AreEqual((fp)600m, h.GetStat(StatId.MaxHealth));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

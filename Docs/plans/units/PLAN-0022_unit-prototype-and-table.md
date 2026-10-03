# 单位原型与定义表

## 本次执行范围

本计划对应原编码 0022 的一次执行：单位原型与定义表。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Prototype/GlobalUnitPrototypeTable.cs`：`GlobalUnitPrototypeTable`。
- `Assets/Scripts/Gameplay/Unit/Prototype/UnitPrototype.cs`：`UnitPrototype`。
- `Assets/Scripts/Gameplay/Stats/StatId.cs`：`StatId`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Kind/UnitKind.cs`：`UnitKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Prototype/GlobalUnitPrototypeTable.cs`：

```csharp
        public void Add(UnitPrototype prototype)
        {
            if (prototype == null)
            {
                throw new ArgumentNullException(nameof(prototype));
            }

            if (prototypes.ContainsKey(prototype.UnitPrototypeId))
            {
                throw new ArgumentException(
                    $"Duplicate UnitPrototypeId {prototype.UnitPrototypeId}.",
                    nameof(prototype));
            }

            prototypes[prototype.UnitPrototypeId] = prototype;
        }
```

`Assets/Scripts/Gameplay/Unit/Prototype/UnitPrototype.cs`：

```csharp
using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Static configuration source for a Unit (Unit v27.3 §1.6).
    /// Loaded at match start and read-only thereafter. SpawnUnit reads this
    /// to populate Unit identity fields and initialize subsystems.
    ///
    /// Fields whose types don't exist yet (HandlerLoadout, LocomotionProfile,
    /// PhysicsProfile2D, UnitRespawnConfig, UnitPoolConfig) are deferred to
    /// future slices. This slice covers the fields already defined by
    /// completed ExecPlans: PrototypeId, Name, RuntimeEntityPrefabId,
    /// UnitKind, UnitSubKindId, BaseStats, BaseGoldValue, BaseExperienceValue.
    /// </summary>
    [Serializable]
    public sealed class UnitPrototype
    {
        public int UnitPrototypeId;
        public string Name;

        /// <summary>
        /// Stable ID into the GlobalPrefabTable for Unity Prefab lookup.
        /// Distinct from UnitPrototypeId (§1.3).
        /// </summary>
        public int RuntimeEntityPrefabId;

        public UnitKind UnitKind;

        public ushort UnitSubKindId;

        /// <summary>
        /// Base stat configuration (§1.6/§5.2.3).
        /// </summary>
        public StatPreset BaseStats;

        public int BaseGoldValue;

        /// <summary>
        /// Post-death object lifecycle policy (Unit v27.3 §1.6).
        /// Defaults to Destroy for non-hero units.
        /// </summary>
        public ushort UnitDisposePolicyId;

        /// <summary>
        /// Respawn configuration (Unit v27.3 §1.6).
        /// Only meaningful when DisposePolicy is KeepAlive.
        /// </summary>
        public UnitRespawnConfig RespawnConfig;

        public UnitPoolConfig PoolConfig;

        public int BaseExperienceValue;

        /// <summary>
        /// Scoreboard creep-score granted to the credited hero on death.
        /// Ordinary minions and small monsters use one; larger camp monsters
        /// may author a larger value without special-casing runtime logic.
        /// </summary>
        public int BaseCreepScoreValue = 1;

        /// <summary>
        /// Built-in buffs granted automatically at spawn and reapplied by the
        /// permanent-buff respawn lifecycle. Configured per prototype
        /// (authoring), consumed by BuffHandler.ApplyInitialBuffs.
        /// </summary>
        public BuffConfigId[] InitialBuffConfigIds =
            Array.Empty<BuffConfigId>();

        public HandlerLoadout Loadout;
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**单位根与能力装配**

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

**按对局加载内容闭包**

GlobalPrefabTable 是唯一聚合；生产根索引路径型子表，Addressables 在 Tick 0 前异步加载，组成对局内非序列化同步表。MapConfigId 和按 ID 排序的英雄集合决定闭包。

缺分区、重复 ID、版本/哈希或阵容不一致在初始快照前失败；不回退全量旧目录；禁止 WaitForCompletion。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/GlobalUnitPrototypeTableTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Add_And_TryGet`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Add_And_TryGet()
        {
            var globalTable = new GlobalUnitPrototypeTable();
            var proto = CreatePrototype(42, validPreset);

            globalTable.Add(proto);

            Assert.IsTrue(globalTable.TryGet(42, out UnitPrototype found));
            Assert.AreSame(proto, found);
            Assert.AreEqual(1, globalTable.Count);
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitPrototypeTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `UnitPrototype_DefaultValues`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void UnitPrototype_DefaultValues()
        {
            var proto = new UnitPrototype();
            Assert.AreEqual(0, proto.UnitPrototypeId);
            Assert.IsNull(proto.Name);
            Assert.AreEqual(0, proto.RuntimeEntityPrefabId);
            Assert.AreEqual(UnitKind.Hero, proto.UnitKind);
            Assert.AreEqual(0, proto.UnitSubKindId);
            Assert.IsNull(proto.BaseStats);
            Assert.AreEqual(0, proto.BaseGoldValue);
            Assert.AreEqual(0, proto.BaseExperienceValue);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

# Buff 合同补全

## 本次执行范围

本计划对应原编码 0128 的一次执行：Buff 合同补全。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [Buff 施加覆写与运行身份](../../requirements/buffs/REQ-FEAT-048_buff-application.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [Buff 反应与句柄生命周期](../../requirements/buffs/REQ-FEAT-049_buff-reaction-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [Buff 上限优先级与驱逐](../../requirements/buffs/REQ-FEAT-050_buff-capacity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

BuffHandler 保存按 ConfigId 稳定的 BuffRuntime，重复 Apply 覆写、更新 stack/duration；BuffSource 与黑板只承载正式数据。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Buff/BuffInfo.cs`：`BuffInfo`。
- `Assets/Scripts/Gameplay/Buff/BuffDefinition.cs`：`BuffDefinition`。
- `Assets/Scripts/Gameplay/Stats/StatHandler.cs`：`StatHandler`、`StatConfig`、`ExperienceGainResult`。
- `Assets/Scripts/Gameplay/Ability/AbilityHandler.cs`：`AbilityHandler`、`PassiveEventKind`、`AbilityHandlerSnapshot`、`AbilityBook`、`AbilitySlotRuntime`、`AbilitySlotSnapshot`、`AbilityBookSnapshot`。
- `Assets/Scripts/Gameplay/Ability/Stages/ApplyBuffStageDef.cs`：`ApplyBuffStageDef`、`BuffTargetRule`。
- `Assets/Scripts/Gameplay/Buff/BuffBlackboard.cs`：`BuffBlackboard`、`BuffValueSnapshot`、`BuffBlackboardSnapshot`。
- `Assets/Scripts/Gameplay/Equipment/BuffEquipmentModule.cs`：`BuffEquipmentModule`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileEffectDispatcher.cs`：`ProjectileEffectDispatcher`、`ProjectileAoETarget`、`ProjectileAoETargetComparer`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Buff/BuffInfo.cs`：

```csharp
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Read-only buff snapshot for external consumers (design v14.2 11.2).
    /// TimeProgress is presentation data and never enters Gameplay.
    /// </summary>
    public readonly struct BuffInfo
    {
        public readonly BuffConfigId Id;
        public readonly string Name;
        public readonly string Description;
        public readonly string IconAddress;
        public readonly int StackCount;
        public readonly int MaxStacks;
        public readonly bool Infinite;
        public readonly int RemainingTicks;
        public readonly int DurationTicks;
        public readonly float TimeProgress;
        public readonly byte[] Tags;
        public readonly BuffSource Source;

        public BuffInfo(
            BuffConfigId id,
            string name,
            string description,
            string iconAddress,
            int stackCount,
            int maxStacks,
            bool infinite,
            int remainingTicks,
            int durationTicks,
            byte[] tags,
            BuffSource source)
        {
            Id = id;
            Name = name;
            Description = description;
            IconAddress = iconAddress;
            StackCount = stackCount;
            MaxStacks = maxStacks;
            Infinite = infinite;
            RemainingTicks = remainingTicks;
            DurationTicks = durationTicks;
            Tags = tags;
            Source = source;
            TimeProgress =
                durationTicks > 0
                    ? Mathf.Clamp01(
                        remainingTicks /
                        (float)durationTicks)
                    : 0f;
        }
    }
}
```

`Assets/Scripts/Gameplay/Buff/BuffDefinition.cs`：

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Static buff configuration (design v14.2 3.1-3.2).
    /// Single ScriptableObject; no SO + Bake layer.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuffDefinition",
        menuName = "MOBA/Buff Definition")]
    public sealed class BuffDefinition :
        ScriptableObject
    {
        public BuffConfigId ConfigId;
        public BuffDisplayInfo Display;
        public BuffLifeRuleConfig Life;
        public BuffStackRuleConfig Stack;
        public BuffTagSet Tags;
        public BuffEffectConfig[] Effects;
        public BuffBlackboardLayout BlackboardLayout;
        public BuffLifecycleReactions LifecycleReactions;
        public BuffEventReactions EventReactions;

        /// <summary>
        /// Dispel priority for the formalized MaxBuffs rule
        /// (design v14.2 13A): 0 = highest, 255 = lowest.
        /// </summary>
        public byte Priority;

        [Tooltip("Periodic cadence in milliseconds. Bake converts it to Ticks.")]
        public DurationAuthoring PeriodicInterval;
        [HideInInspector]
        public int PeriodicIntervalTicks;

        /// <summary>
        /// VFX played once on the owner when the buff is first applied
        /// (presentation only, never affects Gameplay). 0 = none.
        /// </summary>
        public int ApplyVfxDefId;

        /// <summary>
        /// Seconds the apply VFX should stay visible. 0 = use the VFX
        /// prefab's own default duration.
        /// </summary>
        public int ApplyVfxDurationMilliseconds;
        [HideInInspector]
        public float ApplyVfxDurationSeconds;

        /// <summary>
        /// Transitional initial stack count; the reactions slice derives the
        /// initial StackChanged 0 -> InitialStacks event.
        /// </summary>
        public int InitialStacks = 1;

        [NonSerialized] private int bakedTickRate = 30;
        [NonSerialized] private int bakedPeriodicIntervalTicks;
        [NonSerialized] private bool hasBakedPeriodicInterval;

        public int BakedTickRate => bakedTickRate;

        public bool IsValid => ConfigId.IsValid;

        public bool IsInfinite =>
            Life != null && Life.Infinite;

        public int DurationTicks =>
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**Buff 施加覆写与运行身份**

BuffHandler 保存按 ConfigId 稳定的 BuffRuntime，重复 Apply 覆写、更新 stack/duration；BuffSource 与黑板只承载正式数据。

不新增平行 Buff 身份；Added、Reapplied、StackChanged、Removed 顺序明确；结构外源 Buff 先拒绝再创建。

**Buff 反应与句柄生命周期**

静态 BuffEffect/ReactionConfig + Runtime Blackboard；创建者持有 StatModifierHandle/CombatModifierHandle，BuffHandler 不扫描黑板兜底释放。

RemovedComplete 在 store 移除后运行；死亡回调不自行清空 Buff；普通死亡保留永久 Runtime，复活重建本生命 Handle。

**Buff 上限优先级与驱逐**

MaxBuffs 为 byte 默认 255；Priority 0 最高。仅新 ConfigId 首次施加检查：最低优先级非永久为候选，同级按 ConfigId 稳定顺序末项；新 Priority<=候选才驱逐。

否则允许超过软上限；驱逐使用 ManualRemove 正常移除流程；重施已有 Buff 不重复驱逐。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/BuffEffectLibraryTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `PeriodicDamage_DealsDamageEachInterval`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void PeriodicDamage_DealsDamageEachInterval()
        {
            BeginTick(1);
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialHp = target.StatHandler.CurrentHealth;

            var effect = new PeriodicDamageBuffEffect
            {
                DamagePerTick = 10m,
                DamageType = DamageType.Physical,
            };
            var def = CreateBuffDef(1001, 2, 30, effects: new BuffEffect[] { effect });
            target.BuffHandler.DefinitionRegistry = _buffDefs;
            _buffDefs.Register(def);
            target.BuffHandler.Apply(def.ConfigId, def, target.UnitUid);

            for (int tick = 2; tick <= 5; tick++)
            {
                _combat.BeginTick();
                target.BuffHandler.Advance();
                _combat.SettleActiveRequests();
                _combat.EndTick();
            }

            fp afterHp = target.StatHandler.CurrentHealth;
            Assert.Less(afterHp, initialHp, "Should have taken periodic damage");
        }
```
- `Assets/Scripts/Gameplay/Tests/BuffReactionAndInfoTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Blackboard_StaticLayout_ReadWriteResetAndRoundTrip`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Blackboard_StaticLayout_ReadWriteResetAndRoundTrip()
        {
            var layout = new BuffBlackboardLayout
            {
                Slots = new[]
                {
                    new BuffStateSlotDefinition
                    {
                        SlotId = new BuffStateSlotId(1),
                        Kind = BuffValueKind.Int,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = new BuffStateSlotId(2),
                        Kind = BuffValueKind.Bool,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = new BuffStateSlotId(3),
                        Kind = BuffValueKind.Fp,
                    },
                },
            };
            var blackboard = new BuffBlackboard();
            blackboard.Initialize(layout);

            Assert.That(blackboard.SlotCount, Is.EqualTo(3));
            blackboard.WriteInt(new BuffStateSlotId(1), 7);
            blackboard.WriteBool(new BuffStateSlotId(2), true);
            blackboard.WriteFp(new BuffStateSlotId(3), (fp)1.5m);
            Assert.That(
                blackboard.ReadIntOrDefault(new BuffStateSlotId(1)),
                Is.EqualTo(7));
            Assert.That(
                blackboard.ReadBoolOrDefault(new BuffStateSlotId(2)),
                Is.True);
            Assert.That(
                blackboard.ReadFpOrDefault(new BuffStateSlotId(3)),
                Is.EqualTo((fp)1.5m));

            BuffBlackboardSnapshot snapshot =
                blackboard.Capture();
            var restored = new BuffBlackboard();
            restored.Initialize(layout);
            restored.Restore(snapshot);
            Assert.That(
                restored.ReadIntOrDefault(new BuffStateSlotId(1)),
                Is.EqualTo(7));
            Assert.That(
                restored.ReadBoolOrDefault(new BuffStateSlotId(2)),
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。

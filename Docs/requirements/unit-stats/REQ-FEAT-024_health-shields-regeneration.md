# 生命资源护盾与自然恢复

## 目标实现

生命、法力和护盾状态有唯一拥有者并参与快照。

## 技术方案

StatHandler 管理当前状态、最大值和护盾实例；Combat 的 Regen、Heal、Shield 管线读取正式数值与修正。

## 边界情况

恢复状态后失效 Handle 不能静默丢弃；护盾吸收顺序固定；PendingDying 时治疗和护盾按所属管线合同处理。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：当前关联实现定义 CombatSystem、ShieldRequestComparer、HealRequestComparer、DamageRequestComparer、DamageAllocationGroup（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Combat/Requests/ShieldRequest.cs`：当前关联实现定义 ShieldRequest、ShieldType（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Stats/ShieldInstance.cs`：当前关联实现定义 ShieldInstance（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Stats/StatHandler.cs`：当前关联实现定义 StatHandler、StatConfig、ExperienceGainResult（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/CombatSystemTests.cs`：SubmitDamage_ValidRequest_ReducesHealth、Omnivamp_HealsSourceForFractionOfSettledDamage、NaturalRegen_AppliesPerInterval_ForHealthAndCastResource、DamageFormula_ArmorReducesDamage、ZeroArmor_FullDamageApplied、CombatModifiers_ApplyOutgoingAndIncomingFinalPatches、FatalDamage_CompletesFormalDeathSettlement。
- `Assets/Scripts/Gameplay/Tests/StatHandlerCalculationTests.cs`：GetStat_NoModifiers_ReturnsLevelBaseValue、GetStat_FlatAdd_SumsCorrectly、GetStat_BaseRatioAdd_AppliesPercentToLevelBase、GetStat_FinalRatioAdd_AppliesPercentAfterFlatAndBase、GetStat_FixedOrder_FlatThenBaseThenFinal、GetStat_ClampByStatDefinition_MaxValue、GetStat_ClampByStatDefinition_MinValue。
- `Assets/Scripts/Gameplay/Tests/StatHandlerChangeTests.cs`：GetChangeThisTick_BeforeFinalize_NoChange、GetChangeThisTick_AfterModifierAdd_ReturnsDelta、GetChangeThisTick_NetChangeSameAsBaseline_ReturnsFalseZero、FinalizeTick_SnapshotsFinalValueAsPreviousBaseline、GetChangeThisTick_AfterFinalizeTick_ReturnsZero、GetChangeThisTick_StatNotInPreset_ReturnsDefault。
- `Assets/Scripts/Gameplay/Tests/StatHandlerModifierTests.cs`：AddModifier_ReturnsValidHandle_WithCorrectStatSeq、AddModifier_StatSeqMonotonicAcrossStatIds、AddModifier_InvalidStatId_Throws、SetModifierValue_UpdatesAndMarksDirty、SetModifierValue_WrongOwnerUid_ReturnsFalse、RemoveModifier_RemovesAndMarksDirty、RemoveModifier_AlreadyRemoved_ReturnsFalse。
- `Assets/Scripts/Gameplay/Tests/StatHandlerSeqTests.cs`：StatSeq_StartsAt1、StatSeq_NeverReusedAfterRemove、StatSeq_AcrossStatIds_SharedCounter、StatSeq_InvalidHandle_WhenStatSeqZero。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 当前状态值、资源与自然恢复

`StatHandler` 保存：

```text
CurrentHealth
CurrentCastResource
CurrentExperience
ShieldInstances
```

约束：

```text
0 <= CurrentHealth <= GetStat(MaxHealth)
0 <= CurrentCastResource <= GetStat(MaxCastResource)
CurrentExperience 遵守等级经验配置
CurrentShield 由有效 ShieldInstance 汇总
```

这些数值不能通过普通 `StatModifier` 直接增加或减少。

蓝量和能量统一抽象为 `CastResource`：

| 属性或状态 | 说明 |
|---|---|
| `MaxCastResource` | 可被成长和 Modifier 修正的上限 |
| `CurrentCastResource` | 当前运行时资源 |
| `CastResourceRegeneration` | 自然恢复属性 |

怒气、弹药、连击点等英雄特色资源由英雄或技能 Runtime 扩展。

自然恢复在哪个全局阶段应用，由全局 Gameplay Pipeline 决定。  
`StatHandler` 不维护第二套时间系统。

---

### 护盾类型、实例与吸收流程

护盾使用带类型和独立生命周期的实例列表。

```csharp
public enum ShieldType : byte
{
    AllDamage = 0,
    PhysicalDamage = 1,
    MagicDamage = 2,
    MagicDamageAndCrowdControlImmunity = 3,
}
```

| 类型 | 常用称呼 | 吸收范围 | 附加效果 |
|---|---|---|---|
| `AllDamage` | 白盾 | 所有允许被护盾吸收的伤害 | 无 |
| `PhysicalDamage` | 物理盾 | 物理伤害 | 无 |
| `MagicDamage` | 魔法盾 | 魔法伤害 | 无 |
| `MagicDamageAndCrowdControlImmunity` | 黑盾 | 魔法伤害 | 有效期间提供控制免疫 |

概念实例：

```csharp
public sealed class ShieldInstance
{
    public int ShieldInstanceId;
    public ShieldType ShieldType;

    public fp CurrentValue;
    public fp MaxValue;

    public int StartLogicTick;
    public int ExpireLogicTick;

    public SourceToken Source;

    public CrowdControlImmunityHandle
        CrowdControlImmunityHandle;
}
```

多个匹配护盾按 `ShieldInstanceId` 升序稳定消耗。

黑盾创建成功后通过 `CrowdControlHandler` 添加免疫并保存句柄。  
护盾耗尽、到期、主动移除、进入 `Dead`、进入 `Respawning` 或对象池重置时，通过正式路径移除对应免疫。

`StatHandler` 不解释哪些控制可被免疫，只管理护盾与控制免疫句柄的同生共灭。

---


## 需求演进

### 2026-10-02

变动内容：正式死亡由 UnitWorld 同步执行，来源系统仅清理自己的句柄。

legacyDecision：D-009


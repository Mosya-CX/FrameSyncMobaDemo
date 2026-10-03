# 战斗公式修正与动态 Operand

## 目标实现

Buff、装备和技能通过同一可追踪修正合同影响公式。

## 技术方案

CombatModifierRecord 提供固定槽位、Operation、匹配过滤、受限线性 Operand 和 PolicyPatch；来源持有 CombatModifierHandle 并负责清理。

## 边界情况

不允许任意脚本表达式决定确定性公式；同槽稳定合并；普通死亡不全局擦除其他来源。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Modifiers/CombatModifierRecord.cs`：当前关联实现定义 CombatModifierRecord、CombatModifierMatch、CombatFormulaPatch、CombatOperand、CombatOperandTerm（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Modifiers/CombatModifierSet.cs`：当前关联实现定义 CombatModifierSet、RecordComparer、CombatFormulaAccumulator、CombatPolicyResolution（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/CombatModifierSetSnapshotTests.cs`：CaptureRestore_RoundTrip_PreservesAllRecords、Capture_DeepCopiesFormulaAndPolicyContent、Restore_DoesNotCallAttachDetachClear、RestoreAfterDetach_PreservesOriginalSet、RollbackReplay_Equivalence、Determinism_SameSequence_SameSnapshot、Capture_EmptySet_ProducesEmptySnapshot。
- `Assets/Scripts/Gameplay/Tests/CombatModifierSetTests.cs`：Attach_ReturnsValidHandle、Attach_DuplicateId_ThrowsDeterministicSimulationException、Detach_ValidHandle_RemovesRecord、Detach_WrongOwnerUid_ReturnsFalse、Detach_AlreadyDetached_ReturnsFalse、Collect_OutputSortedByModifierId、Collect_Deterministic_InsertOrderInvariant。
- `Assets/Scripts/Gameplay/Tests/CombatSystemTests.cs`：SubmitDamage_ValidRequest_ReducesHealth、Omnivamp_HealsSourceForFractionOfSettledDamage、NaturalRegen_AppliesPerInterval_ForHealthAndCastResource、DamageFormula_ArmorReducesDamage、ZeroArmor_FullDamageApplied、CombatModifiers_ApplyOutgoingAndIncomingFinalPatches、FatalDamage_CompletesFormalDeathSettlement。
- `Assets/Scripts/Gameplay/Tests/MinionInitialBuffTests.cs`：InitialBuffs_AppliedAutomaticallyFromPrototype、MeleeMinion_AttacksMinion_AddsTwoPercentCurrentHealth、RangedMinion_AttacksMinion_AddsThreePointFivePercentCurrentHealth、Minion_AttacksStructure_DealsSixtyPercent。
- `Assets/Scripts/FrameSync/Tests/SnapshotChecksumCompletenessTests.cs`：AggregateSnapshot_RestoresIntentDashAndLocomotion、SharedChecksum_ChangesForIntentDashAndLocomotionState、CombatModifierCapture_IsCanonicalAndDetachRepairsShiftedIndices、AggregateSnapshot_CapturesLiveActionRuntime、ExecuteTick_FormalDeathInvalidationCapturesRestorableBoundary、SharedChecksum_SerializesEveryActionRuntimeSlotMember、Restore_RejectsActionRuntimeWithoutOwningHandlerState。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

`CombatModifierRecord` 表示当前某个技能状态、Buff 实例、装备被动或其它明确生效点，对来源单位或目标单位战斗公式提供的一组运行时修改。

本版冻结：

```text
Modifier 由具体生效点动态创建，是纯 C# 数据对象。
Modifier 没有独立生命周期。
Modifier 不保存 Priority、ExpireTick 或 RemainingUses。
Modifier 必须与创建它的生效点共同存在和消失。
Modifier 统一挂载到 Unit.CombatModifierSet，方便 CombatSystem 查询。
```

职责边界：

```text
技能 / Buff / 装备等生效点
    判断条件。
    计算自身动态状态。
    创建和移除 Modifier；修正内容变化时重新挂载。
    在单位事件回调中决定状态是否结束。

Unit.CombatModifierSet
    保存当前有效 Record。
    按 Id 提供 Attach / Detach 和查询。

CombatSystem
    只读取 Record。
    根据 Match、FormulaSlot、Operation 和 Operand 修改固定公式。
    不管理来源效果的持续时间、次数、层数或冷却。
```

---

### CombatModifierRecord

推荐结构：

```csharp
public sealed class CombatModifierRecord
{
    // 由挂载端填写的稳定 ID。Record 不缓存 Handle。
    public ulong Id;

    public CombatDomain Domain;
    public CombatModifierScope Scope;
    public CombatModifierMatch Match;

    public CombatFormulaPatch[] ValuePatches;
    public CombatPolicyPatch[] PolicyPatches;
}
```

```csharp
public enum CombatDomain : byte
{
    Damage,
    Heal,
    Shield
}

public enum CombatModifierScope : byte
{
    Outgoing,
    Incoming
}
```

`Outgoing` 表示从请求来源单位查询，`Incoming` 表示从请求目标单位查询。

一条 Record 可以包含多个 Patch。例如同一个强化状态可以同时提供：

```text
CoreValue + 50
FinalValue × 1.1
ForceCrit
```

共享同一条 Record，避免把一个生效点拆成多个难以统一管理的运行对象。

---

### Id 与 Handle

`CombatModifierRecord` 只保存：

```text
Id
```

不保存：

```text
CombatModifierHandle
```

挂载端负责生成稳定 Id，通常由自己的稳定字符串信息计算确定性哈希，例如：

```text
"Buff/{BuffInstanceUid}/DamageReduction"
"Ability/{AbilityRuntimeUid}/EmpoweredDamage"
"Equipment/{EquipmentPassiveRuntimeUid}/ForceCrit"
```

推荐入口：

```csharp
ulong modifierId =
    CombatModifierId.FromStableString(stableText);
```

禁止使用：

```text
string.GetHashCode()
HashCode.Combine()
object.GetHashCode()
Unity InstanceId
当前内存地址
依赖当前系统区域文化的字符串格式化
```

`FromStableString` 必须使用项目冻结的 UTF-8 确定性哈希算法，例如固定实现的 `FNV-1a 64` 或 `xxHash64`。数值插入字符串时必须使用稳定、无区域差异的格式。

挂载：

```csharp
CombatModifierHandle handle =
    unit.CombatModifiers.Attach(record);
```

挂载端缓存 `handle`：

```text
BuffRuntime / AbilityRuntime / EquipmentPassiveRuntime
    保存自己当前挂载 Modifier 的 Handle。
```

后续移除：

```csharp
unit.CombatModifiers.Detach(handle);
```

Record 挂载后视为只读；若来源效果的层数、倍率或其它公式内容发生变化，挂载端必须先移除旧 Record，再创建并挂载新 Record：

```csharp
unit.CombatModifiers.Detach(handle);

CombatModifierRecord rebuiltRecord =
    BuildCombatModifierRecord();

handle = unit.CombatModifiers.Attach(rebuiltRecord);
```

重新挂载时可以继续使用原稳定 `Record.Id`，但必须缓存 `Attach` 返回的新 Handle。该替换过程只能发生在来源效果自己的合法同步执行点，禁止在 `CombatModifierSet.Collect` 遍历期间进行。

约束：

```text
Record.Id 在一次挂载生命周期内不可改变。
同一 Unit.CombatModifierSet 中 Id 必须唯一。
当前仍挂载相同 Id 时再次 Attach 必须确定性报错；只有旧 Record 已成功 Detach 后，才允许使用同一稳定 Id 重新 Attach。
Detach 成功后挂载端必须清空旧 Handle。
Record 不得通过 Handle 反向定位或控制挂载端效果。
```

`CombatModifierSet` 在 Attach 时应保留足够的调试来源信息用于检测哈希碰撞；相同 Id 但来源描述不一致时必须产生确定性碰撞错误，不能默认覆盖。

---

### 生命周期与事件驱动

Modifier 的生命周期完全由生效点管理：

```text
BuffRuntime 创建
    -> 动态创建 Record
    -> Attach 到 Owner Unit

Buff 层数或内部状态变化
    -> 使用 Handle Detach 旧 Record
    -> 重新计算并创建新 Record
    -> 使用原稳定 Id Attach
    -> 缓存新的 Handle

BuffRuntime 移除
    -> 使用 Handle Detach
```

技能和装备被动同理。

次数、持续时间、充能和冷却归来源 Runtime：

```text
下一次攻击必暴击
    不使用 RemainingUses。

装备被动进入“强化攻击就绪”
    -> 挂载 ForceCrit Record

匹配的 DamageDealt 事件成立
    -> EquipmentHandler 的 Reaction 结束强化状态
    -> 来源 Runtime 使用 Handle 移除 Record
```

条件判断也归具体生效点：

```text
目标生命低于 30% 时开启增伤
    -> 效果实例在自己的合法检查点读取 StatHandler
    -> 条件首次成立时挂载 Modifier
    -> 已挂载且修正值变化时 Detach 后重新 Attach
    -> 条件失效时移除 Modifier
```

事件回调只能影响后续请求。已经形成的 `DamageResult / HealResult / ShieldResult` 不允许被事件倒过来修改。

---

### CombatModifierMatch

`Match` 只描述 Record 适用于哪些请求，不承载复杂 Gameplay 条件：

```csharp
public readonly struct CombatModifierMatch
{
    public readonly SourceTypeMask SourceTypes;

    // Invalid / 0 表示不限制。
    public readonly int SourceId;
    public readonly int RecipeId;

    public readonly DamageTypeMask DamageTypes;
}
```

用途示例：

| 效果 | Match |
|---|---|
| 所有造成伤害提高 | `Domain = Damage, Scope = Outgoing`，其余不限制 |
| 仅普攻必暴击 | `SourceTypes = Attack` |
| 仅某个技能伤害提高 | `SourceId = 对应 AbilityId` 或限定 `RecipeId` |
| 仅受到物理伤害降低 | `Scope = Incoming, DamageTypes = Physical` |

`Match` 不检查：

```text
目标当前生命比例
Buff 层数
技能 Stage
装备充能
冷却是否完成
```

这些动态条件由挂载端决定当前 Record 是否应该存在以及其数值是多少。

---

### 固定公式槽位

Modifier 不插入任意代码位置，只能修改战斗管线开放的固定中间值：

```csharp
public enum CombatFormulaSlot : byte
{
    CoreValue,
    PreDefenseValue,
    DefenseInput,
    PostDefenseValue,
    FinalValue,
    DerivedValue
}
```

| Slot | 含义 |
|---|---|
| `CoreValue` | Recipe 基础公式完成后的值 |
| `PreDefenseValue` | 护甲 / 魔抗减免前的伤害值 |
| `DefenseInput` | 本次参与抗性公式的有效护甲或魔抗 |
| `PostDefenseValue` | 抗性减免完成后的值 |
| `FinalValue` | 最终伤害、治疗或护盾应用前的值 |
| `DerivedValue` | 生命偷取、全能吸血等派生值 |

治疗和护盾通常只使用：

```text
CoreValue
FinalValue
```

伤害可以使用全部相关槽位。

暴击资格、护盾绕过等非数值规则不塞入数值槽，而由 `CombatPolicyPatch` 处理。

---

### Operation

数值 Patch：

```csharp
public readonly struct CombatFormulaPatch
{
    public readonly CombatFormulaSlot Slot;
    public readonly CombatModifierOperation Operation;
    public readonly CombatOperand Operand;
}
```

```csharp
public enum CombatModifierOperation : byte
{
    Add,
    Multiply,
    ClampMin,
    ClampMax
}
```

不保留通用 `Override`，因为多个覆盖效果在没有 Priority 时缺少自然冲突规则。确实需要替换某项策略时，应增加明确的 `CombatPolicyPatch` 或专用公式槽。

---

### CombatOperand：受限线性表达式

`CombatOperand` 不再是塞有大量互斥字段的 Kind 容器，而是一条统一的线性表达式：

```text
OperandValue = Constant + Σ(ValueRef × Coefficient)
```

推荐结构：

```csharp
public readonly struct CombatOperand
{
    public readonly fp Constant;
    public readonly CombatOperandTerm[] Terms;
}

public readonly struct CombatOperandTerm
{
    public readonly CombatValueRef Value;
    public readonly fp Coefficient;
}

public readonly struct CombatValueRef
{
    public readonly CombatValueRefKind Kind;
    public readonly ushort ValueId;
}
```

```csharp
public enum CombatValueRefKind : byte
{
    BaseValue,
    CurrentSlotValue,
    SourceStat,
    TargetStat
}
```

语义：

| ValueRef | 读取内容 |
|---|---|
| `BaseValue` | 当前请求提交的原始 `BaseValue` |
| `CurrentSlotValue` | 当前槽位进入 Modifier 合并前的固定输入值 |
| `SourceStat` | 来源单位 `StatHandler` 的指定 StatId |
| `TargetStat` | 目标单位 `StatHandler` 的指定 StatId |

示例：

```text
50
    -> Constant = 50

0.2 × Source.AP
    -> Constant = 0
    -> SourceStat(AP) × 0.2

50 + 0.2 × Source.AP + 0.05 × Target.MaxHealth
    -> Constant = 50
    -> SourceStat(AP) × 0.2
    -> TargetStat(MaxHealth) × 0.05
```

Buff 层数、技能蓄力、装备充能等来源 Runtime 数据不进入 `CombatValueRef`。来源效果先自行计算，再把结果写成 Operand 的 `Constant` 或 `Coefficient`；状态变化时使用 Handle 更新 Record。

---

### 同一槽位的稳定合并规则

删除 `Priority` 后，同一槽位不能按挂载顺序依次执行，否则加法和乘法顺序会改变结果。

对所有匹配且当前有效的 Patch，统一计算：

```text
AddTotal = 所有 Add OperandValue 之和
MultiplierTotal = 所有 Multiply OperandValue 之积
LowerBound = 所有 ClampMin OperandValue 中的最大值
UpperBound = 所有 ClampMax OperandValue 中的最小值
```

然后：

```text
SlotOutput = Clamp(
    (SlotInput + AddTotal) × MultiplierTotal,
    LowerBound,
    UpperBound
)
```

如果没有对应约束：

```text
AddTotal = 0
MultiplierTotal = 1
LowerBound = 无下限
UpperBound = 无上限
```

`CurrentSlotValue` 对同一槽位的所有 Operand 都表示相同的 `SlotInput`，不会随着某条 Patch 的处理而变化，因此结果与 Record 枚举顺序无关。

不同先后语义由不同 `FormulaSlot` 表达。例如：

```text
CoreValue Add 50
FinalValue Multiply 1.1
```

表示先增加基础值，再经过暴击和抗性，最后将最终值提高 10%。

---

### Damage 最终值公式

伤害固定管线：

```text
1. RecipeValue = DamageRecipe(BaseValue, SourceStats, TargetStats, RuntimeParams)
2. CoreValue = ApplySlot(CoreValue, RecipeValue)
3. CritValue = ResolveCrit(CoreValue, RecipeDefault + PolicyPatches)
4. PreDefenseValue = ApplySlot(PreDefenseValue, CritValue)
5. BaseDefenseInput = ResolveArmorOrMagicResistance(Target, SourcePenetration)
6. EffectiveDefense = ApplySlot(DefenseInput, BaseDefenseInput)
7. MitigatedValue = ApplyResistanceFormula(PreDefenseValue, EffectiveDefense, DamageType)
8. PostDefenseValue = ApplySlot(PostDefenseValue, MitigatedValue)
9. CalculatedDamage = Max(0, ApplySlot(FinalValue, PostDefenseValue))
10. StatHandler 按 ShieldType 吸收伤害
11. ActualLifeDamage = CalculatedDamage - ActualShieldDamage
```

结果区分：

```text
CalculatedDamage
ActualShieldDamage
ActualLifeDamage
```

白盾吸收所有可吸收伤害；物理盾只吸收物理伤害；魔法盾和黑盾只吸收魔法伤害。

---

### Heal 最终值公式

```text
1. RecipeValue = HealRecipe(BaseValue, SourceStats, TargetStats, RuntimeParams)
2. CoreValue = ApplySlot(CoreValue, RecipeValue)
3. CalculatedHeal = Max(0, ApplySlot(FinalValue, CoreValue))
4. ActualHeal = Min(CalculatedHeal, TargetMaxHealth - TargetCurrentHealth)
```

`CalculatedHeal` 是公式结果；`ActualHeal` 是扣除溢出治疗后真正写入生命的值。

---

### Shield 最终值公式

```text
1. RecipeValue = ShieldRecipe(BaseValue, SourceStats, TargetStats, RuntimeParams)
2. CoreValue = ApplySlot(CoreValue, RecipeValue)
3. CalculatedShield = Max(0, ApplySlot(FinalValue, CoreValue))
4. ActualShield = StatHandler.AddShield(ShieldType, CalculatedShield, DurationPolicy)
```

如果没有护盾上限或拒绝规则：

```text
ActualShield = CalculatedShield
```

`ShieldType` 不改变护盾生成公式，只决定后续伤害吸收匹配和黑盾附加控制免疫语义。

---

### CombatPolicyPatch

非数值规则使用独立结构：

```csharp
public readonly struct CombatPolicyPatch
{
    public readonly CombatPolicyKind Kind;
}
```

第一版可包含：

```text
ForceCrit
ForbidCrit
IgnoreAllShield
IgnorePhysicalShield
IgnoreMagicShield
```

冲突规则必须由代码冻结。例如：

```text
ForbidCrit > ForceCrit > Recipe Default
```

策略 Patch 不使用 Operand，也不依赖挂载顺序。

---

### Collector 查询流程

`CombatModifierCollector` 不保存 Provider，也不允许动态注册委托。

每个请求结算时：

```text
1. 查询 SourceUnit.CombatModifierSet 的 Outgoing Record。
2. 查询 TargetUnit.CombatModifierSet 的 Incoming Record。
3. 使用 Domain、Scope 和 Match 做基础过滤。
4. 解析每条 Patch 的 Operand。
5. 按 FormulaSlot 汇总 Add / Multiply / Clamp。
6. 汇总 PolicyPatch。
7. 运行固定伤害、治疗或护盾管线。
```

Collector 在查询期间只读：

```text
不得 Attach / Detach Modifier。
不得发布单位事件。
不得提交新的战斗请求。
不得修改来源效果 Runtime。
```

Modifier 的增删改只能发生在具体效果自己的确定性生效点或单位事件回调中。

---

### 快照恢复与动态属性接缝

`Unit.CombatModifierSet` 的当前有效不可变 Record 是正式 Gameplay 状态，不在回滚恢复后重新挂载。

统一恢复规则：

```text
Capture
    -> 保存 CombatModifierSet 当前 Record 集合、确定性顺序与必要容器状态
    -> 来源 Runtime 同时保存自己持有的 CombatModifierHandle

Restore
    -> 直接恢复历史 Record 集合与来源 Runtime Handle
    -> 不调用 Attach / Detach / Clear
    -> 不触发来源效果、单位事件或战斗请求

Resolve
    -> 修复 OwnerUnitUid 与必要的跨系统稳定引用

Rebuild
    -> 只重建查询索引、临时 Buffer 与调试缓存
    -> 不重新 Attach CombatModifier
```

如果 Record 与来源 Runtime 的 Handle 在同一快照中不一致，应视为快照字段缺失或来源生命周期 Bug，不允许通过 Rebuild 重新执行生效逻辑来掩盖。

长期属性的动态换算由 Buff、技能或装备等来源 Runtime 在 Combat 阶段之前处理。例如装备用 Tick + `StatHandler.WatchHook.GetChangeThisTick` 发现来源属性变化，再通过 `SetModifierValue` 更新目标长期属性。CombatSystem 不参与属性依赖传播，只在请求结算时通过：

```csharp
Source.StatHandler.GetStat(statId)
Target.StatHandler.GetStat(statId)
```

读取当时已经成立的最终属性值。


## 需求演进

### 2026-10-02

变动内容：正式死亡由 UnitWorld 同步执行，来源系统仅清理自己的句柄。

legacyDecision：D-009


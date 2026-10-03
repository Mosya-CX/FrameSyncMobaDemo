# 强类型单位事件与反应

## 目标实现

伤害、施法、死亡等事实立即进入其固定监听者。

## 技术方案

UnitEventBus 按正式事件数据强类型路由 Handler；支持列表和即时顺序明确，Reaction 只生成所允许的后续请求。

## 边界情况

死亡/击杀事件回调在 T 立即发生，但新普通 Shield、Damage、Heal 延迟到 T+1，合法序列缺口不重编号。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Combat/CombatEvents.cs`：当前关联实现定义 CombatEvents、DamageEventData、HealEventData、ShieldEventData、OnHitEventData（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Core/UnitEventBus.cs`：当前关联实现定义 UnitEventBus（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/CombatEnhancementTests.cs`：Crit_100PercentChance_DoublesDamage、Crit_ZeroPercentChance_NoCrit、Crit_DamageEventData_IsCriticalFlag、Crit_OneHundredPercent_DoesNotRequireRandomService、ProbabilisticCrit_DoesNotConsumeGlobalRandomState、AttackSpeed_ModifiesCooldown、AttackSpeed_PositiveBase_StartsAttack。
- `Assets/Scripts/Gameplay/Tests/CrowdControlHandlerTests.cs`：Add_CreatesIndependentInstances_NoMerge、Immunity_BlocksLowMedium_ConsumesOneShot_BypassesHigh、Cleanse_RemovesMatchingNonHigh_RespectsCount、Unstoppable_SuppressesOutput_AndRejectsForcedMove、DamageTakenSignal_RemovesSleepInstance、Drowsy_OnNaturalExpire_AddsSleepWithConfiguredDuration、Tenacity_ShortensDefaultDuration_IgnoredByIgnoreRule。
- `Assets/Scripts/Gameplay/Tests/MinionThreatSystemTests.cs`：Acquisition_SetsInitialThreat_AndPicksClosestUnclaimed、DamageTaken_AddsThreat_InverselyProportionalToDistance、HigherThreatTarget_SwitchesOnlyWhenNotInWindup、Acquisition_PairsAlliesWithDistinctTargets、ThreatTable_SnapshotRoundTrip_PreservesEntries、SnapshotRoundTrip_PreservesLastThreatRefreshTick、RestoreReplacement_UnsubscribesPreviousController。
- `Assets/Scripts/Gameplay/Tests/PassivePAbilityTests.cs`：KillNonHero_GrantsAttackSpeedAndDerivedStats、KillHero_AppliesThreeTimesBonus、Kill_RefreshesBuffDurationToFiveSeconds、Cooldown_ResolvesPerAbilityLevel、EmpoweredExpiry_ReappliesOneNormalBuffAfterNonHeroKills、AssistHero_AppliesEmpoweredBonusToAssistant。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

`UnitEventBus` 是每个 `Unit` 固定持有的确定性 Gameplay 结果事件路由器。

它负责：

```text
接收已经正式成立的强类型结果事件。
立即、同步、按固定顺序调用具体 Handler。
把事件交给各 Handler 自己的 Reaction 静态配置处理。
```

它不负责：

```text
动态 C# delegate 订阅。
运行时注册监听者。
统一 UnitEventRecord。
统一 EventType + Payload。
全局事件队列。
Tick 末 Drain。
事件历史持久化。
伤害、治疗、技能、攻击或控制的业务结算。
表现层广播。
```

`UnitEventBus` 不是 `UnitHandler`，也不继承 `MonoBehaviour`。  
它由 `Unit` 创建或持有，并直接引用 `Unit` 已缓存的具体 Handler。

结果事件与请求必须区分：

```text
Request
    表示希望某系统之后执行一项业务。
    例如 DamageRequest、HealRequest、ShieldRequest。

Result Event
    表示某项业务已经正式结算成立。
    例如 DamageTaken、HealDealt、UnitDeath。
```

Reaction 如果需要产生新的 Gameplay 结果，必须向对应系统提交正式请求，不能由 `UnitEventBus` 自己结算。

---

### 冻结事件清单

v27.1 暂时只保留以下 11 种事件，不多加也不少减：

| 事件 | 语义 |
|---|---|
| `DamageTaken` | 本单位受到伤害 |
| `DamageDealt` | 本单位对其它单位造成伤害 |
| `HealTaken` | 本单位受到治疗 |
| `HealDealt` | 本单位对其它单位造成治疗 |
| `AbilityCast` | `AbilityHandler` 在推进技能 Stage 时，根据技能配置确认需要触发的一次施法回调 |
| `UnitDying` | 本单位触发致死条件，进入当前死亡判定 |
| `UnitDeath` | `CombatSystem` 完成死亡结算，`UnitWorld` 接受死亡判决并让本单位进入逻辑死亡 |
| `UnitKill` | `CombatSystem` 结算出本单位击杀另一单位后创建、保存并发布的逻辑事件；它本身不是权威击杀 |
| `LevelUp` | 本单位成功提升一级；连续升级时逐级发布 |
| `UnitCollisionEnter` | 与敌方单位进入轻量碰撞 |
| `UnitCollisionExit` | 与敌方单位离开轻量碰撞 |

不新增：

```text
AttackStarted
AttackCommitted
AttackHit
ActionStarted
ActionPhaseChanged
ActionFinished
AbilityStage
DeathAnimationFinished
DeathDisposed
```

这些如果属于其它模块内部信号或查询状态，应由对应模块自己管理，不进入当前 `UnitEventBus` 清单。

---

### 每种事件使用独立强类型结构

不同事件需要的字段不同，因此不设计统一：

```text
UnitEventRecord
UnitEventType
UnitEventPayload
PayloadKind
object Payload
```

每种事件拥有自己的强类型数据：

```csharp
public readonly struct DamageTakenEvent
{
    public readonly UnitUid SourceUnitUid;

    public readonly SourceDescriptor Source;
    public readonly int RecipeId;
    public readonly DamageType DamageType;

    public readonly fp CalculatedDamage;
    public readonly fp ActualShieldDamage;
    public readonly fp ActualLifeDamage;

    public readonly bool WasCritical;
    public readonly fp RemainingHealth;
}

public readonly struct DamageDealtEvent
{
    public readonly UnitUid TargetUnitUid;

    public readonly SourceDescriptor Source;
    public readonly int RecipeId;
    public readonly DamageType DamageType;

    public readonly fp CalculatedDamage;
    public readonly fp ActualShieldDamage;
    public readonly fp ActualLifeDamage;

    public readonly bool WasCritical;
}

public readonly struct HealTakenEvent
{
    public readonly UnitUid SourceUnitUid;

    public readonly SourceDescriptor Source;
    public readonly int RecipeId;

    public readonly fp CalculatedHeal;
    public readonly fp ActualHeal;
    public readonly fp CurrentHealth;
}

public readonly struct HealDealtEvent
{
    public readonly UnitUid TargetUnitUid;

    public readonly SourceDescriptor Source;
    public readonly int RecipeId;

    public readonly fp CalculatedHeal;
    public readonly fp ActualHeal;
}

public readonly struct AbilityCastEvent
{
    public readonly AbilityId AbilityId;
    public readonly AbilitySessionUid AbilitySessionUid;
}

public readonly struct UnitDyingEvent
{
    public readonly UnitUid SourceUnitUid;
    public readonly DyingReason Reason;
}

public readonly struct UnitDeathEvent
{
    public readonly UnitUid KillerUnitUid;
    public readonly DeathReason Reason;
    public readonly fp2 DeathPosition;
}

public readonly struct UnitKillEvent
{
    public readonly UnitUid VictimUnitUid;
    public readonly KillReason Reason;
}

public readonly struct LevelUpEvent
{
    public readonly int PreviousLevel;
    public readonly int CurrentLevel;
}

public readonly struct UnitCollisionEnterEvent
{
    public readonly UnitUid OtherUnitUid;
    public readonly fp2 ContactNormal;
}

public readonly struct UnitCollisionExitEvent
{
    public readonly UnitUid OtherUnitUid;
}
```

伤害字段语义：

| 字段 | 说明 |
|---|---|
| `CalculatedDamage` | 完成战斗公式计算后、护盾吸收前的伤害 |
| `ActualShieldDamage` | 实际由匹配护盾吸收的数值 |
| `ActualLifeDamage` | 实际从生命值扣除的数值 |
| `RemainingHealth` | 本次伤害结算完成后的目标生命 |
| `WasCritical` | 本次结果是否以暴击成立 |

治疗字段语义：

| 字段 | 说明 |
|---|---|
| `CalculatedHeal` | 完成战斗公式计算后的治疗值 |
| `ActualHeal` | 排除生命上限溢出后真正写入的治疗量 |
| `CurrentHealth` | 本次治疗结算完成后的目标生命 |

`SourceDescriptor + RecipeId` 用于让 Buff、技能和装备 Reaction 精确判断结果来源。  
事件中不携带：

```text
CombatModifierHandle
CombatModifierRecord
AppliedModifierIds
```

来源效果根据自身 Runtime 状态和本次结果决定是否结束，并使用自己缓存的 Handle 执行 `Detach`。  
事件只能影响后续 Gameplay 请求，不能倒过来修改已经成立的本次伤害或治疗结果。

事件中不保存：

```text
EventLogicTick
```

事件写入后立即分发。需要当前 Tick 时，生产者或监听者直接读取：

```csharp
int currentLogicTick =
    SimulationTickContext.Current.Tick;
```

只有确实需要跨 Tick 保存、权威确认或回滚定位的外部结果记录，才由其所属系统自行保存对应 LogicTick。

---

### UnitEventBus 直接路由具体 Handler

`UnitEventBus` 不扫描接口、不动态订阅、不构建运行时监听者列表。  
程序在每个 `Publish` 重载中直接调用真正支持该事件的具体 Handler。

冻结规则：

```text
1. Ability、Attack、Buff、Equipment、CrowdControl 等模块
   在各自设计案中声明真实 SupportedUnitEvents。

2. UnitEventBus 只写入这些真实支持关系。

3. 不支持某事件的 Handler 不进入对应 Publish。

4. 不为了凑齐路由而增加空函数。

5. 路由顺序由代码固定，是确定性 Gameplay 规则。
```

以 `UnitDeath` 为例，`Publish` 中只能保留各模块最新版 `SupportedUnitEvents` 已正式声明的方法。  
当前文档不再示例调用未声明存在的：

```text
AttackHandler.OnUnitDeath
CrowdControlHandler.OnUnitDeath
```

概念写法：

```csharp
public void Publish(in UnitDeathEvent evt)
{
    // 仅保留最新版模块设计案明确支持 UnitDeath 的直接调用。
    _owner.AbilityHandler?.OnUnitDeath(evt);
    _owner.BuffHandler?.OnUnitDeath(evt);
}
```

这里仍然是编译期明确的直接路由，不是动态订阅、反射或运行时接口扫描。  
未来某个 Handler 新增真实 Reaction 时，必须同时修改该模块的 `SupportedUnitEvents` 和 `UnitEventBus` 对应固定路由。

### Handler 回调与 Reaction 配置

每个具体 Handler 只实现自己在 `SupportedUnitEvents` 中声明的强类型回调。以下仅以 BuffHandler 为示意：

```csharp
public sealed class BuffHandler : UnitHandler
{
    public void OnDamageTaken(
        in DamageTakenEvent evt);

    public void OnDamageDealt(
        in DamageDealtEvent evt);

    public void OnHealTaken(
        in HealTakenEvent evt);

    public void OnAbilityCast(
        in AbilityCastEvent evt);

    public void OnUnitDying(
        in UnitDyingEvent evt);

    public void OnUnitDeath(
        in UnitDeathEvent evt);

    public void OnUnitKill(
        in UnitKillEvent evt);

    public void OnLevelUp(
        in LevelUpEvent evt);
}
```

不需要统一：

```text
HandleUnitEvent(UnitEventRecord)
```

也不要求所有 Handler 为所有事件实现空方法。

Handler 收到事件后，再查询自己的 Reaction 静态配置：

```text
UnitEventBus.Publish(DamageTakenEvent)
    ↓
BuffHandler.OnDamageTaken
    ↓
查询 Buff Reaction 配置
    ↓
满足触发条件
    ↓
向对应系统提交正式 Request
```

`UnitEventBus` 不理解具体 Buff、装备、技能被动和 Reaction 条件。

---

### 即时同步分发

事件生产者在结果正式成立后直接调用：

```csharp
unit.EventBus.Publish(evt);
```

`Publish` 返回前，当前事件的所有固定 Handler 回调已经完成。

不增加：

```text
PendingEvents
IsDispatching
Drain
MaxEventsPerTick
全局 GameplayEventQueue
```

通常不会形成事件递归，因为 Gameplay 业务采用“请求先缓存、系统统一结算”的方式：

```text
DamageTaken Reaction
    ↓
提交新的 DamageRequest
    ↓
CombatSystem 缓存并按自己的固定顺序处理
    ↓
新的 DamageResult 正式成立
    ↓
再发布新的 DamageTaken / DamageDealt
```

请求何时被对应系统消费由该系统设计案决定，不由 `UnitEventBus` 规定。

---

### 事件生产接缝

#### 伤害

```text
CombatSystem 完成 DamageResult
    ↓
Target.EventBus.Publish(DamageTaken)
    ↓
Source.EventBus.Publish(DamageDealt)
```

#### 治疗

```text
CombatSystem 完成 HealResult
    ↓
Target.EventBus.Publish(HealTaken)
    ↓
Source.EventBus.Publish(HealDealt)
```

#### 技能施放

```text
AbilityHandler 推进 AbilitySession Stage
    ↓
技能配置确认当前节点需要触发施法回调
    ↓
Owner.EventBus.Publish(AbilityCast)
```

`AbilityCast` 不负责多段技能、技能 Stage 广播、表现动作或 Session 结束通知。

#### 死亡判定

```text
CombatSystem 发现致死条件
    ↓
请求 UnitWorld 进入 Dying
    ↓
UnitWorld 写入 LifeState.Dying
    ↓
Victim.EventBus.Publish(UnitDying)
```

#### 逻辑死亡

```text
CombatSystem 完成死亡结算
    ↓
请求 UnitWorld 确认死亡
    ↓
UnitWorld 写入 LifeState.Dead
    ↓
Victim.EventBus.Publish(UnitDeath)
    ↓
死亡回调完成
    ↓
UnitWorld 清理非必要状态
```

#### 击杀

```text
CombatSystem 完成击杀归属结算并保存逻辑击杀结果
    ↓
Killer.EventBus.Publish(UnitKill)
```

`UnitKill` 不是权威帧确认后的正式比赛记录。

#### 升级

```text
StatHandler.AddExperience
    ↓
成功提升一级
    ↓
Owner.EventBus.Publish(LevelUp)
```

连续升级时逐级发布。

#### 轻量单位碰撞

```text
Physics / Unit Collision Bridge
    ↓
碰撞关系正式进入或离开
    ↓
对应 Unit.EventBus.Publish(UnitCollisionEnter / Exit)
```

---

### 生命周期与事件顺序

`UnitDeath` 必须先于死亡清理：

```text
LifeState = Dead
    ↓
Publish UnitDeath
    ↓
所有死亡 Reaction 完成
    ↓
清理 Action、Intent、Buff、护盾、控制等非必要状态
    ↓
播放死亡动画
```

这样死亡时 Reaction 仍能读取和处理本单位死亡前保留的必要运行状态。

死亡动画播放完成不是 Gameplay 事件。  
之后是否保留、回池、销毁或生成废墟由 `UnitWorld` 继续处理。

对象池重置时不需要“清理事件订阅”，因为 `UnitEventBus` 没有动态订阅。  
只需要保证它重新绑定到当前 `Unit` 固定 Handler 引用，或在预制体结构稳定时保持原绑定。

> **帧同步设计关注点**


## 需求演进

### 2026-10-02

变动内容：死亡和击杀反应即时分发，产生的新普通战斗请求延至下一 Tick。

legacyDecision：D-010

### 2026-10-02

变动内容：稳定表现事件身份由逻辑来源持有，Gameplay 不直接播放音效。

legacyDecision：D-014


# 标准控制效果与复合实例

## 本功能范围

本案细化“控制实例与模块参数”中的标准控制效果与复合实例，仅覆盖下列明确接口与边界。

## 目标实现

配置型控制通过静态模块表和有界参数块执行。

## 技术方案

CrowdControlHandler 唯一运行入口；Definition 经 Bake 形成 module op 与 ParamLayout；实例按 Key 暴露、按 Offset 存储，不创建每实例模块对象。

## 边界情况

不新增 Kind、软硬控枚举或 Combat 控制管线；模块重入使用明确延迟规则；缺键和容量溢出可见失败。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。

### Stun

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Stun |
| DurationRule | DefaultTenacity |
| Modules | BlockActions |
| BlockedActions | VoluntaryMove、Turn、VoluntaryAttack、AbilityCast、Mobility、ControlMove、ControlAttack |

没有 Stun 函数，也没有 Kind == Stun 分支。

### Root

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Root |
| Modules | BlockActions |
| BlockedActions | VoluntaryMove、ControlMove、Mobility |

Root 是否允许 Turn 或普通 AbilityCast 由这个模块参数明确表达。

### Silence

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Silence |
| Modules | BlockActions |
| BlockedActions | AbilityCast |

如果某项目规则允许特定技能在 Silence 下使用，由单位框架的 Ability 启动规则处理，不给控制模块硬编码技能白名单。

### Blind

| 项 | 配置 |
|---|---|
| Intensity | Low |
| Tags | Control、Blind |
| Modules | BasicAttackMiss |
| 参数 | 无或命中规则位 |

AttackHandler 在命中阶段查询 ActiveTags 或专用轻量接口。

Blind 不禁止 VoluntaryAttack 启动。

### Slow

| 项 | 配置 |
|---|---|
| Intensity | Low |
| Tags | Control、Slow |
| Modules | MaxMoveSlow |
| 参数 Key | MoveSlowRatio：调用方写入 fp |

多个 Slow 实例全部保留，MaxMoveSlow 模块取最大值。

### Cripple

| 项 | 配置 |
|---|---|
| Intensity | Low |
| Tags | Control、Cripple |
| Modules | MaxAttackSpeedSlow |
| 参数 Key | AttackSpeedSlowRatio |

不与 Slow 共用含义模糊的 Aggregation 通道。

### Taunt

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、ForcedBehavior、Taunt |
| Modules | BlockActions、ForcedBehavior |
| BlockedActions | VoluntaryMove、VoluntaryAttack、AbilityCast |
| 参数 Key | TargetUnitUid、Priority、BehaviorId |

ForcedBehavior 模块提供 ControlAttack / ControlMove 候选，Planner 执行。

### Charm

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、ForcedBehavior、Charm |
| Modules | BlockActions、ForcedBehavior、可选 MaxMoveSlow |
| 参数 Key | TargetUnitUid、Priority、MoveSlowRatio |

同一组标准模块组合出“向目标移动且减速”的效果。

### Fear / Flee

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、ForcedBehavior、Fear |
| Modules | BlockActions、ForcedBehavior |
| 参数 Key | Direction、Priority、MoveScale |

方向必须由确定性 Gameplay 逻辑用 Direction Key 写入 fp2，不使用 Unity Random。

### KnockBack

| 项 | 配置 |
|---|---|
| Intensity | High |
| Tags | Control、ForcedMove、Displacement、Airborne |
| DurationRule | IgnoreTenacity |
| Modules | BlockActions、ForcedMoveOnAdd |
| 参数 Key | Direction fp2、Distance fp、MoveTicks int、ForcedMovePriority short |

由于 Intensity = High，控制免疫不会拦截 KnockBack，Cleanse 也不能移除它。

不可阻挡期间，该请求无论 High 与否都直接被 Handler 拒绝，不创建实例。不存在不可阻挡时，Handler 按 ForcedMovePriority 决定启动、替换或拒绝；MovementHandler 不参与优先级判断。

### Suppression

| 项 | 配置 |
|---|---|
| Intensity | High 或 Medium，由项目决定 |
| Tags | Control、Suppression |
| DurationRule | IgnoreTenacity |
| Modules | BlockActions |
| BlockedActions | VoluntaryMove、Turn、VoluntaryAttack、AbilityCast、Mobility、EquipmentActive、SummonerSpell、ControlMove、ControlAttack |

若项目要求 Suppression 不可被控制免疫和 Cleanse，配置为 High；若允许解除，配置为 Medium。

### Polymorph

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Polymorph |
| Modules | BlockActions、MaxMoveSlow |
| 参数 Key | MoveSlowRatio |

模型变化和图标不属于控制 Definition。

### Sleep

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Sleep |
| Modules | BlockActions、RemoveOnSignal |
| Signal | ActualDamageTaken |

CombatSystem 在 DamageTaken 正式成立后通过 UnitEventBus 即时调用 Handler。Handler 只记录轻量事实；Sleep 模块只返回 RemoveSelf，不造成任何伤害。

### Drowsy 到 Sleep

Drowsy：

| 项 | 配置 |
|---|---|
| Intensity | Low |
| Tags | Control、Drowsy |
| Modules | AddControlOnNaturalExpire |
| 静态模块参数 | SleepControlId |
| 参数 Key | SleepDurationTicks（Int，必填）：转化后 Sleep 的持续 Tick，由施加方在 Add 时写入 |

OnRemove 只有 reason = NaturalExpire 时返回 AddControl 命令。

> `SleepControlId` 一个静态参数；持续时间属于每次施加的意图，按 4.9 的
> 参数哲学走 Key 参数。Bake 校验 `AddControlOnNaturalExpire` 必须绑定一个
> Int 类型参数偏移。

Cleanse Drowsy 不生成 Sleep。

### 复合效果何时拆实例

同一个控制 Definition 适合表达“总是一起出现、一起结束、一起净化”的模块组合。

需要拆成多个 Instance 的情况：

- 两部分持续时间不同；
- 两部分净化规则不同；
- 其中一部分可被免疫、另一部分必须保留；
- 两部分需要不同外部句柄；
- 其中一部分不是控制，而是伤害、治疗、护盾或 Buff。

---



## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036


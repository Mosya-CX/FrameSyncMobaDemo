# 死亡奖励与贡献窗口

## 目标实现

小兵、英雄、野怪、塔奖励使用可解释的整数与贡献规则。

## 技术方案

DeathRewardContext 从正式死亡与贡献日志选收受者，整数稳定分配；经验立即结算，金币统一 RequestGoldIncome，批次摘要参与共享校验。

## 边界情况

D-041 的生产者归属与既有复仇/击杀统计日志描述存在冲突，未确认部分不能静默改写；助攻窗口不受杀手修订而丢失。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Combat/CombatTickResult.cs`：当前关联实现定义 CombatTickResult、DeathResult、GoldAllocation（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/MatchGoldRewardTests.cs`：GoldAllocation_UsesIntegerAmountContract、MinionLastHit_ConfirmsFullConfiguredGold、MonsterLastHit_UsesAuthoredCreepScoreValue、HeroKill_WithTwoAssistants_Splits300As180_60_60、HeroKill_WithoutAssistants_ConfirmsFull300ToKiller。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### CombatContributionEventLog：跨 Tick 战斗事件日志

CombatSystem 为每个仍可能在未来死亡的受害单位维护一份轻量战斗事件日志。日志以**逐事件**方式保存窗口内该 Victim 受到/获得的有效战斗交互（伤害、护盾、治疗），支撑击杀者/助攻判定、死亡回放、伤害统计与后续审计：

```text
CombatContributionEventLog
    VictimUnitUid
    LastHitContributorUid
    Events[]          // 按 (LogicTick, SequenceInTick) 升序
```

```text
CombatContributionEvent
    VictimUnitUid
    ContributorHeroUid
    Kind              // Damage / Shield / Heal
    Amount : fp
    LogicTick
    SequenceInTick
```

#### 事件记录与写入时机

三类事件都在对应战斗请求结算成立后写入：

```text
Damage
    Amount = DamageResult.ActualShieldDamage + DamageResult.ActualLifeDamage
    Amount > 0 才写入（免疫、未命中或最终无实际损失不写）

Shield
    Amount = 本次护盾请求实际生效值
    Amount > 0 才写入

Heal
    Amount = 本次治疗请求有效治疗量
    Amount > 0 才写入
```

同一 Tick 内三类请求共用 `SequenceInTick` 全局顺序，事件按结算顺序追加，顺序天然确定。

来源解析：

```text
来源是 Hero
    -> ContributorHeroUid = SourceUnitUid

来源是召唤物、分身、宠物、陷阱或投掷物
    -> 沿 SourceDescriptor 的稳定所有者链解析所属 Hero

无法解析到 Hero
    -> 不写入事件
```

还必须满足：

```text
ContributorHeroUid 有效
ContributorHeroUid != VictimUnitUid
Contributor 与 Victim 为敌对关系
```

不满足其中任意一条的交互不写入事件日志。

#### 窗口、清理与容量

事件只在全局助攻时限内保留：

```text
AssistContributionDurationTicks（默认 150，约 5 秒）
```

CombatSystem 每 Tick 开始按 `VictimUnitUid` 升序对所有日志执行过期清理；读取某 Victim 的日志用于判定前再执行一次局部清理。过期条件：

```text
CurrentTick > ExpireLogicTick（= 事件 LogicTick + AssistContributionDurationTicks）
```

防御性容量上限：

```text
MaxContributionEventsPerVictim（默认 256）
```

超出上限时丢弃该 Victim 日志中最旧的事件（与过期语义一致），防止极端高频下日志无限增长。

以下场景删除整个 Victim 日志：

```text
Victim 正式死亡且 FormalDeathResult 已冻结
Victim 通过 UnitWorld.DespawnUnit 结束当前 UnitUid 生命周期
当前 UnitUid 被永久销毁或回滚拓扑静默移除
```

普通治疗、回满生命、脱战或仅进入 `Dying` 不立即清除日志，只由过期规则控制。

#### 击杀者与助攻判定

冻结 `DeathRewardContext` 前：

```text
1. 对 Victim 日志执行过期清理。
2. 击杀者 = 最后一条 Kind=Damage 事件（按 LogicTick、SequenceInTick 序）的
   ContributorHeroUid；无有效 Damage 事件时击杀者为空。
3. 助攻者 = 窗口内全部 Kind=Damage 事件的 ContributorHeroUid 集合：
       移除击杀者；
       移除无效、非 Hero、非敌对或已结束 UnitUid 生命周期的记录；
       按 ContributorHeroUid 去重；
       按 ContributorHeroUid 稳定升序。
4. 写入 FormalDeathResult.KillerHeroUid / AssistantHeroUids。
```

击杀者判定**不是**累计贡献最高者，而是**最后造成有效伤害的英雄**（last hit）。助攻只需要"窗口内其他造成过有效伤害的英雄集合"，因此逐事件日志可以直接支撑，无需常驻聚合记录；死亡时如需贡献比例（奖励分配），由窗口事件按 `ContributorHeroUid` 汇总（O(窗口事件数)）。

`FormalDeathResult`、英雄/防御塔奖励分配与 `MatchStatisticsRuntime` 只使用冻结后的 `AssistantHeroUids`，不再次查询 Tracker。

#### 快照与稳定顺序

`CombatSystemSnapshot` 保存：

```text
CombatContributionEventLogSnapshot[]
    VictimUnitUid（升序）
    LastHitContributorUid
    Events[]（按 LogicTick、SequenceInTick 升序）
```

事件写入按结算顺序追加；Capture 前校验稳定顺序（Victim 升序、事件序升序）。`SharedGameplayChecksum` 按事件逐条参与校验，保证两端事件日志逐位一致。

### 总体定位

CombatSystem 在正式死亡前冻结奖励计算输入，在 UnitWorld 同步完成 `Dead / UnitDeath / 死亡清理` 后，根据已冻结上下文完成奖励分配。

奖励与统计分成三条边界：

```text
经验
    -> CombatSystem 生成并立即应用 ExperienceAward
    -> 属于可回滚 Gameplay 成长状态

金币
    -> CombatSystem 生成临时 GoldIncomeAllocation
    -> 写入 CombatTickResult.GoldIncomeAllocations
    -> 由 CombatGoldIncomeProducer 在 GoldIncomeRuntime.SealTick 前调用 GoldIncomeRuntime.RequestGoldIncome
    -> GoldIncomeRuntime 创建正式 GoldIncomeRecord、分配 IncomeSequenceInTick、确认累计并处理服务端持久化边界

KDA 与整局统计
    -> CombatSystem 输出 FormalDeathResult
    -> MatchStatisticsRuntime 按稳定顺序消费并更新
```

统一原则：

```text
CombatSystem 不创建 GoldIncomeRecord。
CombatSystem 不分配 IncomeSequenceInTick。
CombatSystem 不维护 ConfirmedEarnedGoldTotal 或确认进度。
CombatSystem 不实现账户队列、账户余额或商店金币历史。
CombatSystem 不持有 KDA 计数。
金币和经验接收者只允许是 Hero。
```

濒死复活和死亡阻止都不构成正式死亡，不产生经验、金币或 KDA 结果。正常 `Dead -> Respawning -> Alive` 不会重复生成奖励。

### DeathRewardContext

`DeathRewardContext` 在正式死亡时一次性冻结奖励计算所需事实：

```text
DeathRewardContext
    ResultId
    DeathLogicTick

    VictimUnitUid
    VictimUnitKind
    VictimUnitSubKindId
    VictimTeamId
    DeathPosition

    BaseExperienceValue
    BaseGoldValue

    KillerHeroUid
    AssistantHeroUids
    MinionNearbyEnemyHeroUids
```

字段来源：

| 字段 | 来源 |
|---|---|
| `VictimUnitSubKindId` | 死亡单位 `Unit.UnitSubKindId`，用于在 `Structure` 大类中识别防御塔等稳定子类 |
| `BaseExperienceValue` | 死亡单位 `Unit.BaseExperienceValue` |
| `BaseGoldValue` | 死亡单位 `Unit.BaseGoldValue` |
| `KillerHeroUid` | 最终击杀来源解析得到的奖励归属英雄，可为空 |
| `AssistantHeroUids` | 当前助攻判定记录解析出的英雄集合 |
| `MinionNearbyEnemyHeroUids` | 小兵死亡位置一定范围内、与死亡小兵敌对的英雄集合 |

只有 `UnitKind.Hero` 可以进入奖励接收者集合。召唤物、分身、宠物等来源如果存在明确的英雄归属，应先解析为其所属英雄；无法解析到英雄时不作为奖励接收者。

接收者集合统一：

```text
去除 Invalid UnitUid
去除非 Hero 单位
去除重复 UnitUid
按 UnitUid 稳定升序排列
```


---

### 全局奖励参数

通用分配参数从 `GlobalParamTable` 读取：

| 参数 | 说明 |
|---|---|
| `MinionRewardShareRadius` | 小兵死亡时查找敌方英雄共享者的范围 |
| `MinionKillerShareRatio` | 小兵奖励中英雄击杀者优先取得的比例 |
| `HeroKillerShareRatio` | 英雄或防御塔死亡奖励中，英雄击杀者优先取得的比例 |

同一类死亡的金币和经验使用相同的接收者与分配比例，只是写入时机不同。

配置校验：

```text
0 < MinionKillerShareRatio <= 1
0 < HeroKillerShareRatio <= 1
MinionRewardShareRadius >= 0
```

项目要求“击杀者占大头”时，两个比例应在配置校验中进一步要求大于 `0.5`。

---

### 小兵死亡奖励

#### 接收者

小兵死亡时，奖励共享者是：

```text
以死亡小兵位置为中心
位于 MinionRewardShareRadius 内
与死亡小兵阵营关系为 Enemy
LifeState = Alive
UnitKind = Hero
```

这里必须以**死亡小兵的敌方英雄**为准，不以击杀来源当前所属单位类型简单代替阵营过滤。

有效 `KillerHeroUid` 满足以下条件时，即使其在击杀生效后已经略微离开共享范围，也应强制加入奖励接收者集合：

```text
KillerHeroUid 有效
UnitKind = Hero
与死亡小兵阵营关系为 Enemy
```

其他共享者仍必须位于配置范围内。

#### 分配

存在有效英雄击杀者时：

```text
KillerAmount = floor(BaseReward * MinionKillerShareRatio)
RemainingAmount = BaseReward - KillerAmount
```

剩余部分由范围内其他有效敌方英雄均分。

如果没有其他共享英雄：

```text
击杀英雄获得全部 BaseReward
```

如果小兵由防御塔、其他小兵或无法归属到英雄的来源击杀：

```text
不存在击杀者优先份额
范围内全部有效敌方英雄均分完整 BaseReward
```

如果范围内不存在任何有效敌方英雄，则不发放该项奖励。

`BaseReward` 分别取：

```text
经验分配：BaseExperienceValue
金币分配：BaseGoldValue
```

---

### 英雄死亡奖励

英雄死亡时，奖励接收者为：

```text
KillerHeroUid
AssistantHeroUids
```

接收者必须满足：

```text
UnitKind = Hero
与死亡英雄阵营关系为 Enemy
不是死亡英雄本人
```

存在有效英雄击杀者时：

```text
KillerAmount = floor(BaseReward * HeroKillerShareRatio)
RemainingAmount = BaseReward - KillerAmount
```

剩余部分由有效协助英雄均分。

如果没有有效协助英雄：

```text
击杀英雄获得全部 BaseReward
```

如果最终击杀来源无法归属到英雄，但存在有效协助英雄：

```text
全部有效协助英雄均分完整 BaseReward
```

助攻资格由战斗系统既有的跨 Tick 伤害贡献和助攻判定规则决定，不在死亡位置重新做范围查询。

---

### 野怪死亡奖励

野怪死亡不进行范围共享，也不向助攻者分配基础奖励。

```text
存在有效 KillerHeroUid
    -> 该英雄获得全部 BaseExperienceValue
    -> 生成该玩家的全部 BaseGoldValue 对应 GoldIncomeAllocation

不存在有效 KillerHeroUid
    -> 不发放基础经验
    -> 不发放基础金币
```

普通野怪和史诗野怪均遵循这一基础价值规则。史诗野怪的全队金币、地图目标收益或额外团队经验属于比赛规则或特殊奖励效果，不隐含在通用 `BaseGoldValue / BaseExperienceValue` 分配中。

---

### 防御塔死亡奖励

防御塔通过以下稳定身份识别：

```text
VictimUnitKind = Structure
VictimUnitSubKindId = 全局 UnitSubKindTable 中配置的 Tower
```

不要把全部 `Structure` 都默认视为防御塔。水晶、基地核心、废墟等结构是否提供基础击杀奖励，应由各自单位原型和后续比赛规则明确决定。

#### 接收者

防御塔死亡时，基础奖励接收者与英雄死亡相同：

```text
KillerHeroUid
AssistantHeroUids
```

接收者必须满足：

```text
UnitKind = Hero
与死亡防御塔阵营关系为 Enemy
去除重复 UnitUid
按 UnitUid 稳定升序排列
```

助攻资格沿用英雄死亡时的伤害贡献与助攻判定结果，不在防御塔死亡位置重新进行范围共享查询。

#### 分配

防御塔复用英雄死亡的分配公式和全局参数：

```text
KillerShareRatio = HeroKillerShareRatio
```

存在有效英雄击杀者时：

```text
KillerAmount = floor(BaseReward * HeroKillerShareRatio)
RemainingAmount = BaseReward - KillerAmount
```

剩余部分由有效协助英雄均分。

如果没有有效协助英雄：

```text
击杀英雄获得全部 BaseReward
```

如果最终击杀来源无法归属到英雄，但存在有效协助英雄：

```text
全部有效协助英雄均分完整 BaseReward
```

#### 经验与金币

防御塔单位原型应配置：

```text
BaseExperienceValue = 0
```

因此防御塔死亡不会产生有效经验奖励；`ExperienceSettlement` 对 `BaseExperienceValue <= 0` 的结果直接跳过，不生成零值 `ExperienceAward`。

防御塔金币仍以：

```text
BaseGoldValue
```

为基础，按上述击杀者与协助者规则生成最终 `GoldIncomeAllocation`。CombatSystem 只输出分配结果；外部 `CombatGoldIncomeProducer` 在固定金币生产阶段提交，记录、序号、确认和持久化均由 `GoldIncomeRuntime` 负责。

本版暂不考虑防御塔镀层。镀层属于防御塔尚未死亡时的阶段性结构奖励，不能隐含进防御塔死亡的 `BaseGoldValue`，后续应由独立的结构阶段奖励或比赛规则处理。

---

### 确定性整数分配

金币和经验均以非负整数结算。分配时不得通过各接收者独立四舍五入造成总量增加或减少。

击杀者优先份额：

```text
KillerAmount = floor(BaseReward * KillerShareRatio)
RemainingAmount = BaseReward - KillerAmount
```

多人均分：

```text
AverageAmount = RemainingAmount / RecipientCount
Remainder = RemainingAmount % RecipientCount
```

余数按照接收者 `UnitUid` 稳定升序依次每人追加 `1`，直到余数分配完毕。

必须保证：

```text
全部 ExperienceAward.Amount 或 GoldIncomeAllocation.Amount 之和 == 本次实际参与分配的 BaseReward
```

如果不存在任何有效接收者，则不生成 Award，不要求强行消耗基础价值。

---

### ExperienceSettlement：本地帧立即结算

正式死亡所在 Gameplay LogicTick 立即生成：

```text
ExperienceAward
    HeroUnitUid
    Amount
```

应用顺序：

```text
按 HeroUnitUid 稳定升序
    -> hero.StatHandler.AddExperience(Amount)
```

经验结算规则：

- 客户端预测模拟执行；
- 服务端 Gameplay 模拟执行；
- 回滚时跟随英雄 `StatHandler` 的等级和经验状态恢复；
- 重演死亡 Tick 时重新得到相同接收者和数值；
- 经验增加导致的升级、技能点或成长属性变化由 `StatHandler.AddExperience` 及单位成长接口继续处理；
- `CanLevelUp = false`、达到最大等级等限制由 `StatHandler` 自己判断，奖励管线不重复实现。

固定时序：

```text
冻结 DeathRewardContext
    -> UnitWorld.ConfirmUnitDeath
    -> UnitDeath 与死亡清理完成
    -> CombatSystem 构建 FormalDeathResult
    -> 发布 UnitKill
    -> ExperienceSettlement
```

这样 `UnitDeath / UnitKill` Gameplay 回调读取的是奖励应用前状态，经验和升级随后在同一 LogicTick 内生效并影响后续模拟。

### GoldIncomeAllocation 与统一金币请求

死亡所在 LogicTick 已经拥有完整的死亡位置、助攻、范围共享者和英雄到玩家归属，因此 CombatSystem 在该 Tick 直接计算：

```text
GoldIncomeAllocation
    DeathSequenceInTick
    ReceiverPlayerSlot
    Amount
    Reason
```

它是 CombatSystem 的 Tick 输出，不是正式金币记录：

```text
不包含 IncomeSequenceInTick。
不进入 CombatSystemSnapshot。
不保存为跨 Tick 历史。
不表示收入已经被 AuthorityFrame 确认。
```

稳定顺序：

```text
先按 DeathSequenceInTick 升序，
再按同一死亡内 ReceiverPlayerSlot 升序。
```

CombatSystem 只把分配结果写入：

```text
CombatTickResult.GoldIncomeAllocations[]
```

外部固定阶段的 `CombatGoldIncomeProducer` 执行：

```csharp
for each allocation in
    CombatTickResult.GoldIncomeAllocations:

    goldIncomeRuntime.RequestGoldIncome(
        allocation.ReceiverPlayerSlot,
        allocation.Amount,
        allocation.Reason);
```

调用前提：

```text
GoldIncomeRuntime 已 BeginTick。
GoldIncomeRuntime 仍处于 AcceptingRequests。
GoldIncomeRuntime 尚未 SealTick。
```

正式 `GoldIncomeRecord` 与 `IncomeSequenceInTick` 由 `GoldIncomeRuntime` 按全局 Pipeline 的实际稳定请求顺序创建和分配。CombatSystem 本体不持有 `IGoldIncomeRequester`，不自行合并同类记录，也不传入 LogicTick、序号、BatchId 或确认状态。

### FormalDeathResult

正式死亡时生成：

```text
FormalDeathResult
    ResultId
    LogicTick
    DeathSequenceInTick

    VictimUnitUid
    VictimUnitKind
    VictimUnitSubKindId
    VictimTeamId

    KillerUnitUid
    KillerHeroUid
    AssistantHeroUids
    FinalSourceDescriptor
    DeathReason

    DeathRewardContext
    ExperienceAwards[]
```

名称中的 `Formal` 只表示：

```text
当前确定性模拟已经完成正式逻辑死亡判定。
```

它不表示：

```text
该预测 Tick 已被服务端权威确认。
AuthorityFrame 已经构建。
金币批次已经确认或持久化。
比赛结果已经最终提交。
```

事件与输出关系：

```text
CombatSystem 冻结 DeathRewardContext
    -> UnitWorld 写入 Victim.Dead
    -> Victim.EventBus.Publish(UnitDeath)
    -> UnitWorld 完成死亡阶段清理并返回
    -> CombatSystem 构建 FormalDeathResult
    -> Killer.EventBus.Publish(UnitKill)
    -> 应用 ExperienceAward
    -> 生成 GoldIncomeAllocation
    -> 写入 CombatTickResult
```

`FormalDeathResult` 是可预测、可回滚重演的 Tick 结果，不等于外部持久化比赛记录，也不进入 GameplaySnapshot 历史。

---

### MatchStatisticsRuntime 与外部边界

KDA 和整局统计不由 CombatSystem 保存。

固定接缝：

```text
CombatSystem
    -> CombatTickResult.FormalDeathResults[]

MatchStatisticsRuntime
    -> 所有模拟端执行
    -> 按 DeathSequenceInTick 稳定消费
    -> Victim 对应 Death +1
    -> KillerHero 对应 Kill +1
    -> AssistantHeroUids 对应 Assist +1
    -> 更新其它确定性比赛统计
```

`MatchStatisticsRuntime`：

```text
属于 MatchRuleRuntime 的确定性 Gameplay 子状态。
客户端预测、服务端模拟和权威重演使用同一算法。
进入 MatchStatisticsRuntimeSnapshot。
不依赖账户或网络确认后再重新计算 KDA。
```

全局相对顺序冻结为：

```text
GoldIncomeRuntime.BeginTick(T)
    -> NaturalGoldIncomeSystem 按 PlayerSlot 升序请求
    -> CombatSystem.SettleTick
    -> MatchStatisticsRuntime.Consume(FormalDeathResults)
    -> CombatGoldIncomeProducer 按 GoldIncomeAllocations 请求
    -> Map / MatchRule Gold Producers 按代码固定顺序请求
    -> GoldIncomeRuntime.SealTick(T)
```

服务端专用 `MatchRuleRuntime` 另行消费 `TeamBaseDestroyedSignals`；它不能替代所有端执行的 `MatchStatisticsRuntime`。

外部边界：

```text
GoldIncomeRuntime
    负责 GoldIncomeRecord、IncomeSequenceInTick、未确认批次、摘要、确认累计与服务端持久化端口。

FrameSync Runtime
    负责 AuthorityFrame 对账、重演、Checksum 验证与连续确认。

EquipmentShopRuntime
    只读取 IConfirmedGoldIncomeView，并结合 OperationLog 派生 CurrentAvailableGold。

Server Settlement / Result
    可以读取已确认金币批次与 MatchStatisticsRuntime 的最终状态进行持久化，
    但不能反向改写 CombatSystem 的历史死亡结果。
```

## 附录：已接受的金币数值与整数分配

正式初始金币 1500；HeroTest 测试金币 10000。近战兵击杀奖励 21，远程兵 14，英雄基础击杀奖励 300。

英雄奖励先给击杀者 floor(300×3/5)=180，余下 120 由有效助攻者分配，整数余数按稳定身份顺序处理；没有有效助攻者时击杀者得到 300。小兵金币只给击杀者，经验分配另遵经验需求。

分配生产入口的旧合同与源码不一致，见待确认清单；不能因为本附录有明确数值就擅改该 owner。


## 需求演进

### 2026-08-06

变动内容：助攻事件和复仇反应接入正式贡献链。

legacyDecision：D-034

### 2026-08-06

变动内容：保留贡献事件和助攻窗口；末次伤害击杀归属已被最高有效伤害修订替代。

legacyDecision：D-035

### 2026-08-11

变动内容：商店 Trader 懒创建，小兵奖励距离按正式数值边界转换。

legacyDecision：D-040

### 2026-08-11

变动内容：击杀金币整数分配和测试金币；生产者边界冲突留在待确认清单。

legacyDecision：D-041

### 2026-08-24

变动内容：战斗封存波次和冻结批次起始状态；击杀按最高有效生命伤害，取代末次伤害者。

legacyDecision：D-049


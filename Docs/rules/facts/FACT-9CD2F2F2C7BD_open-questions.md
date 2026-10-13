# 待确认清单

## 本轮已经确认的内容

用户明确反馈原不能确认的功能已经手动测试正常。本轮按独立执行批次重新核对具体源码、类型、数据入口和已有测试断言，移除因旧计划缺少 Complete 字段产生的重复确认项。人工确认与源码证据已分别保存在各计划 assessment/evidence，汇总见 [各历史计划的独立源码与人工确认](../../plans)。没有宣称本轮重新运行了全工程测试。

## 实际仍需保留的事项

1. **三狼重做剩余范围**：PLAN-WOLF-165 继续执行，中断未完成；旧实现曾测试但有问题、丢失后重做已由用户确认。核对当前资源接线、动态占用、回营耐心、过渡动画、快照/回滚及聚焦测试；真实审查条目保持待审批。
2. **金币分配生产合同**：装备/金币旧6.5–6.6让MatchStatisticsRuntime生成分配，战斗旧1.5/11.10要求CombatSystem输出GoldIncomeAllocation(PlayerSlot+Reason)，当前按ReceiverHeroUid分配后由管线解析槽位。金币累计唯一owner GoldIncomeRuntime已明确；生产入口的公开合同冲突需决定，手动测试不能消除合同冲突。
3. **EquipmentTargetPolicy**：当前CheckUse按槽位、冷却/充能及模块CanExecute校验；没有独立EquipmentTargetPolicy值域。旧需求仍提及该概念，需要明确是弃用旧概念，还是新增何种目标限制；本轮不擅自发明枚举或修改玩法。

## 已确认待修复的问题

### 已损生命值附伤读取错误

- **登记日期与安排**：2026-10-07。用户要求先记录，之后再修；当前未执行修复，不并入三狼重做。此项是已有源码缺陷，不需要再次让用户确认是否存在。
- **位置与证据**：`Assets/Scripts/Gameplay/Combat/DamageRecipe.cs:62-66`，`ToContext` 内maxHp和currentHp都通过 `GetStat(StatId.MaxHealth)` 获取，随后计算 `missingHp = maxHp - currentHp`。
- **影响**：该分支两次读取同一最大生命值，差额为零；设置了 `TargetMissingHealthRatio` 的配方无法在这里得到预期的已损生命值附伤。不据此推断全部战斗结算都有问题，也没有核对当前哪些正式技能使用了这个配置。
- **现有修复线索**：`Assets/Scripts/Gameplay/Stats/StatHandler.cs:103` 已提供 `CurrentHealth` 属性。后续修复应核对并使用现有当前生命值入口，不新增重复属性或臆造 `StatId.CurrentHealth`。
- **后续测试设计，尚未执行**：隔离其它加成，最大生命值1000、当前生命值400、已损生命值系数10%，预期基础伤害额外增加60；再覆盖满血、当前生命值为零、系数为零及targetStats为空。使用工程既有fp和战斗测试入口；修改后由Unity实际编译、检查Console并运行对应测试。
- **取证边界**：已静态核对源码及现有属性；未运行上述测试、未验证实际技能接线、未修改Unity源码。未来由独立战斗修复任务处理，不重启已关闭的历史战斗计划。
- **来源**：[PLAN-KAR-018自动对照中发现的问题](../../plans/karolina/PLAN-KAR-018_project-knowledge-retrieval.md#单独发现的unity源码问题)；关联 [REQ-FEAT-030伤害与抗性](../../requirements/combat/REQ-FEAT-030_damage-resistance.md) 的DamageRecipe配方职责。

## 后续处理

待确认事项与已确认待修复的问题分开处理，不再让用户逐条确认已经正常工作的历史执行范围。已关闭计划保持结束，新工作建立新的需求/计划关系。用户对功能的人工确认不自动替代尚未提供的构建回执、专项测试或公开合同选择。

## 本轮计划状态确认

PLAN-0094 与 PLAN-CONTENT-138 依用户确认关闭，原批次不再列为待确认；没有声称本轮运行过相应测试或构建。三狼不是已关闭计划重启，而是未完成重做继续原计划。

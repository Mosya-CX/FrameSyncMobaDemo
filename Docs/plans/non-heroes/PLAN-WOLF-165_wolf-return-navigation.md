# 三狼回营与动态导航改造

## 参考需求


- [同Tick动态占用与回营路线](../../requirements/pathfinding-movement/REQ-WOLF-NAV_wolf-return-navigation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [营地共享耐心与越界恢复](../../requirements/non-heroes/REQ-WOLF-PATIENCE_camp-patience.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [三狼警戒进入退出过渡动画](../../requirements/presentation-ui/REQ-WOLF-ANIM_wolf-alert-animation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 当前进度

当前阶段：执行。执行情况：中断待续，重做未完成。

用户确认旧三狼营地实现曾完成并测试，但存在较大问题；用户丢失相关改动后完全重做。旧实现的测试结论不能转用于现在的重做内容。迁移来源中的 Deferred 只保留为历史来源，当前以用户确认纠正状态。

- [x] 已核对原需求、此前三狼计划、程序集与 Unity 基线（当期记录）。
- [x] 已确认两模型含四个过渡 clip（当期记录）。
- [x] 已确认旧 leash 为任一成员越界立即回营、旧 A* 只查静态通行（当期记录）。
- [ ] 将当前已批准语义与现工作树逐项对照，登记 scoped 需求演进；本期已迁中文案，当前按用户确认登记为重做中断，继续核对实现缺口。
- [ ] 当前 Tick 不可变动态占用与 query-specific A* 代价完成度核对/补齐。
- [ ] 路线失效、稳定通行优先和回营目的地预留核对/补齐。
- [ ] 共享耐心、作者/Gizmo、Snapshot/checksum 字段核对/补齐。
- [ ] 提取四个非循环 clip、生成两个 Animator 有限过渡状态。
- [ ] 更新聚焦 EditMode 和 PlayMode 测试。
- [ ] Unity 编译、Console 和实际聚焦行为验收。
- [ ] 独立只读高风险审查并关闭问题。
- [ ] 更新本计划结果与工程当前状态。

## 实施细节

1. 在 locomotion 评估前从稳定移动前单位列表构建一次 DynamicNavigationFrame，按 radius class 保存占用。
2. A* 统一 query context：立即 footprint 硬障碍，其它占用高成本，自身/已预留终点豁免；扩展、对角、blocked fallback 与 LOS 使用同一规则。
3. UnitLocomotionAgent 查 forward corridor，新占用/目标移动/旧通道失效才重搜；影响路线的 generation/cursor 保存快照。
4. JungleCamp/Monster 已有 CampId/slot 作为回营预留 owner；冲突键为 purpose、CampId、slot、UnitUid；RVO 保留速度层职责。
5. Greater 活着以主怪 leash，死后最远存活成员；6.5 之外快速耗尽，较小圈恢复，更远圈绝对 reset；未来影响字段进入 JungleCampSnapshot/checksum。
6. MurkWolfCampContentSetup 提取 N2A/A2N 并生成有限 Animator 状态；表现 latch 仅客户端。幂等生成保留地图已有营地和 Transform。
7. 对比 GameplaySnapshot 实际 schema 25 与原计划字段设计，不能猜测缺字段或静默恢复。

## Agent 测试与验收设计

| 用例函数设计 | 环境/输入 | 预期 |
|---|---|---|
| DynamicOccupancy_ChangesRoute | EditMode，移动障碍进入路径前方 | 路径改变且不穿硬占用 |
| Occupancy_SelfGoalRadiusAndOrder | EditMode，自体/预留终点、多半径与倒序插入 | 豁免正确，同 Tick frame/结果一致 |
| ForwardCorridor_UnchangedDoesNotRebuild | EditMode，远处障碍变化 | 当前路径不无条件重搜 |
| CampReturn_ThreeMembersReachDistinctSlots | EditMode，三成员同时回营 | 到达独立预留点，重复/恢复重演等价 |
| CampPatience_MainDeadAndHysteresis | EditMode，主怪活/死、650外、恢复圈内、极远 | 正确 owner、耐心流失/恢复/绝对重置 |
| Patience_SnapshotChecksumRoundTrip | EditMode，耐心变化后 Capture/Restore | 状态恢复精确且 checksum 可检测变化 |
| WolfTransitions_ClipsAndStateChains | Editor 资源测试，两模型四 clip | 非循环且两个 controller 有 N2A/A2N |
| WolfTransitions_PlayToIdle | PlayMode，靠近/远离/移动/攻击/死亡 | 有限过渡完成，正确 Idle/动作路由 |

这些名称是待匹配/补充的测试设计，不宣称工程已存在或已通过。继续前优先定位已有 MurkWolf/NonHero/Pathfinding 测试，避免重复用例。场景为实际地图 Camp 组合及中立夹具，路径需由 Unity 查询确认。

## 恢复与限制

不构建 Player/Server、不替换流场、不引入其它寻路算法。资源失败保留源码并记录 Unity 失败，不手改 YAML。原用户工作树变化不重置。

## 本期核查结论

纠正迁移时的准备/暂缓解释；当前 PLAN-WOLF-165 继续执行，未关闭，不要求为这次未结束重做另建计划。当前代码和资源存在不等于重做已验收。

## 当前任务审查范围

本轮按用户授权整理 100 个三狼候选变更：69 个新增、31 个修改，其中 40 个 .meta。模型、动画、逻辑/表现 Prefab 与专用脚本直接关联三狼；共享地图、目录、动态导航、快照、校验及测试文件显示完整参考差异，其中其它改动仍需人工判定。

[逐文件收录依据与内容指纹](PLAN-WOLF-165_review-selection.json)保存本地 HEAD 对照来源。审查条目为“事后整理（历史参考）”：没有实际任务开始快照，不能还原已丢失实现或证明全部作者归属。未选既有 Unity 文件保持整理时内容为基线，不整批纳入三狼审批。

UnityMCP 本轮可读取 33 个相关资源，并读取两种逻辑 Prefab 与过渡动画；Unity 未运行、未编译。所返回 Console 包含此前 MCP 连接失败的 Error/Exception/Warning，不能宣称零错误。本轮没有重跑三狼玩法测试或资源构建。Karolina 的审批机制测试使用临时副本，真实三狼条目全部保持待审批，审批也不表示计划完成。

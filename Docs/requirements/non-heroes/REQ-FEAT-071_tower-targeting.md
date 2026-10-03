# 防御塔目标优先级与攻击红线

## 目标实现

塔按明确优先级固定目标，攻击周期和红线可恢复。

## 技术方案

TowerAIController 过滤范围和合法目标；TowerAttackHandler 管理周期与 Commit，TowerTargetLinePresenter 读取当前锁定状态。

## 边界情况

塔不追击；在途炮弹不因重新索敌改目标；英雄正在攻击己方英雄的判定来源固定；结构效果准入在中央入口。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/FrameSync/TowerTargetLinePresenter.cs`：当前关联实现定义 TowerTargetLinePresenter（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Attack/TowerAttackHandler.cs`：当前关联实现定义 TowerAttackHandler（以源码为实际命名）。
- `Assets/Scripts/Gameplay/NonHero/UnitAIController.cs`：当前关联实现定义 UnitAIController、MinionAIController、ThreatEntry、MonsterAIController、TowerAIController（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/PlayMode/TowerTargetLinePresenterPlayModeTests.cs`：DisplayTarget_SwitchesWithIntent_AndStopsForDeadTarget。
- `Assets/Scripts/Gameplay/Tests/TowerAttackHandlerTests.cs`：HeroRamp_FirstHitIsBase_ThenMultipliesByOnePointFive、HeroRamp_CapsAtSixHundred、Ramp_WithZeroHits_ReturnsBaseEvenForSmallBase。
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：MinionWave_ExpandsCanonicalTeamLaneMemberOrder、MinionUnregister_RemovesUidWithoutLeavingTombstone、LaneNearestPoint_ProjectsOntoCenterlineSegment、MinionAI_BetweenDistantCenterlineNodes_RemainsInLaneAdvance、MinionAI_FarFromLane_ReturnStateStillUsesLaneFlowField、AIController_DoesNotTickOnSpawnTick、MinionAI_UsesLaneAdvanceOrderThroughPlanner。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位与状态

TowerAIController 位于塔 Unit 之上，只负责选择目标并下达不允许追击的 AttackOrder。

~~~text
TowerAIController
    UnitUid OwnerUnitUid                          【基类稳定身份】
    int NextDecisionLogicTick                     【需要快照】
    TowerAIProfile Profile                        【静态配置】
    Unit Owner                                    【可重建】
    UnitQueryBuffer CandidateBuffer               【可重用，可重建】
~~~

~~~text
TowerAIProfile
    int DecisionIntervalTicks                     【静态配置】
~~~

防御塔攻击距离、攻击速度、前后摇和投掷物定义来自 Unit 数值与 TowerAttackHandler，不在 TowerAIProfile 中复制。

TowerAIController 与所有其它控制器一起注册到 UnitWorld.AIControllers，不建立 TowerList。它不处理：

- 攻击计时和攻击序列。
- 投掷物生成、移动、命中与回收。
- 红线 LineRenderer。
- 防御塔死亡、处置和废墟生成。

防御塔正式进入 Dead 后，UnitWorld 直接注销 TowerAIController，并根据 UnitDisposePolicy 处理死亡表现、销毁和塔废墟。当前模块没有需要同步更新的 TowerSystem、TowerManager 或防御塔 UID 列表；未来若存在独立地图目标系统，只由该系统维护自己的建筑业务关系。

### 固定索敌优先级

防御塔索敌顺序固定为：

| 优先级 | 目标 |
|---:|---|
| 0 | 正在攻击己方英雄的敌方英雄 |
| 1 | 敌方召唤物 |
| 2 | 敌方炮车兵或超级兵 |
| 3 | 敌方近战兵 |
| 4 | 敌方远程兵 |
| 5 | 最近的敌方英雄 |

同一优先级使用：

~~~text
DistanceSq 升序
UnitUid 升序
~~~

完整比较键为：

~~~text
(PriorityBand, DistanceSq, UnitUid)
~~~

召唤物、炮车兵、超级兵、近战兵和远程兵通过项目稳定的 UnitKind 与 UnitSubKindId 映射，不使用 Unity Tag、对象名称或表现 Prefab 判断。

### 判断敌方英雄是否正在攻击己方英雄

最高优先级是当前状态查询，不依赖 AttackStarted、AttackCommitted 或 AttackHit 事件。

对攻击范围内的敌方英雄候选，读取：

~~~text
candidate.ActionStateView.MainKind
candidate.ActionStateView.FocusTarget
~~~

满足以下条件时归入优先级 0：

1. MainKind == ActionKind.Attack。
2. FocusTarget 可以解析为有效 Unit。
3. FocusTarget 是防御塔一方的英雄。
4. 候选英雄仍是合法塔目标。

UnitActionStateView 只负责暴露当前行为，不允许 TowerAIController 反向修改。AI 仍通过 AttackOrder 改变自己所属塔的行为。

不保存 PendingProtectionTargetUid，也不增加保护仇恨持续时间。需求表达的是正在攻击，而不是曾经攻击；英雄停止攻击己方英雄后，下次完整索敌时自然回到其普通优先级。

### 合法目标过滤

进入优先级比较前统一检查：

- UnitWorld 可以解析目标。
- LifeState 允许目标参加战斗。
- CapabilityState.IsTargetable 为 true。
- 双方阵营敌对。
- 目标位于 TowerAttackHandler 当前攻击距离内。
- 地图规则没有禁止该目标被塔攻击。
- 目标属于六个允许优先级类别之一。

攻击距离以 AttackHandler 和数值系统为权威。若需要避免边界处反复进入和离开，可以配置很小的退出滞后距离，但不能在 TowerAIProfile 再复制完整攻击距离。

### 与在途炮弹的目标锁定

TowerAIController 读取 TowerAttackHandler.HasUnresolvedProjectile。

为 true 时：

- 不重新选择目标。
- 不清除当前锁定目标。
- 不下达新的 AttackOrder。
- 不执行完整候选扫描。

上一发炮弹进入终止状态后，下一次决策重新按完整六级优先级选择目标。这样 TowerAIController、TowerAttackHandler、红线和塔弹始终围绕同一个锁定目标，不会各自指向不同单位。

### 核心索敌算法

~~~text
TickLogic:
    currentLogicTick = SimulationTickContext.Current.Tick

    如果塔不存在、不能参与模拟或不能攻击:
        清除不再正确的 AttackOrder
        返回

    如果 TowerAttackHandler.HasUnresolvedProjectile:
        返回

    如果尚未到 NextDecisionLogicTick 且当前 Order 仍正确:
        返回

    查询攻击范围内的合法候选
    对每个候选计算 PriorityBand
    选择 (PriorityBand, DistanceSq, UnitUid) 最小者

    找到目标:
        必要时下达不允许追击的 AttackOrder
    没有目标:
        清除攻击 Order

    安排下一次决策
~~~

### 不允许追击

塔的 AttackOrder 必须表达 allowChase = false。目标离开攻击范围后，Planner 只能等待或清除攻击意图，不能创建 MoveActionRequest。

塔 Unit 不需要为了复用行为链装配空 MovementHandler。它仍可拥有 BehaviorPlanner、ActionArbiter 与 TowerAttackHandler，但 CapabilityState 不提供普通移动能力。

### 多态快照与性能

TowerState 只保存真正由控制器维护的 NextDecisionLogicTick。Owner 和 TowerAttackHandler 引用在 Resolve 中恢复。当前锁定目标和攻击阶段属于 TowerAttackHandler，不复制到 AI State。

- 炮弹未结束时跳过候选查询。
- CandidateBuffer 复用，不使用 LINQ 排序。
- 单次遍历直接维护当前最佳比较键。
- 使用 fp 平方距离。
- 同优先级最终由 UnitUid 打破平局。

---

### 定位和边界

防御塔具有一条特殊攻击规则：

~~~text
上一发塔弹命中锁定目标，或者因目标失效、超时等规则正式结束以前，
不能开始下一次攻击。
~~~

TowerAttackHandler 是 AttackHandler 的防御塔具体实现：

~~~csharp
public sealed class TowerAttackHandler
    : AttackHandler
~~~

它实现当前攻击模块已经公开的接缝：

~~~csharp
public override AttackPlanStatus GetAttackPlanStatus(
    UnitUid targetUid);

public override void BeginAttack(
    UnitUid targetUid);

public override bool CommitAttack();

public override void CancelBeforeCommit();

public override void ResetAttackTimer(
    AttackTimerResetReason reason);
~~~

这些函数不接收 logicTick 或 SimulationTickContext。需要当前时间时，在函数内部读取 SimulationTickContext.Current.Tick。

本设计不要求公共攻击模块：

- 新增防御塔专用字段。
- 新增防御塔专用 AttackPlanStatus。
- 修改 AttackSequenceIndex 规则。
- 增加 AttackStarted、AttackCommitted 或 AttackHit 事件。
- 改变普通攻击 Handler 的投掷物实现。

TowerAttackHandler 只是现有抽象攻击能力的一种具体实现。

### 权威运行状态

TowerAttackHandler 在当前攻击模块规定的普通攻击运行状态之外，只增加：

~~~text
ProjectileUid LastCommittedProjectileUid         【需要快照】
~~~

普通攻击目标、攻击阶段、计时器、是否已 Commit 和 AttackSequenceIndex 继续由攻击模块当前设计负责。本文不重新定义，也不在 TowerAIController 中保存副本。

TowerAttackHandler 不保存：

- Projectile 对象引用。
- 投掷物位置和运动状态副本。
- 投掷物命中结果副本。
- ProjectileEndReason 历史。
- 动态投掷物结束委托。

上一发炮弹是否未结束通过 ProjectileWorld 查询：

~~~text
projectileState = ProjectileWorld.GetState(
    LastCommittedProjectileUid)

HasUnresolvedProjectile =
    projectileState == Pending
    或 projectileState == Active
~~~

当前投掷物系统把从未存在和已经结束都表示为 Missing。LastCommittedProjectileUid 只在 RequestSpawn 成功并返回有效 UID 后写入，因此该字段查询到 Missing 时，可以确定上一发已不再处于待生成或飞行状态。

### 攻击规划和开始

GetAttackPlanStatus 首先执行塔的普通攻击合法性判断：

- 目标存在且可选中。
- 目标仍在当前攻击范围内。
- 塔当前具有攻击能力。
- 当前攻击计时允许开始。

上述条件满足但 HasUnresolvedProjectile 为 true 时，返回攻击模块已有的等待就绪状态，不新增塔专用枚举。

BeginAttack 再次检查在途炮弹门控，防止调用者绕过 Planner。门控关闭时不能覆盖旧锁定目标或启动新的攻击前摇。

CancelBeforeCommit 只取消尚未正式 Commit 的本次攻击，并按当前攻击模块规则清理目标和阶段。已经生成的上一发塔弹不因新一次前摇取消而被修改。

ResetAttackTimer 只遵循攻击模块已有的计时重置语义。即使普通攻击计时被重置，只要上一发投掷物仍是 Pending 或 Active，塔仍不能开始下一次攻击。

### 下一次攻击门控

下一次塔攻击同时受到普通攻击计时和上一发炮弹状态约束：

~~~text
CanBeginNextTowerAttack:
    currentLogicTick = SimulationTickContext.Current.Tick

    如果当前攻击模块判断普通攻击尚未 Ready:
        返回 false

    查询 LastCommittedProjectileUid
    如果投掷物为 Pending 或 Active:
        返回 false

    返回 true
~~~

| 情况 | 是否可开始下一次攻击 |
|---|---|
| 炮弹已结束，但普通攻击尚未 Ready | 否 |
| 普通攻击已 Ready，但炮弹仍 Pending | 否 |
| 普通攻击已 Ready，但炮弹仍 Active | 否 |
| 炮弹 Missing，普通攻击也 Ready | 是 |
| 普攻计时被重置，但炮弹仍 Active | 否 |

目标死亡、投掷物超时或规则取消会让投掷物正式结束并变为 Missing，此时解除门控，避免防御塔永久停火。

### CommitAttack 的塔实现

TowerAttackHandler 在自己的 CommitAttack 中完成塔弹生成。它不修改 AttackHandler 基类，也不要求公共攻击模块额外返回 ProjectileUid。

核心流程：

~~~text
CommitAttack:
    currentLogicTick = SimulationTickContext.Current.Tick

    按当前攻击模块规则检查本次攻击能否 Commit
    重新解析并验证锁定目标
    如果目标无效或超出攻击范围:
        按 Commit 前失败规则取消
        返回 false

    按既有规则立即逻辑转向目标
    构造锁定该目标的 ProjectileSpawnRequest
    projectileUid = ProjectileWorld.RequestSpawn(request)

    如果 projectileUid 无效:
        返回 false

    LastCommittedProjectileUid = projectileUid
    完成当前攻击模块规定的成功 Commit 状态更新
    返回 true
~~~

RequestSpawn 同步返回预分配的 ProjectileUid，但投掷物可能仍处于 Pending，所以门控必须同时检查 Pending 与 Active。

塔弹推荐规则：

~~~text
TargetUnitUid = 当前锁定目标
运动方式 = 确定性跟踪目标
目标过滤 = 只能命中锁定 TargetUnitUid
SameTarget = Once
EndOnFirstValidHit = true
~~~

路径上的其它单位不能替锁定目标承受塔弹。

### 红线逻辑状态

红线表达防御塔当前 `AttackTarget` 意图，而不是塔弹轨迹，也不是上一发塔弹的历史锁定目标。红线只由客户端表现层读取 Gameplay 的只读当前意图，不向 Gameplay 回写，也不进入快照或校验和。

表现结果：

- 当前 `AttackTarget` 合法且存活时显示红线，与攻击前摇、后摇和塔弹飞行进度无关。
- AI 把 `AttackTarget` 替换为下一目标时，红线在同一表现帧直接切换到新目标。
- 当前目标死亡、不可选中、意图被清除且没有后续目标时，红线立即停止渲染。
- 不允许回退读取 `TowerAttackHandler.LockedTargetUid`；该字段可能描述已经结束的历史塔弹。

### TowerTargetLinePresenter

红线由塔预制体上的表现组件负责：

~~~csharp
public sealed class TowerTargetLinePresenter
    : MonoBehaviour
~~~

它读取：

- 防御塔发射端表现挂点。
- 塔 Unit 当前只读 `AttackTarget` 意图。
- 当前意图对应且仍存活、可选中的敌方单位表现挂点。

Presenter 负责启用、更新和关闭红色 LineRenderer。`UNITY_SERVER` 构建不创建 LineRenderer；客户端 Presenter 不拥有 Gameplay 目标，不向 AI 或 AttackHandler 回写状态，也不进入快照。

线段端点可以在渲染帧平滑跟随模型；目标 UID 的替换直接衔接新端点。是否应该显示红线仍由只读当前意图和目标合法性决定，不能由 Animator Event、本地特效状态或历史塔弹锁定决定。

### 恢复与性能关注点

- 每座塔只额外保存一个 LastCommittedProjectileUid。
- ProjectileWorld.GetState 必须是 UID 索引查询，不能扫描全部活跃投掷物。
- HasUnresolvedProjectile 是派生值，不额外保存 bool。
- LastCommittedProjectileUid 必须与恢复后的 Pending 或 Active 投掷物状态对应，具体恢复顺序由帧同步设计负责。
- TowerTargetLinePresenter 恢复后重新读取逻辑状态，不保存 LineRenderer 进度。

---


## 需求演进

### 2026-08-07

变动内容：小兵初始 Buff、塔攻击与兵线/单位配置拆分。

legacyDecision：D-037

### 2026-09-01

变动内容：建筑中央准入仅允许规定外源普通攻击，自身效果允许，合法拒绝是成功空操作。

legacyDecision：D-054


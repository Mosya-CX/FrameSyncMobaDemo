# 小兵波次与兵线 AI

## 目标实现

稳定生成票据组成波次，小兵推进、协防、追击与回线。

## 技术方案

MinionSystem 固定波次序列和出生配置；MinionAIController 复用 UnitOrder/Planner/Attack；兵线参数与单位参数两类配置。

## 边界情况

同 Tick 生效门、目标失效、追击距离与英雄协防条件明确；初始 Buff 和 Participant 来源由票据固定。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/NonHero/LaneAuthoring.cs`：当前关联实现定义 LaneTeamSpawnAuthoring、LaneTeamSpawnData、LaneRuntimeData、LaneAuthoring（以源码为实际命名）。
- `Assets/Scripts/Gameplay/NonHero/MinionSystem.cs`：当前关联实现定义 MinionSystem（以源码为实际命名）。
- `Assets/Scripts/Gameplay/NonHero/UnitAIController.cs`：当前关联实现定义 UnitAIController、MinionAIController、ThreatEntry、MonsterAIController、TowerAIController（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientBootstrapFirstWavePlayModeTests.cs`：GameScene_FirstWaveUsesFlowFieldsAndMoves、GameScene_MapViewAnchorsToStaticTopologyRootAtWorldOrigin。
- `Assets/Scripts/Gameplay/Tests/MinionThreatSystemTests.cs`：Acquisition_SetsInitialThreat_AndPicksClosestUnclaimed、DamageTaken_AddsThreat_InverselyProportionalToDistance、HigherThreatTarget_SwitchesOnlyWhenNotInWindup、Acquisition_PairsAlliesWithDistinctTargets、ThreatTable_SnapshotRoundTrip_PreservesEntries、SnapshotRoundTrip_PreservesLastThreatRefreshTick、RestoreReplacement_UnsubscribesPreviousController。
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：MinionWave_ExpandsCanonicalTeamLaneMemberOrder、MinionUnregister_RemovesUidWithoutLeavingTombstone、LaneNearestPoint_ProjectsOntoCenterlineSegment、MinionAI_BetweenDistantCenterlineNodes_RemainsInLaneAdvance、MinionAI_FarFromLane_ReturnStateStillUsesLaneFlowField、AIController_DoesNotTickOnSpawnTick、MinionAI_UsesLaneAdvanceOrderThroughPlanner。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位与成员

MinionSystem 是 UnitWorld 直接持有的纯 C# 业务对象。它负责世界级波次日程、波次组成展开和稳定同步生成，但不拥有生成后小兵的移动、攻击或长期群体状态。

~~~text
MinionSystem
    MinionWaveSchedule Schedule                     【静态配置】
    LaneRuntimeData[] Lanes                         【静态配置】

    int WaveIndex                                   【需要快照】
    int NextWaveLogicTick                           【需要快照】
    List<MinionSpawnTicket> PendingTickets          【需要快照或确定性重建】
    int NextTicketCursor                            【需要快照】
    List<UnitUid> ManagedMinionUids                 【需要快照】
~~~

MinionSystem 不维护：

- UnitUid → MinionAIController 映射。
- 每一波的长期 Wave 实例。
- 波次队长。
- 整波共享目标。
- 阵型状态机。

波次生成结束后，每个小兵由自己的 MinionAIController 决策；单位之间的拥堵、避让和路线选择交给移动系统。

ManagedMinionUids 只保存 MinionSystem 当前管理的小兵身份，用于本模块自己的单位管理和生命周期清理。Controller 解析统一调用 UnitWorld.TryGetAIController。小兵正式进入 Dead 后，UnitWorld 直接通知 MinionSystem 注销对应 UnitUid。

本模块交给帧同步设计的完整状态结构为：

~~~text
MinionSystemSnapshot
    int WaveIndex
    int NextWaveLogicTick
    MinionSpawnTicket[] PendingTickets
    int NextTicketCursor
    UnitUid[] ManagedMinionUids
~~~

Schedule、Lanes 和波次组成是静态配置；Controller 状态由 UnitWorld 聚合，不复制到 MinionSystemSnapshot。

### 波次配置

波次日程适合使用 ScriptableObject 编辑，并在对局初始化时解析为只读确定性数据：

~~~text
MinionWaveSchedule                                【静态配置】
    int FirstWaveLogicTick
    int WaveIntervalTicks
    MinionWavePhase[] Phases

MinionWavePhase                                   【静态配置】
    int StartWaveIndex
    MinionWaveComposition[] CompositionCycle

MinionWaveComposition                             【静态配置】
    MinionWaveMember[] Members

MinionWaveMember                                  【静态配置】
    int UnitPrototypeId
    int Count
    int FirstSpawnOffsetTicks
    int SpawnStepTicks
    int FormationGroup
~~~

Inspector 可以使用秒配置首次出兵、波次间隔和成员间隔；初始化时统一换算为 LogicTick/Ticks，运行时不读取 Time.time 或浮点秒累计。

Phase 与 CompositionCycle 用于表达：

- 不同时间阶段的炮车波频率。
- 中后期波次组成变化。
- 特殊模式替换波次。
- 比赛规则向指定队伍和兵线追加超级兵。

MinionSystem 只读取比赛规则已经给出的确定性波次修饰结果，不自行扫描兵营、防御塔或表现对象推导超级兵条件。

### 兵线场景数据

兵线是地图结构，使用 LaneAuthoring 直接编辑：

~~~text
LaneAuthoring : MonoBehaviour
    ushort LaneId
    TeamSpawnAuthoring[] TeamSpawns
    Transform[] CenterlinePoints
    float CorridorHalfWidth
~~~

对局初始化后得到只读数据：

~~~text
LaneRuntimeData                                   【静态配置】
    ushort LaneId
    LaneTeamSpawnData[] TeamSpawns
    fp2[] CenterlinePoints
    fp CorridorHalfWidth
~~~

CenterlinePoints 只用于：

- 标识 HomeLane。
- 判断是否追击过远。
- 计算最近回线点。
- 地图编辑可视化。

正常推进仍读取移动系统已有的队伍流场。兵线中心线不重新生成第二套路径，也不直接传给 MovementHandler。

### 生成票据

~~~text
MinionSpawnTicket
    int SpawnLogicTick                            【需要快照或确定性重建】
    TeamId TeamId                                 【需要快照或确定性重建】
    ushort LaneId                                 【需要快照或确定性重建】
    int UnitPrototypeId                           【需要快照或确定性重建】
    int StableEntryIndex                          【需要快照或确定性重建】
    fp2 SpawnPosition                             【需要快照或确定性重建】
    fp2 SpawnForward                              【需要快照或确定性重建】
~~~

票据只表达某个波次成员何时调用同步 SpawnUnit。StableEntryIndex 是波次展开后的稳定排序位置，不是单位生成序号。

同 Tick 票据使用固定比较键：

~~~text
SpawnLogicTick
TeamId
LaneId
StableEntryIndex
~~~

UnitUid 仍由 UnitWorld 根据当前 LogicTick、RuntimeEntityPrefabId 和全单位共享的 byte SpawnSequenceInTick 构造。MinionSystem 不维护自己的帧内生成序列。

### 核心波次算法

波次算法只包含两个步骤：到期时展开波次，随后按稳定顺序同步生成到期成员。

~~~text
TickWave:
    currentLogicTick = SimulationTickContext.Current.Tick

    当 currentLogicTick 已到达 NextWaveLogicTick:
        根据 WaveIndex 选择当前 Phase 与 Composition
        按 TeamId、LaneId 和成员配置顺序展开票据
        WaveIndex 增加
        NextWaveLogicTick 增加 WaveIntervalTicks

    从 NextTicketCursor 开始:
        依次处理 SpawnLogicTick 不晚于 currentLogicTick 的票据
        每张票据同步生成 Unit 并注册 MinionAIController
        推进 NextTicketCursor
~~~

若一次逻辑推进跨过多个波次时间点，使用循环补齐所有到期波次，不能只生成最后一波。Composition 的选择规则为：

~~~text
选择 StartWaveIndex 不大于 WaveIndex 的最后一个 Phase
cycleIndex = Phase 内波次偏移 mod CompositionCycle.Length
~~~

这是本模块唯一需要完整伪代码说明的核心算法。具体分钟数、兵种比例和超级兵条件保留在配置与比赛规则中。

### 单个小兵的同步生成

~~~mermaid
flowchart TD
    A["到期 MinionSpawnTicket"] --> B["UnitWorld.SpawnUnit"]
    B --> C["立即取得 UnitUid"]
    C --> D["保存 ManagedMinionUids"]
    D --> E["创建并配置 MinionAIController"]
    E --> F["UnitWorld 注册 Controller"]
    F --> G["下一 LogicTick 开始 AI"]
~~~

MinionSystem 只保存管理所需的 UnitUid，不保存 Controller 引用映射。近战兵、远程兵、炮车兵和超级兵的数值、攻击方式与投掷物定义来自 UnitPrototype 和既有 Handler 装配。

### 正式死亡与管理注销

小兵正式进入 Dead 后，UnitWorld 在注销 MinionAIController 之前同步调用：

~~~csharp
public bool UnregisterManagedUnit(
    UnitUid unitUid);
~~~

MinionSystem 只从 ManagedMinionUids 中注销这一个 UID，不处理 LifeState、死亡奖励、Handler、Modifier、死亡表现或对象池。第一版直接稳定扫描 UID 数组，找到后写入无效 UID 作为墓碑；重复通知返回 false，不重复修改状态。墓碑在固定维护阶段批量压缩，避免每次死亡都移动后续元素。

这项注销在 UnitDeath 回调及死亡临时状态清理之后、UnitWorld.UnregisterAIController 之前完成。死亡动画结束与最终回池不会再次改变 MinionSystem 的管理关系。

### 性能与恢复关注点

- PendingTickets 保持有序并用游标消费，不在每 Tick 重排全部票据。
- 已消费票据只在达到容量阈值时批量压缩。
- 波次配置初始化后冻结，不在运行时使用 LINQ 展开。
- ManagedMinionUids 只保存 UID，不复制 Unit 或 Controller 状态。
- UID 注销采用稳定扫描、墓碑和延迟批量压缩，不为此新增 UnitUid → Index 权威映射。
- 对象池预热和复用由 UnitWorld 负责。
- WaveIndex、NextWaveLogicTick、ManagedMinionUids 和无法从日程唯一重建的未消费票据会影响未来生成与管理，必须进入快照。
- 恢复后 UnitWorld 仍是 SpawnSequenceInTick 的唯一权威，MinionSystem 不能根据票据自行恢复序列计数。

---

### 定位与状态

MinionAIController 决定单个小兵推进、索敌、追击或回线。它不移动 Unit，也不直接调用 AttackHandler 启动攻击。

~~~text
MinionAIController
    UnitUid OwnerUnitUid                          【基类稳定身份】
    ushort HomeLaneId                             【需要快照】
    MinionAIState State                           【需要快照】
    int NextDecisionLogicTick                     【需要快照】
    int TargetLockUntilLogicTick                  【需要快照】
    fp2 EngageOrigin                              【需要快照】
    UnitUid PendingAssistTargetUid                【需要快照】
    int PendingAssistExpireLogicTick              【需要快照】
    Unit Owner                                    【可重建】
    UnitQueryBuffer CandidateBuffer               【可重用，可重建】
~~~

~~~text
MinionAIState
    AdvanceLane
    EngageTarget
    ReturnToLane
~~~

~~~text
MinionAIProfile                                   【静态配置】
    int DecisionIntervalTicks
    int TargetLockTicks
    int AssistAggroDurationTicks
    fp AcquireRadiusSq
    fp MaxChaseFromEngageOriginSq
    fp MaxDistanceFromHomeLaneSq
~~~

Profile 只保存 AI 参数，不复制攻击距离、移动速度、攻击前摇或目标是否可选中。这些继续读取 Unit 的数值和 Handler 公开状态。

~~~mermaid
stateDiagram-v2
    [*] --> AdvanceLane
    AdvanceLane --> EngageTarget: 选择目标
    EngageTarget --> ReturnToLane: 目标失效或追击越界
    ReturnToLane --> AdvanceLane: 回到兵线
    ReturnToLane --> EngageTarget: 协防或发现目标
~~~

当前正式攻击目标已经由 Unit Intent、ActionStateView 和 AttackHandler 表达，MinionAIController 不保存一份长期 CurrentTargetUid。PendingAssistTargetUid 只表示尚待下一次决策消费的协防事实。

### 推进和回线

AdvanceLane 状态下，控制器保证 Unit 当前 Order 表达沿 HomeLane 推进。移动系统根据该语义选择队伍流场和局部避障，AI 不传递 FlowFieldId、AStar 标志或 RVO 开关。

当小兵因击退、恐惧或其它控制偏离兵线时，控制和强制位移先按既有行为仲裁执行。重新具备普通行动能力后，AI 根据逻辑位置决定继续推进还是前往最近回线点。

回线点由 LaneRuntimeData.CenterlinePoints 计算。AI 只提交目的位置，实际使用 A* 还是 Direct 仍由 UnitLocomotionAgent 决定。

### 目标选择

默认优先级：

| 优先级 | 候选 |
|---:|---|
| 0 | 有效的英雄协防目标 |
| 1 | 当前行为正在攻击且仍合法的目标 |
| 2 | 敌方小兵 |
| 3 | 敌方英雄或召唤物 |
| 4 | 当前允许攻击的敌方建筑 |

同一优先级的稳定比较键：

~~~text
PriorityBand 升序
DistanceSq 升序
UnitUid 升序
~~~

当前目标仍合法且 TargetLockUntilLogicTick 未到时直接保持。锁定到期并不表示必须换目标，只有严格更高优先级的候选才能抢占；同优先级的微小距离变化不会造成频繁切换。

统一合法性过滤至少检查：

- 目标可以由 UnitWorld 解析。
- 目标 LifeState 允许被选中。
- CapabilityState.IsTargetable 为 true。
- 阵营关系满足当前规则。
- 目标位于索敌和追击边界内。
- 当前地图规则允许小兵攻击该单位分类。

### 英雄协防

小兵协防只响应已经由 Gameplay 正式确认的敌对行为，不读取动画、特效或玩家输入。

固定业务通知到达后，MinionAIController 检查：

- 攻击者是敌方英雄。
- 受害者是己方英雄。
- 攻击者和受害者位于该小兵协防范围内。
- 攻击者仍合法且没有越过硬追击边界。

满足条件时更新 PendingAssistTargetUid，并把 NextDecisionLogicTick 提前到当前或下一次允许决策的 LogicTick。通知函数只记录事实，不在回调中直接创建攻击 Runtime。

同一 Tick 收到多个协防候选时，不引入事件序列号。控制器用固定比较键选出唯一候选：

~~~text
DistanceSq 升序
AttackerUnitUid 升序
~~~

这个选择与通知调用顺序无关。

### 追击边界

从推进转入交战时记录 EngageOrigin。继续追击需要同时满足：

- 与 EngageOrigin 的平方距离不超过最大追击距离。
- 与 HomeLane 中心线的平方距离不超过兵线追击宽度。
- 目标没有进入禁止小兵追击的地图区域。

超出限制后，控制器清除不再正确的攻击 Order，切换 ReturnToLane，并下达前往最近回线点的移动 Order。进入兵线走廊后恢复 AdvanceLane。

### 核心决策算法

~~~text
TickLogic:
    currentLogicTick = SimulationTickContext.Current.Tick

    如果 Owner 不存在、不能参与模拟或暂时不能普通决策:
        返回

    如果未到 NextDecisionLogicTick 且没有协防唤醒:
        返回

    如果存在未过期且合法的协防目标:
        必要时下达 AttackOrder
        State = EngageTarget
        记录 EngageOrigin
        安排下一次决策
        返回

    如果 Unit 当前攻击目标仍合法且没有越过追击边界:
        保持当前 Order
        安排下一次决策
        返回

    在有限范围内按稳定比较键选择新目标
    找到目标:
        下达 AttackOrder
        State = EngageTarget
        记录 EngageOrigin
    否则当前位置偏离 HomeLane:
        下达回线 MoveOrder
        State = ReturnToLane
    否则:
        下达 LaneAdvanceOrder
        State = AdvanceLane

    安排下一次决策
~~~

### 多态快照实现

MinionAIController 的 Capture 只写 UnitAIControllerSnapshot.MinionState，Restore 只读取该分支并验证 ControllerKind。

Owner、LaneRuntimeData 引用和 CandidateBuffer 不进入状态分支。Resolve 通过 OwnerUnitUid 和 HomeLaneId 重新关联；Rebuild 只清理或重建 CandidateBuffer 等派生缓存。

### 性能约束

- 完整索敌只在决策时间或协防唤醒后执行。
- 当前目标合法时不申请候选缓冲。
- 使用 fp 平方距离，不开平方。
- CandidateBuffer 复用，不创建临时 List。
- 不复制移动路径或 FlowField 状态。
- 空闲小兵的初始决策相位由 OwnerUnitUid 稳定错开。

---


## 需求演进

### 2026-08-07

变动内容：小兵初始 Buff、塔攻击与兵线/单位配置拆分。

legacyDecision：D-037

### 2026-08-24

变动内容：动作键暴击和等距中性裁决；覆盖先前对 UID 耦合随机样本和目标平局的许可。

legacyDecision：D-050


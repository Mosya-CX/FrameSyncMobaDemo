# 野怪营地刷新与共享仇恨

## 目标实现

营地稳定生成成员并在主怪死亡后按规则刷新。

## 技术方案

JungleCamp 拥有营地、respawn 和 member slot；MonsterAIController 管理战斗、共享目标、追击与回营，复用普通行为链。

## 边界情况

主怪/小怪死亡、营地清空、目标远离和不可达分别处理；三狼回营/寻路旧计划未完成，不能标成已验收。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/NonHero/JungleCamp.cs`：当前关联实现定义 JungleCampSpawnSlot、JungleCamp（以源码为实际命名）。
- `Assets/Scripts/Gameplay/NonHero/MurkWolfContentIds.cs`：当前关联实现定义 MurkWolfContentIds（以源码为实际命名）。
- `Assets/Scripts/Gameplay/NonHero/UnitAIController.cs`：当前关联实现定义 UnitAIController、MinionAIController、ThreatEntry、MonsterAIController、TowerAIController（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/EditMode/MurkWolfFormalContentTests.cs`：UnitCatalog_ContainsAuthoredGreaterAndMiniWolfValues、GreaterWolf_OnHitBuff_IsPermanentThreePercentCurrentHealth、MapPrefab_AuthorsTwoVisualizedThreeWolfCamps、MapPrefab_AllWolfSpawnSlotsAreWalkableForTheirRadius、MapCampUpsert_PreservesVisualAuthoringAndOtherCamps。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/AddressableMatchContentPlayModeTests.cs`：VarusSelection_LoadsOnlyVarusAndReleasesEveryHandle、AatroxSelection_LoadsItsQMetadataWithoutVarus、SelectedMap_CreatesOwnedSortedCampTopology。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/MurkWolfPrefabPlayModeTests.cs`：GreaterWolf_AlertAndMoveRoutesPlayLoopedMotion、MiniWolf_AlertAndMoveRoutesPlayLoopedMotion、BoundDriver_AlertRouteUsesAuthoredTransitionClips。
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：MinionWave_ExpandsCanonicalTeamLaneMemberOrder、MinionUnregister_RemovesUidWithoutLeavingTombstone、LaneNearestPoint_ProjectsOntoCenterlineSegment、MinionAI_BetweenDistantCenterlineNodes_RemainsInLaneAdvance、MinionAI_FarFromLane_ReturnStateStillUsesLaneFlowField、AIController_DoesNotTickOnSpawnTick、MinionAI_UsesLaneAdvanceOrderThroughPlanner。
- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameworkSmokeBootstrapTests.cs`：Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks、Bootstrap_BindsSelectedHeroPrototypeToPlayerSpawn。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

JungleCamp 表示地图中的一处普通野怪营地，实现为场景 MonoBehaviour：

~~~csharp
public sealed class JungleCamp : MonoBehaviour
~~~

它直接承载场景编辑字段和营地运行状态，不再额外建立 JungleCampBakeData、静态营地配置类或 GlobalGameplayData 中的营地副本。

JungleCamp 不使用 Update。开局时按 CampId 注册给 UnitWorld，随后由固定 Gameplay Pipeline 调用 TickLogic；函数内部自行读取 SimulationTickContext.Current.Tick。

### Inspector 字段与确定性初始化

~~~text
JungleCamp
    ushort CampId                                 【静态配置】
    TeamId CampTeamId                             【静态配置】
    Transform CampAnchor                          【场景引用】
    float InitialSpawnSeconds                     【Inspector 配置】
    float RespawnDelaySeconds                     【Inspector 配置】
    float SoftLeashRadius                         【Inspector 配置】
    float HardLeashRadius                         【Inspector 配置】
    float DisengageDelaySeconds                   【Inspector 配置】
    byte MainMonsterSlotIndex                     【静态配置】
    JungleCampSpawnSlot[] SpawnSlots              【静态配置】

JungleCampSpawnSlot
    byte SlotIndex
    int UnitPrototypeId
    Transform SpawnPoint
~~~

初始化时，JungleCamp 自己把场景字段转换为内部确定性值：

~~~text
CampAnchorPosition : fp2
InitialSpawnLogicTick : int
RespawnDelayTicks : int
SoftLeashRadiusSq : fp
HardLeashRadiusSq : fp
DisengageDelayTicks : int
SpawnPositionBySlot : fp2[]
SpawnForwardBySlot : fp2[]
~~~

这些值仍属于同一个 JungleCamp，不生成额外 BakeData 层。逻辑 Tick 不再读取 Transform.position 或浮点秒累计。

CampId 在地图内唯一，SpawnSlots 按 SlotIndex 稳定排序。MainMonsterSlotIndex 必须由设计者明确指定，不能根据体型、血量、Prototype 名称或 UnitSubKindId 临时猜测。

### 营地运行状态

~~~text
JungleCampState
    Dormant
    Idle
    InCombat
    Returning
    WaitingRespawn
~~~

~~~text
JungleCamp Runtime
    JungleCampState State                         【需要快照】
    UnitUid[] MemberUidsBySlot                    【需要快照】
    bool[] MemberAliveBySlot                      【需要快照】
    bool MainMonsterDead                          【需要快照】
    UnitUid PrimaryTargetUid                      【需要快照】
    int LastHostileActionLogicTick                【需要快照】
    int NextRespawnLogicTick                      【需要快照】
    int ResetBeginLogicTick                       【需要快照】
~~~

Unit、MonsterAIController 和 Transform 引用都属于可重建引用。

本营地交给帧同步设计的完整状态结构为：

~~~text
JungleCampSnapshot
    ushort CampId
    JungleCampState State
    UnitUid[] MemberUidsBySlot
    bool[] MemberAliveBySlot
    bool MainMonsterDead
    UnitUid PrimaryTargetUid
    int LastHostileActionLogicTick
    int NextRespawnLogicTick
    int ResetBeginLogicTick
~~~

不保存 CombatGroupState。普通营地的共同作战关系已经由固定成员槽位、State 和 PrimaryTargetUid 表达，不再增加 CombatGroup 类或第二套共享目标状态。

~~~mermaid
stateDiagram-v2
    [*] --> Dormant
    Dormant --> Idle: 首次生成
    Idle --> InCombat: 合法敌对行为
    InCombat --> Returning: 主怪存活且脱战
    Returning --> Idle: 全部成员回营
    Returning --> InCombat: 再次合法开战
    InCombat --> WaitingRespawn: 主怪已死且战斗结束
    WaitingRespawn --> Idle: 整营重新生成
~~~

### 初次生成

到达 InitialSpawnLogicTick 后，JungleCamp 按 SlotIndex 升序：

1. 调用 UnitWorld.SpawnUnit 同步生成成员。
2. 立即取得 UnitUid。
3. 创建绑定 CampId 与 SlotIndex 的 MonsterAIController。
4. 注册到统一 AIControllers。
5. 写入 MemberUidsBySlot 和 MemberAliveBySlot。
6. 清理旧主目标、主怪死亡标记和刷新时间。
7. 进入 Idle。

同 Tick 生成多个营地时，UnitWorld 按 CampId 升序推进 JungleCamp；每个营地再按 SlotIndex 升序调用 SpawnUnit。UnitWorld 仍统一分配 SpawnSequenceInTick。

### 战斗状态和共享目标

任一存活成员主动发现目标或收到合法敌对行为后，先调用所属 JungleCamp 的明确入口。营地验证目标后：

- 设置 PrimaryTargetUid。
- 更新 LastHostileActionLogicTick。
- 进入 InCombat。
- 把全部存活 MonsterAIController 的下一次决策提前。

普通营地第一版只维护一个共享主目标，不建立无限容量仇恨表。这样既避免每只野怪重复扫描，也避免成员因通知顺序立即分散。

脱战条件由营地统一判断，例如：

- 任一存活成员越过 HardLeashRadius。
- 当前目标失效且超过 DisengageDelayTicks。
- 所有合法目标离开 SoftLeashRadius。
- 地图规则明确要求重置营地。

主野怪仍存活时，营地进入 Returning。存活成员分别由 MonsterAIController 下达 ReturnToCampOrder。生命恢复、Buff 清理或控制清理必须走既有正式系统，JungleCamp 不能直接写 CurrentHealth 或 Handler 内部字段。

### 主野怪死亡与刷新

普通营地开始刷新倒计时的必要且充分业务条件是：

~~~text
MainMonsterDead == true
并且
State != InCombat
~~~

主野怪在战斗中死亡时，只记录 MainMonsterDead，不立即计时。剩余小怪可以继续战斗。等营地正式退出战斗后，再清理残余成员并开始整营刷新。

成员死亡由 UnitWorld 在正式写入 LifeState.Dead、发布 UnitDeath 并完成死亡临时状态清理后直接通知 JungleCamp。营地只更新自己的成员槽位和刷新业务状态，不参与死亡判定，也不清理该 Unit 的 Handler 或 Modifier。OnMemberDeath 返回后，UnitWorld 再注销对应 MonsterAIController。

核心算法：

~~~text
OnMemberDeath:
    根据死亡 UnitUid 找到 SlotIndex
    MemberAliveBySlot[SlotIndex] = false

    如果是 MainMonsterSlotIndex:
        MainMonsterDead = true

    如果没有存活成员:
        结束当前战斗关系

    尝试启动刷新倒计时

TryStartRespawnCountdown:
    currentLogicTick = SimulationTickContext.Current.Tick

    如果 MainMonsterDead 为 false:
        返回

    如果 State 为 InCombat:
        返回

    请求 UnitWorld 按非死亡规则清场仍存活的次要成员
    清理成员槽位
    NextRespawnLogicTick = currentLogicTick + RespawnDelayTicks
    State = WaitingRespawn
~~~

主野怪死亡后清理的存活次要成员属于营地规则清场，不属于正式死亡：不写入 Dead，不发布 UnitDying、UnitDeath 或 UnitKill，也不产生击杀奖励和死亡 Reaction。具体非死亡处置入口、Gameplay 停用及最终回池由 UnitWorld 负责，本模块只提交受影响的成员 UID 并清理自己的槽位，不定义第二套处置流程。

WaitingRespawn 状态到达 NextRespawnLogicTick 后，重新同步生成全部槽位。新一代成员获得新的 UnitUid，不进入 Respawning，也不会与上一代残余小怪重叠。

### 边界情况

| 情况 | 结果 |
|---|---|
| 只击杀次要野怪，主野怪存活 | 不启动刷新 |
| 主野怪死亡，营地仍在战斗 | 记录死亡，暂不计时 |
| 主野怪死亡，战斗随后结束 | 清理剩余次要成员并开始刷新 |
| 全部成员死亡 | 营地立即具备非战斗条件并开始刷新 |
| 主怪存活时营地脱战 | 正常 Returning，不刷新 |
| WaitingRespawn 期间再次收到旧成员通知 | 通过代际 UID 和槽位当前 UID 校验后忽略 |

最后一条避免对象池中的旧 Unit 或延迟业务通知污染新一代营地状态。

### 史诗野怪边界

JungleCamp 只实现普通营地规则。它不负责：

- 大龙地图生成与复活规则。
- 小龙元素轮换、龙魂或远古龙。
- 厄塔汗形态、出生区域和地图改造。
- 先锋等拥有独立阶段的地图机制。

未来需要某一种史诗野怪时，单独实现该机制并接入 UnitWorld，不预先建立 EpicMonsterSpawner 或 EpicMonsterCampBase。

### 性能与恢复关注点

- JungleCamp 数量有限，由 UnitWorld 按 CampId 稳定推进。
- 营地成员按固定槽位数组保存，不使用运行时 HashSet。
- 共享 PrimaryTargetUid，避免所有成员重复完整索敌。
- 距离判断使用 fp 平方距离。
- State、成员 UID、成员存活标记、主怪死亡标记、主目标和所有未来 LogicTick 会影响刷新与战斗，必须进入快照。
- 场景 Transform 和 MonsterAIController 引用不进入快照，恢复后按 CampId、SlotIndex 和 UnitUid 重建。

---

### 定位与状态

MonsterAIController 负责单只普通野怪的行动决策。成员关系、共享目标、主怪死亡和刷新计时属于 JungleCamp。

~~~text
MonsterAIController
    UnitUid OwnerUnitUid                          【基类稳定身份】
    ushort CampId                                 【需要快照】
    byte CampSlotIndex                            【需要快照】
    MonsterAIState State                          【需要快照】
    int NextDecisionLogicTick                     【需要快照】
    Unit Owner                                    【可重建】
    JungleCamp Camp                               【可重建】
~~~

~~~text
MonsterAIState
    CampIdle
    EngageTarget
    ReturnToCamp
~~~

~~~text
MonsterAIProfile                                  【静态配置】
    int DecisionIntervalTicks
    fp AggroRadiusSq
    fp ReturnArriveDistanceSq
~~~

牵引半径、脱战延迟和出生位置属于 JungleCamp，不在每个控制器中复制。PrimaryTargetUid 也只由 JungleCamp 保存。

### 待机和开战

CampIdle 时不下达无意义的原地移动 Order。到达决策时间后，主动作战型野怪可以在营地锚点附近做有限范围扫描。

发现目标后，MonsterAIController 不私自进入战斗，而是向 JungleCamp 提交该候选。JungleCamp 验证并决定是否让整营进入 InCombat，再统一唤醒成员。

普通野怪默认不随机巡逻。确实需要巡逻的特定野怪可以配置固定巡逻点，但不为所有营地增加随机漫游状态和随机种子。

### 交战

营地处于 InCombat 时，控制器读取 JungleCamp.PrimaryTargetUid：

- 目标合法时，必要时下达 AttackOrder。
- Planner 根据攻击距离决定原地攻击或追击。
- 目标失效时，通知 JungleCamp 重新判断主目标或脱战。

控制器不复制营地主目标，也不自行选择与营地不同的普通目标。

追击继续复用：

~~~text
AttackOrder
    → AttackTarget Intent
    → ChaseForAttack
    → MovementHandler
    → UnitLocomotionAgent
    → AStar 或 Direct
~~~

### 回营

营地进入 Returning 后，MonsterAIController：

1. 清除已经不正确的攻击 Order。
2. 下达目标为自身出生槽位的 ReturnToCampOrder。
3. 在营地允许前不重新主动索敌。
4. 到达槽位后清除移动 Order，并通知 JungleCamp 当前成员已归位。

~~~mermaid
stateDiagram-v2
    [*] --> CampIdle
    CampIdle --> EngageTarget: 营地进入战斗
    EngageTarget --> ReturnToCamp: 营地开始重置
    ReturnToCamp --> EngageTarget: 营地允许重新开战
    ReturnToCamp --> CampIdle: 营地重置完成
~~~

ReturnToCampOrder 只表达目的，不携带 AStar、Direct 或重寻路参数。控制效果和强制位移仍由 ActionArbiter 与移动系统按既有优先级处理。

### 核心决策算法

~~~text
TickLogic:
    currentLogicTick = SimulationTickContext.Current.Tick
    解析 Owner 与 JungleCamp

    如果 Owner 不存在或不能参与模拟:
        返回

    如果 Camp 为 Dormant 或 WaitingRespawn:
        清除不再正确的 Order
        返回

    如果 Camp 为 Returning:
        保证当前为 ReturnToCampOrder
        返回

    如果 Camp 为 InCombat:
        PrimaryTarget 合法:
            保证当前为 AttackOrder
        否则:
            请求 Camp 判断脱战
        返回

    如果 Camp 为 Idle 且到达 NextDecisionLogicTick:
        执行有限主动索敌
        找到候选时交给 Camp 验证
        安排下一次决策
~~~

### 技能型野怪

拥有技能的野怪继续通过 Unit 已装配的 AbilityHandler 执行技能。MonsterAIController 可以按确定性条件下达既有施法 Order，但不复制技能 Stage、冷却、前摇或命中状态。

行为差异显著的野怪可以派生专用 MonsterAIController。普通野怪和 Boss 行为不能全部塞入一个枚举与巨型 switch。

### 多态快照与性能

MonsterState 只保存 CampId、CampSlotIndex、State 和 NextDecisionLogicTick。Owner 与 Camp 引用在 Resolve 中恢复，营地主目标继续从 JungleCamp 读取；Rebuild 不生成新的权威状态。

- Idle 主动扫描按 OwnerUnitUid 稳定错峰。
- InCombat 成员复用营地主目标，不重复全量扫描。
- Returning 只检查与出生槽位的平方距离。
- 不维护动态威胁字典或通用行为树。

---


## 需求演进

### 2026-08-24

变动内容：动作键暴击和等距中性裁决；覆盖先前对 UID 耦合随机样本和目标平局的许可。

legacyDecision：D-050


# 开局生成与事件接入流程

## 本功能范围

本案细化“同步生成死亡复活与回池”中的开局生成与事件接入流程，仅覆盖下列明确接口与边界。

## 目标实现

单位生死、复活、规则移除和池化形成单一生命周期。

## 技术方案

UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。

## 边界情况

ClearForDeath、ClearForRespawn、ClearForDespawn 按固定顺序；永久 Buff 与装备跨死亡保留所属状态；回池新生命周期不沿用旧身份。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。

### 开局初始化

```text
加载 GlobalPrefabTable
    ↓
加载 StatDefinitionTable 与 GlobalParamTable
    ↓
加载 UnitSubKindTable 与 TeamBaseUnitSubKindId
    ↓
加载 UnitDisposePolicyTable
    ↓
加载 GlobalUnitPrototypeTable
    ↓
统一配置校验
    ↓
UnitPoolRegistry Prewarm
    ↓
UnitWorld Ready
```

初始化时固定 Unit 预制体中的 Handler 结构、`UnitEventBus` 路由依赖和 `CombatModifierSet` 容器接缝。

---

### 同步生成与 AIController 注册

```text
调用 UnitWorld.SpawnUnit(request)
    ↓
内部读取 SimulationTickContext.Current.Tick
    ↓
读取 UnitPrototype
    ↓
通过 GlobalPrefabTable 解析 RuntimeEntityPrefabId
    ↓
UnitWorld 分配 byte SpawnSequenceInTick
    ↓
构造 UnitUid
    ↓
按 UnitPrototypeId Rent / Instantiate
    ↓
绑定 UnitHandler.Owner
    ↓
初始化 StatHandler 与 CombatModifierSet
    ↓
通过 PhysicsEntity2D.SetLogicPose 初始化逻辑姿态
    ↓
注册 UnitRegistry 和 PhysicsEntity2D
    ↓
同步返回 UnitUid
```

返回后：

```csharp
UnitUid unitUid =
    unitWorld.SpawnUnit(request);

unitWorld.TryGetUnit(
    unitUid,
    out Unit unit);
// 必须立即成功。
```

具体管理方随后决定 AI 分配并完成首次注册：

```text
MinionSystem / JungleCamp / 地图装配
    ↓
只把 unitUid 加入自己的受管 UID 集合
    ↓
确定 AIProfile、Lane / Wave / Camp / Team 等业务配置
    ↓
创建或取得已配置的具体 AIController
    ↓
UnitWorld.RegisterAIController(unitUid, controller)
    ↓
UnitWorld 唯一保存 UnitUid -> UnitAIController
    ↓
管理方不再保存 Controller 引用或第二份映射
```

后续管理方只保留 `UnitUid` 与自身 AI 分配、波次、营地、编队和行为状态。需要访问 Controller 时统一通过 `UnitWorld.TryGetAIController`。

新单位不延迟生成，也不存在 Pending Spawn。  
主动 Gameplay 从下一 `LogicTick` 开始，直接由以下条件推导：

```csharp
bool canRunActiveGameplay =
    SimulationTickContext.Current.Tick
    > unitUid.SpawnLogicTick;
```

生成 Tick 内：

```text
Unit 已注册并可查询。
可以成为目标并参与碰撞。
可以受到伤害、治疗、Buff 和控制。
可以接收 UnitEventBus 被动结果事件。
AIController 可以完成注册。
```

生成 Tick 内不执行：

```text
主动 AI 决策
主动 Order
BehaviorPlanner
ActionRuntime 主动推进
普通主动移动
普通攻击
主动技能推进
```

不保存 `FirstAITickLogicTick` 或 `FirstActiveLogicTick`。  
不传 `SimulationTickContext` 参数。  
不把 `SpawnLogicTick` 放入 `UnitSpawnRequest`。  
不改成 Submit + Flush 形式。

---

### 行为总流程

```text
Command
    ↓
需要行为语义时翻译为 Order
    ↓
更新 Intent
    ↓
BehaviorPlanner
    先读取 CrowdControlBehaviorOverride
    没有强制行为时读取普通 Intent
    ↓
ActionRequest
    ↓
ActionArbiter
    读取 Capability、CrowdControlStateView、
    当前 Runtime 和 Reservation
    ↓
ActionRuntime
    ↓
MovementHandler / AttackHandler / AbilityHandler
```

强制位移：

```text
CrowdControlHandler
    ↓
MovementHandler.StartForcedDisplacement
```

技能点：

```text
AllocateAbilitySkillPointCommand
    ↓
CommandDispatcher
    ↓
Unit.AbilityHandler.TryAllocateSkillPoint
```

不进入 Order 或 Action 链路。

---

### 攻击行为接缝

```text
BehaviorPlanner
    ↓
AttackHandler.GetAttackPlanStatus(targetUid)
    ├── 需要追击
    ├── 等待攻击就绪
    └── 可以申请 AttackAction

AttackActionRuntime.Start
    ↓
AttackHandler.BeginAttack(targetUid)

到达 Commit 节点
    ↓
AttackHandler.CommitAttack()

Commit 前取消
    ↓
AttackHandler.CancelBeforeCommit()

正式攻击计时器重置规则
    ↓
AttackHandler.ResetAttackTimer(reason)
```

攻击模块内部如何生成 DamageRequest 或 Projectile，遵循攻击模块 v4。  
攻击模块需要产生本单位的 11 种正式结果事件时，通过 `UnitEventBus` 的对应强类型入口发布，不引入另一套 GameplayEventQueue。

---

### UnitEventBus 即时结果流

```text
外部系统完成正式结果
    ↓
构造具体强类型事件
    ↓
对应 Unit.EventBus.Publish(evt)
    ↓
UnitEventBus 直接按固定顺序调用具体 Handler
    ↓
Handler 查询自己的 Reaction 静态配置
    ↓
如需后续 Gameplay 业务，提交对应系统 Request
```

没有：

```text
统一 UnitEventRecord
EventLogicTick
动态订阅
内部 Pending 队列
Tick 末 Drain
```

事件只保留专题六冻结的 11 种。  
伤害与治疗事件可以携带 `SourceDescriptor / RecipeId / Calculated / Actual` 等结果字段，但不携带 Modifier Record、Handle 或命中 ID 列表。

---

### 死亡与复活总流程

```text
CombatSystem 当前 Combat Settlement Cycle
    ↓
发现致死条件
    ↓
同步请求 UnitWorld: Alive -> Dying
    ↓
Publish UnitDying
    ↓
UnitDying Reaction 新请求继续进入当前 Combat 循环
    ↓
CombatSystem 处理死亡阻止
    ├── 被阻止
    │       ↓
    │   同步请求 UnitWorld: Dying -> Alive
    │
    └── 确认正式死亡
            ↓
        同步请求 UnitWorld.ConfirmUnitDeath
            ↓
        UnitWorld: Dying -> Dead
            ↓
        Publish UnitDeath
            ↓
        UnitDeath Reaction 新请求继续进入当前 Combat 循环
            ↓
        各 Handler 只清理自身不跨死亡保留的临时状态
            ↓
        非英雄管理方注销或更新管理关系
            ↓
        UnitWorld 注销非英雄 AIController
            ↓
        播放死亡动画
            ↓
        UnitWorld 按 DisposePolicy 处理
```

普通死亡和进入 `Respawning` 都不执行：

```text
StatHandler.ClearModifiers
CombatModifiers.Clear
```

永久技能被动、装备属性、常驻装备被动、永久 Buff 和其它允许跨死亡保留的 Modifier 继续存在。  
只有对应来源 Runtime 结束时，才使用自己的 Handle 移除 Modifier。

英雄：

```text
Dead
    ↓ UnitWorld 等待 RespawnDelayTicks
Respawning
    ↓ 按与死亡阶段相同的固定 Handler 顺序调用 ClearForRespawn
    ↓ 保留 Runtime 重建 LifeStageHandle，PersistentHandle 不重复挂载
    ↓ 恢复位置、生命和资源
Alive
```

小兵和普通野怪：

```text
Dead
    ↓ 正式死亡时立即注销管理关系和 AIController
    ↓ 死亡动画完成
Pool
```

史诗野怪：

```text
Dead
    ↓ 正式死亡时立即注销管理关系和 AIController
    ↓ 死亡动画完成
Destroy
```

防御塔：

```text
Dead
    ↓ 正式死亡时更新对应建筑管理关系并注销 AIController
    ↓ 死亡动画完成
Destroy Tower
    ↓
UnitWorld.SpawnUnit(TowerRuinPrototype)
```

CombatSystem 完成击杀归属后，保存自己的逻辑结果并向 Killer 的 `UnitEventBus` 发布 `UnitKill`。  
正式权威击杀、KDA、金币和经验结算仍归战斗系统与比赛流程总控。

---

### 非死亡规则清场总流程

正常 Gameplay 非死亡移除：

```text
召唤物到期 / 脚本清场 / 拥有者解除
    ↓
UnitWorld.DespawnUnit
    ↓
停止行为和空间执行
    ↓
Handler.ClearForDespawn
    ↓
完整清理 Modifier、护盾、控制和 Runtime
    ↓
更新非英雄管理关系
    ↓
注销 AIController、Physics 和 UnitRegistry
    ↓
Pool / Destroy
```

该流程不进入 `Dying / Dead`，不发布 `UnitDying / UnitDeath / UnitKill`，也不产生死亡奖励。

回滚拓扑重建：

```text
UnitWorld.Restore
    ↓
比较当前 UnitUid 集合与目标快照
    ↓
多余单位
    -> RemoveUnitForRollbackRestore 静默移除

缺失单位
    -> 创建运行时载体

仍存在单位
    -> 保留对象
    ↓
Restore / Resolve / Rebuild
```

回滚期间不调用 `DespawnUnit`，不产生 Gameplay 清场回调，也不播放表现。

---

### 与全局 Gameplay Pipeline 的关系

单位框架不再维护另一套完整 Tick 步骤列表。  
完整顺序以全局 Gameplay Pipeline 设计案为唯一权威。

单位框架只要求以下相对顺序：

```text
控制系统完成 Advance / Rebuild
    早于
Capability 刷新、BehaviorPlanner 和 ActionArbiter

新生单位主动 Gameplay 检查
    通过 CurrentTick > UnitUid.SpawnLogicTick 推导
    早于主动 Order、Planner、Runtime 和 AI Tick

BehaviorPlanner
    早于
ActionRuntime / Handler 推进

移动执行
    在全局移动和物理阶段写入 PhysicsEntity2D

CombatSystem 当前结算循环
    同步请求 UnitWorld 写入 Dying / Alive / Dead
    并立即调用对应 UnitEventBus

UnitWorld 确认 Dead
    先发布 UnitDeath
    后清理来源自身不跨死亡保留的状态
    再注销非英雄管理关系与 AIController

Combat 阶段结束后的 UnitWorld 生命周期阶段
    只处理死亡表现、回池、Destroy、SpawnRuin
    和正常复活等跨 Tick 节点

Gameplay 规则触发的非死亡 Despawn
    在对应规则的固定阶段同步完成；
    不进入 Combat 死亡流程，也不进入死亡表现队列

死亡动画完成
    早于 UnitWorld 最终回池 / 销毁

快照保存
    位于全局 Pipeline 规定的最终阶段
```

所有 Gameplay 时间命名统一使用：

```text
LogicTick
Ticks
StartLogicTick
EndLogicTick
ElapsedTicks
```

模块内部需要当前 Tick 时读取：

```csharp
SimulationTickContext.Current.Tick
```

不把 Context 作为普通参数层层传递，也不维护第二套本地时钟。

---

### 统一回滚接缝

```csharp
public interface IRollback<TState>
{
    void Capture(ref TState state);
    void Restore(in TState state);
    void Resolve(in RollbackContext context);
    void Rebuild(in RollbackContext context);
}
```

单位框架接缝：

```text
Capture
    UnitWorld 聚合 Unit、AIController、生命周期节点和有状态 Handler。
    StatHandler 保存完整数值状态。
    CombatModifierSet 保存当前有效 Record。
    来源 Runtime 保存对应 Handle。

Restore
    先由 UnitWorld 静默移除目标快照中不存在的多余单位，
    并为快照中缺失的单位创建运行时载体。
    然后直接恢复历史状态。
    不调用正常 Gameplay 的添加、修改、移除和清理接口。
    不调用 DespawnUnit。
    不触发 UnitEventBus、数值变化或护盾回调。

Resolve
    按 UnitUid 修复 Owner、Target、AI 和跨系统引用。
    AI 主动生效时间不恢复额外字段，
    由 CurrentTick > Owner.UnitUid.SpawnLogicTick 推导。

Rebuild
    只重建 Physics 索引、CapabilityState、
    CrowdControlStateView、UI / Presentation 镜像和临时缓存。
```

不再：

```text
重建 StatModifier。
重新 Attach CombatModifier。
恢复 UnitEventBus 队列。
恢复 WatchHook 监听关系。
```

`UnitEventBus` 没有跨 Tick 队列；`WatchHook` 只是查询服务，因此二者没有独立快照关系。

具体快照字段和序列化格式由帧同步设计案决定。

---

### 最终边界

```text
Unit
    保存单位身份、分类、LifeState、Intent、行为链路和 Handler 引用。
    不公开 LifeState 写权限。

UnitWorld
    唯一正式写入 LifeState。
    同步生成单位并返回 UnitUid。
    提供 DespawnUnit 作为召唤物到期、脚本清场等非死亡 Gameplay 移除入口。
    提供内部 RemoveUnitForRollbackRestore 作为无事件、无表现的回滚拓扑清理入口。
    管理单位注册，并唯一维护 UnitUid -> UnitAIController 映射、Controller 查询、Tick、停用、恢复与注销。
    管理死亡表现、正常复活、回池、销毁和废墟生成。
    作为单位回滚聚合入口。

CombatSystem
    结算伤害、治疗、死亡阻止和击杀归属。
    通过 UnitWorld 请求生命周期转换。
    管理金币、经验、KDA 和权威确认。

UnitEventBus
    只路由冻结的 11 种强类型结果事件。
    只调用各 Handler 正式声明支持的回调。
    直接、即时、固定代码顺序分发。

CombatModifierSet
    保存当前有效的不可变战斗公式修正。
    只提供 Attach / Detach / Collect / Clear。
    正常 Gameplay 不提供 Update。
    Record.Id 由创建时 LogicTick 与调用处稳定字符串的确定性哈希组合生成。
    当前有效 Record 可直接快照恢复。

StatHandler
    根据 StatDefinition、StatPreset、等级成长和独立 StatModifier
    计算单位最终长期属性。
    自己创建 Modifier 并分配 StatSeq。
    通过 OwnerUnitUid + StatId + StatSeq 的 Handle 定位 Modifier。
    提供 AddModifier / SetModifierValue / RemoveModifier。
    完整快照属性、Modifier、护盾、缓存、Dirty 和帧间变化基线。

WatchHook
    不是订阅系统。
    只查询某个 StatId 相较上一 LogicTick 是否变化以及 Delta。

PhysicsEntity2D
    由物理系统唯一定义。
    单位框架只调用正式逻辑姿态接口，不直接写内部 Transform 或 Unity Transform。
```


## 需求演进

### 2026-10-02

变动内容：生成 Tick 可被动参与，主动工作晚于出生 Tick。

legacyDecision：D-008

### 2026-10-02

变动内容：正式死亡由 UnitWorld 同步执行，来源系统仅清理自己的句柄。

legacyDecision：D-009


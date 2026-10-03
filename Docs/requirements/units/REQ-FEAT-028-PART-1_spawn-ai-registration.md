# 同步单位生成与AI注册

## 本功能范围

本案细化“同步生成死亡复活与回池”中的同步单位生成与AI注册，仅覆盖下列明确接口与边界。

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

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 五、`UnitWorldSnapshot`

```text
UnitWorldSnapshot
    UnitSnapshot[]
    PendingUnitLifecycleQueue

    MinionSystemSnapshot
        WaveIndex
        NextWaveLogicTick
        PendingTickets[]
        NextTicketCursor
        ManagedMinionUids[]

    JungleCampSnapshot[]
        CampId
        State
        MemberUidsBySlot[]
        MemberAliveBySlot[]
        MainMonsterDead
        PrimaryTargetUid
        LastHostileActionLogicTick
        NextRespawnLogicTick
        ResetBeginLogicTick

    UnitAIControllerSnapshot[]
        ControllerKind
        OwnerUnitUid
        MinionState
        LaneId
        MinionLastThreatRefreshLogicTick
        MinionNextDecisionLogicTick
        MinionTargetLockUntilLogicTick
        MinionEngageOrigin
        MinionPendingAssistTargetUid
        MinionPendingAssistExpireLogicTick
        MinionThreatTable[] (UnitUid, Threat)
        MonsterState
        CampId
        MonsterCampSlotIndex
        MonsterNextDecisionLogicTick
        TowerState

    RuntimeRevision
```

字段与 Non-Hero v4 的正式运行状态对齐。

### AI 主动生效

不保存：

```text
FirstAITickLogicTick
FirstActiveLogicTick
CommonState
```

统一推导：

```text
CurrentTick > Owner.UnitUid.SpawnLogicTick
```

具体 Controller 只保存自己真实存在的状态分支。

### Unit 金字塔聚合

UnitWorld 按稳定 UnitUid 捕获 Unit。Unit 再聚合：

```text
LifeState
RespawnPosition
Locomotion
Behavior / Intent / Action
AttackHandler
AbilityHandler
BuffHandler
CrowdControlHandler
StatHandler
EquipmentHandler
事件处理后形成的 RuntimeState
```

正式死亡不会全量清空 StatHandler 或 CombatModifiers。各来源系统通过自己的 Handle 精确清理临时效果，快照保存清理后的真实状态。

UnitEventBus 自身不保存。

### 非英雄死亡后的管理状态

小兵死亡后：

```text
ManagedMinionUids
```

反映已经注销的管理关系。

野怪死亡后：

```text
MemberAliveBySlot
MainMonsterDead
State
NextRespawnLogicTick
```

反映营地成员死亡与复活计划。

死亡非英雄的 AIController 从 UnitWorld 注册表移除，因此对应快照中不再存在活动 Controller；营地或管理系统仍保存复活所需的稳定业务状态。

---

### 定位

`UnitWorld` 是单位实体和单位生命周期的权威管理中心。

它负责：

```text
同步生成 Unit。
分配 UnitUid。
注册和反注册 Unit。
管理多单位对象池。
唯一正式写入 Unit.LifeState。
校验 LifeState 转换。
接受 CombatSystem 和其它系统的生命周期请求。
管理死亡表现、复活等待和复活初始化。
管理回收、销毁和 TowerRuin 生成。
```

它不负责：

```text
伤害与治疗结算。
死亡是否被战斗效果阻止。
击杀归属。
金币、经验和 KDA 计算。
技能、攻击、Buff 或控制内部规则。
```

核心边界：

```text
CombatSystem
    判定战斗结果。
    请求 UnitWorld 进入 Dying、返回 Alive 或确认 Dead。

UnitWorld
    决定并执行单位生命周期状态转换。
    管理 Dead 之后的实体生命周期。

Unit
    保存 LifeState。
    不对外暴露写权限。
```

---

### UnitWorld 核心结构

```text
UnitWorld
├── UnitRegistry
├── UnitPoolRegistry
├── UnitPrototypeDatabase
├── GlobalPrefabTable
├── UnitDisposePolicyDatabase
├── UnitAIControllerRegistry
├── PendingUnitLifecycleQueue
├── UnitSpawnService
├── UnitLifecycleService
└── UnitRespawnService
```

其中 `UnitAIControllerRegistry` 是项目内唯一的：

```text
UnitUid -> UnitAIController
```

映射容器。任何小兵、野怪、建筑或其它单位管理模块都不得建立同义的第二份 Controller 映射。

推荐公开能力：

```csharp
public sealed class UnitWorld
{
    public UnitUid SpawnUnit(
        in UnitSpawnRequest request);

    public bool TryGetUnit(
        UnitUid unitUid,
        out Unit unit);

    // 正常 Gameplay 的非死亡移除入口。
    public bool DespawnUnit(
        in UnitDespawnRequest request);

    // 以下三个接口必须在调用栈内同步应用 LifeState。
    public bool RequestEnterDying(
        UnitUid unitUid,
        in UnitDyingContext context);

    public bool RequestRecoverFromDying(
        UnitUid unitUid,
        in DyingRecoveryContext context);

    public bool ConfirmUnitDeath(
        UnitUid unitUid,
        in UnitDeathContext context);

    public bool RequestImmediateRespawn(
        UnitUid unitUid,
        in UnitRespawnRequest request);

    // 只处理跨 Tick 生命周期节点。
    public void ProcessPendingLifecycle();
}
```

以下三个生命周期接口名称已经冻结，所有调用方必须统一使用，不再保留同义别名：

```text
所有 LifeState 写入集中在 UnitWorld。
外部系统只能提交请求或判决上下文。
UnitWorld 负责验证当前状态和允许的转换。
Dying / Alive / Dead 转换由同步接口立即完成。
DespawnUnit 不伪造 LifeState 转换，而是直接结束当前 UnitUid 生命周期。
PendingUnitLifecycleQueue 不保存本 Tick 的 Dying / Dead 正式写入，也不保存同步 Despawn。
```

`PendingUnitLifecycleQueue` 仅允许保存：

```text
死亡表现完成节点
对象池回收节点
Destroy / SpawnRuin 节点
英雄复活就绪节点
Respawning 完成节点
其它明确需要跨 Tick 等待的生命周期节点
```

---

### UnitSpawnRequest 与同步 SpawnUnit

`SpawnUnit` 保持同步接口：

```csharp
public UnitUid SpawnUnit(
    in UnitSpawnRequest request);
```

`UnitSpawnRequest` 只携带运行时变量：

```csharp
public readonly struct UnitSpawnRequest
{
    public readonly int UnitPrototypeId;
    public readonly TeamId TeamId;

    public readonly fp2 Position;
    public readonly fp2 Forward;

    public readonly UnitUid OwnerUid;
    public readonly UnitSpawnReason Reason;
}
```

不允许传入：

```text
SpawnLogicTick
RuntimeEntityPrefabId
UnitKind
UnitSubKindId
BaseStats
HandlerLoadout
DisposePolicy
RespawnConfig
PoolConfig
```

`SpawnLogicTick` 直接读取：

```csharp
int currentLogicTick =
    SimulationTickContext.Current.Tick;
```

同步生成流程：

```text
SpawnUnit
    ↓
读取 UnitPrototype
    ↓
解析 RuntimeEntityPrefabId
    ↓
通过公共 GlobalPrefabTable 取得 PrefabKind = Unit 的预制体
    ↓
UnitWorld 分配 byte SpawnSequenceInTick
    ↓
构造 UnitUid
    ↓
按 UnitPrototypeId Rent 或 Instantiate
    ↓
InitializeForNewRuntime
    ↓
通过 PhysicsEntity2D.SetLogicPose 初始化逻辑姿态
    ↓
注册 UnitRegistry 与 PhysicsEntity2D
    ↓
返回 UnitUid
```

`SpawnUnit` 返回前，单位已经同步生成并注册：

```csharp
UnitUid unitUid = unitWorld.SpawnUnit(request);

bool exists = unitWorld.TryGetUnit(
    unitUid,
    out Unit unit);
// 此处必须为 true。
```

不存在：

```text
SubmitSpawnRequest
PendingSpawnQueue
FlushSpawnRequests
待生成单位查询
```

`UnitSpawnRequest` 中的 Request 只表示生成输入，不代表异步任务。

---

### 帧内单位生成序号

`UnitWorld` 维护：

```csharp
private int _currentSequenceLogicTick;
private byte _nextSpawnSequenceInTick;
```

首次在新 Tick 生成单位时重置：

```text
_currentSequenceLogicTick = Current.Tick
_nextSpawnSequenceInTick = 0
_spawnSequenceExhausted = false
```

每次同步生成依次分配 `0..255`。  
当 `255` 已被分配后，将 `_spawnSequenceExhausted` 设为 `true`；本 Tick 再次申请序号时抛出确定性错误。

它是当前 `UnitWorld` 内所有单位共享的帧内序号空间，不按 Prototype 或 Prefab 分开计数。

当本 Tick 生成数量超过 256 个时：

```text
产生确定性错误。
禁止回绕。
禁止静默覆盖。
禁止改用本地非确定性补救。
```

---

### UnitRegistry

```csharp
public sealed class UnitRegistry
{
    public void Register(Unit unit);
    public void Unregister(Unit unit);

    public bool TryGet(
        UnitUid uid,
        out Unit unit);

    public IEnumerable<Unit> GetAll();
    public IEnumerable<Unit> GetByTeam(
        TeamId teamId);
    public IEnumerable<Unit> GetByKind(
        UnitKind kind);
    public IEnumerable<Unit> GetBySubKind(
        UnitKind kind,
        ushort unitSubKindId);
}
```

推荐索引：

```text
UnitUid -> Unit
TeamId -> Unit Set
UnitKind -> Unit Set
(UnitKind, UnitSubKindId) -> Unit Set
```

英雄处于 `Dead / Respawning` 时对象继续存在，可以保留在全量注册表；正常战斗查询必须根据 `LifeState` 和目标规则过滤。

进入对象池或被销毁前必须先 `Unregister`。

---

### AIController 分配、注册与唯一映射

`UnitWorld` 不根据 `UnitKind` 猜测单位应该使用哪一种 AI，也不负责决定 Lane、Wave、Camp、Formation、Leash 等玩法上下文。

具体管理方负责决定 AI 分配：

```text
MinionSystem
    保存自己管理的 Minion UnitUid
    决定 MinionAIProfile / Lane / Wave / Formation
    创建或取得已完成初始配置的 MinionAIController
    调用 UnitWorld.RegisterAIController

JungleCamp
    保存自己管理的 Monster UnitUid
    决定 MonsterAIProfile / Camp / CombatGroup / Leash
    创建或取得已完成初始配置的 MonsterAIController
    调用 UnitWorld.RegisterAIController

地图装配或建筑管理方
    保存自己管理的 Structure UnitUid
    决定 TowerAIProfile / Team / Targeting
    创建或取得已完成初始配置的 TowerAIController
    调用 UnitWorld.RegisterAIController
```

注册完成后，`UnitWorld` 成为下列关系的唯一维护者：

```text
UnitAIControllerRegistry
    UnitUid -> UnitAIController
```

小兵、野怪或建筑管理方不得再维护第二份：

```text
UnitUid -> UnitAIController
```

也不长期保存：

```text
Unit 引用
UnitAIController 引用
Controller 容器下标
依赖对象地址或 Unity InstanceId 的绑定关系
```

管理方只保存：

```text
自己管理的 UnitUid 集合
AIProfileId / AI 分配结果
Lane / Wave / Formation
Camp / CombatGroup / Leash
推进目标、回营目标或其它业务行为状态
```

需要访问 Controller 时，统一通过 `UnitWorld` 查询：

```csharp
public bool RegisterAIController(
    UnitUid ownerUnitUid,
    UnitAIController controller);

public bool UnregisterAIController(
    UnitUid ownerUnitUid);

public bool TryGetAIController(
    UnitUid ownerUnitUid,
    out UnitAIController controller);
```

注册职责：

```text
具体管理方：
    决定 Controller 类型与 AIProfile。
    创建或取得 Controller。
    完成首次业务配置。
    将 Controller 交给 UnitWorld 注册。
    注册后只保存 UnitUid 和自身业务状态。

UnitWorld：
    校验 Owner UnitUid 当前存在。
    校验该 UnitUid 尚未绑定其它 Controller。
    校验 Controller 的 Owner 与 UnitUid 一致。
    保存唯一 UnitUid -> UnitAIController 映射。
    按稳定顺序遍历和 Tick Controller。
    提供统一查询入口。
    在正式死亡、复活、回池和销毁时停用、恢复或注销。
    聚合 Controller 的回滚状态和注册关系。
```

`RegisterAIController` 成功后，Controller 的运行时归属关系由 `UnitWorld` 管理。管理方若需要改变行为，应：

```text
通过 UnitUid 查询 Controller 后调用正式接口
    或
向自己保存的 AI 分配 / 行为状态写入新结果，
由 Controller 在规定阶段读取
```

不得绕过 `UnitWorld` 持有并操作另一份长期 Controller 引用。

主动生效采用统一派生规则：

```csharp
bool canTickAI =
    SimulationTickContext.Current.Tick
    > owner.UnitUid.SpawnLogicTick;
```

因此：

```text
Unit 在 SpawnUnit 返回时已经存在并可查询。
AIController 可以在生成 Tick 内完成注册。
生成 Tick 内不执行主动 AI Tick。
从下一 LogicTick 开始 Tick。
```

不保存：

```text
FirstAITickLogicTick
FirstActiveLogicTick
仅用于表达生成 Tick 禁止主动行为的 EnabledForAITick
```

AIController 的 Tick 条件由以下状态共同推导：

```text
UnitUid -> UnitAIController 注册关系存在
Owner Unit 仍可查询
Owner LifeState 允许主动行为
CurrentTick > Owner.UnitUid.SpawnLogicTick
```

生命周期：

```text
非英雄 Unit 正式进入 Dead
    -> 对应管理模块注销或更新成员状态。
    -> UnitWorld 立即注销 UnitUid -> UnitAIController 映射。
    -> 实体仍可保留到死亡动画结束。

Hero 进入 Dead / Respawning
    -> 当前版本没有英雄 AI，不建立额外规则。
    -> 若未来存在英雄 AI，其保留或注销由英雄 AI 设计明确。

Unit 回到 Alive
    -> 只有仍存在合法 Controller 注册关系时才能恢复 AI Tick。

Unit 回池或销毁
    -> UnitWorld 确认不存在残留 UnitUid -> UnitAIController 映射。

池化对象以新 UnitUid 再次生成
    -> 对应管理方根据新 UnitUid 重新完成 AI 分配和注册。
```

回滚职责：

```text
UnitWorld
    保存和恢复 UnitUid -> UnitAIController 唯一映射。
    聚合 Controller 自身的权威运行状态。

MinionSystem / JungleCamp / 建筑管理方
    保存自己管理的 UnitUid 集合。
    保存 AIProfile 分配和玩法业务状态。
    不保存 Controller 引用或第二份映射。

Resolve
    UnitWorld 按 UnitUid 修复 Controller Owner 关系。
    管理方只修复自己管理的 UnitUid 与业务记录。
```

AI 是否能够 Tick 由注册关系、`LifeState` 和 `UnitUid.SpawnLogicTick` 推导，不进入 Controller 快照。

---

### UnitDisposePolicy 与 UnitRespawnConfig

对象处置和复活规则分离。

#### UnitDisposePolicy

```csharp
public enum UnitDisposePolicyType : byte
{
    KeepAliveObject,
    PoolAfterDeathPresentation,
    DestroyAfterDeathPresentation,
    DestroyAndSpawnRuinAfterDeathPresentation
}

[Serializable]
public sealed class UnitDisposePolicy
{
    public ushort Id;
    public UnitDisposePolicyType Type;

    [Min(0)]
    public int DeathPresentationTicks;

    public int RuinUnitPrototypeId;
}
```

它只回答：

```text
逻辑死亡后播放多久死亡表现。
表现结束后保留、回池、销毁还是生成废墟。
```

它不保存：

```text
击杀归属。
金币经验。
死亡是否被阻止。
正常英雄复活等待规则。
```

#### UnitRespawnConfig

```csharp
[Serializable]
public sealed class UnitRespawnConfig
{
    public bool CanRespawn;
    public int RespawnDelayTicks;

    public RespawnHealthRule HealthRule;
    public RespawnResourceRule ResourceRule;
}
```

它由 `UnitWorld` 使用，负责：

```text
Dead 后是否进入正常复活流程。
何时进入 Respawning。
复活时生命和资源如何初始化。
```

默认映射：

| 单位 | DisposePolicy | RespawnConfig |
|---|---|---|
| Hero | `KeepAliveObject` | `CanRespawn = true` |
| Minion | `PoolAfterDeathPresentation` | 不复活 |
| 普通 Monster | `PoolAfterDeathPresentation` | 不复活 |
| Epic Monster | `DestroyAfterDeathPresentation` | 不复活 |
| Tower | `DestroyAndSpawnRuinAfterDeathPresentation` | 不复活 |

运行时不通过 `UnitKind` 临时推导，最终以 `UnitPrototype` 配置为准。

---

### LifeState 写权限和转换接口

`UnitWorld` 内部统一应用状态：

```csharp
private void ApplyLifeState(
    Unit unit,
    LifeState targetState)
{
    ValidateTransition(
        unit.LifeState,
        targetState);

    unit.ApplyLifeStateFromUnitWorld(
        targetState);
}
```

允许的基础转换：

```text
Alive -> Dying
Dying -> Alive
Dying -> Dead
Dead -> Respawning
Respawning -> Alive
```

其它特殊转换必须通过明确的 `UnitWorld` API 和规则验证，不能由外部系统直接写字段。

外部系统接入示例：

```text
CombatSystem
    -> RequestEnterDying
    -> RequestRecoverFromDying
    -> ConfirmUnitDeath

GameFlowController
    -> RequestImmediateRespawn
    -> 修改或提供复活规则输入

地图脚本
    -> 需要非死亡清场时调用 DespawnUnit
    -> 其它特殊状态转换通过 UnitWorld 的专用生命周期接口请求
```

这些系统拥有接入权，但不拥有最终写权限。

其中：

```text
RequestEnterDying
RequestRecoverFromDying
ConfirmUnitDeath
```

都是同步接口。调用返回前，目标状态、对应 `UnitEventBus` 回调以及该状态下必须立即完成的 Handler 清理和非英雄关系注销已经完成。

它们不能只向 `PendingUnitLifecycleQueue` 写入请求后延迟返回。  
跨 Tick 队列只处理死亡表现、对象处置与正常复活等后续节点。

---



## 需求演进

### 2026-10-02

变动内容：生成 Tick 可被动参与，主动工作晚于出生 Tick。

legacyDecision：D-008

### 2026-10-02

变动内容：正式死亡由 UnitWorld 同步执行，来源系统仅清理自己的句柄。

legacyDecision：D-009


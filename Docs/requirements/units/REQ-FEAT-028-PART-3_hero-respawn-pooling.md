# 英雄复活与对象池新生命周期

## 本功能范围

本案细化“同步生成死亡复活与回池”中的英雄复活与对象池新生命周期，仅覆盖下列明确接口与边界。

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

### 英雄死亡与复活

英雄通常配置：

```text
DisposePolicy = KeepAliveObject
RespawnConfig.CanRespawn = true
```

流程：

```text
Dying
    ↓ UnitWorld 在 Combat 当前结算循环内接受死亡判决
Dead
    ↓ 播放死亡动画
    ↓ 保持动画最后一帧
    ↓ UnitWorld 等待 RespawnDelayTicks
Respawning
    ↓ 清理仅限复活前应结束的临时状态
    ↓ 恢复位置、生命和资源
    ↓ 播放复活表现
Alive
```

`Dead -> Respawning` 是 `UnitWorld` 的正常英雄生命周期，不由 `CombatSystem` 管理。

复活就绪 Tick：

```csharp
int respawnReadyLogicTick =
    deathLogicTick
    + prototype.RespawnConfig.RespawnDelayTicks;
```

死亡表现和复活等待可以重叠。  
进入 `Respawning` 的最早 Tick 不应早于必要的死亡表现节点。

进入 `Respawning` 时：

```text
LifeState = Respawning。
先清理 Intent、ActionRuntime、Reservation 和强制位移等临时行为状态。
再按 7.14 冻结顺序调用全部 Handler.ClearForRespawn。
再次执行幂等控制清理。
清理全部护盾及黑盾免疫，除非护盾规则明确允许跨复活保留。
清理不允许跨复活保留的 Buff。
跨死亡保留的 Ability / Buff / Equipment Runtime
    只重建自己明确标记为 LifeStageHandle 的当前生命阶段 Handle。
PersistentHandle 继续保留，不重复 Attach。
恢复复活位置。
按 RespawnConfig 恢复生命和资源。
重置移动执行状态。
仍不可接受普通主动行为。
```

进入 `Respawning` 时同样禁止全量调用：

```text
StatHandler.ClearModifiers()
Unit.CombatModifiers.Clear()
```

以下状态继续由其来源规则决定是否保留：

```text
技能等级与冷却
固定技能被动
装备实例、装备属性与常驻装备被动
英雄永久成长
永久 Buff
其它明确配置为跨死亡 / 复活保留的 Modifier
```

复活完成后：

```text
重建或刷新必要派生状态。
LifeState = Alive。
恢复正常目标、行为和空间参与。
```

英雄保留：

```text
同一个 Unit 对象。
同一个 UnitUid。
UnitPrototypeId。
TeamId。
UnitKind / UnitSubKindId。
Level / CurrentExperience，除非游戏规则明确修改。
仍然有效的 StatModifierHandle / CombatModifierHandle。
```

控制实例编号和 `StatSeq` 在英雄死亡和复活时都不重置，因为 `UnitUid` 未改变。

---

### 对象池单位的新生命周期

小兵、普通野怪和大部分召唤物通常配置：

```text
PoolAfterDeathPresentation
```

流程：

```text
Dead
    ↓ UnitDeath 回调
    ↓ 清理死亡状态
    ↓ 播放死亡动画
    ↓ 表现结束
    ↓ UnitRegistry.Unregister
    ↓ PhysicsWorld.Unregister
    ↓ UnitLocomotionAgent.Deactivate
    ↓ UnitHandler.ResetForPool
    ↓ UnitPoolRegistry.Return
```

回池时清理：

```text
UnitUid
TeamId
OwnerUid
LifeState
Intent
ActionRuntimeSet
ReservationState
Buff 实例
护盾实例和黑盾免疫句柄
控制实例、免疫和强制行为胜者
临时 Modifier
攻击与技能运行状态
物理注册状态和查询快照
移动路径与碰撞状态
表现层临时对象
```

不需要清理动态事件订阅，因为 `UnitEventBus` 没有动态订阅。

再次 `SpawnUnit` 时：

```text
视为新的运行时生命周期。
分配新的 UnitUid。
重置 StatHandler.StatSeq 为 1。
重置控制实例编号。
重新应用 UnitPrototype 配置。
重新绑定空间查询快照。
重新初始化 Level / CurrentExperience 的初始规则。
```

---

### 非死亡规则移除与回滚清场

单位可能因为非死亡规则结束当前运行时生命周期，例如：

```text
召唤物持续时间结束。
临时单位被拥有者主动解除。
地图脚本清场。
比赛阶段切换或房间重置。
回滚恢复时，当前世界中存在但目标快照中不存在该单位。
```

这些情况都不是死亡，不能伪装成：

```text
Alive -> Dying -> Dead
UnitDying
UnitDeath
UnitKill
死亡动画
金币、经验、KDA 或赏金结算
```

#### Gameplay 非死亡移除入口

单位框架统一使用 `Despawn` 表达正常 Gameplay 规则导致的非死亡移除：

```csharp
public enum UnitDespawnReason : byte
{
    SummonExpired,
    OwnerRemoved,
    ScriptedCleanup,
    MatchCleanup
}

public enum UnitDespawnMode : byte
{
    Pool,
    Destroy
}

public readonly struct UnitDespawnRequest
{
    public readonly UnitUid UnitUid;
    public readonly UnitDespawnReason Reason;
    public readonly UnitDespawnMode Mode;
}

public bool DespawnUnit(
    in UnitDespawnRequest request);
```

`DespawnUnit` 是同步入口。返回 `true` 时，目标 `UnitUid` 已经结束运行时生命周期，并且：

```text
UnitRegistry 已注销。
PhysicsWorld 已注销。
UnitUid -> UnitAIController 映射已注销。
非英雄管理方已更新或注销受管关系。
TryGetUnit(UnitUid) 返回 false。
该 UnitUid 不再接受新的 Order、战斗请求、Buff、控制或事件。
```

`DespawnUnit` 的固定顺序：

```text
1. 验证 UnitUid 当前存在，且尚未进入其它最终处置流程。

2. 立即停止主动行为、Intent、ActionRuntime、Reservation、
   普通移动和强制位移。

3. 按固定顺序调用 Handler.ClearForDespawn(reason)。
   每个 Handler 结束自己拥有的 Runtime、句柄和外部关系。

4. 清理护盾、控制、临时技能、Buff、装备运行时和其它单位状态。

5. 因为当前 UnitUid 生命周期正式结束，
   允许执行 StatHandler.ClearModifiers()
   和 Unit.CombatModifiers.Clear() 作为完整兜底清理。

6. 通知对应非英雄管理方按“非死亡移除”更新关系。
   该通知不能被记录成死亡、击杀或营地战斗死亡。

7. UnitWorld 注销 UnitUid -> UnitAIController 唯一映射。

8. 注销 PhysicsEntity2D 和 UnitRegistry。

9. 根据 UnitDespawnMode 立即回池或 Destroy。
```

`DespawnUnit` 不读取 `UnitDisposePolicy.DeathPresentationTicks`，也不播放死亡动画。`Pool` 模式必须验证目标 Prototype 允许对象池复用；不允许池化时必须使用 `Destroy`。需要消失、传送或解散表现时，先由表现层创建纯表现对象，再结束 Gameplay Unit；表现不得延迟该 `UnitUid` 的逻辑注销。

召唤物到期示例：

```text
Summon Runtime 到达 EndLogicTick
    ↓
UnitWorld.DespawnUnit(
    UnitUid,
    SummonExpired,
    Pool / Destroy
)
    ↓
无 UnitDying / UnitDeath / UnitKill
    ↓
当前 UnitUid 立即失效
```

#### 回滚拓扑重建入口

回滚恢复不是 Gameplay 规则，不能调用公开的 `DespawnUnit`，也不能触发 Handler 的普通业务清理。`UnitWorld.Restore` 在恢复单位拓扑时使用内部静默入口：

```csharp
private void RemoveUnitForRollbackRestore(
    UnitUid unitUid);
```

它只处理“当前世界存在、目标快照不存在”的多余单位：

```text
不修改 LifeState。
不发布 UnitEventBus。
不调用 ClearForDeath / ClearForRespawn / ClearForDespawn。
不通知 Gameplay 非英雄死亡或移除规则。
不播放任何表现。
不提交 CombatRequest。
```

静默移除顺序：

```text
注销 UnitUid -> UnitAIController 映射。
注销 Physics 和 UnitRegistry。
直接清理当前 Unit 的本地运行时容器。
执行无副作用的 ResetForPool 或等价静默重置。
将对象返回对应 Prototype 的对象池，或按恢复实现销毁。
```

同时：

```text
目标快照中仍存在的单位
    -> 直接 Restore，不先执行正常生命周期清理。

目标快照中存在但当前世界缺失的单位
    -> 由 UnitWorld 按快照身份创建运行时载体，
       再 Restore / Resolve / Rebuild。

MinionSystem / JungleCamp 等管理方
    -> 直接恢复自己的 UnitUid 集合和业务状态，
       不接收回滚期间的 Gameplay 注销通知。
```

因此，非死亡清场分为两个明确入口：

| 场景 | 入口 | 是否 Gameplay | 是否发布事件 |
|---|---|---:|---:|
| 召唤物到期、脚本清场 | `DespawnUnit` | 是 | 否 |
| 回滚拓扑重建 | `RemoveUnitForRollbackRestore` | 否 | 否 |
| 正式死亡 | `ConfirmUnitDeath` | 是 | `UnitDeath` |

三者不能互相替代。

---

### 多单位对象池

对象池按：

```text
UnitPrototypeId
```

分池，而不是按：

```text
UnitKind
UnitSubKindId
RuntimeEntityPrefabId
```

原因：

```text
同一 Prefab 可能承载不同 Gameplay Prototype。
不同 Prototype 的 Handler 配置、基础数值和重置边界可能不同。
对象池复用必须严格匹配单位原型。
```

推荐结构：

```csharp
public sealed class UnitPoolRegistry
{
    private readonly Dictionary<
        int,
        UnitPool
    > _pools;

    public bool TryRent(
        int unitPrototypeId,
        out Unit unit);

    public Unit RentOrCreate(
        int unitPrototypeId);

    public void Return(
        Unit unit);
}
```

池为空时是否扩容、最大池容量和预热数量来自 `UnitPoolConfig`。

波次生成可以批量调用同步 `SpawnUnit`。  
UnitWorld 依照调用顺序分配帧内 `byte` 序号，调用方必须提供稳定顺序。

---



## 需求演进

### 2026-10-02

变动内容：生成 Tick 可被动参与，主动工作晚于出生 Tick。

legacyDecision：D-008

### 2026-10-02

变动内容：正式死亡由 UnitWorld 同步执行，来源系统仅清理自己的句柄。

legacyDecision：D-009


# 正式死亡清理与处置

## 本功能范围

本案细化“同步生成死亡复活与回池”中的正式死亡清理与处置，仅覆盖下列明确接口与边界。

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

### 致死、死亡回调与死亡清理

`Alive -> Dying -> Alive / Dead` 必须在 `CombatSystem` 当前 Tick 的 Combat Settlement Cycle 内同步完成：

```text
CombatSystem 发现致死条件
    ↓
同步调用 UnitWorld.RequestEnterDying
    ↓
UnitWorld: Alive -> Dying
    ↓
Victim.EventBus.Publish(UnitDying)
    ↓
UnitDying Reaction 提交的新 CombatRequest
继续进入当前 Combat Settlement Cycle
    ↓
CombatSystem 继续死亡阻止与正式死亡结算
    ├── 死亡被阻止
    │       ↓
    │   同步调用 UnitWorld.RequestRecoverFromDying
    │       ↓
    │   Dying -> Alive
    │
    └── 确认正式死亡
            ↓
        同步调用 UnitWorld.ConfirmUnitDeath
            ↓
        Dying -> Dead
            ↓
        Victim.EventBus.Publish(UnitDeath)
            ↓
        UnitDeath Reaction 提交的新 CombatRequest
        继续进入当前 Combat Settlement Cycle
            ↓
        清理各来源自身不应跨死亡保留的临时状态
            ↓
        注销非英雄管理关系与 AIController
            ↓
        启动死亡表现
```

核心实现顺序：

```csharp
public bool ConfirmUnitDeath(
    UnitUid unitUid,
    in UnitDeathContext context)
{
    if (!_registry.TryGet(unitUid, out Unit unit))
        return false;

    if (unit.LifeState != LifeState.Dying)
        return false;

    ApplyLifeState(unit, LifeState.Dead);

    // 必须先回调，后清理。
    unit.EventBus.Publish(
        new UnitDeathEvent(
            context.KillerUnitUid,
            context.Reason,
            unit.PhysicsEntity.GetLogicPosition()
        )
    );

    ClearNonPersistentStateForDeath(
        unit,
        context
    );

    if (unit.UnitKind != UnitKind.Hero)
    {
        NotifyNonHeroManagerFormalDeath(
            unit.UnitUid
        );

        UnregisterAIController(
            unit.UnitUid
        );
    }

    BeginDeathPresentation(
        unit,
        context
    );

    return true;
}
```

`NotifyNonHeroManagerFormalDeath` 是单位框架对非英雄管理模块的生命周期接缝，具体接口由对应系统定义，例如：

```text
MinionSystem.UnregisterManagedUnit(UnitUid)

JungleCamp.OnMemberDeath(UnitUid)
    或按 Member Slot 更新死亡状态

其它非英雄管理模块
    注销或更新自己的管理关系
```

死亡回调完成后，各 Handler 只清理自己负责且不应跨死亡保留的状态：

```text
ActionRuntimeSet
    终止当前行为和 Reservation。

AttackHandler
    清理当前攻击过程和临时攻击状态。

AbilityHandler
    中断当前施法 / 引导。
    保留技能等级、冷却、固定被动和明确允许跨死亡保留的 Runtime。

BuffHandler
    清理不跨死亡保留的 Buff。
    被清理 BuffRuntime 使用自己保存的 Handle
    移除自己挂载的 StatModifier / CombatModifier。
    永久或明确跨死亡保留的 Buff 继续存在。

CrowdControlHandler
    清理控制实例、控制免疫和强制行为胜者。
    控制来源移除自己挂载的 Modifier。

EquipmentHandler
    通常保留装备实例、装备属性、常驻被动和对应 Handle。

StatHandler / CombatModifierSet
    不主动判断来源是否应保留。
    不在普通死亡流程中全量清空。
```

普通死亡明确禁止：

```text
StatHandler.ClearModifiers()
Unit.CombatModifiers.Clear()
```

因为它们会错误删除：

```text
技能等级提供的固定属性
装备属性与常驻装备被动
英雄永久被动
永久或跨死亡保留 Buff
其它明确允许跨死亡保留的 Modifier
```

每个 Modifier 来源只移除自己应结束的 Handle。  
`StatHandler` 和 `CombatModifierSet` 不实时扫描 Buff、技能或装备来源是否合法。

`UnitDeath` 回调必须先于这些清理，保证死亡 Reaction 可以读取必要的死亡前 Runtime、Modifier、护盾、技能和装备状态。

CombatSystem 完成击杀归属后：

```text
保存自身的逻辑击杀结果
    ↓
Killer.EventBus.Publish(UnitKill)
```

`UnitKill` Reaction 产生的新 CombatRequest 同样可以继续进入当前 Tick 的 Combat Settlement Cycle。  
`UnitKill` 是否成为权威击杀不由 `UnitWorld` 决定。

---

### 死亡表现与最终处置

进入 `Dead` 后：

```csharp
private void BeginDeathPresentation(
    Unit unit,
    in UnitDeathContext context)
{
    UnitPrototype prototype =
        _prototypeDatabase.Get(
            unit.UnitPrototypeId);

    UnitDisposePolicy policy =
        _disposePolicyDatabase.Get(
            prototype.UnitDisposePolicyId);

    PlayDeathAnimation(unit);

    int endLogicTick =
        SimulationTickContext.Current.Tick
        + policy.DeathPresentationTicks;

    _pendingLifecycleQueue.Enqueue(
        UnitLifecycleItem.DeathPresentationEnd(
            unit.UnitUid,
            endLogicTick,
            policy.Id
        )
    );

    if (policy.Type ==
        UnitDisposePolicyType.KeepAliveObject)
    {
        ScheduleNormalRespawnIfEnabled(
            unit,
            prototype.RespawnConfig
        );
    }
}
```

死亡动画结束不发布额外单位事件。

表现完成后：

```text
KeepAliveObject
    保持 Dead 对象，等待 UnitWorld 正常复活节点。

PoolAfterDeathPresentation
    反注册并回收到 UnitPoolRegistry。

DestroyAfterDeathPresentation
    反注册并销毁 GameObject。

DestroyAndSpawnRuinAfterDeathPresentation
    反注册并销毁本体，
    再通过正常 UnitWorld.SpawnUnit 生成 TowerRuin。
```

TowerRuin 是独立 `UnitPrototype`，不是 Tower 的模型状态。

---



## 需求演进

### 2026-10-02

变动内容：生成 Tick 可被动参与，主动工作晚于出生 Tick。

legacyDecision：D-008

### 2026-10-02

变动内容：正式死亡由 UnitWorld 同步执行，来源系统仅清理自己的句柄。

legacyDecision：D-009


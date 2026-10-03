# 投射物运动命中与寿命顺序

## 本功能范围

本案细化“提交运动寿命与回收”中的投射物运动命中与寿命顺序，仅覆盖下列明确接口与边界。

## 目标实现

投射物在固定 Tick 阶段提交、移动、命中和回收。

## 技术方案

ProjectileWorld 拥有每 Tick spawn sequence；CommitSpawns 是唯一创建入口，随后 AdvanceMotion、UpdateLifecycle、ResolveHits、EmitEffects、FlushDestroy。

## 边界情况

FrameSync 不维护第二个投射物 BeginTick 序列；Pending 与 Active 状态均可恢复；池化实体和逻辑实例各有释放 owner。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。

### 固定 Tick 边界

投掷物 Tick 必须拆分为以下固定阶段：

```text
ProjectileWorld.CommitSpawns()
ProjectileWorld.AdvanceMotion()
ProjectileWorld.UpdateLifecycle()

PhysicsWorld.BuildUnitFinalGrid()
UnitCollisionEventBuffer.DetectEnterExit()

ProjectileWorld.ResolveHits()
ProjectileWorld.EmitEffects()
ProjectileWorld.FlushDestroy()
```

不再存在外部投掷物 Seq 起始重置入口，也不允许：

```text
在 CommitSpawns 开头重置 Seq
在 FlushDestroy 结尾重置 Seq
由 FrameSync Pipeline 主动重置投掷物 Seq
```

原因是投掷物请求可能来自同一 Tick 的多个阶段：

```text
CommitSpawns 之前：
    Ability
    Attack
    Buff
    其它单位子系统

CommitSpawns 之后：
    HitModule
    EndModule
    其它后续 Gameplay 反应
```

任何单独的投掷物阶段函数都无法安全包住本 Tick 的全部 `RequestSpawn`。  
因此 Seq 只在 `RequestSpawn` 内根据 `SimulationTickContext.Current.Tick` 懒重置。

全局推荐顺序：

```text
1. 设置 SimulationTickContext.Current。
2. 单位行为、技能、普攻、Buff 等系统提交本 Tick 的生成请求。
3. 单位移动与空间修正完成。
4. ProjectileWorld.CommitSpawns。
5. ProjectileWorld.AdvanceMotion。
6. ProjectileWorld.UpdateLifecycle。
7. PhysicsWorld.BuildUnitFinalGrid。
8. UnitCollisionEventBuffer.DetectEnterExit。
9. ProjectileWorld.ResolveHits。
10. ProjectileWorld.EmitEffects。
11. ProjectileWorld.FlushDestroy。
12. Gameplay 逻辑结束后，由激活实体各自的 PhysicsEntity2D.LateUpdate 写入实体根 Unity Transform。
13. Tick 末保存 GameplaySnapshot，SnapshotTick = 当前 Tick + 1。
```

`SimulationTickContext` 不作为这些接口的参数。  
各函数需要 Tick、DeltaTick 或执行模式时，统一在内部读取：

```text
SimulationTickContext.Current.Tick
SimulationTickContext.Current.DeltaTick
SimulationTickContext.Current.ExecutionMode
```

禁止 `ProjectileWorld` 自行保存第二套当前 Tick。

`SpawnSequenceTick` 仅是 Seq 计数器所对应的 Tick 标签，不用于驱动模拟，也不能替代 `SimulationTickContext.Current.Tick`。

`PhysicsWorld.BuildUnitFinalGrid()` 由顶层 Gameplay Tick 调度，不由 `ProjectileWorld` 内部调用。

本 Tick 在 `CommitSpawns` 之后产生的新请求保持 `Pending`，在下一逻辑 Tick 的 `CommitSpawns` 中创建，但其 `ProjectileUid.SpawnLogicTick` 仍是请求被接受时的 Tick。

### `AdvanceMotion`

该阶段只处理：

```text
运动
朝向
跟踪
距离累计
运行时 Shape 变化
Bounds 更新
```

调用：

```text
ProjectileDef.MotionModules
```

不允许：

```text
命中查询
执行伤害
直接回收投掷物
构建 UnitFinalGrid
写 Unity Transform
```

完全不运动的投掷物可以没有 MotionModules。  
静止不是一种必须额外实现的“运动类型”。

---

### `UpdateLifecycle`

该阶段处理：

```text
AgeTicks
RemainingTicks
目标有效性
最大飞行距离
生命周期阶段
结束条件
```

调用：

```text
ProjectileDef.LifecycleModules
```

模块可以调用：

```text
RequestEnd(reason)
```

但不能当场从 ActiveProjectiles 删除对象。

---

### `ResolveHits`：唯一命中入口

`ResolveHits` 是整个系统唯一允许调用 `ProjectileHitQueryService` 的阶段。

固定流程：

```text
1. 按稳定顺序遍历 ActiveProjectiles。
2. 跳过 PendingEnd 且规则不允许继续命中的投掷物。
3. 判断本 Tick 是否达到命中查询间隔。
4. 调 ProjectileHitQueryService 查询候选。
5. 应用 ProjectileTargetFilter。
6. 应用 HitMemory 和 HitPolicy。
7. 生成稳定排序后的 ProjectileHitResult。
8. 写入 PendingHitBuffer。
```

禁止：

```text
MotionModule 调命中查询
LifecycleModule 调命中查询
HitModule 再次调命中查询
帧同步系统重复调用命中检测
表现层触发 Gameplay 命中
```

这样可确保一个投掷物在同一逻辑 Tick 中不会因多个入口重复命中。

---

### `EmitEffects`

该阶段消费 `PendingHitBuffer`。

对每个已确认命中：

```text
1. 更新 ProjectileHitMemory。
2. 更新总命中数、穿透计数或弹跳计数。
3. 依次执行 Def.HitModules。
4. 根据 HitPolicy 请求结束、继续穿透或生成弹跳目标。
5. 新投掷物统一调用 RequestSpawn，获得预分配 ProjectileUid。
```

HitModules 可以提交：

```text
DamageRequest
HealRequest
ShieldRequest
Buff Apply Request
Control Request
ProjectileSpawnRequest
Projectile End Request
```

HitModules 不直接修改：

```text
Unit 当前生命
Unit 逻辑位置
PhysicsWorld 网格
ActiveProjectiles 集合
```

由于本 Tick 的 `CommitSpawns` 已经完成，HitModules 新提交的投掷物保持 `Pending`，从下一逻辑 Tick 开始参与投掷物阶段。

---

### `FlushDestroy`

该阶段统一处理已请求结束的投掷物：

```text
1. 按稳定 Uid 顺序整理 PendingEnds。
2. 对每个投掷物执行 EndModules 一次。
3. EndModules 产生的新投掷物统一调用 RequestSpawn。
4. 从 PhysicsWorld 反注册 Entity。
5. 从 ActiveRegistry 移除 Uid。
6. 从 ActiveProjectiles 移除。
7. 清理 Entity 查询快照与运行时空间状态。
8. 清理 Projectile 运行时状态。
9. 释放 Projectile 到 LogicPool。
10. 关闭 Entity 所在 GO。
11. 释放 Entity 到 PrefabId 对应的对象池。
```

销毁后：

```text
ProjectileUid 不会复用。
ActiveRegistry 不再包含该 Uid。
PendingSpawnByUid 也不包含该 Uid。
GetState(uid) 返回 Missing。
```

不保留：

```text
EndLogicTick
EndReason 历史记录
Destroyed Tombstone
```

如某个系统确实需要记录投掷物结束结果，应由该系统在结束事件或效果请求中保存自己的业务结果，而不是把完整历史查询职责压给 `ProjectileWorld`。

---



## 需求演进

### 2026-10-02

变动内容：投射物保存 pending/active 状态，生成序列由 ProjectileWorld 拥有。

legacyDecision：D-012


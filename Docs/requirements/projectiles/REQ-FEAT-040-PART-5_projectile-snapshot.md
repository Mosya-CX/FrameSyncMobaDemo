# 投射物快照恢复与稳定遍历

## 本功能范围

本案细化“提交运动寿命与回收”中的投射物快照恢复与稳定遍历，仅覆盖下列明确接口与边界。

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

### 字段标记

| 标记 | 含义 |
|---|---|
| `Snapshot` | 会影响后续逻辑 Tick，通常需要保存 |
| `Static` | 来自确定性只读配置，通过稳定 ID 重新解析 |
| `Rebuildable` | 恢复后可根据其他状态确定性重建 |
| `Transient` | 只在当前阶段存在，快照点前必须消费或清理 |

---

### 统一回滚接口与聚合入口

`ProjectileWorld` 作为投掷物快照聚合根，实现：

```csharp
public sealed class ProjectileWorld
    : IRollback<ProjectileWorldSnapshot>
{
    public void Capture(ref ProjectileWorldSnapshot state);
    public void Restore(in ProjectileWorldSnapshot state);
    public void Resolve(in RollbackContext context);
    public void Rebuild(in RollbackContext context);
}
```

聚合关系：

```text
GameplaySnapshot
    ProjectileWorldSnapshot
        PendingSpawnRecordSnapshot[]
        ProjectileSnapshot[]
```

职责：

| 阶段 | `ProjectileWorld` 负责 |
|---|---|
| `Capture` | 捕获待生成请求和活跃投掷物的真实运行状态 |
| `Restore` | 恢复本系统稳定对象和自身状态，不解析跨系统引用 |
| `Resolve` | 通过稳定 UID 修复 Owner、Target、Source 等跨系统引用 |
| `Rebuild` | 重建本系统 Registry、Pending Index 和稳定排序索引 |

顶层帧同步协调器只调用这四个聚合根接口，不直接访问：

```text
Projectile.HitMemory
Projectile.ModuleStates
SpawnBoard 内部槽位
PhysicsEntity2D 内部字段
```

物理派生数据和空间索引由 `PhysicsWorld.Rebuild` 负责，不能由 `ProjectileWorld` 重复重建。

### `ProjectileWorldSnapshot`

```text
ProjectileWorldSnapshot
    PendingSpawnRecordSnapshot[] PendingSpawns
    ProjectileSnapshot[] ActiveProjectiles

    ProjectileRegistryRuntimeState 可重建
```

字段规则：

| 字段 | 标记 | 说明 |
|---|---|---|
| `PendingSpawns` | `Snapshot` | UID 已经分配，下一次 `CommitSpawns` 仍需创建 |
| `ActiveProjectiles` | `Snapshot` | 当前所有活跃投掷物 |
| `PendingSpawnByUid` | `Rebuildable` | 从 `PendingSpawns` 重建 |
| `ActiveRegistry` | `Rebuildable` | 从恢复后的活跃 ProjectileUid 重建 |
| `ActiveProjectiles 排序索引` | `Rebuildable` | 按 Uid 重建 |
| `SpawnSequenceTick / NextSpawnSequenceInTick / SpawnSequenceExhausted` | `Transient` | 仅用于 RequestSpawn 内部懒重置和分配，不进入 Tick 末快照 |

本版不保存：

```text
DestroyedRecords
历史 ProjectileUid 墓碑
已销毁投掷物结束原因
```

恢复后，既不在 `PendingSpawns` 也不在 `ActiveProjectiles` 中的 UID 统一查询为 `Missing`。

#### `PendingSpawnRecordSnapshot`

```text
PendingSpawnRecordSnapshot
    ProjectileUid Uid
    int ProjectileDefId

    UnitUid OwnerUnitUid
    TeamId TeamSnapshot
    ProjectileSourceDescriptor Source

    SpawnBoardRuntimeSnapshot Board
```

`Uid` 已包含请求 Tick 与该 Tick 内的稳定生成 Seq。  
这些数据已经跨过 `RequestSpawn`，并会影响下一次 `CommitSpawns`，因此必须由 `ProjectileWorldSnapshot` 聚合。

---

### `ProjectileSnapshot`

推荐字段：

```text
ProjectileSnapshot
    ProjectileUid Uid
    int ProjectileDefId

    UnitUid OwnerUnitUid
    TeamId Team
    ProjectileSourceDescriptor Source

    PhysicsEntityState PhysicsState
    ProjectileRuntimeSnapshot Runtime
    SpawnBoardRuntimeSnapshot Board
    ProjectileHitMemorySnapshot HitMemory
    ProjectileModuleStateSnapshot ModuleStates
```

#### 身份与来源

| 字段 | 标记 | 说明 |
|---|---|---|
| `Uid` | `Snapshot` | 运行时身份 |
| `ProjectileDefId` | `Snapshot` | 恢复静态 Def |
| `Def` 引用 | `Static` | 通过 DefId 解析 |
| `Def.PrefabId` | `Static` | 通过 Def 解析并从 `GlobalPrefabTable` 取得预制体 |
| `OwnerUnitUid` | `Snapshot` | 归属单位 |
| `Owner` 引用 | `Rebuildable` | 在 `Resolve` 中通过 UnitRegistry 解析 |
| `Team` | `Snapshot` | 投掷物业务阵营 |
| `Source` | `Snapshot` | 后续伤害和规则溯源 |

#### 物理状态

```text
PhysicsEntityState
```

由物理与范围查询系统唯一正式定义。  
投掷物设计案不再重复列出其内部 Position、PreviousPosition、Forward、Shape 或 Bounds 字段。

边界：

```text
ProjectileWorld
    负责把每个投掷物对应的 PhysicsEntityState
    聚合进 ProjectileSnapshot。

Physics System
    负责定义 PhysicsEntityState 的字段、
    Capture / Restore 语义和派生数据重建规则。
```

这样保持：

```text
快照聚合归 ProjectileWorld。
物理状态契约归 Physics System。
```

`PhysicsEntity2D` 运行时引用仍为 `Rebuildable`：根据 `ProjectileDef.PrefabId` 取得预制体实例并重新绑定。

#### 公共运行状态

| 字段 | 标记 |
|---|---|
| `LifeState` | `Snapshot` |
| `AgeTicks` | `Snapshot` |
| `RemainingTicks` | `Snapshot` |
| `Speed` | `Snapshot` |
| `TravelDistance` | `Snapshot` |
| `TargetUnitUid` | `Snapshot` |
| `TargetPoint` | `Snapshot` |
| `TargetDirection` | `Snapshot` |
| `TotalHitCount` | `Snapshot` |
| `RemainingPierceCount` | `Snapshot` |
| `RemainingBounceCount` | `Snapshot` |
| `EndRequested` | `Snapshot` |
| `EndReason` | `Snapshot` |

#### Board

| 内容 | 标记 |
|---|---|
| `InitOnly` 槽位 | `Transient` |
| 后续 Tick 会读取的 `RuntimeRead` 槽位 | `Snapshot` |
| Schema | `Static`，通过 Def 解析 |

#### 命中记忆

| 字段 | 标记 |
|---|---|
| `TotalHitCount` | `Snapshot` |
| `TargetUid` | `Snapshot` |
| 每目标 `HitCount` | `Snapshot` |
| 每目标 `LastHitLogicTick` | `Snapshot` |

#### 模块运行状态

模块状态只保存当前定义真实存在的字段，例如：

```text
曲线运动阶段
下次查询 Tick
跟踪丢失计时
当前弹跳目标
区域扩张 Tick
分段运动索引
```

静态模块配置不保存，通过：

```text
ProjectileDefId + ModuleSlotIndex
```

重新解析。

### 不进入快照的数据

```text
UnitFinalGrid
RvoGrid
PhysicsWorld 空间桶
Physics 派生 Bounds 与查询缓存
PendingSpawnByUid
ActiveRegistry
ActiveProjectiles 排序索引
Unity GameObject 激活列表
Unity Transform
对象池空闲栈
PendingHitBuffer
命中查询候选缓冲
临时去重集合
ProjectileLookupState 缓存
DestroyedRecords
SpawnSequenceTick
NextSpawnSequenceInTick
SpawnSequenceExhausted
当前函数局部变量
表现事件播放状态
```

恢复阶段：

```text
Restore
    恢复 PendingSpawns。
    恢复 Projectile 自身状态。
    取得并恢复对应 PhysicsEntityState。
    暂不解析跨系统运行时引用。

Resolve
    OwnerUnitUid -> Unit。
    TargetUnitUid -> Unit。
    Source 中稳定 UID -> 对应业务对象。
    ProjectileUid 引用 -> Projectile。

Rebuild
    ProjectileWorld 重建 PendingSpawnByUid、ActiveRegistry 和稳定排序索引。
    把 SpawnSequenceTick 设为 InvalidLogicTick，
    并清空 NextSpawnSequenceInTick 与 SpawnSequenceExhausted。
    PhysicsWorld 重建物理注册、派生 Bounds、RvoGrid 和 UnitFinalGrid。
    `PhysicsEntity2D.LateUpdate` 在 Gameplay 恢复完成后的下一次 Unity LateUpdate 中重新写实体根 Unity Transform。
```

`ProjectileWorld.Rebuild` 不重建物理系统派生数据。

### 快照点要求

推荐 Gameplay 快照只在完整逻辑 Tick 结束后保存：

```text
当前 Tick 的 CommitSpawns 已完成
ResolveHits 已完成
EmitEffects 已完成
FlushDestroy 已完成
本 Tick 新产生的 RequestSpawn 已进入 PendingSpawns
SnapshotTick = 当前 Tick + 1
```

因此：

```text
PendingSpawns
    会跨到下一 Tick，必须 Snapshot。

PendingHitBuffer
PendingEndBuffer
阶段局部候选列表
SpawnSequenceTick
NextSpawnSequenceInTick
SpawnSequenceExhausted
    不属于 Tick 末持久状态，保持 Transient。
```

销毁历史不进入快照。  
一个已销毁投掷物在恢复后的时间线中如果不再存在，也只会查询为 `Missing`。

如果未来允许在投掷物阶段中间保存快照，则必须重新审查这些缓冲和当前阶段游标，本设计第一版不支持该复杂模式。

---

### 统一 Tick 上下文与禁止项

投掷物系统统一使用：

```text
SimulationTickContext.Current.Tick
SimulationTickContext.Current.DeltaTick
SimulationTickContext.Current.ExecutionMode
```

`SimulationTickContext` 作为当前模拟 Tick 的全局只读上下文使用，不作为 `ProjectileWorld` 或模块接口参数层层传递。

禁止新增：

```text
ProjectileWorld.CurrentTick
GameplayClock.CurrentLogicTick
LogicClock
GlobalCurrentFrame
```

`ExecutionMode` 不得改变确定性 Gameplay 结果。相同配置、输入和快照状态在 `ServerAuthority / ClientPrediction / ClientReplay` 下必须得到相同的投掷物位置、UID、命中和结束状态。

Gameplay Tick 还禁止：

```text
float 参与投掷物逻辑
Time.deltaTime
Unity Physics
Transform.position 作为逻辑输入
Mathf
Vector3.normalized 参与确定性计算
运行时随机 GUID
Unity InstanceId 作为身份
无稳定顺序的 Dictionary 遍历结果
```

运行时逻辑使用：

```text
fp
fp2
整数 LogicTick
稳定配置 ID
ProjectileUid
UnitUid
```

---

### 稳定遍历顺序

以下操作必须稳定：

```text
ActiveProjectiles 遍历
PendingHitBuffer 消费
PendingEnds 回收
PendingSpawns 提交与 CommitSpawns
弹跳目标选择
同 Tick Uid 序号分配
```

第一版推荐：

```text
ProjectileUid 升序
```

`PendingSpawns` 的提交顺序直接使用：

```text
ProjectileUid.SpawnLogicTick
ProjectileUid.SpawnSequenceInTick
```

不再额外保存 `StableSubmitSequence`。任何遍历都不能依赖线程竞争顺序或容器内部枚举顺序。

---

### 分配控制

高频 Tick 中避免：

```text
每投掷物每 Tick new List
LINQ
闭包
装箱接口调用
临时 Dictionary
字符串 Key
```

推荐：

```text
World 级复用候选缓冲
World 级复用 PendingHitBuffer
Projectile 内部复用 HitMemory
固定 ModuleState 槽位
Unity ObjectPool
预热常用 PrefabId 的实体池
```

这里的复用缓冲是内部容器，不需要再套一层 `ObjectPool<List<Unit>>`。

---

### `PhysicsEntity2D.LateUpdate` Transform 写入边界

当前项目正式冻结：

```text
PhysicsEntity2D.LateUpdate
    是实体根 Unity Transform 的唯一最终写入点。
```

完整链路：

```text
ProjectileWorld.AdvanceMotion
    调用 PhysicsEntity2D 正式逻辑接口
    只修改确定性逻辑姿态

ProjectileWorld.ResolveHits
    通过物理查询服务读取确定性空间结果

ProjectileWorld.FlushDestroy
    反注册并回收已结束 Entity

Unity LateUpdate
    -> 激活并已绑定的 PhysicsEntity2D.LateUpdate
    -> 把最终确定性逻辑姿态写入实体根 Unity Transform
```

约束：

```text
ProjectileWorld 不直接写 Unity Transform。
MotionModule 不直接写 Unity Transform。
其它 Gameplay 组件不得重复写实体根 Transform。
PhysicsEntity2D.LateUpdate 只做逻辑姿态到 Transform 的单向输出。
不得从 Unity Transform 反向读取并覆盖 Gameplay 逻辑姿态。
池中未激活或未绑定的 PhysicsEntity2D 不执行有效同步。
```

回滚恢复后：

```text
Restore / Resolve / Rebuild
    恢复确定性逻辑姿态

下一次 PhysicsEntity2D.LateUpdate
    把恢复后的最终姿态写入 Unity Transform
```

客户端重演不要求每个重演 LogicTick 都执行 Unity `LateUpdate`，因为 Unity Transform 不参与 Gameplay 计算。



## 需求演进

### 2026-10-02

变动内容：投射物保存 pending/active 状态，生成序列由 ProjectileWorld 拥有。

legacyDecision：D-012


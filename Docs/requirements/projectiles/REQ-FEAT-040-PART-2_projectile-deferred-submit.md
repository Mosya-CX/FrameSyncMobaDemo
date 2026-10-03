# 投射物延迟提交与查询

## 本功能范围

本案细化“提交运动寿命与回收”中的投射物延迟提交与查询，仅覆盖下列明确接口与边界。

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

### Spawn 输入

```text
ProjectileSpawnRequest
    int ProjectileDefId
    UnitUid OwnerUnitUid
    ProjectileSourceDescriptor Source
    SpawnBoardInput Input
```

说明：

| 字段 | 说明 |
|---|---|
| `ProjectileDefId` | 选择投掷物逻辑定义 |
| `OwnerUnitUid` | 归属单位 |
| `Source` | 来源类型、来源配置 ID、会话或段数等稳定信息 |
| `Input` | 用于构建 SpawnBoard 的外部稳定参数 |

第一版不接受：

```text
World Owner
Parent Projectile
Random Seed
Unity Transform
任意 object Params
```

---

### `RequestSpawn`：延迟生成请求

统一入口：

```text
ProjectileUid ProjectileWorld.RequestSpawn(
    ProjectileSpawnRequest request
)
```

接口不传递 `SimulationTickContext`。  
`RequestSpawn` 在函数内部统一读取：

```text
SimulationTickContext.Current.Tick
```

`RequestSpawn` 只执行：

```text
1. 根据 ProjectileDefId 查询 ProjectileDatabase。
2. 校验 OwnerUnitUid、Def.PrefabId 和 SpawnSchema。
3. 把 SpawnBoardInput 转换为确定性只读 SpawnBoard。
4. 读取当前请求 Tick。
5. 确保内部 Seq 状态已经切换到当前 Tick。
6. 使用 NextSpawnSequenceInTick 分配本 Tick 内部 Seq。
7. 构造最终 ProjectileUid。
8. 创建 PendingSpawnRecord。
9. 写入 PendingSpawns 与 PendingSpawnByUid。
10. 返回 ProjectileUid。
```

#### Tick 内 Seq 懒重置

`ProjectileWorld` 不再依赖外部 Tick 起始重置函数。

在第一次处理某个 Tick 的成功生成请求时：

```text
currentTick = SimulationTickContext.Current.Tick

if SpawnSequenceTick != currentTick
    SpawnSequenceTick = currentTick
    NextSpawnSequenceInTick = 0
    SpawnSequenceExhausted = false
```

随后每次成功接受请求：

```text
使用当前 NextSpawnSequenceInTick
然后推进到下一个 Seq
```

规则：

```text
请求校验失败
    不切换序号状态
    不消耗 Seq

同一 Tick 内不同 PrefabId
    共用 ProjectileWorld 的同一套 Seq

第一个成功请求
    Seq = 0

超过 byte 可表达范围
    产生确定性溢出错误
    禁止回绕
```

`SpawnSequenceTick` 不是第二套 Tick 来源。  
当前 Tick 的唯一权威仍然是：

```text
SimulationTickContext.Current.Tick
```

它不执行：

```text
取得 Projectile
取得 PhysicsEntity2D
激活 GameObject
注册 PhysicsWorld
加入 ActiveProjectiles
执行 SpawnModules
```

请求校验失败时：

```text
返回 ProjectileUid.Invalid
不写入 PendingSpawns
不消耗 SpawnSequenceInTick
```

请求成功后立即得到稳定 UID，但实例状态仍是：

```text
Pending
```

只有 `CommitSpawns` 完成后才变成：

```text
Active
```

### 待生成记录

```text
PendingSpawnRecord
    ProjectileUid Uid
    int ProjectileDefId

    UnitUid OwnerUnitUid
    TeamId TeamSnapshot
    ProjectileSourceDescriptor Source

    SpawnBoard Board
```

说明：

| 字段 | 说明 |
|---|---|
| `Uid` | `RequestSpawn` 时预分配的最终投掷物 UID；已经包含请求 Tick、PrefabId 和本 Tick Seq |
| `ProjectileDefId` | 提交时选择的逻辑配置 |
| `OwnerUnitUid` | 归属单位 |
| `TeamSnapshot` | 提交时取得的业务阵营快照 |
| `Source` | 来源溯源 |
| `Board` | 已校验并冻结的初始化黑板 |

不再额外保存：

```text
SubmitLogicTick
ProjectileUid.SpawnSequenceInTick
EffectiveSpawnTick
CommitTick
```

请求 Tick 与稳定帧内顺序已经由 `ProjectileUid.SpawnLogicTick` 和 `ProjectileUid.SpawnSequenceInTick` 表达。

`PendingSpawnRecord` 不保存：

```text
Projectile 引用
PhysicsEntity2D 引用
GameObject 引用
Unity Transform
```

---

### 外部状态查询

外部直接使用 `ProjectileUid` 查询，不增加额外句柄。

```text
ProjectileLookupState
    Missing
    Pending
    Active
```

语义：

| 状态 | 含义 |
|---|---|
| `Missing` | 无效 UID、从未接受过、待生成请求已取消，或投掷物已经销毁 |
| `Pending` | 已接受请求并分配 UID，但尚未执行 `CommitSpawns` |
| `Active` | 已创建 `Projectile`、绑定 `PhysicsEntity2D` 并加入活跃注册表 |

查询接口：

```text
ProjectileLookupState GetState(ProjectileUid uid)

bool TryGetActive(
    ProjectileUid uid,
    out Projectile projectile
)
```

查询顺序：

```text
1. ActiveRegistry 包含 UID
       -> Active

2. PendingSpawnByUid 包含 UID
       -> Pending

3. 其它情况
       -> Missing
```

`TryGetActive` 只在 `Active` 状态返回当前 Tick 临时可用的 `Projectile` 引用。

外部规则：

```text
跨 Tick 保存 ProjectileUid。
不得跨 Tick 保存 Projectile 引用。
```

本版有意合并：

```text
从未存在
曾经存在但已销毁
```

因此销毁后不会保留墓碑记录，也不能再查询结束原因或销毁 Tick。

---

### `CommitSpawns`：唯一实例创建入口

```text
ProjectileWorld.CommitSpawns()
```

是唯一真正创建投掷物实例的入口。

固定流程：

```text
1. 按 ProjectileUid.SpawnLogicTick、SpawnSequenceInTick 的稳定顺序遍历本次可提交记录。
2. 根据 ProjectileDefId 重新取得 ProjectileDef。
3. 读取 Def.PrefabId。
4. 从 PhysicsEntityPool 对应池取得 PhysicsEntity2D。
5. 从 LogicPool 取得 Projectile。
6. 使用 PendingSpawnRecord 中已经分配的 Uid 初始化 Projectile。
7. 解析 OwnerUnitUid，恢复 Owner 临时引用。
8. 显式绑定 Projectile 与 PhysicsEntity2D。
9. 通过物理正式接口绑定查询身份并设置初始逻辑姿态。
10. 执行 SpawnModules；需要改变空间状态时继续调用物理正式接口。
11. 注册 PhysicsWorld。
12. 写入 ActiveRegistry。
13. 加入 ActiveProjectiles。
14. 从 PendingSpawnByUid 和 PendingSpawns 移除记录。
```

提交完成后必须满足：

```text
projectile.Uid == pending.Uid
projectile.Entity == entityFromPrefabPool

entity.Owner == projectile
entity.UidSnapshot == projectile.Uid
entity.TeamSnapshot == projectile.Team
entity.Kind == Projectile
```

`Projectile` 不通过 `GetComponent<PhysicsEntity2D>()` 查找实体。  
组件查找只允许由池创建函数或预制体加载校验阶段完成一次。

如果一个已经接受的请求被 Gameplay 规则显式取消：

```text
从 PendingSpawns 和 PendingSpawnByUid 移除
不创建实例
之后 GetState(uid) 返回 Missing
```

预制体缺失、对象池无法创建或静态表不一致不应成为普通 Gameplay 分支，应视为配置或运行环境错误，避免不同客户端产生不同结果。

---



## 需求演进

### 2026-10-02

变动内容：投射物保存 pending/active 状态，生成序列由 ProjectileWorld 拥有。

legacyDecision：D-012


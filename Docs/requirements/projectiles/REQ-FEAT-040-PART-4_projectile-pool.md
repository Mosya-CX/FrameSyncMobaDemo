# 投射物实体池与回收

## 本功能范围

本案细化“提交运动寿命与回收”中的投射物实体池与回收，仅覆盖下列明确接口与边界。

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

### 为什么实体池返回 `PhysicsEntity2D`

每个 `PrefabKind.Projectile` 运行时预制体 GO 必须满足公共 Prefab 契约要求，并挂载：

```text
PhysicsEntity2D : MonoBehaviour
```

池的创建函数：

```text
1. 使用 PrefabKind.Projectile 与 RuntimeEntityPrefabId 查询 GlobalPrefabTable。
2. Instantiate Prefab GO。
3. 在加载或首次创建时取得 PhysicsEntity2D。
4. 返回该组件作为池对象。
```

因此：

```text
ObjectPool<PhysicsEntity2D>
```

本质上池化的是该组件所在的整套 GameObject 实例。

获取 GO：

```text
entity.gameObject
```

不需要额外设计：

```text
ProjectileHost
ProjectileHostPrefab
ProjectileGoWrapper
```

---

### `PhysicsEntityPool`

```text
PhysicsEntityPool
    RuntimeEntityPrefabId
        -> ObjectPool<PhysicsEntity2D>
```

第一次访问某个 `ProjectileDef.PrefabId` 时创建对应池。

每个池固定绑定一个 `GlobalPrefabTable` 条目，避免：

```text
从错误的池取出不同 GO
回收时找不到原池
同一池混用不同物理预制体
```

回收时使用：

```text
projectile.Def.PrefabId
```

或由池内部保存的稳定 PoolKey 找回原池。

---

### 获取与释放

获取 `PhysicsEntity2D` 时：

```text
GameObject.SetActive(true)
调用物理系统正式 Pool Acquire / Reset 接口
等待 ProjectileWorld 设置身份绑定和初始逻辑姿态
```

释放时：

```text
PhysicsWorld.Unregister
调用物理系统正式 Pool Release / Reset 接口
GameObject.SetActive(false)
Release 到原 PrefabId 池
```

投掷物系统不声明物理组件内部如何清理查询信息、恢复 Shape、由物理系统维护派生空间数据 或重置逻辑姿态。  
这些细节由物理系统唯一负责，投掷物对象池只调用其正式生命周期接口。

### 逻辑池

所有 `Projectile` 逻辑对象结构相同，因此使用一个逻辑池：

```text
ObjectPool<Projectile>
```

无需按 PrefabId 分池。

取出时初始化：

```text
Uid
DefId / Def
OwnerUnitUid / Owner
Team
Source
Entity
Board
State
HitMemory
ModuleStates
```

释放前必须清理：

```text
Owner 强引用
Def 强引用
Entity 引用
Board RuntimeRead 槽位
HitMemory
ModuleStates
PendingEnd 状态
```

---

### 本专题边界

本设计案只做两件事：

```text
1. 定义 ProjectileWorld 聚合快照的入口。
2. 标记哪些真实运行数据需要帧同步设计师重点审查。
```

本设计案不规定：

```text
快照二进制格式
压缩方式
网络传输协议
恢复点选择
全局回滚顺序
权威帧确认规则
```

---



## 需求演进

### 2026-10-02

变动内容：投射物保存 pending/active 状态，生成序列由 ProjectileWorld 拥有。

legacyDecision：D-012


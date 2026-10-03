# 单位身份与根状态

## 本功能范围

本案细化“单位根与能力装配”中的单位身份与根状态，仅覆盖下列明确接口与边界。

## 目标实现

单位由明确类型、空间引用、属性与 Handler 能力组成。

## 技术方案

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

## 边界情况

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

`Unit` 是单位行为、战斗语义、运行时身份和生命周期状态的根对象。

它负责：

| 内容 | 说明 |
|---|---|
| 运行时身份 | `UnitUid`、`TeamId` |
| 静态身份引用 | `UnitPrototypeId`、`UnitKind`、`UnitSubKindId` |
| 击杀收益基准 | `BaseGoldValue`、`BaseExperienceValue` |
| 存活状态 | `LifeState` |
| 能力装配结果 | 由 `HandlerLoadout` 推导出的 `UnitAbilityMask` |
| 当前能力 | `CapabilityState` |
| 当前意图 | `UnitIntent` |
| 行为规划 | `BehaviorPlanner` |
| 行为仲裁 | `ActionArbiter` |
| 行为运行 | `ActionRuntimeSet` |
| 数值容器 | `StatHandler` |
| 战斗公式修正 | `CombatModifierSet` |
| 单位事件 | `UnitEventBus` |
| 轻量隐形标记 | `UnitTag`（同效果去重、多次施放来源隔离） |
| 移动执行引用 | `UnitLocomotionAgent` |
| 空间状态组件引用 | `PhysicsEntity2D` |

`Unit` 不负责实际寻路、RVO、空间网格、墙体挤出、碰撞求解和范围查询算法。  
`Unit` 也不负责直接修改自己的生命周期状态，或决定死亡后是复活、回收、销毁还是生成废墟；这些统一交给 `UnitWorld`。

本版需要特别明确：

```text
Unit 是单位身份、分类和玩法运行状态的核心拥有者。
LifeState 保存在 Unit 上，但正式写权限归 UnitWorld。
PhysicsEntity2D 是空间状态组件，不是单位身份中心。
```

也就是说：

```text
Unit.UnitUid 是权威身份。
Unit.TeamId 是权威阵营。
Unit.UnitKind / Unit.UnitSubKindId 是权威单位分类。
Unit.LifeState 是生命周期状态的权威存储；状态转换由 UnitWorld 权威应用。

PhysicsEntity2D 是物理系统提供的权威空间状态组件。
单位框架只持有引用并调用物理系统正式接口；查询镜像字段和内部空间结构由物理系统设计案定义。
```

> **帧同步设计关注点**  


### Unit 核心属性

| 属性 | 说明 |
|---|---|
| `UnitUid` | 帧同步运行时唯一 ID，由 `SpawnLogicTick + RuntimeEntityPrefabId + SpawnSequenceInTick` 组成 |
| `UnitPrototypeId` | 单位 Gameplay 原型编号，来自 `GlobalUnitPrototypeTable` |
| `TeamId` | 阵营，单位侧权威数据 |
| `UnitKind` | 单位稳定大类 |
| `UnitSubKindId` | `UnitKind` 下的主要子分类，直接使用 `ushort` |
| `BaseGoldValue` | 本单位被击杀时，击杀者金币收益的静态基准值 |
| `BaseExperienceValue` | 本单位被击杀时，击杀者经验收益的静态基准值 |
| `LifeState` | `Alive / Dying / Dead / Respawning` |
| `AbilityMask` | 根据是否装配对应 Handler 自动推导 |
| `Capability` | 当前是否允许启动对应行为 |
| `Intent` | 当前长期行为意图 |
| `Handlers` | 移动、攻击、技能、Buff、群体控制、装备等能力模块 |
| `Stats` | 数值系统入口 |
| `CombatModifiers` | 当前有效战斗公式修正的统一挂载与查询容器 |
| `EventBus` | 单位内部强类型结果事件路由器 |
| `Locomotion` | 移动执行代理引用 |
| `PhysicsEntity` | 单位当前空间状态组件引用 |

v27.1 不保留第二套 `UnitId`。  
跨系统身份统一使用 `UnitUid`；`UnitRegistry` 如果需要数组槽位或紧凑索引，可以维护内部 `RegistryIndex`，但它不是单位身份，也不能对外替代 `UnitUid`。

v27.1 也不恢复运行时 `UnitTags`。  
稳定大类由 `UnitKind` 表达，所属大类下的主要分类由 `UnitSubKindId` 表达；基础数值、Handler 装配、死亡处理、对象池和空间形状仍然由 `UnitPrototype` 的独立配置字段直接表达。

### A 轻量隐形标记 UnitTag

#### A.1 定位

`UnitTag` 是直接内嵌在 `Unit` 上的轻量跨 Tick 隐形标记（普通 C# 字段，不挂 MonoBehaviour，不走 Buff 系统）。它的用途是：

- **同效果去重**：同一来源的同一效果（例如一次韦鲁斯 R 的腐败藤蔓感染）在一段存活时间内只对同一单位生效一次；
- **多次施放来源隔离**：两次施放使用相同的字符串 Key，但携带不同的 `UnitTagUid`，后一次施放会替换前一次的标记，开启完全独立的新生命周期（两次 R 的数据互不共享）。

`UnitTag` 只表达"标记 / 去重 / 来源隔离"，没有任何数值、属性或表现效果；需要数值或效果时仍使用 `Buff` 等正式系统。


#### A.2 数据结构

```csharp
public readonly struct UnitTagUid
{
    public readonly UnitUid SourceUnit;  // 施加者单位来源 id
    public readonly byte    SourceKind;  // 来源类型：技能 / Buff / 装备 / 系统
    public readonly int     SourceId;    // 具体来源 id（如技能 10014、Buff 9113）
    public readonly int     Tick;        // 施加时的逻辑 Tick
}

public struct UnitTag
{
    public string     Key;            // 字符串标识符
    public int        RemainingTicks; // 存活计时；0 = 永久
    public UnitTagUid Uid;            // 唯一施加身份
}
```

`UnitTagUid` 由"单位来源 id + 施加来源 id（技能、Buff、装备等）+ 逻辑 Tick"组成。同一个效果在同一帧内至多施加一次（调用方先按 Key 查重），因此同 Tick 同来源同 id 不会重复，无需额外的帧内序号。

#### A.3 规则

| 场景 | 行为 |
|---|---|
| 同 Key + 相同 Uid 再次施加 | 刷新 `RemainingTicks` |
| 同 Key + 不同 Uid（第二次 R） | 替换旧标记，重新开始完整存活计时 |
| `RemainingTicks > 0` | 每个逻辑 Tick 递减 1，归零自动移除 |
| `RemainingTicks == 0` | 永久标记，不递减 |
| 死亡 / 重生 / 回收 | 全部清除 |

#### A.4 确定性与快照

- 标记列表按 Key 的字符串序（`Ordinal`）维护稳定顺序；查重、推进、捕获均为确定性操作，不依赖 Unity 对象顺序或枚举顺序。
- `UnitTag` 是跨 Tick 状态，必须进入 `UnitSnapshot`（`UnitTag[]`）参与帧同步快照与回滚；Restore 时按 Key 排序恢复。
- 快照 Resolve 阶段校验 `UnitTagUid.SourceUnit` 必须仍存在于 `UnitWorld`，否则视为确定性恢复错误。
- Tick 推进由帧同步管线在 Handler Tick 阶段统一调用（与 `BuffHandler.Advance` 同批），不依赖渲染帧。

#### A.5 用例：韦鲁斯 R 腐败藤蔓

一次 R 感染目标时以 Key = `"VarusR.Vine"`、`Uid = (R 施法者, Ability, R 技能 id, 当前 Tick)` 施加标记：

- 同一次 R 内，目标已带同 Key 标记则不再被重复感染；
- 第二次 R 的 `Tick` 不同（`Uid` 不同），旧标记被替换，可重新感染同一英雄并独立传播；
- 标记存活期（如 180 Tick）覆盖整条传播链，且远小于 R 冷却，天然满足"两次 R 之间信息不互通"。

### UnitUid

`UnitUid` 是帧同步内的单位运行时唯一身份。  
它必须由确定性数据构成，不能依赖 Unity `InstanceId`、对象池内存地址、随机 GUID 或客户端本地非确定性对象地址。

推荐结构：

```csharp
public readonly struct UnitUid
{
    public readonly int SpawnLogicTick;
    public readonly int RuntimeEntityPrefabId;
    public readonly byte SpawnSequenceInTick;

    public UnitUid(
        int spawnLogicTick,
        int runtimeEntityPrefabId,
        byte spawnSequenceInTick)
    {
        SpawnLogicTick = spawnLogicTick;
        RuntimeEntityPrefabId = runtimeEntityPrefabId;
        SpawnSequenceInTick = spawnSequenceInTick;
    }
}
```

构成规则：

```text
UnitUid
    = SpawnLogicTick
    + RuntimeEntityPrefabId
    + SpawnSequenceInTick
```

| 字段 | 说明 |
|---|---|
| `SpawnLogicTick` | 单位被确定性生成的逻辑 Tick |
| `RuntimeEntityPrefabId` | 全局运行时实体预制体表中的稳定编号 |
| `SpawnSequenceInTick` | `UnitWorld` 在当前 LogicTick 内分配的单位生成序号 |

`UnitPrototypeId` 与 `RuntimeEntityPrefabId` 不是同一个概念：

| 编号 | 作用 |
|---|---|
| `UnitPrototypeId` | 查找单位 Gameplay 配置，例如分类、Handler、基础数值和生命周期配置 |
| `RuntimeEntityPrefabId` | 查找实际运行时预制体，并参与构造运行时 UID |

序号权威归 `UnitWorld`：

```csharp
public sealed class UnitWorld
{
    private int _currentSequenceLogicTick;
    private byte _nextSpawnSequenceInTick;
    private bool _spawnSequenceExhausted;

    private byte AllocateSpawnSequence()
    {
        int currentLogicTick =
            SimulationTickContext.Current.Tick;

        if (_currentSequenceLogicTick != currentLogicTick)
        {
            _currentSequenceLogicTick = currentLogicTick;
            _nextSpawnSequenceInTick = 0;
            _spawnSequenceExhausted = false;
        }

        if (_spawnSequenceExhausted)
        {
            throw new DeterministicSimulationException(
                "Unit spawn sequence overflow in one LogicTick."
            );
        }

        byte result = _nextSpawnSequenceInTick;

        if (_nextSpawnSequenceInTick == byte.MaxValue)
        {
            _spawnSequenceExhausted = true;
        }
        else
        {
            _nextSpawnSequenceInTick++;
        }

        return result;
    }
}
```

本版采用一个 `UnitWorld` 内的帧内单位生成序号空间，不再按 `RuntimeEntityPrefabId` 分别维护计数器。  
因此同一 Tick 内生成的所有单位依次获得不同的 `SpawnSequenceInTick`。

示例：

```text
Current Tick = 1200

近战小兵：1200 / 1001 / 0
远程小兵：1200 / 1002 / 1
英雄分身：1200 / 2005 / 2
```

超过本 Tick 可分配数量时必须产生确定性错误，不允许回绕到 `0` 后继续生成重复 UID。

新生单位的主动 Gameplay 生效时间直接由 `UnitUid.SpawnLogicTick` 推导，不增加额外状态字段：

```csharp
public bool CanRunActiveGameplayThisTick =>
    SimulationTickContext.Current.Tick
    > UnitUid.SpawnLogicTick;
```

等价于：

```text
FirstActiveLogicTick
    = UnitUid.SpawnLogicTick + 1
```

生成 Tick 内，单位已经存在并注册，可以：

```text
被查询与成为目标
参与物理碰撞
受到伤害、治疗、Buff 和控制
接收 UnitEventBus 被动结果事件
```

但不能执行：

```text
主动 AI 决策
主动 Order
BehaviorPlanner
ActionRuntime 主动推进
普通主动移动
普通攻击
主动技能推进
```

`CanRunActiveGameplayThisTick` 是派生查询，不进入快照。

物理模拟系统可以镜像同一份 UID 用于空间查询。  
但在单位框架内，权威归属仍然是：

```text
Unit.UnitUid
```

> **帧同步设计关注点**  


### UnitKind 与 UnitSubKindId

单位分类仍然只是 `Unit` 专题中的一个小板块，不单独扩展成大型分类系统。

稳定大类：

```csharp
public enum UnitKind : byte
{
    Hero,
    Minion,
    Monster,
    Structure
}
```

大类下属分类直接使用基础数字字段：

```csharp
public UnitKind UnitKind { get; private set; }
public ushort UnitSubKindId { get; private set; }
```

`UnitSubKindId` 不再封装成额外结构体。  
在 `UnitPrototype` 和全局分类映射表中，它就是可序列化、可在 Inspector 编辑的 `ushort` 字段。

选择 `ushort` 的原因：

```text
只表达非负 ID。
0 可以保留为 None / Unspecified。
范围足够容纳长期扩展的下属分类。
无需为一个简单分类编号再增加值类型包装。
```

`UnitKind` 和 `UnitSubKindId` 的职责：

| 字段 | 职责 |
|---|---|
| `UnitKind` | 表达稳定的大类，用于 Hero / Minion / Monster / Structure 等宽泛查询 |
| `UnitSubKindId` | 表达该大类下唯一的主要子分类 |
| `UnitPrototypeId` | 标识具体单位原型，不等同于分类 |
| 独立配置字段 | 表达 Handler、数值、死亡策略、对象池、空间形状等具体能力 |

示例：

| 单位 | UnitKind | UnitSubKindId 对应名称 |
|---|---|---|
| 普通英雄 | Hero | `NormalHero` |
| 英雄克隆体 | Hero | `CloneHero` |
| 近战小兵 | Minion | `MeleeMinion` |
| 炮车兵 | Minion | `SiegeMinion` |
| 普通野怪 | Monster | `NormalMonster` |
| 史诗野怪 | Monster | `EpicMonster` |
| 防御塔 | Structure | `Tower` |
| 水晶 | Structure | `Inhibitor` |
| 防御塔废墟 | Structure | `TowerRuin` |

这里的 `EpicMonster`、`Tower` 等只是全局配置表中的分类名称，不是代码层新增的 `UnitKind` 枚举成员。

权威配置来源：

```text
UnitPrototype
├── UnitKind
└── ushort UnitSubKindId
```

运行时初始化后，`Unit` 只读持有这两个值，不允许 Buff、技能或临时状态修改。

推荐查询：

```csharp
registry.GetByKind(UnitKind.Monster);

registry.GetBySubKind(
    UnitKind.Monster,
    epicMonsterSubKindId
);
```

`UnitSubKindId` 表示一个主要子分类，不承担任意标签组合。  
如果一个玩法特征不能被自然地视为该 `UnitKind` 下的唯一主分类，就不应强塞进 `UnitSubKindId`，而应由对应系统的独立配置表达。

死亡策略也不通过分类隐式推导：

```text
UnitKind / UnitSubKindId
    用于身份查询。

UnitPrototype.UnitDisposePolicyId / RespawnConfig
    分别决定死亡表现后的对象处置和 UnitWorld 正常复活规则。
```

全局 `UnitSubKindTable` 负责提供：

```text
ushort Id
ParentUnitKind
DebugName
```

并在加载阶段校验 `UnitPrototype.UnitKind` 与映射表中的 `ParentUnitKind` 一致。具体表结构见专题八。

### PhysicsEntity2D 作为 Unit 的空间状态引用

`PhysicsEntity2D` 由物理与范围查询系统唯一定义。  
它是挂在单位预制体或子节点上的 Unity `MonoBehaviour`；`Unit` 只缓存组件引用，不通过 `new` 创建，也不在单位框架中重复声明其内部字段。

单位框架只冻结以下接缝：

```text
Unit.UnitUid / TeamId / UnitKind / UnitSubKindId / LifeState
    仍由 Unit 权威保存。

PhysicsEntity2D
    权威保存物理系统定义的逻辑空间状态。

UnitWorld
    生成时绑定组件、写入查询身份并调用 SetLogicPose。

UnitLocomotionAgent
    通过 ApplyLogicPositionDelta / SetLogicPose /
    TeleportLogicPosition / SetLogicForward 等正式接口更新空间状态。

Presentation Sync
    根据逻辑状态写 Unity Transform。
```

单位框架不再定义或复制：

```text
PhysicsTransform2D
Shape
Bounds
UidSnapshot
TeamSnapshot
OwnerBinding
```

这些结构、读写接口、查询快照和派生 AABB 规则全部以物理系统设计案为准。

关键限制：

```text
PhysicsEntity2D 不拥有 Unit 的 Gameplay 身份和生命周期。
单位 Gameplay 代码不直接写 PhysicsEntity2D 内部字段。
单位 Gameplay 代码不直接写 Unity Transform。
```

### UnitPrototype 作为 Unit 的静态配置来源

单位分类、默认动作能力、基础数值、等级经验、击杀收益基准、对象处置策略、复活配置、对象池配置和空间形状配置都必须提前配置，不能在生成函数中临时拼装。

推荐结构：

```csharp
[Serializable]
public sealed class UnitPrototype
{
    public int UnitPrototypeId;
    public string Name;

    // 指向全局运行时实体预制体表。
    public int RuntimeEntityPrefabId;

    public UnitKind UnitKind;

    // Inspector 直接编辑，不增加包装结构体。
    public ushort UnitSubKindId;

    public HandlerLoadout HandlerLoadout;
    public StatPreset BaseStats;

    [Min(0)]
    public int BaseGoldValue;

    [Min(0)]
    public int BaseExperienceValue;

    public LocomotionProfile LocomotionProfile;
    public PhysicsProfile2D PhysicsProfile;

    // 逻辑死亡表现结束后，如何处理实体对象。
    public ushort UnitDisposePolicyId;

    // Dead -> Respawning -> Alive 的 UnitWorld 配置。
    public UnitRespawnConfig RespawnConfig;

    public UnitPoolConfig PoolConfig;
}

[Serializable]
public sealed class UnitRespawnConfig
{
    public bool CanRespawn;

    [Min(0)]
    public int RespawnDelayTicks;

    public RespawnHealthRule HealthRule;
    public RespawnResourceRule ResourceRule;
}
```

`UnitDisposePolicyId` 和 `RespawnConfig` 的职责必须分开：

```text
UnitDisposePolicy
    负责死亡表现结束后：
    KeepAliveObject / Pool / Destroy / DestroyAndSpawnRuin。

UnitRespawnConfig
    负责保留对象单位：
    是否允许复活、等待多久、以什么生命和资源规则恢复。
```

二者都由 `UnitWorld` 读取和执行。  
`CombatSystem` 不负责解释对象池、废墟或正常英雄复活等待规则。

`BaseGoldValue` 与 `BaseExperienceValue` 的权威配置来源是 `UnitPrototype`。  
生成时将它们复制到 `Unit` 的只读运行时属性，之后不能被 Buff、装备或普通 `StatModifier` 修改。

```csharp
public int BaseGoldValue { get; private set; }
public int BaseExperienceValue { get; private set; }
```

单位框架只提供基础价值查询接口，不负责奖励计算、分配、保存与发放。

`BaseExperienceValue` 与 `StatHandler.CurrentExperience` 不是同一个概念：

```text
BaseExperienceValue
    是“其它单位击杀本单位可以得到多少经验”的基准值。

CurrentExperience
    是“本单位自己已经积累了多少升级经验”的运行时状态。
```

两套配置关系：

```text
GlobalUnitPrototypeTable
    UnitPrototypeId
        -> Unit Gameplay 配置
        -> RuntimeEntityPrefabId
        -> UnitDisposePolicyId
        -> UnitRespawnConfig

GlobalPrefabTable
    RuntimeEntityPrefabId
        -> Unity Prefab

UnitDisposePolicyTable
    UnitDisposePolicyId
        -> 对象处置与死亡表现配置
```

`PhysicsProfile2D` 只表示单位空间形状的静态配置，例如默认形状、形状参数、初始 Forward 和是否需要注册到物理空间查询。  
它不是目标规则配置，也不是技能命中规则配置。

配置加载阶段必须验证：

```text
UnitSubKindId 存在且 ParentUnitKind 与 UnitKind 一致。
RuntimeEntityPrefabId 能在全局运行时预制体表中找到。
UnitDisposePolicyId 能在 UnitDisposePolicyTable 中找到。
RespawnConfig.CanRespawn 与 DisposePolicy 类型相容。
需要对象池的策略拥有有效 PoolConfig。
需要生成废墟的策略拥有有效 RuinUnitPrototypeId。
```

`UnitPrototype` 在开局加载后只读。  
生成函数只能读取它，不能修改它。

> **帧同步设计关注点**  




## 需求演进

### 2026-10-02

变动内容：生成 Tick 可被动参与，主动工作晚于出生 Tick。

legacyDecision：D-008


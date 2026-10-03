# 全局配置与离线校验

## 目标实现

Gameplay 使用验证完毕的稳定配置和版本握手。

## 技术方案

GlobalGameplayData Bake 为定点与整数运行配置，PrefabKind 固定 Unit、Projectile、ParticleVfx、AudioEmitter、Misc。毫秒作者时间在 TickRate 明确后转换。

## 边界情况

Editor 不能创造 PrefabKind 运行枚举；静态错误在 Tick 前暴露；时间不能硬编码旧 30 Hz。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/RuntimeConfig/GlobalGameplayData.cs`：当前关联实现定义 FrameSyncSettingsAuthoring、CriticalDataVersionsAuthoring、GameModeConfigAuthoring、PhysicsSettingsAuthoring、UnitSettingsAuthoring（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/GlobalGameplayDataContractTests.cs`：ProjectAsset_BakesInspectorFloatsToDeterministicValues。
- `Assets/Scripts/FrameSync/Tests/BootstrapDeterminismProbeTests.cs`：ServerFirstTick_MatchesClientPredictionFirstTick。
- `Assets/Scripts/Bootstrap/Tests/EditMode/FormalGoldRewardConfigTests.cs`：FormalConfig_UsesRequestedInitialAndKillGoldValues。
- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameworkSmokeBootstrapTests.cs`：Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks、Bootstrap_BindsSelectedHeroPrototypeToPlayerSpawn。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：ClientComposition_InitializesFromProjectAssets、DestroyDuringContentLoad_ReleasesTransferredScope、ExternalFlow_PrimesLoadingBeforeContentInitialization、GenericSkillIndicators_BindDedicatedRuntimeMaterials、GenericSkillIndicators_RebindBeforeLeaseRelease_ReplacesOwnedInstances。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

```text
GlobalGameplayData
    GlobalParamTable
    GlobalPrefabTable
    UnitPrototypeDatabase
    AbilityDatabase
    BuffDatabase
    EquipmentDatabase
    ProjectileDatabase
    CombatRecipeDatabase
    MapRuntimeData
    PathfindingBakeData
    PhysicsSettings
    FrameSyncSettings
    GameModeDatabase
```

### `FrameSyncSettings`

```text
TickRate
LogicDelta
MinCommandLeadTicks
MaxFutureCommandTicks
SnapshotWindowTicks
MaxPredictionLeadTicks
MaxLogicTicksPerUnityFrame
AuthorityRecoveryRetryTicks
MaxAuthorityRecoveryAttemptsBeforeDisconnect
StartLeadTicks
```

第一版固定：

```text
SnapshotIntervalTicks = 1
```

不提供可调 Inspector 字段。未来更改快照间隔时，必须重新设计恢复起点表达。

### `GameModeConfig`

```text
GameModeId
MapConfigId
GameStartPlayerCount
TeamCount
CountdownTicks
EndingDurationTicks
VictoryRuleId
InitialEarnedGold
```

`InitialEarnedGold` 用于初始化所有端的 `ConfirmedEarnedGoldTotal`。

### 固定 `PrefabKind`

Prefab 类型由代码固定：

```csharp
public enum PrefabKind
{
    Unit,
    Projectile,
    ParticleVfx,
    AudioEmitter,
    Misc
}
```

Inspector 不允许新增、删除或修改 Prefab 类型语义。

### `GlobalPrefabTable`

```text
GlobalPrefabTable
    KindRangeConfigs[]
    PrefabGroups[]
```

```text
PrefabKindRangeConfig
    PrefabKind
    IdRangeStart
    IdRangeEnd
    RequiredComponentRule
```

```text
PrefabGroup
    PrefabKind
    Entries[]
```

```text
PrefabEntry
    PrefabId
    UnityPrefab
    GameplayConfigId optional
    EditorAssetGuid
```

Unit 和 Projectile 的 `PrefabId` 可作为 `RuntimeEntityPrefabId` 参与 UID；ParticleVfx、AudioEmitter 和 Misc 的 ID 只用于表现加载。

### Inspector 要求

自定义 Inspector 按固定类型显示分组和 ID 范围，并直接显示每个条目的已分配 ID：

```text
▼ Unit [1000～1999]

1000  Hero_BlueWarrior   ✓
1001  Minion_Melee       ✓
1002  Minion_Ranged      ✓
----  Minion_Super       未分配
```

至少支持：

```text
编辑每组 PrefabId 范围。
拖入单个或多个 Prefab。
拖入文件夹批量导入。
为未分配条目自动分配 ID。
在 Inspector 中显示、搜索和复制 PrefabId。
按 ID 或名称排序显示。
检测重复、越界和未分配。
显示已用数量和剩余数量。
校验 Required Component。
生成 Bake 数据。
```

已有 ID 默认锁定，排序和拖动不能改变 ID。显式重新分配必须二次确认、显示变更预览并提示引用风险。

### Bake 结果

```text
BakedGlobalPrefabTable
    PrefabRecordsById[]
    PrefabKindRanges[]
```

Bake 阶段完成：

```text
ID 唯一性和范围校验
跨数据库引用校验
Required Component 校验
稳定数组生成
float -> fp
Inspector 整数毫秒 -> Tick（整数运算与显式舍入策略）
生成 IdToIndexMap
生成版本号与内容摘要
```

运行时禁止使用 AssetDatabase、名称或加载顺序分配 ID。

### Unit 与 Projectile 配置引用

```text
UnitPrototypeDatabase
    UnitPrototypeId
    RuntimeEntityPrefabId
    单位系统烘焙数据

ProjectileDatabase
    ProjectileDefinitionId
    RuntimeEntityPrefabId
    投掷物系统烘焙数据
```

单位、投掷物和表现层不再各自定义第二套 Prefab 表。

### 版本握手

```text
GameplayDataVersion
MapDataVersion
GlobalPrefabTableVersion
CommandSchemaVersion
SnapshotSchemaVersion
```

任一关键版本不一致，不得开始帧同步。

---

### 定位

单位框架读取以下开局前加载并冻结的静态配置：

```text
GlobalParamTable
StatDefinitionTable
GlobalUnitPrototypeTable
GlobalPrefabTable
UnitSubKindTable
UnitDisposePolicyTable
```

运行时只读，不允许生成函数临时补记录或修改稳定 ID。

---

### GlobalUnitPrototypeTable

每个 `UnitPrototype` 至少包含：

| 字段 | 说明 |
|---|---|
| `UnitPrototypeId` | 单位 Gameplay 原型编号 |
| `RuntimeEntityPrefabId` | 全局运行时实体预制体编号 |
| `UnitKind` | 单位稳定大类 |
| `UnitSubKindId` | `ushort` 下属分类 ID |
| `HandlerLoadout` | 默认 Handler 装配 |
| `BaseStats` | `StatPreset`：各 `StatId` 的基础值、成长值、等级初始值和每级经验需求 |
| `BaseGoldValue` | 被击杀时金币收益基准值，Inspector 可编辑 |
| `BaseExperienceValue` | 被击杀时经验收益基准值，Inspector 可编辑 |
| `LocomotionProfile` | 移动执行侧配置 |
| `PhysicsProfile2D` | 空间形状配置 |
| `UnitDisposePolicyId` | 死亡表现后对象处置策略 |
| `RespawnConfig` | UnitWorld 管理的正常复活配置 |
| `PoolConfig` | 对象池配置 |

生成单位时只能传入 `UnitPrototypeId` 和运行时变量。  
其它内容全部由静态配置解析。

---

### GlobalPrefabTable

单位框架不再定义独立的运行时 Prefab 表，只引用项目公共契约：

```text
GlobalPrefabTable
    PrefabKind = Unit
    PrefabId = RuntimeEntityPrefabId
```

关系：

```text
UnitSpawnRequest.UnitPrototypeId
    ↓
GlobalUnitPrototypeTable
    ↓
RuntimeEntityPrefabId
    ↓
GlobalPrefabTable
    ↓
PrefabKind 必须为 Unit
    ↓
UnityPrefab / RuntimeLoaderKey
```

`UnitUid` 使用 `RuntimeEntityPrefabId`。  
对象池仍按 `UnitPrototypeId` 分池。

单位框架只负责：

```text
引用 RuntimeEntityPrefabId。
验证对应 PrefabKind == Unit。
通过公共运行时查询入口取得 Prefab。
```

公共表的 Inspector、ID 范围、批量导入和自动分配规则归公共 Prefab 设计案。

---

### UnitSubKindTable

`UnitSubKindId` 直接使用 `ushort`，不增加包装结构体。

```csharp
[Serializable]
public sealed class UnitSubKindRecord
{
    public ushort Id;
    public UnitKind ParentUnitKind;
    public string DebugName;
}
```

规则：

```text
0 保留为 None / Unspecified。
1..65535 由全局表分配。
Id 全局唯一。
UnitPrototype.UnitKind 必须与 ParentUnitKind 一致。
```

Inspector 可以直接编辑 `ushort`；可选自定义 PropertyDrawer 显示映射名称，但底层字段不变。

全局配置额外提供稳定只读入口：

```csharp
public ushort TeamBaseUnitSubKindId
{
    get;
}
```

加载时验证：

```text
TeamBaseUnitSubKindId != 0。
UnitSubKindTable 中存在该记录。
该记录的 ParentUnitKind == Structure。
```

`CombatSystem` 可以在初始化时读取并缓存，用于查询：

```csharp
unit.UnitKind == UnitKind.Structure
&& unit.UnitSubKindId
    == TeamBaseUnitSubKindId
```

单位框架只提供稳定 ID 和查询入口，不负责基地被摧毁后的胜负判断。

---

### UnitDisposePolicyTable

```csharp
[CreateAssetMenu(
    menuName = "MOBA/Config/Unit Dispose Policy Table")]
public sealed class UnitDisposePolicyTable
    : ScriptableObject
{
    [SerializeField]
    private List<UnitDisposePolicy> records;
}
```

每条记录：

| 字段 | 说明 |
|---|---|
| `Id` | 稳定 `ushort` 策略编号 |
| `Type` | 保留、回池、销毁或销毁并生成废墟 |
| `DeathPresentationTicks` | 逻辑死亡后死亡表现持续时间 |
| `RuinUnitPrototypeId` | 需要生成废墟时填写 |

这张表不包含：

```text
死亡是否被阻止。
击杀归属。
金币经验。
KDA。
英雄正常复活等待。
```

正常复活由 `UnitPrototype.RespawnConfig` 和 `UnitWorld` 管理。

---

### StatDefinitionTable 与数值成长参数

`StatDefinitionTable` 是所有通用 `StatId` 的静态定义表。

每条记录至少包含：

| 字段 | 说明 |
|---|---|
| `StatId` | 稳定属性身份 |
| `DebugName` | Inspector、日志和调试面板名称 |
| `DefaultBaseValue` | UnitPrototype 未填写时的明确默认值 |
| `SupportsLevelGrowth` | 是否允许 `StatPresetEntry.GrowthValue` |
| `HasMinValue / MinValue` | 最终值统一下限 |
| `HasMaxValue / MaxValue` | 最终值统一上限 |

属性成长曲线参数继续放在统一 `GlobalParamTable`：

```text
L = Level - 1

LevelBaseValue
    = BaseValue
      + GrowthValue × L
        × (StatGrowthC + StatGrowthD × L)
```

| 参数 | 默认值 | 使用位置 |
|---|---:|---|
| `StatGrowthC` | `0.7025` | `StatHandler` 等级成长 |
| `StatGrowthD` | `0.0175` | `StatHandler` 等级成长 |

这些参数是全局静态配置，不在 `StatHandler` 代码中硬编码。

每级所需经验属于 `UnitPrototype.BaseStats.LevelExperience`。  
多个原型需要共用经验表时，可以在配置工具层引用同一静态资源，但运行时仍解析为只读确定性配置。

`StatDefinitionTable` 加载时必须验证：

```text
StatId 唯一。
MinValue <= MaxValue。
百分比属性使用统一的归一化 fp 语义。
StatPreset 中的每个 StatId 都存在于表中。
不支持成长的属性没有非零 GrowthValue。
所有参与模拟的客户端使用完全一致的定义表版本。
```

---

### 移速与抗性参数

| 参数 | 默认值 | 使用位置 |
|---|---:|---|
| `MoveSpeedToLogicVelocityScale` | `0.01` | `StatHandler.MoveSpeed -> 移动系统速度输入` |
| `ArmorDamageReductionConstant` | `100` | 战斗系统物理减伤 |
| `MagicResistDamageReductionConstant` | `100` | 战斗系统魔法减伤 |

负抗性、穿透和最终伤害规则由战斗系统决定。

---

### 到达与追踪参数

| 参数 | 默认值 | 使用位置 |
|---|---:|---|
| `DefaultPointMoveArriveDistance` | `0.05` | Planner 计算点地移动停止距离 |
| `DefaultAttackMoveStopPadding` | `0` | 攻击追踪停止距离补偿 |
| `DefaultCastMoveStopPadding` | `0` | 施法追踪停止距离补偿 |

RVO、寻路、墙体挤出和物理半径等级仍归对应系统配置。

---

### 加载校验

开局前至少验证：

```text
UnitPrototypeId 唯一。
RuntimeEntityPrefabId 存在且稳定。
Prefab 上存在 Unit。
需要空间查询时存在 PhysicsEntity2D。
UnitKind 与 UnitSubKindId 映射一致。
TeamBaseUnitSubKindId 存在且属于 Structure。
HandlerLoadout 与能力要求一致。
StatDefinitionTable 完整且 StatId 唯一。
BaseStats 的 StatId 不重复，基础值、成长值和等级经验配置合法。
不支持成长的属性没有非零 GrowthValue。
BaseGoldValue / BaseExperienceValue 不小于 0。
UnitDisposePolicyId 存在。
DisposePolicy 与 PoolConfig 相容。
KeepAliveObject 与 RespawnConfig 相容。
RuinUnitPrototypeId 在需要时有效。
所有参与模拟的客户端使用一致配置版本。
```


## 需求演进

### 2026-10-02

变动内容：PrefabKind 为代码固定枚举，Editor 仅管理 ID/条目。

legacyDecision：D-019

### 2026-10-02

变动内容：作者 float 仅在 Bake/初始化边界转正式 fp，Tick 内不回转作为权威。

legacyDecision：D-022

### 2026-08-20

变动内容：开局采用单调时钟，作者毫秒独立于 TickRate；后续目标 Tick 估计补充此规则。

legacyDecision：D-045


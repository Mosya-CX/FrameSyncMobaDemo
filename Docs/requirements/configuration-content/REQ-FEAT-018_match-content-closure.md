# 按对局加载内容闭包

## 目标实现

只加载 Core、选定地图和去重英雄集合，随后确定性同步查询。

## 技术方案

GlobalPrefabTable 是唯一聚合；生产根索引路径型子表，Addressables 在 Tick 0 前异步加载，组成对局内非序列化同步表。MapConfigId 和按 ID 排序的英雄集合决定闭包。

## 边界情况

缺分区、重复 ID、版本/哈希或阵容不一致在初始快照前失败；不回退全量旧目录；禁止 WaitForCompletion。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/RuntimeConfig/GlobalPrefabSubTableAsset.cs`：当前关联实现定义 GlobalPrefabPartitionKind、MatchContentAssetKind、MatchContentAssetAddress、GlobalPrefabPartitionReference、GlobalPrefabSubTableAsset（以源码为实际命名）。
- `Assets/Scripts/RuntimeConfig/GlobalPrefabTable.cs`：当前关联实现定义 PrefabKind、PrefabEntry、PrefabGroup、PrefabKindRangeConfig、GlobalPrefabTable（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameworkSmokeBootstrapTests.cs`：Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks、Bootstrap_BindsSelectedHeroPrototypeToPlayerSpawn。
- `Assets/Scripts/Bootstrap/Tests/EditMode/MatchScopedContentConfigurationTests.cs`：FormalRoot_SelectsOnlyCoreMapAndRequestedHeroes、FormalPartitions_ArePathOnlyAndCoverTwentyTwoEntries、SelectedHeroCatalogs_BakeWithoutOtherHero、UnitCatalogPartitions_CoreAloneOwnsSharedDisposePolicies、LogicAndHeroGroups_AreLocalAndPartitioned、SessionSelection_IsStableAndResettable、FormalGameScene_HasNoLegacyDirectCatalogReferences。
- `Assets/Scripts/Bootstrap/Tests/EditMode/PresentationAddressablesMigrationTests.cs`：ProjectilesAreSplitIntoLogicAndAddressableViews、ProjectileViewRootsAreAtWorldOrigin、MapLogicAndClientViewHaveDisjointResponsibilities、VfxAndAudioLibrariesContainAddressesNotDirectAssets、GameplayConfigurationsHaveNoDirectSpriteDependencies、UiPagesAndPresentationRootsAreAddressableAndOutsideResources、GenericSkillIndicatorsUseSupportedTransparentShader。
- `Assets/Scripts/Bootstrap/Tests/EditMode/UnitAddressablesMigrationTests.cs`：AllFormalUnitEntriesResolveLogicPrefabAndAddressableView、LogicPrefabsContainNoPresentationComponentsOrAssets、ClientViewsContainPresentationHostButNoGameplayRoot、ClientViewRootsAreAtWorldOrigin。
- `Assets/Scripts/ClientContent/Tests/PlayMode/ProjectileViewBinderPlayModeTests.cs`：ProjectileViewLeaseStaysResidentAcrossLifetimes。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 七、`GlobalPrefabTable`：运行时引用边界

表现层只引用项目公共的 `GlobalPrefabTable` 运行时契约，不定义第二套 Prefab 表，也不负责该表的 Unity 编辑器实现。

### 表现层使用的最小运行时语义

表现层只需要能够通过公共 Bake 数据查询：

```text
PrefabId
PrefabKind
UnityPrefab / Runtime Loader Key
GameplayConfigId optional
```

典型用途：

| 消费者 | `PrefabKind` | 用途 |
|---|---|---|
| `VfxManager` | `ParticleVfx` | 查询 ParticleSystem 表现预制体并按 `PrefabId` 分池 |
| `AudioManager` | `AudioEmitter` | 查询 AudioEmitter 预制体并按 `PrefabId` 分池 |
| 单位表现装配 | `Unit` | 读取公共单位 Prefab 契约，不重新定义 Unit Prefab 表 |
| 投掷物表现接入 | `Projectile` | 读取公共投掷物 Prefab 契约，不重新定义 Projectile Prefab 表 |

单位和投掷物的 `PrefabId` 在 Gameplay 中可作为 `RuntimeEntityPrefabId` 参与对应 UID；Particle VFX、AudioEmitter 等普通表现对象不会因此成为 Gameplay 实体。

### 表现定义与 PrefabId 分离

`VfxDefId` 和 `SfxDefId` 是表现规则 ID，`PrefabId` 是实际资源 ID。

一个 VFX 或 SFX 定义可以引用一个表现 Prefab，并配置自己的挂点、时长、参数映射和播放策略；多个定义也可以复用同一个 Prefab。

对象池按 `PrefabId` 分池，定义表按 `VfxDefId / SfxDefId` 查询。

### 职责边界

表现层文档不负责：

```text
GlobalPrefabTable Authoring 结构
自定义 Inspector
PrefabKind 分组编辑
ID 范围编辑
PrefabId 自动分配
排序、搜索和批量导入
重复、越界和未分配校验
Required Component 编辑器规则
Bake 生成流程
PrefabId 重新分配和稳定性工具
```

这些属于公共 Prefab 资源基础设施与帧同步总控约束。

`UnitAnimationDriver`、`VfxManager`、`AudioManager` 和 `UnitPresentationHost` 都只是公共 Bake 数据的消费者，不拥有或维护 `GlobalPrefabTable`。

`PrefabId` 不进入 `PresentationEventId`。同一个逻辑事件即使解析为不同的本地表现资源，其事件身份仍保持不变。


## 需求演进

### 2026-08-10

变动内容：正式资源路径唯一，重复目录和旧引用退役。

legacyDecision：D-038

### 2026-08-23

变动内容：客户端视图可重建、句柄唯一释放。直接逻辑引用和完全服务器排除 Addressables 部分已被后续内容闭包修订取代。

legacyDecision：D-048

### 2026-08-26

变动内容：Core+Map+去重 Hero 对局闭包在 Tick 前加载，逻辑 Addressables 允许在服务器；客户端表现仍排除。

legacyDecision：D-051


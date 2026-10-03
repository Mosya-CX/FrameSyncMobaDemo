# 客户端视图与服务器资源隔离

## 目标实现

客户端视图延迟出现仍安全，服务器只保留逻辑依赖。

## 技术方案

客户端视图可重建、只读逻辑；异步句柄有唯一 owner 和一次释放。服务器保留 Logic-* 本地 Addressables catalog/bundles，排除 Client-* 及表现程序集。

## 边界情况

D-051 已修订早期“服务器完全排除 Addressables”和“逻辑 Prefab 必须直接引用”的规则；回滚、回池或销毁期间加载成功不能绑定旧生命。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/ClientContent/AddressablesClientContentService.cs`：当前关联实现定义 AddressablesClientContentService、CacheEntry、AudioCacheEntry、SpriteCacheEntry（以源码为实际命名）。
- `Assets/Scripts/ClientContent/ClientUnitViewBinder.cs`：当前关联实现定义 ClientUnitViewBinder、Binding（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/ClientContent/Tests/PlayMode/AddressablesClientContentServicePlayModeTests.cs`：RepresentativeClientRoots_LoadAndReleaseAsynchronously。
- `Assets/Scripts/Bootstrap/Tests/EditMode/LocalAddressablesConfigurationTests.cs`：FormalSettingsAreLocalOnlyAndContainAllClientGroups、ClientRootEntries_HaveUniqueAddressesAndExistingAssets、InventoryClassificationIsStable。
- `Assets/Scripts/ClientContent/Tests/PlayMode/ProjectileViewBinderPlayModeTests.cs`：ProjectileViewLeaseStaysResidentAcrossLifetimes。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。


## 需求演进

### 2026-08-23

变动内容：客户端视图可重建、句柄唯一释放。直接逻辑引用和完全服务器排除 Addressables 部分已被后续内容闭包修订取代。

legacyDecision：D-048

### 2026-08-26

变动内容：Core+Map+去重 Hero 对局闭包在 Tick 前加载，逻辑 Addressables 允许在服务器；客户端表现仍排除。

legacyDecision：D-051


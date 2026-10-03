# 购买合成与 Command 二次校验

## 目标实现

商店能在满栏、已有组件和余额变化下确定购买结果。

## 技术方案

EquipmentShopRuntime 从装备数据库计算 EquipmentPurchasePlan；UI RequestCheck 是预检查，ProcessCommand 再以当前确定性状态验证，组件选择和剩余价格稳定。

## 边界情况

不能从 UI 预检查直接扣款；懒创建 Trader 不形成第二金币权威；部分堆叠、重复标签和不足余额明确拒绝。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Equipment/EquipmentShopRuntime.cs`：当前关联实现定义 EquipmentShopRuntime、ShopTraderRuntime、ShopOperationRecord、EquipmentShopOperationType、EquipmentShopFailureReason（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/EquipmentShopRuntimeSnapshotTests.cs`：RestoreResolve_PreservesValidControlledUnitReference、Restore_RejectsNoncanonicalPlayerOrder。
- `Assets/Scripts/Gameplay/Tests/EquipmentShopTransactionTests.cs`：Purchase_UsesOwnedRecipePartAndFreedLowestSlot、SellUndo_RequiresReturnedGoldAndRestoresSlot、DuplicateRule_AllowsSmallItemsAndRejectsFinishedItems、SequentialPurchases_AfterSnapshotRestore_MatchContinuousState。
- `Assets/Scripts/Bootstrap/Tests/EditMode/ShopFoundationEditModeTests.cs`：Database_RegisterAndRetrieve、Database_AllDefinitions_Sorted、Capture_SnapshotsOwnTheirCreatedTradersList、ShopTrader_CreateAndRetrieve、GoldDelta_InitialZero、GoldDelta_AfterManualLog、Snapshot_RoundTrip_PreservesTrader。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/HeroTestSceneEquipmentPlayModeTests.cs`：BuildWorld_LoadsSelectedVarusPartition、BuildWorld_LoadsFormalEquipmentCatalogForShop、LocalTickShop_UsesFormalGoldPurchaseRecipeAndUndo。
- `Assets/Scripts/FrameSync/Tests/EquipmentShopViewTests.cs`：CurrentAvailableGold_UsesConfirmedIncomeAndEffectiveShopDelta。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 定位

`EquipmentShopRuntime` 是一局 Gameplay 中唯一的商店运行时。

```csharp
public sealed class EquipmentShopRuntime
    : IRollback<EquipmentShopRuntimeSnapshot>
{
    private EquipmentDatabase _equipmentDatabase;
    private EquipmentGlobalParams _globalParams;

    private ShopTraderRuntime?[]
        _tradersByPlayerSlot;

    private IConfirmedGoldIncomeView
        _confirmedGoldIncomeView;

    private IEquipmentShopCommandSubmitter
        _commandSubmitter;
}
```

所有模拟端都创建同样的商店运行时。

商店不解析网络 AuthorityFrame，也不维护金币确认进度；它只通过只读端口取得当前已确认累计收入。

它不是商店 UI Prefab 上的 `MonoBehaviour`。

---

### 商店没有独立静态商品配置

本案不增加：

```text
EquipmentShopDefinition
EquipmentShopDatabase
EquipmentShopId
商品 Catalog
```

商品直接来自：

```text
GlobalGameplayData.EquipmentDatabase
```

当前正式注册的装备都属于标准商店商品。

商店 UI 的分类、搜索和合成树使用：

```text
EquipmentTier
EquipmentTagDefinition
Name
Recipe
```

是否处于商店范围由地图或比赛规则系统提供：

```text
ShopAccessState.CanTrade
```

---

### Gameplay 金币与商店交易金币严格分离

所有 Gameplay 金币来源只能调用：

```text
GoldIncomeRuntime.RequestGoldIncome
```

包括自然金币、补刀、击杀、助攻、地图目标、比赛规则和其它正式奖励。

商店购买、出售和撤销不是 Gameplay 金币获取。它们只通过：

```text
OperationLog + ShopOperationRecord.Reverted
```

表达交易金币变化。

正式冻结：

```text
Gameplay 奖励金币
    -> GoldIncomeRuntime。

购买、出售、撤销
    -> EquipmentShopRuntime.OperationLog。
```

出售金币不进入 `GoldIncomeRuntime`，避免确认收入与可逆商店交易形成双重权威。

---

### 确认收入只读端口

`GoldIncomeRuntime` 实现：

```csharp
public interface IConfirmedGoldIncomeView
{
    int GetConfirmedEarnedGoldTotal(
        PlayerSlot player);

    int ConfirmedIncomeThroughTick
    {
        get;
    }
}
```

`EquipmentShopRuntime` 只持有该只读接口：

```csharp
private IConfirmedGoldIncomeView
    _confirmedGoldIncomeView;
```

商店不负责接收金币请求、创建金币记录、封闭批次、确认 AuthorityFrame 或提交账户持久化。

---

### 商店金币事实与当前可用金币

```text
购买 GoldDelta < 0。
出售 GoldDelta > 0。
Reverted == true 的记录不计入当前金币结果。
```

```text
EffectiveShopGoldDelta =
    Sum(
        OperationLog 中所有
        Reverted == false 的 GoldDelta
    )
```

```text
CurrentAvailableGold =
    GoldIncomeRuntime
        .GetConfirmedEarnedGoldTotal(player)
    +
    EffectiveShopGoldDelta
```

`CurrentAvailableGold` 是只读派生值，不直接赋值、不网络同步、不进入 GameplaySnapshot，也不保存逐 Tick 历史。

---

### 可选派生缓存与交易链懒创建

允许维护：

```text
CachedConfirmedEarnedGoldTotal。
CachedEffectiveShopGoldDelta。
IsEffectiveShopGoldDeltaDirty。
```

它们只是可重建性能缓存，不得成为第二份权威状态。

商店初始没有 `ShopTraderRuntime`。玩家第一次成功购买或出售时创建：

```csharp
public sealed class ShopTraderRuntime
{
    public PlayerSlot Player;
    public UnitUid ControlledUnitUid;
    public int NextOperationSequence;

    public readonly List<ShopOperationRecord>
        OperationLog;

    public readonly List<int>
        UndoableOperationStack;

    // 派生缓存，不进入快照
    public int CachedEffectiveShopGoldDelta;
    public bool IsEffectiveShopGoldDeltaDirty;

    public ShopUndoInvalidReason
        LastUndoInvalidReason;

    public CombatParticipationFlags
        LastCombatParticipationFlags;

    public int RuntimeRevision;
}
```

未创建 TraderRuntime 时：

```text
EffectiveShopGoldDelta = 0。
CurrentAvailableGold =
    GoldIncomeRuntime
        .GetConfirmedEarnedGoldTotal(player)。
```

`NextOperationSequence` 是整场对局持续递增的 `int`，并进入 `EquipmentShopRuntimeSnapshot`。

---

### 交换槽位不属于商店

交换槽位走：

```text
SwapEquipmentSlotCommand
    -> CommandDispatcher
    -> Unit
    -> EquipmentHandler.SwapSlots
```

不进入：

```text
EquipmentShopRuntime.ProcessCommand
OperationLog
UndoableOperationStack
```

交换不会永久清空商店撤销栈。

撤销时重新检查原交易记录要求的槽位状态：

```text
当前槽位状态匹配记录
    -> 可以撤销。

当前不匹配
    -> 当前不可撤销。

之后重新交换回匹配状态
    -> 可以再次通过撤销检查。
```

---

### 两层检查

购买、出售和撤销只保留两层检查。

#### 第一层：本地 RequestCheck

仅由发起操作的本地客户端调用：

```text
CheckPurchaseRequest
CheckSellRequest
CheckUndoRequest
```

作用：

```text
决定是否提交 Command。
立即向 UI 返回失败原因。
```

失败时不提交 Command，也不修改 Gameplay。

#### 第二层：所有端 ProcessCommand 可行性检查

Command 在目标 Tick 执行时，所有端调用：

```text
EquipmentShopRuntime.ProcessCommand
```

所有端基于相同的：

```text
当前 Tick 确认收入基线。
有效 OperationLog.GoldDelta。
EquipmentHandler 状态。
ShopTraderRuntime。
装备配置。
Command 稳定顺序。
```

得到相同成功或失败结果。

不增加第三套服务端业务检查，也不增加：

```text
ShopOperationAuthorityResult
```

各端交易结果不一致属于程序错误或状态反同步。

---

### Request 接口与 Command 边界

购买请求只表达：

```text
哪个 PlayerSlot。
想购买哪个 EquipmentId。
```

购买请求和购买 Command 都不携带：

```text
PreferredSlot。
TargetSlot。
DestinationSlot。
任何由客户端指定的目标装备槽位。
```

正式接口：

```csharp
public EquipmentShopRequestCheck
    RequestPurchase(
        PlayerSlot localPlayer,
        EquipmentId target);

public EquipmentShopRequestCheck
    RequestSell(
        PlayerSlot localPlayer,
        EquipmentSlot sourceSlot);

public EquipmentShopRequestCheck
    RequestUndo(
        PlayerSlot localPlayer);
```

请求通过后，商店通过帧同步层端口提交正式：

```text
EquipmentShopCommand
```

```csharp
public interface IEquipmentShopCommandSubmitter
{
    void SubmitPurchase(
        PlayerSlot player,
        EquipmentId item);

    void SubmitSell(
        PlayerSlot player,
        EquipmentSlot sourceSlot);

    void SubmitUndo(
        PlayerSlot player);
}
```

购买目标槽位不是玩家意图，而是目标 Tick 执行交易时，根据当时装备栏、配方组件和堆叠状态确定性派生的交易结果。

Command 字段、TargetTick、CommandSequence、网络发送和重演由帧同步设计负责。

#### 金币敏感命令分类

```csharp
public bool IsGoldSensitive(
    in EquipmentShopCommand command);
```

正式分类：

```text
Purchase -> true。
Undo     -> true。
Sell     -> false。
```

`Undo` 统一视为金币敏感，因为撤销卖出需要支付原出售所得金币。

该接口只用于 RequestCheck/ProcessCommand 规则一致性、确定性测试、协议说明和诊断；不用于金币确认后扫描历史 Command、主动触发后缀重演或自动补买。

---

### 可用金币查询与确定性购买计划

#### 可用金币

```csharp
public int GetCurrentAvailableGold(
    PlayerSlot player)
{
    int confirmedIncome =
        _confirmedGoldIncomeView
            .GetConfirmedEarnedGoldTotal(
                player);

    int shopDelta =
        GetEffectiveShopGoldDelta(player);

    return confirmedIncome + shopDelta;
}
```

```csharp
private int GetEffectiveShopGoldDelta(
    PlayerSlot player)
{
    ShopTraderRuntime trader =
        TryGetTrader(player);

    if (trader == null)
        return 0;

    if (trader.IsEffectiveShopGoldDeltaDirty)
    {
        trader.CachedEffectiveShopGoldDelta =
            RecalculateEffectiveShopGoldDelta(
                trader.OperationLog);

        trader.IsEffectiveShopGoldDeltaDirty =
            false;
    }

    return trader.CachedEffectiveShopGoldDelta;
}
```

UI、本地 RequestCheck 和所有端 `ProcessCommand` 必须调用同一查询入口。

预测但尚未确认的 `GoldIncomeRecordBatch` 不进入该查询。

#### UI 动态购买价格

UI 商店视图绑定当前本地玩家，并提供：

```csharp
public int CalculatePurchasePrice(
    EquipmentId targetEquipmentId);
```

计算规则：

```text
读取目标装备基础价格。
按正式配方与稳定槽位顺序选择当前已有小件。
扣除这些小件的配置价值。
返回最终动态购买价格。
```

公式：

```text
PurchasePrice =
    TargetEquipment.Value
    -
    Sum(当前可消耗配方小件的 Value)
```

该函数：

```text
只返回价格。
不判断金币是否足够。
不判断最终是否可以买下。
不修改 EquipmentHandler。
不提交 Command。
```

目标装备来自正式商店列表，因此 UI 不需要额外的 Preview 结构或失败原因包装。

内部组件匹配规则必须与 `TryBuildPurchasePlan` 完全一致，避免 UI 显示价格与正式购买价格不同。

#### EquipmentPurchasePlan

购买检查和购买执行必须共用同一个纯查询规划器：

```csharp
public struct EquipmentPurchasePlan
{
    public EquipmentId TargetEquipmentId;

    public int PurchaseCost;

    public EquipmentSlot[]
        ConsumedComponentSlots;

    public bool MergeIntoExistingStack;

    public EquipmentSlot DestinationSlot;

    public EquipmentSlotChange[]
        SlotChanges;
}
```

```csharp
private EquipmentPurchasePlanResult
    TryBuildPurchasePlan(
        PlayerSlot player,
        EquipmentId target);
```

规划器：

```text
只读取当前 Gameplay 状态。
不修改 EquipmentHandler。
不修改 OperationLog。
不修改撤销栈。
不修改任何金币状态。
不提交 Command。
```

返回结果包含完整的交易后六格状态和失败原因。

购买 Command 不携带 `EquipmentPurchasePlan`。所有端在目标 Tick 根据当时的确定性状态重新构建计划。

---

### 购买 RequestCheck

本地请求检查调用：

```text
TryBuildPurchasePlan(localPlayer, target)
```

规划顺序固定为：

```text
1. 验证本地玩家、ControlledUnitUid 和商店范围。
2. 从正式 EquipmentDatabase 取得目标装备。
3. 解析目标装备配方。
4. 按稳定规则选择要消耗的小件槽位。
5. 在模拟六格中先删除这些小件。
6. 基于删除后的模拟六格自动确定：
       可合并的目标同类 Stack；
       或最低合法空槽位。
7. 把目标装备写入模拟六格。
8. 对完整交易后状态执行全部合法性检查。
9. 计算 PurchaseCost。
10. 检查 GetCurrentAvailableGold(player) 是否足够。
```

完整检查至少包括：

```text
配方可解析。
组件数量足够。
组件槽位选择确定。
交易后的六格存在合法放置结果。
Stack 不超过 MaxStack。
成装不会重复。
全局唯一标签不会冲突。
目标消耗品可以合并，或存在自动分配槽位。
购买价格合法。
当前可用金币足够。
```

当前版本没有：

```text
Definition.Purchasable
```

RequestCheck 只使用规划结果决定是否提交 Command。

它不会实际删除小件或加入目标装备。

检查通过也只表示当前本地状态允许提交，不保证目标 Tick 执行时仍然成功。

---

### 购买 ProcessCommand

所有端在目标 Tick 调用：

```text
TryBuildPurchasePlan(player, target)
```

并按相同状态、槽位顺序和配方顺序得到相同计划。

失败：

```text
不修改 EquipmentHandler。
不追加 OperationLog。
不修改 UndoableOperationStack。
不修改派生金币缓存。
```

成功后严格按以下顺序提交：

```text
1. 记录计划中全部受影响槽位的 Before。
2. 按槽位升序删除 ConsumedComponentSlots 中的小件。
3. 小件全部删除完成后：
       若 MergeIntoExistingStack == true，
           合并到计划确定的 DestinationSlot；
       否则，
           在计划确定的 DestinationSlot 创建目标装备。
4. 记录全部受影响槽位的 After。
5. 验证实际 After 与计划 SlotChanges.After 一致。
6. 追加 Purchase Record：
       GoldDelta = -PurchaseCost
       Reverted = false
7. 将 OperationSequence 压入 UndoableOperationStack。
8. 标记 EffectiveShopGoldDelta 缓存 Dirty。
```

强制规则：

> 完整交易计划验证通过后，必须先真实删除全部配方小件，再合并或放入目标装备。

禁止：

```text
先把目标装备放入当前装备栏，再删除组件。
因交易前没有空槽而拒绝本可通过合成释放槽位的购买。
在提交过程中临时出现第七件装备。
让 Command 指定目标装备槽位。
```

购买不修改：

```text
GoldIncomeRuntime.ConfirmedEarnedGoldTotal。
GoldIncomeRecordBatch。
独立余额字段。
```

---

### 配方组件选择、自动分配与合成价格

#### 组件选择

购买时优先消耗玩家当前持有的完整配方组件。

同一种组件存在多件时：

```text
低槽位优先。
```

不同组件的处理顺序：

```text
按 Recipe 配置数组顺序。
```

组件槽位确定后，先在模拟六格中全部删除。

#### 自动分配目标装备

模拟删除小件后，按以下稳定规则确定目标装备位置：

```text
1. 若目标装备允许堆叠，并存在可继续合并的同类实例：
       选择最低槽位的可合并实例。

2. 否则：
       选择模拟删除组件后的最低空槽位。

3. 没有合法合并位置或空槽：
       购买失败。
```

配方组件释放的槽位可以用于放置目标装备。

例如交易前六格已满，但目标装备会消耗 Slot 0 和 Slot 2 的小件：

```text
模拟删除后：
    Slot 0 为空。
    Slot 2 为空。

目标装备：
    自动进入最低空槽 Slot 0。
```

#### 交易后合法性

成装重复、唯一标签和 Stack 检查必须基于：

```text
删除组件并加入目标装备后的完整模拟六格。
```

不能基于交易前装备栏判断。

例如高级鞋会消耗基础鞋：

```text
交易前：
    基础鞋带有 Boots 唯一标签。

模拟交易：
    先删除基础鞋。
    再加入高级鞋。

交易后：
    六格中仍只有一个 Boots 标签。
```

因此升级合法。

#### 合成价格

```text
PurchaseCost =
    Target.Value
    -
    Sum(ConsumedComponents.Value)
```

`ConsumedComponents` 必须与计划中实际选择并删除的小件完全一致。

必须先完成整份 `EquipmentPurchasePlan`，再提交任何 Gameplay 状态变化。

---


## 需求演进

### 2026-08-11

变动内容：商店 Trader 懒创建，小兵奖励距离按正式数值边界转换。

legacyDecision：D-040


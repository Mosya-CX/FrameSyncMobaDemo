# 卖出撤销与交易失效

## 目标实现

买卖与撤销以可回滚交易链表达。

## 技术方案

OperationLog 与 UndoableOperationStack 维护 EffectiveShopGoldDelta；出售和撤销出售属于商店增量，不产生 GoldIncome 记录。

## 边界情况

离开范围、参与战斗、使用装备等永久失效规则必须准确；金币确认不扫描后续 Purchase/Undo，也不创建金币专用脏 Tick。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Equipment/EquipmentShopRuntime.cs`：当前关联实现定义 EquipmentShopRuntime、ShopTraderRuntime、ShopOperationRecord、EquipmentShopOperationType、EquipmentShopFailureReason（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/EquipmentShopRuntimeSnapshotTests.cs`：RestoreResolve_PreservesValidControlledUnitReference、Restore_RejectsNoncanonicalPlayerOrder。
- `Assets/Scripts/Bootstrap/Tests/EditMode/ShopFoundationEditModeTests.cs`：Database_RegisterAndRetrieve、Database_AllDefinitions_Sorted、Capture_SnapshotsOwnTheirCreatedTradersList、ShopTrader_CreateAndRetrieve、GoldDelta_InitialZero、GoldDelta_AfterManualLog、Snapshot_RoundTrip_PreservesTrader。
- `Assets/Scripts/Gameplay/Tests/EquipmentShopTransactionTests.cs`：Purchase_UsesOwnedRecipePartAndFreedLowestSlot、SellUndo_RequiresReturnedGoldAndRestoresSlot、DuplicateRule_AllowsSmallItemsAndRejectsFinishedItems、SequentialPurchases_AfterSnapshotRestore_MatchContinuousState。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/HeroTestSceneEquipmentPlayModeTests.cs`：BuildWorld_LoadsSelectedVarusPartition、BuildWorld_LoadsFormalEquipmentCatalogForShop、LocalTickShop_UsesFormalGoldPurchaseRecipeAndUndo。
- `Assets/Scripts/FrameSync/Tests/EquipmentShopViewTests.cs`：CurrentAvailableGold_UsesConfirmedIncomeAndEffectiveShopDelta。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 八、`EquipmentShopRuntimeSnapshot`

```text
EquipmentShopRuntimeSnapshot
    ShopTraderRuntimeSnapshot[]
```

每个交易者至少保存：

```text
PlayerSlot
ControlledUnitUid
NextOperationSequence

OperationLog[]
    OperationSequence
    OperationType
    LogicTick
    GoldDelta
    Reverted
    RevertedLogicTick
    SlotChanges
    EquipmentRevisionBefore
    EquipmentRevisionAfter

UndoableOperationStack
LastUndoInvalidReason
LastCombatParticipationFlags
RuntimeRevision
```

不保存：

```text
ConfirmedEarnedGoldTotal
CurrentAvailableGold
TotalShopExpenditure
EffectiveShopGoldDelta
CachedEffectiveShopGoldDelta
```

其中：

```text
EffectiveShopGoldDelta
    从 OperationLog 中 Reverted == false 的 GoldDelta 求和。

CurrentAvailableGold
    = ConfirmedEarnedGoldTotal
      + EffectiveShopGoldDelta。
```

`CachedEffectiveShopGoldDelta` 只是派生缓存，必须在 `Rebuild` 阶段从 OperationLog 重建。

---

### 卖出

RequestCheck：

```text
当前允许访问商店。
SourceSlot 合法。
槽位存在装备。
```

出售金额：

```text
SellValue =
    Definition.Value
    × GlobalParamTable.EquipmentSellRate
```

所有端成功执行：

```text
记录 Slot Before
    ↓
EquipmentHandler 移除装备
    ↓
记录 Slot After
    ↓
追加 Sell Record
    GoldDelta = +SellValue
    Reverted = false
    ↓
压入 UndoableOperationStack
    ↓
标记 EffectiveShopGoldDelta 缓存 Dirty
```

出售不会增加：

```text
GoldIncomeRuntime.ConfirmedEarnedGoldTotal。
GoldIncomeRecordBatch。
```

当前版本没有：

```text
Definition.Sellable
```

---

### 交易记录

```csharp
public struct ShopOperationRecord
{
    public int OperationSequence;

    public EquipmentShopOperationType
        OperationType;

    public PlayerSlot Player;
    public UnitUid ControlledUnitUid;

    public int LogicTick;
    public int GoldDelta;

    public EquipmentSlotChange[]
        SlotChanges;

    public bool Reverted;
    public int RevertedLogicTick;

    public int EquipmentRevisionBefore;
    public int EquipmentRevisionAfter;
}
```

操作类型：

```csharp
public enum EquipmentShopOperationType
{
    Purchase,
    Sell
}
```

当前版本直接保留整场 `OperationLog`。

撤销不追加新的 Undo 记录。

---

### UndoableOperationStack

撤销栈保存：

```text
OperationSequence
```

使用：

```text
Push
Peek
Pop
Clear
```

只允许后进先出。

---

### 撤销查询与 RequestCheck

UI 商店视图提供：

```csharp
public bool CanUndo();
```

它只返回当前本地玩家是否可以撤销：

```text
true：
    UI 启用撤销按钮。

false：
    UI 禁用撤销按钮。
```

`CanUndo()` 与 `RequestUndo`、Undo `ProcessCommand` 共用同一套撤销可行性检查，但不提交 Command，也不修改任何状态。

正式检查包括：

```text
当前允许访问商店。
当前 TraderRuntime 存在。
UndoableOperationStack 非空。
没有发生永久撤销失效。
当前 ControlledUnitUid 与原记录一致。
原记录 Reverted == false。
当前受影响槽位仍匹配原记录 After。
撤销卖出时 GetCurrentAvailableGold(player)
    足以支付 Original.GoldDelta。
```

原卖出记录的 `GoldDelta > 0`，撤销后该记录不再计入余额，因此需要当前金币至少覆盖该返还金额。

失败时不提交 Undo Command。

---

### 撤销 ProcessCommand

所有端：

```text
取得撤销栈顶 OperationSequence
    ↓
读取原 Purchase 或 Sell Record
    ↓
重新执行相同可行性检查
    ↓
通过 EquipmentHandler 恢复全部 Before
    ↓
OriginalRecord.Reverted = true
    ↓
OriginalRecord.RevertedLogicTick = Current Tick
    ↓
弹出撤销栈顶
    ↓
标记 EffectiveShopGoldDelta 缓存 Dirty
```

撤销只修改：

```text
EquipmentHandler。
OperationLog 中的原记录。
UndoableOperationStack。
派生金币缓存 Dirty 标记。
```

不会修改账户或任何独立余额字段。

---

### 永久撤销失效规则

以下情况清空对应玩家的撤销栈：

```text
走出商店范围。
接受有效伤害。
造成有效伤害。
接受有效治疗。
造成有效治疗。
接受有效护盾。
造成有效护盾。
成功使用主动装备。
成功消耗商店购买的消耗品、Stack 或 Charge。
```

失效只清空：

```text
UndoableOperationStack
```

不清空或删除：

```text
OperationLog
```

---

### 离开商店范围

当：

```text
ShopAccessState.CanTrade
    true -> false
```

时调用：

```csharp
InvalidateUndo(
    player,
    ShopUndoInvalidReason.LeftShopRange);
```

关闭商店 UI 不等于离开商店范围。

---

### CombatSystem 帧内参与记录

CombatSystem 基于有效伤害、治疗和护盾结果维护当前 Combat Phase 的参与掩码：

```csharp
[Flags]
public enum CombatParticipationFlags
{
    None = 0,

    DamageDealt = 1 << 0,
    DamageTaken = 1 << 1,

    HealDealt = 1 << 2,
    HealTaken = 1 << 3,

    ShieldGranted = 1 << 4,
    ShieldReceived = 1 << 5
}
```

有效条件：

```text
EffectiveDamage > 0
EffectiveHeal > 0
EffectiveShield > 0
```

0 伤害、完全免疫、0 有效治疗和失败护盾不计入。

自然恢复不经过普通治疗结果，不触发商店撤销失效。

---

### CombatSystem 调用商店接口

Combat Phase 结算完成后，CombatSystem 按 `PlayerSlot` 稳定升序调用：

```csharp
public interface IEquipmentShopUndoInvalidator
{
    void InvalidateUndoByCombat(
        PlayerSlot player,
        CombatParticipationFlags flags);
}
```

商店内部自行检查：

```text
TraderRuntime 是否存在。
UndoableOperationStack 是否为空。
```

CombatSystem 不读取交易链，也不管理撤销资格。

自我伤害、自我治疗或自我护盾只对同一玩家执行一次幂等失效。

---

### FailureReason

```csharp
public enum EquipmentShopFailureReason
{
    None,

    InvalidLocalPlayer,
    ControlledUnitNotFound,

    NotInShopRange,
    ItemNotFound,

    InsufficientGold,
    InventoryFull,
    InvalidRecipe,

    DuplicateFinishedItem,
    UniqueTagConflict,

    InvalidSlot,
    EmptySlot,

    NoUndoableTransaction,
    UndoInvalidatedByLeavingShop,
    UndoInvalidatedByCombat,
    UndoInvalidatedByEquipmentUse,
    TransactionStateChanged
}
```

---


## 需求演进

### 2026-10-02

变动内容：确认金币不主动重演后续商店命令或补建本地已拒绝命令。

legacyDecision：D-006

### 2026-10-02

变动内容：可用金币为确认累计加有效商店增量，只读派生且不保存快照。

legacyDecision：D-007


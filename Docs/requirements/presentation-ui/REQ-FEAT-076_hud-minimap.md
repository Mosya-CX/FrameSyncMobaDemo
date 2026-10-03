# HUD 数值技能与小地图

## 目标实现

局内界面只读展示权威投影，通过类型化请求互动。

## 技术方案

HUD 查询 Unit/Handler、WatchHook 和 IEquipmentShopView；UIDisplayConvert 在显示边界把 fp 转换成显示值，技能升级与装备交互提交请求。

## 边界情况

UI 不重复伤害或金币公式；选择英雄列表来自 HeroDisplayTable；连续再施法投影、技能可用与冷却按当前只读 Runtime。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Bootstrap/GameFlowLuaBridge.cs`：当前关联实现定义 GameFlowLuaBridge（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Presentation/HudSnapshotDto.cs`：当前关联实现定义 HudSnapshotDto、AbilityCooldownDto、BuffInfoDto（以源码为实际命名）。
- `Assets/Scripts/LuaBridge/UIDisplayConvert.cs`：当前关联实现定义 UIDisplayConvert（以源码为实际命名）。
- `Assets/Scripts/RuntimeConfig/HeroDisplayTable.cs`：当前关联实现定义 HeroDisplayEntry、HeroDisplayTable（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/LuaBridge/Tests/UIDisplayConvertTests.cs`：ResourceInt_Floors_And_ClampsAtZero、StatInt_Rounds、Decimal2_RoundsToTwoPlaces、PercentInt_MultipliesByHundred、Rate01_ClampsAndHandlesZeroMax。
- `Assets/Scripts/Bootstrap/Tests/EditMode/PresentationAddressablesMigrationTests.cs`：ProjectilesAreSplitIntoLogicAndAddressableViews、ProjectileViewRootsAreAtWorldOrigin、MapLogicAndClientViewHaveDisjointResponsibilities、VfxAndAudioLibrariesContainAddressesNotDirectAssets、GameplayConfigurationsHaveNoDirectSpriteDependencies、UiPagesAndPresentationRootsAreAddressableAndOutsideResources、GenericSkillIndicatorsUseSupportedTransparentShader。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/HeroTestSceneEquipmentPlayModeTests.cs`：BuildWorld_LoadsSelectedVarusPartition、BuildWorld_LoadsFormalEquipmentCatalogForShop、LocalTickShop_UsesFormalGoldPurchaseRecipeAndUndo。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/UiLuaPagesSmokeTests.cs`：LuaPages_BootAndFlowRoutes。
- `Assets/Scripts/RuntimeConfig/Editor/Tests/HeroDisplayAndPrefabRangeTests.cs`：Sync_AutoCreatesRow_ForEachHeroPrototype、Sync_IgnoresNonHeroPrototypes、Sync_IsIdempotent、Sync_PreservesAvatarAndManualName、Sync_RemapsPrefabId_WhenPrototypeChanges、Sync_RemovesRow_WhenHeroPrototypeDisappears、DefaultUnitRange_AcceptsConfiguredIds。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 不增加 UI 数据中间层

继续删除：

```text
UIContext
UIServiceSet
PageActions
UIStore
```

Lua 可以直接访问经过 xLua 导出的具体只读接口和类型化 Request。

允许：

```text
读取配置
读取 Unit / Handler
建立 UI WatchHook
读取 IEquipmentShopView
调用应用流程
调用类型化 Request
```

禁止：

```text
直接修改 StatHandler
直接修改 AbilityRuntime
直接增删 EquipmentHandler
直接修改 ShopTraderRuntime
直接调用 ProcessCommand
直接访问 CommandCollector
```

### xLua 导出类型

至少导出：

```text
UIManager
FrameSyncGameRuntime
GoldIncomeRuntime
IConfirmedGoldIncomeView
EquipmentShopRuntime
IEquipmentShopView
EquipmentDatabase
EquipmentDefinition
EquipmentShopRequestCheck
EquipmentShopFailureReason
EquipmentSlot
EquipmentId
AbilityHandler
AbilityBook
AbilityRuntime
AbilitySlot
StatHandler 的 UI 查询类型
WatchHook 或其 UI 包装
```

### 获取当前 Runtime、商店视图和 Unit

```lua
local function GetFrameRuntime()
    return CS.FrameSyncGameRuntime.Instance
end

local function GetShopRuntime()
    local frame = GetFrameRuntime()

    if frame == nil
        or frame.GameplayRuntime == nil then
        return nil
    end

    return frame.GameplayRuntime.EquipmentShopRuntime
end

local function GetShopView()
    local frame = GetFrameRuntime()

    if frame == nil then
        return nil
    end

    return frame.LocalEquipmentShopView
end

local function GetLocalUnit()
    local frame = GetFrameRuntime()

    if frame == nil then
        return nil
    end

    return frame:GetLocalControlledUnit()
end
```

`LocalEquipmentShopView` 是绑定当前本地玩家的 `IEquipmentShopView` 实例。具体属性名可以按组合根实现调整，但 Lua 不重复传 `PlayerSlot` 给只读查询。

### 装备配置查询

```lua
local database =
    CS.GlobalGameplayData.Instance.EquipmentDatabase

local definition =
    database:Get(equipmentId)
```

Lua 可读取：

```text
Id
Name
Description
Icon
Tier
Value
MaxStack
CanStack
FixedStats
Effects
Tags
Recipe
```

### 商店 Request

```lua
local result =
    shopRuntime:RequestPurchase(
        localPlayerSlot,
        equipmentId)

local result =
    shopRuntime:RequestSell(
        localPlayerSlot,
        equipmentSlot)

local result =
    shopRuntime:RequestUndo(
        localPlayerSlot)
```

`Allowed == true` 只表示 Command 已提交。

### 技能升级 Request

```lua
CS.FrameSyncGameRuntime.Instance
    :RequestAllocateAbilitySkillPoint(slot)
```

UI 只申请 Command。

### 当前可用金币

```lua
local view = GetShopView()

local gold =
    view ~= nil
    and view:GetCurrentAvailableGold()
    or 0
```

内部语义：

```text
CurrentAvailableGold =
    GoldIncomeRuntime.GetConfirmedEarnedGoldTotal(localPlayer)
    + EquipmentShopRuntime.GetEffectiveShopGoldDelta(localPlayer)
```

其中：

```text
ConfirmedEarnedGoldTotal
    只包含已经按连续 AuthorityFrame 确认的 Gameplay 收入。

EffectiveShopGoldDelta
    只包含 OperationLog 中 Reverted == false 的购买和出售变化。
```

Lua 不读取或计算：

```text
GoldIncomeRecordBatch
GoldIncomeBatchDigest
ConfirmedIncomeThroughTick
未确认金币批次
OperationLog.GoldDelta
```

预测但尚未确认的 Gameplay 金币收入不进入当前可购买余额。

### 动态购买价格

```lua
local price =
    view:CalculatePurchasePrice(
        selectedEquipmentId)
```

该接口：

```text
只返回价格
不检查金币是否足够
不提交 Command
不修改装备
```

组件选择必须与正式 `TryBuildPurchasePlan` 一致，但组件列表不暴露给 Lua。

### 卖出金额

卖出金额由 Lua 按正式公式计算：

```text
SellValue =
    EquipmentDefinition.Value
    × GlobalParamTable.EquipmentSellRate
```

第一版每次卖出一个单位，因此消耗品即使 `StackCount > 1`，显示金额也不乘 `StackCount`。

```lua
function UIFormat.CalculateSellValue(
    definitionValue,
    equipmentSellRate)

    return math.floor(
        definitionValue
        * CS.UIDisplayConvert.ToFloat(
            equipmentSellRate))
end
```

该取整规则必须与 Gameplay 完全一致，并通过共享测试样例校验。

### 撤销查询

```lua
local canUndo =
    view ~= nil
    and view:CanUndo()
```

UI 只决定 `UndoBtn.interactable`。

UI 不显示：

```text
撤销的是购买还是卖出
撤销后金币变化
撤销失败原因
```

### Unit 和 Handler 查询

允许读取：

```text
StatHandler 数值
AbilityHandler / AbilityBook / AbilityRuntime
EquipmentHandler 六个槽位
```

禁止调用写接口。

### UI WatchHook

```text
Show 时绑定
Hide / Dispose 时释放
回调只刷新 UI
```

Lua 不动态订阅 `UnitEventBus`。

### Revision、金币确认与恢复边界

`GoldIncomeRuntime` 负责：

```text
BeginTick
RequestGoldIncome
SealTick
金币批次摘要
ConfirmAuthorityFrame
DiscardUnconfirmedFromTick
ConfirmedEarnedGoldTotal
ConfirmedIncomeThroughTick
```

普通回滚：

```text
丢弃 ReplayFromTick 及之后的未确认金币批次
保留已确认累计收入
重演时重新生成金币请求、批次和摘要
```

`AuthorityRecovery`：

```text
补齐缺失 AuthorityFrame
按 Tick 接受和确认金币批次
必要时重演
```

Lua 不参与这些流程。

恢复、确认或重演完成后，Lua 重新调用：

```text
IEquipmentShopView.GetCurrentAvailableGold()
IEquipmentShopView.CalculatePurchasePrice(equipmentId)
IEquipmentShopView.CanUndo()
```

---

### 三层数值类型

```text
Inspector Authoring
    float

Runtime Gameplay
    fp

UI Presentation
    int / float / string
```

逻辑运行时的小数继续使用 `fp`。

Inspector 为了编辑使用 `float`。

Unity UI 为了显示使用 `float` 或字符串。

### UI 可以在哪里转换 `fp`

本版允许两种转换位置：

```text
C# Client View 提前转换
Lua 调用 UIDisplayConvert 转换
```

推荐优先由 C# 客户端只读 View 提供显示值。

如果 Lua 直接读到了 `fp`，必须调用统一的 C# 转换工具，不在 Lua 中自行解析 RawValue。

```lua
local display =
    CS.UIDisplayConvert

local attack =
    display:StatInt(runtimeStats.AttackDamage)
```

### `UIDisplayConvert`

```csharp
[LuaCallCSharp]
public static class UIDisplayConvert
{
    public static float Float(fp value)
    {
        return (float)value;
    }

    public static int ResourceInt(fp value)
    {
        return Mathf.Max(0, Mathf.FloorToInt((float)value));
    }

    public static int StatInt(fp value)
    {
        return Mathf.RoundToInt((float)value);
    }

    public static float Decimal2(fp value)
    {
        return Mathf.Round((float)value * 100f) / 100f;
    }

    public static int PercentInt(fp rate)
    {
        return Mathf.RoundToInt((float)rate * 100f);
    }

    public static float Rate01(fp current, fp max)
    {
        if (max <= fp.zero)
            return 0f;

        return Mathf.Clamp01((float)(current / max));
    }
}
```

### 显示规则

| 属性 | UI 类型 | 显示 |
|---|---|---|
| 当前血量、最大血量 | `int` | `1234` |
| 当前施法资源、最大资源 | `int` | `420` |
| 攻击力、法强、护甲、魔抗 | `int` | `186` |
| 移速、攻击距离 | `int` | `398` |
| 攻击速度 | `float` | `1.32` |
| 自然生命回复 | `float` | `9.20` |
| 施法资源回复 | `float` | `11.50` |
| 暴击率 | `int percent` | `40%` |
| 百分比护甲穿透 | `int percent` | `20%` |
| 百分比法术穿透 | `int percent` | `10%` |
| 生命偷取、全能吸血 | `int percent` | `8%` |
| 血条和资源条 | `float 0..1` | Slider |
| 冷却遮罩 | `float 0..1` | Image.fillAmount |

### 取整

资源使用向下取整：

```text
100.8 HP
    -> 100
```

普通属性使用四舍五入：

```text
185.6 AttackDamage
    -> 186
```

### 百分比

Runtime：

```text
CriticalChance = fp 0.4
```

UI：

```text
UIDisplayConvert.PercentInt
    -> 40

UIFormat.Percent
    -> "40%"
```

UI 转换后的值不能写回 Gameplay。

---

### HUD 组成

HUD 由：

```text
Map
TotalBar
```

组成。

`TotalBar` 包含：

```text
MatchBar
SmallStatus
CompactStats
ExpandedStats
SkillBar
EquipBar
```

### HUD 数据来源

HUD 读取：

```text
FrameSyncGameRuntime.GetLocalControlledUnit()
Unit.StatHandler
Unit.AbilityHandler
Unit.EquipmentHandler
Unit.ActionStateView
MatchRuntimeView
MapPresentationView
IEquipmentShopView.GetCurrentAvailableGold()
```

生命、资源、经验和属性优先使用 `WatchableValue / WatchHook` 局部刷新。

HUD 不读取 GameplaySnapshot，也不订阅 UnitEventBus。

### 关键组件

#### MatchBar

```text
TimeText
ScoreText
KdaText
```

#### Map

```text
MapImage
MapIconList
```

#### SmallStatus

```text
Portrait
LevelText
HpBar
HpText
ResourceBar
ResourceText
ExpBar
DeadRoot
RespawnText
```

#### CompactStats

```text
CompactStatsRoot
AttackText
AbilityText
ArmorText
ResistText
MoveText
```

#### ExpandedStats

```text
ExpandedStatsRoot
StatsHold

BaseAttackText
BonusAttackText
FullAbilityText
FullArmorText
FullResistText

AttackSpeedText
CritText
HasteText

FullMoveText
RangeText

ArmorPenFlatText
ArmorPenRateText
MagicPenFlatText
MagicPenRateText

LifeStealText
OmniVampText
HpRegenText
ResourceRegenText
```

#### SkillBar

```text
SkillList
```

每个 `SkillCell` 增加：

```text
UpgradeRoot
UpgradeBtn
```

`UpgradeRoot` 位于对应技能槽位上方。
它不保存技能点，只根据当前 `AbilityHandler` 查询结果显示。

#### EquipBar

```text
GoldText
EquipList
```

### HUD 本地 UI 状态

HUD Lua 只保存：

```text
属性栏是否展开
Cell 悬停状态
WatchHook 释放句柄
```

HUD 不保存：

```text
生命、资源或属性副本
PendingSkillPoints 副本
技能升级 Pending 状态
装备栏预测副本
金币历史
交易链
回滚状态
```

### 按住 C

```text
Pressed
    -> ExpandedStatsRoot Active
    -> CompactStatsRoot Inactive

Released
    -> ExpandedStatsRoot Inactive
    -> CompactStatsRoot Active
```

该操作不进入 GameplayCommand。

### 技能介绍

每个 SkillCell 内部拥有：

```text
InfoRoot
InfoNameText
InfoLevelText
InfoDescText
InfoCostText
InfoCooldownText
```

Hover Enter 显示，Hover Exit 隐藏。

不使用公共 `UITip`。

### 技能点查询与升级按钮

技能点权威属于：

```text
AbilityHandler.PendingSkillPoints
```

HUD 刷新技能栏时：

```text
PendingSkillPoints <= 0
    -> 所有 SkillCell.UpgradeRoot 隐藏

PendingSkillPoints > 0
    -> 每个已绑定槽位显示 UpgradeRoot
    -> UpgradeBtn.interactable =
       AbilityHandler.CanAllocateSkillPoint(slot)
```

UI 只读取查询结果。

按钮是否可用不代表未来 Command 执行时一定成功，因为目标 LogicTick 的单位等级、技能状态或其它条件可能已经变化。

### 技能点 Command Request 边界

点击升级按钮：

```text
SkillCell.lua
    -> FrameSyncGameRuntime.RequestAllocateAbilitySkillPoint(slot)
    -> UI 输入结束
```

UI 不直接调用：

```text
AbilityHandler.TryAllocateSkillPoint
GameplayCommandFactory
CommandCollector
```

UI 不讨论 Command 后续怎样分发或执行，只要求帧同步层提供类型明确的申请入口。

### 装备格交互

HUD 装备格只在 Shop 已打开时响应点击：

```text
点击 EquipCell
    -> 将 EquipmentSlot 作为页面焦点传给 Shop
    -> Shop 保存 focusOwnedSlot
    -> Shop 重新读取该槽位当前内容
```

装备槽位是卖出操作的唯一定位方式。

UI 不保存：

```text
装备实例 UID
预期装备配置 ID
旧装备对象引用
```

预测或回滚后，如果该槽位变为空或换成其它装备，Shop 按当前槽位内容刷新。

装备格点击不负责：

```text
购买目标槽位
交换槽位
主动装备使用
直接出售
```

真正卖出只能由：

```text
Shop SellBtn
    -> RequestSell(player, focusOwnedSlot)
```

发起。

### HUD Lua 主结构

```lua
local UIBase = require("UI.Core.UIBase")

local HUD = setmetatable({}, { __index = UIBase })
HUD.__index = HUD

function HUD.New(refs)
    local self = UIBase.New(HUD, refs)

    self.expanded = false

    self:BindEvent(self.ui.StatsHold.Pressed, function()
        self:SetStatsExpanded(true)
    end)

    self:BindEvent(self.ui.StatsHold.Released, function()
        self:SetStatsExpanded(false)
    end)

    return self
end

function HUD:Show()
    self:SetStatsExpanded(false)
    self:BindCurrentUnitHooks()
    self:Refresh()
end

function HUD:Hide()
    UIBase.Hide(self)
    self:SetStatsExpanded(false)
end

function HUD:Refresh()
    self:RefreshMatch()
    self:RefreshMap()
    self:RefreshHero()
    self:RefreshCompactStats()
    self:RefreshFullStats()
    self:RefreshSkills()
    self:RefreshEquip()
    self:RefreshGold()
end

return HUD
```

### HUD 金币刷新

```lua
function HUD:RefreshGold()
    local frame =
        CS.FrameSyncGameRuntime.Instance

    local view =
        frame ~= nil
        and frame.LocalEquipmentShopView
        or nil

    local gold =
        view ~= nil
        and view:GetCurrentAvailableGold()
        or 0

    self.ui.GoldText.text =
        tostring(gold)
end
```

HUD 不读取确认收入记录、OperationLog 或账户累计金币自行计算余额。

### HUD 英雄数据查询

```lua
function HUD:RefreshHero()
    local view =
        CS.FrameSyncGameRuntime.Instance:GetLocalControlledUnit()

    local display =
        CS.UIDisplayConvert

    self.ui.Portrait.sprite = view.Portrait
    self.ui.LevelText.text = tostring(view.Level)

    local hp = display.ResourceInt(view.Hp)
    local maxHp = display.ResourceInt(view.MaxHp)

    self.ui.HpBar.value =
        display.Rate01(view.Hp, view.MaxHp)

    self.ui.HpText.text =
        string.format("%d / %d", hp, maxHp)

    local resource =
        display.ResourceInt(view.Resource)

    local maxResource =
        display.ResourceInt(view.MaxResource)

    self.ui.ResourceBar.value =
        display.Rate01(view.Resource, view.MaxResource)

    self.ui.ResourceText.text =
        string.format(
            "%d / %d",
            resource,
            maxResource)

    self.ui.ExpBar.value =
        display.Rate01(view.Exp, view.NextLevelExp)

    self.ui.DeadRoot:SetActive(view.IsDead)
    self.ui.RespawnText.text =
        view.IsDead and tostring(view.RespawnSeconds) or ""
end
```

### 完整属性刷新

```lua
function HUD:RefreshFullStats()
    local stats =
        CS.FrameSyncGameRuntime.Instance:GetLocalControlledUnit().StatHandler

    local display =
        CS.UIDisplayConvert

    self.ui.BaseAttackText.text =
        tostring(display.StatInt(stats.BaseAttackDamage))

    self.ui.BonusAttackText.text =
        tostring(display.StatInt(stats.BonusAttackDamage))

    self.ui.FullAbilityText.text =
        tostring(display.StatInt(stats.AbilityPower))

    self.ui.FullArmorText.text =
        tostring(display.StatInt(stats.Armor))

    self.ui.FullResistText.text =
        tostring(display.StatInt(stats.MagicResist))

    self.ui.AttackSpeedText.text =
        UIFormat.Decimal2(display.Decimal2(stats.AttackSpeed))

    self.ui.CritText.text =
        UIFormat.Percent(display.PercentInt(stats.CriticalChance))

    self.ui.HasteText.text =
        tostring(display.StatInt(stats.AbilityHaste))

    self.ui.ArmorPenRateText.text =
        UIFormat.Percent(display.PercentInt(stats.ArmorPenRate))

    self.ui.MagicPenRateText.text =
        UIFormat.Percent(display.PercentInt(stats.MagicPenRate))

    self.ui.HpRegenText.text =
        UIFormat.Decimal2(display.Decimal2(stats.HpRegen))

    self.ui.ResourceRegenText.text =
        UIFormat.Decimal2(display.Decimal2(stats.ResourceRegen))
end
```

### 技能栏刷新

```lua
function HUD:RefreshSkills()
    local frame =
        CS.FrameSyncGameRuntime.Instance

    local unit =
        frame ~= nil
        and frame:GetLocalControlledUnit()
        or nil

    if unit == nil or unit.AbilityHandler == nil then
        self.ui.SkillList:SetItems({})
        return
    end

    local handler = unit.AbilityHandler
    local book = handler.AbilityBook
    local pendingPoints = handler.PendingSkillPoints

    local cells = {}

    for i = 0, book.SlotCount - 1 do
        local slot = book:GetSlotAt(i)
        local runtime = book:GetRuntime(slot)

        if runtime ~= nil then
            local definition = runtime.Def
            local icon = definition.Icon

            local hasCast, castView =
                handler:TryGetCurrentCast()

            if hasCast
                and castView.Runtime == runtime
                and castView.CurrentCastStage ~= nil
                and castView.CurrentCastStage.IconOverride ~= nil then
                icon = castView.CurrentCastStage.IconOverride
            end

            cells[#cells + 1] = {
                Slot = slot,
                Icon = icon,
                Name = definition.Name,
                Description = definition.Description,

                Level = runtime.Level,
                MaxLevel = definition.MaxLevel,
                Learned = runtime.Learned,

                CooldownRate =
                    runtime.CooldownState.DisplayRate,

                CooldownSeconds =
                    runtime.CooldownState.DisplayRemainingSeconds,

                ShowUpgrade =
                    pendingPoints > 0,

                CanUpgrade =
                    pendingPoints > 0
                    and handler:CanAllocateSkillPoint(slot)
            }
        end
    end

    self.ui.SkillList:SetItems(cells)
end
```

实际冷却、消耗和按键文本读取成员以技能系统正式公开接口为准。

UI 不缓存技能点或技能等级。

### `SkillCell.lua`

```lua
local UICellBase = require("UI.Core.UICellBase")

local SkillCell = setmetatable({}, { __index = UICellBase })
SkillCell.__index = SkillCell

function SkillCell.New(refs)
    local self = UICellBase.New(SkillCell, refs)

    self.slot = nil

    self:BindEvent(self.ui.Hover.Enter, function()
        self.ui.InfoRoot:SetActive(true)
    end)

    self:BindEvent(self.ui.Hover.Exit, function()
        self.ui.InfoRoot:SetActive(false)
    end)

    self:BindClick(self.ui.UpgradeBtn, function()
        self:RequestUpgrade()
    end)

    self.ui.InfoRoot:SetActive(false)
    self.ui.UpgradeRoot:SetActive(false)

    return self
end

function SkillCell:RequestUpgrade()
    if self.slot == nil then
        return
    end

    local frame =
        CS.FrameSyncGameRuntime.Instance

    if frame == nil then
        return
    end

    -- 这里只申请帧同步 Command。
    -- 不直接调用 AbilityHandler.TryAllocateSkillPoint，
    -- 不修改 Level、Learned 或 PendingSkillPoints。
    frame:RequestAllocateAbilitySkillPoint(self.slot)
end

function SkillCell:Bind(data)
    UICellBase.Bind(self, data)

    self.slot = data.Slot

    self.ui.Icon.sprite = data.Icon
    self.ui.KeyText.text = data.KeyText or ""

    self.ui.LevelText.text =
        string.format("%d/%d", data.Level, data.MaxLevel)

    self.ui.CooldownMask.fillAmount =
        data.CooldownRate

    local showCooldown = data.CooldownSeconds > 0

    self.ui.CooldownText.gameObject:SetActive(showCooldown)
    self.ui.CooldownText.text =
        showCooldown
        and tostring(data.CooldownSeconds)
        or ""

    self.ui.Unlearned:SetActive(not data.Learned)
    self.ui.NoResource:SetActive(
        data.Learned and not data.EnoughResource)

    self.ui.UpgradeRoot:SetActive(data.ShowUpgrade)
    self.ui.UpgradeBtn.interactable =
        data.ShowUpgrade and data.CanUpgrade

    self.ui.InfoNameText.text = data.Name or ""
    self.ui.InfoLevelText.text =
        string.format("等级 %d", data.Level)

    self.ui.InfoDescText.text = data.Description or ""
    self.ui.InfoCostText.text = data.CostText or ""
    self.ui.InfoCooldownText.text = data.CooldownText or ""
end

return SkillCell
```

`SkillCell` 不保存 Pending Command、预测等级或升级结果。

Command 被本地预测执行后，`AbilityHandler` 当前公开状态会变化，HUD 重新绑定 Cell：

```text
PendingSkillPoints 减少
AbilityRuntime.Level 增加
AbilityRuntime.Learned 更新
升级按钮重新计算
```

如果权威对账导致回滚，HUD 同样只读取重演后的当前公开状态。

### `EquipCell.lua`

每个装备格显示：

```text
Icon
StackText
ChargeText
CooldownMask
InfoRoot
InfoNameText
InfoDescText
InfoSellPriceText
```

显示规则：

```text
Definition.CanStack
    -> 显示 StackCount

ChargeCount > 0
    -> 显示 ChargeCount

ReadyTick > CurrentLogicTick
    -> 显示冷却遮罩
```

`StackCount` 和 `ChargeCount` 是两个不同概念，不共用文本。

出售价格显示为“卖出一个单位”的价格：

```lua
function EquipCell:Bind(data)
    self.slot = data.Slot

    self.ui.Icon.sprite = data.Icon

    self.ui.StackText.gameObject:SetActive(
        data.CanStack)

    self.ui.StackText.text =
        data.CanStack
        and tostring(data.StackCount)
        or ""

    self.ui.ChargeText.gameObject:SetActive(
        data.ChargeCount > 0)

    self.ui.ChargeText.text =
        data.ChargeCount > 0
        and tostring(data.ChargeCount)
        or ""

    self.ui.InfoSellPriceText.text =
        tostring(data.SingleUnitSellValue)
end
```

点击装备格仍只在 Shop 打开时传递 `EquipmentSlot` 作为卖出焦点。

---

### 帧同步层提供的技能点请求入口

UI 只要求一个类型明确的接口：

```csharp
public bool RequestAllocateAbilitySkillPoint(
    AbilitySlot slot);
```

Lua：

```lua
frame:RequestAllocateAbilitySkillPoint(slot)
```

接口内部怎样创建、收集和执行 Command 属于帧同步与单位框架设计，不在 UI 设计案中展开。

返回 `false` 只表示当前无法申请本地 Command，不表示 UI 可以直接执行技能升级。

---


## 需求演进

### 2026-10-02

变动内容：英雄列表按目录驱动，正 HeroConfigId 可重复选择，由最终内容闭包验证。

legacyDecision：D-028

### 2026-08-12

变动内容：连续再施法界面只读投影，施法朝向保持 Gameplay 授权。

legacyDecision：D-042


-- Shop overlay page (design v9.1 11)
local UIBase = require("UI.Core.UIBase")

local Shop = setmetatable({}, { __index = UIBase })
Shop.__index = Shop

function Shop.New(refs)
    local self = UIBase.New(Shop, refs)

    self.selectedEquipmentId = 0
    self.focusOwnedSlot = -1

    _G._ShopRefresh = function()
        self:Refresh()
    end

    self:BindClick(self.ui.BuyBtn, function()
        if self.selectedEquipmentId > 0 then
            GameFlow.RequestPurchase(self.selectedEquipmentId)
        end
        self:Refresh()
    end)

    self:BindClick(self.ui.SellBtn, function()
        if self.focusOwnedSlot >= 0 then
            GameFlow.RequestSell(self.focusOwnedSlot)
        end
        self:Refresh()
    end)

    self:BindClick(self.ui.UndoBtn, function()
        GameFlow.RequestUndo()
        self:Refresh()
    end)

    self:BindClick(self.ui.CloseBtn, function()
        GameFlow.CloseShop()
    end)

    return self
end

function Shop:Refresh()
    self:RefreshCatalog()
    self:RefreshDetail()
    self:RefreshUndo()
    local status = GameFlow.GetShopStatus() or ""
    if self.ui.StateText ~= nil then
        self.ui.StateText.text = status
    end
end

function Shop:RefreshCatalog()
    local count = GameFlow.GetShopItemCount()
    if _G._ShopSelectedEquipmentId ~= nil then
        self.selectedEquipmentId =
            _G._ShopSelectedEquipmentId
    end
    local cells = {}
    for i = 0, count - 1 do
        local equipmentId = GameFlow.GetShopItemId(i)
        cells[#cells + 1] = {
            EquipmentId = equipmentId,
            Name = GameFlow.GetShopItemName(i),
            Icon = GameFlow.GetShopItemIcon(i),
            Price = GameFlow.GetShopItemPrice(i),
            Selected = (equipmentId ==
                self.selectedEquipmentId),
            Owned = GameFlow.IsEquipmentOwned(
                equipmentId),
        }
    end
    if self.ui.EquipmentList ~= nil then
        self.ui.EquipmentList:SetItems(cells)
    end
end

function Shop:RefreshDetail()
    local id = self.selectedEquipmentId
    self.focusOwnedSlot = -1
    if id <= 0 then
        if self.ui.Detail ~= nil then
            self.ui.Detail:SetActive(false)
        end
        if self.ui.SellBtn ~= nil then
            self.ui.SellBtn.interactable = false
        end
        return
    end
    local slotCount = GameFlow.GetLocalEquipmentSlotCount()
    for slot = 0, slotCount - 1 do
        if GameFlow.GetLocalEquipmentSlotId(slot) == id then
            self.focusOwnedSlot = slot
            break
        end
    end
    if self.ui.Detail ~= nil then
        self.ui.Detail:SetActive(true)
    end
    if self.ui.EquipmentName ~= nil then
        self.ui.EquipmentName.text =
            GameFlow.GetShopItemNameById(id) or ""
    end
    if self.ui.EquipmentCost ~= nil then
        self.ui.EquipmentCost.text =
            "Cost: " ..
            (GameFlow.GetShopItemPriceById(id) or 0)
    end
    if self.ui.EquipmentEffectDescription ~= nil then
        self.ui.EquipmentEffectDescription.text =
            GameFlow.GetShopItemEffectById(id) or ""
    end
    if self.ui.PropertyBonusDescription ~= nil then
        self.ui.PropertyBonusDescription.text =
            GameFlow.GetShopItemStatById(id) or ""
    end
    if self.ui.SellBtn ~= nil then
        self.ui.SellBtn.interactable =
            self.focusOwnedSlot >= 0
    end
end

function Shop:RefreshUndo()
    if self.ui.UndoBtn ~= nil then
        self.ui.UndoBtn.interactable =
            GameFlow.CanUndo()
    end
end

return Shop

-- Shop catalog cell (design v9.1 11)
local UICellBase = require("UI.Core.UICellBase")

local ShopCell = setmetatable({}, { __index = UICellBase })
ShopCell.__index = ShopCell

function ShopCell.New(refs)
    local self = UICellBase.New(ShopCell, refs)
    self.equipmentId = 0

    if self.ui.Button ~= nil then
        self:BindClick(self.ui.Button, function()
            if self.equipmentId > 0 then
                _G._ShopSelectedEquipmentId =
                    self.equipmentId
                if _G._ShopRefresh ~= nil then
                    _G._ShopRefresh()
                end
            end
        end)
    end

    return self
end

function ShopCell:Bind(data)
    UICellBase.Bind(self, data)
    self.equipmentId = data.EquipmentId
    if self.ui.Cost ~= nil then
        self.ui.Cost.text =
            tostring(data.Price or 0)
    end
    if self.ui.SelectTip ~= nil then
        self.ui.SelectTip:SetActive(
            data.Selected == true)
    end
    if self.ui.OwnedMask ~= nil then
        self.ui.OwnedMask:SetActive(
            data.Owned == true)
    end
end

return ShopCell

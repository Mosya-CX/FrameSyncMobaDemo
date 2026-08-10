-- Hero-select status cell: shows one player's name + chosen hero icon.
-- The cell data is produced by Select.lua from the synced lobby state.
local UICellBase = require("UI.Core.UICellBase")

local ICON_PLACEHOLDER =
    Color(0.25, 0.25, 0.25, 0.9)

local SelectStatusCell =
    setmetatable(
        {},
        { __index = UICellBase })
SelectStatusCell.__index =
    SelectStatusCell

function SelectStatusCell.New(refs)
    return UICellBase.New(
        SelectStatusCell,
        refs)
end

function SelectStatusCell:Bind(data)
    UICellBase.Bind(self, data)
    if self.ui.PlayerNameText ~= nil then
        self.ui.PlayerNameText.text =
            data.Name or ""
    end
    if self.ui.SelectHeroIcon ~= nil then
        if data.Avatar ~= nil then
            self.ui.SelectHeroIcon.sprite =
                data.Avatar
            self.ui.SelectHeroIcon.color =
                Color.white
        else
            self.ui.SelectHeroIcon.sprite =
                nil
            self.ui.SelectHeroIcon.color =
                ICON_PLACEHOLDER
        end
        -- A locked (confirmed) player keeps full opacity; an unselected
        -- player's icon is dimmed slightly.
        if data.Locked ~= true then
            self.ui.SelectHeroIcon.color =
                Color(
                    self.ui.SelectHeroIcon.color.r,
                    self.ui.SelectHeroIcon.color.g,
                    self.ui.SelectHeroIcon.color.b,
                    0.45)
        end
    end
end

return SelectStatusCell

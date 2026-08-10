-- Hero select cell (design v9.1 9.3.4)
local UICellBase = require("UI.Core.UICellBase")

local ICON_PLACEHOLDER =
    Color(0.25, 0.25, 0.25, 0.9)

local HeroCell = setmetatable({}, { __index = UICellBase })
HeroCell.__index = HeroCell

function HeroCell.New(refs)
    local self = UICellBase.New(HeroCell, refs)
    self.heroId = 0

    if self.ui.Button ~= nil then
        self:BindClick(self.ui.Button, function()
            if self.heroId > 0 then
                GameFlow.ChooseHero(self.heroId)
                _G._HeroSelectedId = self.heroId
                if _G._HeroRefresh ~= nil then
                    _G._HeroRefresh()
                end
            end
        end)
    end

    return self
end

function HeroCell:Bind(data)
    UICellBase.Bind(self, data)
    self.heroId = data.HeroId
    if self.ui.NameText ~= nil then
        self.ui.NameText.text = data.Name or ""
    end
    if self.ui.HeadIcon ~= nil then
        if data.Avatar ~= nil then
            self.ui.HeadIcon.sprite = data.Avatar
            self.ui.HeadIcon.color = Color.white
        else
            self.ui.HeadIcon.sprite = nil
            self.ui.HeadIcon.color =
                ICON_PLACEHOLDER
        end
    end
    if self.ui.SelectTip ~= nil then
        self.ui.SelectTip:SetActive(
            data.Selected == true)
    end
    if self.ui.Button ~= nil then
        self.ui.Button.interactable = data.Available
    end
end

return HeroCell

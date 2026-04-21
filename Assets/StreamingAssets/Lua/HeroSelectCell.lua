local UICellBase = require "UICellBase"
local HeroSelectCell = UICellBase.New()

-- HeadIcon 表示英雄头像的Image组件
-- NameText 表示英雄名字的TMP_Text组件
-- SelectButton 表示选择的Button组件
-- SelectTip 表示选择提示层的RectTransform组件

function HeroSelectCell:Init()
    self.info = nil
    self.clickCallback = nil

    if self.SelectTip ~= nil then
        self.SelectTip.gameObject:SetActive(false)
    end

    if self.SelectButton ~= nil then
        self.SelectButton.onClick:RemoveAllListeners()
        self.SelectButton.onClick:AddListener(function()
            self:OnClick()
        end)
    end
end

function HeroSelectCell:SetData(info, clickCallback)
    self.info = info
    self.clickCallback = clickCallback

    if self.HeadIcon ~= nil then
        self.HeadIcon.sprite = info.Head
    end

    if self.NameText ~= nil then
        self.NameText.text = info.Name or ""
    end
end

function HeroSelectCell:SetSelected(selected)
    if self.SelectTip ~= nil then
        self.SelectTip.gameObject:SetActive(selected)
    end
end

function HeroSelectCell:OnClick()
    if self.clickCallback ~= nil and self.info ~= nil then
        self.clickCallback(self.info)
    end
end

return HeroSelectCell
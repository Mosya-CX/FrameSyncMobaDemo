-- Buff bar cell (user-added BuffBar; design v14.2 UI rules).
local UICellBase = require("UI.Core.UICellBase")

local BuffCell =
    setmetatable({}, { __index = UICellBase })
BuffCell.__index = BuffCell

local ICON_PLACEHOLDER =
    Color(0.3, 0.3, 0.3, 0.8)

function BuffCell.New(refs)
    local self = UICellBase.New(BuffCell, refs)
    self._lastStackLog = nil
    return self
end

function BuffCell:Bind(data)
    UICellBase.Bind(self, data)

    if self.ui.Icon ~= nil then
        if data.Icon ~= nil then
            self.ui.Icon.sprite = data.Icon
            self.ui.Icon.color = Color.white
        else
            self.ui.Icon.sprite = nil
            self.ui.Icon.color = ICON_PLACEHOLDER
        end
    end

    if self.ui.UsageLine ~= nil then
        if data.IsPermanent then
            self.ui.UsageLine.gameObject
                :SetActive(false)
        else
            self.ui.UsageLine.gameObject
                :SetActive(true)
            self.ui.UsageLine.value =
                data.TimeProgress or 0
        end
    end

    if self.ui.StackText ~= nil then
        if data.ShowStack then
            local stackText = self.ui.StackText
            stackText.text =
                tostring(data.Stacks or 1)
            stackText.gameObject
                :SetActive(true)
            if stackText.transform ~= nil then
                stackText.transform
                    :SetAsLastSibling()
            end
            stackText.color = Color.white
            stackText.fontSize = 14
        else
            self.ui.StackText.gameObject
                :SetActive(false)
        end
    end

    local logKey =
        tostring(data.ShowStack) .. ":" ..
            tostring(data.Stacks or -1) .. ":" ..
            tostring(self.ui.StackText == nil)
    if self._lastStackLog ~= logKey then
        self._lastStackLog = logKey
        print(
            "[HudBuffCell] name=" ..
                tostring(data.Name or "?") ..
                " showStack=" ..
                tostring(data.ShowStack) ..
                " stacks=" ..
                tostring(data.Stacks or -1) ..
                " stackTextNil=" ..
                tostring(self.ui.StackText == nil))
    end
end

return BuffCell

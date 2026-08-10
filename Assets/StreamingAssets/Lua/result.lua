-- Result page (design v9.1 9.5): shows the local team's win/loss icons.
local UIBase = require("UI.Core.UIBase")

local Result = setmetatable({}, { __index = UIBase })
Result.__index = Result

function Result.New(refs)
    local self = UIBase.New(Result, refs)

    self:BindClick(self.ui.ContinueBtn, function()
        GameFlow.ReturnMainMenu()
    end)

    return self
end

function Result:Refresh()
    local victory = GameFlow.IsLocalTeamVictory()
    local draw = GameFlow.LastMatchDraw()

    if self.ui.VictoryIcon ~= nil then
        self.ui.VictoryIcon:SetActive(victory)
    end
    if self.ui.DefeatIcon ~= nil then
        self.ui.DefeatIcon:SetActive(
            not victory and not draw)
    end
    if self.ui.TitleText ~= nil then
        self.ui.TitleText.text =
            victory and "Victory"
            or (draw and "Draw" or "Defeat")
    end
end

return Result

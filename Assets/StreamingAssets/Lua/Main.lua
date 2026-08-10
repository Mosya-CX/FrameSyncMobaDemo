-- Main menu page (design v9.1 9.1)
local UIBase = require("UI.Core.UIBase")

local Main = setmetatable({}, { __index = UIBase })
Main.__index = Main

function Main.New(refs)
    local self = UIBase.New(Main, refs)

    self:BindClick(self.ui.StartBtn, function()
        GameFlow.StartMatchmaking()
    end)

    self:BindClick(self.ui.QuitBtn, function()
        GameFlow.QuitApplication()
    end)

    return self
end

function Main:Refresh()
    self.ui.NameText.text = GameFlow.AccountDisplayName or ""
    self.ui.StartBtn.interactable = GameFlow.CanStartMatchmaking()
end

return Main

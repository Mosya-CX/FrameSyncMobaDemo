-- Matchmaking page (design v9.1 9.2)
local UIBase = require("UI.Core.UIBase")
local UIFormat = require("UI.Core.UIFormat")

local Match = setmetatable({}, { __index = UIBase })
Match.__index = Match

function Match.New(refs)
    local self = UIBase.New(Match, refs)

    self:BindClick(self.ui.CancelBtn, function()
        GameFlow.CancelMatchmaking()
    end)

    return self
end

function Match:Refresh()
    local searching = GameFlow.IsSearching()
    self.ui.StateText.text = GameFlow.GetMatchStatus()
    self.ui.TimeText.text = UIFormat.Time(GameFlow.MatchElapsedSeconds())
    self.ui.SearchingRoot:SetActive(searching)
    self.ui.CancelBtn.interactable = GameFlow.CanCancelMatchmaking()
end

return Match

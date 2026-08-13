-- Loading page (design v9.1 9.4)
local UIBase = require("UI.Core.UIBase")

local Load = setmetatable({}, { __index = UIBase })
Load.__index = Load

function Load.New(refs)
    return UIBase.New(Load, refs)
end

function Load:Refresh()
    local value = GameFlow.LocalLoadProgress()
    value = math.max(0, math.min(1, value))
    self.ui.ProgressBar.value = value
    self.ui.ProgressText.text = string.format(
        "%s  %d%%",
        GameFlow.GetLoadingStatus(),
        math.floor(value * 100))
end

return Load

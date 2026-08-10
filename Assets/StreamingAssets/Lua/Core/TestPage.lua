-- Neutral EditMode test fixture page (not production content).
-- Records lifecycle calls into globals so C# tests can assert the LuaHost
-- proxy drives the page instance correctly.
local UIBase = require("UI.Core.UIBase")

local TestPage = setmetatable({}, { __index = UIBase })
TestPage.__index = TestPage

function TestPage.New(refs)
    local self = UIBase.New(TestPage, refs)
    self.sawRefs = refs ~= nil
    self.instanceId = 0
    return self
end

function TestPage:Show()
    _G._TestPageShow = (_G._TestPageShow or 0) + 1
end

function TestPage:Refresh()
    _G._TestPageRefresh = (_G._TestPageRefresh or 0) + 1
end

function TestPage:Hide()
    _G._TestPageHide = (_G._TestPageHide or 0) + 1
end

function TestPage:Dispose()
    _G._TestPageDispose = (_G._TestPageDispose or 0) + 1
end

return TestPage

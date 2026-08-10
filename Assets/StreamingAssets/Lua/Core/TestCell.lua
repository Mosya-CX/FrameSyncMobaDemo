-- Neutral EditMode test fixture cell (not production content).
local UICellBase = require("UI.Core.UICellBase")

local TestCell = setmetatable({}, { __index = UICellBase })
TestCell.__index = TestCell

function TestCell.New(refs)
    local self = UICellBase.New(TestCell, refs)
    return self
end

function TestCell:SetIndex(index)
    UICellBase.SetIndex(self, index)
    _G._TestCellIndex = index
end

function TestCell:Bind(data)
    UICellBase.Bind(self, data)
    _G._TestCellData = data
end

function TestCell:Dispose()
    _G._TestCellDispose = (_G._TestCellDispose or 0) + 1
    UICellBase.Dispose(self)
end

return TestCell

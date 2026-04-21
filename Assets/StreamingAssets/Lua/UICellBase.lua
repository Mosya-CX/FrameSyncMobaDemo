local CellBase = {}

function CellBase:New()
	local instance = {}
	setmetatable(instance, {__index = CellBase})
	return instance
end

function CellBase:Init()
end

return CellBase
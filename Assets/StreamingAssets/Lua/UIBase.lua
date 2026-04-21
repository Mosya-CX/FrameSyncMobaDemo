local UIBase = {}

function UIBase.New()
	local instance = {}
	setmetatable(instance, {__index = UIBase})
	return instance
end

function UIBase:Init()
end

function UIBase:OnEnable()
end

function UIBase:OnDisable()
end

function UIBase:Update()
end

function UIBase:OnDestroy()
end

function UIBase:Destroy()
	UIManager.DestroyPanel(self.behaviour)
end

return UIBase
-- Page Lua base class (design v9.1 section 6.3)
local UIBase = {}
UIBase.__index = UIBase

function UIBase.New(class, refs)
    local self = setmetatable({}, class or UIBase)

    self.ui = refs

    self._unityListeners = {}
    self._unsubscribers = {}
    self._disposed = false

    return self
end

function UIBase:BindEvent(event, callback)
    event:AddListener(callback)

    table.insert(self._unityListeners, {
        Event = event,
        Callback = callback
    })

    return callback
end

function UIBase:BindClick(button, callback)
    return self:BindEvent(button.onClick, callback)
end

function UIBase:AddUnsubscriber(callback)
    table.insert(self._unsubscribers, callback)
end

function UIBase:UnbindUnityEvents()
    for i = #self._unityListeners, 1, -1 do
        local item = self._unityListeners[i]

        if item.Event ~= nil and item.Callback ~= nil then
            item.Event:RemoveListener(item.Callback)
        end

        self._unityListeners[i] = nil
    end
end

function UIBase:UnsubscribeRuntimeEvents()
    for i = #self._unsubscribers, 1, -1 do
        local callback = self._unsubscribers[i]

        if callback ~= nil then
            callback()
        end

        self._unsubscribers[i] = nil
    end
end

function UIBase:Show()
end

function UIBase:Refresh()
end

function UIBase:Hide()
    self:UnsubscribeRuntimeEvents()
end

function UIBase:Dispose()
    if self._disposed then
        return
    end

    self._disposed = true

    self:UnsubscribeRuntimeEvents()
    self:UnbindUnityEvents()

    self.ui = nil
end

return UIBase

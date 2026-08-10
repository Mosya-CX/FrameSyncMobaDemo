-- Cell Lua base class (design v9.1 section 6.4)
local UICellBase = {}
UICellBase.__index = UICellBase

function UICellBase.New(class, refs)
    local self = setmetatable({}, class or UICellBase)

    self.ui = refs
    self.data = nil
    self.index = -1

    self._listeners = {}
    self._disposed = false

    return self
end

function UICellBase:BindEvent(event, callback)
    event:AddListener(callback)

    table.insert(self._listeners, {
        Event = event,
        Callback = callback
    })

    return callback
end

function UICellBase:BindClick(button, callback)
    return self:BindEvent(button.onClick, callback)
end

function UICellBase:SetIndex(index)
    self.index = index
end

function UICellBase:Bind(data)
    self.data = data
end

function UICellBase:Dispose()
    if self._disposed then
        return
    end

    self._disposed = true

    for i = #self._listeners, 1, -1 do
        local item = self._listeners[i]

        if item.Event ~= nil and item.Callback ~= nil then
            item.Event:RemoveListener(item.Callback)
        end

        self._listeners[i] = nil
    end

    self.data = nil
    self.ui = nil
end

return UICellBase

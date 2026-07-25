-- ui_bootstrap.lua
-- FrameSyncMoba Lua UI entry point.
-- Receives per-tick UI data from LuaBridge and updates Unity UI GameObjects.
--
-- Design: MOBA_UI_Lua_System_Design_v9_1
-- Lua reads C# state through read-only Lua globals.
-- Presentation never writes to Gameplay.

local UI = {}

--- Called each frame after LuaBridge.PushTickData() completes.
--- Reads HUD table globals and updates UI elements.
function UI.Refresh()
    local hud = HUD
    if hud == nil then
        return
    end

    -- Health bar
    if UI.healthSlider ~= nil and hud.MaxHealth > 0 then
        local ratio = hud.CurrentHealth / hud.MaxHealth
        if ratio < 0 then ratio = 0 end
        if ratio > 1 then ratio = 1 end
        UI.healthSlider.value = ratio
    end

    -- Gold display
    if UI.goldText ~= nil then
        UI.goldText.text = tostring(hud.CurrentGold)
    end

    -- Cooldown indicators (slots 0-3)
    if UI.cooldownFills ~= nil then
        for slot = 0, 3 do
            local fill = UI.cooldownFills[slot]
            if fill ~= nil then
                local remaining = hud["CooldownRemaining" .. slot] or 0
                local total = hud["CooldownTotal" .. slot] or 1
                if remaining > 0 and total > 0 then
                    fill.fillAmount = 1 - (remaining / total)
                else
                    fill.fillAmount = 1
                end
            end
        end
    end

    -- Unit level
    if UI.levelText ~= nil then
        UI.levelText.text = tostring(hud.UnitLevel)
    end
end

--- Initialize UI references from Unity GameObjects.
--- Called once after Lua VM is ready and scene is loaded.
function UI.Initialize(healthSlider, goldText, cooldownFills, levelText)
    UI.healthSlider = healthSlider
    UI.goldText = goldText
    UI.cooldownFills = cooldownFills
    UI.levelText = levelText
end

return UI

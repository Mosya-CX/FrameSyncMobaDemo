-- hud.lua -- In-game HUD overlay
-- Displays: health bar, cast resource bar, cooldown indicators, buff/debuff icons, gold, KDA
-- UI/Lua v9.1 section 1.4
-- Data delivered by LuaBridge.PushTickData into HUD global table

local HUD = {}

-- Internal state
local lastHealthPct = 1.0
local lastResourcePct = 1.0
local lastGold = 0
local lastKills = 0
local lastDeaths = 0
local lastAssists = 0

-- Called each frame by LuaBridge after PushTickData
function HUD.Update()
    -- Read from HUD global table (set by LuaBridge)
    local healthCurrent = tonumber(HUD.CurrentHealth) or 0
    local healthMax = tonumber(HUD.MaxHealth) or 1
    local gold = tonumber(HUD.CurrentGold) or 0
    local kills = tonumber(HUD.Kills) or 0
    local deaths = tonumber(HUD.Deaths) or 0
    local assists = tonumber(HUD.Assists) or 0

    -- Health bar
    local healthPct = 0
    if healthMax > 0 then
        healthPct = math.min(1.0, math.max(0.0, healthCurrent / healthMax))
    end
    if healthPct ~= lastHealthPct then
        HUD.UpdateHealthBar(healthPct, healthCurrent, healthMax)
        lastHealthPct = healthPct
    end

    -- Resource bar (cast resource)
    local resourceCurrent = tonumber(HUD.ResourceCurrent) or 0
    local resourceMax = tonumber(HUD.ResourceMax) or 1
    local resourcePct = 0
    if resourceMax > 0 then
        resourcePct = math.min(1.0, math.max(0.0, resourceCurrent / resourceMax))
    end
    if resourcePct ~= lastResourcePct then
        HUD.UpdateResourceBar(resourcePct)
        lastResourcePct = resourcePct
    end

    -- Cooldowns
    HUD.UpdateCooldowns()

    -- Gold display
    if gold ~= lastGold then
        HUD.UpdateGoldDisplay(gold)
        lastGold = gold
    end

    -- KDA display
    if kills ~= lastKills or deaths ~= lastDeaths or assists ~= lastAssists then
        HUD.UpdateKDA(kills, deaths, assists)
        lastKills = kills
        lastDeaths = deaths
        lastAssists = assists
    end
end

function HUD.UpdateHealthBar(pct, current, maxHealth)
    -- Set fill amount on health bar image
    -- UI system handles the visual through healthBarFill Image.fillAmount
    local bar = HUD.FindElement("HealthBarFill")
    if bar ~= nil then
        bar.fillAmount = pct
    end
    -- Update health text: "current / max"
    local text = HUD.FindElement("HealthText")
    if text ~= nil then
        text.text = string.format("%d / %d", math.floor(current), math.floor(maxHealth))
    end
end

function HUD.UpdateResourceBar(pct)
    local bar = HUD.FindElement("ResourceBarFill")
    if bar ~= nil then
        bar.fillAmount = pct
    end
end

function HUD.UpdateCooldowns()
    for i = 0, 3 do
        local remaining = tonumber(HUD["CooldownRemaining" .. i]) or 0
        local total = tonumber(HUD["CooldownTotal" .. i]) or 1
        local pct = 0
        if total > 0 then
            pct = remaining / total
        end
        -- Update cooldown overlay fill + text for ability slot i
        local overlay = HUD.FindElement("CooldownOverlay" .. i)
        if overlay ~= nil then
            overlay.fillAmount = pct
            overlay.gameObject:SetActive(remaining > 0)
        end
        local cdText = HUD.FindElement("CooldownText" .. i)
        if cdText ~= nil then
            if remaining > 0 then
                local seconds = math.ceil(remaining / 30.0) -- 30 ticks/sec
                cdText.text = tostring(seconds)
            else
                cdText.text = ""
            end
        end
    end
end

function HUD.UpdateGoldDisplay(gold)
    local text = HUD.FindElement("GoldText")
    if text ~= nil then
        text.text = tostring(gold)
    end
end

function HUD.UpdateKDA(kills, deaths, assists)
    local text = HUD.FindElement("KDAText")
    if text ~= nil then
        text.text = string.format("%d / %d / %d", kills, deaths, assists)
    end
end

-- Unity-side element lookup via cached references
local _elementCache = {}
function HUD.FindElement(name)
    if _elementCache[name] == nil then
        local go = UnityEngine.GameObject.Find("HUDCanvas/" .. name)
        if go ~= nil then
            _elementCache[name] = go:GetComponent("UnityEngine.UI.Image") or
                                   go:GetComponent("UnityEngine.UI.Text")
        else
            _elementCache[name] = false -- mark as not found
        end
    end
    if _elementCache[name] == false then return nil end
    return _elementCache[name]
end

-- Called when the HUD page is shown
function HUD.OnShow()
    HUD.FindElement("HUDCanvas")
end

-- Called when the HUD page is hidden
function HUD.OnHide()
end

return HUD

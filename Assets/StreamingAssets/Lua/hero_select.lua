-- hero_select.lua
-- Hero select screen Lua script.
-- Reads available hero list and handles selection events.
-- (ExecPlan 0093, UI/Lua Design v9.1)

local selectedHeroId = 0
local isLocked = false

function OnHeroClicked(heroId)
    if isLocked then return end
    selectedHeroId = heroId
    print("[HeroSelect] Selected Hero " .. heroId)
end

function OnLockIn()
    if isLocked or selectedHeroId <= 0 then return end
    isLocked = true
    print("[HeroSelect] Locked Hero " .. selectedHeroId)
end

function GetSelectedHero()
    return selectedHeroId
end

function IsLocked()
    return isLocked
end

function Reset()
    selectedHeroId = 0
    isLocked = false
end

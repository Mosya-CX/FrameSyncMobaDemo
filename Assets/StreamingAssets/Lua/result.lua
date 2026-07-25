-- result.lua
-- Match result screen Lua script.
-- Reads HUD table populated by LuaBridge each tick.
-- (ExecPlan 0092, UI/Lua Design v9.1)

local function OnResultShow()
    local winner = HUD.WinnerTeam or 0
    local reason = HUD.EndReason or "Unknown"
    local duration = HUD.MatchDuration or 0

    local mins = math.floor(duration / 60)
    local secs = duration % 60
    local timeStr = string.format("%d:%02d", mins, secs)

    if winner == 0 then
        print("[Result] Draw after " .. timeStr)
    else
        print("[Result] Team " .. winner .. " wins after " .. timeStr .. " (" .. reason .. ")")
    end

    -- KDA from Scoreboard table
    local names = Scoreboard.Names or {}
    local kills = Scoreboard.Kills or {}
    local deaths = Scoreboard.Deaths or {}
    local assists = Scoreboard.Assists or {}

    for i = 1, #names do
        print(string.format("[Result] %s - K:%d D:%d A:%d",
            names[i], kills[i] or 0, deaths[i] or 0, assists[i] or 0))
    end
end

-- Called by LuaRuntime when result screen is triggered
function ShowResult()
    OnResultShow()
end

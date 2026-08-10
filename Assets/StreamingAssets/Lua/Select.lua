-- Hero select page (design v9.1 9.3)
local UIBase = require("UI.Core.UIBase")

local Select = setmetatable({}, { __index = UIBase })
Select.__index = Select

function Select.New(refs)
    local self = UIBase.New(Select, refs)

    self.selectedHeroId = 0
    self.confirmed = false

    _G._HeroRefresh = function()
        self:Refresh()
    end

    self:BindClick(self.ui.ConfirmButton, function()
        if self.confirmed then
            return
        end
        if self.selectedHeroId > 0 then
            self.confirmed = true
            GameFlow.ConfirmHero()
            self:Refresh()
        end
    end)

    return self
end

local function getAvatarByHeroId(heroId)
    if heroId == nil or heroId <= 0 then
        return nil
    end
    local count = GameFlow.HeroSelectCount()
    for i = 1, count do
        if GameFlow.GetHeroSelectId(i) == heroId then
            return GameFlow.GetHeroSelectAvatar(i)
        end
    end
    return nil
end

local function fillStatus(list, teamId)
    if list == nil then
        return
    end
    local items = {}
    local count = GameFlow.GetSelectStatusCount()
    for i = 1, count do
        if GameFlow.GetSelectStatusTeam(i) == teamId then
            local heroId = GameFlow.GetSelectStatusHeroId(i)
            items[#items + 1] = {
                Name = GameFlow.GetSelectStatusIsMe(i)
                        and "Me"
                        or GameFlow.GetSelectStatusName(i),
                HeroId = heroId,
                Avatar = getAvatarByHeroId(heroId),
                Locked = GameFlow.GetSelectStatusLocked(i),
            }
        end
    end
    list:SetItems(items)
end

function Select:Refresh()
    if not self.confirmed and
        _G._HeroSelectedId ~= nil then
        self.selectedHeroId = _G._HeroSelectedId
    end
    local count = GameFlow.HeroSelectCount()
    local cells = {}
    for i = 1, count do
        local heroId = GameFlow.GetHeroSelectId(i)
        cells[#cells + 1] = {
            HeroId = heroId,
            Name = GameFlow.GetHeroSelectName(i),
            Avatar = GameFlow.GetHeroSelectAvatar(i),
            Available = not self.confirmed and
                not GameFlow.IsHeroBlockedByTeammate(heroId),
            Selected = (heroId == self.selectedHeroId),
        }
    end
    self.ui.HeroList:SetItems(cells)

    -- 我方/敌方玩家选择状态（全端同步，实时刷新）
    local myTeam = GameFlow.GetSelectStatusMyTeam()
    if myTeam > 0 then
        fillStatus(self.ui.OurTeamSelectStatus, myTeam)
        for i = 1, GameFlow.GetSelectStatusCount() do
            if GameFlow.GetSelectStatusTeam(i) ~= myTeam then
                fillStatus(
                    self.ui.OpposingTeamSelectStatus,
                    GameFlow.GetSelectStatusTeam(i))
                break
            end
        end
    end

    self.ui.ConfirmButton.interactable =
        not self.confirmed and
        GameFlow.CanConfirmHero() and
        self.selectedHeroId > 0
end

return Select

-- Battle HUD page (design v9.1 10)
local UIBase = require("UI.Core.UIBase")

local HUD = setmetatable({}, { __index = UIBase })
HUD.__index = HUD

local ABILITY_SLOTS = {
    { Key = "Q", Index = 0 },
    { Key = "W", Index = 1 },
    { Key = "E", Index = 2 },
    { Key = "R", Index = 3 },
}

local ABILITY_ICONS = {
    { Key = "Q_Icon", Index = 0 },
    { Key = "W_Icon", Index = 1 },
    { Key = "E_Icon", Index = 2 },
    { Key = "R_Icon", Index = 3 },
}

local EQUIPMENT_SLOTS = {
    { Key = "EquipmentSlot1", Index = 0 },
    { Key = "EquipmentSlot2", Index = 1 },
    { Key = "EquipmentSlot3", Index = 2 },
    { Key = "EquipmentSlot4", Index = 3 },
    { Key = "EquipmentSlot5", Index = 4 },
    { Key = "EquipmentSlot6", Index = 5 },
}

local MAIN_STATS = {
    { Key = "AttackDamageText", Name = "AttackDamage" },
    { Key = "AbilityPowerText", Name = "AbilityPower" },
    { Key = "ArmorText", Name = "Armor" },
    { Key = "MagicResistText", Name = "MagicResist" },
    { Key = "AttackSpeedText", Name = "AttackSpeed" },
    { Key = "SkillHasteText", Name = "SkillHaste" },
    { Key = "CritChanceText", Name = "CritChance" },
    { Key = "MoveSpeedText", Name = "MoveSpeed" },
}

local EXTEND_STATS = {
    { Key = "RegenerationText", Name = "Regeneration" },
    { Key = "HealAndShieldPowerText", Name = "HealAndShieldPower" },
    { Key = "ArmorPenetrationText", Name = "ArmorPenetration" },
    { Key = "MagicPenetrationText", Name = "MagicPenetration" },
    { Key = "LifeStealText", Name = "LifeSteal" },
    { Key = "OmnivampText", Name = "Omnivamp" },
    { Key = "AttackRangeText", Name = "AttackRange" },
    { Key = "TenacityText", Name = "Tenacity" },
}

function HUD.New(refs)
    local self = UIBase.New(HUD, refs)

    -- Local "point spent, waiting for the deterministic command to land"
    -- markers. The command executes on a later logic tick, so the button is
    -- disabled immediately on click to prevent double-spending, and is only
    -- re-enabled once PendingSkillPoints actually changed (design v9.1 10.7).
    self._pendingAlloc = {}

    for i = 1, #ABILITY_SLOTS do
        local entry = ABILITY_SLOTS[i]
        local btn =
            self.ui[entry.Key .. "_LevelUpBtn"]
        if btn ~= nil then
            self:BindClick(btn, function()
                self._pendingAlloc[entry.Index] =
                    GameFlow.GetLocalPendingSkillPoints()
                btn.interactable = false
                GameFlow.AllocateLocalSkillPoint(
                    entry.Index)
            end)
        end
    end

    for i = 1, #EQUIPMENT_SLOTS do
        local entry = EQUIPMENT_SLOTS[i]
        if self.ui[entry.Key] ~= nil then
            self:BindClick(self.ui[entry.Key], function()
                GameFlow.FocusShopEquipment(
                    entry.Index,
                    GameFlow.GetLocalEquipmentSlotId(
                        entry.Index))
            end)
        end
    end

    return self
end

function HUD:Refresh()
    self:RefreshHead()
    self:RefreshVitals()
    self:RefreshPing()
    self:RefreshLevel()
    self:RefreshCooldowns()
    self:RefreshIcons()
    self:RefreshSkillLockups()
    self:RefreshGold()
    self:RefreshMatchBar()
    self:RefreshStats()
    self:RefreshPassive()
    self:RefreshEquipment()
    self:RefreshExpandPanel()
    self:RefreshBuffs()
end

function HUD:RefreshLevel()
    if self.ui.LevelText ~= nil then
        self.ui.LevelText.text =
            tostring(GameFlow.GetLocalLevel())
    end
end

function HUD:RefreshSkillLockups()
    local pending =
        GameFlow.GetLocalPendingSkillPoints()
    for i = 1, #ABILITY_SLOTS do
        local entry = ABILITY_SLOTS[i]
        local lock =
            self.ui[entry.Key .. "_LockMask"]
        local btn =
            self.ui[entry.Key .. "_LevelUpBtn"]
        local level =
            GameFlow.GetLocalAbilityLevel(
                entry.Index)
        if lock ~= nil then
            lock.gameObject:SetActive(
                level <= 0)
        end
        if btn ~= nil then
            -- Design v9.1 10.7: with pending points every bound slot shows
            -- its upgrade button; interactable reflects whether this slot
            -- can actually take the point right now (level gate / max rank
            -- / casting state). With zero points all buttons hide.
            btn.gameObject:SetActive(
                pending > 0)
            local waiting =
                self._pendingAlloc[entry.Index]
            if waiting ~= nil and
                pending ~= waiting then
                -- The spent point landed: the button follows the real
                -- authoritative state again.
                self._pendingAlloc[entry.Index] = nil
                waiting = nil
            end
            if waiting ~= nil then
                -- Clicked but the command has not executed yet: keep the
                -- button disabled so one click cannot double-spend.
                btn.interactable = false
            else
                btn.interactable =
                    pending > 0 and
                    GameFlow
                        .CanAllocateLocalSkillPoint(
                            entry.Index)
            end
        end
    end
end

function HUD:RefreshPing()
    if self.ui.PingText ~= nil then
        local ping = GameFlow.GetLocalPing()
        local node = self.ui.PingText.gameObject
        if ping ~= nil and ping >= 0 and node ~= nil then
            node:SetActive(true)
            self.ui.PingText.text = ping .. " ms"
        elseif node ~= nil then
            node:SetActive(false)
        end
    end
end

function HUD:RefreshHead()
    if self.ui.HeadIcon ~= nil then
        local avatar = GameFlow.GetLocalHeroAvatar()
        if avatar ~= nil then
            self.ui.HeadIcon.sprite = avatar
            self.ui.HeadIcon.color = Color.white
        end
    end
end

function HUD:RefreshVitals()
    local hp = GameFlow.GetLocalHp()
    local maxHp = GameFlow.GetLocalMaxHp()
    if self.ui.HealthSlider ~= nil then
        self.ui.HealthSlider.value =
            maxHp > 0 and (hp / maxHp) or 0
    end
    if self.ui.HealthText ~= nil then
        self.ui.HealthText.text =
            hp .. " / " .. maxHp
    end

    local resource = GameFlow.GetLocalResource()
    local maxResource =
        GameFlow.GetLocalMaxResource()
    if self.ui.ManaSlider ~= nil then
        self.ui.ManaSlider.value =
            maxResource > 0 and
                (resource / maxResource) or 0
    end
    if self.ui.ManaText ~= nil then
        self.ui.ManaText.text =
            resource .. " / " .. maxResource
    end

    local exp = GameFlow.GetLocalExp()
    local nextExp =
        GameFlow.GetLocalNextLevelExp()
    if self.ui.ExpSlider ~= nil then
        self.ui.ExpSlider.value =
            nextExp > 0 and (exp / nextExp) or 0
    end
end

function HUD:RefreshCooldowns()
    for i = 1, #ABILITY_SLOTS do
        local entry = ABILITY_SLOTS[i]
        local remaining =
            GameFlow.GetCooldownRemaining(
                entry.Index)
        local total =
            GameFlow.GetCooldownTotal(
                entry.Index)
        local ratio =
            total > 0 and (remaining / total) or 0

        local mask =
            self.ui[entry.Key .. "_CooldownMask"]
        local text =
            self.ui[entry.Key .. "_CooldownText"]
        if mask ~= nil then
            mask.fillAmount = ratio
            mask.gameObject:SetActive(
                remaining > 0)
        end
        if text ~= nil then
            text.text =
                remaining > 0
                and string.format(
                    "%.1f",
                    GameFlow.GetCooldownRemainingSeconds(
                        entry.Index))
                or ""
        end
    end
end

function HUD:RefreshIcons()
    for i = 1, #ABILITY_ICONS do
        local entry = ABILITY_ICONS[i]
        local icon = self.ui[entry.Key]
        if icon ~= nil then
            local abilityId =
                GameFlow.GetActiveAbilityId(
                    entry.Index)
            local hasAbility = abilityId > 0
            icon.gameObject:SetActive(hasAbility)
            if hasAbility then
                local sprite =
                    GameFlow.GetActiveAbilityIcon(
                        entry.Index)
                if sprite ~= nil then
                    icon.sprite = sprite
                    icon.color = Color.white
                end
            end
        end
    end
end

function HUD:RefreshGold()
    if self.ui.Gold ~= nil then
        self.ui.Gold.text =
            "Gold: " .. GameFlow.GetHudGold()
    end
end

function HUD:RefreshMatchBar()
    if self.ui.TimeText ~= nil then
        local seconds =
            math.floor(
                GameFlow.GetGameElapsedSeconds() +
                    0.5)
        local minutes = math.floor(
            seconds / 60)
        self.ui.TimeText.text = string.format(
            "%02d:%02d",
            minutes,
            seconds - minutes * 60)
    end
    if self.ui.TeamScoreText ~= nil then
        self.ui.TeamScoreText.text = string.format(
            '<color="red">%d</color> vs ' ..
                '<color="blue">%d</color>',
            GameFlow.GetRedTeamScore(),
            GameFlow.GetBlueTeamScore())
    end
    if self.ui.KDAText ~= nil then
        self.ui.KDAText.text = string.format(
            "%d / %d / %d",
            GameFlow.GetLocalKills(),
            GameFlow.GetLocalDeaths(),
            GameFlow.GetLocalAssists())
    end
    if self.ui.CreepScoreText ~= nil then
        self.ui.CreepScoreText.text =
            tostring(GameFlow.GetLocalCreepScore())
    end
end

function HUD:RefreshStats()
    for i = 1, #MAIN_STATS do
        local entry = MAIN_STATS[i]
        if self.ui[entry.Key] ~= nil then
            self.ui[entry.Key].text =
                GameFlow.GetLocalStatText(entry.Name)
        end
    end
    for i = 1, #EXTEND_STATS do
        local entry = EXTEND_STATS[i]
        if self.ui[entry.Key] ~= nil then
            self.ui[entry.Key].text =
                GameFlow.GetLocalStatText(entry.Name)
        end
    end
end

function HUD:RefreshPassive()
    local mask = self.ui.Passive_CooldownMask
    local text = self.ui.Passive_CooldownText
    if self.ui.PassiveIcon ~= nil then
        local icon = GameFlow.GetPassiveAbilityIcon()
        if icon ~= nil then
            self.ui.PassiveIcon.sprite = icon
            self.ui.PassiveIcon.color = Color.white
            self.ui.PassiveIcon.gameObject
                :SetActive(true)
        else
            self.ui.PassiveIcon.gameObject
                :SetActive(false)
        end
    end
    if mask == nil then
        return
    end

    local remaining =
        GameFlow.GetPassiveCooldownRemainingSeconds()
    local total =
        GameFlow.GetPassiveCooldownTotalSeconds()
    local ratio =
        total > 0 and (remaining / total) or 0

    mask.fillAmount = ratio
    mask.gameObject:SetActive(remaining > 0)
    if text ~= nil then
        text.text =
            remaining > 0
            and string.format("%.1f", remaining)
            or ""
    end
end

function HUD:RefreshEquipment()
    for i = 1, #EQUIPMENT_SLOTS do
        local entry = EQUIPMENT_SLOTS[i]
        local button = self.ui[entry.Key]
        if button == nil then
            return
        end
        local slot = button.image
        local icon =
            GameFlow.GetLocalEquipmentSlotIcon(
                entry.Index)
        if icon ~= nil then
            slot.sprite = icon
            slot.color = Color.white
        else
            local hasItem =
                GameFlow.GetLocalEquipmentSlotId(
                    entry.Index) > 0
            slot.sprite = nil
            slot.color = hasItem
                and Color(0.5, 0.5, 0.5, 0.8)
                or Color(0.2, 0.2, 0.2, 0.6)
        end
    end
end

function HUD:RefreshExpandPanel()
    if self.ui.ExtendPropertyRoot == nil or
        self.ui.MainPropertyRoot == nil then
        return
    end

    local expanded =
        GameFlow.IsExpandStatsHeld()
    self.ui.ExtendPropertyRoot:SetActive(
        expanded)
    self.ui.MainPropertyRoot:SetActive(
        true)
end

function HUD:RefreshBuffs()
    if self.ui.BuffList == nil then
        return
    end

    local count =
        GameFlow.GetLocalBuffCount()
    local cells = {}
    for i = 0, count - 1 do
        cells[#cells + 1] = {
            Icon = GameFlow.GetLocalBuffIcon(i),
            Name = GameFlow.GetLocalBuffName(i),
            Stacks = GameFlow.GetLocalBuffStacks(i),
            TimeProgress =
                GameFlow.GetLocalBuffTimeProgress(i),
            IsPermanent =
                GameFlow.GetLocalBuffIsPermanent(i),
            ShowStack =
                GameFlow.GetLocalBuffShowStack(i),
        }
    end
    self.ui.BuffList:SetItems(cells)
end

return HUD

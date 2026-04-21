local UIBase = require "UIBase"
local GameplayHUD = UIBase.New()

-- PassiveAbilitySlot 表示被动技能图标的Image组件
-- AbilitySlotQ 表示Q技能图标的Image组件
-- AbilitySlotW 表示W技能图标的Image组件
-- AbilitySlotE 表示E技能图标的Image组件
-- AbilitySlotR 表示R技能图标的Image组件
-- HealthBarSlider 表示当前生命值/最大生命值的Silder的组件
-- ManaBarSlider 表示当前法力值/最大法力值的Silder的组件
-- AttackDamageValueText 表示攻击力的TMP_Text组件
-- AbilityPowerValueText 表示法强的TMP_Text组件
-- ArmorValueText 表示物防的TMP_Text组件
-- MagicResistValueText 表示法防的TMP_Text组件
-- AttackSpeedValueText 表示攻速的TMP_Text组件
-- SkillHasteValueText 表示技能急速的TMP_Text组件
-- CritChanceValueText 表示暴击率的TMP_Text组件
-- MoveSpeedValueText 表示移动速度的TMP_Text组件
-- HealthValueText 表示当前生命值/最大生命值的TMP_Text组件
-- ManaValueText 表示当前法力值/最大法力值的TMP_Text组件

local UnitStatType = CS.UnitStatType

local function fp_to_number(v)
    if v == nil then
        return 0
    end

    local n = tonumber(tostring(v))
    if n == nil then
        return 0
    end

    return n
end

local function ratio(current, max)
    local c = fp_to_number(current)
    local m = fp_to_number(max)
    if m <= 0 then
        return 0
    end
    return c / m
end

local function set_text(comp, value)
    if comp ~= nil then
        comp.text = tostring(value)
    end
end

function GameplayHUD:Init()
    self:ClearView()
end

function GameplayHUD:Update()
    local hero = self:GetLocalHero()
    if hero == nil or hero.IsDead then
        self:ClearView()
        return
    end

    self:Refresh(hero)
end

function GameplayHUD:GetLocalHero()
    local player = CS.GamePlayer.Local
    if player ~= nil and player.ControlledHero ~= nil then
        return player.ControlledHero
    end

    local controller = CS.LocalController.Local
    if controller ~= nil and controller.LocalHero ~= nil then
        return controller.LocalHero
    end

    return nil
end

function GameplayHUD:Refresh(hero)
    local currentHealth = hero.CurrentHealth
    local maxHealth = hero.MaxHealth
    local currentMana = hero.CurrentMana
    local maxMana = hero.MaxMana

    local attackDamage = hero.AttackDamage
    local abilityPower = hero.AbilityPower
    local armor = hero.Armor
    local magicResist = hero.MagicResist
    local critChance = hero.CritChance
    local moveSpeed = hero.MoveSpeed
    local attackSpeed = hero.Stats:Get(UnitStatType.AttackSpeed)
    local skillHaste = hero.Stats:Get(UnitStatType.SkillHaste)

    if self.HealthBarSlider ~= nil then
        self.HealthBarSlider.value = ratio(currentHealth, maxHealth)
    end

    if self.ManaBarSlider ~= nil then
        self.ManaBarSlider.value = ratio(currentMana, maxMana)
    end

    set_text(self.HealthValueText, string.format("%d/%d", math.floor(fp_to_number(currentHealth)), math.floor(fp_to_number(maxHealth))))
    set_text(self.ManaValueText, string.format("%d/%d", math.floor(fp_to_number(currentMana)), math.floor(fp_to_number(maxMana))))

    set_text(self.AttackDamageValueText, math.floor(fp_to_number(attackDamage)))
    set_text(self.AbilityPowerValueText, math.floor(fp_to_number(abilityPower)))
    set_text(self.ArmorValueText, math.floor(fp_to_number(armor)))
    set_text(self.MagicResistValueText, math.floor(fp_to_number(magicResist)))
    set_text(self.AttackSpeedValueText, string.format("%.2f", fp_to_number(attackSpeed)))
    set_text(self.SkillHasteValueText, math.floor(fp_to_number(skillHaste)))
    set_text(self.CritChanceValueText, string.format("%.1f%%", fp_to_number(critChance) * 100))
    set_text(self.MoveSpeedValueText, math.floor(fp_to_number(moveSpeed)))
end

function GameplayHUD:ClearView()
    if self.HealthBarSlider ~= nil then
        self.HealthBarSlider.value = 0
    end

    if self.ManaBarSlider ~= nil then
        self.ManaBarSlider.value = 0
    end

    set_text(self.HealthValueText, "0/0")
    set_text(self.ManaValueText, "0/0")
    set_text(self.AttackDamageValueText, "0")
    set_text(self.AbilityPowerValueText, "0")
    set_text(self.ArmorValueText, "0")
    set_text(self.MagicResistValueText, "0")
    set_text(self.AttackSpeedValueText, "0")
    set_text(self.SkillHasteValueText, "0")
    set_text(self.CritChanceValueText, "0%")
    set_text(self.MoveSpeedValueText, "0")
end

return GameplayHUD
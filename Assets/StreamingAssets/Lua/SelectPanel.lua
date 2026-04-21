local UIBase = require "UIBase"
local SelectPanel = UIBase.New()

local Resources = CS.UnityEngine.Resources
local Object = CS.UnityEngine.Object
local HeroSelectInfoTableType = typeof(CS.HeroSelectInfoTable)

-- HeroSelectScroll 表示 ScrollRect 组件，content 下挂 cell
-- ConfirmButton 表示确认英雄选择的 Button 组件

function SelectPanel:Init()
    self.cells = {}
    self.selectedInfo = nil
    self.confirmed = false
    self.dataTable = nil

    if self.ConfirmButton ~= nil then
        self.ConfirmButton.onClick:RemoveAllListeners()
        self.ConfirmButton.onClick:AddListener(function()
            self:OnConfirmClick()
        end)
    end

    self:LoadTable()
    self:BuildCells()
    self:RestoreLocalSelection()
    self:RefreshView()
end

function SelectPanel:OnEnable()
    self.confirmed = GameManager:IsLocalHeroSelectionLocked()
    self:RestoreLocalSelection()
    self:RefreshSelection()
    self:RefreshView()
end

function SelectPanel:Update()
    local locked = GameManager:IsLocalHeroSelectionLocked()
    if locked ~= self.confirmed then
        self.confirmed = locked
        self:RefreshView()
    end
end

function SelectPanel:LoadTable()
    self.dataTable = Resources.Load("Data/HeroSelectInfoTable", HeroSelectInfoTableType)

    if self.dataTable == nil then
        Debug.LogError("[SelectPanel] 无法加载 Resources/Data/HeroSelectInfoTable")
    end
end

function SelectPanel:ClearCells()
    self.cells = {}

    if self.HeroSelectScroll == nil or self.HeroSelectScroll.content == nil then
        return
    end

    local content = self.HeroSelectScroll.content
    for i = content.childCount - 1, 0, -1 do
        local child = content:GetChild(i)
        if child ~= nil then
            Object.Destroy(child.gameObject)
        end
    end
end

function SelectPanel:BuildCells()
    self:ClearCells()

    if self.dataTable == nil or self.dataTable.heroSelectInfos == nil then
        return
    end

    if self.HeroSelectScroll == nil or self.HeroSelectScroll.content == nil then
        Debug.LogError("[SelectPanel] HeroSelectScroll 或 content 未配置")
        return
    end

    local list = self.dataTable.heroSelectInfos
    local content = self.HeroSelectScroll.content

    for i = 0, list.Count - 1 do
        local info = list[i]
        local cell = self.behaviour:CreateCell("HeroSelectCell", content)

        if cell ~= nil then
            cell:SetData(info, function(clickedInfo)
                self:OnClickHero(clickedInfo)
            end)

            table.insert(self.cells, cell)
        end
    end
end

function SelectPanel:RestoreLocalSelection()
    local selectedPrefabId = GameManager:GetLocalSelectedHeroPrefabId()
    if selectedPrefabId < 0 or self.dataTable == nil then
        return
    end

    local list = self.dataTable.heroSelectInfos
    for i = 0, list.Count - 1 do
        local info = list[i]
        if info.prefabId == selectedPrefabId then
            self.selectedInfo = info
            break
        end
    end

    self:RefreshSelection()
end

function SelectPanel:OnClickHero(info)
    if self.confirmed then
        return
    end

    self.selectedInfo = info
    self:RefreshSelection()
    self:RefreshView()
end

function SelectPanel:RefreshSelection()
    local selectedPrefabId = nil
    if self.selectedInfo ~= nil then
        selectedPrefabId = self.selectedInfo.prefabId
    end

    for _, cell in ipairs(self.cells) do
        local selected = false
        if cell.info ~= nil and selectedPrefabId ~= nil then
            selected = cell.info.prefabId == selectedPrefabId
        end

        if cell.SetSelected ~= nil then
            cell:SetSelected(selected)
        end
    end
end

function SelectPanel:RefreshView()
    if self.ConfirmButton ~= nil then
        self.ConfirmButton.interactable = (self.selectedInfo ~= nil and not self.confirmed)
    end
end

function SelectPanel:OnConfirmClick()
    if self.selectedInfo == nil then
        return
    end

    if self.confirmed then
        return
    end

    self.confirmed = true
    self:RefreshView()

    GameManager:ConfirmHeroSelectionFromLua(self.selectedInfo.prefabId)
end

function SelectPanel:OnDestroy()
    if self.ConfirmButton ~= nil then
        self.ConfirmButton.onClick:RemoveAllListeners()
    end
end

return SelectPanel
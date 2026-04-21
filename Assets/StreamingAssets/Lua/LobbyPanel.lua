local UIBase = require "UIBase"
local LobbyPanel = UIBase.New()

-- MatchingMask 开始匹配要启用的一个层级 Rect Transform组件
-- QuitGameButton 表示退出游戏的Button组件
-- MatchButton 表示开始匹配的Button组件
-- CancelMatchButton 表示取消匹配的Button组件，在MatchingMask层下

function LobbyPanel:Init()
    if self.MatchButton ~= nil then
        self.MatchButton.onClick:RemoveAllListeners()
        self.MatchButton.onClick:AddListener(function()
            GameManager:StartMatchmakingFromLua()
            self:RefreshView()
        end)
    end

    if self.CancelMatchButton ~= nil then
        self.CancelMatchButton.onClick:RemoveAllListeners()
        self.CancelMatchButton.onClick:AddListener(function()
            GameManager:CancelMatchmakingFromLua()
            self:RefreshView()
        end)
    end

    if self.QuitGameButton ~= nil then
        self.QuitGameButton.onClick:RemoveAllListeners()
        self.QuitGameButton.onClick:AddListener(function()
            CS.UnityEngine.Application.Quit()
        end)
    end

    self:RefreshView()
end

function LobbyPanel:OnEnable()
    self:RefreshView()
end

function LobbyPanel:Update()
    self:RefreshView()
end

function LobbyPanel:RefreshView()
    local matching = GameManager:IsMatchmaking()

    if self.MatchingMask ~= nil then
        self.MatchingMask.gameObject:SetActive(matching)
    end

    if self.MatchButton ~= nil then
        self.MatchButton.gameObject:SetActive(not matching)
    end
end

function LobbyPanel:OnDestroy()
    if self.MatchButton ~= nil then
        self.MatchButton.onClick:RemoveAllListeners()
    end

    if self.CancelMatchButton ~= nil then
        self.CancelMatchButton.onClick:RemoveAllListeners()
    end

    if self.QuitGameButton ~= nil then
        self.QuitGameButton.onClick:RemoveAllListeners()
    end
end

return LobbyPanel
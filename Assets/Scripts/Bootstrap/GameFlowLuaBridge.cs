using System;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.RuntimeConfig;
using UnityEngine;
using XLua;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Lua-callable application flow bridge. All members are static primitives
    /// so page Lua never touches transport/Gameplay internals. GameBootstrap
    /// binds the delegates at composition time.
    /// </summary>
    [LuaCallCSharp]
    public static class GameFlowLuaBridge
    {
        public static UIManager UiManager;

        public static string AccountDisplayName;

        public static Func<bool> CanStartMatchmaking =
            () => false;
        public static Action StartMatchmaking =
            () => { };
        public static Action CancelMatchmaking =
            () => { };
        public static Action QuitApplication =
            () => { };

        public static Func<bool> IsSearching =
            () => false;
        public static Func<float> MatchElapsedSeconds =
            () => 0f;
        public static Func<string> GetMatchStatus =
            () => "Idle";
        public static Func<bool> CanCancelMatchmaking =
            () => false;

        public static Action<int> ChooseHero =
            _ => { };
        public static Action ConfirmHero =
            () => { };
        public static Func<int> ConfirmedCount =
            () => ConfirmedHeroCount;
        /// <summary>
        /// Confirmed hero count synced from the server lobby state.
        /// Presentation-only.
        /// </summary>
        public static int ConfirmedHeroCount;
        public static Func<int> PlayerCount =
            () => 0;
        public static Func<bool> CanConfirmHero =
            () => false;

        // ---- Hero select catalog (design v10.2 lobby hero select) ----
        // Indexes are 1-based for Lua. HeroId is the UnitPrototypeId that the
        // lobby lock/GameStart flow uses (HeroConfigId).

        public static Func<int> HeroSelectCount =
            () => 0;
        public static Func<int, int> GetHeroSelectId =
            _ => 0;
        public static Func<int, int> GetHeroSelectPrefabId =
            _ => 0;
        public static Func<int, string> GetHeroSelectName =
            _ => "";
        public static Func<int, Sprite>
            GetHeroSelectAvatar =
                _ => null;

        // ---- Lobby hero-select live state (synced to every endpoint) ----
        // LobbySelectionSnapshot[] is replaced whenever the server broadcasts
        // the full lobby state, so every client renders the same choices.

        public static LobbySelectionSnapshot[]
            LobbySelection =
                Array.Empty<LobbySelectionSnapshot>();
        public static int LocalPlayerSlot = -1;

        public static void ApplyLobbySelection(
            LobbySelectionSnapshot[] snapshots,
            int localPlayerSlot)
        {
            LobbySelection =
                snapshots ??
                Array.Empty<
                    LobbySelectionSnapshot>();
            LocalPlayerSlot =
                localPlayerSlot;
        }

        /// <summary>Total assigned players in the lobby (Lua 1-based).</summary>
        public static Func<int> GetSelectStatusCount =
            () => LobbySelection.Length;

        public static Func<int, int> GetSelectStatusTeam =
            index =>
            {
                if (index < 1 ||
                    index > LobbySelection.Length)
                    return 0;
                return LobbySelection[index - 1]
                    .TeamId;
            };

        public static Func<int> GetSelectStatusMyTeam =
            () =>
            {
                for (int i = 0;
                     i < LobbySelection.Length;
                     i++)
                {
                    LobbySelectionSnapshot s =
                        LobbySelection[i];
                    if (s.PlayerSlot ==
                        LocalPlayerSlot)
                    {
                        return s.TeamId;
                    }
                }
                return 0;
            };

        public static Func<int, string>
            GetSelectStatusName =
                index =>
                {
                    if (index < 1 ||
                        index > LobbySelection.Length)
                        return "";
                    return LobbySelection[index - 1]
                        .AccountId ?? "";
                };

        public static Func<int, int> GetSelectStatusHeroId =
            index =>
            {
                if (index < 1 ||
                    index > LobbySelection.Length)
                    return 0;
                return LobbySelection[index - 1]
                    .HeroConfigId;
            };

        public static Func<int, bool>
            GetSelectStatusLocked =
                index =>
                {
                    if (index < 1 ||
                        index > LobbySelection.Length)
                        return false;
                    return LobbySelection[index - 1]
                        .IsLocked;
                };

        public static Func<int, bool>
            GetSelectStatusIsMe =
                index =>
                {
                    if (index < 1 ||
                        index > LobbySelection.Length)
                        return false;
                    return LobbySelection[index - 1]
                            .PlayerSlot ==
                        LocalPlayerSlot;
                };

        /// <summary>
        /// True when a teammate (same team, not this client) has already
        /// selected the given hero; the HeroList disables it then.
        /// </summary>
        public static Func<int, bool>
            IsHeroBlockedByTeammate =
                heroId =>
                {
                    if (heroId <= 0)
                        return false;
                    int myTeam = 0;
                    for (int i = 0;
                         i < LobbySelection.Length;
                         i++)
                    {
                        LobbySelectionSnapshot s =
                            LobbySelection[i];
                        if (s.PlayerSlot ==
                            LocalPlayerSlot)
                        {
                            myTeam = s.TeamId;
                            break;
                        }
                    }
                    for (int i = 0;
                         i < LobbySelection.Length;
                         i++)
                    {
                        LobbySelectionSnapshot s =
                            LobbySelection[i];
                        if (s.PlayerSlot ==
                                LocalPlayerSlot ||
                            s.TeamId != myTeam)
                        {
                            continue;
                        }
                        if (s.HeroConfigId == heroId)
                        {
                            return true;
                        }
                    }
                    return false;
                };

        /// <summary>
        /// Binds the hero select catalog accessors to a display table.
        /// Indexes are 1-based for Lua; HeroId is the UnitPrototypeId used as
        /// HeroConfigId by the lobby lock flow.
        /// </summary>
        public static void BindHeroSelect(
            HeroDisplayTable table)
        {
            HeroSelectCount =
                () => table != null
                    ? table.Count
                    : 0;
            GetHeroSelectId =
                index =>
                {
                    if (table == null ||
                        index < 1 ||
                        index > table.Count)
                        return 0;
                    return table.GetEntry(index - 1)
                        .UnitPrototypeId;
                };
            GetHeroSelectPrefabId =
                index =>
                {
                    if (table == null ||
                        index < 1 ||
                        index > table.Count)
                        return 0;
                    return table.GetEntry(index - 1)
                        .HeroPrefabId;
                };
            GetHeroSelectName =
                index =>
                {
                    if (table == null ||
                        index < 1 ||
                        index > table.Count)
                        return "";
                    return table.GetEntry(index - 1)
                        .DisplayName ?? "";
                };
            GetHeroSelectAvatar =
                index =>
                {
                    if (table == null ||
                        index < 1 ||
                        index > table.Count)
                        return null;
                    return ClientSpriteRegistry.Resolve(
                        table.GetEntry(index - 1)
                            .AvatarAddress);
                };
        }

        public static Func<float> LocalLoadProgress =
            () => 0f;
        public static Func<string> GetLoadingStatus =
            () => "Preparing";
        public static Func<bool> LastMatchVictory =
            () => false;
        public static Func<bool> IsLocalTeamVictory =
            () => false;
        public static Func<bool> LastMatchDraw =
            () => false;
        public static Action ReturnMainMenu =
            () => { };

        public static Func<int> GetShopItemCount =
            () => 0;
        public static Func<int, int> GetShopItemId =
            _ => 0;
        public static Func<int, string> GetShopItemName =
            _ => "";
        public static Func<int, string> GetShopItemDescription =
            _ => "";
        public static Func<int, Sprite> GetShopItemIcon =
            _ => null;
        public static Func<int, int> GetShopItemPrice =
            _ => 0;
        public static Func<int, string> GetShopItemNameById =
            _ => "";
        public static Func<int, int> GetShopItemPriceById =
            _ => 0;
        public static Func<int, string> GetShopItemEffectById =
            _ => "";
        public static Func<int, string> GetShopItemStatById =
            _ => "";
        public static Func<int> GetCurrentGold =
            () => 0;
        public static Func<bool> CanUndo =
            () => false;
        public static Action<int> RequestPurchase =
            _ => { };
        public static Action<int> RequestSell =
            _ => { };
        public static Action RequestUndo =
            () => { };
        public static Func<int, bool> IsEquipmentOwned =
            _ => false;
        public static Func<string> GetShopStatus =
            () => "";

        // ---- HUD (design v9.1 10) ----

        public static Func<int> GetLocalHp =
            () => 0;
        public static Func<int> GetLocalMaxHp =
            () => 0;
        public static Func<int> GetLocalResource =
            () => 0;
        public static Func<int> GetLocalMaxResource =
            () => 0;
        public static Func<int> GetLocalLevel =
            () => 0;
        public static Func<int> GetLocalExp =
            () => 0;
        public static Func<int> GetLocalNextLevelExp =
            () => 0;
        public static Func<bool> IsExpandStatsHeld =
            () => false;
        public static Func<int, int> GetCooldownRemaining =
            _ => 0;
        public static Func<int, int> GetCooldownTotal =
            _ => 0;
        public static Func<int, float>
            GetCooldownRemainingSeconds =
                _ => 0f;
        public static Func<string, string>
            GetLocalStatText =
                _ => "";
        public static Func<int, int> GetActiveAbilityId =
            _ => 0;
        public static Func<int, Sprite>
            GetActiveAbilityIcon =
                _ => null;
        public static Func<Sprite> GetPassiveAbilityIcon =
            () => null;
        public static Func<Sprite> GetLocalHeroAvatar =
            () => null;
        public static Func<int> GetHudGold =
            () => 0;

        /// <summary>
        /// Current network round-trip time in milliseconds, or -1 when no
        /// network sync is active (the HUD Ping label is hidden then).
        /// </summary>
        public static Func<int> GetLocalPing =
            () => -1;
        /// <summary>
        /// Closes the Shop overlay and returns to the battle HUD.
        /// </summary>
        public static Action CloseShop =
            () => { };

        // ---- MatchBar (MatchPart scoreboard, kept per user) ----

        public static Func<float> GetGameElapsedSeconds =
            () => 0f;
        public static Func<int> GetBlueTeamScore =
            () => 0;
        public static Func<int> GetRedTeamScore =
            () => 0;
        public static Func<int> GetLocalCreepScore =
            () => 0;
        public static Func<int> GetLocalKills =
            () => 0;
        public static Func<int> GetLocalDeaths =
            () => 0;
        public static Func<int> GetLocalAssists =
            () => 0;

        // ---- Expanded stats ----

        public static Func<int, int> GetLocalStatValue =
            _ => 0;

        // ---- Equipment bar ----

        public static Func<int> GetLocalEquipmentSlotCount =
            () => 0;
        public static Func<int, int> GetLocalEquipmentSlotId =
            _ => 0;
        public static Func<int, string>
            GetLocalEquipmentSlotName =
                _ => "";
        public static Func<int, int> GetLocalEquipmentSlotStack =
            _ => 0;
        public static Func<int, Sprite>
            GetLocalEquipmentSlotIcon =
                _ => null;
        public static Action<int, int> FocusShopEquipment =
            (_, __) => { };

        // ---- Passive ability slot ----

        public static Func<float>
            GetPassiveCooldownRemainingSeconds =
                () => 0f;
        public static Func<float>
            GetPassiveCooldownTotalSeconds =
                () => 0f;

        // ---- Buff bar (user-added BuffBar; design v14.2 UI rules) ----

        public static Func<int> GetLocalBuffCount =
            () => 0;
        public static Func<int, Sprite>
            GetLocalBuffIcon =
                _ => null;
        public static Func<int, string>
            GetLocalBuffName =
                _ => "";
        public static Func<int, int> GetLocalBuffStacks =
            _ => 0;
        public static Func<int, float>
            GetLocalBuffTimeProgress =
                _ => 0f;
        public static Func<int, bool>
            GetLocalBuffIsPermanent =
                _ => false;
        public static Func<int, bool>
            GetLocalBuffShowStack =
                _ => false;

        // ---- Skill points / level UI (design v15.2 1.12) ----

        public static Func<int> GetLocalPendingSkillPoints =
            () => 0;
        public static Func<int, int> GetLocalAbilityLevel =
            _ => 0;
        public static Func<int, bool> GetLocalAbilityIsUltimate =
            _ => false;
        public static Func<int, bool> CanAllocateLocalSkillPoint =
            _ => false;
        public static Action<int> AllocateLocalSkillPoint =
            _ => { };

        // ---- Debug helpers (GameScene only) ----

        public static Action DebugHealLocal =
            () => { };
        public static Action DebugRestoreManaLocal =
            () => { };
        public static Action DebugReviveLocal =
            () => { };
        public static Action DebugLevelUpLocal =
            () => { };
        public static Action<int> DebugAddGoldLocal =
            _ => { };
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Lobby-scene UI/flow controller.
    ///
    /// Local direct mode: the Lobby driver owns transport and hero-select
    /// progression; this controller only presents the Main page and shares the
    /// Lua bindings.
    ///
    /// UOS online mode (client): owns the matchmaking ticket, assignment
    /// polling, NGO connection, identity handshake and the hero-select -> lock
    /// -> GameScene progression.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyFlowController :
        MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private LobbyNetworkBridge lobbyBridge;
        [SerializeField] private ClientUiActionRouter uiActions;
        [SerializeField] private LocalNgoEndpointDriver localNgoDriver;
        [SerializeField, Min(1)]
        private int assignmentPollIntervalMilliseconds;
        [FormerlySerializedAs("assignmentPollIntervalSeconds")]
        [SerializeField, HideInInspector]
        private float legacyAssignmentPollIntervalSeconds;
        [SerializeField, Min(1)]
        private int presentationRefreshIntervalMilliseconds;
        [FormerlySerializedAs("presentationRefreshIntervalSeconds")]
        [SerializeField, HideInInspector]
        private float legacyPresentationRefreshIntervalSeconds;

        private bool matchmakingRunning;
        private bool connectionPollRunning;
        private long matchStartRealtimeMilliseconds;
        private int selectedHeroConfigId = -1;
        private bool sceneLoadTriggered;
        private bool matchPresentationActive;
        private long nextPresentationRefreshRealtimeMilliseconds;
        private float localLoadProgress;
        private string matchStatus = "Idle";
        private string loadingStatus = "Preparing battle";

        private void Awake()
        {
            if (lobbyBridge == null)
                lobbyBridge =
                    FindObjectOfType<LobbyNetworkBridge>(true);
            if (uiActions == null)
                uiActions =
                    FindObjectOfType<ClientUiActionRouter>(true);
            if (localNgoDriver == null)
                localNgoDriver =
                    FindObjectOfType<LocalNgoEndpointDriver>(true);
            if (lobbyBridge != null)
                GameSessionContext.LobbyBridge =
                    lobbyBridge;
            if (uiActions != null)
                GameSessionContext.UiActions =
                    uiActions;
        }

        private void Start()
        {
            BindLua();
            PreloadHeroSelect();
            if (GameSessionContext.IsDedicatedServer)
                return;
            if (GameSessionContext.FlowMode !=
                FrameFlowMode.UosOnline)
            {
                // Local direct mode: LocalNgoEndpointDriver drives transport
                // and hero select. No login: allocate a random local display
                // uid for the main-menu player name.
                GameFlowLuaBridge.AccountDisplayName =
                    "Player_" +
                    Guid.NewGuid()
                        .ToString("N")
                        .Substring(0, 8);
            }
            else if (GameSessionContext.ClientFlow == null)
            {
                Debug.LogError(
                    "[Lobby] UOS client flow is missing. Load ClientBootstrap first.");
                return;
            }
            else
            {
                lobbyBridge.IdentityAccepted +=
                    OnIdentityAccepted;
                lobbyBridge.HeroLocked +=
                    OnHeroLocked;
                lobbyBridge.ConfirmedCountChanged +=
                    OnConfirmedCountChanged;
                lobbyBridge.LoadSceneRequested +=
                    OnLoadSceneRequested;
                GameFlowLuaBridge.AccountDisplayName =
                    GameSessionContext.ClientFlow
                        .AccountSession.TestAccountId;
            }
            // Show the Main page only after AccountDisplayName is ready so
            // the Lua Refresh renders the actual player name.
            if (uiManager != null)
                uiManager.ShowPage(UIPageId.Main);
            uiManager?.RefreshLuaHost(UIPageId.Main);
        }

        private void BindLua()
        {
            GameFlowLuaBridge.UiManager = uiManager;
            GameFlowLuaBridge.CanStartMatchmaking =
                () => !matchmakingRunning;
            GameFlowLuaBridge.StartMatchmaking =
                BeginMatchmaking;
            GameFlowLuaBridge.CancelMatchmaking =
                CancelMatchmaking;
            GameFlowLuaBridge.QuitApplication =
                QuitApplication;
            GameFlowLuaBridge.IsSearching =
                () => matchmakingRunning;
            GameFlowLuaBridge.MatchElapsedSeconds =
                () =>
                    matchPresentationActive
                        ? (NowMilliseconds() -
                          matchStartRealtimeMilliseconds) / 1000f
                        : 0f;
            GameFlowLuaBridge.GetMatchStatus =
                () => matchStatus;
            GameFlowLuaBridge.CanCancelMatchmaking =
                () => matchmakingRunning;
            GameFlowLuaBridge.ChooseHero =
                heroId =>
                {
                    if (heroId <= 0)
                        return;
                    selectedHeroConfigId = heroId;
                    if (uiActions != null &&
                        uiActions.IsBound)
                        uiActions.SelectHero(heroId);
                };
            GameFlowLuaBridge.ConfirmHero =
                ConfirmSelectedHero;
            GameFlowLuaBridge.ConfirmedHeroCount = 0;
            GameFlowLuaBridge.ConfirmedCount =
                () => GameFlowLuaBridge
                    .ConfirmedHeroCount;
            GameFlowLuaBridge.PlayerCount =
                () => 2;
            GameFlowLuaBridge.CanConfirmHero =
                () => selectedHeroConfigId > 0;
            GameFlowLuaBridge.BindHeroSelect(
                GameSessionContext.HeroDisplayTable);
            GameFlowLuaBridge.LocalLoadProgress =
                () => localLoadProgress;
            GameFlowLuaBridge.GetLoadingStatus =
                () => loadingStatus;
            GameFlowLuaBridge.ReturnMainMenu =
                ReturnToMainMenu;
        }

        /// <summary>
        /// Pre-populates the Select page hero list when the Lobby loads so
        /// the cells are already instantiated before matchmaking opens the
        /// page (avoids a hitch when identity is accepted).
        /// </summary>
        private void PreloadHeroSelect()
        {
            if (GameSessionContext.IsDedicatedServer ||
                uiManager == null)
                return;
            uiManager.RefreshLuaHost(UIPageId.Select);
        }

        private void BeginMatchmaking()
        {
            if (matchmakingRunning)
                return;
            matchmakingRunning = true;
            matchPresentationActive = true;
            matchStatus = "Searching";
            matchStartRealtimeMilliseconds =
                NowMilliseconds();
            if (GameSessionContext.FlowMode !=
                FrameFlowMode.UosOnline)
            {
                Debug.Log(
                    "[Lobby] Local direct mode: NGO connection is already in " +
                    "progress; waiting for identity acceptance.");
                if (uiManager != null)
                    uiManager.ShowPage(UIPageId.Match);
                if (localNgoDriver != null)
                    localNgoDriver
                        .RequestLocalClientStart();
                return;
            }
            if (GameSessionContext.ClientFlow == null)
            {
                matchmakingRunning = false;
                matchPresentationActive = false;
                matchStatus = "Unavailable";
                return;
            }
            if (uiManager != null)
                uiManager.ShowPage(UIPageId.Match);
            StartCoroutine(RunMatchmaking());
        }

        private async void CancelMatchmaking()
        {
            if (!matchmakingRunning)
                return;
            matchmakingRunning = false;
            matchPresentationActive = false;
            matchStatus = "Cancelled";
            connectionPollRunning = false;
            StopAllCoroutines();
            if (uiManager != null)
                uiManager.ShowPage(UIPageId.Main);

            ClientApplicationFlow flow =
                GameSessionContext.ClientFlow;
            if (flow == null)
                return;
            try
            {
                await flow.CancelMatchmakingAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private IEnumerator RunMatchmaking()
        {
            ClientApplicationFlow flow =
                GameSessionContext.ClientFlow;
            Exception failure = null;
            var beginTask =
                flow.BeginMatchmakingAsync();
            while (!beginTask.IsCompleted)
                yield return null;
            if (beginTask.IsFaulted)
                failure = beginTask.Exception;
            if (failure == null)
            {
                while (matchmakingRunning)
                {
                    System.Threading.Tasks.Task<bool>
                        pollTask =
                            flow.PollAssignmentAsync();
                    while (!pollTask.IsCompleted)
                        yield return null;
                    if (pollTask.IsFaulted)
                    {
                        failure = pollTask.Exception;
                        break;
                    }
                    if (pollTask.Result)
                        break;
                    yield return new WaitForSeconds(
                        ResolveAssignmentPollMilliseconds() /
                        1000f);
                }
            }
            if (failure != null ||
                !matchmakingRunning)
            {
                HandleMatchmakingFailure(
                    flow,
                    failure);
                yield break;
            }
            matchmakingRunning = false;
            matchStatus = "Match found - connecting";
            Debug.Log(
                $"[Lobby] Assignment received: " +
                $"{flow.Assignment.IpAddress}:{flow.Assignment.Port}.");
            yield return StartCoroutine(
                PollConnection(flow));
        }

        private void HandleMatchmakingFailure(
            ClientApplicationFlow flow,
            Exception exception)
        {
            matchmakingRunning = false;
            matchPresentationActive = false;
            matchStatus = "Matchmaking failed";
            if (exception != null)
                Debug.LogException(
                    exception,
                    this);
            if (flow?.AccountSession != null)
                GameFlowLuaBridge.AccountDisplayName =
                    flow.AccountSession.TestAccountId;
            if (uiManager != null)
                uiManager.ShowPage(UIPageId.Main);
        }

        private IEnumerator PollConnection(
            ClientApplicationFlow flow)
        {
            connectionPollRunning = true;
            Exception failure = null;
            while (connectionPollRunning)
            {
                try
                {
                    if (flow.PollConnection())
                        break;
                }
                catch (Exception exception)
                {
                    failure = exception;
                    break;
                }
                yield return new WaitForSeconds(
                    ResolveAssignmentPollMilliseconds() /
                    1000f);
            }
            if (failure == null &&
                connectionPollRunning)
            {
                Debug.Log(
                    "[Lobby] NGO connection established; sending identity.");
                matchStatus = "Connected - joining lobby";
                if (lobbyBridge != null)
                {
                    lobbyBridge.BindClient(
                        0,
                        flow.AccountSession
                            .TestAccountId,
                        uiActions);
                    lobbyBridge.NotifyClientConnected();
                }
            }
            else if (failure != null)
            {
                Debug.LogException(
                    failure,
                    this);
            }
            connectionPollRunning = false;
        }

        private void ConfirmSelectedHero()
        {
            if (selectedHeroConfigId <= 0)
                return;
            if (uiActions != null &&
                uiActions.IsBound)
                uiActions.LockHero(
                    selectedHeroConfigId);
        }

        private void OnIdentityAccepted()
        {
            matchPresentationActive = false;
            matchStatus = "Match ready";
            Debug.Log(
                "[Lobby] Client identity accepted; opening hero select.");
            if (uiManager != null)
                uiManager.ShowPage(UIPageId.Select);
        }

        private void OnHeroLocked()
        {
            Debug.Log(
                "[Lobby] Client locked hero; waiting for all players.");
        }

        private void OnConfirmedCountChanged(
            int confirmedCount)
        {
            GameFlowLuaBridge.ConfirmedHeroCount =
                confirmedCount;
        }

        private void OnLoadSceneRequested()
        {
            if (sceneLoadTriggered)
                return;
            sceneLoadTriggered = true;
            ClientApplicationFlow flow =
                GameSessionContext.ClientFlow;
            if (flow != null &&
                flow.State ==
                ClientApplicationState.Lobby)
                flow.BeginLoadingGame();
            Debug.Log(
                "[Lobby] Server confirmed all heroes; loading GameScene.");
            if (uiManager != null)
                uiManager.ShowPage(UIPageId.Load);
            StartCoroutine(LoadGameSceneAsync());
        }

        private IEnumerator LoadGameSceneAsync()
        {
            localLoadProgress = 0f;
            loadingStatus = "Loading battle scene";
            AsyncOperation operation =
                SceneManager.LoadSceneAsync(
                    GameSessionContext.GameSceneName);
            if (operation == null)
                throw new InvalidOperationException(
                    "Failed to create the GameScene load operation.");
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                localLoadProgress =
                    Mathf.Clamp01(operation.progress / 0.9f);
                yield return null;
            }
            localLoadProgress = 1f;
            loadingStatus = "Entering battle";
            uiManager?.RefreshLuaHost(UIPageId.Load);
            yield return null;
            operation.allowSceneActivation = true;
        }

        private void Update()
        {
            if (uiManager == null ||
                NowMilliseconds() <
                nextPresentationRefreshRealtimeMilliseconds)
                return;
            nextPresentationRefreshRealtimeMilliseconds =
                checked(
                    NowMilliseconds() +
                    ResolvePresentationRefreshMilliseconds());
            if (uiManager.IsOpen(UIPageId.Match))
                uiManager.RefreshLuaHost(UIPageId.Match);
            if (uiManager.IsOpen(UIPageId.Load))
                uiManager.RefreshLuaHost(UIPageId.Load);
        }

        private static long NowMilliseconds()
        {
            return FrameSyncLaunchSchedule.SecondsToMilliseconds(
                Time.realtimeSinceStartupAsDouble);
        }

        private int ResolveAssignmentPollMilliseconds()
        {
            return assignmentPollIntervalMilliseconds > 0
                ? assignmentPollIntervalMilliseconds
                : legacyAssignmentPollIntervalSeconds > 0f
                    ? (int)Math.Round(
                        legacyAssignmentPollIntervalSeconds * 1000f)
                    : 2000;
        }

        private int ResolvePresentationRefreshMilliseconds()
        {
            return presentationRefreshIntervalMilliseconds > 0
                ? presentationRefreshIntervalMilliseconds
                : legacyPresentationRefreshIntervalSeconds > 0f
                    ? (int)Math.Round(
                        legacyPresentationRefreshIntervalSeconds * 1000f)
                    : 200;
        }

        private void ReturnToMainMenu()
        {
            GameSessionContext.LobbyBridge?
                .Shutdown();
            matchmakingRunning = false;
            matchPresentationActive = false;
            matchStatus = "Idle";
            if (uiManager != null)
                uiManager.ShowPage(UIPageId.Main);
        }

        private static void QuitApplication()
        {
            Debug.Log("[Lobby] Quit requested.");
            Application.Quit();
        }
    }
}

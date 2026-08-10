using FrameSyncMoba.FrameSync;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Application flow mode selected at process startup.
    /// LocalDirect bypasses UOS for local C/S development and never reports
    /// provider success. UosOnline uses the installed UOS SDKs.
    /// </summary>
    public enum FrameFlowMode : byte
    {
        LocalDirect = 0,
        UosOnline = 1,
    }

    /// <summary>
    /// Cross-scene session state for the
    /// ClientBootstrap/ServerBootstrap -> Lobby -> GameScene flow.
    ///
    /// This object only carries application/presentation hand-off state. It
    /// never enters Gameplay snapshots, GameplayCommand or
    /// SharedGameplayChecksum and must never be used as Gameplay authority.
    /// </summary>
    public static class GameSessionContext
    {
        public const string ClientBootstrapSceneName = "ClientBootstrap";
        public const string ServerBootstrapSceneName = "ServerBootstrap";
        public const string LobbySceneName = "Lobby";
        public const string GameSceneName = "GameScene";

        /// <summary>Flow mode selected by the bootstrap scene.</summary>
        public static FrameFlowMode FlowMode =
            FrameFlowMode.LocalDirect;

        /// <summary>True when this process is the Dedicated Server role.</summary>
        public static bool IsDedicatedServer;

        /// <summary>
        /// True when the bootstrap/Lobby scenes own the application flow so
        /// GameBootstrap must not boot UOS again.
        /// </summary>
        public static bool FlowManagedExternally;

        /// <summary>
        /// True when the network was already started by the bootstrap scene
        /// (UOS Dedicated Server) and the Lobby driver must not start it again.
        /// </summary>
        public static bool NetworkAlreadyStarted;

        /// <summary>
        /// GameBootstrap registered by GameScene. Null while still in the
        /// bootstrap/Lobby scenes.
        /// </summary>
        public static GameBootstrap Bootstrap;

        /// <summary>Persistent LobbyNetworkBridge (DontDestroyOnLoad).</summary>
        public static LobbyNetworkBridge LobbyBridge;

        /// <summary>Persistent ClientUiActionRouter (DontDestroyOnLoad).</summary>
        public static ClientUiActionRouter UiActions;

        /// <summary>
        /// Optional deterministic version handshake used by the Lobby
        /// identity gate. Computed by the bootstrap/Lobby scenes from the same
        /// GlobalGameplayData asset that GameScene bakes.
        /// </summary>
        public static FrameSyncVersionHandshake? Versions;

        /// <summary>
        /// Hero select presentation data handed off from the bootstrap scene.
        /// Presentation-only; never enters Gameplay snapshots or checksums.
        /// </summary>
        public static RuntimeConfig.HeroDisplayTable
            HeroDisplayTable;

        /// <summary>
        /// Server-side lobby result waiting for GameScene's GameBootstrap to
        /// build/apply/broadcast the authoritative payload.
        /// </summary>
        public static GameStartConfig? PendingServerStart;

        /// <summary>
        /// Client-side payload that arrived before GameScene registered its
        /// GameBootstrap. Applied as soon as GameBootstrap initializes.
        /// </summary>
        public static GameBootstrapPayload? ReceivedClientPayload;

        /// <summary>
        /// Server-side allocation slots. Local mode uses the driver's frozen
        /// local slots; UOS mode uses the allocation-derived slots.
        /// </summary>
        public static LocalLobbySlotDefinition[] ServerSlots;

        /// <summary>Client application flow owned by the bootstrap scene.</summary>
        public static ClientApplicationFlow ClientFlow;

        /// <summary>Dedicated Server application flow owned by the bootstrap scene.</summary>
        public static DedicatedServerApplicationFlow ServerFlow;

        /// <summary>
        /// Resets all session hand-off state. Never resets during a running
        /// match; it is only called when a bootstrap scene starts a new session.
        /// </summary>
        public static void ResetSession()
        {
            FlowMode = FrameFlowMode.LocalDirect;
            IsDedicatedServer = false;
            FlowManagedExternally = false;
            NetworkAlreadyStarted = false;
            Bootstrap = null;
            LobbyBridge = null;
            UiActions = null;
            Versions = null;
            HeroDisplayTable = null;
            PendingServerStart = null;
            ReceivedClientPayload = null;
            ServerSlots = null;
            ClientFlow = null;
            ServerFlow = null;
        }

        /// <summary>
        /// Computes the deterministic version handshake from the same
        /// GlobalGameplayData asset that GameScene bakes. Must be identical on
        /// every endpoint of one match.
        /// </summary>
        public static FrameSyncVersionHandshake
            ComputeVersions(
                GlobalGameplayData globalGameplayData)
        {
            if (globalGameplayData == null)
                throw new System.InvalidOperationException(
                    "GameSessionContext requires GlobalGameplayData " +
                    "to compute the deterministic version handshake.");
            BakedGlobalGameplayData config =
                globalGameplayData.BakeOrThrow();
            return new FrameSyncVersionHandshake(
                config.GameplayDataVersion,
                config.MapDataVersion,
                config.GlobalPrefabTableVersion,
                config.CommandSchemaVersion,
                (uint)GameplaySnapshot
                    .CurrentSchemaVersion);
        }
    }
}

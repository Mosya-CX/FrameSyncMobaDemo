using System;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Physics;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Project-wide deterministic configuration")]
        [SerializeField] private GlobalGameplayData globalGameplayData;
        [SerializeField] private bool dedicatedServer;

        [Header("Client-local input (unused on Dedicated Server)")]
        [SerializeField] private PlayerInputController playerInputController;
        [SerializeField] private Camera gameplayCamera;

        public FrameSyncGameRuntime Runtime { get; private set; }
        public UnitWorld UnitWorld { get; private set; }
        public PhysicsWorld PhysicsWorld { get; private set; }
        public bool IsInitialized => Runtime != null;

        private void Awake()
        {
            if (globalGameplayData == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires GlobalGameplayData.");
            BakedGlobalGameplayData config = globalGameplayData.BakeOrThrow();

            PhysicsWorld = new PhysicsWorld
            {
                Settings = new PhysicsWorldSettings
                {
                    GridCellSize = config.UnitGridCellSize,
                },
            };
            UnitWorld = new UnitWorld
            {
                PhysicsWorld = PhysicsWorld,
                GlobalPrefabTable = config.PrefabTable,
                UnitPrototypeTable = new GlobalUnitPrototypeTable(),
                StatDefinitionTable = new StatDefinitionTable(),
                EquipmentDatabase = new EquipmentDatabase(),
                AbilityDefinitions = new AbilityDefinitionRegistry(),
                BuffDefinitions = new BuffDefinitionRegistry(),
                StatGrowthC = config.StatGrowthC,
                StatGrowthD = config.StatGrowthD,
                TickRate = config.TickRate,
            };
            Runtime = new FrameSyncGameRuntime(UnitWorld, PhysicsWorld, config);
            Runtime.MatchRule.BeginCountdown(0, config.CountdownTicks);

            if (dedicatedServer && playerInputController != null)
                throw new InvalidOperationException(
                    "Dedicated Server bootstrap must not reference PlayerInputController.");
        }

        public void BindLocalPlayer(
            UnitType controlledUnit,
            int playerSlot,
            ulong clientId)
        {
            if (dedicatedServer)
                throw new InvalidOperationException(
                    "Dedicated Server cannot bind local player input.");
            if (!IsInitialized || controlledUnit == null)
                throw new InvalidOperationException(
                    "Bootstrap and controlled Unit must be initialized first.");
            if (playerInputController == null || gameplayCamera == null)
                throw new InvalidOperationException(
                    "Client bootstrap requires PlayerInputController and Gameplay Camera.");

            controlledUnit.ControlledByPlayerSlot = playerSlot;
            var buffer = new LocalInputEventBuffer();
            var resolver = new MouseWorldResolver(gameplayCamera, fp.zero);
            var requester = new PlayerCommandRequester(
                controlledUnit,
                new GameplayInputGate(),
                Runtime.CommandCollector,
                playerSlot,
                clientId,
                () => Runtime.CurrentTick,
                () => Runtime.CurrentTick);
            playerInputController.Initialize(buffer, resolver, requester);
        }
    }
}

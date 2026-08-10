# FrameSyncMobaDemo -- Repository Map

> Last verified: 2026-08-10 after real UOS two-client Gameplay validation and
> a final source audit of the incomplete UOS/LocalDirect lifecycle-owner fix.

## Unity baseline

| Item | Location / result |
|---|---|
| Unity | 2022.3.62f1c1 |
| Production code | `Assets/Scripts/` |
| Current designs | `Docs/Architecture/DESIGN_INDEX.md` -> `Docs/Design/` |
| Fixed point | package `Unity.Mathematics.FixedPoint.fp` |
| Input Actions | `Assets/Input/PlayerInputActions.inputactions` |
| Global config | `Assets/Config/Formal/GlobalGameplayData.asset`, `GlobalPrefabTable.asset` |
| Smoke scenes | `Assets/Scenes/Tests/FrameworkSmoke.unity`, `Assets/Scenes/Tests/ClientFrameworkSmoke.unity` |
| Endpoint scenes | `Assets/Scenes/ClientBootstrap.unity`, `Assets/Scenes/ServerBootstrap.unity` |
| Runtime unit prefabs | `Assets/Config/Formal/Prefabs/` (C/S single source) |
| Formal animation assets | `Assets/Resources/Animation/` |
| Page-prefab UI | `Assets/Resources/Prefab/UI/UIManager.prefab` and page prefabs in the same folder |
| Map pathfinding authoring | `Assets/Resources/Prefab/Map.prefab` (map grid, oriented obstacles, three lanes, six field references and read-only visualizer) |
| Full-match baked flow data | `Assets/Config/Formal/FlowFields/` (Team 1/2 x Small/Medium/Large bake outputs) |
| Neutral match scene | `Assets/Scenes/GameScene.unity` |
| Local builds | `Builds/LocalNgo/Client/`, `Builds/LocalNgo/Server/` |
| UOS server build | `Builds/UosServer/FrameSyncMobaServer.x86_64` |
| Composition root | `FrameSyncMoba.Bootstrap.GameBootstrap` |
| Compilation | Latest C# compile passed through Unity MCP after the client lifecycle owner-change attempt; focused helper test 1/1 passed, but the callback behavior remains incorrect. Later Console errors were caused by an invalid MCP asset-path query, not project compilation. |

## Assembly direction

```text
Deterministic     Physics     RuntimeConfig
       \             |             /
                    Unit
                     |
                 FrameSync
                     |
                 PlayerInput
                     |
            LuaBridge / Bootstrap
```

`FrameSyncMoba.FrameSync` has no NGO/UOS/InputSystem dependency.
`FrameSyncMoba.Bootstrap` is the application boundary and references NGO,
Unity Transport, Collections and installed UOS client/server SDK assemblies.
No reverse Gameplay dependency or assembly cycle was introduced.

## Runtime ownership

| Domain | Primary location | Owner |
|---|---|---|
| Tick, random, canonical values | `Assets/Scripts/Deterministic/` | deterministic foundation |
| Spatial state and queries | `Assets/Scripts/Physics/` | `PhysicsEntity2D`, grids and geometry |
| Unit and Gameplay modules | `Assets/Scripts/Gameplay/` | prefab-authored Unit/Handlers and pure deterministic services |
| Commands, snapshots, checksum | `Assets/Scripts/FrameSync/` | Tick pipeline, rollback, gold, authority/recovery |
| Device-to-Command conversion | `Assets/Scripts/PlayerInput/` | local input state and canonical Command requests |
| Scenes, NGO/UOS, UI | `Assets/Scripts/Bootstrap/` | application and presentation composition |
| Flow authoring | Map-owned `FlowFieldSceneAuthoring`, `Pathfinding/Editor/FlowFieldBaker.cs` | static grid/lane inputs and six baked assets |
| Flow visualization | Map-owned `FlowFieldVisualizer`, `Pathfinding/Editor/FlowFieldVisualizerEditor.cs` | read-only grid/obstacle/lane/flow Gizmos and Scene-view controls |

## Main composition and network paths

```text
Inspector authoring
    -> frozen runtime tables
    -> GameBootstrap
    -> FrameSyncGameRuntime
    -> SimulationTickPipeline
    -> UnitWorld / Gameplay modules
```

```text
PlayerInput / shop UI
    -> PlayerCommandRequester
    -> canonical GameplayCommand
    -> GameplayCommandBundle
    -> Bootstrap FrameSyncNetworkBridge (NGO)
    -> CommandRelayBuffer / AuthorityFrameReplicator
    -> PredictionRollbackCoordinator / AuthorityRecoveryCoordinator
```

UOS matchmaking/allocation and NGO connection lifecycle live in
`ApplicationFlow.cs`, `UosNgoApplicationAdapters.cs`,
`LobbyFlowController.cs` and `FrameSyncNetworkBridge.cs`. On the client,
the intended ownership is exclusive per flow mode: `LobbyFlowController` owns matchmaking,
transport connection and identity in `FrameFlowMode.UosOnline`, while
`LocalNgoEndpointDriver` owns client connection notification/wait behavior only
in `FrameFlowMode.LocalDirect`. Current source does not fully enforce this:
`Update()` checks `OwnsClientConnectionLifecycle`, but the driver's
`OnClientConnected()` NGO callback remains subscribed in both modes and calls
`NotifyClientConnectedOnce()` without that check. This is an open conformance
defect, not the intended ownership model.

Local/server lobby and bootstrap composition lives in `LobbyNetworkBridge.cs`
and `LocalNgoEndpointDriver.cs`; the latter still binds and starts the local or
allocated server endpoint as required. `BootstrapPayloadWireCodec.cs`
transports the existing FrameSync payload; it does not define another Gameplay
protocol. NGO/UOS state does not enter Gameplay snapshots or checksums.

Ability ScriptableObject persistence is owned by the matching source files
`AbilityRuntimeCatalogAsset.cs` and `AbilityLoadoutAsset.cs`; the neutral
catalog/loadout assets use those MonoScript GUIDs, and `FrameworkSmoke.unity`
serializes the catalog reference.

## Public protocol ownership

| Protocol | Authoritative owner |
|---|---|
| Unit UID | `FrameSyncMoba.Unit.UnitUid` |
| Gameplay Command | `FrameSyncMoba.FrameSync.GameplayCommand` |
| Gameplay Snapshot | `FrameSyncMoba.FrameSync.GameplaySnapshot` |
| Aim / Ability signal | `AimSnapshot`, `AbilitySignal`, `AbilitySignalVerb` |
| Fixed point | package `fp` |
| Shared checksum | `FrameSyncMoba.FrameSync.SharedGameplayChecksum` |
| Authority/recovery wire contracts | `FrameSyncMoba.FrameSync.AuthorityReplication` |
| Match start/result contracts | `FrameSyncMoba.FrameSync.ApplicationFlowContracts` |
| Presentation identity | `FrameSyncMoba.Unit.PresentationEventId` |
| Shop/gold read views | `IConfirmedGoldIncomeView`, `IEquipmentShopView` |

Repository search found one authoritative definition for each protected protocol.

## Current limitations

- Live UOS allocation, Ready, matchmaking, public NGO connection and sustained
  two-client Gameplay have been exercised once. The attempted follow-up owner
  fix is incomplete because the NGO connection callback remains ungated. It
  needs a source correction, callback-level behavior test and rebuilt live run.
- The server logged one UTP send-queue-full error after broadcasting the roughly
  45 KB fragmented bootstrap to two clients. Both clients applied it, but the
  endpoint queue settings still need MCP/Unity-API adjustment and live retest.
- The server honored D-033's five-second launch barrier, while the operator saw
  the client HUD open with match time near 30 seconds. Current client logs lack
  timing markers at payload receive/barrier/HUD open; this remains undiagnosed,
  not evidence that D-033 or endpoint clocks are definitely wrong.
- Complete live UOS result delivery, return-to-Lobby and remote settlement have
  not yet received the same acceptance as connection and Gameplay.
- Existing UI prefab visuals require the operator acceptance pass; code owns
  component binding, routing and serialized-reference validation.
- Equipment active target/range/NeedApproach behavior remains partial because
  the current design declares `EquipmentTargetPolicy` without defining its
  values or exact matching rules.
- `Assets/Config/Formal/` is the single runtime resource set (everything the
  packaged C/S build references); `Assets/Config/Tests/` holds test-only
  configs. `Fixtures/` was removed after all scenes/tests were repointed to
  the Formal chain.

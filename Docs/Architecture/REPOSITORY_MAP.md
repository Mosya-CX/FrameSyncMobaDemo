# FrameSyncMobaDemo -- Repository Map

> Last verified: 2026-08-01 after ExecPlan 0125 real-map validation.

## Unity baseline

| Item | Location / result |
|---|---|
| Unity | 2022.3.62f1c1 |
| Production code | `Assets/Scripts/` |
| Current designs | `Docs/Architecture/DESIGN_INDEX.md` -> `Docs/Design/` |
| Fixed point | package `Unity.Mathematics.FixedPoint.fp` |
| Input Actions | `Assets/Input/Gameplay.inputactions` |
| Global config | `Assets/Config/Runtime/GlobalGameplayData.asset`, `GlobalPrefabTable.asset` |
| Neutral fixture catalog | `Assets/Fixtures/Framework/Config/NeutralUnitRuntimeCatalog.asset` |
| Neutral Ability catalog | `Assets/Fixtures/Framework/Config/NeutralAbilityRuntimeCatalog.asset` |
| Neutral Unit prefab | `Assets/Fixtures/Framework/Prefabs/NeutralFrameworkUnit.prefab` |
| Smoke scene | `Assets/Scenes/FrameworkSmoke.unity` |
| Endpoint scenes | `Assets/Scenes/ClientBootstrap.unity`, `Assets/Scenes/ServerBootstrap.unity` |
| Formal Unit prefabs | `Assets/Resources/Prefab/Unit/` |
| Formal animation assets | `Assets/Resources/Animation/` |
| Page-prefab UI | `Assets/Resources/Prefab/UI/UIManager.prefab` and page prefabs in the same folder |
| Map pathfinding authoring | `Assets/Resources/Prefab/Map.prefab` (map grid, oriented obstacles, three lanes, six field references and read-only visualizer) |
| Full-match baked flow data | `Assets/Config/FullMatchTest/FlowFields/` (Team 1/2 x Small/Medium/Large bake outputs) |
| Neutral match scene | `Assets/Scenes/GameScene.unity` |
| Local builds | `Builds/LocalNgo/Client/`, `Builds/LocalNgo/Server/` |
| Composition root | `FrameSyncMoba.Bootstrap.GameBootstrap` |
| Compilation | Passing through Unity MCP, 0 Console errors |

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
`ApplicationFlow.cs`, `UosNgoApplicationAdapters.cs` and
`FrameSyncNetworkBridge.cs`. They do not enter Gameplay snapshots or checksums.

Local lobby/bootstrap composition lives in `LobbyNetworkBridge.cs` and
`LocalNgoEndpointDriver.cs`. `BootstrapPayloadWireCodec.cs` transports the
existing FrameSync payload; it does not define another Gameplay protocol.

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

- Live UOS matchmaking/allocation and remote NGO process validation require
  provider credentials/dashboard state and are not simulated by local tests.
- Existing UI prefab visuals require the operator acceptance pass; code owns
  component binding, routing and serialized-reference validation.
- Equipment active target/range/NeedApproach behavior remains partial because
  the current design declares `EquipmentTargetPolicy` without defining its
  values or exact matching rules.
- Framework fixtures are neutral acceptance assets, not production content.

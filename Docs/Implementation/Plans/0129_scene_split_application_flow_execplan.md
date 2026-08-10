# ExecPlan 0129: Scene-split application flow

> Status: Completed on 2026-08-04.

## Purpose

Split the previously single-scene Bootstrap composition into the repository
owner's confirmed scene flow:

```text
Client:  ClientBootstrap (startup/init) -> Lobby (menu/match/select) -> GameScene
Server:  ServerBootstrap (allocation/server/Ready) -> Lobby (barrier) -> GameScene
```

and verify the UOS login link.

## Scope

In scope:

- `GameSessionContext` cross-scene hand-off (Bootstrap only).
- `ClientBootstrap` / `ServerBootstrap` thin startup components.
- `LobbyFlowController` (UOS client matchmaking) + `LocalNgoEndpointDriver`
  moved to the Lobby scene (local direct connect + server role).
- `LobbyNetworkBridge` decoupled from GameBootstrap; start scheduling deferred
  to GameScene; server payload build/apply/broadcast and client
  Loaded/Ready/payload apply in GameScene.
- GameScene built from the ClientFrameworkSmoke composition with the
  full-match catalogs/topology; Build Settings + LocalNgoBuildMenu include
  Lobby/GameScene.

Out of scope: final UI art, production heroes, matchmaking rule design,
packaged dual-process re-validation, UOS dashboard upload.

## Changes

- Added `GameSessionContext.cs`, `ClientBootstrap.cs`, `ServerBootstrap.cs`,
  `LobbyFlowController.cs`.
- Reworked `LocalNgoEndpointDriver.cs` and `LobbyNetworkBridge.cs`.
- Adapted `GameBootstrap.cs` (external-flow registration, server payload
  hand-off, client Loaded/Ready, shared-scene server role, HUD/Load pages).
- Scenes: thin ClientBootstrap/ServerBootstrap; Lobby (UI + drivers);
  GameScene (full-match GameBootstrap + Map + UI).
- Updated `LocalNgoBuildMenu.cs` to pack the three scene chain.
- Tests: rewrote `LocalNgoEndpointSceneTests` (scene transitions) and
  `ClientBootstrapFirstWavePlayModeTests` (GameScene full-match fixture);
  fixed `GameBootstrapPlayModeTests` real-asset composition.

## Validation

- Unity compilation: zero errors.
- EditMode (Bootstrap): 42/42 passed.
- PlayMode (Bootstrap): 12/12 passed, including the full
  ServerBootstrap -> Lobby -> GameScene fixture match and client/server scene
  transitions.
- UOS: `UosClientSession.InitializeAsync` succeeded in the editor.
- Packaged dual-process NGO re-validation after the scene split is the
  remaining external gate (one build request, then wait).

## Remaining blockers

- `matchmakingConfigID` is empty in `UOSSettings.asset` /
  `UOSEnvironments.asset`; a UOS Matchmaking rule and dashboard upload are
  required before live ticket/assignment and the Dedicated Server dashboard
  flow can be exercised.

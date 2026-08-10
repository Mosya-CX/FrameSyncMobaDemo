# ExecPlan 0124: Lobby, NGO and UOS end-to-end composition

> Status: Local packaged C/S accepted and the first live UOS two-client run
> reached sustained Gameplay on 2026-08-10. Result/return/remote settlement is
> not yet live-accepted. The live run also exposed an unresolved client
> lifecycle ownership race, an unresolved startup transport-queue warning and an
> unresolved client Loading/HUD timing observation. See `CURRENT_HANDOFF.md`.

## Purpose

Compose the existing application state machines, UOS adapters, NGO connection
service and FrameSync bridge into the formal Client + Dedicated Server flow:
account, matchmaking, allocation, lobby, loading barrier, bootstrap payload,
Gameplay, result, settlement and return/shutdown.

## Progress

- [x] Compose valid ClientBootstrap and ServerBootstrap scenes/build entries.
- [x] Implement lobby/start wire messages around existing contracts.
- [x] Add one application-flow owner/driver per endpoint.
- [x] Validate local Server + two Client NGO bootstrap and authority loop.
- [x] Validate real UOS image allocation, server Ready, two-client matchmaking,
  public NGO connection, identity, hero lock, load barrier, bootstrap and
  sustained Gameplay.
- [ ] Correct the UOS/LocalDirect client-connection ownership race found in the
  first live logs. The current attempt gates `Update()` but leaves
  `OnClientConnectedCallback` ungated; compilation and a helper-return test
  passed without proving the behavior.
- [ ] Add callback-level behavior coverage, rebuild and live-revalidate the
  ownership correction.
- [ ] Remove the startup UTP send-queue saturation and revalidate the same
  two-client bootstrap burst.
- [ ] Instrument and explain the observed late client Loading/HUD transition.
- [ ] Validate result, return-to-Lobby and remote UOS settlement end to end.

## Surprises and discoveries

- ClientBootstrap has a NetworkManager but no `FrameSyncNetworkBridge`; online
  flow is disabled.
- ServerBootstrap lacks required catalogs, NetworkManager, transport and bridge.
- Only FrameworkSmoke is enabled in Build Settings.
- Application state machines exist, but no owner drives them.
- The canonical bootstrap is about 45 KB and therefore requires NGO reliable
  fragmented delivery rather than `ReliableSequenced`.
- Initial snapshot restore must establish `SnapshotTick - 1` as the accepted
  authority baseline and defer authority comparison until local prediction
  has executed that Tick.
- Server rollback history must be released after authority publication.
- Client authoring spawn queues must be discarded when the authoritative
  initial snapshot supplies the complete Unit topology.
- The Multiverse startup/profile ID and Matchmaking config ID are different
  provider contracts. The working values are recorded in `CURRENT_HANDOFF.md`;
  putting the profile ID into `MatchmakingConfigID` causes a config-not-found
  failure.
- Provider readiness depends on the coupled image resource tier in practice.
  The tested 1 CPU / 1536 MB tier became Ready in about 10 seconds; the smaller
  tier did not. The same image succeeding at the larger tier disproved the
  earlier suspicion that the Ready SDK call itself was necessarily wrong.
- In UOS mode `LobbyFlowController` owns client matchmaking, transport
  connection and identity. `LocalNgoEndpointDriver` must not also execute its
  LocalDirect notification/wait behavior; doing so caused a real, non-blocking
  `Lobby action requires a connected NGO client` race in both client logs.
- Broadcasting the approximately 45 KB fragmented bootstrap to two clients
  produced one UTP send-queue-full error with the endpoint scenes' current
  `m_MaxPacketQueueSize` 128 / `m_MaxPayloadSize` 6144. Both clients applied
  the payload, but this is still a startup reliability risk.
- The server timeline proves the D-033 five-second launch wait was honored.
  The client HUD was nevertheless observed opening with match time near 30
  seconds, and the current packaged client logs do not timestamp payload
  receive, barrier reach or HUD open. The root cause therefore remains unknown.

## Decision log

- Validate deterministic/bootstrap behavior and local NGO before involving
  provider state.
- Keep lobby/start messages outside Gameplay Command and reuse the existing
  `GameBootstrapPayload`.
- Treat UOS dashboard credentials, matchmaking rules and allocation as an
  explicit external validation gate, never as a fake-success code path.
- Keep initial authoring-spawn discard specific to initial authority restore;
  ordinary rollback retains its existing restore semantics.
- Enforce exactly one client connection-lifecycle owner per flow mode:
  `LocalNgoEndpointDriver` for `LocalDirect`, `LobbyFlowController` for
  `UosOnline`. Keep `LobbyNetworkBridge` validation strict; do not catch or
  suppress the ownership error.
- Do not revise D-033 from the operator timing observation alone. First log UTC,
  local monotonic time and simulation Tick at server broadcast, client payload
  receive/apply, launch barrier, HUD open and first accepted AuthorityFrame.
- Treat the UTP queue warning as unresolved until transport capacity is changed
  through Unity MCP/Unity APIs and a live server log no longer reports it.

## Current repository context

ClientBootstrap and ServerBootstrap contain the required NGO/UTP bridges,
endpoint drivers and application bindings. Lobby/start wire messages transport
and apply the existing payload. Local packaged multi-process behavior is
accepted. The real UOS configuration is populated and a live allocation took
two distinct clients through Gameplay. `LocalNgoEndpointDriver` now gates its
`LocalNgoEndpointDriver.Update()` gates polling to `FrameFlowMode.LocalDirect`,
but its unconditionally subscribed `OnClientConnected()` callback still calls
the same notification method in UOS mode. The focused test only verifies the
helper result and does not cover that callback. Completing this ownership fix,
endpoint transport queue capacity and launch/HUD timestamp diagnostics are the
immediate implementation/validation follow-up. Result/return/settlement remains
a later acceptance item within this plan's original scope.

## Exact design sources

- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`, application,
  lobby, NGO/UOS, bootstrap, authority, recovery and settlement sections.
- `Docs/Design/MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md`,
  page transitions and UI ownership.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md` for bootstrap
  restore and FrameSync result validation.

## Scope

In scope:

- Valid Client/Dedicated Server scene composition and Build Settings.
- One explicit driver for each application state machine.
- Lobby identity/select/lock/loaded/ready/start messages that remain outside
  Gameplay Commands.
- Canonical transport of the existing start payload and exact version checks.
- NGO two-process connection, command/authority/recovery/result loop.
- UOS provider configuration hooks, allocation readiness, settlement and
  shutdown behavior.

Out of scope:

- New Gameplay protocols or transport logic inside deterministic assemblies.
- Final UI art, production heroes/content, matchmaking rule design or new
  Packages.
- Masking missing UOS credentials with fake success.

## Affected assemblies and exact production types

`FrameSyncMoba.Bootstrap` and its existing NGO/UOS references. Reuse
`ClientApplicationFlow`, `DedicatedServerApplicationFlow`,
`LobbySessionFlowNetwork`, `GameBootstrapPayload`,
`FrameSyncNetworkBridge`, `NgoConnectionService` and installed UOS adapters.

Expected production-code change: 1,200-2,500 lines, plus scene/build
configuration.

## Public contracts, ownership and dependency direction

Lobby messages are application messages and must not become Gameplay Commands.
Bootstrap owns NGO/UOS and scenes. FrameSync owns transport-neutral payload,
command, authority, recovery and result semantics. No reverse dependency into
Gameplay is allowed.

## Deterministic ordering

Lobby slots, player configs and ready checks use ascending `PlayerSlot`.
Gameplay starts only at the scheduled `ServerTick + StartLeadTicks`; network
arrival order cannot determine spawn IDs, payload bytes or simulation order.

## Snapshot and serialization impact

Use the existing canonical snapshot/payload and command codecs. The initial
snapshot is restored before `StartTick`; later snapshots/recovery retain current
schema and ordering. Reject critical version mismatch before Gameplay.

## Implementation steps

1. Repair ClientBootstrap/ServerBootstrap references and enable the required
   scenes in Build Settings.
2. Add endpoint application drivers and UI-facing commands for matchmaking,
   lobby selection/lock, loading/ready, result and return.
3. Define narrowly scoped lobby/start wire codecs around the existing formal
   contracts, with limits and validation.
4. Broadcast/apply the 0121 payload, bind the 0123 local client and enter
   Gameplay at the scheduled Tick.
5. Validate NGO host/client as separate processes, disconnect/recovery and
   result delivery.
6. Configure and validate live UOS allocation only after local NGO passes.

## Tests

EditMode tests cover lobby codecs, malformed input, state transitions and exact
version/payload validation. PlayMode/multi-process tests cover scene lifecycle,
NGO connection, readiness barrier, scheduled start, commands, authority frames,
recovery and result. Live UOS is a separate recorded integration check because
it requires external provider/dashboard state.

## Unity MCP validation

Use MCP for scenes, serialized references, Build Settings, compilation, Console
and focused PlayMode tests. Use separate client/server processes for the final
NGO path; do not infer network success from pure tests.

## Failure conditions and recovery

Stop for an unapproved Package, unavailable required UOS provider capability or
a formal contract conflict. Local NGO and deterministic work may complete while
live UOS remains explicitly blocked by external credentials/configuration.

## Completion criteria

- Both endpoint scenes boot without missing references.
- Assigned clients cross the full ready barrier and receive/apply the same
  payload.
- Gameplay commands, authority/recovery and result work in two processes.
- Live UOS allocation/ready/settlement is either verified or precisely recorded
  as an external blocker; no placeholder success is accepted.

## Production-content exclusion

The end-to-end match uses the neutral fixtures from earlier plans. No production
hero, ability, Buff, equipment, map or final UI content is added.

## Results

The original local acceptance compiled with zero Console errors. Its focused
initial-snapshot and ServerBootstrap PlayMode tests passed. A fresh Server and
two Clients connected, passed identity/Ready, applied the same StartTick 3
payload, bound controlled Units and advanced Gameplay to Tick 10/11 while the
server reached Tick 11. No duplicate UID, bootstrap overflow,
recovery-baseline, snapshot-capacity, frame-order or disconnect error occurred.

The 2026-08-10 live UOS pass subsequently proved more of the production path:

- the Linux server image reached UOS Ready at 1 CPU / 1536 MB;
- two separately identified clients received an allocation, connected to the
  public NGO endpoint, completed identity and hero selection, loaded GameScene,
  submitted Loaded/Ready and applied the StartTick 3 bootstrap;
- the server continued deterministic Gameplay for minutes, with later combat
  events at stable increasing Ticks (including Tick 1625 at 17:36:04.626);
- the server's timestamp/Tick relationship is consistent with the configured
  five-second D-033 launch delay.

The live pass did not close the entire plan. Both clients logged a LocalDirect
notification racing the UOS connection owner. An attempted correction in
`LocalNgoEndpointDriver.cs` compiled and
`ApplicationFlowTests.ClientConnectionLifecycle_HasExactlyOneOwnerPerFlowMode`
passed 1/1, but the final source audit found that it only gates `Update()`;
`OnClientConnected()` still reaches `NotifyClientConnectedOnce()` for UOS. The
test only checks the helper return value and is not behavior coverage for the
callback. The defect therefore remains open. The server also logged one UTP
send-queue-full error immediately after bootstrap broadcast, and the client
Loading/HUD timing observation remains undiagnosed because the required
timestamp markers were absent. Result/return/remote settlement was not covered
by this live run. Exact logs, IDs and the continuation checklist are recorded
in `Docs/Implementation/CURRENT_HANDOFF.md`.

## Validation results (latest evidence)

| Area | Evidence | Disposition |
|---|---|---|
| Source compilation after owner attempt | Unity MCP refresh/compile, no project compiler error | Passed compilation only |
| Owner rule | Helper-return EditMode test 1/1; callback remains ungated | Not closed; behavior test required |
| Local packaged C/S | One server + two clients; accepted by repository owner | Passed for current local flow |
| UOS allocation/Ready | Real provider allocation; Ready in about 10 seconds at 1 CPU / 1536 MB | Passed at tested tier |
| UOS two-client Gameplay | Identity -> select -> GameScene -> Loaded/Ready -> StartTick 3 -> sustained Gameplay | Passed |
| Client owner-race exception | Current attempt misses `OnClientConnectedCallback` | Source correction + test + rebuild/live retest required |
| Bootstrap transport burst | One server send-queue-full error; both payloads still applied | Not closed |
| Launch/HUD timing | Server five-second wait proven; client HUD observation lacks timestamps | Not diagnosed |
| Result/return/settlement | Not reached in the recorded live pass | Not accepted |

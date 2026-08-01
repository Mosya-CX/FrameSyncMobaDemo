# ExecPlan 0124: Lobby, NGO and UOS end-to-end composition

> Status: Code complete and local NGO bootstrap/authority loop verified on
> 2026-07-29. Manual UI flow and live UOS allocation remain external acceptance
> gates.

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
- [ ] Validate visible UI actions/result flow and live UOS allocation in the
  operator acceptance pass.

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

## Decision log

- Validate deterministic/bootstrap behavior and local NGO before involving
  provider state.
- Keep lobby/start messages outside Gameplay Command and reuse the existing
  `GameBootstrapPayload`.
- Treat UOS dashboard credentials, matchmaking rules and allocation as an
  explicit external validation gate, never as a fake-success code path.
- Keep initial authoring-spawn discard specific to initial authority restore;
  ordinary rollback retains its existing restore semantics.

## Current repository context

ClientBootstrap and ServerBootstrap contain the required NGO/UTP bridges,
endpoint drivers and application bindings. Lobby/start wire messages transport
and apply the existing payload. Local multi-process behavior is verified;
provider dashboard/allocation and final visual UI behavior remain external
acceptance work.

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

Unity compiled with zero Console errors. The focused initial-snapshot test and
the ServerBootstrap PlayMode test passed. Fresh Server and Client builds were
then run as three processes: both clients connected, passed identity/Ready,
applied the same StartTick 3 payload, bound controlled Units and advanced
Gameplay to Tick 10/11 while the server reached Tick 11. No duplicate UID,
bootstrap overflow, recovery-baseline, snapshot-capacity, frame-order or
disconnect error occurred. Live UOS upload/allocation was not attempted.

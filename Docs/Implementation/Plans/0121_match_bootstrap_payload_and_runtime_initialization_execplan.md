# ExecPlan 0121: Match bootstrap payload and runtime initialization

> Status: Completed on 2026-07-29.

## Purpose

Close the highest-priority gap between the existing formal application
contracts and `FrameSyncGameRuntime`. A validated `GameBootstrapPayload` must be
able to initialize both endpoint roles at the same start Tick, with the same
random state, player-slot ownership, initial snapshot and critical versions.

## Progress

- [x] Reconfirm the current call graph and duplicate-protocol search.
- [x] Add one authoritative payload construction path.
- [x] Add one authoritative payload application path.
- [x] Initialize the runtime, gold state and command authorization from it.
- [x] Add focused deterministic tests and Unity MCP validation.

## Surprises and discoveries

- `GameStartConfig`, `FrameSyncVersionHandshake` and
  `GameBootstrapPayload` already exist and must be reused.
- `ApplyGameStartConfig` currently stores only the config and match ID.
- There is no production caller for `ApplyGameStartConfig` or
  `BindLocalPlayer`; server command authorization therefore has no live slot
  binding.
- The current `GlobalGameplayData.RandomSeed` cannot override the match seed
  supplied by `GameStartConfig`.

## Decision log

- Keep the existing FrameSync contracts; do not create a second bootstrap DTO.
- Separate deterministic payload construction/application from transport.
  NGO/UOS transmission remains in ExecPlan 0124.
- Allow a neutral fixture to build an initial snapshot for validation, but do
  not add an offline production game mode.

## Current repository context

`GameBootstrap` constructs and begins a Tick-0 runtime during `Awake`, queues
serialized spawns and starts countdown before any start payload exists.
`GameBootstrapPayload` requires an initial `GameplaySnapshot`,
`InitialSnapshotTick == StartTick`, a matching random seed and exact versions.
The current scenes do not supply that payload.

## Exact design sources

- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`, sections
  3-4, 7-8, 12 and 17.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`, restore phases,
  snapshot Tick meaning and random state.
- `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`, initial
  confirmed gold.

## Scope

In scope:

- Authoritative construction and application of the existing
  `GameBootstrapPayload`.
- Exact `FrameSyncVersionHandshake` validation.
- Runtime initialization at `StartTick`, including deterministic random and
  initial gold.
- Stable `PlayerSlotConfig` ownership lookup used by command authorization.
- Restore/resolve/rebuild of the supplied initial snapshot.
- A small neutral fixture used only by tests and later composition plans.

Out of scope:

- Network serialization or UOS calls.
- GameScene map authoring, final spawn placement or production content.
- UI, device input, presentation and live scene transitions.
- New UID, Command, Snapshot, Aim, AbilitySignal, Checksum or FixedPoint types.

## Affected assemblies and exact production types

- `FrameSyncMoba.FrameSync`: existing payload/version/config contracts and
  runtime snapshot initialization helpers only where ownership requires it.
- `FrameSyncMoba.Bootstrap`: `GameBootstrap` and a narrowly owned bootstrap
  payload builder/applier.
- Reuse `GameStartConfig`, `PlayerSlotConfig`, `GameBootstrapPayload`,
  `FrameSyncVersionHandshake`, `GameplaySnapshot`,
  `FrameSyncGameRuntime`, `GoldIncomeRuntime` and `FrameSyncRandom`.

Expected production-code change: 900-1,600 lines.

## Public contracts

Do not change the fields or meanings of the existing start contracts unless a
formal design conflict is found. Any new API should be an application service
around those contracts, not a duplicate DTO. The initialized controlled-unit
mapping is keyed by ascending `PlayerSlot` and authoritative stable `UnitUid`.

## Ownership and dependency direction

FrameSync owns transport-neutral payload semantics. Bootstrap owns Unity scene
composition and application sequencing. Gameplay assemblies must not reference
Bootstrap, NGO, UOS, UI or Presentation.

## Deterministic ordering

- Validate and consume player slots in ascending `PlayerSlot`.
- Spawn/capture all initial state in explicit stable order.
- Never use scene/component registration order, dictionary enumeration or Unity
  instance identity for payload bytes, unit bindings or initial checksum.

## Snapshot and serialization impact

No new snapshot schema is expected. Application must restore the supplied
snapshot through Restore, Resolve and Rebuild, then verify that the runtime's
next Tick equals `StartTick`. Canonical payload wire serialization is deferred
to ExecPlan 0124.

## Implementation steps

1. Extract runtime construction so it can be configured before simulation
   begins; prevent countdown or Tick execution before bootstrap completion.
2. Build the initial authoritative state from validated neutral composition,
   initialize match gold/random/slot bindings, capture the initial snapshot and
   construct the existing payload.
3. Apply payload by checking local versions, restoring the snapshot in three
   phases, applying match ID/slot ownership/random/start Tick and enabling Tick
   execution only after success.
4. Make server command authorization read the applied slot-to-unit bindings.
5. Fail visibly on version, snapshot Tick, seed, player-slot or stable-reference
   disagreement.

## Tests

EditMode:

- Same config/composition produces byte-equivalent initial state/checksum twice.
- Server-role and client-role payload application produce equal Tick/checksum.
- Player-slot order, command authorization and initial gold are correct.
- Version, Tick, seed, snapshot and controlled-unit mismatches fail.
- Restore/resolve/rebuild and capture round trip preserve initial state.

PlayMode is required only for a minimal `GameBootstrap` lifecycle test proving
that no Tick runs before payload application and that applying it starts at the
declared Tick. Do not test NGO or UI here.

## Unity MCP validation

After implementation, trigger script compilation, inspect Console, run the
focused EditMode fixture and the single bootstrap lifecycle PlayMode test.
Avoid unrelated full-suite runs unless compilation or shared-contract changes
justify them.

## Failure conditions and recovery

Stop if implementing this requires changing the formal fields of
`GameBootstrapPayload`, weakening snapshot validation or creating a second
command/UID/snapshot protocol. Revert only incomplete 0121 work; do not alter
completed framework systems to hide the conflict.

## Completion criteria

- No simulation advances before a valid payload is applied.
- Both endpoint roles begin at the declared `StartTick` with equal deterministic
  state and exact versions.
- Command authorization has a real applied player-slot/unit mapping.
- Focused tests and Unity compilation pass.

## Production-content exclusion

Neutral IDs and prefabs are acceptance fixtures only. This plan creates no
formal hero, ability, Buff, equipment or map content.

## Results

The existing `GameBootstrapPayload` is now produced and applied without a
second DTO. It freezes start Tick, random state, player-slot ownership,
critical versions and the initial snapshot, then restores both endpoint roles
to the same next Tick. Focused payload/runtime tests and Unity compilation
passed.

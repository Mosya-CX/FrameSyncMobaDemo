# ExecPlan 0136 - Network-monotonic launch and TickRate-independent authoring

> Status: Implemented and focused-tested (2026-08-20); rebuilt Local C/S and
> UOS process acceptance remains pending.

## Goal

Remove authoritative startup dependence on endpoint calendar clocks and make
content timing authorable in stable real-time units while preserving integer
Tick runtime, snapshot, checksum and rollback semantics.

The complete slice covers:

- replace absolute UTC launch authorization with NGO synchronized server time;
- keep client prediction bounded by both local monotonic pacing and the formal
  authority-frame prediction window;
- prevent a late client or time correction from inferring an unbounded Gameplay
  backlog from a timestamp;
- introduce one shared integer-millisecond authoring contract and explicit
  `Ceil`, `Nearest` and `Floor` Bake policies;
- validate configurable TickRate in the supported 10..120, multiple-of-5 range;
- migrate authored durations, cooldowns and cadences across global match data,
  abilities/stages, fixed passives, Buffs, equipment, projectiles, unit
  lifecycle, minion waves and currently hard-coded AI/pathfinding cadences;
- keep all runtime/snapshot fields (`StartTick`, `DurationTicks`,
  `RemainingTicks`, `ExpireTick`, AuthorityFrame Tick) expressed as integers;
- migrate formal assets without changing their 30-Tick baseline behavior;
- update the authoritative design, decision log, module status and tests.

No Player package or UOS upload is part of this plan.

## Authoritative inputs

- current user requirement (2026-08-20);
- `Docs/Architecture/DECISION_LOG.md` (D-044 is superseded only for its UTC and
  wall-clock-ceiling clauses);
- `Docs/Architecture/DESIGN_INDEX.md`;
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`;
- `Docs/Design/moba_ability_system_design_v15_2.md`;
- `Docs/Design/BuffSystem_Design_v14_2_PermanentBuffRespawnPatch.md`;
- `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`;
- `Docs/Design/MOBA_FrameSync_Unity_Projectile_System_Design_v19.md`;
- `Docs/Design/moba_non_hero_unit_modules_design_v5.md`;
- `Docs/Design/unit_behavior_framework_design_v27_3.md`.

## Architecture decisions

1. Calendar UTC is diagnostic metadata only. It may appear in asynchronous log
   records and filenames but may not authorize or pace Gameplay.
2. `GameBootstrap` owns launch scheduling. `FrameSyncGameRuntime` owns no
   network/wall-clock timestamp.
3. The launch wire contract carries integer milliseconds in NGO's synchronized
   server-time domain. Both roles compare the value with
   `NetworkManager.ServerTime`, never `DateTime.UtcNow`.
4. After the endpoint launch threshold is reached, an injected monotonic clock
   establishes a local pacing anchor. The client simulation ceiling advances
   at TickRate from that anchor and is additionally capped by the existing
   authority prediction window.
5. A late commit starts from the local receive/threshold anchor. Catch-up is
   authorized by continuous AuthorityFrames and remains bounded by
   `MaxLogicTicksPerUnityFrame`; timestamp lateness alone creates no backlog.
6. Authored Gameplay time is serialized as integer milliseconds plus an
   explicit rounding policy. Runtime definitions continue to contain baked
   integer Tick counts.
7. `Ceil` is the default for positive durations/cooldowns so authored effects
   do not become shorter. `Nearest` is reserved for cadence approximation and
   `Floor` for explicitly early boundaries. Zero remains zero.
8. Conversion is pure integer arithmetic with overflow checks. Gameplay Bake
   never multiplies a serialized `float seconds` by TickRate.
9. Existing 30-Tick formal content is migrated as
   `milliseconds = floor(legacyTicks * 1_000 / 30)`, using the declared migration
   rounding rule. The migration is editor-owned and idempotent.

## Milestones

### M1 - Shared time contract and inventory

- Add `DurationAuthoring`, `DurationRoundingPolicy` and pure checked conversion
  helpers in `FrameSyncMoba.RuntimeConfig`.
- Add an Editor PropertyDrawer that displays and stores integer milliseconds.
- Add conversion/validation tests at 20/30/60 Tick and overflow boundaries.
- Record every authored Tick field and classify protocol windows versus
  real-time content durations.

### M2 - Launch protocol

- Replace `LaunchUtcTicks` in `MatchLaunchCommit` and its codec with
  `LaunchServerTimeMilliseconds`.
- Replace `FrameSyncLaunchSchedule` UTC arithmetic with network-time launch
  threshold and local-monotonic pacing helpers.
- Remove launch time from `FrameSyncGameRuntime`.
- Inject the network/monotonic clocks in `GameBootstrap`, preserving LocalDirect
  and UOS ownership.
- Convert Loading progress to monotonic elapsed time.
- Advance GameplayDataVersion and reject mixed packages.

### M3 - Content authoring migration

- Global: countdown, launch delay, ending, hero respawn, minion wave, jungle,
  natural regen, periodic gold and attack-sequence reset.
- Ability: cooldown ranks, fixed-passive cooldowns, all CastModel durations,
  recast windows/delays, Stage delays/control durations/cooldown reductions,
  dash/pull speed and toggle resource rates.
- Buff: life/extend/periodic timings and timing-bearing effect modules.
- Equipment: active and internal cooldowns and per-target cooldowns.
- Projectile: lifetime, hit Buff/CC duration, query/cooldown authoring values.
- Unit/non-hero: dispose/respawn, wave member spacing and currently fixed
  AI/repath/combat time windows.
- Keep snapshots and baked definitions Tick-based.

### M4 - Formal asset migration

- Add an idempotent Editor migration command for current formal assets.
- Run it through Unity MCP, save/import assets and inspect representative
  Aatrox, Varus, Buff, equipment, projectile, global and unit records.
- Verify the 30-Tick baked outputs are byte/field equivalent where no deliberate
  rounding change is required.

### M5 - Verification

- Compile through Unity MCP and clear/read the Console.
- Run focused RuntimeConfig, Ability, Buff, equipment, projectile, non-hero and
  launch protocol EditMode tests.
- Run deterministic equivalence at 20/30/60 Tick, snapshot/restore/replay and
  stable-order coverage.
- Run Bootstrap PlayMode composition and loading/launch lifecycle tests.
- Run the full EditMode and PlayMode suites and separate new failures from the
  documented baseline.

### M6 - Documentation

- Add D-045 superseding D-044's absolute-UTC clauses.
- Revise FrameSync v10.2 sections 3.5, 8 and 17 for network-monotonic launch and
  integer-millisecond authoring.
- Update affected system designs where their authoring examples expose Tick
  values while preserving runtime Tick contracts.
- Update `MODULE_STATUS.md`, this ExecPlan and user-facing configuration notes.

## Acceptance criteria

- Changing either endpoint's OS clock by at least plus/minus 12 hours does not
  change launch eligibility, loading progress or client simulation ceiling.
- `DateTime.UtcNow` is absent from launch and simulation scheduling code.
- The same authored duration bakes to the expected Tick count at 20, 30 and 60
  Tick using its declared policy.
- Current formal 30-Tick content retains its intended timings.
- Gameplay commands, snapshots and checksums contain no authoring seconds or
  milliseconds.
- Late, duplicate and conflicting launch commits behave deterministically and
  cannot create timestamp-derived runaway prediction.
- Unity compilation has no new errors; focused tests pass; full-suite deltas are
  reported without hiding the existing baseline failures.

## Progress log

- 2026-08-20: current designs, D-044, launch source, NGO 1.12.2
  `NetworkTimeSystem`, assembly boundaries and initial authored-time inventory
  reviewed. Unity MCP reports the Editor idle and not compiling. Existing
  Console errors are MCP hub-connection diagnostics rather than C# compiler
  errors. The unrelated user edit to `.codex/config.toml` is preserved.
- 2026-08-20: added the checked integer-millisecond authoring contract,
  Inspector drawer and 10..120/multiple-of-5 TickRate validation. Migrated
  current formal global, Ability, Buff, equipment, projectile, unit, minion,
  jungle, AI/pathfinding and presentation timing data while preserving runtime
  Tick state. The complete legacy 30 Hz inventory is recorded in
  `LEGACY_30HZ_TIME_AUTHORING_INVENTORY.md`.
- 2026-08-20: removed `LaunchUtcTicks` from runtime and bootstrap payloads;
  launch wire v2 now carries synchronized-server-time milliseconds and
  bootstrap payload wire v3 rejects old packages. Loading and simulation pacing
  use monotonic milliseconds. Only continuous AuthorityFrames can create a
  catch-up backlog.
- 2026-08-20: Unity compilation completed with zero C# errors. Focused
  RuntimeConfig (47), Bootstrap EditMode (86) and FrameSync (86) selected tests
  reported Passed. Gameplay regression testing initially exposed 15 timing
  compatibility failures; default 30 Hz fixture ownership and unbaked legacy
  periodic Buff fallback were corrected, eliminating all 15. The selected Unit
  suite returned 505 pass / 10 retained baseline failures, matching the
  previously documented categories. Bootstrap PlayMode returned 24 pass / 3
  retained SpawnPoint/team fixture failures. No tests were disabled.

## Outcome

The requested source and formal-asset slice is complete. Calendar UTC is no
longer part of launch authorization or simulation pacing, content authors use
integer milliseconds, and a match may select any supported TickRate without
rewriting content durations. Runtime determinism remains Tick-based. A fresh
matching Local C/S and UOS client/server build is still required to validate the
new protocol against real transports; packaging was intentionally outside this
task.

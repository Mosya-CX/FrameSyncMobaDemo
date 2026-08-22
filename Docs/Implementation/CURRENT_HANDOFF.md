# Current Handoff — FrameSyncMobaDemo

> Document class: Current State / New-task SaveGame
> Replaced: 2026-08-22
> Update policy: replace current state; never append a dated development log

## 1. Repository state

- Branch: `master`.
- Base HEAD before the workflow migration: `60c84fd`.
- The current worktree contains the approved workflow-document migration from
  2026-08-22; preserve unrelated user changes and inspect `git status` before
  editing.
- Unity version: `2022.3.62f1c1`.
- Current formal designs are only those listed in
  `Docs/Architecture/DESIGN_INDEX.md`.

## 2. Current workflow state

- Workflow version 4 uses direct user requests. The A/B/C candidate loop is
  retired.
- The user supplies a design/context and concrete desired behavior. Codex
  resolves current authority, scopes, implements and verifies it directly.
- Formal ExecPlans are required only by `.agents/PLANS.md` risk/scope triggers.
- High-risk changes receive one independent read-only design/diff review.
- `CURRENT_HANDOFF.md` is a replace-on-update current save state, not a project
  diary.
- Historical audit, prompt and candidate documents live under `Docs/Archive/`
  and are not current authorities.
- Workflow health guards start as Warning in their first implementation round
  and become Blocking after the baseline is corrected in the second round.

## 3. Reliable validation baseline

- Last recorded Unity compilation: passing through Unity MCP on 2026-08-23
  after HeroTest migrated to formal PlayerInput composition; Console had no
  compilation or runtime errors.
- Current EditMode evidence: FrameSync `91/91`, Bootstrap EditMode `86/86`,
  Unit `521 passed / 10 retained failures`. The two additional passing tests
  cover Varus W/Q concurrency. The ten failures exactly match the
  recorded legacy categories; no new failure was introduced.
- Last full PlayMode run (2026-08-14): `56/60` passed, 4 retained failures.
- PlayerInput mapping is `17/17`; focused PlayMode input simulation is `4/4`
  and HeroTest shop/requester integration is `2/2`.
- `HeroTestScene` live-started on 2026-08-23 with controlled prototype 1001.
  Its scene-authored `PlayerInputController` references the shared
  `PlayerInputActions` asset and binds a formal `PlayerCommandRequester` to the
  Varus runtime; HUD and indicators initialized without a Console error.
- Focused D-045 verification recorded in ExecPlan 0136:
  - RuntimeConfig: 47 passed;
  - Bootstrap EditMode: 86 passed;
  - FrameSync: 86 passed;
  - selected Unit: 505 passed / 10 retained baseline failures;
  - selected Bootstrap PlayMode: 24 passed / 3 retained fixture failures.
- D-047 focused PlayMode attempts stopped in the same retained fixtures before
  action behavior: FrameworkSmoke SpawnPoint/team mismatch and prefab ID 9
  outside the formal Unit range. No scene or prefab changed in ExecPlan 0137.

## 4. Current implementation state

- Deterministic foundation, core Unit/Physics, FrameSync authority/recovery,
  Snapshot/checksum, Combat, Attack, Projectile, Buff/CC, ability/player input,
  presentation, Lua UI and the current minion/tower fixture are implemented to
  the evidence levels recorded in `MODULE_STATUS.md`.
- D-045 replaced calendar-UTC launch authorization with synchronized server-time
  milliseconds plus local monotonic pacing. Runtime Gameplay remains Tick-based.
- D-047 is frozen in the Unit Framework v27.4 amendment. Intent/Planner/Arbiter
  ownership is separated, Arbiter is structural rather than numeric-priority
  based, and fixed Main/Base Runtime slots are authoritative and rollback-safe.
  Spec resolution, Handler start adaptation and automatic Stage reconciliation
  are internal services rather than growing the Arbiter policy class.
  Pure Toggle signals and persistent sessions own no Main/Base Runtime and do
  not preempt another action; Ability control blocks and Handler legality still
  apply.
  The former no-Planner direct-Handler command fallback is removed; invalid
  Unit composition now fails visibly, and CancelAbility enters through Arbiter.
- Aatrox Q Main + E Base Dash and Varus Q Hold + Move/Release are covered,
  including automatic Stage transitions, same-session Main/Base migration,
  forced Move/Taunt Attack and exact restored Runtime validation.
- HeroTest no longer owns hero-specific QWER translation. It uses the same
  generic PlayerInput composition as GameScene; slot mappings derive from
  `CastModelDef` and `AimKind`, and Shop/QWER/Move/Attack/skill allocation share
  one `PlayerCommandRequester` and CommandSeq owner.
- GameplaySnapshot schema is 23. GameplayDataVersion is 3; launch wire v2 and
  bootstrap payload wire v4 require
  matching rebuilt endpoints.
- ExecPlan 0136's declared source/formal-asset scope is complete and focused
  tested. Matching Local C/S and UOS live acceptance was outside that plan and
  remains a separate future user-requested task.
- The Decision Log duplicate was corrected without semantic change: formal
  equipment remains D-039; lobby-selected hero spawn binding is D-046.

## 5. Current findings

### P0

- No known source-side P0 is recorded for the already-tested core
  bootstrap/Gameplay path.

### P1

- Matching rebuilt Local C/S and UOS endpoint acceptance for D-045 is pending.
- The retained UOS run contained a startup UTP send-queue saturation warning;
  transport capacity still needs focused configuration evidence and a fresh
  live run before closure.
- The retained old UOS package logged a LocalNGO notification exception. Current
  source gates UOS/LocalDirect ownership, but callback-level behavior coverage
  is still the appropriate proof before further ownership changes.
- Full-suite retained failures remain visible and must not be described as a
  clean all-tests baseline.
- A matching Local C/S or UOS live run has not yet been performed for the
  schema-23/bootstrap-wire-4 package; source/EditMode acceptance is complete.

### P2 / product completion

- Jungle camp/test-monster content is incomplete.
- Several HUD/presentation assets and production polish remain incomplete.
- Result/return and remote settlement do not yet have the same live UOS
  acceptance depth as connection and sustained Gameplay.
- `EquipmentTargetPolicy` remains underspecified by the Current formal design;
  do not invent its values.

## 6. Frozen continuation constraints

- Bootstrap owns Unity scheduling, scenes and NGO/UOS integration;
  deterministic FrameSync/Gameplay assemblies remain transport-independent.
- Calendar UTC is diagnostic metadata only and must not authorize Gameplay.
- Reality-time Gameplay authoring uses integer milliseconds with an explicit
  Bake rounding policy; runtime/snapshot/checksum/Command state remains integer
  Tick-based.
- All endpoints in one test use matching GameplayDataVersion, wire versions and
  Snapshot schema.
- Ordinary unit actions must follow Intent -> Planner -> Arbiter -> fixed
  Main/Base Runtime -> Handler. Automatic Handler Stage transitions are
  reconciled through Arbiter before Tick-end capture; do not mutate Runtime
  reservations directly from presentation or Planner.
- Unit/Handler composition remains prefab-authored. Presentation never feeds
  authoritative state.
- Builds follow `BUILD_GUIDE.md` and `C_S_TEST_GUIDE.md`. Send a build command
  once, perform no other Unity operation during the build, and wait for the
  user to report completion.

## 7. Resume protocol

For the next user request:

1. read `AGENTS.md`, this handoff, affected `MODULE_STATUS.md` rows and
   `DESIGN_INDEX.md`;
2. search only relevant Decision entries and Current design sections;
3. inspect the affected code, asmdefs and focused tests;
4. create an ExecPlan only when `.agents/PLANS.md` requires it;
5. implement and validate the concrete request directly;
6. do not generate future A/B/C candidates.

Historical UOS timelines and daily feature notes were removed from this current
save state. Use Git, Completed ExecPlans and archived documents when historical
diagnosis is explicitly required.

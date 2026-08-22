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

- Last recorded Unity compilation: passing through Unity MCP on 2026-08-20
  after the D-045 timing migration; no C# compilation error was recorded.
- Last full EditMode run (2026-08-14): `877/887` passed, 10 retained failures.
- Last full PlayMode run (2026-08-14): `56/60` passed, 4 retained failures.
- Focused D-045 verification recorded in ExecPlan 0136:
  - RuntimeConfig: 47 passed;
  - Bootstrap EditMode: 86 passed;
  - FrameSync: 86 passed;
  - selected Unit: 505 passed / 10 retained baseline failures;
  - selected Bootstrap PlayMode: 24 passed / 3 retained fixture failures.
- Live Unity inspection on 2026-08-22 found the Editor idle and not compiling.
  The current Console contained Unity MCP Hub negotiation errors, not reported
  C# compiler errors. This documentation-only workflow migration did not trigger
  a new compile or test run.

## 4. Current implementation state

- Deterministic foundation, core Unit/Physics, FrameSync authority/recovery,
  Snapshot/checksum, Combat, Attack, Projectile, Buff/CC, ability/player input,
  presentation, Lua UI and the current minion/tower fixture are implemented to
  the evidence levels recorded in `MODULE_STATUS.md`.
- D-045 replaced calendar-UTC launch authorization with synchronized server-time
  milliseconds plus local monotonic pacing. Runtime Gameplay remains Tick-based.
- GameplayDataVersion is 3; launch wire v2 and bootstrap payload wire v3 require
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

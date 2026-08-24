# Current Handoff — FrameSyncMobaDemo

> Document class: Current State / New-task SaveGame
> Replaced: 2026-08-24
> Update policy: replace current state; never append a dated development log

## 1. Repository state

- Branch: `master`.
- Base HEAD before the workflow migration: `60c84fd`.
- The current worktree contains the approved D-048 local Addressables and
  Dedicated Server presentation split, D-049 same-Tick Combat fairness, and
  D-050 action-keyed Crit / neutral Projectile tie implementation. Preserve
  all three scopes and inspect `git status` before editing.
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

- Last recorded clean Unity compilation: passing through Unity MCP on
  2026-08-23 for Linux Dedicated Server and ordinary Player subtargets.
  Server Player assemblies contain neither `FrameSyncMoba.ClientContent` nor
  the Odin Addressables Editor module. A UnityMCP skill-file sharing violation
  occurred during one domain reload; it is tool-owned and not a project compile
  failure.
- Current Addressables EditMode evidence: FrameSync `91/91`, Bootstrap
  `106/106`, local group/root validation `5/5`, and Dedicated Server stale
  StreamingAssets exclusion `1/1`.
- Current D-049 evidence: UnityMCP compilation has no Console errors;
  Deterministic `53/53`, FrameSync `91/91`, same-Tick fairness `15/15`,
  CombatSystem `14/14`, contribution log `5/5`, assist integration `2/2` and
  gold reward `5/5` pass. The focused GameScene first-wave/tower-combat/
  match-closure and map-root PlayMode class passes `2/2`. Its Dedicated Server
  input-ignore and missing presentation-fixture VFX/SFX warnings are retained
  fixture warnings, not Gameplay failures.
- Current D-050 evidence: final UnityMCP compilation and one-minute Console
  query are clean; Deterministic is `53/53`, FrameSync is `98/98`, RuntimeConfig
  Editor is `47/47`, and the full Unit suite is `545 passed / 10 retained
  failures`. Focused action identity, global-random isolation, UID relabel,
  capped equal-distance hit, Deferred/Projectile round-trip and checksum
  coverage pass. ClientContent Projectile PlayMode is `2/2`; the four recorded
  unrelated PlayMode fixture failures were reproduced unchanged.
- Current Addressables PlayMode evidence: representative real root loading and
  release `1/1`; UI page lifecycle and async-clear race `3/3`; Aatrox prefab
  `8/8`; map prefab `1/1`; GameScene map-view anchor placement `1/1`;
  HeroTest equipment `2/2`; unit view root-origin EditMode guard `1/1`.
  `MinionTowerLongRunTest` plays without exceptions and ticks its diagnostic
  wave; `HeroTestScene` binds unit and projectile Addressable views.
- The full Unit suite still has exactly 10 retained failures in the recorded
  Buff, charge, combat enhancement, authored lane, movement, active-Tick guard
  and assembly-boundary categories; the current result is `542 passed / 10
  retained failures`. No D-050 regression was added to that baseline.
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
- D-049 replaces submission/Unit traversal order as same-Tick Combat authority.
  The pipeline now advances each Handler class globally; Combat requests are
  collected, canonically sealed and settled in bounded causal waves. Same-target
  damage/heal and typed shields use fixed-point conserving batch allocation.
  Formal death stays synchronous through UnitWorld after active waves finish.
- Kill credit is scheme A: aggregate each enemy owner Hero's proportional
  `ActualLifeDamage` in the lethal batch and select the maximum. Shield-only,
  zero and pure-overkill contributions cannot win. Exact ties use scheme C's
  pure `DeterministicHash64` over immutable match facts and do not consume the
  deterministic random stream. Assist windows remain event-log based; KDA and
  gold continue to consume the unchanged `DeathResult.KillerHeroUid` contract.
- The existing `LastHitContributorUid` Snapshot member is retained as an audit
  fact only. New pending-wave, allocation and killer scratch state is transient,
  is cleared on Restore/Tick end and is asserted empty before Capture.
- D-050 adds immutable `GameplayParticipantId` to Unit runtime/Snapshot/checksum
  state and carries `OriginActionId` plus stable `EffectOrdinal` through Combat
  headers, Deferred Damage, event-derived damage, and pending/active Projectile
  state. Probabilistic Crit is a pure match-seeded hash over action, target
  participant and effect identity and never advances `DeterministicRandomService`.
  Equal-distance moving and AoE Projectile candidates sort by seeded participant
  score before using Participant/UnitUid only as complete-collision fallbacks, so
  technical UID relabeling does not change the selected gameplay participant.
  Restore rejects missing/duplicate participant identities instead of repairing
  them. Gameplay Snapshot schema is 24 and bootstrap payload wire remains 4.
- Event-derived Damage folds its parent `EffectOrdinal` into the child key;
  invalid negative ordinals fail at Damage submission, Deferred and Restore
  boundaries. Pending tracked Projectile restore also preserves `TargetUnitUid`.
- D-048 is integrated in source and assets. The client has 63 local-only
  Addressable roots in six groups and no remote catalog. Eight Unit prefabs,
  eight Projectile prefabs and the map are split into formal synchronous logic
  prefabs plus client-only asynchronous views. Models, animation, materials,
  VFX, audio, indicators and UI moved under `Assets/ClientContent/`; ordinary
  dependencies intentionally are not all marked as independent addresses.
- `GlobalPrefabTable` remains the sole PrefabId registry and stores an optional
  view address beside each direct logic prefab. Addressables never enters
  Gameplay spawn, Snapshot, restore, checksum or Command state.
- The client map view anchors to the static `DeterministicMapTopology` root at
  the world origin instead of the GameBootstrap root, which also carries the
  gameplay Camera and moves every LateUpdate; the new
  `GameScene_MapViewAnchorsToStaticTopologyRootAtWorldOrigin` PlayMode test
  guards the placement.
- D-048 fallout fixes: unit view prefab roots are normalized to the world
  origin (guarded by `ClientViewRootsAreAtWorldOrigin`); projectile view roots
  are normalized to the world origin too (guarded by
  `ProjectileViewRootsAreAtWorldOrigin`, Model-child sub-offsets preserved);
  `UnitOutlineHoverDriver` finds the outline on the view subtree;
  `CreateFixtureGameStartConfig` marks the fallback fixture spawn
  PlayerControlled so local fixture scenes still resolve player slots;
  `MinionTowerDiagnosticDriver` reads lane key points from the logic Map
  prefab; `HeroTestDriver` binds unit and projectile client views;
  `HeroTestDriver` also wires `BlightStackMarkPresenter` (`vfx/4102`) so Varus
  W Blight stacks render in the hero test scene;
  `ClientProjectileViewBinder` accepts the presentation loader contract.
- `LocalNgoBuildMenu` restores the editor's active build target after every
  menu build (single or composite), so a Server-subtarget build no longer
  leaves the editor compiling with `UNITY_SERVER` (which excludes
  `FrameSyncMoba.ClientContent` and breaks the next script compilation).
- `ClientProjectileViewBinder` now holds one resident Addressables lease per
  view address for the whole match instead of per projectile, so short-lived
  attack missiles no longer unload/reload the view asset and render without a
  model; guarded by `ProjectileViewLeaseStaysResidentAcrossLifetimes`.
- `ClientContentRuntimeHost` additionally preloads every projectile view
  address when GameScene binds, so the projectile bundle is resident before
  the first missile spawns (verified: 8 preloaded leases in GameScene play).
- `CorruptionVineSpreadBuffEffect` attributes chain-spread Blight to the
  original R caster (blackboard caster slot), so Blight applied by Varus R
  detonates on the caster's subsequent Ability damage on every infected hero.
- Tick-rate test configuration: `GlobalGameplayData.frameSync.TickRate` and
  `HeroTestScene` simulation are set to 50 tps. Runtime Bake converts the
  authored milliseconds with the current TickRate at load, so no asset
  re-bake is required; the offline `FrameSyncMoba/Bake All Ability Assets`
  tool now bakes at the configured TickRate via `RuntimeConfigBakeContext`,
  and `MOBA/Bake Crowd Control Catalog` is tick-rate independent (module
  compilation only). Rate-sensitive tests no longer hardcode tick counts:
  the GlobalGameplayData contract asserts bake == authored TickRate, and the
  GameScene first-wave test waits for runtime spawn/route state and uses a
  one-simulated-second movement window, so they hold at any TickRate.
- The client loader uses exact reference-counted leases. Unit/Projectile binders
  compare both stable UID and object identity, so rollback replacement under an
  unchanged UID rebinds correctly. Sprite loads use cancellation generations to
  reject stale completion after registry clear.
- Dedicated Server does not initialize Addressables, excludes the
  `FrameSyncMoba.ClientContent` assembly, prevents stale client Addressables
  output from entering StreamingAssets, strips presentation scene content and
  audits forbidden build dependencies. The local content build succeeded with
  seven bundles plus one catalog totaling 612,459,164 bytes.
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
- GameplaySnapshot schema is 24. GameplayDataVersion is 4; launch wire v2 and
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
  schema-24/GameplayDataVersion-4/bootstrap-wire-4 package; source/EditMode
  acceptance is complete.
- ExecPlan 0138 still requires one final Windows client plus Linux Dedicated
  Server Player rebuild/report inspection. The first combined build was not
  accepted: its Windows Player accidentally carried Linux Addressables and
  rendered models/TMP/Sprites magenta; server scene stripping also removed
  Camera/Light before their URP additional-data dependants. Both source defects
  are fixed and guarded by Bootstrap EditMode tests, but the user will perform
  the corrected build manually. Do not initiate it unless explicitly asked.

### P2 / product completion

- Jungle camp/test-monster content is incomplete.
- Several HUD/presentation assets and production polish remain incomplete.
- `UIManager` currently loads all seven Addressable page prefabs during
  initialization even when a page is configured not to pre-instantiate.
- Presentation assets and the new client loader assembly are server-excluded,
  but older presentation classes still compile in shared managed assemblies.
  A complete presentation-code asmdef split is a separate large refactor.
- The projectile presentation bundle is dominated by three 72–82 MiB source
  GLBs. Reducing that client package size requires content/import optimization.
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
- New client resources follow
  `Docs/Implementation/Addressables/RESOURCE_ARCHITECTURE.md`. Only independent
  runtime roots receive addresses; transitive dependencies remain dependencies.

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

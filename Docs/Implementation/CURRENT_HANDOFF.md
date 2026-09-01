# Current Handoff — FrameSyncMobaDemo

> Document class: Current State / New-task SaveGame
> Replaced: 2026-08-31
> Update policy: replace current state; never append a dated development log

## 1. Repository state

- Branch: `master`.
- Base HEAD before the workflow migration: `60c84fd`.
- The current worktree contains the approved D-048 client presentation split,
  D-049 same-Tick Combat fairness, D-050 action-keyed Crit / neutral Projectile
  tie implementation, D-051 match-scoped local Addressables content and D-052
  independent interpolated animation sampling, plus the completed 0154 W/VFX/
  reward closure. The current 0155/0156/0157 formal demo launcher is an
  independent standalone .NET tool under `Tools/UosGameLauncher`; preserve it
  alongside the six Unity scopes and
  inspect `git status` before editing.
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
- ExecPlan 0155 adds a player-facing single-client launcher. Its published
  binary is `Builds/Demo/Launcher/FrameSyncMobaLauncher.exe`; it locates the
  copied `Builds/Demo/Game/AAALOL.exe`, checks `AAALOL_Data`, accepts one login
  name and passes only `-onlineFlow` plus `--TestAccountId`. The player-facing
  surface has no settings page, directory picker, announcement card, update
  flow or launcher-owned log system, and it does not alter the developer-only
  `Tools/UosClientLauncher`. The supplied Background/Banner/Logo PNGs and
  generated multi-size AppIcon ICO are integrated; release build and published
  self-test are clean. The first independent read-only review found no P0/P1;
  final packaged visual acceptance remains external.
- ExecPlan 0156 finalizes that launcher as the four-art fixed-layout build:
  `Background.png`, `Banner.png`, `Logo.png` and generated multi-size
  `AppIcon.ico` are copied beside the published binary. The source AppIcon PNG
  is excluded from publish, the fixed Game path is not serialized, and the
  final package self-test plus executable-icon extraction both pass.
- ExecPlan 0157 supersedes the earlier no-update launcher boundary with the
  user-approved standalone Player distribution channel. It generates a signed
  full ZIP plus SHA-256 content objects from `Builds/Demo/Game`; an empty first
  package downloads into the sibling `Game`, while trusted older installs reuse
  unchanged files. The EXE embeds the signing trust root, all launch/no-op/
  offline-recovery paths require signed full-file hash validation, and directory
  swap recovery validates both candidates before deletion. D-051 local
  Addressables and all Unity Gameplay/network contracts remain unchanged.
- ExecPlan 0159 supersedes 0158's confusing physical directory names with a
  schema-v3 upload tree containing only `client-manifest.json`, its detached
  signature and `content/<sha256>`. The Launcher is now 1.3.1; every remote data
  Entry remains capped at 95,000,000 bytes, and the complete ZIP is explicitly a
  local reconstructed filename rather than a nonexistent CDN `packages` path.
  Upload the two root files plus `content` into the Bucket root before creating
  a Release; do not upload the enclosing local `Upload` folder.
- ExecPlan 0160 adds a separate Unity release-client window at
  `FrameSyncMoba/Build Local NGO/Build Release Client (Optional CDN Package)...`.
  It produces the non-Development UOS Online Player as
  `Builds/Demo/Game/AAALOL.exe` with Unity-generated `AAALOL_Data`/`AAALOL_*`
  companions. Its signed CDN packaging checkbox is optional and off by default;
  when selected it invokes the existing schema-v3 packager into
  `Builds/CdnUpload/<version>/Upload` only after a successful Player build.
  `Builds/UosClient/FrameSyncMobaClient.exe` remains the independent full test
  client and is neither read nor overwritten by this release path.
- Historical audit, prompt and candidate documents live under `Docs/Archive/`
  and are not current authorities.
- Workflow health guards start as Warning in their first implementation round
  and become Blocking after the baseline is corrected in the second round.
- `Builds/` remains ignored local/generated output. Only user-accepted client and
  server archives are copied to `Release/<version>/Client|Server` for Git;
  `Release/**/*.zip` uses Git LFS. Do not force-add `Builds/` or publish private
  signing keys, UOS credentials, logs or unaccepted intermediates.

## 3. Reliable validation baseline

- Last recorded clean source compilation: Unity MCP forced script refresh on
  2026-09-01 after the optional release-client/CDN menu integration; the focused
  `UosBuildMenuTests` suite is 6/6, the standalone Launcher Release build and
  self-test pass, the Editor is idle, and the isolated Console Error query is
  empty. No real Player build was triggered during this source verification. The
  2026-08-23 Linux Server/ordinary Player build evidence
  predates server logic Addressables and is not final D-051 packaging evidence.
- Current launcher distribution evidence: Release build/format and source plus
  published self-tests pass; loopback tests cover empty full install,
  same-version/same-size update, local same-size repair, Range resume, bad
  signature, corrupt-object full-ZIP fallback and dual-candidate recovery. The
  audited schema-v3 1.0.0 upload set has 277 logical files and 284 physical
  `content` Entries; installed size is 702.23 MiB and the reconstructed full ZIP
  size is 548.74 MiB. No physical upload file exceeds 95,000,000 bytes (about
  90.60 MiB). The 1.3.1 bootstrap ZIP contains only the seven allowlisted
  Launcher files and an empty `Demo/Game`; its manifest path is the Bucket-root
  `client-manifest.json`. One primary button owns explicit Download, Update,
  Start, Cancel and Stop states; download/update never auto-launch, and Start
  rechecks the signed remote state before process creation.
  Actual UOS upload, Badge propagation and player-machine acceptance remain
  user-owned.
- Current D-051 evidence: Bootstrap EditMode `118/118`; match-content
  configuration `8/8`; client build audit `6/6`; real Varus-only load,
  Aatrox-exclusion plus Aatrox Q-VFX ownership/release PlayMode `2/2`; formal
  asynchronous GameBootstrap composition and destroy/load cleanup `2/2`;
  Aatrox content `10/10`; Equipment/Core
  partition `6/6`.
- Corrected 2026-08-27 UOS logs proved both clients and the Dedicated Server
  loaded all four selected partitions, then failed in Unit catalog composition
  because the same source dispose-policy asset had been duplicated into three
  Logic bundles as different runtime objects. ExecPlan 0142 makes Core the sole
  serialized owner; Hero Unit catalogs now carry no policy reference. Matching
  rebuilt Windows Client/Linux Server live acceptance remains user-owned.
- ExecPlan 0143 source verification: Bootstrap EditMode is `119/119`; the
  generic Direction/Range/Ground indicator material guard passes; the external
  GameScene Loading ownership PlayMode is `1/1` focused and also passes when
  Addressables is already cached by earlier tests. The broad Bootstrap
  PlayMode probe has 34 leaf results (`25 passed / 9 retained failures`); all
  nine failures are existing scene spawn-fixture, HeroTest pre-partition lookup,
  UOS configuration or async-cancellation-log categories.
- The user's rebuilt 19:43 Windows client disproved ExecPlan 0143's
  source-asset-only indicator acceptance: the new bundles contained
  `Sprites/Default`, but Varus generic indicators and Aatrox W/E still rendered
  magenta. ExecPlan 0144 replaces that dependency with a project-owned URP
  shader and driver-owned runtime materials. PlayerInput is `36/36`, Bootstrap
  EditMode is `119/119`, and real Addressables acquisition plus framebuffer
  blue/not-magenta PlayMode is `1/1`.
- The 20:12 rebuilt clients then exposed ExecPlan 0144's remaining lookup
  assumption: all three Addressable Prefabs loaded, but the Bundle shader was
  not registered for global `Shader.Find`, so the defensive fallback disabled
  the renderers. ExecPlan 0145 removes global lookup entirely and clones each
  loaded source material directly. The focused test now asserts exact source
  Shader object inheritance in addition to texture/tint and framebuffer output.
- The 20:42 rebuild proved the remaining Player-side failure: all three
  Prefabs loaded and executed Show/Hide, but their transparent circle/line
  textures rendered as solid magenta Quad geometry. The 20:30 Bundle contains
  the expected materials, textures and one-pass D3D11 Shader, while the Player
  GraphicsSettings did not retain that Shader and the migrated materials still
  requested stale `_SURFACE_TYPE_TRANSPARENT`. ExecPlan 0146 now retains the
  Shader in the client Player core, excludes it only inside server build scope,
  restores GraphicsSettings exactly and clears all indicator material keywords.
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
- Current atomic dead-target evidence: UnityMCP compilation and Console Error
  query are clean. Six focused EditMode cases pass for formal death, Despawn,
  committed non-revocation, invalid death ordering and real `ExecuteTick`
  capture/ClientReplay restore; the focused Unit lifecycle PlayMode case also
  passes. The broad baselines are Unit `549 passed / 10 retained failures`,
  FrameSync `98 passed / 1 retained missing-prefab fixture failure`, and Unit
  PlayMode `2 passed / 1 retained PrefabId 9 range fixture failure`.
- Aatrox basic-attack content now uses direct settlement
  (`ProjectileDefId = 0`) instead of Varus projectile `101`; both melee-minion
  Prefabs were already direct. The AttackRange-based ranged threshold is
  unchanged and independent of projectile selection. The focused content
  EditMode test and Aatrox Prefab PlayMode instantiation test both pass.
- All six formal non-Structure Unit controllers now bind every basic-attack
  State to `AttackMotionTime` and every Walk/Move State to live `MoveSpeed`
  with a formal-base-speed normalization. Aatrox is `6/6` attacks and `3/3`
  moves; Varus is `2/2` and `2/2`; each melee/caster minion is `1/1` and
  `1/1`. The two Structure controllers remain Idle/Death-only with zero attack
  States. The two new all-formal-unit asset contracts, existing controller
  completeness, focused Aatrox EditMode and Aatrox Prefab PlayMode tests pass;
  full FrameSync EditMode is `100 passed / 1 retained missing-PrefabId-1101
  fixture failure`.
- D-052 adds `LoopMotionTime` to every formal Idle/Walk/Move state and raises
  Aatrox to 16 Animator parameters. The client samples attack and loop progress
  at a configurable presentation-owned rate (formal 20 Hz), interpolates render
  frames from completed Gameplay Tick plus sub-Tick accumulator, rebuilds loop
  phase from the logic-time epoch on state/rate/rollback changes and never predicts past uncommitted Impact or
  Ready. No `PresentationTick`, Snapshot/checksum/network field or fixed 30 Hz
  fallback was introduced. The clock is isolated by exact `UnitWorld`, reads the
  Runtime-owned last completed Tick, and target locomotion routing resolves
  before same-frame sampling. Focused evidence is sampler/clock `13/13`,
  animation assets `6/6`, AttackHandler `21/21`, bound-Driver Aatrox PlayMode
  `9/9` and config PlayMode `3/3`; full FrameSync is `113 passed / 1 retained
  fixture failure`, full Unit is
  `551 passed / 10 retained failures`, and broad Bootstrap PlayMode is
  `27 passed / 9 retained failures` in the same recorded categories. A rebuilt
  client visual comparison at multiple animation rates remains user-owned.
- ExecPlan 0149 skips the D-052 zero-duration Animator evaluation only when an
  ability-stage CrossFade already selected the route that frame; movement
  changes during an existing movable cast still resolve their idle/walk route
  immediately. A real Varus logic Unit/View/Driver binding now verifies Q Focus
  channel-walk entry, casting-time stop/start routing and continuing loop
  sampling. PlayerInput keeps
  Direction/Point indicators through `FocusRequested` and `CommitRequested`,
  while no-aim pending Toggles such as Varus W neither create nor suppress an
  indicator. The W-then-Q Input System test records exactly W Commit, Q Focus
  and Q Commit; the existing Gameplay tests confirm that W remains active in
  isolation and is intentionally consumed only when Q Focus enters its charge
  Stage. `PhysicsEntity2D` now advances position and rotation interpolation on
  independent clocks, so facing churn cannot restart the followed position;
  focused Physics and locked-camera PlayMode regressions pass. Current evidence
  is PlayerInput `36/36`, FrameSync `113 passed / 1 retained fixture failure`,
  focused new PlayMode `4/4`, and broad Bootstrap PlayMode `31 passed / 9
  retained failures`. Rebuilt-client visual acceptance remains user-owned.
- ExecPlan 0150 closes the remaining source-side client regressions from the
  latest UOS report. Request receipts are reconciled against the Runtime-owned
  last completed Gameplay Tick; Q remains visible through pending Focus/Commit,
  and a no-aim W request cannot hide another slot's indicator. Unit position and
  rotation presentation use independent interpolation clocks; the explicitly
  late camera follows the already projected root with bounded configurable
  smoothing. Generic indicators now build runtime materials on built-in
  `Sprites/Default` and copy source texture/tint, preserving texture-alpha
  circles/rings/lines without a custom Bundle Shader dependency. Aatrox Q VFX
  duration is `ImpactDelayTicks / World.TickRate`. PlayerInput is `37/37`,
  FrameSync is `117/117`, and the exact input, camera, Physics, indicator,
  animation and Aatrox timing PlayMode/EditMode regressions pass.
- ExecPlan 0151 closes the follow-up Varus/input and indicator regressions in
  source. A requester-level and real Input System test preserve Q Focus,
  right-click route Move and primary-click Commit at the intended TargetTick;
  the live refusal was not reproduced, so no speculative same-Tick command
  merge or Planner/Intent contract change was made. Request receipt, canonical
  execution, AbilitySignal, local-state and Toggle-restore logs now include
  Tick/mode/slot/verb/sequence or session and before/after state for the next
  UOS run. The pure Varus W Toggle is attack-neutral while a real Hold stage
  still blocks ordinary attacks. Generic indicators clone the complete
  Addressables-loaded source Material (including its resolved Shader, texture,
  tint and render state) without `Shader.Find`; the source guard and
  framebuffer regression pass. Unity MCP confirms `HeroTestScene` is open,
  valid and clean with hero and dummy prototype `1001` (Varus). PlayerInput is
  `38/38`; the focused Q route, W-then-Q, one-shot W and indicator checks pass.
  Rebuilt Windows/UOS acceptance and a live W state-jump reproduction remain
  user-owned. The required reviewer turns were rejected by the host usage
  limit, so the local read-only review found no P0/P1 and no second review was
  run.
- ExecPlan 0152 closes the reproduced Varus W double-Toggle at the application
  command boundary. One physical W press produced sequence 2 once, but the
  client placed it in Bundle sequences 11 through 21; after Tick 136 froze, the
  server's former Tick-local dedupe accepted the same identity again at Tick
  138. `GameplayCommandIdentity` now defines the match-scoped
  `ClientId + CommandSeq` identity, and the server discards accepted identities
  before late retargeting or authorization. The client bridge also sends only
  newly changed, not-yet-successfully-sent canonical identities instead of
  rebuilding an identical reliable Bundle every Unity Update. Two deliberate
  consecutive W presses remain distinct because they own different
  CommandSeq values. No wire, Snapshot, checksum, rollback or Toggle contract
  changed. Unity compilation is clean; full FrameSync is `121/121` and
  Bootstrap EditMode is `123/123`. The first independent review found one P1
  concerning authorization after owner invalidation; it was fixed and covered
  by regression. The threshold for a second review was not met. Rebuilt UOS
  acceptance remains user-owned.
- ExecPlan 0153 closes the two remaining failures proven by the 12:22 UOS
  clients. Generic Direction/Range/Ground indicator instances had survived a
  GameScene content rebind after their generation-1 Addressables leases were
  released, so their cloned materials referenced unloadable dependencies while
  Aatrox Q's independent line path remained valid. The Host now acquires all
  generation-2 leases first, synchronously rebuilds every generic instance and
  runtime material while both generations are resident, adopts the new leases
  and only then releases generation 1. Driver destruction is idempotently
  cleaned, and Aatrox Q lines remain re-showable after rebind. Varus Q may still
  return to Idle when rollback replay removes its predicted Focus; if Relay
  later accepts the exact tracked Unit/slot/verb/CommandSeq at a strictly newer
  Tick, PlayerInput reconstructs only the local latch and waits for authority
  Gameplay execution before allowing Commit. It does not preserve local state
  through rollback and changes no wire, Snapshot or checksum state. Unity
  compilation and final Console are clean; PlayerInput is `41/41`; atomic
  generation, existing framebuffer/world-space, W-then-Q and representative
  Addressables PlayMode cases each pass `1/1`. The first independent review's
  one P1, one P2 and one P3 were all fixed; one P1 did not meet the user's
  threshold for a second review. Rebuilt UOS acceptance remains user-owned.
- The 2026-08-31 UOS evidence closes three later live failures in source. A
  client rollback at Tick 4700 restored attacker `3/1101/1` with a locomotion
  target `3040/1201/1` that formal death had already removed; UnitWorld now
  clears Attack, Planner intent and unit-follow locomotion in the same stable
  invalidation traversal before the Tick-end Snapshot, while Resolve remains a
  strict dangling-reference failure boundary. Varus Q sequence 57 was requested
  for Tick 3017 but accepted by the server at Tick 3018; ExecPlan 0153 supersedes
  the earlier keep-pending workaround: rollback may clear the local latch, then
  the exact later accepted relay restores that local-only latch before its
  authority Tick executes. HeroTestScene proved the
  indicator assets themselves were sound; Unity scene inspection then showed
  that only GameScene hosted `SkillIndicatorDriver` on the moving/rotating
  Camera root. Runtime generic indicators and Aatrox zone lines now live under
  a scene-owned, unparented world-space root and no longer inherit Camera
  LateUpdate. Unity compilation and the final isolated Console query are clean;
  PlayerInput at that checkpoint was `39/39`; current 0153 evidence is `41/41`.
  The focused death/Despawn/input regressions are `6/6`,
  the indicator world-space/material/framebuffer PlayMode case is `1/1`, and
  the full Unit probe is `553 passed / 10 retained failures`. Varus W was not
  exercised in the supplied run and no new W conclusion is recorded.
- ExecPlan 0154 completes the next live-evidence closure. In the first of two
  UOS matches, Aatrox W sequence 17 executed at Tick 3156 before its accepted
  relay arrived; the relay used the same already-completed TargetTick and
  previously re-latched the local indicator state. `PlayerCommandRequester`
  now compares accepted TargetTick with the Runtime-owned completed Tick, so it
  waits only for genuinely future authority execution. The exact callback-order
  regression and full PlayerInput suite pass `42/42`.
  `VfxManager` warms shared and selected-hero VFX addresses and one inactive
  pool instance before client event dispatch; Varus E VFX 4001 therefore no
  longer waits for its first event to perform the cold load. Playback and
  `ClientProjectileViewBinder` now log source/spawn Tick, address, cache/pool
  hit and elapsed milliseconds to correlate any residual first-use gap with
  the polluted-ground view. Minion experience range is 1200 authored distance
  (1.5x the former 800), covered at 11.99/12.01. The UOS CDN full-client ZIP
  and future remote-Addressables procedures are documented in
  `BUILD_GUIDE.md`; no build or upload was performed. Unity refresh is clean,
  FrameSync is `123/123`, Bootstrap EditMode is `123/123`, the focused VFX and
  reward tests pass, and client PlayMode/UOS rebuild acceptance remains
  user-owned because the Editor target is `UNITY_SERVER`.
- The editor-only `HeroTestDriver` now composes the selected Core, map and hero
  Addressables partitions into a transient resolved `GlobalPrefabTable` before
  baking Unit/Ability/Projectile registries. The current `HeroTestScene`
  Varus fixture (prototype `1001`, runtime Prefab `1101`) starts successfully
  and spawns its five dummy units; the production `GameBootstrap` path and
  packaged build inputs are unchanged.
- The supplied Client-A Tick 5152 checksum mismatch is not reproduced by the
  minimal predicted-Move replacement, frozen-anchor replay or active-route
  Restore/Replay tests; those new regressions pass, including preservation of a
  future local command beyond the authority-correction replay boundary. Per the
  user's direction, no speculative deterministic-state change was made. With
  D-032's explicit `-checksumDetail` flag, the server and client mismatch path
  print matching aggregate, lifecycle, projectile, minion-wave and per-Unit
  identity/pose/intent/action/attack/locomotion state; healthy server Ticks do
  not pay this full Snapshot/logging cost by default.
  The next rebuilt UOS logs are the required evidence for assigning the first
  divergent member; no Snapshot/checksum/wire contract changed.
- Current Addressables PlayMode evidence: representative real root loading and
  release `1/1`; UI page lifecycle and async-clear race `3/3`; Aatrox prefab
  `9/9`; map prefab `1/1`; GameScene map-view anchor placement `1/1`;
  HeroTest equipment `2/2`; unit view root-origin EditMode guard `1/1`.
  `MinionTowerLongRunTest` plays without exceptions and ticks its diagnostic
  wave; `HeroTestScene` binds unit and projectile Addressable views.
- The full Unit suite still has exactly 10 retained failures in the recorded
  Buff, charge, combat enhancement, authored lane, movement, active-Tick guard
  and assembly-boundary categories; the current probe is `545 passed / 10
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
- Formal death target invalidation is now a UnitWorld-to-ActionArbiter state
  transition immediately after frozen Combat settlement. A matching
  uncommitted AttackHandler windup and Main ActionRuntime are canceled in one
  deterministic call before Tick-end capture; non-death Despawn reuses that
  path, committed attacks are preserved, and rollback topology removal is not
  treated as Gameplay cancellation.
- D-051 is integrated in source and assets. `GlobalPrefabTable` remains the sole
  PrefabId authority but its formal root now contains four partition descriptors
  and zero direct logical prefab groups. Core, Map 1, Varus and Aatrox child
  tables retain all 20 former rows as path-only mappings with exact
  version/dependency hashes. The original combined catalogs remain
  non-addressed migration evidence and `GameScene` has no direct formal content
  catalog references.
- Lobby freezes MapConfigId plus the sorted unique complete hero roster before
  `GameScene`. Bootstrap asynchronously loads only Core + selected Map + selected
  Heroes, validates and canonicalizes the closure, then creates one
  nonserialized resolved table and combined registries before initial Snapshot
  or Tick 0. The later bootstrap payload must match that closure exactly.
- Four `Logic-*` groups contain 35 deterministic roots. Client presentation has
  63 local-only roots across eight `Client-*` groups and no remote catalog;
  hero-specific views/projectiles/VFX moved to `Client-Hero-1001/1002`.
  Addressables is a pre-Tick transport and never enters Gameplay Tick, Snapshot,
  restore, checksum, Command, spawn order or random state.
- Externally managed GameScene flow now primes `UIPageId.Load` before the first
  match-scoped Addressables await. This replaces the scene's standalone Main
  fallback before it can render and preserves Load until the existing launch
  barrier opens HUD.
- The three generic indicator source materials use built-in `Sprites/Default`.
  `SkillIndicatorDriver` clones each complete Addressables-loaded source
  Material, preserving its exact resolved Shader, texture, tint and render
  state, and owns the runtime clone cleanup. Each content generation is swapped
  atomically: new leases are fully acquired and every generic instance is
  rebuilt before the old leases are released. No live instance crosses an
  unowned Addressables window, and `OnDestroy` clears the independent world
  root/materials. No second global `Shader.Find` is used for these prefabs.
  Texture alpha supplies the circle/ring/line shape on the existing Quad
  meshes. This covers Varus generic indicators plus Aatrox W/E; Aatrox Q
  remains on its dedicated multi-zone path and is verified after rebind.
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
- `VfxManager.PreloadAsync` additionally preloads every entry in the configured
  `FullMatchVfxLibrary` and creates one inactive instance per definition before
  the client registers external flow readiness. Manager-owned leases and pool
  instances remain presentation-only; no Gameplay Tick is delayed or changed.
  `[VfxPreload]`, `[VfxPlayback]` and `[ClientProjectileView]` diagnostics are
  intentionally detailed for the next UOS run.
- `MatchRuleRuntime.MinionRewardShareRadius` is 1200 authored/stat distance.
  It is still converted exactly once through
  `UnitWorld.StatDistanceToLogicDistanceScale`; only the configured range
  changed, not recipient ordering or reward settlement semantics.
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
- Dedicated Server now initializes the shared Bootstrap content loader and
  ships a local Addressables catalog with only the four `Logic-*` groups. It
  still excludes `FrameSyncMoba.ClientContent`, every `Client-*` group and
  presentation dependencies; output audit requires logic catalog/bundles and
  rejects any client bundle. The previous 612,459,164-byte client measurement
  predates D-051 and is not the new package-size result.
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
  one `PlayerCommandRequester` and CommandSeq owner. Completed-Tick local state
  may return to Idle when rollback removes the original predicted Q Focus; an
  exact strictly later accepted relay reconstructs the local-only latch, and
  its authority execution restores primary Commit eligibility through route
  Move. Pure Toggle W remains attack-neutral, and the expanded request,
  execution, signal, local-state and Toggle-restore diagnostics expose the
  next live transition without entering Gameplay state.
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
- ExecPlans 0138/0141 still require one final Windows client plus Linux
  Dedicated Server Player rebuild/report inspection. The first combined build was not
  accepted: its Windows Player accidentally carried Linux Addressables and
  rendered models/TMP/Sprites magenta; server scene stripping also removed
  Camera/Light before their URP additional-data dependants. Both source defects
  are fixed and guarded by Bootstrap EditMode tests, but the user will perform
  the corrected build manually. Do not initiate it unless explicitly asked.
- ExecPlan 0153 source fixes are complete and focused-tested. A new rebuilt
  Windows/UOS pass is still required to confirm atomic rebind keeps Varus
  generic indicators plus Aatrox W/E textured/visible and that Q remains
  usable after route movement and a retargeted authority acceptance. Varus W's
  0152 identity/send fix also still needs this rebuilt live pass. If the W
  jump or Tick 5152-class divergence recurs, the new request/execution/signal/
  session logs and the existing checksum-detail world dump are the required
  evidence. These presentation/input changes require no protocol/schema
  migration; the diagnostics are read-only.

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
  Snapshot schema. D-051 additionally requires matching Lobby LoadScene v2 and
  the same local content versions/hashes.
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
  New logic content must join the one root/child aggregate and complete loading
  before initial Snapshot/Tick 0; never reintroduce a full-scene direct catalog.

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

# ExecPlan 0143 — Loading handoff and indicator shader compatibility

Plan ID: 0143
Status: Completed
Created: 2026-08-27
Completed: 2026-08-27
Risk: Medium
Design conformance: Strict
Estimated code delta: 80–180 C# lines plus three regenerated Material assets and focused documentation
Actual code delta: approximately 200 C# lines plus three regenerated Material assets and focused documentation
Affected assemblies: FrameSyncMoba.Bootstrap; FrameSyncMoba.Bootstrap.Editor; FrameSyncMoba.Bootstrap.EditModeTests; FrameSyncMoba.Bootstrap.PlayModeTests
Design sources: Docs/Architecture/MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md; Docs/Architecture/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md
Decision dependencies: D-030; D-045; D-048; D-051
Validation basis: UOS client observation; source/asset inspection; Unity compilation and Console; focused EditMode/PlayMode tests

## 1. Purpose

Keep the Loading page continuously visible while an externally managed match
changes from hero selection into GameScene, so GameScene's default Main page
cannot flash before match content is ready. Make all three generic skill
indicator prefabs render with the same built-player-compatible shader family as
the already working Aatrox Q directional-zone indicator.

## 2. Progress

- [x] Correlate the reported symptoms with current scene/UI initialization and indicator asset paths.
- [x] Confirm external flow shows Loading before scene activation but GameBootstrap configures GameScene UI only after asynchronous match-content loading.
- [x] Confirm generic indicators use three URP/Unlit materials while Aatrox Q uses `Sprites/Default`.
- [x] Prime GameScene's external Loading presentation before the first asynchronous content load.
- [x] Make presentation migration enforce a supported transparent indicator shader and regenerate the three Material assets through Unity APIs.
- [x] Add focused loading-handoff and indicator-material regression tests.
- [x] Compile through Unity, inspect Console, run focused EditMode/PlayMode tests and review the diff.
- [x] Update current status/handoff evidence and complete the plan.

## 3. Repository facts and discoveries

- `LobbyFlowController` already opens `UIPageId.Load` before activating
  GameScene, so the visible Main page is not produced by the Lobby transition.
- GameScene's `UIManager.Awake()` starts loading all page prefabs and honors its
  serialized `OpenOnStart` Main page. `GameBootstrap.InitializeAsync()` does
  not override that pending page until after match-scoped Addressables have
  loaded and all Gameplay catalogs have baked.
- `DirectionIndicator`, `RangeCircleIndicator` and
  `GroundTargetIndicator` depend on `IndicatorBarBody`, `IndicatorCircle` and
  `IndicatorRing`; all three materials currently use
  `Universal Render Pipeline/Unlit`.
- Aatrox Q's multi-zone visualization creates a `LineRenderer` material with
  `Sprites/Default`, explaining why it remains visible while generic indicator
  prefabs render magenta in the built client.

## 4. Design sources and traceability

- UI design v9.1 §§1.3, 2.3 and 9.4: Select transitions to Load after hero
  confirmation, and Load remains the sole main page until Gameplay is ready.
  -> PlayMode coverage asserts external GameScene bootstrap queues/opens Load
  and never Main during initialization.
- Presentation design v13.2 and D-048: presentation resources are local,
  asynchronous and cannot affect authoritative Gameplay.
  -> Indicator material changes remain inside Client presentation assets and
  have no Gameplay contract or state impact.
- D-030: `SkillIndicatorDriver` owns local aim/focus indicators.
  -> EditMode coverage validates every generic indicator prefab dependency uses
  the approved supported shader.
- D-045/D-051: loading remains a pre-Tick-0 barrier while selected content is
  loaded and frozen.
  -> Early UI priming does not advance Runtime or bypass content validation.

## 5. Scope

### In scope

- Early client-only Loading-page ownership in externally managed GameScene flow.
- Generic skill indicator material shader compatibility.
- Idempotent Editor migration enforcement and focused tests.
- Current plan, module-status and handoff evidence.

### Out of scope

- Hero selection, network, UID, Command, Snapshot, checksum or Gameplay rules.
- Indicator geometry, colors, final art replacement or new packages.
- Player builds and UOS live acceptance; the user owns packaging.

No public protocol, serialization, Snapshot, checksum or deterministic
lifecycle contract changes.

## 6. Implementation plan

1. Add a small client-only GameBootstrap preparation step immediately after
   external-flow resolution and before the first content await; bind the UI
   bridge, initialize UIManager, close pending defaults and queue Load.
2. Extend `ClientPresentationAssetMigration` with an idempotent material
   normalization step that preserves authored tint/texture while assigning the
   built-player-compatible transparent shader.
3. Run the normalization through Unity so `.mat` assets are serialized by the
   Editor, then add asset invariants and PlayMode loading-handoff coverage.
4. Compile, inspect the Console, run focused tests, review exact diffs and
   update current evidence.

## 7. Public contracts and ownership

No new public types or assembly dependency directions. `GameBootstrap` remains
the GameScene composition owner; `UIManager` remains the page lifecycle owner;
`ClientPresentationAssetMigration` remains the idempotent Editor owner of moved
presentation assets and their Addressables roots.

## 8. Validation

- Unity script compilation and fresh Error/Exception Console inspection.
- EditMode: all three generic indicator prefabs resolve materials using the
  approved shader, preserve blue transparent tint and remain Addressable.
- PlayMode: external-flow client bootstrap queues/opens Load during async
  initialization and does not expose Main; existing GameBootstrap selected
  content initialization remains successful.
- No deterministic equivalence/Snapshot/rollback test is required because no
  authoritative Gameplay state or contract changes.
- Player build and UOS visual acceptance remain external and user-owned.

## 9. Independent review

Not required: this is a Medium-risk presentation/bootstrap timing fix with no
public protocol or deterministic-state changes.

## 10. Failure and recovery

- The loading preparation is idempotent and the later complete presentation
  binding remains authoritative.
- The material normalization is idempotent and can be rerun through Unity.
- If Unity compilation or focused tests fail, retain exact evidence and resume
  from the Progress list. Do not issue a Player build command.

## 11. Results

- `GameBootstrap` now primes the externally owned Loading page before the first
  match-content await. It replaces Main in both UIManager's pending and already
  initialized paths, while the existing launch barrier remains the sole route
  from Loading to HUD.
- `IndicatorBarBody`, `IndicatorCircle` and `IndicatorRing` now use built-in
  `Sprites/Default`; their original textures, blue tints and alpha values are
  preserved. The idempotent presentation migration enforces the same material
  state on future reruns.
- Unity script compilation completed with an empty isolated Error/Exception
  Console. Bootstrap EditMode passed `119/119`; focused Loading handoff
  PlayMode passed `1/1` and also passed in the broad suite after cached
  Addressables timing.
- The broad Bootstrap PlayMode probe has 34 leaf results (`25 passed / 9
  retained failures`). All nine are existing FrameworkSmoke/GameScene spawn
  fixtures, HeroTest pre-partition lookup, UOS configuration or asynchronous
  cancellation-log categories; the new Loading test passes in that order.
- No public contract, deterministic Gameplay, Snapshot, checksum, schema or
  wire version changed. No Player build command was issued. Final Windows
  Client/UOS visual acceptance remains user-owned.

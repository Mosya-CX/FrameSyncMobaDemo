# ExecPlan 0125 — Unit prefab, UI lifecycle, and integrated pathfinding closure

## 1. Purpose

Make the current full-match test composition usable rather than merely spawnable:

- Hero, melee/ranged minion, and tower prefabs expose the required Unit,
  Handler, physics, presentation, socket, Animator, and animation-driver
  composition for their configured prototype.
- UI behavior lives on page prefabs. A scene-level `UIManager` owns page
  creation, opening, closing, layers, and fixed refresh instead of placing
  every page/controller under `ClientUI`.
- A*, team flow fields, deterministic RVO, and their combined movement path
  are verified together. Editor-only flow-field visualization makes the baked
  directions and blocked cells visible without becoming Gameplay authority.

## 2. Progress

- [x] Reconnect Unity MCP and confirm a clean idle Editor.
- [x] Select current formal design sources and identify the historical
  read-only comparison baseline (`32bbfd4`).
- [x] Audit every full-match Unit prefab and its animation assets through MCP.
- [x] Prove Unit/presentation/animation binding on the full-match fixtures.
- [x] Apply the validated binding to every formal prefab under
  `Assets/Resources/Prefab/Unit`.
- [x] Implement the formal `UIManager` / `UIPanel` / `UIPage` ownership path.
- [x] Move page behavior and serialized references onto UI prefabs.
- [x] Remove page-controller crowding from the scene `ClientUI` object.
- [x] Run focused A*, flow-field, RVO, and combined-path tests.
- [x] Repair confirmed pathfinding defects without restoring legacy authority.
- [x] Add an Editor-only flow-field visualization host.
- [x] Compile, inspect Console, and run only the focused tests required by the
  changed behavior.
- [x] Record validation results and remaining art-authoring limits.
- [x] Re-audit the real Map prefab after visual review, repair oriented obstacle
  rasterization and the exact blue/red lane waypoint chains.
- [x] Move map/grid/lane/flow ownership to `Resources/Prefab/Map`, separate
  visualization from bake data, and remove scene-local duplicates.
- [x] Re-open both endpoint scenes through Unity MCP, verify their final
  composition, and rerun the real ClientBootstrap first-wave PlayMode smoke.

## 3. Surprises and discoveries

- The current full-match catalogs bake and an authority smoke reaches the
  first wave, but this proves only deterministic composition, not complete
  prefab presentation or UI lifecycle.
- The first audit covered `Assets/Config/FullMatchTest/Prefabs`; those are
  validation fixtures, not the user-authored formal prefabs. The seven formal
  prefabs under `Assets/Resources/Prefab/Unit` are the migration targets.
- All currently available Varus clips are under
  `Assets/Resources/Animation/Varus`. The existing controller omits Attack2,
  Q release, E, and R, so this is a binding defect rather than missing art.
- Commit `32bbfd4` contains a legacy `UIManager` and flow-field Gizmo view.
  They are useful UX references only. Their singleton, float interpolation,
  asynchronous RVO, and mutable dictionary iteration cannot be restored into
  authoritative Gameplay.
- The formal FrameSync design excludes offline Gameplay. Integrated movement
  validation must use deterministic authority execution or focused tests,
  not an unconfirmed client-prediction loop.
- The two tower fixture prefabs contained duplicate Handler/Physics components;
  they were normalized to the single instances referenced by the root Unit.
- Flow fields baked correctly in memory but reloaded empty because
  `FlowFieldKey` and `TeamFlowFieldData` were not Unity-serializable. Both
  existing contracts now carry `Serializable`, and all six assets survive
  save/reload with 40,401 cells.
- `UnitAnimationDriver.LateUpdate` exposed that the original context
  implementation cleared `SimulationTickContext.Current` at Tick end. The
  formal FrameSync, Attack and Presentation designs instead require Current as
  the sole cross-system simulation-time source. Active execution ownership is
  now separate from the retained published context; the rejected UnitWorld
  clock and Tick-parameter overload were removed.
- Four 45-degree thin wall colliders had been authored into the deterministic
  map as their world AABBs, producing square blocked regions. Obstacles now
  bake center, axes and half-extents and rasterize as oriented rectangles per
  radius layer.
- `TryGetAdvanceTarget` used forward projection; tied projections made blue
  top/bottom lanes stop at their middle waypoint. Team spawn proximity now
  selects the opposite centerline endpoint.
- Low lane-guide costs let the shorter middle lane own top/bottom spawn cells.
  Map-owned offline guide costs now keep each route on its authored skeleton;
  all six directions visit their B/M/R points in order and end at the enemy
  foundation.

## 4. Decision log

- Use current formal designs as authority and legacy Git only as a read-only
  implementation/visualization reference, per D-024.
- Keep all pathfinding visualization Editor/presentation-only. It reads baked
  data and never writes `PhysicsEntity2D`, routes, snapshots, or checksums.
- The Map prefab is the single authoring owner of map config, three lanes, six
  baked field references and the visualizer. Bootstrap scenes load that data
  from the prefab instead of maintaining scene-local copies.
- Preserve the existing authoritative UID, Command, Snapshot, Aim,
  AbilitySignal, Checksum, and fixed-point contracts.
- Keep specific Varus assets as an explicitly requested test hero fixture;
  no champion-specific branch is added to deterministic framework code.

## 5. Current repository context

Relevant assemblies and code:

- `FrameSyncMoba.Unit`: `Unit`, Handlers, `UnitLocomotionAgent`,
  `AStarPathService`, `TeamFlowFieldService`, `DeterministicRVOSystem`,
  `MovementHandler`.
- `FrameSyncMoba.FrameSync`: `UnitPresentationHost`,
  `UnitAnimationDriver`, `PresentationSocketSet`, VFX presentation.
- `FrameSyncMoba.Bootstrap`: `GameBootstrap`, current UI controllers and
  composition root.

Relevant assets:

- `Assets/Config/FullMatchTest/Prefabs/`
- `Assets/Config/FullMatchTest/Animation/`
- `Assets/Resources/Prefab/Unit/`
- `Assets/Resources/Animation/`
- `Assets/Resources/Prefab/UI/`
- `Assets/Scenes/ClientBootstrap.unity`
- `Assets/Config/FullMatchTest/FullMatchDeterministicMapConfig.asset`
- `Assets/Config/FullMatchTest/FullMatchMinionWaveConfig.asset`

Existing focused tests:

- `Assets/Scripts/Gameplay/Tests/AStarPathfindingTests.cs`
- `Assets/Scripts/Gameplay/Tests/FlowFieldBuildTests.cs`
- `Assets/Scripts/Gameplay/Tests/RVOSystemTests.cs`
- `Assets/Scripts/Gameplay/Tests/PathfindingIntegrationTests.cs`
- movement and snapshot tests under the same directory.

## 6. Design sources

- `Docs/Design/unit_behavior_framework_design_v27_3.md`
  - Unit root, prefab-authored Handlers, PhysicsEntity2D binding.
- `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`
  - UnitPresentationHost, UnitAnimationDriver, Animator parameters,
    PresentationSocketSet.
- `Docs/Design/MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md`
  - UIManager, UIPage, UIPanel, page roots, lifecycle and prefab adjustment.
- `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md`
  - route selection, A*, team flow fields, RVO, combined Tick order.
- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`
  - PhysicsEntity2D ownership and presentation-only Gizmos.
- `Docs/Design/moba_non_hero_unit_modules_design_v5.md`
  - minion/tower composition and AI behavior.

## 7. Scope

### In scope

- Correct components and serialized references first on current test Unit
  prefabs, then on the seven formal Unit prefabs after focused validation.
- Generic Animator/profile/socket binding required to make both the fixtures
  and formal resources qualified units.
- Formal page-prefab lifecycle and UIManager composition for existing UI.
- Focused pathfinding fixes and deterministic integration tests.
- Editor-only baked flow-field visualization.

### Out of scope

- Final production hero kit, final balance, art, animation clips, VFX, audio,
  equipment catalog, jungle content, or UOS deployment.
- New network protocol, snapshot schema, fixed-point type, or Package.
- Restoring the legacy RVO2 package or legacy float/asynchronous pathfinding.
- Rebuilding or uploading a Player.

## 8. Implementation plan

1. Inspect prefabs, animation controllers/profiles, and scene UI through MCP.
2. Repair generic prefab composition on the full-match fixtures and validate
   required components against each `UnitPrototype`.
3. After that validation passes, apply the same binding rules to the formal
   Unit prefabs and connect the complete available animation set.
4. Add minimal `UIPage`, `UIPanel`, and `UIManager` production types in
   Bootstrap, migrate existing page controllers to their prefabs, and make
   `GameBootstrap` request pages through the manager.
5. Inspect and run the four focused pathfinding suites. Compare failing or
   missing behavior with current design and selected legacy algorithms.
6. Implement only confirmed deterministic pathfinding corrections.
7. Add a current-data Editor Gizmo visualizer for grid, blocked cells, and
   team flow directions.
8. Compile and run focused EditMode/PlayMode checks proportional to changed
   logic and Unity lifecycle.

## 9. Public contracts

Planned new Bootstrap presentation contracts:

- `UIPage` stable page identity.
- `UIPanel` prefab-host lifecycle.
- `UIManager.OpenPage`, `ClosePage`, `TryGetPage`, and fixed refresh.

No Gameplay/network public protocol or snapshot contract is planned to
change. Any discovered need to change one is a failure condition.

## 10. Validation

- Unity MCP compilation: passed; no Error logs in the final Console window.
- MCP asset validation: seven formal Unit prefabs have exactly one root Unit,
  PhysicsEntity2D and each required Handler; 32 relevant Unit/UI prefabs and
  both endpoint scenes contain zero missing scripts.
- EditMode: `IntegratedPathfindingPipelineTests` 4/4,
  `SimulationTickContextTests` 9/9, `AttackHandlerTests` 8/8 and
  `UnitAnimationAssetTests` 7/7 passed.
- PlayMode: `UIManagerPrefabPlayModeTests` 1/1 and
  `ClientBootstrapFirstWavePlayModeTests` 1/1 passed.
- The first-wave smoke loaded the real ClientBootstrap, persisted and loaded
  six flow fields, spawned 18 minions at Tick 900, observed a FlowField
  LaneAdvance route, and observed subsequent movement.
- Initial real-map validation: `MapPathfindingAssetIntegrationTests` 5/5 and
  `MapPathfindingPrefabPlayModeTests` 1/1 passed. These cover Map ownership,
  oriented thin-wall rasterization, ordered six-route flow traces, every A*
  lane segment, deterministic Flow+RVO progress, and prefab lifecycle.
- Follow-up visual correction validation: real-map pathfinding 9/9, A* 15/15,
  flow build 9/9, RVO 5/5 and integrated pipeline 4/4 passed. The Map prefab
  PlayMode test and real ClientBootstrap first-wave PlayMode test each passed
  1/1. The baked wall long axes now match their BoxColliders; lane skeletons
  snap to their owning grid-center column; distance-scaled flow direction uses
  forward/mixed/inward Dir8 bands; arrival targets render as explicit markers.
- OwnerLane-boundary follow-up: fixed-stride visualization no longer omits
  inter-lane boundary cells. A compiled six-field audit covered 5,774 non-target
  boundary cells, recovered 5,410 stride-skipped cells, and found zero missing
  directions or undrawn boundaries. The added focused Test Runner case is
  compiled but its rerun is pending because the user's open `GameScene` is dirty.
- Post-migration composition inspection found one Map-owned authoring source,
  three lanes and one visualizer in Client, with no scene-local map authoring
  in Server. The focused Client first-wave smoke then passed again in 6.6 s.
- Real-map minion/tower follow-up: all six `LaneAuthoring` team spawns now use
  `Map/MinionSpawns`; melee/ranged stable sub-kind IDs are baked; tower orders
  are no-chase and use stable formal priority bands. `NonHeroTopologyTests`
  passed 8/8. The focused real ClientBootstrap PlayMode case passed 1/1 and
  verified 18 first-wave units, lane movement, tower projectile Combat damage,
  formal minion death, AI unregister, immobile towers and base-win closure.
- Blue/red tower formal and runtime prefabs each have one presentation host,
  one Animator and one driver using the authored Idle/Death clips. No attack
  clip or attack-state substitution was invented.
- A full `FrameSyncMoba.Unit.Tests` diagnostic run reported no pathfinding
  failures; ten pre-existing non-pathfinding tests still fail (combat/movement
  defaults, Dash fixtures, and two tests that assert the superseded
  post-Tick-context restriction).

## 11. Failure and recovery

- Stop if a fix requires a new Package, a duplicate protected protocol, or a
  change to current Snapshot/Command semantics.
- Prefab and scene edits are made through Unity MCP and saved in small groups.
- Code edits remain separable by Unit presentation, UI lifecycle, and
  pathfinding so a failed group can be diagnosed without discarding user
  assets or unrelated working-tree changes.

## 12. Results

Completed. Formal Unit assets, page-prefab UI lifecycle, route selection,
static collision constraint, baked team flow fields, deterministic RVO
integration and Editor visualization are connected and focused-tested.
The real Map prefab now owns the current grid topology and exact three-lane
routes; rotated bars retain their shape, and blue/red flow traces reach every
authored waypoint in the required order.
The four diagonal bars also retain the same long-axis orientation as their
source BoxColliders. Top/bottom straight-lane cells follow the authored
tangent, while off-lane cells converge progressively instead of immediately
turning perpendicular.
The formal Varus controller exposes all nine available clips, and every
cross-system simulation-time read uses `SimulationTickContext.Current`.

Remaining non-blocking limits:

- Tower locked-target projectile gating and red-line presentation still need a
  dedicated conformance slice; current real-map targeting, damage, death and
  match closure are verified independently of that presentation feature.
- `AbilityStageProgress` is currently presentation-only elapsed Tick data;
  normalized stage duration requires a later explicit presentation projection.
- The Test Runner emits one `<null>` missing-script warning during scene-test
  setup, while direct MCP inspection finds zero missing scripts in both
  endpoint scenes and all 32 relevant prefabs.
- Final Scene-view color/readability remains an operator visual-acceptance
  item; it does not affect the verified baked data or Gameplay calculations.

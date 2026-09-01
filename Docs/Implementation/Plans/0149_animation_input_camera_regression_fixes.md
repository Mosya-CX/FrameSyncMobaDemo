# ExecPlan 0149 — Animation, Ability Input and Locked-Camera Regression Fixes

Plan ID: 0149
Status: Completed
Created: 2026-08-29
Completed: 2026-08-29
Risk: Medium
Design conformance: Strict
Estimated code delta: 180-320 lines across source and focused tests
Affected assemblies: `FrameSyncMoba.FrameSync`, `FrameSyncMoba.PlayerInput`, `FrameSyncMoba.Physics`, `FrameSyncMoba.Bootstrap`, and focused test assemblies
Design sources: Presentation v13.2 plus D-052; Player Input v1.1 §§9, 15.3, 17.4 and 21.4; Ability v15.2; Pathfinding v13.1 presentation projection contract
Decision dependencies: D-045, D-047, D-048, D-052
Validation basis: Unity compilation and Console; focused EditMode PlayerInput/Gameplay tests; focused PlayMode Animator, Input System, Physics projection and Camera tests

## 1. Purpose

Correct three rebuilt-client regressions reported after ExecPlan 0148: a
moving unit can visually freeze while casting, Varus W-to-Q can appear to lose
W and fail to expose Q's indicator/cast flow, and a locked camera can jitter
when the followed hero changes facing frequently. Preserve deterministic
Gameplay authority and make no build request.

## 2. Progress

- [x] Resolve current design authority, baseline Console and relevant runtime
  topology.
- [x] Identify the pending-request indicator contract mismatch and the coupled
  position/rotation interpolation clock in `PhysicsEntity2D`.
- [x] Reproduce the movement-cast Animator route and fix the smallest complete
  presentation slice.
- [x] Keep ability indicators continuous across `FocusRequested`,
  `GameplayFocusing` and `CommitRequested`; cover Varus W-on then Q-Focus.
- [x] Decouple position and rotation presentation interpolation so facing
  changes cannot restart movement projection.
- [x] Compile, run focused EditMode/PlayMode tests and inspect the Console.
- [x] Run the required first independent read-only review, resolve findings,
  and update current status/handoff documentation.

## 3. Repository facts and discoveries

- The latest available UOS client logs predate the current animation build and
  contain no matching exception, so this pass relies on code-path reproduction
  and focused Unity tests.
- Player Input v1.1 requires a preparatory indicator while `FocusRequested` and
  requires the indicator to remain during `CommitRequested` until Gameplay
  advances. `PlayerInputController.UpdateIndicator` currently recognizes only
  `LocalAiming` and `GameplayFocusing`.
- Varus Q intentionally consumes an active W Toggle when Q's charge Stage
  enters and starts W cooldown. That W-off transition after Q Focus is expected;
  an isolated W press must remain active, and W must not block Q.
- `PhysicsEntity2D.ProjectPresentationPose` currently restarts one shared
  interpolation timer when either target position or target rotation changes.
  Frequent facing changes therefore repeatedly restart position interpolation;
  the locked camera exactly follows that projected root and exposes the jitter.
- ExecPlan 0148 added zero-duration Animator evaluation on route changes to
  resolve locomotion clips before same-frame sampling. Moving-cast routing must
  be tested against the real Varus controller before retaining or narrowing
  that evaluation.
- The bound Varus regression confirmed that a newly selected ability Stage must
  not receive a second zero-time Animator evaluation, while an `IsMoving`
  change during an existing movable cast must still resolve its parameter-
  driven idle/walk route in the same presentation update.

## 4. Scope

### In scope

- Real-controller movement/cast transition reproduction and a presentation-only
  routing/sampling correction.
- Pending-request indicator visibility and Varus W/Q input/runtime coverage.
- Independent position and rotation interpolation state in
  `PhysicsEntity2D`, plus locked-camera regression coverage.
- Focused tests and current implementation documentation.

### Out of scope

- Ability balance or the intentional rule that Varus Q consumes active W.
- Gameplay Tick, Command ordering, Snapshot/checksum/wire schema, rollback
  semantics or authoritative movement changes.
- Camera redesign, new packages, asset rebundling or Player/Server builds.

Snapshot/serialization/checksum impact: none. All changed state is local
presentation/input state and reconstructible from existing Gameplay data.

## 5. Implementation plan

1. Add a real Varus Animator regression test for moving into Q Focus and back to
   locomotion; use its failure to narrow zero-duration route resolution and loop
   sampling.
2. Make indicator eligibility match Player Input v1.1 for LocalAiming,
   FocusRequested, GameplayFocusing and CommitRequested, without permitting
   duplicate Commands.
3. Add a cross-slot requester/Gameplay test proving W remains active in
   isolation, Q Focus is submitted while W is active, Q becomes focusing, and
   the intentional consume happens only when Q executes.
4. Split `PhysicsEntity2D` position and rotation target/elapsed tracking and
   add PlayMode coverage showing rotation churn does not alter position
   interpolation progress or locked-camera offset.
5. Compile through Unity, run focused EditMode and PlayMode suites, inspect
   Error/Exception logs, review the diff, then update current status evidence.

## 6. Validation

- Unity script refresh/compilation and empty new Error/Exception Console.
- Focused `FrameSyncMoba.PlayerInput.Tests` for pending-state and cross-slot
  request ordering.
- Existing D-047 Varus Toggle/Charge Gameplay tests plus any focused addition
  needed for the reported sequence.
- Real-controller PlayMode test for moving Q Focus animation progression and
  return to locomotion.
- `PhysicsEntity2DPlayModeTests` and `CameraControllerPlayModeTests` covering
  frequent direction changes during positional interpolation.
- One independent read-only review under the user's review policy.

## 7. Failure and recovery

All edits remain ordinary working-tree changes. No scene, prefab or controller
YAML will be edited manually. If real-controller behavior requires an asset
change, perform it through Unity APIs and re-run the asset/PlayMode tests. No
build command is sent; rebuilt-client acceptance remains user-owned.

## 8. Results

- `UnitAnimationDriver` skips the extra zero-time Animator evaluation only on
  the frame where `PlayAbilityStage` already selected a new Stage. Movement
  changes during an existing movable cast still resolve immediately, covering
  Varus Q's channel-idle/channel-walk routes without freezing loop sampling.
- `PlayerInputController` keeps aim indicators visible through
  `FocusRequested`, `GameplayFocusing` and `CommitRequested`. A pending no-aim
  Toggle such as Varus W continues scanning other slots and cannot create,
  retain or suppress an indicator.
- `PhysicsEntity2D` now owns separate elapsed clocks for position and rotation
  projection, so rapid facing changes no longer restart position interpolation
  or stall the exactly-following locked camera.
- Unity synchronous refresh compiled with no new Error/Exception Console entry.
  The four new focused PlayMode regressions pass: real bound Varus Q animation,
  W-then-Q input/indicator flow, facing-churn position interpolation and locked-
  camera follow. The existing bound Aatrox locomotion regression also passes.
- Full FrameSync EditMode is `113 passed / 1 retained failure`; the retained
  failure is the pre-existing missing `PrefabId 1101` fixture. Broad Bootstrap
  PlayMode is `31 passed / 9 retained failures`, unchanged in the known smoke-
  scene, spawn-fixture, pre-partition HeroTest lookup, UOS cancellation and Lua
  page categories. PlayerInput `36/36` and Physics PlayMode `32/32` were also
  recorded during this plan.

## 9. Review

The required first independent read-only review reported two P2 findings and no
P0/P1: the initial narrowing incorrectly skipped all casting-time movement route
refreshes, and the first Varus test drove the Animator directly instead of the
bound Gameplay Unit/Host/Driver path. Both findings were resolved and the
replacement regression passes. Under the user's review policy, no second review
was started because there was no P0 and not multiple P1 findings.

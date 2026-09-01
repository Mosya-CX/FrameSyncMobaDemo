# ExecPlan 0150 — Live Client Combat and Presentation Regression Closure

Plan ID: 0150
Status: Completed
Created: 2026-08-29
Completed: 2026-08-30
Risk: High
Design conformance: Strict
Estimated code delta: 300-700 lines across source, assets and focused tests
Actual code delta: approximately 1,200 changed text lines across 27 tracked
source/test/config files against the shared pre-0148 HEAD; the uncommitted
0148/0149/0150 worktree prevents an exact plan-only Git split
Affected assemblies: Gameplay Attack/Ability, FrameSync, PlayerInput, Physics,
Bootstrap, presentation assets, Editor migration tooling and focused tests
Design sources: FrameSync v10.2; Snapshot Appendix v7.2; Unit Framework v27.4;
Combat v13.2-v13.4; Attack v6.2 §§4-6; Ability v15.2; Player Input v1.1
§§9-11, 15.3, 17.4 and 21.3-21.4; Presentation v13.2; Unit Physics v13.1
Decision dependencies: D-004, D-011, D-012, D-015-D-017, D-045,
D-047-D-052
Validation basis: supplied UOS client/server logs; Unity compilation and
Console inspection; deterministic EditMode rollback coverage; focused
Input/Animator/Camera/indicator PlayMode tests; independent read-only review

## 1. Purpose

Close the six actionable client regressions from the latest UOS build:
locked-camera jitter, transient Varus W projection, premature Varus Q indicator
closure, Aatrox Q VFX timing drift, generic indicator magenta blocks and the
awkward Varus attack opening. Preserve the observed Tick 5152 checksum failure
instead of suppressing it and emit enough matching server/client world state to
identify the first differing field in the user's next live run. Packaging stays
user-owned.

## 2. Progress

- [x] Resolve current design authority and inspect the supplied UOS evidence.
- [x] Add two minimal command-replacement/rollback equivalence regressions; both
  pass, so do not claim a source root cause for the live Tick 5152 mismatch.
- [x] Add symmetric server/client mismatch diagnostics covering world, Unit,
  actions, attack, locomotion, projectile and lifecycle state. Keep the full
  server dump behind D-032's explicit `-checksumDetail` flag and, per the
  user's direction, do not add a separate logging-only test.
- [x] Reconcile local ability receipts against the last completed Gameplay Tick;
  retain Q through pending Focus/Commit and prevent a no-aim W request from
  hiding another slot's indicator.
- [x] Separate followed-root position and rotation interpolation clocks and keep
  the locked camera as the sole late follow writer with bounded smoothing.
- [x] Rebuild generic indicator runtime materials on built-in `Sprites/Default`
  while copying source texture/tint, covering Varus indicators and Aatrox W/E.
- [x] Scale Aatrox Q VFX lifetime by the runtime Gameplay TickRate and retain
  logic-time attack/loop animation sampling for Varus attack entry.
- [x] Run focused EditMode/PlayMode verification and refresh Unity compilation.
- [x] Resolve the required first independent read-only review, correct its one
  P2 and two P3 findings, and update current status documentation.

## 3. Repository facts and discoveries

- Client A diverged at authority replay Tick 5152 while canonical Commands
  matched; Client B did not. The last known-good comparison was Tick 5146 after
  Varus attacked tower `3/1302/7` and Blight was involved.
- Minimal predicted-Move replacement, frozen-anchor replay and active-route
  Snapshot/Restore regressions all reproduce cleanly. The live first differing
  member therefore remains unknown. The user explicitly chose richer live
  state logging followed by another UOS run instead of further speculative
  changes in this slice.
- `PlayerCommandRequester` previously reconciled receipts against the currently
  executing static Tick. Bootstrap now supplies `Runtime.LastCompletedTick`,
  preventing a future-target request from being cleared before its Gameplay
  result can exist.
- Q release is intentionally inert. Q remains visible through local aiming,
  `FocusRequested`, `GameplayFocusing` and `CommitRequested`; left click owns the
  Commit and Gameplay closure owns the final hide.
- `PhysicsEntity2D` was restarting one shared interpolation clock for both pose
  channels. Rapid facing changes could therefore continually restart position
  interpolation even when the logical position target had not changed.
- Camera following was already in `LateUpdate`; unrelated visual systems do not
  need to be moved there. `CameraController` has explicit late execution order
  and follows the already projected Unit root.
- The generic indicators are textured Quad geometry. The visible circle/ring/
  line shape comes from texture alpha, so a reliable runtime material must keep
  `_MainTex` and `_Color`. The built-in `Sprites/Default` route avoids the custom
  Shader retention failure seen in the rebuilt Player.
- Aatrox Q Stage authoring stores `ImpactDelayTicks`; dividing by a hard-coded
  30 produced incorrect presentation duration when Gameplay TickRate changed.

## 4. Design sources and traceability

- `Docs/Design/MOBA_Player_Input_Command_Module_Design_v1_1.md` §§15.3,
  17.4, 21.3-21.4:
  pending Focus/Commit continuity and release semantics are protected by
  `PlayerInputSimulationPlayModeTests` HoldRelease and W-then-Q cases.
- `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`
  plus D-052: client-only animation clocks, interpolation and presentation
  authority are protected by `VarusAnimationPlayModeTests`,
  `UnitAnimationAssetTests` and `AnimationSamplingTests`.
- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`: presentation may
  interpolate deterministic poses but never write Gameplay. The rapid-facing
  regression is protected by `PhysicsEntity2DPlayModeTests` and
  `CameraControllerPlayModeTests`.
- FrameSync v10.2 and Snapshot Appendix v7.2: checksum enforcement and ordinary
  Restore/Replay boundaries remain unchanged. The new focused proofs are
  `FrameSyncPipelineTests.ReplacedPredictedMove_RestoreThenAuthoritativeMoveMatchesCleanReplay`
  and
  `AuthorityReplicationTests.AcceptedRelayReplacingExecutedLocalMove_ReplaysFromFrozenAnchor`.
- Ability v15.2 and D-045: Aatrox Q VFX lifetime derives from the current runtime
  TickRate; `AatroxFormalContentTests.DarkinBlade_VfxDurationUsesConfiguredGameplayTickRate`
  guards the conversion.

## 5. Scope

### In scope

- Live checksum comparison diagnostics without weakening mismatch enforcement.
- Local W/Q request projection and indicator lifetime.
- Locked-camera and followed-root presentation scheduling/interpolation.
- Generic indicator material construction and shipped texture preservation.
- Aatrox Q VFX timing and Varus attack-entry presentation.
- Focused tests and current implementation documentation.

### Out of scope

- Guessing a deterministic-state fix before the next live world dump identifies
  the first differing member.
- Balance changes, redesigned ability mechanics or a new input mode.
- A third-party package or general rendering/camera framework rewrite.
- Windows Client or Linux Dedicated Server packaging commands.
- Unrelated cleanup in the already-dirty working tree.

Snapshot/serialization/checksum implications: no schema, serialized member,
checksum membership, wire or Restore semantic changed. Diagnostics only read an
already captured `GameplaySnapshot` after a mismatch or on selected server
command ticks.

## 6. Implementation plan

1. Reproduce the smallest rollback command-replacement paths around the supplied
   mismatch and retain exact assertions even when no source divergence appears.
2. Print matching aggregate, subsystem and per-Unit world facts on server and
   client so the next UOS logs identify the earliest differing member.
3. Drive request reconciliation from a completed-Gameplay-Tick provider and
   preserve the formal Focus/Commit indicator state machine.
4. Separate Unit pose interpolation channels; follow the projected root once in
   late camera update with configurable smoothing and snap distance.
5. Construct generic runtime indicator materials from a Player-retained built-in
   Shader and preserve source texture/tint.
6. Remove the fixed 30 Hz Aatrox Q presentation conversion and verify real
   Animator assets keep attack/locomotion motion-time bindings.
7. Compile through Unity, run focused suites, review the diff independently and
   update the plan/module status/current handoff.

## 7. Public contracts and ownership

- `FrameSyncGameRuntime.LastCompletedTick` is a read-only Bootstrap-facing
  observation of the Runtime-owned simulation boundary.
- `PlayerCommandRequester` accepts an optional `Func<int>` completed-Tick
  provider; existing construction keeps the prior static-context fallback for
  tests and non-Bootstrap callers.
- `MobaCameraPresentationConfig` owns client-only locked-follow smoothing/snap
  values. They do not enter Gameplay, Snapshot, checksum or networking.
- `ChecksumDiagnosticFormatter` remains internal to FrameSync and introduces no
  protocol type.
- No authoritative UID, Command, Snapshot, ability or input DTO was duplicated
  or changed.

## 8. Validation

- Unity refresh/compilation completed with the Editor idle. The final isolated
  Unity Console query contains zero Error and zero Exception entries. An earlier
  MCP log-file lock was transient and the final clear/refresh succeeded.
- PlayerInput EditMode: `37 passed / 0 failed`.
- FrameSync EditMode: `117 passed / 0 failed`.
- Exact PlayMode regressions pass for HoldRelease, W-then-Q, one-shot W Toggle,
  generic indicator framebuffer/materials, locked-camera smoothing, rapid
  facing, Physics position interpolation, bound moving Varus Q animation and
  Aatrox locomotion routing.
- Exact Aatrox variable-TickRate VFX test and formal animated-Unit attack/move
  binding test pass.
- Post-review exact regressions pass: future local commands survive authority
  correction/replay, and replacing/disabling a controller mid-Q hides the old
  indicator without losing the still-pending Q projection.
- Broad Bootstrap PlayMode: `30 passed / 10 retained failures`; the relevant
  generic-indicator failure was corrected and its exact rerun passes. The other
  failures are the recorded scene fixture, old monolithic HeroTest lookup,
  cancellation, UOS configuration and Lua-page categories.
- Broad Unit EditMode: `551 passed / 11 failed` before the new Aatrox assertion
  was corrected; its exact rerun passes. The remaining ten are the recorded Unit
  baseline categories and were not expanded by this slice.
- No Player or Dedicated Server build was requested or sent.

## 9. Independent review

The required first read-only high-risk review reported no P0/P1 findings. Its
one P2 found that full server world dumps had accidentally entered the default
command-Tick hot path; this was corrected to D-032's explicit
`-checksumDetail` gate and now emits every Tick only in the diagnostic run. Its
first P3 found stale indicators could survive controller disable/driver swap;
the controller now hides them and the real Input System test covers both paths.
Its second P3 found that the rollback regression did not exercise future
command reinsertion; the authority test now queues a command at the replay-end
Tick and asserts it remains after correction. Both exact tests pass. No second
review was run because the user permits one only after a P0 or multiple P1s.

## 10. Failure and recovery

All edits remain ordinary working-tree changes and preserve unrelated user
changes. No checksum or exception guard is disabled. If the next UOS mismatch
shows a public Snapshot/wire semantic defect, that follow-up stops for explicit
approval before changing its contract. The user can rebuild matching endpoints
from the current source when ready.

## 11. Results

Source implementation, focused Unity verification, independent review and
current documentation are complete. PlayerInput uses the completed Gameplay
boundary; W/Q indicators survive the correct pending states and clean up on
rebind/disable; Unit pose channels and the late locked camera no longer restart
one another; generic indicators preserve their texture-alpha shapes on a
built-in Player Shader; Aatrox Q VFX duration follows the configured TickRate;
and attack/loop animation sampling remains logic-time based. The known
limitation is explicit: Tick 5152's live deterministic divergence has not been
assigned a root cause. Start the rebuilt server with `-checksumDetail` for the
next diagnostic UOS run; the client writes the matching detail automatically if
the replay mismatch remains.

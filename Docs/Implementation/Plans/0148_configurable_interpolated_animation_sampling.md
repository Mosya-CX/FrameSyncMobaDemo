# ExecPlan 0148 — Configurable Interpolated Unit Animation Sampling

Plan ID: 0148
Status: Completed
Created: 2026-08-29
Completed: 2026-08-29
Risk: Medium
Design conformance: Strict
Estimated code delta: 350-550 lines plus six Animator Controller migrations
Actual code delta: 1,449 additions / 45 deletions across C# source and tests,
plus six Animator Controller and affected config/profile migrations
Affected assemblies: `FrameSyncMoba.Unit`, `FrameSyncMoba.FrameSync`, `FrameSyncMoba.Bootstrap`, their EditMode/PlayMode test assemblies
Design sources: `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md` §§3.5-3.6; `Docs/Design/moba_attack_module_design_v6_2.md` §§4.2 and 7; `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md` §8.9
Decision dependencies: D-001, D-022, D-023, D-045, D-048
Validation basis: Unity compilation and Console; focused FrameSync/Unit EditMode tests; formal Animator asset tests; focused Bootstrap PlayMode Animator tests

## 1. Purpose

Let client unit animation progress be sampled at a presentation-owned frequency
that is configurable independently of Gameplay `TickRate`. Between animation
samples, render frames interpolate normalized progress. Deterministic Gameplay
continues to own state selection and action timing; animation sampling remains
client-only and reconstructible.

## 2. Progress

- [x] Resolve authority, current controller topology, clip lengths and baseline.
- [x] Add the shared client animation-sampling configuration and continuous
  read-only simulation-time projection.
- [x] Add a reusable interpolation sampler and apply it to attack Motion Time.
- [x] Apply interpolated, modulo loop sampling to formal Idle/Move states while
  retaining live `MoveSpeed` playback-rate ownership.
- [x] Correct the Ready-boundary attack progress regression discovered by the
  first animation-speed review.
- [x] Migrate all formal controllers/config assets through Unity APIs.
- [x] Compile, run focused EditMode/PlayMode/integration tests and inspect the
  final Console.
- [x] Run the first independent read-only review required by the user's review
  policy.
- [x] Update the Current presentation design, decision/status evidence and
  current handoff.

## 3. Repository facts and discoveries

- `UnitAnimationDriver` currently samples deterministic attack progress in
  every `LateUpdate`; attack progress is stepped at Gameplay Tick cadence.
- Six formal non-Structure controllers bind attack states to
  `AttackMotionTime`. Their Idle/Walk/Move clips are looped but currently
  free-run locally through Animator state speed.
- Aatrox owns three differently sized Walk clips; Varus owns normal and Q
  channeling Walk clips. Runtime loop sampling must therefore read the active
  clip length rather than assume one duration per unit.
- The formal client presentation asset currently owns render pacing and
  logic-pose smoothing. It is the existing client-only global configuration
  root and can also own animation sample rate without entering Gameplay data.
- At `NextAttackReadyLogicTick`, the current attack animation snapshot returns
  recovery progress zero while retaining `ImpactCommitted`, which can move
  Motion Time from nearly one back to 0.5 on the exit frame.
- The working tree contains extensive unrelated user changes; this plan will
  edit only the declared animation/config/test/document slice.

## 4. Design sources and traceability

- Presentation v13.2 §§3.5.2 and 3.6.5
  -> `UnitAnimationAssetTests` verifies public parameters and Motion Time
  bindings across every formal view.
- Attack v6.2 §4.2 and presentation mapping
  -> focused `AttackHandlerTests` verifies Start/Impact/Ready progress including
  the Ready boundary at multiple TickRates.
- D-001/D-045 Tick and monotonic pacing ownership
  -> FrameSync tests verify that the presentation clock projects, but never
  changes, Gameplay Tick scheduling.
- User requirement: presentation-owned sample frequency and interpolation
  -> pure sampler tests cover frequency independence, interpolation, disabled
  interpolation, loop wrap and time regression/reset.
- Unity Animator lifecycle
  -> focused Bootstrap PlayMode tests verify the migrated controllers accept
  externally sampled loop Motion Time without self-reentry.

## 5. Scope

### In scope

- One configurable global unit-animation synchronization frequency and an
  interpolation toggle in the existing formal client-presentation config.
- A read-only continuous simulation-time projection derived from the current
  completed Gameplay Tick plus the Unity scheduler's sub-Tick accumulator.
- Interpolated attack Motion Time and interpolated modulo Idle/Move loop phase.
- Immediate resampling on deterministic state changes, rollback/time regression
  and attack phase boundaries.
- Formal controller/config migration, tests and current documentation.

### Out of scope

- Gameplay TickRate, Tick execution order, Commands, prediction limits or
  AuthorityFrame protocol changes.
- Network transmission of Animator state or presentation samples.
- Ability-specific Clip retiming beyond existing deterministic stage entry.
- Death/respawn Clip duration redesign, root motion or animation-event Gameplay
  authority.
- Player or Dedicated Server builds.

Snapshot/serialization/checksum impact: none. The new clock, sampler and loop
anchors are presentation-only, are not restored, and rebuild from current
Gameplay state after rollback. Unity assets: one formal presentation config,
six non-Structure controllers and affected controller/profile tests.

## 6. Implementation plan

1. Add presentation-owned synchronization settings and a continuous time view
   in `FrameSyncMoba.FrameSync`; publish the view from `GameBootstrap.Update`
   after simulation advancement.
2. Add a reusable configurable progress sampler that supports forward
   interpolation, hold sampling, looped unwrapped phase and clock regression.
3. Extend the attack presentation snapshot only with existing Start/Impact/
   Ready timing fields and correct recovery progress at Ready without changing
   `IsAttacking` half-open-cycle semantics.
4. Update `UnitAnimationDriver` to resample attack progress and Idle/Move loop
   phase at the configured rate, interpolate each render frame, and re-anchor
   on deterministic state/variant/speed changes.
5. Add `LoopMotionTime` to the formal controller/profile contract and migrate
   all six animated controllers through Unity Animator APIs. Towers keep no
   attack state.
6. Add pure/EditMode and PlayMode coverage, compile through Unity, inspect
   Console and review semantic asset diffs.
7. Run one independent read-only review, resolve scope-local findings, then
   update formal/current-state documentation.

## 7. Public contracts and ownership

- `UnitAnimationSynchronizationSettings` — client presentation configuration,
  owned by `FrameSyncMoba.FrameSync`; never authoritative Gameplay input.
- `AnimationPresentationTime` — read-only projection published by Bootstrap and
  consumed by presentation; it does not own or advance a Tick.
- `ConfigurableAnimationProgressSampler` — presentation interpolation helper;
  no serialization or cross-network identity.
- `AttackAnimationSnapshot` gains read-only existing timing projections only;
  `AttackHandler` remains their owner.
- `LoopMotionTime` is a public Animator Float parameter and profile hash owned
  by Presentation v13.2.

No assembly dependency is reversed: Bootstrap already references FrameSync;
FrameSync already references Unit; Unit remains independent of Bootstrap.

## 8. Validation

- Unity AssetDatabase refresh and script compilation with no new errors.
- Focused pure sampler EditMode tests at mismatched Gameplay/sample rates,
  interpolation on/off, loop wrap and clock regression.
- Focused attack animation snapshot tests at Start, Impact, Ready and after
  Ready for at least two TickRates.
- `UnitAnimationAssetTests` for every formal Hero/Minion controller and both
  Structure exclusions.
- Focused Aatrox/Varus/minion Animator PlayMode checks for externally driven
  loop Motion Time and unchanged transition topology.
- Relevant FrameSync/Bootstrap test assemblies, with any retained unrelated
  baseline failure identified exactly.
- Final Unity Console Error/Exception inspection and semantic controller diff
  audit.

## 9. Independent review

The required first independent read-only review found frame-skip endpoint,
mid-segment rate-change, asynchronous View/rollback phase-anchor, shortened
attack-boundary segment, `LateUpdate` ordering, stop-frame `MoveSpeed` and stale
30 Hz-comment issues. All seven were corrected and covered before rerunning the
focused suites.

Because that review contained several P1 findings, the user's policy allowed a
second independent review. It found no P0, one P1, two P2 and one P3: a static
clock could leak the previous match's Tick; the locomotion change frame could
sample the Animator's old state; the PlayMode evidence did not run the real
Driver binding; and one comment still named `LateUpdate`. The final revision
binds the clock to `UnitWorld`, publishes `FrameSyncGameRuntime.LastCompletedTick`,
clears only the owning world, resolves routing changes with a zero-duration
Animator evaluation before sampling, adds a bound logic-Unit/View/Driver test,
and fixes the comment. There was only one P1 in the second review, so no third
review was started under the user's stated limit.

## 10. Failure and recovery

All source/document edits remain ordinary Git working-tree changes. Controller
migration is idempotent: rerunning it restores the required parameters and
state bindings. If Unity asset mutation fails, preserve the pre-existing assets,
record the MCP failure and require a final Unity API audit before completion.

No build is sent. Rebuilt-client visual acceptance remains user-owned.

## 11. Results

Implemented D-052 without adding `PresentationTick` or changing Gameplay Tick,
Snapshot, checksum, Command or network contracts. The formal default is 20 Hz
with optional render interpolation. Attack Motion Time is bounded by locked
Start/Impact/Ready timing; looped locomotion uses clip length, current playback
rate and a reconstructible logic-time epoch. The clock is match-isolated by
`UnitWorld` identity and is absent from Dedicated Server publication.

Unity forced synchronous refresh compiled with empty isolated Console Error and
Exception queries. Focused results: animation sampling/clock 13/13, formal
Animator assets 6/6, AttackHandler 21/21, Aatrox bound-Driver PlayMode 9/9 and
presentation config PlayMode 3/3. Full FrameSync is 113 passed / 1 retained
failure: `BootstrapDeterminismProbeTests.ServerFirstTick_MatchesClientPredictionFirstTick`
still lacks PrefabId 1101 in its pre-existing fixture. Previously recorded full
Unit 551 passed / 10 retained failures and broad Bootstrap PlayMode 27 passed /
9 retained failures were not changed by this presentation slice. No build was
sent; rebuilt-client visual comparison at multiple animation rates remains
user-owned.

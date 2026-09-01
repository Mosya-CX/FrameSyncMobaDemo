# ExecPlan 0154 — Live W Receipt Race, VFX Warmup, Reward Radius and CDN Guide

Plan ID: 0154
Status: Completed
Created: 2026-08-31
Completed: 2026-08-31
Risk: High
Design conformance: Strict
Estimated code delta: 220-420 lines across PlayerInput, FrameSync,
Bootstrap/client presentation tests and current operational documentation
Actual code delta: PlayerInput receipt reconciliation and regression coverage;
client VFX warmup/pool/timing diagnostics with selected-hero filtering;
Bootstrap client warmup gate; projectile bind timing diagnostics;
minion-radius update and boundary coverage; client-only PlayMode test assembly
constraint; CDN deployment guidance
Affected assemblies: PlayerInput, FrameSync, Bootstrap, Bootstrap PlayMode tests
Design sources: Player Input v1.1 §§9.1-10.4 and 15.1-17.4;
Presentation v13.2 §§10.4-10.6; Combat v13.2 reward settlement; D-029,
D-040, D-041, D-048 and D-051
Decision dependencies: D-029, D-040, D-041, D-048 and D-051
Validation basis: 2026-08-31 two-match UOS client logs; Unity
compilation/Console; focused EditMode/PlayMode tests; initial independent
review followed by a targeted P1 fix

## 1. Purpose

Prevent an accepted-after-local-execution relay from permanently latching
Aatrox W's local Commit/indicator state. Remove Varus E's first-use VFX cold
load by warming the configured match VFX before gameplay presentation consumes
events, while preserving the authoritative E impact/ground-field Tick and
recording enough timing evidence for a later live build. Increase the minion
death experience-sharing radius from 800 to 1200 authored distance (1.5x), and
document how to distribute the complete Windows client through UOS CDN versus
hosting remote Addressables content.

## 2. Progress

- [x] Correlate the two live Aatrox W runs and identify the callback-order race.
- [x] Trace Varus E VFX and ground-field Addressables lifecycles.
- [x] Locate the authoritative minion experience-sharing radius and current
  local-only Addressables profile.
- [x] Implement the W receipt-order fix and exact regression test.
- [x] Add generic VFX preload/pool warmup plus timing diagnostics and focused
  lifecycle tests.
- [x] Increase the reward radius to 1200 and update distance-boundary tests.
- [x] Restore a clean Unity compilation baseline, run focused verification,
  review all changes, and update current documentation.

## 3. Repository facts and discoveries

- In the first live match, Aatrox W sequence 17 executed at Tick 3156 before
  its accepted relay was observed. `ObserveCompletedGameplayTick` cleared
  `AwaitingAcceptedExecution`, then `ObserveAcceptedGameplayCommands` set it
  back to true even though Tick 3156 was already complete. No later execution
  of that sequence could clear the local-only latch. The second match received
  the callbacks in the opposite order and did not reproduce.
- Varus E's `AreaDamageStageDef` submits VFX 4001 and ground projectile 108 in
  the same authoritative Tick. Projectile views are preloaded, but
  `VfxManager` currently acquires and instantiates VFX 4001 only on its first
  event; its lease/pool then makes later casts fast. The VFX prefab has a
  250 ms presentation duration. Warmup can create an immediate overlapping
  transition without delaying the deterministic polluted-field spawn.
- `MatchStatisticsRuntime.MinionRewardShareRadius` is the sole current radius
  owner. It is 800 authored distance, converted once by
  `UnitWorld.StatDistanceToLogicDistanceScale`; 1.5x is 1200.
- Every current Addressables group uses Local Build/Load paths. Uploading the
  complete client ZIP is ordinary file distribution and needs no game code.
  Remote Addressables is a separate deployment mode requiring group/profile
  and build-pipeline work.
- The initial Unity Console contains two pre-existing compile errors because
  Bootstrap PlayMode tests reference the client-only ClientContent assembly
  while the Editor is on a UNITY_SERVER target. Verification must restore the
  test assembly's existing client-only boundary before running tests.

## 4. Design sources and traceability

- Player Input v1.1 local state remains presentation-only and observes, but
  never drives, authoritative Gameplay. An accepted-after-completed regression
  proves the latch reconciles without changing Commands, Tick or rollback.
- Presentation v13.2 keeps VFX/object pools reconstructible and outside
  Snapshot/checksum. VFX preload tests prove address leases remain resident
  and first playback consumes a warmed instance.
- D-040/D-041 own the converted minion reward radius and experience sharing.
  `MatchRewardDistanceTests` protects the new 12-unit boundary.
- D-048/D-051 require explicit Addressables lease ownership and client/server
  separation. VFX warmup runs only on clients and releases its leases through
  the existing `VfxManager` lifetime.

## 5. Scope

### In scope

- Local Aatrox W accepted/observed callback-order reconciliation.
- Generic configured-library VFX asset and one-instance pool warmup for shared
  and selected-hero entries, plus elapsed-time logs for preload and playback.
- Varus E's existing 250 ms visual overlap with the immediately authoritative
  polluted field; no Gameplay delay.
- Minion experience-share radius 800 -> 1200 and its boundary test.
- UOS CDN operational guidance for complete client ZIP distribution and a
  clearly separate future remote-Addressables path.
- The minimum asmdef correction required to compile client-only PlayMode tests
  while the Editor target defines UNITY_SERVER.

### Out of scope

- Ability Session, Command wire bytes, TargetTick, Snapshot/checksum or schema.
- Delaying Varus E damage or ground projectile Gameplay lifetime.
- Creating a UOS Bucket/Release/Badge, uploading a build, embedding credentials
  or switching the project to remote Addressables without a selected Bucket
  and rollout policy.
- Building the Player or server; the user owns packaging.

## 6. Implementation plan

1. In `ObserveAcceptedGameplayCommands`, compare the accepted TargetTick with
   the injected completed-Gameplay Tick. Keep `AwaitingAcceptedExecution` only
   when authority execution is still in the future, and log the decision.
2. Add the exact live callback-order regression: Commit is observed/executed,
   accepted relay arrives for that already-completed Tick, then local state
   reconciles to Idle rather than staying latched.
3. Expose stable VFX-library enumeration and add `VfxManager.PreloadAsync`.
   Acquire shared and selected-hero addresses, instantiate one inactive pooled
   object per definition, retain existing manager-owned leases, and log
   per-entry and aggregate elapsed time. Log load/pool timing on playback for
   live evidence.
4. Await client VFX warmup in `GameBootstrap.InitializeAsync` before external
   flow registration/readiness; skip it on Dedicated Server. Cover the manager
   preload and first-play reuse path in EditMode; rebuilt client timing remains
   a user-owned acceptance step.
5. Change `MinionRewardShareRadius` to 1200 and update the near/far test to
   11.99/12.01 logic units.
6. Add UOS CDN release guidance to `BUILD_GUIDE.md`, explicitly separating a
   downloadable complete client ZIP (no code change) from remote Addressables
   (future configuration/build changes).

## 7. Public contracts and ownership

`VfxLibrary` gains read-only indexed enumeration and `VfxManager` gains an
async presentation warmup method. Both remain FrameSync presentation-owned;
no Gameplay protocol changes. `LocalAbilityInputState` remains PlayerInput-only
and absent from Snapshot/checksum/wire bytes. The reward radius remains a
FrameSync-owned deterministic constant.

## 8. Validation

- Unity refresh/compilation and isolated Console Error query.
- Focused PlayerInput EditMode accepted-after-completed callback-order test,
  then full PlayerInput suite.
- Focused FrameSync reward-boundary EditMode test and VFX manager EditMode
  preload/reuse/filter tests.
- Relevant Bootstrap initialization/client-presentation tests where the
  current Editor target permits them.
- Read-only diff/design review after all fixes; a second review only if the
  first finds P0 or multiple P1 issues, per user policy.
- Rebuilt UOS live visual/input acceptance remains user-owned.

## 9. Independent review

Completed after implementation and focused verification. The initial
independent read-only review identified one P1: the first warmup implementation
loaded the full VFX library instead of the selected-hero closure. VFX entries
now carry explicit owner hero IDs, Bootstrap passes the frozen selection, and
preload skips unselected entries. The remaining observations were P2/P3 test
depth/documentation items; the user's second-review threshold was therefore
not met.

## 10. Failure and recovery

The W change is local-only and can be reverted independently. If VFX warmup
fails, initialization fails visibly rather than silently entering a match with
missing presentation; timing logs identify the address and elapsed stage. CDN
credentials and remote URLs remain outside source control. Existing local
Addressables packaging remains the default.

## 11. Results

The W callback-order race is fixed locally: an accepted relay whose TargetTick
is already at or before the Runtime-owned completed Gameplay Tick no longer
re-latches `AwaitingAcceptedExecution`. A future accepted TargetTick still
waits for authority execution, and no Gameplay, Snapshot, checksum or wire
state changed.

`VfxManager.PreloadAsync` now acquires shared and selected-hero VFX addresses
and creates one inactive pool instance before client presentation is
registered. GameBootstrap passes the frozen match hero IDs and awaits this
warmup on clients only. VFX 4001 (Varus E) therefore starts from a resident
prefab and pool instance at the same authoritative Tick as its existing
polluted-ground event; `[VfxPreload]`, `[VfxPlayback]` and
`[ClientProjectileView]` logs expose address, owner/selection, source/spawn
Tick, cache/pool hit and elapsed milliseconds for the next rebuilt live run.
The focused filter log explicitly loads `4001` for hero `1001`, skips `3101`
owned by `1002`, and reports `entries=1/2`.

`MinionRewardShareRadius` is now 1200 authored/stat distance (1.5x), with
11.99/12.01 boundary coverage at the current 0.01 conversion scale. The
complete-client ZIP and future remote-Addressables UOS CDN procedures are in
`Docs/Implementation/BUILD_GUIDE.md`; no build, upload or remote-profile
switch was performed.

Unity forced refresh completed with an empty isolated Console Error query.
PlayerInput EditMode passed 42/42, FrameSync EditMode passed 123/123,
Bootstrap EditMode passed 123/123, and the focused W, VFX preload/filter and
reward-boundary tests passed. Client presentation PlayMode and rebuilt UOS
acceptance remain user-owned because the connected Editor is currently on the
`UNITY_SERVER` target.

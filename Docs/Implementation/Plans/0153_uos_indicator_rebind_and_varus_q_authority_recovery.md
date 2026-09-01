# ExecPlan 0153 — UOS Indicator Rebind and Varus Q Authority Recovery

Plan ID: 0153
Status: Completed
Created: 2026-08-31
Completed: 2026-08-31
Risk: High
Design conformance: Strict
Estimated code delta: 180-320 lines across PlayerInput, ClientContent-facing
presentation tests and current implementation documentation
Actual code delta: focused changes across five runtime/test files plus current
implementation documentation
Affected assemblies: PlayerInput, ClientContent, Bootstrap PlayMode tests
Design sources: Player Input v1.1 §§9.1-10.4, 15.1-15.3 and 17.1-17.4;
FrameSync v10.2 §§9.4 and 10.5-10.6; D-048/D-051
Decision dependencies: D-015, D-017, D-029, D-048 and D-051
Validation basis: 2026-08-31 12:22 UOS ClientA/ClientB logs; Unity
compilation/Console; focused EditMode and PlayMode tests; final independent
read-only review

## 1. Purpose

Restore every generic skill indicator after the GameScene client-content host
rebinds its Addressables generation, while preserving Aatrox Q's independent
directional-zone path. When a predicted Varus Q Focus is removed by rollback
and later accepted on a retargeted authority Tick, reconstruct the local input
latch from that exact accepted command so the indicator and primary-click
Commit eligibility return without preserving local state through rollback.

## 2. Progress

- [x] Correlate the latest UOS indicator and Varus Q failure timelines.
- [x] Resolve current Player Input, FrameSync and Addressables ownership rules.
- [x] Rebuild generic indicator instances and runtime materials on every
  successful Addressables reconfiguration.
- [x] Rehydrate a retired local Focus/Commit latch only from its exact tracked
  accepted authority command.
- [x] Add focused resource-generation and accepted-after-retirement tests.
- [x] Compile through Unity, run focused and broad affected suites, perform the
  first independent review and update current implementation state.

## 3. Repository facts and discoveries

- Both 12:22 clients acquired and configured indicator Addressables generation
  1. When `GameBootstrap.UnitWorld` became ready, `BindCurrentScene` advanced to
  generation 2, disposed every generation-1 indicator lease and reacquired the
  same three addresses.
- Generation 2 logs contain `Configure` but no new instance/material-bind log.
  `SkillIndicatorDriver.EnsureInstances` creates only when its instance fields
  are null, so the surviving Quad instances retain runtime materials cloned
  from the released generation. Aatrox Q remains visible because it creates an
  independent runtime LineRenderer material without indicator textures.
- ClientA sequence 50 predicted Focus at Tick 4563. Authority replay for Tick
  4563 contained no command, so the local latch correctly returned to Idle.
  Relay/Authority later accepted and executed the exact sequence at Tick 4571,
  creating the Gameplay Session, but both accepted-command and completed-Tick
  reconciliation skip an Idle local state. Primary clicks 54 and 55 therefore
  reported no Focus context and emitted no Commit.
- Local ability input state, receipts and indicators are explicitly excluded
  from Gameplay Snapshot/checksum/network state. Reconstructing them from an
  already accepted command does not mutate AbilityRuntime or deterministic
  Gameplay.

## 4. Design sources and traceability

- Player Input v1.1 §§9.1-10.4: `FocusRequested` and `GameplayFocusing` are the
  only HoldRelease contexts eligible for primary-click Commit. Protected by an
  accepted-after-retirement Commit regression.
- Player Input v1.1 §§15.2-15.3: Focus/Commit pending state keeps the indicator
  until Gameplay advances or ends the Session. Protected by the same local
  state regression plus the existing input-indicator PlayMode suite.
- Player Input v1.1 §§17.1-17.4: local state is not rollback state and may only
  observe Gameplay/accepted input facts. The test explicitly proves Idle after
  rollback absence, then recovery only after the exact accepted CommandSeq.
- FrameSync v10.2 §§9.4 and 10.5-10.6: the server may retarget a genuinely late
  command and relay its actual TargetTick. No TargetTick or rollback policy is
  changed.
- D-048/D-051: every Addressables handle has one owner/release and client
  presentation is reconstructible. A PlayMode reconfiguration regression
  proves old instances/materials are replaced while the new assets are live.

## 5. Scope

### In scope

- `SkillIndicatorDriver` generic instance/material replacement on Configure.
- Exact tracked accepted-command recovery for locally retired Focus/Commit.
- Focused PlayerInput EditMode and indicator lifecycle/render PlayMode tests.
- Current plan, module-status and handoff evidence.

### Out of scope

- Indicator artwork, Addressables group layout, shader or material asset edits.
- Ability Session, TargetTick, server retargeting or rollback-policy changes.
- W Toggle semantics, Command wire bytes, Snapshot/checksum or schema changes.
- Packaging and rebuilt UOS live acceptance.

Unity lifecycle implications: old runtime indicator GameObjects/material clones
are destroyed before instances are recreated from the newly leased Prefabs.
Snapshot/serialization/checksum implications: none.

## 6. Implementation plan

1. Give `SkillIndicatorDriver.Configure` an explicit replace-owned-instances
   path that destroys all generic instances and their runtime material clones,
   resets cached child references/visibility, then instantiates from the new
   Prefabs. Preserve the world-space root and independent directional-zone
   lines unless ordinary cleanup requires them.
2. In `ObserveAcceptedGameplayCommands`, resolve the exact local request
   diagnostic before inspecting local state. If that request was observed
   missing at its original Tick and the slot is Idle, reconstruct only Focus as
   `FocusRequested` or Commit as `CommitRequested`, using the accepted Tick and
   sequence. Cancel and unrelated/stale sequence identities do not rehydrate.
3. Retain existing non-Idle receipt retargeting and completed-Tick transition
   to `GameplayFocusing`/Idle.
4. Add the exact live ordering test: prediction observed, rollback replay lacks
   the command, local state retires, later Relay acceptance restores the latch,
   authority execution observes the Session and primary click emits Commit.
5. Make the Host swap atomic: retain the old leases while acquiring and
   configuring the new generation, adopt the new leases, then release the old
   generation. Add a PlayMode regression proving all three generic instances
   and materials are replaced and the independent directional-zone line still
   renders after rebind.

## 7. Public contracts and ownership

No new public protocol or serialized type is introduced.
`LocalAbilityInputState` remains PlayerInput-owned, local-only and absent from
Snapshot/checksum/network bytes. `SkillIndicatorDriver` continues to own its
instantiated presentation objects and runtime materials. Addressables leases
remain solely owned and released by `ClientContentRuntimeHost`.

## 8. Validation

- Clean Unity synchronous refresh/compilation and isolated Console Error query.
- PlayerInput EditMode exact late-acceptance/retirement/recovery/Commit test.
- Existing PlayerInput assembly full EditMode suite.
- Bootstrap PlayMode indicator reconfiguration test, existing generic
  framebuffer/world-space test and input-indicator simulation test.
- Bootstrap EditMode full suite for composition regression.
- Independent read-only design/diff/test review after all fixes.

## 9. Independent review

Run one independent review after implementation and verification. Under the
user's policy, run a second review only if the first reports a P0 or multiple
P1 findings.

## 10. Failure and recovery

All changes are source/test-only and preserve existing assets and wire state.
If reconfiguration validation fails, retain the new lease until the new
instances are built and restore only the focused instance-replacement path. If
accepted-command recovery fails, retain the diagnostic evidence and do not
work around it by preserving local state through rollback. Rebuilt UOS visual
and input acceptance remains user-owned.

## 11. Results

- `ClientContentRuntimeHost` no longer releases indicator leases at generation
  start. It acquires all three new Prefabs, synchronously configures every live
  driver while both generations are resident, adopts the new leases and only
  then disposes the old generation. A stale async generation disposes only its
  own temporary leases.
- `SkillIndicatorDriver.Configure` now destroys and recreates Direction,
  RangeCircle and GroundTarget instances plus their runtime material clones on
  every successful generation bind. The unparented world root and Aatrox Q
  directional LineRenderers remain independently owned; `OnDestroy` performs
  idempotent cleanup.
- `PlayerCommandRequester` still permits rollback replay to retire Focus to
  Idle. It reconstructs local-only `FocusRequested`/`CommitRequested` only when
  the same controlled Unit, slot, verb and CommandSeq was observed missing at
  its requested Tick and the accepted Tick is strictly later. Gameplay,
  Snapshot, checksum and wire state are unchanged.
- Unity MCP forced refresh/compilation completed with the Editor idle and an
  empty final Console Error query. PlayerInput EditMode is **41/41**. Focused
  PlayMode passes are atomic indicator generation replacement **1/1**, existing
  Addressables material/world-space/framebuffer blue-not-magenta **1/1**, W then
  Q pending-indicator/command flow **1/1**, and representative Addressables
  acquire/release **1/1**.
- The broad `GameBootstrapPlayModeTests` probe retained two unrelated existing
  lifecycle failures: asynchronous ClientComposition did not initialize within
  its 600-frame fixture limit, and destroy-during-load emitted an undeclared
  `OperationCanceledException`. The broad ClientContent namespace retained the
  unrelated missing Projectile prefab 2101 fixture. None intersects the new
  focused paths; they remain recorded rather than masked.
- The first independent read-only review reported one P1 (the old-lease gap),
  one P2 (test/world-root cleanup) and one P3 (negative identity and Aatrox-line
  coverage). All were fixed and the focused suites rerun. With no P0 and only
  one P1, the user's threshold did not authorize a second review.

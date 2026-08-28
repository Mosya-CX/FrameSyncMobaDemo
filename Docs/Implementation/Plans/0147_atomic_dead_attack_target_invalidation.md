# ExecPlan 0147 — Atomic Dead Attack Target Invalidation

Plan ID: 0147
Status: Completed
Created: 2026-08-28
Completed: 2026-08-28
Risk: High
Design conformance: Strict
Estimated code delta: 80-160 lines across Unit/FrameSync production and focused tests
Actual code delta: production +87/-49 lines; tests +413/-25 lines
Affected assemblies: FrameSyncMoba.Unit; FrameSyncMoba.FrameSync; their EditMode tests; FrameSyncMoba.Unit.PlayModeTests
Design sources: Unit Framework v27.3; Unit Framework v27.4 Action Arbitration Amendment §§1, 3, 5-7; Attack v6.2 §§1.3, 4.5, 6.2; Combat v13.2 §10; Snapshot Appendix v7.2 §5.2
Decision dependencies: D-009; D-047; D-049
Validation basis: Unity MCP compile/Console, focused Unit and FrameSync EditMode tests, focused existing lifecycle PlayMode test, independent read-only review

## 1. Purpose

Prevent rollback from restoring a Tick-end snapshot where an uncommitted
`AttackHandler` windup was canceled because its target formally died while the
fixed Main `ActionRuntime` still retained the Attack reservation. Formal target
invalidation must atomically cancel the Handler mechanism and release Main in
the same deterministic call.

## 2. Progress

- [x] Confirm the 2026-08-28 ClientA failure and isolate the Tick 5331 snapshot invariant violation.
- [x] Resolve current ownership, death, arbitration and Snapshot authorities.
- [x] Inspect affected source, tests, assemblies, dirty worktree and Unity Console baseline.
- [x] Implement ActionArbiter-owned atomic invalid-target cancellation.
- [x] Drive formal-death invalidation from frozen `DeathResults` after Combat settlement.
- [x] Preserve non-death Despawn invalidation without restoring the old Handler-owned scan.
- [x] Add atomicity and Snapshot/Restore regression tests.
- [x] Compile through Unity MCP and inspect Console.
- [x] Run focused EditMode and lifecycle PlayMode verification.
- [x] Complete independent read-only design/diff/test review.
- [x] Update current module/handoff evidence and close the plan.

## 3. Repository facts and discoveries

- `AttackHandler.ClearTargetIfMissing()` currently calls
  `CancelBeforeCommit()` after Combat death handling but cannot release the
  independently stored Main Runtime.
- The pipeline reconciles Runtime slots immediately after Handler advance,
  before Combat settlement. A target can formally die later in the same Tick.
- The Tick 5331 ClientA anchor therefore stored an empty/canceled attack
  mechanism together with an occupied Main Attack Runtime; Tick 5332 rollback
  failed in `ActionRuntimeSet.ResolveSlot` before replay.
- Unit and FrameSync remain one-way (`FrameSyncMoba.FrameSync` references
  `FrameSyncMoba.Unit`). No asmdef change is required.
- The worktree contains extensive unrelated Addressables, presentation,
  documentation and resume changes. The target C# files were clean at intake
  and all unrelated changes must be preserved.
- Unity Console intake contained only pre-existing MCP Hub negotiation errors,
  not a Gameplay/compiler diagnostic.

## 4. Design sources and traceability

- Unit v27.4 §1/§3: ActionArbiter owns ordinary cancellation; Main/Base own
  reservation identity; Handler owns attack timing/mechanism.
  -> `AttackHandlerTests` atomic invalid-target cases.
- Unit v27.4 §5/§6: capture observes completed/released Runtime slots and
  Restore rejects Attack without a matching uncommitted windup.
  -> FrameSync snapshot capture/restore regression.
- Attack v6.2 §4.5/§6.2: pre-Commit cancellation invokes
  `CancelBeforeCommit`; behavior cancellation belongs to the Unit framework.
  -> Handler target/timing and Main-slot assertions.
- D-009 / Combat v13.2 §10: formal death is synchronous through UnitWorld.
  -> production driver consumes finalized `DeathResults` only after Combat
  settlement, without changing settlement waves or death ownership.
- D-049: Combat traversal/settlement order is not action cancellation order.
  -> no cancellation runs inside active settlement waves; iteration uses the
  existing stable DeathResult and UnitWorld orders.

## 5. Scope

### In scope

- Replace Handler-owned missing/dead-target cleanup with an internal
  ActionArbiter/ActionRuntime atomic cancellation boundary.
- Invoke that boundary for finalized formal deaths after Combat settlement.
- Preserve equivalent target invalidation for formal non-death Despawn.
- Add focused deterministic and rollback Snapshot tests.

### Out of scope

- Snapshot fields, schema, wire versions or checksum serialization changes.
- Combat settlement/death attribution changes.
- Reverse target indexes, new Unit events, packages, scenes, prefabs or assets.
- Range loss, hostility changes or general target-selection redesign.
- Player packaging or UOS rebuild; the user owns builds.

Snapshot/serialization/checksum implication: no membership or version change;
the existing snapshot must now always contain a semantically valid pairing.
Lifecycle implication: formal death remains Combat/UnitWorld-owned; only
dependent uncommitted Attack actions are ended after settlement.

## 6. Implementation plan

1. Add an internal target-specific cancellation method to `ActionRuntimeSet`
   that validates a matching active uncommitted AttackHandler, calls
   `CancelBeforeCommit`, and clears Main before returning.
2. Expose the orchestration boundary through `ActionArbiter` without exposing
   Combat DTOs to the Unit assembly.
3. Add a UnitWorld helper that applies one invalid target UID to units in the
   existing stable order; use it from non-death Despawn.
4. In `SimulationTickPipeline`, consume each frozen DeathResult after
   `CombatSystem.EndTick()` and before Snapshot capture; remove the old
   `AttackHandler.ClearTargetIfMissing` scan and method.
5. Replace the Handler-only test with atomic Handler/Main assertions and add a
   FrameSync capture/restore regression that reproduces the poisoned-anchor
   shape through the production invalidation boundary.

## 7. Public contracts and ownership

- No public protocol, DTO, enum, Snapshot or serialized contract changes.
- New helpers are internal/private implementation contracts.
- `ActionRuntimeSet` remains the Main/Base reservation owner.
- `AttackHandler` remains timing/mechanism authority.
- `ActionArbiter` remains the cross-owner cancellation boundary.
- `SimulationTickPipeline` owns the post-Combat scheduling point; Unit does not
  depend on FrameSync or Combat result transport types.

## 8. Validation

- Unity MCP AssetDatabase refresh/script compilation.
- Unity Console Error and Exception inspection, separating the pre-existing
  MCP Hub negotiation noise.
- EditMode: `FrameSyncMoba.Unit.Tests.AttackHandlerTests`.
- EditMode: `SnapshotChecksumCompletenessTests` production `ExecuteTick`
  death/capture/restore regression.
- EditMode: broader FrameSync assembly if focused tests pass.
- PlayMode: `UnitPrefabCompositionPlayModeTests` formal-death atomicity case.
- Determinism proofs: repeated/capture-restore behavior and stable explicit
  iteration; no unordered enumeration affects output.
- Diff review against the exact ownership/death/snapshot sections above.

## 9. Independent review

The independent read-only review found no P0/P1 issue. Its three P2 requests
were addressed before completion: the FrameSync regression now traverses the
real `ExecuteTick` scheduling seam; formal death and Despawn both lock the
committed-attack non-revocation rule; and non-increasing death sequences fail
deterministically. Per the user's review policy, no second sub-agent review was
required because the first review produced neither P0 nor P1 findings.

## 10. Failure and recovery

All changes are ordinary source edits and can be resumed from this plan. Do not
reset or overwrite unrelated dirty-worktree changes. If Unity MCP compilation
or tests are unavailable, record the exact MCP failure and leave the plan at
Verification Pending rather than claiming completion. No build command will be
issued.

## 11. Results

- `AttackHandler.ClearTargetIfMissing` and the Tick-end Handler-only scan were
  removed. Frozen formal deaths now enter `UnitWorld`, which traverses its
  canonical Unit order and asks each `ActionArbiter` to cancel a matching
  uncommitted windup and release Main in the same call.
- Non-death `DespawnUnit` reuses the same target-specific path while rollback
  topology removal remains untouched. Committed attacks keep their target,
  Ready Tick and emitted damage/projectile ownership.
- No public DTO, Snapshot field, checksum member, schema, wire version, scene,
  asset or package changed.
- Unity MCP synchronous refresh compiled with an empty Console Error query.
- Focused verification: six new/replaced EditMode tests and one PlayMode test
  pass, including a real two-Tick Combat death followed by aggregate
  capture/restore.
- Broad baselines: Unit EditMode `549 passed / 10 retained failures`;
  FrameSync EditMode `98 passed / 1 retained GlobalPrefabTable fixture
  failure`; Unit PlayMode `2 passed / 1 retained PrefabId 9 range fixture
  failure`. The retained failures match categories already present before this
  fix and do not traverse the changed cancellation path.
- Independent review: no P0/P1; all three P2 test-strengthening findings were
  implemented and verified.

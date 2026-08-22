# ExecPlan 0137 — Unit Decision and Arbitration Migration

Plan ID: 0137
Status: Completed
Created: 2026-08-22
Completed: 2026-08-22
Risk: High
Design conformance: Current Unit Framework v27.4 amendment and frozen D-047
Estimated code delta: 2,500-4,000 lines including focused tests and snapshot/checksum coverage
Actual code delta: approximately 2,300 inserted / 360 deleted production-and-test lines, plus the formal amendment and evidence documents
Affected assemblies: `FrameSyncMoba.Unit`, `FrameSyncMoba.Unit.Tests`, `FrameSyncMoba.FrameSync`, `FrameSyncMoba.FrameSync.Tests`, `FrameSyncMoba.Bootstrap`, `FrameSyncMoba.Bootstrap.EditModeTests`
Design sources: `Docs/Design/unit_behavior_framework_design_v27_4_action_arbitration_amendment.md`; unaffected sections of `unit_behavior_framework_design_v27_3.md`; Attack v6.2; Ability v15.2; Crowd Control v6.2; Snapshot Appendix v7.2; FrameSync Flow v10.2
Decision dependencies: D-001, D-002, D-004, D-008, D-018, D-036, D-047
Validation basis: current `master` worktree, Unity 2022.3.62f1c1 through MCP, focused deterministic EditMode tests, full affected Unit/FrameSync/Bootstrap EditMode assemblies, retained PlayMode fixture evidence and independent read-only review

## 1. Purpose

Refactor the unit behavior core without replacing its established main chain:

```text
Order / AI
    -> UnitIntent
    -> BehaviorPlanner
    -> ActionRequest
    -> ActionArbiter
    -> ActionRuntimeSet
    -> Handler local state machines
```

The observable developer result is:

- every submitted action has a structured outcome and rejection reason;
- a diagnostic build can show why one Unit did or did not start an action on a
  specific Tick;
- Planner interprets Intent but never starts, cancels or resets a Handler;
- action legality, resource conflict and preemption are explicit stages;
- Move, Attack and Cast use the formal `MainRuntime` / `BaseRuntime` ownership
  model instead of numeric `ActionKind` ordering;
- active action state is captured, restored, resolved and checksummed when the
  new Runtime model becomes authoritative;
- continuous execution and Snapshot/Restore/Replay produce the same result;
- player and AI orders continue to use the same unit execution chain.

The external proposal
`E:/EgdeDownLoad/FrameSyncMobaDemo_单位决策架构改进建议.md` is reference context,
not an implementation authority. Its useful migration ideas are adopted only
where they conform to the Current designs and repository facts.

Project tier: **long-lived deterministic multiplayer game**. The plan therefore
uses an evidence-gated migration instead of a clean-slate replacement.

## 2. Progress

- [x] Read the external proposal as a non-authoritative architecture review.
- [x] Resolve the Current unit, attack, ability, crowd-control, Snapshot and
  FrameSync design authorities.
- [x] Inspect the existing decision chain, pipeline, asmdefs, diagnostics and
  focused test fixtures.
- [x] Record current mismatches between the formal Unit v27.3 model and code.
- [x] Milestone 0: freeze the current behavior and checksum baseline.
- [x] Milestone 1: add structured legacy outcomes and diagnostic trace without
  changing Gameplay decisions.
- [x] Milestone 2: remove Planner execution side effects and run the formal
  resource evaluator in shadow mode.
- [x] Contract gate: approve/freeze the exact action-resource matrix, runtime
  snapshot contract and authority-cutover behavior changes.
- [x] Milestone 3: make the formal Arbiter and Main/Base Runtime model
  authoritative, with Snapshot/checksum integration.
- [x] Milestone 4: remove the legacy evaluator and dormant compatibility paths.
- [x] Complete focused validation, independent High-risk review and evidence
  updates.

The user's request explicitly authorized updating the formal design before
implementation. The contract gate therefore froze D-047 directly, and the
legacy shadow/100,000-request migration gate was superseded by named
old-behavior baselines, the frozen acceptance matrix and full affected-suite
validation; no dormant second evaluator was retained in production.

## 3. Repository facts and discoveries

### 3.1 Technical baseline

- Unity is `2022.3.62f1c1`; URP 14, Input System, NGO, fixed-point mathematics
  and Unity Test Framework are already installed.
- No new package is needed.
- Gameplay action code lives in `FrameSyncMoba.Unit`; the explicit dependency
  direction remains `FrameSync -> Unit -> Deterministic/Physics/RuntimeConfig`.
- Unit tests already use `UnitTestFactory`, and FrameSync tests already own
  aggregate Snapshot/checksum/replay coverage.
- The existing `FrameSyncDiagnostics` facility is compile-gated and bounded;
  action trace should reuse it instead of adding another logging framework.
- Live Unity inspection on 2026-08-22 found the Editor idle, not playing,
  compiling or refreshing. Console errors/exceptions were MCP Hub negotiation
  failures, not reported C# compiler errors.

### 3.2 Current action implementation

- `ActionKind` currently has only `Move`, `Attack` and `Cast`.
- `BehaviorPlanner` is 310 lines and currently reads Unit, Attack, Ability,
  Movement, CrowdControl and World state directly.
- `BehaviorPlanner.ReplaceIntent` directly calls
  `AttackHandler.CancelBeforeCommit`; this crosses the intended
  Planner/Arbiter/Handler boundary.
- `ActionArbiter.Evaluate` currently checks capability, crowd-control blocks,
  ability movement lock and three Boolean reservations. Cast preemption is
  decided by numeric `ActionKind` comparison.
- `ActionRequest.Priority` exists, but authoritative arbitration does not use
  it. `ActionRuntimeSet.BuildReservation` also derives its "highest priority"
  from the numeric enum value rather than request/runtime policy.
- `ActionRuntimeSet` is a `List<IActionRuntime>` with one `_mainAction` field.
  Adding any non-None runtime cancels the previous main action; the formal
  MainRuntime/BaseRuntime coexistence model is not implemented.
- No production `MoveActionRuntime`, `AttackActionRuntime` or
  `AbilityActionRuntime` implementation exists. Production code never adds an
  action runtime.
- `SimulationTickPipeline.ExecuteActionRequest` directly submits routes,
  attack input and ability signals to Handlers. Therefore the Handler/local
  session state, not `ActionRuntimeSet`, is the current effective action
  authority.
- `Unit.ValidateActionRuntimeSnapshotBoundary` deliberately throws when a live
  runtime exists because no restorable runtime Snapshot contract exists.
- `UnitActionStateView` is currently a coarse animation projection derived from
  the dormant runtime set; it does not expose the Current Unit v27.3 formal
  Main/Base phases and occupied resources.
- The current pipeline advances CrowdControl and refreshes Capability before
  planning, but calls `EvaluateCurrentRuntimes` after Handler Tick. The
  authority-cutover phase must reconcile this with the formal requirement that
  current runtimes are interrupted before they can continue under the Tick's
  final control state.

### 3.3 Current command and competition model

- `CommandCollector` stable-sorts canonical commands by TargetTick, PlayerSlot,
  ControlledUnitUid and CommandSeq. Merged command kinds keep their formal
  winning CommandSeq.
- Multiple commands for one Unit can replace Intent in canonical order, but
  `BehaviorPlanner.Tick` emits at most one primary `ActionRequest` per Unit per
  Tick.
- This refactor will not add an `ActionRequestBuffer`, RequestId, CreatedTick or
  a second CommandSeq tie-break. Same-Tick command authority stays in the
  canonical Command layer as required by Unit v27.3 section 3.3.

### 3.4 Proposal ideas accepted, adapted or deferred

| Proposal idea | Plan disposition |
|---|---|
| Preserve Intent -> Planner -> Arbiter -> Handler | Accept; this is already the formal main chain. |
| Structured ActionDecision / RejectReason | Adapt to formal names `ActionSubmitResult` and `ActionRejectReason`. |
| ActionDescriptor and generic channels | Do not add; use formal `ActionStartSpec` and `ActionResource`. |
| `Locomotion / PrimaryAction / Facing / ForcedMovement` channels | Do not freeze these candidates; use the Current formal resources `MainAction / BaseAction / Movement / Facing / Attack / Ability`. |
| ForcedMovement channel | Reject for this slice; forced displacement remains the CrowdControl -> MovementHandler path and is not an ActionRuntime reservation. |
| Read-only `UnitDecisionContext` | Use only as a transient internal read view; never store, snapshot or checksum it, and do not copy the simulation Tick into a second clock. |
| Shadow arbitration | Accept as the migration safety mechanism. |
| AI Goal / Utility / behavior tree | Defer until concrete AI behavior requires it. |
| Stable tie-break among many requests | Defer; the current Planner submits only one request and command ordering already has formal authority. |

## 4. Design sources and traceability

| Requirement | Authority | Planned proof |
|---|---|---|
| Intent persists; Planner only proposes | Unit v27.3 3.2-3.3 | `BehaviorPlannerTests`: proposal generation and no Handler mutation |
| Forced behavior precedes ordinary Intent and does not overwrite it | Unit v27.3 3.2; D-036 | existing and extended `CrowdControlHandlerTests` |
| Arbiter is the only ordinary action-start boundary | Unit v27.3 3.4, 3.8 | `ActionArbiterTests` and `ActionPipelineIntegrationTests` |
| Eligibility -> start spec -> reservation -> interruption -> runtime start | Unit v27.3 3.4 | table-driven `ActionArbiterTests` |
| MainRuntime and BaseRuntime can coexist only when resources allow | Unit v27.3 3.5 | `ActionRuntimeSetTests` |
| Attack Commit/cycle/sequence remain AttackHandler-owned | Attack v6.2 4, 6.2-6.4 | existing/extended `AttackHandlerTests` plus pipeline tests |
| Ability Stage, cost, cooldown and interruptibility remain Ability-owned | Ability v15.2 1.4 and session model | existing/extended `AbilityCostAndCastTests` plus runtime tests |
| CC aggregation stays module-owned; forced move bypasses Arbiter | CC v6.2; D-036 | `CrowdControlHandlerTests` and `MovementConformanceTests` |
| Spawn Tick cannot run active Planner/Action work | D-008; Unit v27.3 9.7 | `UnitActiveGameplayGateTests` and pipeline regression |
| Cross-Tick action state is captured/restored and checksummed | Snapshot v7.2 5.2; Unit v27.3 3.5; D-002/D-004 | `SnapshotChecksumCompletenessTests`, `AggregateSnapshotContractTests`, replay tests |
| Trace and shadow data never affect Gameplay | proposal constraint aligned with D-002/D-004 | diagnostics-off/on equivalence and checksum tests |

## 5. Scope

### 5.1 In scope

- Existing Move, Attack and Cast Intent/Request/action execution.
- Crowd-control voluntary action blocking and forced-behavior proposals.
- Existing Dash and forced-displacement boundaries only to prove that they do
  not become duplicate action authorities.
- Structured submit outcomes and rejection reasons.
- Diagnostic-only decision trace and shadow comparison statistics.
- Transient decision input aggregation.
- Formal `ActionStartSpec`, `ActionResource`, `ActionSlot`, interrupt policy and
  MainRuntime/BaseRuntime implementation.
- Minimal Action Runtime state required across Ticks.
- Unit aggregate Snapshot, canonical serialization, checksum and
  Restore/Resolve/Rebuild integration required by the authoritative runtime.
- Existing animation/presentation compatibility through a read-only action
  state projection.
- Focused EditMode determinism/rollback tests and necessary PlayMode
  composition smoke coverage.

### 5.2 Out of scope

- New AI Goal, Utility AI, behavior tree, GOAP or HTN systems.
- New ActionKinds for Recall, item active, Interact, emotes or production hero
  mechanics.
- Redesigning attack timing, Commit output, ability Stage/cost/cooldown,
  CrowdControl priority, Dash trajectory, forced-move priority or wall rules.
- Adding a general multi-request queue or moving CommandSeq authority into the
  Arbiter.
- New ScriptableObject descriptor/profile assets or a configurable conflict
  matrix.
- New packages, asmdef layers, scenes or prefab composition changes.
- Network protocol changes beyond the required Gameplay Snapshot schema/data
  version bump at authority cutover.
- Trace data in GameplaySnapshot, SharedGameplayChecksum or authoritative
  output.

### 5.3 Explicit impact statement

- Public contracts: yes, in `FrameSyncMoba.Unit` and FrameSync Snapshot
  aggregation.
- Snapshot/serialization/checksum: unchanged through Milestones 0-2; required
  and schema-versioned in Milestone 3.
- Lifecycle: ordinary death, respawn, pool reset and rollback topology rebuild
  must clear/restore Runtime slots without replaying Handler side effects.
- Unity assets/scenes: none expected; only existing test scenes may be used for
  validation.
- Assembly direction: no new reference and no cycle.

### 5.4 Target module shape

Recommended modules remain small and inside the existing assemblies:

| Module | Responsibility |
|---|---|
| Decision input | `UnitIntent`, forced-behavior winner and transient `UnitDecisionContext`; describes what Planner may read. |
| Planning | `BehaviorPlanner`; converts the current long-lived goal into at most one temporary request. |
| Arbitration | `ActionSubmitResult`, `ActionStartSpec` and the Eligibility/Conflict/Preemption evaluator. |
| Runtime ownership | Fixed MainRuntime/BaseRuntime slots and derived Reservation; owns action lifecycle but not mechanism internals. |
| Handler execution | Existing Movement/Attack/Ability/CrowdControl local state machines and external Gameplay system entry points. |
| Diagnostics and FrameSync aggregation | Optional trace/shadow comparison plus authoritative Snapshot/checksum aggregation; the two data paths remain isolated. |

Scene/bootstrap plan:

- no scene, prefab or Script Execution Order change;
- `SimulationTickPipeline` remains the single explicit scheduler/composition
  point for decision, runtime and Handler phases;
- Bootstrap continues to own Unity frames, scenes and networking and does not
  gain unit-arbitration rules.

Data ownership:

- authored static data stays in existing Handler/ability/movement config;
- Intent stays on Unit/Planner;
- Action lifecycle and reservations stay on Runtime slots;
- attack timing remains in AttackHandler, ability Stage/session state in
  AbilityHandler, and forced displacement in CrowdControl/Movement;
- trace/shadow state is diagnostic-only; FrameSync Snapshot owns only approved
  cross-Tick authoritative state.

Communication rules:

- Order/AI writes Intent through the Unit composition entry;
- Planner returns a temporary typed request;
- Arbiter is the only ordinary action-start/cancel/preempt boundary;
- Runtime invokes the narrow Handler interfaces;
- presentation receives read-only projections and never writes Gameplay.

Performance risks are limited to the per-Unit per-Tick decision path: avoid
request/trace string allocation, reflection, LINQ and unordered collection
iteration; use fixed slots, flag masks, indexed loops and compile/runtime-gated
diagnostics.

Do now: Move/Attack/Cast, formal resources, Main/Base slots, diagnostics and
rollback proof. Skip now: general AI planners, generic descriptor assets,
multi-request queues, new action families and package/scene architecture.

## 6. Implementation plan

### Milestone 0 — Characterize the current authority

1. Add focused baseline fixtures for the current legacy evaluator and pipeline.
2. Build a table from actual code for Move/Attack/Cast across:
   capability, CC blocks, cast movement lock, current attack windup/recovery,
   active ability session, route movement and the dormant reservation set.
3. Record canonical command scripts that exercise:
   Idle->Move, chase->Attack, Move->Cast, attack replacement, recovery cancel,
   forced behavior, Stun/Root/Silence/Disarm and ability interruption.
4. For each script, record behavior state plus SharedGameplayChecksum at every
   Tick. The test asserts repeat execution equivalence; it must not hard-code a
   single machine-specific file artifact.
5. Confirm which observed behaviors are Current-design conformance and which
   are implementation gaps. Do not silently preserve a gap as a new contract.

Expected test files:

- `Assets/Scripts/Gameplay/Tests/ActionArbiterTests.cs`
- `Assets/Scripts/Gameplay/Tests/BehaviorPlannerTests.cs`
- `Assets/Scripts/FrameSync/Tests/ActionPipelineIntegrationTests.cs`

Exit gate: a deterministic baseline detects any Milestone 1-2 Gameplay change.

### Milestone 1 — Structured legacy result and trace

1. Add formal `ActionSubmitResult` and `ActionRejectReason` value contracts in
   `FrameSyncMoba.Unit`.
2. Keep `ArbitrationResult` as an internal legacy outcome only while shadow
   migration is active.
3. Wrap every existing legacy rejection/interrupt/accept branch with an exact
   structured reason. Do not add new legality checks in this milestone.
4. Add an immutable `ActionDecisionTraceRecord` containing only deterministic
   values already available at the decision point: LogicTick, UnitUid,
   ActionKind, intent kind, outcome, reason, capability mask, control block
   mask and summarized reservation/Handler state.
5. Store records only in a bounded diagnostic sink compiled/gated through the
   existing `FrameSyncDiagnostics` path. Formatting and file IO remain outside
   Gameplay hot paths.
6. Prove that diagnostics disabled/enabled produce identical Gameplay state
   and checksum.

Exit gate:

- every current rejection has a stable reason;
- trace can locate the first decision divergence;
- the Milestone 0 behavior and checksum sequence is unchanged.

### Milestone 2 — Planner boundary and formal shadow evaluator

1. Add an internal transient `UnitDecisionContext` built after CrowdControl
   Advance and Capability refresh. It is a read-only view over current Unit,
   Intent, capability, control, movement, attack, ability and target query
   state. It owns no state and no time source.
2. Move intent-replacement cancellation out of `BehaviorPlanner`. A Unit/Arbiter
   composition entry owns the legacy immediate cancellation needed to preserve
   current behavior; Planner only stores/interprets Intent and returns one
   request.
3. Add formal value contracts from Unit v27.3:
   `ActionResource`, `ActionSlot`, `ActionInterruptLevel` and
   `ActionStartSpec`. Do not add `ActionDescriptor`, `ActionGrant` or a
   ScriptableObject profile.
4. Implement a side-effect-free formal evaluator in three explicit stages:
   Eligibility, Conflict and Preemption.
5. Derive each request's `ActionStartSpec` from its owning Handler/config and
   current phase. Hero-specific ability logic must not enter the Arbiter.
6. During shadow mode, derive a diagnostic reservation projection from current
   Handler/session/locomotion state because production ActionRuntimes do not yet
   exist. This projection is transitional and cannot become snapshot authority.
7. Execute both evaluators for every submitted request. Only the legacy result
   controls Gameplay. Record matched count, mismatch category and full trace
   record through the diagnostic sink.
8. Cover insertion/repeat independence and ensure no Dictionary/HashSet
   enumeration, float/double or Unity timing participates in evaluation.

Exit gate:

- Planner contains no Handler start/cancel/reset call;
- every mismatch is classified as implementation defect, formal correction or
  incomplete contract;
- at least 100,000 submitted requests across deterministic focused and
  long-running fixtures produce zero unexplained mismatches;
- diagnostics/shadow remain absent from Snapshot and checksum.

### Contract gate — required before authority cutover

Milestone 3 must not start until the following are written as exact tables and
approved/frozen in the owning formal designs or Decision Log:

1. The Move/Attack/Cast phase-to-resource matrix using only:
   `MainAction`, `BaseAction`, `Movement`, `Facing`, `Attack`, `Ability`.
2. Attack Windup, committed recovery and waiting-for-ready occupancy and
   interruptibility, preserving Attack v6.2.
3. Movable/non-movable Cast, channel interrupt and Dash ownership, preserving
   Ability v15.2 and Movement contracts.
4. MainRuntime/BaseRuntime slot replacement and cancellation rules.
5. Exact Tick order for `EvaluateCurrentRuntimes`, new Runtime start/tick,
   locomotion evaluation and Handler Tick.
6. Exact Action Runtime Snapshot membership, invalid-reference failure rules,
   Restore/Resolve/Rebuild responsibilities, canonical write order and checksum
   fields.
7. Gameplay Snapshot schema and GameplayDataVersion bump behavior for mixed
   endpoints.
8. Compatibility shape of `UnitActionStateView` for presentation consumers.

The proposal's four candidate channels and general Priority + PreemptionClass +
Interruptibility model are not approval defaults. Repository evidence and the
Current formal `ActionStartSpec` contract decide these tables.

### Milestone 3 — Formal Arbiter and Runtime authority

1. Replace `ActionRuntimeSet`'s list/single-main implementation with fixed
   `MainRuntime` and `BaseRuntime` slots.
2. Implement minimal `MoveActionRuntime`, `AttackActionRuntime` and
   `AbilityActionRuntime` wrappers. They own Action lifecycle, resources,
   cancellation/interruption and finish state but do not duplicate:
   route/RVO state, attack timing/sequence/Commit, or ability Stage/cost/cooldown.
3. Move ordinary action start from
   `SimulationTickPipeline.ExecuteActionRequest` into
   `ActionArbiter.Submit`. Remove direct Handler start paths only after each has
   a Runtime-owned equivalent.
4. Move current-runtime interruption to the contract-gated fixed phase before
   blocked Runtime/Handler work can advance.
5. Make Reservation a stable derived projection of the two Runtime slots; do
   not create a second mutable channel-owner table.
6. Add Action Runtime capture/restore/resolve/rebuild owned by Unit semantics
   and aggregated by FrameSync `UnitSnapshot`.
7. Validate restored target/ability references visibly. Restore must not call
   normal start/cancel Handler APIs or silently repair invalid state.
8. Add every cross-Tick Action field to canonical serialization and
   SharedGameplayChecksum in stable Main-then-Base order.
9. Bump the Snapshot schema and GameplayDataVersion once; reject mixed old/new
   endpoints.
10. Project the new Runtime state into the presentation-facing read-only
    `UnitActionStateView` without allowing presentation writes.
11. Keep the legacy evaluator only as a diagnostic comparison path until the
    Milestone 3 acceptance suite and independent review pass.

Exit gate:

- formal evaluator is the sole Gameplay authority;
- continuous and Snapshot/Restore/Replay execution are equivalent;
- server/client deterministic runs agree;
- all expected shadow differences are protected by named tests and approved
  contract rows;
- no new compile error or warning is attributable to the slice.

### Milestone 4 — Cleanup and closeout

1. Delete the legacy numeric-kind evaluator, transitional Handler-derived
   shadow reservation projection and obsolete `ArbitrationResult`.
2. Delete the unreachable units-without-Planner direct command path only after
   prefab/composition tests prove every runtime Unit receives Planner/Arbiter.
3. Remove `ActionRequest.Priority` if no Current formal consumer remains; do
   not replace it with a universal numeric priority.
4. Replace the live-runtime Snapshot rejection test with full Runtime
   round-trip/replay coverage.
5. Retain diagnostic trace as a bounded optional facility; remove only the
   old-vs-new comparison toggle and migration counters.
6. Review allocations and branches in the per-Tick path. No LINQ, closure,
   boxing, runtime reflection or string formatting is allowed in the disabled
   diagnostic path.
7. Update only this ExecPlan, affected `MODULE_STATUS.md` rows and
   `CURRENT_HANDOFF.md` after implementation evidence exists.

## 7. Public contracts and ownership

| Contract | Owner | Rule |
|---|---|---|
| `ActionSubmitResult`, `ActionRejectReason` | `FrameSyncMoba.Unit` | Small immutable result; not a Command or Snapshot by itself. |
| `ActionResource`, `ActionSlot`, `ActionInterruptLevel`, `ActionStartSpec` | `FrameSyncMoba.Unit` | Formal action semantics; no ScriptableObject/runtime-config authority. |
| `UnitDecisionContext` | `FrameSyncMoba.Unit` internal | Transient readonly view; never public protocol, stored state or time source. |
| `ActionDecisionTraceRecord` | `FrameSyncMoba.Unit` diagnostics | Diagnostic projection only; bounded and excluded from Gameplay authority. |
| `IActionRuntime` and concrete Runtime state | `FrameSyncMoba.Unit` | Own Action lifecycle only; Handler local state remains authoritative for mechanism details. |
| `ActionRuntimeSnapshot` (final name contract-gated) | `FrameSyncMoba.Unit` | Lowest assembly owns Action semantics; FrameSync aggregates and serializes it. |
| `UnitSnapshot` aggregate/schema | `FrameSyncMoba.FrameSync` | Adds approved Main/Base Action state in canonical order. |
| `UnitActionStateView` | `FrameSyncMoba.Unit` | Read-only projection; presentation consumes without write authority. |

No new UID, Command, Aim, AbilitySignal, fixed-point, checksum or PlayerSlot
type may be introduced.

## 8. Validation

### 8.1 Per-milestone checks

After every C# milestone:

1. Trigger Unity AssetDatabase refresh/script compilation through Unity MCP.
2. Wait until `IsCompiling == false` and inspect Console Error, Exception and
   relevant Warning entries.
3. Run the milestone's focused EditMode fixtures.
4. Review the diff against the exact contract rows in section 4.

### 8.2 Focused EditMode suites

- New `BehaviorPlannerTests`.
- New `ActionArbiterTests`.
- New `ActionRuntimeSetTests` and concrete Runtime tests.
- Existing/extended `AttackHandlerTests`.
- Existing/extended `AbilityCostAndCastTests` and relevant ability model tests.
- Existing/extended `CrowdControlHandlerTests`.
- `MovementConformanceTests`, `MovementIntegrationTests` and relevant Dash/
  forced-move tests.
- `UnitActiveGameplayGateTests`, `UnitWorldIntegrationTests` and
  `UnitAssemblyBoundaryTests`.
- New `ActionPipelineIntegrationTests`.
- `SnapshotChecksumCompletenessTests`, `AggregateSnapshotContractTests`,
  `ChecksumNewStateCoverageTests` and `FrameSyncPipelineTests`.

### 8.3 Required deterministic cases

- repeated execution equivalence;
- continuous versus Snapshot/Restore/Replay equivalence;
- insertion/order independence for any table-driven evaluator inputs;
- invalid restored target/ability/runtime references fail deterministically;
- diagnostics off/on equivalence;
- legacy/formal shadow equivalence before authority cutover;
- client prediction versus authority replay equivalence after cutover;
- SharedGameplayChecksum changes when every new cross-Tick Action field changes.

### 8.4 Behavior matrix

- Idle -> Move, Move -> Attack, Attack -> Move, Move -> Cast, Cast -> Move.
- AttackTarget chase, range entry, target invalidation and intent continuation.
- Attack Windup allowed/denied interruption; committed recovery movement/cast;
  waiting-for-ready; unchanged ready Tick and AttackSequenceIndex.
- Movable and non-movable casts; normal cancel; TryInterrupt/ForceInterrupt;
  resource/cooldown failure remains Ability-owned.
- Stun, Root, Silence, Disarm, Fear/Charm/Taunt forced behavior, Cleanse,
  Unstoppable, KnockUp/KnockBack and competing forced displacement.
- Dash/forced-move coexistence follows existing Movement/CC rules and never
  creates a second forced-move owner.
- Spawn Tick, death, respawn, pool reset and rollback topology rebuild.

### 8.5 PlayMode and integration

- Run the existing Unit prefab/composition PlayMode suite after Runtime
  authority changes.
- Run the existing Framework smoke and relevant HeroTest/Gameplay integration
  fixtures if the pipeline or presentation projection changes.
- A full EditMode/PlayMode suite is required before Milestone 4 completion
  because Milestone 3 changes core Unit/FrameSync contracts. Retained baseline
  failures must remain separately identified, not described as new regressions.
- Live Local C/S or UOS acceptance is not required by this source plan unless a
  later user request explicitly adds it. Any build request must follow the
  one-command build discipline in the project guides.

## 9. Independent review

Risk is High because the authority cutover changes cross-assembly public action
contracts, runtime ownership, Snapshot and checksum.

After Milestone 3 implementation and focused validation, run one independent
read-only review through a separate review agent using only:

- the exact Current design sections and approved contract-gate rows;
- section 7 public ownership summary;
- the current production/test diff;
- design-to-test mapping and recorded results.

The review must cover P0/P1/P2 findings for:

- Planner/Arbiter/Runtime/Handler ownership;
- deterministic ordering and forbidden APIs;
- attack, ability, CC and forced-move semantic preservation;
- Snapshot/serialization/checksum completeness and restore phases;
- test adequacy and diagnostics isolation.

Scope-local P0/P1 findings are fixed and revalidated before completion.
Scope-external findings are recorded without expanding this plan.

## 10. Failure and recovery

- Milestones 0-2 keep the legacy evaluator authoritative. Disabling the shadow
  call restores the exact pre-migration decision path.
- Commit each milestone at an independently compiling/tested boundary. Do not
  delete legacy code before Milestone 3 acceptance.
- If a shadow mismatch is unexplained, record its Tick/Unit/request/context and
  stop authority cutover; continue unrelated tests and matrix completion.
- If the exact Runtime Snapshot contract remains unapproved, stop at the end of
  Milestone 2. Do not persist derived channel ownership or omit cross-Tick
  Runtime state to avoid a schema change.
- A Snapshot schema/data-version bump rejects mixed packages; recovery is to
  rebuild all endpoints from the same source, never to accept an old schema.
- Restore failures must remain visible deterministic exceptions. Do not repair
  missing Unit/ability references or replay Handler start callbacks.
- If Unity MCP fails, record the operation, failure, fallback and required Unity
  re-verification. A source-only compiler does not close the plan.
- Preserve the current unrelated workflow-document worktree changes.

## 11. Results

Completed against D-047 and Unit Framework v27.4.

- `BehaviorPlanner` no longer mutates Handlers; Unit/Arbiter owns Intent
  replacement and all ordinary starts. Numeric request/ActionKind priority was
  removed. Spec resolution, Handler start adaptation and Runtime reconciliation
  are separate internal services; Arbiter retains only policy orchestration.
  The unreachable no-Planner direct-Handler Pipeline fallback was removed;
  invalid composition fails visibly and CancelAbility routes through Arbiter.
- `ActionRuntimeSet` is fixed Main/Base state. Move, Attack, ordinary Cast,
  Dash and sequential-recast windows follow the frozen matrix. Handler-owned
  automatic/signal Stage transitions are re-described and may migrate the same
  session between slots without self-cancel.
- Forced Move/Taunt Attack bypass only voluntary capability blocks. Mobility
  blocks ability Dash. Handler rejection preserves the existing action.
- GameplaySnapshot is schema 23; bootstrap payload wire is 4. Both Runtime
  slots and all ten members are serialized/checksummed. Restore validates exact
  matrix/stage/target ownership and never replays start callbacks.
- Disabled action diagnostics return before record/string construction and do
  not enqueue.
- Unity compilation passed through MCP. EditMode: FrameSync `91/91`, Bootstrap
  `86/86`, Unit `519 passed / 10 retained failures`; the ten failures exactly
  match the recorded baseline categories. Focused action, forced-control,
  malformed restore, automatic Release and dual-slot replay tests passed.
- Focused PlayMode attempts encountered the already-recorded FrameworkSmoke
  SpawnPoint/team mismatch and prefab ID 9 fixture before action behavior. No
  scene/prefab changed. Last full PlayMode baseline remains `56/60` with four
  retained fixture failures.
- Independent review findings on control requests, Dash Mobility, atomic
  preemption, Toggle, exact restore shape, repeated requests, wire version,
  automatic Stage reconciliation, slot migration and diagnostic allocation
  were fixed and revalidated. Its final no-Planner direct-path P2 was also
  removed and FrameSync/Bootstrap suites rerun. No scope-local P0/P1 remains.
- Post-plan HeroTest acceptance found that its old Aatrox-specific QWER polling
  bypassed the formal PlayerInput mappings after the scene was switched to
  Varus. The harness now scene-authors `PlayerInputController` and the shared
  InputAction asset, derives every slot mapping from `CastModelDef`/`AimKind`,
  and uses one `PlayerCommandRequester` for gameplay, skill allocation and
  Shop commands. Focused evidence: PlayerInput EditMode `17/17`, simulated
  PlayMode input `4/4`, HeroTest requester/shop `2/2`, plus a clean live scene
  start with the Varus requester bound.

Remaining external acceptance: rebuild matching schema-23/wire-4 endpoints and
run Local C/S or UOS only under a new user-requested plan.

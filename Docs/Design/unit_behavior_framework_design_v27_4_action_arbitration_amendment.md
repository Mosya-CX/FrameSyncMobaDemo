# Unit Behavior Framework v27.4 — Action Arbitration Amendment

Status: Current formal amendment  
Date: 2026-08-22  
Base design: `unit_behavior_framework_design_v27_3.md`  
Decision: D-047

This amendment replaces Unit Framework v27.3 sections 3.2-3.8 and supplies
the exact ActionRuntime membership required by Snapshot Appendix v7.2 section
5.2. All unaffected v27.3 contracts remain Current.

## 1. Ownership chain

The only ordinary unit-decision chain is:

```text
Order / AI -> UnitIntent -> BehaviorPlanner -> ActionRequest
           -> ActionArbiter.Submit -> Main/Base ActionRuntime
           -> owning Handler local state machine
```

- `UnitIntent` is one persistent goal, not a list of concurrently running
  mechanisms.
- `BehaviorPlanner` reads the goal and proposes at most one temporary request
  per Unit per Tick. It never starts, cancels or resets a Handler.
- Unit composition owns Intent replacement and asks Arbiter to terminate the
  previous uncommitted behavior when required.
- `ActionArbiter.Submit` is the only ordinary action-start boundary. It owns
  eligibility, structural resource conflict and preemption.
- Every command-capable Unit must compose Planner and Arbiter. Missing
  composition fails visibly; there is no direct-Handler command fallback.
  `CancelAbility` also enters through Arbiter before Runtime reconciliation.
- `ActionRuntimeSet` owns fixed `Main` and `Base` lifecycle slots. It owns
  reservation identity only; attack timing, ability sessions/stages and
  movement trajectories remain in their Handlers.
- Crowd-control forced displacement remains
  `CrowdControl -> MovementHandler`; it is not an ActionRuntime.

## 2. Arbiter input and output

Arbiter returns immutable `ActionSubmitResult`: outcome (`Rejected`, `Granted`,
or `GrantedWithPreemption`), a stable rejection reason, and on grant the exact
`ActionStartSpec`. `ActionKind` numeric order and request priority are not
policy. Command ordering remains exclusively owned by the Command layer.

Arbiter may read capability, aggregated control blocks, the two Runtime-slot
reservations, and a structural Stage description provided by AbilityHandler.
It must not branch on hero ID, ability ID, attack timing internals or
presentation state. Ability validation, cost, cooldown, signal transition and
Stage entry remain AbilityHandler-owned.

Forced-behavior Move/Attack requests bypass only the coarse voluntary
Capability veto. They still require the corresponding baked AbilityMask,
target/mechanism validity, readiness and range, and remain subject to the
fine-grained `ControlMove` / `ControlAttack` blocks. A control Attack remains
marked in its MainRuntime so `VoluntaryAttack` alone does not cancel its
windup. Forced-behavior Move and Attack use `Forced` interrupt level and are
not rejected by an ordinary cast's voluntary movement lock.

## 3. Frozen resource matrix

`ActionResource` is
`MainAction | BaseAction | Movement | Facing | Attack | Ability`.

| Request / phase | Slot | Occupied resources | Interruptible | Additional rule |
|---|---|---|---|---|
| Voluntary or control route Move | Base | BaseAction, Movement, Facing | yes | A movement-locking ability Stage rejects voluntary Move before conflict evaluation. |
| Attack windup before Commit | Main | MainAction, Attack, Facing | yes | Attack Commit releases MainRuntime. Recovery remains AttackHandler state and keeps the existing move-cancel rule. |
| Ordinary ability action Stage | Main | MainAction, Ability, plus Facing only when `CastStage.LockMovement` | authored | LockMovement blocks voluntary Move/Attack but does not reserve Movement against configured special movement. |
| Ability Dash Stage | Base | BaseAction, Movement | authored | Dash may coexist with another Main cast and preserves that cast's locked facing. |
| Sequential-recast waiting window | none | none | n/a | AbilitySession remains alive, but the completed impact releases MainRuntime until a real legal Commit enters the next impact Stage. |
| Pure Toggle activation, active state or deactivation | none | none | n/a | Toggle signals may change the persistent AbilitySession/state but are not active casts, never reserve Main/Base resources and never preempt another action. |

Same-slot replacement or overlapping required resources cause a conflict. A
conflict may preempt only when the active Runtime is interruptible or the new
request has a stronger formal interrupt level. A signal advancing the same
active ability slot is continuation, not self-preemption. Handler rejection
never creates a Runtime token.

After every Handler advance, Arbiter re-describes each active ability Stage
and rewrites the Runtime reservation from authored Stage data. An automatic or
signal-driven Stage transition may change resources or migrate the same
AbilitySession between Main and Base; the old slot is released without sending
a self-cancel. A newly required resource preempts an interruptible conflicting
Runtime. An automatic transition that conflicts with an uninterruptible
Runtime is invalid authored behavior and fails visibly.

## 4. Required concurrency scenarios

These are content-driven acceptance scenarios, not hero branches.

### 4.1 Aatrox Q impact plus E dash

1. Q begins as Main Cast/AbilityStage and owns
   `MainAction | Ability | Facing`, with `BlocksVoluntaryMove=true`.
2. A later E order becomes Intent; Planner proposes one Cast request.
3. E's authored Dash Stage resolves to Base and owns
   `BaseAction | Movement`.
4. Arbiter grants E without interrupting Q. Q retains aim/facing and impact
   clock; E translates through MovementHandler.

### 4.2 Varus Q hold plus route movement

1. Focus starts Q Hold as Main with `MainAction | Ability` and
   `BlocksVoluntaryMove=false`.
2. A Move order becomes Intent and Planner proposes Move.
3. Arbiter grants Base Move with `BaseAction | Movement | Facing`; Q charge
   continues.
4. Release continues the same Q Runtime. Its Stage adds Facing and locks
   movement, so it preempts/cancels Base Move.

## 5. Tick phases

1. Advance crowd control and refresh Capability.
2. Reconcile Runtime slots with Handler state.
3. Interrupt now-blocked active Runtimes.
4. Planner proposes and Arbiter submits at most one request.
5. Evaluate locomotion, avoidance and route movement.
6. Advance Handler local state machines.
7. Re-describe active ability Stages, migrate Main/Base ownership when needed,
   and reconcile completed/released Runtime slots.
8. Capture/checksum may observe the resulting fixed slots.

A Runtime newly blocked by the Tick's final control state must not advance once
more before interruption.

## 6. Snapshot, restore and checksum contract

`GameplaySnapshot` schema is 23. Bootstrap payload wire version is 4 because
its reflective UnitSnapshot object graph changes; wire-v3 payloads are rejected
at the header before object decoding. Every `UnitSnapshot` contains one fixed
`ActionRuntimeSetSnapshot` with `Main` and `Base`. Each slot contains, in
canonical field order:

1. `IsOccupied : bool`
2. `Slot : ActionSlot`
3. `Kind : ActionKind`
4. `Phase : ActionRuntimePhase`
5. `OccupiedResources : ActionResource`
6. `Interruptible : bool`
7. `BlocksVoluntaryMove : bool`
8. `IsControlAction : bool`
9. `TargetUnitUid : UnitUid`
10. `AbilitySlot : byte`

Request objects, trace records, derived reservations, Tick copies, Handler
timers, aim copies, route copies and Stage copies are excluded; their authority
is transient input or an existing Handler snapshot.

Restore validates enum, empty-slot shape and the exact frozen Move/Attack
slot-resource matrix without executing start/cancel callbacks. Cast Resolve
also requires the stored slot/resources/interruptibility/movement-lock fields
to match the currently restored authored Stage. Resolve fails visibly when a
Move lacks an active locomotion task, an Attack lacks its target or matching
uncommitted windup, or a Cast lacks its ability/session/action-active Stage.
Rebuild derives no Gameplay state. SharedGameplayChecksum writes both slots and
every member above. Diagnostics are never snapshotted or checksummed, and
disabled diagnostics perform no per-request string formatting or enqueue.

## 7. Lifecycle and presentation

Death, respawn and pool reset clear both slots. Topology restore restores slots
without replaying Handler side effects. `UnitActionStateView` is read-only:
Main animation comes from Main, falling back to route movement when only Base
Move exists; Base animation comes from Base Move/Dash or the existing forced
move projection. Presentation never writes Runtime state.

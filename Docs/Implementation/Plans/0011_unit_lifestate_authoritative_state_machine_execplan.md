# ExecPlan 0011 — Unit LifeState Authoritative State Machine

> Status: **Approved — executing.** Owner approved C->B->A as 0010/0011/0012 in one batch.

## Purpose
Implement the frozen LifeState enum, Unit storage and UnitWorld transition-validation from Unit v27.3 §1.8 + D-009.

## Design sources
- Unit v27.3 §1.8 (lines 588-695): LifeState enum, Unit.LifeState storage, internal ApplyLifeStateFromUnitWorld, transition graph.
- DECISION_LOG D-009: RequestEnterDying/RequestRecoverFromDying/ConfirmUnitDeath frozen names.

## In scope
- LifeState enum (byte: Alive, Dying, Dead, Respawning)
- Unit.LifeState (public get, private set, default Alive) + internal ApplyLifeStateFromUnitWorld
- UnitWorld transition validator (§1.8 graph, error-before-mutation)
- UnitWorld.RequestEnterDying / RequestRecoverFromDying / ConfirmUnitDeath
- Dead->Respawning and Respawning->Alive internal transitions
- Focused EditMode tests

## Out of scope
Combat settlement, callbacks, ClearForDeath/Respawn, respawn timing, object-pool, CapabilityState, snapshot, AI/Orders/movement/Abilities.

## Transition graph (§1.8)
Alive -> Dying (RequestEnterDying)
Dying -> Alive (RequestRecoverFromDying)
Dying -> Dead (ConfirmUnitDeath)
Dead -> Respawning (internal)
Respawning -> Alive (internal)

## Results
Populated after execution.
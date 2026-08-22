# HISTORICAL IMPLEMENTATION ROADMAP — NOT CURRENT TASK ORDER

> Document class: Historical Planning Snapshot
> Archived: 2026-08-22
> The framework has progressed beyond these phases. Current work is driven by
> direct user requests, Current formal designs and live repository state.

# FrameSyncMobaDemo — Implementation Roadmap

> This roadmap defines implementation order, not task completion status.  
> Actual status is tracked in `MODULE_STATUS.md`.
> For the current working-tree hand-off, real UOS evidence, open startup defects
> and the exact next continuation order, read `CURRENT_HANDOFF.md` before using
> this roadmap. Historical phase text must not override that live repository
> context.

> Current sequencing rule: D-023 rejects a standalone test-harness-first milestone, but every production feature slice includes the smallest focused automated test for that feature. Comprehensive regression expansion may come later. Every slice also requires Unity compilation, Console inspection and the smallest relevant runtime smoke validation.

## Delivery strategy

Do not ask Codex to implement the whole project in one task.

Use small vertical slices that each produce:

```text
A compileable increment
A visible or testable result
Determinism tests
Snapshot/rollback tests where applicable
Updated module status
```

## Phase 0 — Repository baseline and architecture map

### Goal

Establish the real code and asset baseline without implementing Gameplay features.

### Deliverables

```text
Completed REPOSITORY_MAP.md
Updated MODULE_STATUS.md
Unity compile baseline
EditMode / PlayMode baseline
asmdef dependency diagram
Duplicate-contract report
First implementation ExecPlan
```

### Gate

No implementation phase starts until duplicate protocol types and circular asmdef risks are known.

---

## Phase 1 — Deterministic foundation

### Scope

```text
Fixed-point type or approved project equivalent
Stable UID primitives
Stable deterministic containers
Deterministic random service
Canonical serialization helpers
Checksum writer
SimulationTickContext
Test simulation harness
```

### Out of scope

```text
Full networking
Full UnitWorld
Presentation
Unity Input System
```

### Acceptance

```text
Same input produces byte-identical canonical output.
Insertion order cannot change stable output.
No float, Time, UnityEngine.Random, or Unity object identity in authoritative paths.
```

---

## Phase 2 — UnitWorld minimum lifecycle

### Scope

```text
UnitUid
UnitWorld registry
Synchronous SpawnUnit
SpawnLogicTick active gate
Minimal Unit Runtime
Formal lifecycle API shells with real state transitions
Unit snapshot round trip
```

### Vertical result

Spawn deterministic units, execute several Ticks, snapshot, restore, and replay identically.

### Gate

Unit Registry and snapshot schema must be stable before Combat and Projectile integrate.

---

## Phase 3 — Physics, range query, movement and Move input slice

### Scope

```text
Logical 2D position
PhysicsWorld query structures
RangeQuery
Minimal deterministic movement
Move Command Request
Right-click ground -> Move Command
Move Command -> Unit Intent
```

### Tests

```text
Ground point quantization
Move canonical serialization
Same Move Commands produce identical positions
Snapshot/restore/replay movement equivalence
Input callback does not modify Gameplay
Rollback does not reread Input System
```

---

## Phase 4 — Normal attack slice

### Scope

```text
Right-click hostile Unit selection
Attack Command
Attack Order / Intent
Attack Action
AttackHandler minimum Commit
Basic Combat DamageRequest
Attack presentation event
```

### Vertical result

Right-click hostile target, deterministic attack commit, damage settlement, presentation event.

---

## Phase 5 — Combat core and lifecycle

### Scope

```text
Shield / Damage / Heal request pipeline
Dying and formal death
Damage contribution
FormalDeathResults
Deferred UnitDeath / UnitKill reaction requests
MatchStatisticsRuntime
Combat snapshot
Death and respawn Handler ordering
```

### Tests

```text
Same-Tick Dying / Dead behavior
Deferred reaction import on T+1
Legal deferred sequence gaps
Invalid restore references fail
Combat snapshot round trip
No global Modifier clear
```

---

## Phase 6 — Buff and crowd control

### Scope

```text
Source-owned Modifier handles
Buff Runtime
CrowdControl Runtime
Immunity / unstoppable handles
ClearForDeath
ClearForRespawn
Permanent source handle reconstruction
```

### Gate

No module may directly clear another source's Modifier handles.

---

## Phase 7 — Ability core and player ability input

### Scope

```text
AbilityDef / CastModelDef Bake
AbilityRuntime / AbilitySession / Stage
Focus / Commit / Cancel
AimSnapshot
BakedPlayerAbilityInputProfile
PressCommit
LocalAimPrimaryCommit
PressFocusReleaseOrPrimaryCommit
Request receipts and duplicate Commit suppression
Ability indicator runtime view
```

### Vertical results

```text
Self/no-target ability
Point/direction local Aim then primary-click Commit
Hold-release ability:
    press Focus
    release or primary click Commit
    right click does not Cancel
```

### Tests

```text
Focus and Commit same TargetTick with ordered CommandSeq
Left-click Commit then key release creates one Commit
Key release Commit then left-click creates one Commit
Charge uses execution Ticks, not render time
AI directly emits AbilityAction without player input
```

---

## Phase 8 — Projectile integration

### Scope

```text
ProjectileUid
Per-Tick spawn sequence ownership
Pending spawn
Movement
Hit resolution
End / destroy
Projectile snapshot
Ability and Attack projectile production
```

### Tests

```text
Same Tick spawn ordering
Rollback regenerates identical ProjectileUid
Snapshot only stores PendingSpawns and ActiveProjectiles
```

---

## Phase 9 — Equipment, shop and GoldIncomeRuntime

### Scope

```text
Equipment Runtime
OperationLog
Purchase / Sell / Undo
GoldIncomeRuntime
Fixed source request order
GoldIncomeRecordBatch
GoldIncomeBatchDigest
CurrentAvailableGold
Confirmed settlement sink
```

### Tests

```text
RequestCheck failure creates no Command
Gold confirmation does not replay later prediction
OperationLog restore and derived balance
Gold digest deterministic and included in checksum
```

---

## Phase 10 — Full Snapshot and local rollback

### Scope

```text
Snapshot every Tick
Typed IRollback roots
Restore / Resolve / Rebuild
Rollback anchor
Command history
LocalFrameVerificationRecord
Prediction replay
```

### Gate

All integrated Gameplay modules must pass continuous-vs-replay equivalence before network authority integration.

---

## Phase 11 — FrameSync authority and recovery

### Scope

```text
CommandCollector
GameplayCommandBundle
AcceptedCommandRelay
AuthorityFrame
Canonical Command comparison
Required SharedGameplayChecksum
Single-Tick AuthorityFrame barrier
AuthorityRecovery missing-frame retransmission
Prediction lead pause
Predicted match-end pause
```

### Tests

```text
Command mismatch correction
Checksum mismatch correction
Missing authority frame recovery
No BaseSnapshot fallback
No rollback across authority boundary
Gold confirmation does not trigger active replay
```

---

## Phase 12 — Presentation and UI/Lua

### Scope

```text
VisualSnapshot
PresentationEventId
Rollback-aware event handling
Attack/Ability SFX events
VFX
Animator integration
Lua UI bridge
Read-only CurrentAvailableGold
InputSystemUIInputModule
UI pointer and keyboard gate
```

### Gate

Presentation cannot write deterministic Gameplay state.

---

## Phase 13 — Non-hero units and pathfinding

### Scope

```text
MinionSystem
JungleCamp
AIController ownership
Deterministic pathfinding
Spawn active gate
Death unregister
Deferred request source lifetime
```

---

## Phase 14 — Application flow, UOS and Dedicated Server

### Scope

```text
Test account
Menu
UOS Matchmaking
Assignment
Dedicated Server bootstrap
Lobby
Game start payload
Result
Persistence flush
Shutdown
Return to menu
```

### Out of scope for current product version

```text
Host mode
Offline Gameplay
Mid-match join
Client process restart recovery
Server process restart recovery
```

---

## Phase 15 — Integrated determinism and release gate

### Required suites

```text
Long deterministic simulation
Randomized canonical Command streams
Snapshot/restore/replay equivalence
Client/server checksum equality
Combat chain reactions
Ability hold-release
Projectile lifecycle
Gold/shop
Authority gap recovery
Presentation rollback
Dedicated Server lifecycle
```

### Release gate

No P0/P1 design mismatch, no compile errors, all required test suites pass, and module status is fully updated.

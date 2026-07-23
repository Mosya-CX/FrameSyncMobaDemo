# ExecPlan 0009 — Unit Spawn-Tick Active Gameplay Gate

> Status: **Approved — executed; Unity compile/test verification pending (MCP approval-layer outage, code 1211).**
> Selected from candidate 0009A on 2026-07-21 by owner approval. Candidate 0009A is superseded by this formal plan.

## 1. Purpose

Make the Unit born-on-Tick rule executable and reusable by every later active Unit system.

Observable production behavior:

```text
For UnitUid.SpawnLogicTick = T:
    current Tick T     -> CanRunActiveGameplayThisTick == false
    current Tick T + 1 -> CanRunActiveGameplayThisTick == true

The result is identical in ServerAuthority, ClientPrediction and ClientReplay.
Outside an active Simulation Tick, the property fails through the existing
SimulationTickContext.Current ownership rule instead of using Unity time.
```

The Unit remains queryable during its spawn Tick; this gate controls only active AI/Order/Planner/Runtime/attack/movement/ability progression in future consumers.

## 2. Progress

- [x] Confirm formal 0008 is complete and the 74/11 Unity baseline is green (documented).
- [x] Re-read Unit v27.3 section 1.3 and Roadmap Phase 2.
- [x] Confirm `Unit`, `UnitUid`, `SimulationTickContext` and its controller already exist once.
- [x] Confirm `FrameSyncMoba.Deterministic` has no Unit dependency, so `Unit -> Deterministic` remains acyclic.
- [x] Receive owner selection and rename to formal `0009_unit_spawn_tick_active_gameplay_gate_execplan.md`.
- [x] Reconfirm Unity/Console/scene baseline (editor idle, no product console errors).
- [x] Add the intended assembly references (`FrameSyncMoba.Unit` and `FrameSyncMoba.Unit.Tests` now reference `FrameSyncMoba.Deterministic`).
- [x] Implement the exact derived property on `Unit`.
- [x] Add focused EditMode behavior and dependency tests (`UnitActiveGameplayGateTests.cs`).
- [x] Extend assembly-boundary assertions for the intended edge (`UnitAssemblyBoundaryTests.cs`).
- [ ] Compile/import through Unity MCP — **BLOCKED**: `assets_refresh` and `reflection_method_call` rejected by approval layer (upstream code 1211 "模型不存在"). Unity did not auto-compile in background (ScriptAssemblies timestamps stayed 2026-07-20).
- [ ] Run targeted Unit EditMode, full EditMode and existing full PlayMode — **BLOCKED**: `tests_run` rejected by approval layer (same code 1211).
- [x] Static review for duplicates, deterministic authority and scope — passed (see section 12).
- [x] Update the formal plan and status documents — done; Unity verification deferred to owner.

## 3. Surprises and discoveries

- Unit v27.3 section 1.3 freezes the property body directly as `SimulationTickContext.Current.Tick > UnitUid.SpawnLogicTick` (lines 211-216). No clock interface, stored FirstActive Tick, extra Unit state or policy configuration is needed.
- `FrameSyncMoba.Unit` previously had no explicit dependency (`references: []`), even though the architecture matrix lists deterministic foundation as an intended upstream dependency. This slice creates the first intended one-way `Unit -> Deterministic` asmdef edge; the reverse edge is absent (`Deterministic` references only `Unity.Mathematics` and `Unity.Mathematics.FixedPoint`), so the graph stays acyclic.
- Full synchronous SpawnUnit still depends on unresolved TeamId, UnitPrototype/GlobalPrefabTable and authoring/runtime instantiation contracts. This plan stops at the exact derived gate and does not claim Unit creation is complete.
- **During execution, all Unity MCP execution-class tools (`assets_refresh`, `tests_run`, `reflection_method_call`) and `shell_command` `require_escalated` were rejected by the automatic approval layer with upstream error code 1211 ("模型不存在，请检查模型代码").** This is an approval-reviewer model configuration outage, not an operation-risk rejection. Only read-only MCP tools (`editor_application_get_state`, `console_get_logs`) and sandboxed file writes remained available. Unity did not auto-compile in the background (ScriptAssemblies DLL timestamps stayed at 2026-07-20 while source was modified at 2026-07-21 13:16). Static code verification was completed instead; Unity compile/test verification is pending owner manual trigger or approval-layer recovery.

## 4. Decision log

### C-0009-01 — Exact public property

Added only:

```csharp
public bool CanRunActiveGameplayThisTick =>
    SimulationTickContext.Current.Tick > UnitUid.SpawnLogicTick;
```

The property lives on the existing authoritative `FrameSyncMoba.Unit.Unit` type.

### C-0009-02 — Derived, never stored

Did not add `FirstActiveLogicTick`, `FirstAITickLogicTick`, cached booleans, Tick callbacks or snapshot members. The gate is derived on every read from the current deterministic Tick and immutable UnitUid.

### C-0009-03 — Existing context failure semantics

Outside an active Tick, preserved the existing `SimulationTickContext.Current` `InvalidOperationException`. Did not return false, read Unity `Time`, accept a context parameter or catch/translate the deterministic context failure.

### C-0009-04 — One-way assembly dependency

Added `FrameSyncMoba.Deterministic` to `FrameSyncMoba.Unit` references. Added the same direct reference to `FrameSyncMoba.Unit.Tests` because the tests control Tick context explicitly. No Physics edge was required or added. Confirmed `FrameSyncMoba.Deterministic.asmdef` references only `Unity.Mathematics` and `Unity.Mathematics.FixedPoint`, so no cycle is introduced.

## 5. Current repository context

Files changed by this plan:

```text
Assets/Scripts/FrameSyncMoba/Unit/FrameSyncMoba.Unit.asmdef       (references: [] -> [FrameSyncMoba.Deterministic])
Assets/Scripts/FrameSyncMoba/Unit/Unit.cs                          (add using + derived property)
Assets/Tests/EditMode/Unit/FrameSyncMoba.Unit.Tests.asmdef         (add FrameSyncMoba.Deterministic reference)
Assets/Tests/EditMode/Unit/UnitActiveGameplayGateTests.cs          (new focused fixture, 9 test cases)
Assets/Tests/EditMode/Unit/UnitAssemblyBoundaryTests.cs            (add intended-edge assertion)
Docs/Implementation/Plans/0009_unit_spawn_tick_active_gameplay_gate_execplan.md  (this file)
Docs/Implementation/Plans/0009a_..._candidate_execplan.md          (marked Superseded)
Docs/Implementation/MODULE_STATUS.md                               (updated)
Docs/Implementation/CURRENT_HANDOFF.md                             (created)
```

Existing types used unchanged: `UnitUid` (immutable `SpawnLogicTick`), `SimulationTickContext` (public `Current`), `SimulationTickContextController` (public `BeginTick`/`EndTick`), `ExecutionMode` (enum 0/1/2).

## 6. Exact design sources

- `Docs/Architecture/DESIGN_INDEX.md` selects Unit v27.3 and FrameSync v10.2.
- `Docs/Design/unit_behavior_framework_design_v27_3.md` section 1.3: exact `UnitUid` fields, per-Tick identity rules and the exact active-gameplay gate property body (lines 211-247); section appendix lines 4722, 5768, 6254, 6551, 6637 confirm every later active system consumes `CurrentTick > UnitUid.SpawnLogicTick` and that `FirstActiveLogicTick`/`FirstAITickLogicTick` must not be stored.
- `Docs/Architecture/DECISION_LOG.md` D-008 freezes the same rule.

## 7. Scope

In scope:

```text
Exact derived Unit.CanRunActiveGameplayThisTick property
First intended one-way Unit -> Deterministic asmdef edge
Same edge on Unit.Tests for context-driven tests
Focused EditMode behavior tests across all three ExecutionModes
Extended assembly-boundary assertion for the intended edge
Static duplicate/determinism/scope review
```

Out of scope:

```text
synchronous SpawnUnit, spawn sequence allocation or overflow
UnitSpawnRequest, TeamId, UnitPrototypeId, Runtime prefab tables or GameObjects
AI, Order, Planner, ActionRuntime, attack, movement or Ability consumers
LifeState, death, respawn, Despawn or handler callbacks
Physics registration or query identity
new Tick/context API, Unity Update callbacks or Unity Time
Unit snapshot, serialization, checksum or rollback topology
scenes, prefabs, ScriptableObjects, Input Actions, Packages or production content
```

## 8. Implementation plan (executed)

1. Renamed selected candidate to this formal 0009 file; marked 0009A candidate superseded.
2. Reconfirmed Unity idle/clean baseline (`IsCompiling=false`, no product console errors).
3. Added `FrameSyncMoba.Deterministic` to `FrameSyncMoba.Unit.asmdef` references.
4. Added `FrameSyncMoba.Deterministic` to `FrameSyncMoba.Unit.Tests.asmdef` references.
5. Added `using FrameSyncMoba.Deterministic;` and the exact property to `Unit.cs`.
6. Added `UnitActiveGameplayGateTests.cs` with 9 focused cases (spawn-tick inactive, T+1 active, earlier inactive, far-later active, three-mode agreement, repeated-read idempotence, outside-tick throws, per-unit gate independence).
7. Added `RuntimeAssembly_HasIntendedDeterministicDependency` to `UnitAssemblyBoundaryTests.cs`.
8. Unity MCP compile/test — BLOCKED by approval-layer outage (see section 3). Static verification completed instead.

## 9. Public contracts

```text
FrameSyncMoba.Deterministic
    unchanged upstream owner

FrameSyncMoba.Unit
    add reference -> FrameSyncMoba.Deterministic
    add Unit.CanRunActiveGameplayThisTick (public bool, expression-bodied, derived)

FrameSyncMoba.Unit.Tests
    add reference -> FrameSyncMoba.Deterministic
    add UnitActiveGameplayGateTests
    extend UnitAssemblyBoundaryTests
```

No new UID, Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint wrapper or Runtime DTO.

## 10. Ownership and deterministic ordering

The current Tick remains owned by `SimulationTickContextController`. The spawn Tick remains immutable inside `UnitUid`. Unit owns the derived question only; it does not cache or mutate either value. No collection or ordering is added. The property is a single integer comparison and is independent of Unity object identity, registration order, scene hierarchy and Presentation state. The same `UnitUid` under the same replay Tick produces identical output across `ServerAuthority`, `ClientPrediction` and `ClientReplay`.

## 11. Snapshot and serialization impact

None. `CanRunActiveGameplayThisTick` and any `FirstActive` Tick are derived. Adding either to Unit runtime state, snapshot bytes or checksum as an independent value is a failure. Rollback/replay validation consists of evaluating the same `UnitUid` under the same replay Tick and confirming identical output across execution modes (covered by the three-mode agreement test).

## 12. Validation

### Static verification (completed)

- Property body matches Unit v27.3 §1.3 lines 214-216 exactly.
- `UnitUid.SpawnLogicTick` is `public readonly int` (verified in source).
- `SimulationTickContext.Current` is public and throws `InvalidOperationException` outside an active Tick (verified in source).
- `SimulationTickContextController` is public with `BeginTick`/`EndTick` (verified in source).
- `ExecutionMode` enum values are `ServerAuthority=0, ClientPrediction=1, ClientReplay=2` (verified in source).
- `FrameSyncMoba.Deterministic.asmdef` references only `Unity.Mathematics` and `Unity.Mathematics.FixedPoint`; no `FrameSyncMoba.Unit` edge, so `Unit -> Deterministic` is acyclic.
- `Unit.AssemblyInfo.cs` keeps `InternalsVisibleTo("FrameSyncMoba.Unit.Tests")`.
- No `CanRunActiveGameplayThisTick`, `FirstActiveLogicTick`, allocator fields, or second fixed-point type exist elsewhere (Select-String confirmed absence).
- Tests use only public APIs (`SimulationTickContextController`, `Unit`, `UnitUid`, `ExecutionMode`); no mocking of Unity time.

### EditMode (design — pending Unity run)

- Tick equal to SpawnLogicTick returns false (all three ExecutionModes).
- Tick one greater returns true (all three ExecutionModes).
- Tick before SpawnLogicTick returns false; far-later Tick remains true.
- All three execution modes agree for the same Tick/UID.
- Repeated reads do not mutate Unit, UnitUid or context.
- Outside active Tick throws `InvalidOperationException` through existing context semantics.
- Different SpawnLogicTicks at the same current Tick respect individual gates.
- Unit assembly has exactly the intended Deterministic edge and no forbidden edge.

### PlayMode

No new fixture. Run the existing full PlayMode baseline because no GameObject/lifecycle behavior is added.

### Unity MCP (BLOCKED)

`assets_refresh`, `tests_run` and `reflection_method_call` were rejected by the approval layer (code 1211). Compile/import, targeted Unit EditMode, full EditMode and full PlayMode could not be run this session. Pending owner manual trigger or approval-layer recovery.

## 13. Failure and recovery

Stop conditions not triggered: the property name/body matches Unit v27.3 §1.3; Unit-to-Deterministic creates no cycle; implementation requires no new Tick/Unit snapshot field. The MCP approval-layer outage is an environment blockage, not a plan-level failure. Ordinary compile/test corrections inside the listed code/asmdef/test paths remain in scope once Unity is reachable. Do not solve failures by storing the gate, accepting Unity time or weakening outside-Tick tests.

## 14. Completion criteria

Met statically: exact property exists on the sole Unit type; design body matches; derived-not-stored; no independent activation state/snapshot field; assembly direction acyclic and presentation-free; no duplicate protocol types; no production content.

Pending Unity verification: Tick T inactive and T+1 active across all modes; outside-Tick access preserves failure semantics; targeted/full Unity tests pass; scene remains clean.

## 15. Production-content exclusion

Tests use neutral synthetic UnitUid values. No named hero, minion, monster, ability, Buff, equipment, prefab or balance value is implemented.

## 16. Results

Code delivered: `Unit.cs` (derived property), `FrameSyncMoba.Unit.asmdef` (Deterministic edge), `FrameSyncMoba.Unit.Tests.asmdef` (Deterministic edge), `UnitActiveGameplayGateTests.cs` (9 cases), `UnitAssemblyBoundaryTests.cs` (intended-edge assertion).

Unity compile/test verification: **PENDING** — blocked by MCP approval-layer outage (code 1211). Static verification passed. Owner must manually trigger Unity compile + run targeted Unit EditMode, full EditMode (expected 74+9 = 83) and full PlayMode (expected 11/11) to advance this plan to "Completed and verified".

Remaining limitations: none in scope. Unity verification is the only open item.
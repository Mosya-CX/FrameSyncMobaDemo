# ExecPlan 0003 — Deterministic Random Collection Operations

> Status: **Complete — implemented and Unity-verified on 2026-07-20.**  
> This is independent of completed ExecPlan 0002 and modifies only the existing deterministic random service and its focused tests.

## 1. Purpose

Complete the collection-oriented part of the formal deterministic random API without creating another random stream or a generic container framework:

- select a stable index from a positive count;
- select one value from a stable read-only indexed list;
- shuffle an array in place using the formal descending Fisher-Yates loop;
- preserve exact random-state capture/restore behavior and stable draw counts.

Observable result:

```text
The same seed and the same stable indexed input produce the same pick and shuffle.
Pick operations consume exactly one base draw.
Shuffling N values consumes max(0, N - 1) base draws.
Capture -> collection operations -> Restore -> replay reproduces the result.
```

## 2. Progress

- [x] Complete and Unity-verify ExecPlan 0002 independently.
- [x] Re-read the current design index, FrameSync random sections and current random implementation/tests.
- [x] Confirm no second deterministic random service or collection-random protocol exists.
- [x] Choose the smallest safe collection surfaces without inventing a stable-container type.
- [x] Create this ExecPlan before modifying the public service.
- [x] Add `PickIndex`, `PickOne` and `ShuffleInPlace` to `DeterministicRandomService`.
- [x] Add proportional EditMode behavior/state tests.
- [x] Compile and inspect Console through Unity MCP.
- [x] Run targeted deterministic and full EditMode suites; check PlayMode baseline.
- [x] Review scope/duplicates and update this plan and status documentation.

## 3. Surprises and discoveries

- FrameSync v10.2 freezes the operation names, Fisher-Yates direction and stable-input requirement, but not exact C# collection interfaces.
  Impact: `PickOne` uses `IReadOnlyList<T>` because it needs only stable indexed reads; `ShuffleInPlace` initially accepts `T[]` because arrays guarantee indexed mutation without the ambiguous read-only/custom behavior of `IList<T>`.
- No current production consumer requires `List<T>` or a project-owned stable collection abstraction.
  Impact: no overload or generic container protocol is added speculatively.
- The existing random snapshot stores only package state.
  Impact: collection operations need no snapshot schema change; they are replayable solely through stable base-draw consumption.
- Unity MCP's assembly filter again reports the project-wide discovered total separately from the filtered passing count.
  Impact: the deterministic targeted run is recorded as 30 executed/passed cases out of 35 project tests; the following unfiltered run passed all 35/35.

## 4. Decision log

### D-0003-01 — Exact public additions

Add only:

```csharp
public int PickIndex(int count);
public T PickOne<T>(System.Collections.Generic.IReadOnlyList<T> readOnlyList);
public void ShuffleInPlace<T>(T[] values);
```

No new service, random state, DTO, container type or extension method is added.

### D-0003-02 — Stable draw counts

- `PickIndex(count > 0)` delegates to `NextInt(0, count)` and consumes one draw, including `count == 1`.
- `PickOne` validates first, then consumes the same one draw through `PickIndex`.
- `ShuffleInPlace` follows `for i = Length - 1 down to 1`, consuming exactly `Length - 1` draws.
- Invalid inputs consume no draw.

### D-0003-03 — Indexed stable input only

The methods use `Count`/`Length` and index access. They never enumerate a `Dictionary`, `HashSet` or arbitrary enumerable. Callers own the semantic requirement that the supplied list/array order is stable.

### D-0003-04 — No array/list allocation in production

Selection returns an existing value and shuffle mutates the caller's array. Production methods use no LINQ, iterator, closure, reflection or temporary collection.

## 5. Current repository context

- `FrameSyncMoba.Deterministic` is a no-engine assembly referencing only `Unity.Mathematics` and `Unity.Mathematics.FixedPoint`.
- `DeterministicRandomService` is the sole project Gameplay random stream.
- It already provides canonical base integer mapping, fixed-point/bool/chance operations and State capture/restore.
- `FrameSyncMoba.Deterministic.Tests` contains 25 passing cases before 0003.
- Full EditMode after completed 0002 is 30/30.
- No PlayMode test is required by this no-engine slice.
- Pre-task Git workspace records are ignored; only the files named below belong to this execution.

Expected modified files:

```text
Assets/Scripts/FrameSyncMoba/Deterministic/DeterministicRandomService.cs
Assets/Tests/EditMode/Deterministic/DeterministicRandomServiceTests.cs
Docs/Implementation/Plans/0003_deterministic_random_collection_operations_execplan.md
Docs/Implementation/MODULE_STATUS.md
Docs/Architecture/REPOSITORY_MAP.md
```

No asmdef modification is expected.

## 6. Design sources

- `Docs/Architecture/DESIGN_INDEX.md` selects FrameSync v10.2 and Snapshot Appendix v7.2.
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`
  - 16.1 requires one Gameplay random stream;
  - 16.2 requires state capture/replay;
  - 16.3 names `PickIndex`, `PickOne` and `ShuffleInPlace`;
  - 16.4 requires stable base-draw counts, stable ordered inputs and descending Fisher-Yates shuffle.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`
  - 11 keeps `DeterministicRandomSnapshot.State` as the required state.
- `Docs/Implementation/ROADMAP.md`
  - Phase 1 requires the deterministic random service and insertion-order-stable output.
- `Docs/Architecture/DECISION_LOG.md`
  - D-022 fixes the `fp` boundary;
  - D-023 requires proportional tests.
- `Docs/Implementation/Plans/0001_deterministic_tick_context_and_random_state_execplan.md`
  - D-0001-04 deliberately deferred these collection operations to the same service.

## 7. Scope

### In scope

- the three exact public methods in D-0003-01;
- null/empty/nonpositive input validation before state consumption;
- indexed stable access and formal Fisher-Yates order;
- existing snapshot replay of collection calls;
- minimal EditMode tests and Unity MCP validation;
- plan/status synchronization.

### Out of scope

- `RandomDirection2D`, circle-point helpers or any geometric randomness;
- new random streams, seeds, call-count state or snapshot members;
- new deterministic container/list interfaces;
- `IEnumerable<T>`, Dictionary or HashSet based overloads;
- weighted choice, sampling without replacement or production loot/AI/content logic;
- Unit, UnitWorld or any 0002 contract modification;
- Packages, asmdefs, scenes, prefabs, ScriptableObjects, Input Actions or ProjectSettings;
- production heroes, abilities, Buffs, equipment, units or map content.

## 8. Implementation plan

1. Add `System.Collections.Generic` to the existing service.
2. Implement positive-count validation and `PickIndex` through the existing canonical `NextInt`.
3. Implement null/empty validation and indexed `PickOne` through `PickIndex`.
4. Implement null validation and the formal descending Fisher-Yates array shuffle.
5. Add minimal tests for selection mapping/draw count, invalid-state preservation, deterministic shuffle/draw count and snapshot replay.
6. Refresh/compile through Unity MCP, inspect Console, run targeted/full EditMode and query PlayMode baseline.
7. Review the two-file code diff for allocation, unstable enumeration, duplicate protocol and scope.
8. Complete Results and update module/repository documentation.

## 9. Public contracts

Modified public type:

```text
FrameSyncMoba.Deterministic.DeterministicRandomService
```

Added signatures are exactly those in D-0003-01. Existing signatures and `DeterministicRandomSnapshot` are unchanged.

No UID, Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint or Runtime DTO type is added.

## 10. Validation

### EditMode

- `PickIndex`/`PickOne` use one canonical ranged draw and stay in range.
- null, empty and nonpositive inputs throw before changing random State.
- the same seed/input yields the same shuffled permutation.
- shuffle preserves all input elements and consumes exactly `max(0, N - 1)` draws.
- capture/restore reproduces picks and shuffled output.
- all existing deterministic and Unit tests remain green.

Run targeted `FrameSyncMoba.Deterministic.Tests`, then all EditMode tests.

### PlayMode

Not required. Query the project baseline; do not add a fixture for a no-engine API.

### Unity MCP

Clear or isolate Console, refresh AssetDatabase, wait for idle compilation, inspect diagnostics, run tests, and confirm `GameScene` remains clean.

## 11. Failure and recovery

Stop if the current indexed design changes the named operations or requires a new collection/snapshot protocol, a Package, an asmdef dependency change, or an out-of-scope public contract.

Otherwise fix only the two code/test files named in this plan. Do not reset or inspect the pre-task Git baseline. Never disable or weaken a failing test.

## 12. Completion criteria

- all three exact methods exist on the sole random service;
- validation occurs before random-state consumption;
- pick draw counts and Fisher-Yates draw/order rules are stable;
- no production allocation or unstable enumeration is introduced;
- snapshot capture/restore replays collection operations without schema change;
- Unity compiles with no new product diagnostic;
- targeted and full EditMode tests pass;
- PlayMode remains correctly not applicable;
- no asmdef, Package, Unity asset or production content changes occur;
- this plan and status documents contain actual results.

## 13. Results

```text
Production file modified:
    Assets/Scripts/FrameSyncMoba/Deterministic/DeterministicRandomService.cs
Public contract additions:
    PickIndex(int count)
    PickOne<T>(IReadOnlyList<T> readOnlyList)
    ShuffleInPlace<T>(T[] values)
Snapshot/serialization:
    No schema or state change; existing State capture/restore replays all calls.
Tests modified:
    Assets/Tests/EditMode/Deterministic/DeterministicRandomServiceTests.cs
    5 focused collection-operation cases added.
Unity compilation:
    AssetDatabase refresh completed.
    IsCompiling=false and IsUpdating=false.
    Post-refresh Console query returned no entry.
Targeted EditMode:
    FrameSyncMoba.Deterministic.Tests: 30 passed, 0 failed, 0 skipped.
Full EditMode:
    35 passed, 0 failed, 0 skipped.
PlayMode:
    No tests found; not applicable to this no-engine API slice.
Deterministic invariants:
    Pick operations consume exactly one canonical ranged draw.
    Invalid inputs do not change State.
    Shuffle follows descending Fisher-Yates and consumes Length - 1 draws.
    Empty/single arrays consume no draw.
    Same seed/input and capture/restore produce identical results.
    Production methods use indexed access without LINQ, enumeration or allocation.
Duplicate result:
    The existing DeterministicRandomService remains the sole Gameplay random
    service; no new Snapshot, container or protocol type was added.
Remaining limitations:
    Geometric random helpers and a project stable-container framework remain
    outside this slice. Shuffle currently accepts arrays only by design.
Scope-external changes:
    None. No asmdef, Package, scene, prefab, ScriptableObject, Input Actions,
    ProjectSettings, Unit contract or production content was changed.
    GameScene remained clean.
```

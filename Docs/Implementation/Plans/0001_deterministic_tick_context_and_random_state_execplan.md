# ExecPlan 0001 — Deterministic Tick Context and Random State

> Status: **Complete — implemented on 2026-07-19 and independently accepted with fixes on 2026-07-20.**  
> This is the only current executable 0001. The earlier assembly/test-harness-only proposal is archived under `Plans/Archive/` and must not be executed.

## 1. Purpose

Implement the first production-quality deterministic foundation slice used by every later Gameplay module:

- a single global read-only `SimulationTickContext` containing `Tick`, `DeltaTick` and `ExecutionMode`;
- an explicit top-level controller that is the only public write capability for beginning and ending a Gameplay Tick;
- the single `DeterministicRandomService` required by FrameSync;
- capture/restore of `DeterministicRandomSnapshot.State` so replay produces the same sequence;
- a project-owned runtime assembly boundary and focused EditMode tests for the delivered behavior.

Observable result:

```text
Top-level code begins Tick T.
Gameplay reads one immutable Current context with DeltaTick = 1.
The deterministic random service advances one shared state.
Capture -> advance -> restore -> replay produces the same values.
Unity compiles the isolated runtime assembly and its focused tests pass.
```

This slice delivers real reusable runtime behavior. It is not a test-harness-only milestone and contains no production hero or gameplay content.

## 2. Progress

- [x] Owner selected the 16 Current design files under `Docs/Design/`.
- [x] Owner accepted the current working tree and all 616 intentional deletions as the implementation baseline.
- [x] Owner approved the package `fp` type (`Unity.Mathematics.FixedPoint.fp`) for authoritative Gameplay and focused tests with each feature.
- [x] Confirm Unity MCP is connected and the Editor is idle.
- [x] Search current code for duplicate Tick context and deterministic random contracts; none exist.
- [x] Review the FrameSync, Snapshot, Pathfinding, Combat and Unit requirements used by this slice.
- [x] Archive the superseded 0001 draft.
- [x] Synchronize the resolved audit findings and current implementation readiness documentation.
- [x] Create the deterministic runtime and EditMode test asmdefs.
- [x] Implement `ExecutionMode`, `SimulationTickContext` and its controller.
- [x] Implement `DeterministicRandomSnapshot` and the primitive `DeterministicRandomService` slice.
- [x] Add focused EditMode tests for Tick ownership, deterministic sequences, snapshot replay and assembly boundaries.
- [x] Refresh assets and compile through Unity MCP.
- [x] Inspect the post-change Console through Unity MCP.
- [x] Run the targeted EditMode assembly and full EditMode baseline through Unity MCP.
- [x] Confirm PlayMode is not required for this pure deterministic slice; the baseline reports no PlayMode tests.
- [x] Review the final diff and update this plan, `MODULE_STATUS.md` and `REPOSITORY_MAP.md`.
- [x] Re-audit the real repository against Purpose, Scope, Public Contracts, Validation and Completion Criteria on 2026-07-20 rather than relying on completion markers.
- [x] Recheck protocol duplication, deterministic hazards, snapshot state, stable ordering, lifecycle ownership and assembly dependency direction.
- [x] Fix the two acceptance P1 findings: canonical ranged-integer mapping and incomplete public-primitive/mode-invariance test coverage.
- [x] Recompile through Unity MCP, inspect Console, run the targeted and full EditMode suites, and rerun the PlayMode discovery baseline.
- [x] Record acceptance results and synchronize `MODULE_STATUS.md`; leave `REPOSITORY_MAP.md` and `DECISION_LOG.md` unchanged because ownership, structure and formal architecture decisions did not change.

## 3. Surprises and discoveries

- 2026-07-19: the only 0001 present at execution start was explicitly marked `Superseded` and `do not execute`.
  Impact: it was moved to `Docs/Implementation/Plans/Archive/`; this plan is the replacement.
- 2026-07-19: no current C# definition of `SimulationTickContext`, `ExecutionMode`, `DeterministicRandomService` or `DeterministicRandomSnapshot` exists.
  Impact: this slice creates the first definitions and does not migrate or duplicate an existing protocol.
- 2026-07-19: FrameSync and Pathfinding consistently name the enum `ExecutionMode`; Combat contains one illustrative local declaration using `SimulationExecutionMode`.
  Impact: the owning FrameSync contract and Pathfinding formal naming rule resolve the type name to `ExecutionMode`; no second alias is created.
- 2026-07-19: `Unity.Mathematics.Random` exposes its `uint state`, and the installed fixed-point package exposes `fp.FromRaw(long)` and `fp.RawValue`.
  Impact: the service can preserve one stable package-backed random state and map one `uint` draw exactly into `[0, 1)` as the Q31.32 type's 32 raw fractional bits.
- 2026-07-19: package source places `fp` in namespace `Unity.Mathematics.FixedPoint`; several audit documents had shortened it incorrectly to `Unity.Mathematics.fp`.
  Impact: active architecture/status/planning documents now use the actual full name. This is the same owner-selected `fp.cs` type and does not introduce a wrapper or change the design.
- 2026-07-19: clearing the pre-change Unity Console failed because the MCP log cache file was locked by the reconnected MCP process.
  Impact: validation uses timestamps/post-refresh diagnostics and explicit compiler/test results; the pre-existing MCP connection errors remain non-product logs.
- 2026-07-20: all 19 pre-acceptance EditMode cases passed, but several delivered public random primitives and the explicit execution-mode invariance requirement lacked direct behavioral coverage.
  Impact: five focused cases were added for mixed primitives, mixed snapshot replay, integer bounds, mid-range chance semantics and mode invariance; no production scope was added.
- 2026-07-20: the installed package's ranged `Random.NextInt` uses a multiply-high mapping, while FrameSync section 16.4 specifies the canonical `NextUInt() % range` mapping.
  Impact: the wrapper now computes the formal modulo mapping from exactly one unsigned draw, including ranges whose width exceeds `int.MaxValue`; one focused test locks the mapping and state consumption.
- 2026-07-20: PlayMode discovery still reports no tests and the MCP package logs that tool response as an Editor Console error.
  Impact: this is an expected tool-level baseline for a pure no-engine slice, not a compiler or product-code failure; no PlayMode fixture was invented merely to remove the message.

## 4. Decision log

### D-0001-01 — Smallest production foundation slice

Implement Tick context ownership and primitive deterministic random state together. They are directly connected by the top-level Tick pipeline and are required by all downstream deterministic systems.

Do not add UID, Command, aggregate Snapshot, Aim, AbilitySignal, Checksum, UnitWorld or other Roadmap phases.

### D-0001-02 — Formal execution-mode name

Use public enum `ExecutionMode` with:

```text
ServerAuthority
ClientPrediction
ClientReplay
```

Reason: FrameSync owns Tick semantics and uses this name; Pathfinding explicitly freezes `ExecutionMode ExecutionMode`. The single Combat example using `SimulationExecutionMode` is not used to create an alias or duplicate protocol.

### D-0001-03 — Explicit controller owns writes

`SimulationTickContext.Current` is globally readable and immutable during a Tick. A distinct `SimulationTickContextController` exposes `BeginTick` and `EndTick`; normal Gameplay code receives no setter through the context itself.

The first implementation fixes `DeltaTick` to `1`, rejects nested active Ticks and clears `Current` at Tick end. The controller is owned only by the future top-level FrameSync pipeline/composition root.

### D-0001-04 — Package-backed single random stream

Wrap `Unity.Mathematics.Random` rather than inventing another PRNG. A zero seed/state is invalid because the package generator requires nonzero state.

This slice exposes only the primitive operations needed to establish the service:

```text
NextUInt
NextInt
NextInt(minInclusive, maxExclusive)
NextFp01
NextFp(minInclusive, maxExclusive)
NextBool
Chance01
ChancePercent
Capture
Restore
```

Collection selection, shuffle and geometric random helpers remain later additions to the same service when a consuming slice requires them.

### D-0001-05 — Snapshot contains State only

`DeterministicRandomSnapshot` stores `uint State`. The design marks `CallCount` optional, so it is omitted. Restore rejects zero state and performs no silent repair.

### D-0001-06 — Proportional tests

Add focused EditMode tests with the feature. No PlayMode test is added because the runtime has no scene, GameObject, Input System, presentation or Unity lifecycle dependency.

### D-0001-07 — Canonical ranged-integer mapping

`NextInt(minInclusive, maxExclusive)` consumes exactly one `NextUInt()` and returns `minInclusive + draw % range`, as specified by FrameSync section 16.4. The range width is calculated through `long` and represented as `uint` so every valid signed-int exclusive range remains well-defined without overflow.

This is conformance to an existing formal design rule, not a new cross-project architecture decision; therefore `Docs/Architecture/DECISION_LOG.md` is unchanged.

## 5. Current repository context

Before implementation:

- Unity 2022.3.62f1c1; Editor idle and connected through Unity MCP.
- `com.unity.mathematics` 1.2.6 provides `Unity.Mathematics.Random`.
- `com.danielmansson.mathematics.fixedpoint` provides assembly `Unity.Mathematics.FixedPoint` and type `Unity.Mathematics.FixedPoint.fp`.
- no project-owned Gameplay or test asmdef exists;
- no current project test is discoverable;
- current formal Gameplay runtime types are absent;
- all legacy tracked deletions are intentional and must not be restored.

Expected paths:

```text
Assets/Scripts/FrameSyncMoba/Deterministic/
    FrameSyncMoba.Deterministic.asmdef
    ExecutionMode.cs
    SimulationTickContext.cs
    SimulationTickContextController.cs
    DeterministicRandomSnapshot.cs
    DeterministicRandomService.cs

Assets/Tests/EditMode/Deterministic/
    FrameSyncMoba.Deterministic.Tests.asmdef
    SimulationTickContextTests.cs
    DeterministicRandomServiceTests.cs
    DeterministicAssemblyBoundaryTests.cs
```

## 6. Design sources

Authoritative requirements:

- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`
  - 8.1 fixed logic Tick;
  - 8.5 `SimulationTickContext`;
  - 13.2 top-level Tick ordering;
  - 16 `DeterministicRandomService`.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`
  - 11 `DeterministicRandomSnapshot`;
  - 12 Restore/Resolve/Rebuild order.
- `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md`
  - 1.4 exact context shape, naming and `DeltaTick = 1`.
- `Docs/Design/moba_combat_system_design_v13_2.md`
  - 1.4 read-only Tick context constraints.
- `Docs/Design/unit_behavior_framework_design_v27_3.md`
  - 1.3 confirms consumers read `SimulationTickContext.Current.Tick` and do not own another clock.
- `Docs/Architecture/DECISION_LOG.md`
  - D-001 Tick semantics;
  - D-004 restore phases;
  - D-022 `fp` boundary;
  - D-023 proportional feature tests;
  - D-024 accepted implementation baseline.

## 7. Scope

### In scope

- one no-engine production deterministic asmdef;
- one Editor-only test asmdef;
- Tick context enum/value/controller;
- one package-backed deterministic random stream;
- state capture and restore;
- primitive integer/fixed-point/bool/chance operations;
- focused EditMode tests;
- Unity MCP compilation, Console and test validation;
- required documentation/status updates.

### Out of scope

- UID types or sequence owners;
- Command schemas or canonical Command bytes;
- aggregate GameplaySnapshot, rollback coordinator or snapshot store;
- Checksum writer or `SharedGameplayChecksum`;
- Unit, Physics, Pathfinding, Combat, Projectile, Ability, Attack, Buff, CrowdControl, Equipment/Gold, Player Input, Presentation, UI or Application runtime;
- collection shuffle/pick and random geometry helpers;
- ScriptableObject authoring/Bake assets;
- scene, prefab, Input Actions, Package or ProjectSettings changes;
- production heroes, skills, Buffs, equipment, minions, monsters, maps or balance content;
- restoration of deleted legacy files.

## 8. Implementation plan

1. Create `FrameSyncMoba.Deterministic` with explicit references only to `Unity.Mathematics` and `Unity.Mathematics.FixedPoint`, `autoReferenced=false`, `noEngineReferences=true` and no unsafe code.
2. Add `ExecutionMode` with explicit stable values `0..2`.
3. Add immutable `SimulationTickContext` with public read-only `Current` and internal set/clear operations.
4. Add `SimulationTickContextController` that prevents nested Ticks, fixes `DeltaTick=1`, exposes the Current value during a Tick and clears it at end.
5. Add immutable `DeterministicRandomSnapshot(State)`.
6. Add `DeterministicRandomService` backed by `Unity.Mathematics.Random`, with validation, primitive functions and capture/restore.
7. Map a single `uint` draw to `fp` `[0,1)` through `fp.FromRaw(value)`, preserving exactly one base draw.
8. Ensure chance calls consume exactly one draw even for clamped 0%/100%, following the design pseudocode's stable-call rule.
9. Create the Editor-only test assembly and focused tests.
10. Refresh/compile through Unity MCP, inspect diagnostics, run targeted and full EditMode tests, then update Results.

## 9. Public contracts

New public runtime contracts:

```text
enum ExecutionMode
readonly struct SimulationTickContext
sealed class SimulationTickContextController
readonly struct DeterministicRandomSnapshot
sealed class DeterministicRandomService
```

No existing public contract is modified. No UID, Command, aggregate Snapshot, Aim, AbilitySignal, Checksum or FixedPoint type is added.

Assembly dependency contracts:

```text
FrameSyncMoba.Deterministic
    -> Unity.Mathematics
    -> Unity.Mathematics.FixedPoint

FrameSyncMoba.Deterministic.Tests
    -> FrameSyncMoba.Deterministic
    -> Unity.Mathematics.FixedPoint
    -> Unity Test Framework (Editor only)
```

## 10. Validation

### Compilation

- refresh AssetDatabase through Unity MCP;
- wait for `IsCompiling=false`;
- inspect all post-change Console diagnostics;
- accept no new C# error or warning from this slice.

### EditMode

Required focused checks:

- `Current` throws outside an active Tick;
- begin sets exactly the requested Tick/mode and `DeltaTick=1`;
- nested begin is rejected without replacing Current;
- end clears Current;
- identical seed produces identical integer sequence;
- capture/advance/restore/replay reproduces the sequence;
- zero seed and zero restored state are rejected;
- `NextFp01` remains within `[0,1)`;
- clamped chance calls consume one deterministic draw;
- runtime assembly has no direct UnityEngine, UnityEditor, Netcode, Transport, Input, UI, Presentation or UOS reference.

Run:

1. `FrameSyncMoba.Deterministic.Tests`.
2. all EditMode tests.

### PlayMode

Not required. If implementation unexpectedly touches Unity lifecycle, scene objects, input or presentation, stop and revise this plan rather than silently omitting PlayMode validation.

### Determinism and replay

- same seed and calls produce the same outputs;
- snapshot restore reproduces subsequent outputs exactly;
- execution mode does not change Tick values or random results;
- no Unity time, random, object identity or collection enumeration order participates.

### Acceptance validation results — 2026-07-20

- `P0`: none found.
- `P1-0001-A`: ranged `NextInt` did not implement the formal modulo mapping. Fixed in `DeterministicRandomService` and locked by a one-draw canonical-mapping test.
- `P1-0001-B`: the original 19 tests did not directly cover all delivered primitive categories, mixed-operation snapshot replay, mid-range chance behavior or execution-mode invariance. Fixed with five focused behavior tests.
- `P2-0001-A`: `MODULE_STATUS.md` still contained the old 19-test count and two stale audit statements claiming selected design/numeric sources were absent. Corrected as required status synchronization; no unrelated code cleanup was performed.
- Duplicate-protocol search found only the planned `DeterministicRandomSnapshot`; no UID, Command, Aim, AbilitySignal, Checksum, FixedPoint or Runtime DTO definition was added by 0001.
- The production asmdef still references only `Unity.Mathematics` and `Unity.Mathematics.FixedPoint`; the Editor test asmdef depends downstream on the runtime assembly.
- Static review found no authoritative `float`/`double`, Unity time/random/object identity, hash-container enumeration, Presentation writeback, placeholder implementation, swallowed exception, disabled test or TODO in the slice.
- Unity MCP final compilation: `IsCompiling=false`, `IsUpdating=false`, and the post-refresh Console query was empty.
- Targeted EditMode: 25 passed, 0 failed, 0 skipped.
- Full EditMode: 25 passed, 0 failed, 0 skipped.
- PlayMode: no tests found; not required by this pure deterministic, no-engine slice.

## 11. Failure and recovery

The slice is additive. Do not reset or restore the dirty working tree.

If compilation fails, fix only errors caused by the files listed in this plan. Record external diagnostics without expanding scope.

If the random package API differs from the inspected installed version, adjust the wrapper without changing the public state contract. Do not add a package.

If a focused test fails, fix the production behavior or the test's incorrect assumption; never delete, disable or weaken the test to obtain a pass.

The task resumes from the first unchecked Progress item.

## 12. Results

```text
Runtime assembly:
    FrameSyncMoba.Deterministic
    autoReferenced=false
    noEngineReferences=true
    direct references: Unity.Mathematics, Unity.Mathematics.FixedPoint
Production contracts:
    ExecutionMode
    SimulationTickContext
    SimulationTickContextController
    DeterministicRandomSnapshot
    DeterministicRandomService
Focused tests:
    FrameSyncMoba.Deterministic.Tests (Editor only)
    SimulationTickContextTests
    DeterministicRandomServiceTests
    DeterministicAssemblyBoundaryTests
    25 discovered cases

Unity compilation:
    AssetDatabase refresh completed.
    IsCompiling=false and IsUpdating=false after final import.
    No C# compiler diagnostic was observed.
Console diagnostics:
    No diagnostic originates from formal 0001 production or test code.
    The final one-minute post-validation query returned no Console entry.
    Existing/operational entries remain: Visual Studio UDP port warning;
    two domain-reload assertions during MCP refresh; MCP Console/Clear Logs
    IOException because its own log cache was locked; and the expected
    PlayMode "No tests found" tool entry. None is a CS#### diagnostic.
Targeted EditMode:
    Passed 25/25; failed 0; skipped 0.
Full EditMode:
    Passed 25/25; failed 0; skipped 0.
PlayMode:
    No tests found. No PlayMode fixture is required because the slice has no
    Unity lifecycle, scene, GameObject, Input System or Presentation behavior.

Design invariants verified:
    Current is readable only during an active Tick.
    Tick context is immutable to consumers and DeltaTick is exactly 1.
    Nested/competing Tick ownership is rejected without replacing Current.
    ExecutionMode numeric values are stable at 0, 1 and 2.
    One nonzero random state produces a repeatable sequence.
    Ranged NextInt uses one unsigned draw and the formal modulo mapping.
    Capture/Restore reproduces both uniform and mixed primitive sequences exactly.
    NextFp01 maps one uint draw into Q31.32 [0,1) raw fractional bits.
    Mid-range and clamped chance calls use the formal scale and consume exactly one draw.
    ServerAuthority, ClientPrediction and ClientReplay do not change random results.
    The runtime assembly has no direct Engine, Editor, Netcode, Transport,
    Input, UI, UOS or XLua dependency.
Files changed:
    Five runtime C# files and one runtime asmdef under
    Assets/Scripts/FrameSyncMoba/Deterministic/.
    Three test C# files and one test asmdef under
    Assets/Tests/EditMode/Deterministic/.
    Unity-generated .meta files for those new assets.
    Audit/status/plan documentation and the asmdef inventory.
Remaining limitations:
    No top-level FrameSync loop consumes the context yet.
    UID, Command, canonical writer/checksum, aggregate GameplaySnapshot,
    rollback orchestration and stable containers remain future slices.
    The random service intentionally omits collection/geometric helpers.
Scope-external changes:
    None. No Package, ProjectSettings, scene, prefab, ScriptableObject,
    Input Actions or production content was changed. No intentional deletion
    was restored. The only additional documentation correction changes the
    package type's full name from the invalid Unity.Mathematics.fp spelling to
    the source-defined Unity.Mathematics.FixedPoint.fp. The 2026-07-20
    acceptance changed only one in-scope random mapping, its focused tests,
    acceptance/status documentation, and the unexecuted next ExecPlan.
```

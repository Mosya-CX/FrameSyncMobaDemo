# ExecPlan 0002 — Stable UnitUid Value Contract

> Status: **Complete — implemented and Unity-verified on 2026-07-20.**  
> The owner authorized continuous execution of 0002 followed by a separate 0003 deterministic-random collection slice.

## 1. Purpose

Create the single authoritative value and comparison contract for deterministic unit runtime identity:

- one strongly typed `UnitUid` owned by the Unit domain;
- the exact formal identity components `SpawnLogicTick`, `RuntimeEntityPrefabId` and `SpawnSequenceInTick`;
- value equality suitable for stable lookup keys;
- the formal lexicographic comparison used by deterministic sorting;
- an isolated no-engine Unit assembly and focused EditMode verification.

Observable production behavior:

```text
Given the same three deterministic spawn components,
two independently constructed UnitUid values compare equal.

Given any set of UnitUid values,
sorting them always uses:
    SpawnLogicTick ascending,
    then RuntimeEntityPrefabId ascending,
    then SpawnSequenceInTick ascending,
and the canonical result is independent of insertion order.
```

This is a real reusable runtime contract required by UnitWorld, snapshots, pathfinding, physics queries, Combat, Commands and Presentation lookup. It does not create units or implement any game content.

## 2. Progress

- [x] Accept ExecPlan 0001 against the real repository, fix its in-scope P1 findings and rerun Unity validation.
- [x] Confirm no current `UnitUid` or competing project UID definition exists.
- [x] Reconcile Unit v27.3, FrameSync v10.2, Pathfinding v13.1, Snapshot v7.2 and Roadmap requirements relevant to this value-only slice.
- [x] Compare the stable UnitUid contract with deterministic random collection operations and canonical byte/checksum primitives.
- [x] Select the smallest candidate whose public shape and ordering are already frozen by current designs.
- [x] Create this plan without executing it.
- [x] Recheck the repository and current design index immediately before implementation.
- [x] Create the Unit runtime and Editor test assembly definitions.
- [x] Implement only the exact `UnitUid` value/equality/comparison contract.
- [x] Add focused EditMode behavior and assembly-boundary tests.
- [x] Compile through Unity MCP, inspect Console and run targeted/full EditMode tests.
- [x] Review the implementation for duplication, scope and dependency direction without re-auditing the pre-task Git baseline.
- [x] Update this plan, `MODULE_STATUS.md`, `REPOSITORY_MAP.md` and the asmdef inventory for the structure actually created.

## 3. Surprises and discoveries

- 2026-07-20: Roadmap Phase 1 names stable UID primitives, while Phase 2 repeats `UnitUid` as part of the UnitWorld lifecycle slice.
  Impact: this plan extracts only the already-frozen value/comparison leaf; allocation, registry, spawning, lifecycle and snapshots remain Phase 2 work.
- 2026-07-20: Unit v27.3 owns the fields and sequence authority; Pathfinding v13.1 independently freezes the exact lexicographic comparison order and explicitly forbids Pathfinding from redefining the UID.
  Impact: one Unit-owned contract can be implemented without inventing an infrastructure-owned duplicate.
- 2026-07-20: designs use invalid UnitUid concepts in consuming systems but do not freeze a universal sentinel, validation ranges or `IsValid` API in the reviewed identity section.
  Impact: this slice must not guess an invalid value or reject component combinations; validity policy waits for its owning lifecycle/serialization contract.
- 2026-07-20: FrameSync requires UIDs to be serializable, but the current repository has no frozen canonical writer or aggregate snapshot root.
  Impact: this plan exposes the exact immutable components and lossless reconstruction but does not invent byte layout, endianness, serializer APIs or snapshot membership.
- 2026-07-20: Unity MCP's assembly-filtered test response reported 30 project tests discovered while reporting 5 passing tests for the requested Unit assembly.
  Impact: the targeted result is recorded as 5 executed Unit tests; the immediately following unfiltered run passed all 30/30 project EditMode tests.

## 4. Decision log

### D-0002-01 — Select the stable UnitUid value leaf

Implement the identity value and comparison semantics before UnitWorld. This advances the Roadmap foundation and removes a high-centrality missing contract without coupling several independent systems into one slice.

### D-0002-02 — Unit owns the public type

Create `UnitUid` in namespace and assembly `FrameSyncMoba.Unit`. Do not place it in FrameSync, Physics, Pathfinding or a generic UID package because Unit v27.3 owns its meaning and spawn sequence.

The initial value-only assembly needs no dependency on `FrameSyncMoba.Deterministic`. A later UnitWorld slice may add that one-way dependency when it consumes `SimulationTickContext`; this plan must not add an unused edge.

### D-0002-03 — Exact value surface, no speculative lifecycle API

Implement only:

```text
SpawnLogicTick: int
RuntimeEntityPrefabId: int
SpawnSequenceInTick: byte
construction
field-wise equality
lexicographic comparison
equality operators
value-compatible GetHashCode
```

Do not add `Invalid`, `IsValid`, generation, parsing, string protocols, registry indices, aliases or conversion to other UID types. Their semantics are not part of this reviewed leaf.

### D-0002-04 — Comparison is canonical; hashing is not ordering

`CompareTo` follows the exact formal tuple order. `GetHashCode` must be value-compatible and deterministic for equal values, but no test or production contract treats a hash value or hash-container enumeration as canonical order.

### D-0002-05 — Serialization remains a separate frozen contract

Do not introduce canonical bytes, a general writer, aggregate Snapshot, `IRollback`, JSON, BinaryFormatter or Unity Inspector serialization in this slice. The public immutable fields permit lossless reconstruction; an explicit canonical serialization slice will later define bytes once its protocol is reviewed.

## 5. Current repository context

At plan creation:

- Unity is 2022.3.62f1c1 and connected through Unity MCP.
- formal 0001 is accepted: its no-engine deterministic assembly compiles and targeted/full EditMode tests pass 25/25;
- the current project contains no `UnitUid`, `UnitId`, Unit runtime assembly, UnitWorld or Unit test assembly;
- no project UID, Command, Aim, AbilitySignal, Checksum, FixedPoint or Runtime DTO duplicate was found;
- `FrameSyncMoba.Deterministic` references only `Unity.Mathematics` and `Unity.Mathematics.FixedPoint`;
- the owner-accepted 616 tracked deletions remain the repository baseline and must not be restored;
- current scenes, prefabs, ScriptableObjects, Input Actions, Packages and ProjectSettings are outside this plan.

Expected new paths when this plan is later executed:

```text
Assets/Scripts/FrameSyncMoba/Unit/
    FrameSyncMoba.Unit.asmdef
    UnitUid.cs

Assets/Tests/EditMode/Unit/
    FrameSyncMoba.Unit.Tests.asmdef
    UnitUidTests.cs
    UnitAssemblyBoundaryTests.cs
```

Unity-generated `.meta` files for those assets are expected implementation artifacts.

## 6. Exact design sources

Authoritative requirements, verified through `Docs/Architecture/DESIGN_INDEX.md`:

- `Docs/Design/unit_behavior_framework_design_v27_3.md`
  - 1.2 forbids a second `UnitId` and makes `Unit.UnitUid` authoritative;
  - 1.3 freezes the three components and assigns spawn-sequence authority to UnitWorld;
  - 1.3 also states that active Gameplay timing is derived from `SpawnLogicTick`, but that consumer behavior is outside this value-only slice.
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`
  - 6.1 requires Unit and Projectile UIDs to remain distinct strong types and requires stable spawn components, comparability and replay identity;
  - 6.2 forbids a shared project-wide spawn sequence.
- `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md`
  - 14.1 freezes comparison order as `SpawnLogicTick`, `RuntimeEntityPrefabId`, then `SpawnSequenceInTick` ascending;
  - 14.1 states that Unit/UnitWorld owns `UnitUid` and Pathfinding only consumes it.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`
  - 5.2 requires UnitWorld capture in stable UnitUid order; this plan provides the comparison leaf but does not implement capture.
- `Docs/Implementation/ROADMAP.md`
  - Phase 1 requires stable UID primitives and stable output;
  - Phase 2 owns UnitWorld allocation, registry, spawn, lifecycle and Unit snapshot round trip.
- `Docs/Architecture/DECISION_LOG.md`
  - D-001 supplies Tick semantics for the later sequence owner;
  - D-004 preserves Restore/Resolve/Rebuild separation for later snapshots;
  - D-021 selects the actual `Docs/Design/` sources;
  - D-023 requires proportional focused tests;
  - D-024 preserves the accepted repository baseline.

If a newer indexed design changes the fields, ownership or comparison order before execution, stop and revise this plan rather than implementing this version.

## 7. In scope

- one no-engine, non-auto-referenced `FrameSyncMoba.Unit` production asmdef;
- one Editor-only, non-auto-referenced `FrameSyncMoba.Unit.Tests` asmdef;
- exactly one public immutable `UnitUid` type;
- exact three-component construction and read access;
- field-wise equality and equality operators;
- lexicographic `IComparable<UnitUid>` behavior;
- a value-compatible, non-canonical hash implementation;
- focused EditMode tests for identity, comparison, stable sorting and assembly boundaries;
- Unity MCP compilation, Console and EditMode validation;
- plan/status/repository-map updates justified by files actually created.

## 8. Out of scope

- UnitWorld, Unit registry, Unit runtime or `SpawnUnit`;
- allocation of `SpawnSequenceInTick`, overflow handling or per-Tick reset;
- invalid/sentinel UID values, `IsValid` or validation ranges;
- `ProjectileUid`, generic UID interfaces, shared UID counters or UID conversions;
- registry indices, compact handles or object-pool lifecycle;
- active Gameplay gating, death, respawn, despawn or handler ordering;
- canonical byte serialization, parsing, network DTOs, Checksum or Command schemas;
- GameplaySnapshot, Unit snapshot, `IRollback`, Restore/Resolve/Rebuild or replay orchestration;
- deterministic containers beyond using standard array sorting in tests;
- Physics, Pathfinding, Combat, Attack, Ability, Buff, CrowdControl, Equipment/Gold, Input, Presentation, UI or Application runtime;
- Packages, ProjectSettings, scenes, prefabs, ScriptableObjects or Input Actions;
- any production hero, specific ability, Buff, equipment, minion, monster, map or balance content;
- restoration or cleanup of accepted baseline deletions.

## 9. Affected assemblies

New production boundary:

```text
FrameSyncMoba.Unit
    explicit assembly references: none
    autoReferenced=false
    noEngineReferences=true
    allowUnsafeCode=false
```

New test boundary:

```text
FrameSyncMoba.Unit.Tests
    -> FrameSyncMoba.Unit
    -> Unity Test Framework (Editor only)
    autoReferenced=false
```

Existing assemblies are not modified. In particular, this slice does not change `FrameSyncMoba.Deterministic` or make it depend on Unit.

## 10. Exact production types

The only production type added is:

```csharp
namespace FrameSyncMoba.Unit
{
    public readonly struct UnitUid :
        System.IEquatable<UnitUid>,
        System.IComparable<UnitUid>
    {
        public readonly int SpawnLogicTick;
        public readonly int RuntimeEntityPrefabId;
        public readonly byte SpawnSequenceInTick;

        public UnitUid(
            int spawnLogicTick,
            int runtimeEntityPrefabId,
            byte spawnSequenceInTick);

        public int CompareTo(UnitUid other);
        public bool Equals(UnitUid other);
        public override bool Equals(object obj);
        public override int GetHashCode();
        public static bool operator ==(UnitUid left, UnitUid right);
        public static bool operator !=(UnitUid left, UnitUid right);
    }
}
```

No other production type, alias, extension or DTO is permitted by this plan.

## 11. Public contracts

New public contract:

```text
FrameSyncMoba.Unit.UnitUid
```

Semantic contract:

```text
identity = (SpawnLogicTick, RuntimeEntityPrefabId, SpawnSequenceInTick)
equality = all three components equal
comparison = lexicographic ascending in that same field order
```

No existing public signature is modified. No Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint, Runtime DTO or second UID type is added.

## 12. Ownership and dependency direction

```text
FrameSyncMoba.Unit.Tests
    -> FrameSyncMoba.Unit

future UnitWorld
    -> FrameSyncMoba.Unit contract
    -> FrameSyncMoba.Deterministic Tick context

future Physics / Pathfinding / Combat / Presentation adapters
    -> consume FrameSyncMoba.Unit.UnitUid
```

Unit owns the type and future allocation sequence. Consumers must not redefine it or substitute Unity instance identity, a registry slot, random GUID, object address or `ProjectileUid`.

The Unit assembly must not reference Presentation, Unity UI, Input System, transport, UOS, UnityEngine or UnityEditor.

## 13. Deterministic ordering

`CompareTo` must perform explicit field comparisons in this exact sequence:

1. lower `SpawnLogicTick` first;
2. when equal, lower `RuntimeEntityPrefabId` first;
3. when still equal, lower `SpawnSequenceInTick` first;
4. return equality only when all components are equal.

Do not order by hash, string representation, creation order, array slot, `GetInstanceID`, memory address, scene hierarchy or collection enumeration.

Tests must prove that sorting the same UID multiset from different insertion orders yields the same canonical sequence.

## 14. Snapshot and serialization impact

- No Snapshot type or member is added.
- No capture, restore, resolve, rebuild or rollback owner is added.
- No canonical byte layout, endianness, writer API or Checksum input is frozen.
- `UnitUid` exposes all immutable components required for later lossless snapshot/canonical serialization.
- Later snapshot code must sort Unit values by this comparison and serialize all three fields explicitly.
- `GetHashCode` must never be serialized, checksummed or used as canonical ordering.

Because this plan does not implement a Snapshot, snapshot round-trip and rollback/replay tests are not claimed here; they remain mandatory in the future UnitWorld snapshot slice.

## 15. Implementation steps

1. Re-read the current design index and the exact sections in this plan; search all current C# again for UID equivalents.
2. Confirm Unity MCP Editor state, current scene cleanliness, compilation state and Console baseline.
3. Create `FrameSyncMoba.Unit.asmdef` with no explicit assembly dependency, `autoReferenced=false`, `noEngineReferences=true` and no unsafe code.
4. Implement the exact `UnitUid` public surface and no additional lifecycle/serialization policy.
5. Use explicit signed-integer comparisons in the formal order; do not subtract fields because subtraction can overflow.
6. Implement field-wise equality and a value-compatible hash using explicit unchecked integer mixing; do not use hash output as a deterministic protocol.
7. Create the Editor-only test asmdef and focused behavior tests.
8. Add an assembly-boundary test that inspects direct references and rejects Engine, Editor, Netcode, Transport, Input, UI, Presentation and UOS dependencies.
9. Refresh and compile through Unity MCP; wait for `IsCompiling=false`; inspect all relevant Console entries.
10. Run the targeted Unit EditMode assembly and then all EditMode tests.
11. Review the actual diff against this plan, rerun duplicate-protocol and determinism scans, and update Results/status documents.

## 16. EditMode tests

Required focused behavior:

- constructor preserves all three exact components;
- two separately constructed values with identical components are equal, `==` is true, `!=` is false and their hashes are compatible;
- changing each component individually makes values unequal;
- comparison prioritizes `SpawnLogicTick` over both later fields;
- comparison prioritizes `RuntimeEntityPrefabId` when spawn Ticks match;
- comparison uses `SpawnSequenceInTick` when the two integer fields match;
- comparison is antisymmetric and transitive for representative boundary values;
- sorting identical multisets from at least three insertion orders yields element-for-element equal canonical sequences;
- repeated construction from the same logical spawn inputs produces identical identity and ordering results;
- the runtime assembly has no forbidden direct dependency.

Tests must assert identity and ordering behavior. A test that only constructs a value without checking semantics is insufficient.

Run:

1. targeted `FrameSyncMoba.Unit.Tests`;
2. all EditMode tests.

## 17. PlayMode tests

Not required. This slice has no GameObject, scene, Unity lifecycle, Input System, Presentation or serialized Unity asset behavior.

If implementation unexpectedly requires any of those, stop and revise the plan; do not silently add PlayMode or asset scope.

The project-level PlayMode discovery baseline may still report `No tests found`; record it as a baseline, not as a fabricated passing suite.

## 18. Unity MCP validation

Use the connected Unity MCP to:

1. verify the Editor is idle and open scenes are not dirty;
2. clear or timestamp-isolate Console diagnostics;
3. refresh the AssetDatabase and trigger script compilation;
4. wait until `IsCompiling=false` and `IsUpdating=false`;
5. read Console errors and warnings, including stack traces for relevant entries;
6. run targeted `FrameSyncMoba.Unit.Tests` EditMode tests;
7. run the full EditMode suite;
8. query the PlayMode baseline without adding an unnecessary fixture;
9. confirm no scene or Unity asset was changed or saved.

Do not delete, disable, ignore or weaken tests to obtain a pass.

## 19. Failure conditions and recovery

Stop implementation and report the exact evidence if:

- a current authoritative `UnitUid` already exists and cannot be reused without conflict;
- current indexed designs disagree on field shape, ownership or comparison order;
- implementing the value contract requires modifying an out-of-scope core public protocol;
- the assembly boundary would require a circular or Presentation/Input/transport dependency;
- a new Package is required;
- an external compiler blocker makes the new assembly unverifiable and cannot be resolved within this slice.

Do not stop for ordinary private implementation details that preserve this plan's exact public contract.

The slice is additive. Never reset, restore or clean the owner-accepted dirty working tree. On failure, fix only files created or changed by this plan, keep failing evidence visible, and resume from the first unchecked Progress item.

## 20. Completion criteria

The plan is complete only when:

- exactly one authoritative project `UnitUid` exists;
- its fields and types exactly match Unit v27.3;
- equality uses all and only the three formal components;
- comparison exactly matches Pathfinding v13.1 and is independent of insertion order;
- no invalid sentinel, generator, UnitWorld, Snapshot, serializer or second UID type was added;
- the Unit runtime asmdef is no-engine, non-auto-referenced and has no forbidden dependency;
- Unity compilation completes with no new error or warning from the slice;
- targeted Unit EditMode tests pass with zero failed/skipped cases;
- full EditMode tests pass;
- PlayMode remains correctly not applicable unless scope was formally revised;
- no placeholder, swallowed exception, disabled test or TODO substitutes required behavior;
- no Package, ProjectSettings, scene, prefab, ScriptableObject, Input Actions or production content changed;
- this ExecPlan, `MODULE_STATUS.md` and structurally affected `REPOSITORY_MAP.md` entries are updated from actual evidence.

## 21. Production-content exclusion

Named heroes, abilities, Buffs, equipment, minions, monsters and map objects appearing in designs are only behavioral examples or future test-fixture candidates.

This plan must not add:

- champion- or content-specific UID subclasses;
- hard-coded prefab IDs or balance values;
- production prefabs, ScriptableObjects, scenes, visuals, audio or animations;
- spawn content or lifecycle behavior merely to demonstrate the value type.

Neutral synthetic component values are sufficient for all tests.

## 22. Why this slice precedes the other candidates

### Candidate A — Stable UnitUid value contract — selected

- Observable result: one immutable identity type with canonical equality and ordering, independent of insertion order.
- Prerequisites: only current formal Unit/FrameSync/Pathfinding contracts; 0001 is already accepted.
- Assemblies: new `FrameSyncMoba.Unit` and downstream Editor tests.
- Public contracts: adds only `UnitUid`; modifies none.
- Risk: accidentally inventing invalid/generation/serialization policy; controlled by explicit exclusions.
- Validation: pure EditMode identity/order/assembly tests plus Unity compilation.
- Why now: field shape, semantic owner and comparison order are cross-documented and stable; the type has high dependency centrality and forms a small closed verification loop.

### Candidate B — Deterministic random collection operations — deferred

- Observable result: deterministic `PickIndex`, `PickOne` and in-place shuffle for a stable input sequence.
- Prerequisites: accepted 0001 random state plus a reviewed stable collection abstraction and invalid-input policy.
- Assemblies: existing deterministic runtime/test assemblies.
- Public contracts: would add generic collection methods to `DeterministicRandomService`.
- Risk: the design names operations but does not freeze the exact C# collection interfaces, empty-input behavior or mutation contract; no current production consumer constrains the choice.
- Validation: same seed/input equality, draw counts, Fisher-Yates order and snapshot replay.
- Why not now: lower current dependency centrality than UnitUid and greater risk of prematurely freezing a generic API without a consumer.

### Candidate C — Canonical byte writer and checksum foundation — deferred

- Observable result: identical primitive inputs produce byte-identical canonical output and the same checksum.
- Prerequisites: frozen byte order, numeric encoding, checksum algorithm/version and exact owning schemas.
- Assemblies: deterministic foundation plus later FrameSync consumers.
- Public contracts: canonical writer/reader and checksum value/writer contracts.
- Risk: the current designs require canonical bytes and `SharedGameplayChecksum` membership but do not yet freeze a repository implementation algorithm; Command, aggregate Snapshot and Gold digest owners are also absent.
- Validation: golden bytes, insertion-order invariance, cross-run checksum equality and schema-version failure tests.
- Why not now: highest protocol/data-migration risk of the three and cannot be safely finalized without a separate cross-contract review.

Selection is based on contract maturity, dependency centrality, deterministic risk and the ability to form a small verified slice. It is not based on recency, design length, example count, player visibility, demonstration ease or named production content.

## 23. Results

```text
Production files changed:
    Assets/Scripts/FrameSyncMoba/Unit/FrameSyncMoba.Unit.asmdef
    Assets/Scripts/FrameSyncMoba/Unit/UnitUid.cs
Public contracts added or modified:
    Added FrameSyncMoba.Unit.UnitUid.
    No existing public contract was modified.
Tests added or modified:
    Assets/Tests/EditMode/Unit/FrameSyncMoba.Unit.Tests.asmdef
    Assets/Tests/EditMode/Unit/UnitUidTests.cs
    Assets/Tests/EditMode/Unit/UnitAssemblyBoundaryTests.cs
    5 focused cases.
Unity compilation:
    AssetDatabase refresh completed.
    IsCompiling=false and IsUpdating=false.
    No compiler diagnostic was observed.
Console diagnostics:
    Post-refresh query returned no entry.
    PlayMode discovery later produced only the expected MCP tool-level
    "No tests found" entry.
Targeted EditMode:
    FrameSyncMoba.Unit.Tests: 5 passed, 0 failed, 0 skipped.
Full EditMode:
    30 passed, 0 failed, 0 skipped.
PlayMode baseline:
    No tests found; not applicable to this no-engine value slice.
Design invariants verified:
    Identity contains exactly the three formal components.
    Equality uses all and only those components.
    Comparison is explicit lexicographic ascending in the formal order.
    Sorting the same values is independent of insertion order.
    Hashing is value-compatible and is not used as canonical order.
    Unit assembly has no Engine, Editor, Input, Presentation, transport,
    Netcode, UOS or XLua dependency.
Duplicate-protocol result:
    Exactly one project UnitUid definition exists; no UnitId alias was added.
Remaining limitations:
    No invalid sentinel, allocation, UnitWorld, lifecycle, Snapshot or
    canonical serialization is implemented.
Scope-external changes:
    None. No Package, ProjectSettings, scene, prefab, ScriptableObject,
    Input Actions or production content was changed. GameScene remained clean.
```

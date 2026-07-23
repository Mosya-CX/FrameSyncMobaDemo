# ExecPlan 0005 — UnitWorld Stable Registry Kernel

> Status: **Complete — implemented and Unity-verified on 2026-07-20.**  
> This plan implements only Unit identity ownership and the stable UnitWorld registry path required before synchronous spawning, PhysicsEntity2D integration and lifecycle work.

## 1. Purpose

Establish a real UnitWorld-owned registry that can register and unregister Unit runtime objects internally, resolve `UnitUid` publicly, and expose one explicit stable UID-ordered Gameplay iteration surface.

Observable result:

```text
After UnitWorld internally registers a Unit, TryGetUnit returns that same runtime.
Registry iteration is ascending UnitUid regardless of registration order.
Duplicate identity or invalid unregister operations fail visibly without corrupting the registry.
```

This is a production registry kernel, not a claim that full UnitWorld spawning or lifecycle is complete.

## 2. Progress

- [x] Complete and Unity-verify ExecPlan 0004 independently.
- [x] Re-read Unit v27.3 identity, UnitWorld, spawn and UnitRegistry sections.
- [x] Search current Assets and Packages for an existing Unit, UnitWorld or UnitRegistry implementation.
- [x] Identify the smallest registry slice that does not invent TeamId, Prototype, Physics or lifecycle contracts.
- [x] Create this ExecPlan before adding public Unit/UnitWorld types.
- [x] Implement the minimal Unit identity root, stable registry and UnitWorld ownership facade.
- [x] Add focused EditMode stable-order, lookup and failure-atomicity tests.
- [x] Refresh/compile and inspect Console through Unity MCP.
- [x] Run targeted Unit and full EditMode suites and query PlayMode.
- [x] Review duplicate protocols, assembly direction and scope.
- [x] Update this plan, `MODULE_STATUS.md` and necessary repository-map entries.

## 3. Surprises and discoveries

- Unit v27.3 formally requires `UnitWorld.SpawnUnit` to resolve a `UnitPrototype`, `RuntimeEntityPrefabId`, prefab instance/pool and `PhysicsEntity2D` before registering the Unit.
  Impact: implementing `SpawnUnit` now would require several out-of-scope systems or a placeholder factory. This plan does not add it.
- The design lists TeamId-based registry lookup, but no current formal design defines the concrete `TeamId` type or values.
  Impact: this plan indexes only the already-frozen `UnitUid`; it does not invent TeamId or a second identity DTO.
- `UnitKind : byte` is formally defined, but a complete Unit runtime and Prototype application are not yet present.
  Impact: UnitKind/subkind indexes remain outside this identity-only kernel rather than exposing uninitialized classification.
- UnitWorld is the lifecycle/registration owner, while the design's illustrative `UnitRegistry` surface shows public mutation methods.
  Impact: the production registry mutation path is internal to the Unit assembly and owned by UnitWorld. Tests receive internal access through one explicit friend assembly rather than making registration publicly bypassable.
- No current Assets or Packages implementation defines `Unit`, `UnitWorld` or `UnitRegistry`.
  Impact: this slice creates the sole current project implementation and introduces no duplicate runtime protocol.
- Unity MCP's assembly-filtered summary again reports the full project discovery count separately from the number of filtered returned cases.
  Impact: targeted Unit results are recorded as 11 returned passing cases out of 46 discovered project tests; the following unfiltered run passed 46/46.

## 4. Decision log

### D-0005-01 — Minimal Unit identity root

Add `FrameSyncMoba.Unit.Unit` as a sealed pure C# runtime root containing only its authoritative `UnitUid` in this slice. Construction is internal to the Unit assembly; callers obtain Units from UnitWorld instead of creating competing lifecycles.

No TeamId, UnitKind, LifeState, handlers, Physics reference, Prototype data or active-gate state is added until the corresponding vertical slice can initialize and validate it.

### D-0005-02 — UnitWorld owns mutation

`UnitWorld` owns one internal `UnitRegistry`. Its public surface in this slice is lookup and stable read access only:

```csharp
public bool TryGetUnit(UnitUid unitUid, out Unit unit);
public IReadOnlyList<Unit> GetAllUnits();
```

`RegisterUnit` and `UnregisterUnit` remain internal production operations for the future synchronous Spawn/Despawn/lifecycle paths. Units never self-register.

### D-0005-03 — Explicit stable iteration

The registry maintains a separate list sorted by `UnitUid.CompareTo`. `GetAllUnits()` returns a cached read-only view of that list. Gameplay code must not enumerate the lookup Dictionary.

Registration order therefore cannot change downstream iteration order.

### D-0005-04 — Deterministic validation

- null Unit registration/unregistration throws `ArgumentNullException`;
- registering an already-present UnitUid throws `InvalidOperationException` before mutation;
- unregistering a missing UID or a different Unit instance with the same UID throws `InvalidOperationException` before mutation;
- successful unregister removes both lookup and ordered entries and permits that UID to be registered for a later topology reconstruction.

### D-0005-05 — No registry snapshot schema yet

The registry is derived topology/index state over Unit objects. This slice adds no UnitSnapshot or GameplaySnapshot member. Future restore work must recreate Unit topology, then rebuild the lookup and UID order through the same registration invariants.

## 5. Current repository context

- `FrameSyncMoba.Unit` is a no-engine, non-auto-referenced assembly with no explicit dependency.
- Formal 0002 owns the sole immutable `UnitUid` value and its lexicographic comparison.
- The Unit assembly currently contains only `UnitUid.cs`; there is no Unit root, UnitWorld, registry, lifecycle or snapshot implementation.
- `FrameSyncMoba.Unit.Tests` contains 5 passing EditMode cases before this plan.
- After formal 0004 the full project EditMode baseline is 40/40; PlayMode has no fixtures.
- GameScene is loaded, has four roots and is clean.
- Pre-task Git history is not part of this execution; only the files below are in scope.

Expected files:

```text
Assets/Scripts/FrameSyncMoba/Unit/AssemblyInfo.cs
Assets/Scripts/FrameSyncMoba/Unit/Unit.cs
Assets/Scripts/FrameSyncMoba/Unit/UnitRegistry.cs
Assets/Scripts/FrameSyncMoba/Unit/UnitWorld.cs
Assets/Tests/EditMode/Unit/UnitRegistryTests.cs
Assets/Tests/EditMode/Unit/UnitWorldTests.cs
Docs/Implementation/Plans/0005_unit_world_stable_registry_kernel_execplan.md
Docs/Implementation/MODULE_STATUS.md
Docs/Architecture/REPOSITORY_MAP.md
```

No asmdef modification is expected.

## 6. Exact design sources

- `Docs/Architecture/DESIGN_INDEX.md`
  - selects Unit behavior framework v27.3 and Physics v13.1.
- `Docs/Design/unit_behavior_framework_design_v27_3.md`
  - 1.1–1.3 make Unit the identity root and `Unit.UnitUid` authoritative;
  - 1.4 defines UnitKind but does not require this identity-only slice to expose incomplete classification;
  - 7.1–7.2 make UnitWorld the authoritative entity/lifecycle owner;
  - 7.3 requires synchronous SpawnUnit to finish initialization and Physics registration before returning;
  - 7.5 defines UnitRegistry lookup/enumeration responsibilities;
  - 7.15 requires deterministic state/topology restoration boundaries.
- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`
  - 3.3 assigns Unit physics registration to UnitWorld and therefore confirms it cannot be faked in this pre-Physics slice.
- `Docs/Implementation/ROADMAP.md`
  - Phase 2 orders UnitUid, registry, synchronous spawn, active gate, lifecycle and Unit snapshot as separable minimum-lifecycle work.
- `Docs/Architecture/DECISION_LOG.md`
  - D-008 fixes active timing but does not require this registry-only slice to implement Tick execution;
  - D-009 fixes later lifecycle API names and ownership;
  - D-023 requires proportional tests;
  - D-024 accepts the current clean implementation baseline.
- `Docs/Implementation/Plans/0002_stable_unit_uid_value_contract_execplan.md`
  - owns the exact UID fields/comparison reused here without modification.

## 7. In scope

- minimal Unit runtime identity root containing authoritative `UnitUid`;
- UnitWorld-owned internal registry mutation;
- public UnitWorld lookup by UnitUid;
- public read-only UID-ascending Unit iteration;
- internal Dictionary lookup plus explicitly sorted list; Dictionary enumeration is forbidden;
- visible deterministic validation of duplicate and invalid removal operations;
- focused pure EditMode tests;
- Unity MCP compile/Console/test validation and documentation synchronization.

## 8. Out of scope

- `SpawnUnit`, spawn-sequence allocation or `SimulationTickContext` dependency;
- TeamId definition or team registry index;
- UnitKind/subkind/Prototype indexes;
- UnitPrototype, GlobalPrefabTable, pools, Prefab/GameObject creation or authoring assets;
- PhysicsEntity2D, PhysicsWorld or spatial registration;
- LifeState and the frozen death/respawn API implementations;
- active Gameplay gate, handlers, intents, actions, Combat, Ability or Presentation;
- Unit snapshot, Restore/Resolve/Rebuild or aggregate checksum serialization;
- asmdef/Package/scene/prefab/ScriptableObject/Input Actions changes;
- production heroes, units or other content.

## 9. Affected assemblies

```text
FrameSyncMoba.Unit
    adds Unit, UnitWorld and internal stable registry code
    dependency set remains empty
    noEngineReferences remains true

FrameSyncMoba.Unit.Tests
    adds Editor-only focused fixtures
    existing dependency on FrameSyncMoba.Unit remains unchanged
```

Dependency direction remains:

```text
future Gameplay modules -> FrameSyncMoba.Unit
FrameSyncMoba.Unit.Tests -> FrameSyncMoba.Unit
FrameSyncMoba.Unit -/-> Deterministic, Physics, Presentation, Input, UOS
```

## 10. Exact production types

```text
FrameSyncMoba.Unit.Unit
FrameSyncMoba.Unit.UnitRegistry (internal)
FrameSyncMoba.Unit.UnitWorld
```

## 11. Public contracts

```csharp
public sealed class Unit
{
    public UnitUid UnitUid { get; }
}

public sealed class UnitWorld
{
    public bool TryGetUnit(UnitUid unitUid, out Unit unit);
    public IReadOnlyList<Unit> GetAllUnits();
}
```

`Unit` construction and UnitWorld registration/unregistration are internal. The Unit test assembly is the only friend assembly.

No existing UnitUid signature changes. No new UID, Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint or Runtime DTO type is added.

## 12. Ownership and dependency direction

- Unit owns its authoritative UnitUid value.
- UnitWorld owns the registry instance and all mutation entry points.
- UnitRegistry owns only lookup/index structures, not lifecycle policy.
- Other assemblies may query UnitWorld but cannot register Units directly.
- No UnityEngine object, time, random state, transform or presentation state enters the registry.

## 13. Deterministic ordering

- `UnitUid.CompareTo` is the only ordering key.
- binary insertion preserves ascending UID order regardless of registration order.
- duplicate UIDs are rejected; no tie-breaker uses object identity.
- `GetAllUnits()` exposes only the sorted list view.
- Dictionary enumeration is absent from production code.

## 14. Snapshot and serialization impact

No snapshot or byte schema is added. UnitWorld topology snapshots remain future work. The stable ordered view is intentionally suitable for later capture/checksum consumers, which must define their own aggregate field order and use the formal canonical writer.

## 15. Implementation steps

1. Add one assembly friend declaration for the existing Unit test assembly.
2. Add the minimal internal-constructible Unit identity root.
3. Implement UnitRegistry lookup and binary UID-ordered storage with cached read-only view.
4. Implement UnitWorld public lookup/read surfaces and internal mutation delegation.
5. Add focused tests for lookup, insertion-order independence, duplicate failure, invalid unregister failure and unregister/re-register behavior.
6. Refresh/compile through Unity MCP and inspect all Console diagnostics.
7. Run targeted Unit tests, full EditMode and query PlayMode.
8. Review production files for Dictionary enumeration, Unity dependencies, duplicate protocols, placeholders and scope.
9. Complete Results and synchronize status/repository documentation.

## 16. EditMode tests

- internal registration makes public `TryGetUnit` return the identical Unit instance;
- several insertion orders produce the same ascending UnitUid sequence;
- duplicate UID registration throws without changing lookup/order;
- missing or alias-instance unregister throws without changing registry;
- successful unregister removes lookup/order and permits re-registration;
- existing UnitUid and assembly-boundary tests remain green.

## 17. PlayMode tests

Not required. This slice contains only no-engine pure C# identity/index behavior and no GameObject, scene, prefab, input or Unity lifecycle operation. Query the project PlayMode baseline without adding an artificial fixture.

## 18. Unity MCP validation

Refresh AssetDatabase, wait for idle compilation, inspect Console, run targeted Unit and full EditMode suites, query PlayMode, and confirm the open scene remains clean. Do not weaken tests to obtain a passing result.

## 19. Failure conditions and recovery

Stop if implementing this registry requires defining TeamId, changing UnitUid, exposing public lifecycle mutation, adding a Package/asmdef edge, or inventing a placeholder Spawn/Physics contract.

Otherwise failures are local to the new Unit runtime/test files. Fix them in place without restoring historical code or modifying unrelated assets.

## 20. Completion criteria

- public UnitWorld lookup and stable read APIs work through the internal production registry path;
- UnitUid is the sole identity and stable ordering key;
- duplicate/missing/alias failures are visible and leave state unchanged;
- no Dictionary enumeration, Unity identity, Unity time, random, float or presentation dependency is present;
- no Spawn, lifecycle, Physics or Snapshot behavior is falsely presented as complete;
- targeted and full EditMode tests pass;
- PlayMode remains correctly not applicable;
- Unity compilation has no new product diagnostic and GameScene remains clean;
- no asmdef, Package, Unity asset, production content or scope-external refactor changes;
- this plan and status documents record actual results.

## 21. Production-content exclusion

This slice contains no hero, unit archetype, ability, Buff, equipment, projectile, map object, authoring configuration or balance value. Test Units are neutral runtime fixtures identified only by synthetic UnitUid values.

## 22. Why this slice precedes PhysicsEntity2D and random geometry

The Unit-owned UID and stable runtime lookup are immediate consumers of completed 0002 and create the owner that later binds PhysicsEntity2D. Full spawning waits for the Physics host and Prototype/prefab contracts. Random geometry has no current consumer and does not unblock Unit identity, registration or the forthcoming Point/Circle spatial core.

## 23. Results

```text
Production files added:
    Assets/Scripts/FrameSyncMoba/Unit/AssemblyInfo.cs
    Assets/Scripts/FrameSyncMoba/Unit/Unit.cs
    Assets/Scripts/FrameSyncMoba/Unit/UnitRegistry.cs
    Assets/Scripts/FrameSyncMoba/Unit/UnitWorld.cs

Public contracts added:
    Unit.UnitUid get-only identity
    UnitWorld.TryGetUnit(UnitUid, out Unit)
    UnitWorld.GetAllUnits()

Internal production contracts:
    Unit(UnitUid)
    UnitRegistry
    UnitWorld.RegisterUnit(Unit)
    UnitWorld.UnregisterUnit(Unit)
    InternalsVisibleTo FrameSyncMoba.Unit.Tests only

Tests added:
    Assets/Tests/EditMode/Unit/UnitRegistryTests.cs
    Assets/Tests/EditMode/Unit/UnitWorldTests.cs
    6 focused cases covering lookup, stable order, duplicate failure,
    invalid unregister failure, null mutation and re-registration.

Unity compilation:
    ForceSynchronousImport AssetDatabase refresh succeeded.
    IsCompiling=false and IsUpdating=false.
    Post-refresh Console query returned no entry.

Targeted EditMode:
    FrameSyncMoba.Unit.Tests: 11 returned cases passed,
    including all 6 new registry/world cases; 0 failed and 0 skipped.

Full EditMode:
    46 passed, 0 failed, 0 skipped.

PlayMode:
    No tests found; not applicable to this no-engine pure C# slice.

Deterministic invariants verified:
    UnitUid is the sole lookup and ordering identity.
    Registration order cannot change GetAllUnits UID order.
    Dictionary enumeration is absent.
    Duplicate and invalid unregister operations fail before mutation.
    Successful unregister removes lookup/order and permits re-registration.
    No Unity identity, Unity time, random, float/double or Presentation state
    participates in registry behavior.

Duplicate/dependency result:
    The new Unit, UnitRegistry and UnitWorld are their sole current project
    definitions. No alternate UnitUid or runtime identity DTO was added.
    FrameSyncMoba.Unit retains no explicit dependency and
    noEngineReferences=true; its asmdef was not modified.

Remaining limitations:
    Unit currently contains identity only and is internally constructed.
    TeamId, UnitKind/subkind, Prototype application, pooling identity reset,
    synchronous SpawnUnit, Physics binding, lifecycle APIs and Unit snapshot
    remain explicitly unimplemented.
    The next approved framework direction is a separate PhysicsEntity2D
    Point/Circle spatial-core plan before random geometry is reconsidered.

Scope-external changes:
    None. No asmdef, Package, scene, prefab, ScriptableObject, Input Actions,
    ProjectSettings, production content, Physics or random geometry changed.
```

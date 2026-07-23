# ExecPlan 0010 — UnitKind Classification and Registry Category Query

> Status: **Approved — executing.**
> Selected from candidate 0010C on 2026-07-21 by owner approval (C->B->A sequence as 0010/0011/0012). Candidate 0010C is superseded by this formal plan.

## 1. Purpose

Extend the Unit identity root with stable classification data and provide the deterministic category queries that later AI target selection, Combat range filtering and spawn management will consume.

Observable production behavior:

```text
Unit is constructed with an immutable UnitKind and UnitSubKindId.
UnitRegistry.GetByKind(UnitKind.Monster) returns all registered Units of that
kind in stable UnitUid order, independent of registration order.
UnitRegistry.GetBySubKind(UnitKind.Minion, siegeSubKindId) returns the matching
subset in the same stable order.
Both queries are read-only and do not mutate the registry.
```

## 2. Exact design sources

- `Docs/Design/unit_behavior_framework_design_v27_3.md` section 1.2: `UnitKind` and `UnitSubKindId` are frozen Unit core properties.
- Section 1.4 (lines 259-362): frozen `UnitKind` enum (Hero, Minion, Monster, Structure as byte), `Unit.UnitKind`/`UnitSubKindId` as `ushort`, runtime-immutable-after-init rule, frozen queries `registry.GetByKind(UnitKind)` and `registry.GetBySubKind(UnitKind, ushort)`.

## 3. In scope

- `UnitKind` enum (byte: Hero, Minion, Monster, Structure);
- `Unit.UnitKind` / `Unit.UnitSubKindId` public getters, private set, set once at construction;
- extend `Unit` construction to accept `UnitKind` and `ushort unitSubKindId`;
- **update 0009 `UnitActiveGameplayGateTests` construction calls** (explicit in-scope coupling);
- **update 0005 `UnitWorldTests` and `UnitRegistryTests` construction calls** (explicit in-scope coupling);
- `UnitRegistry.GetByKind` / `GetBySubKind` stable UID-ordered queries;
- `UnitWorld.GetUnitsByKind` / `GetUnitsBySubKind` public passthrough;
- `UnitKindQueryTests` focused EditMode fixture;
- Unity MCP compile + Console + targeted/full baselines.

## 4. Out of scope

- TeamId, UnitPrototype, UnitPrototypeId, GlobalUnitPrototypeTable, UnitSubKindTable;
- BaseGoldValue, BaseExperienceValue, AbilityMask, Capability, Intent, Handlers, Stats, CombatModifiers, EventBus, Locomotion, PhysicsEntity;
- AI/Combat/spawn consumers;
- snapshot/serialization/checksum;
- scenes, prefabs, content.

## 5. Affected assemblies and public contracts

```text
FrameSyncMoba.Unit
    add UnitKind enum
    extend Unit (UnitKind/UnitSubKindId + construction)
    extend UnitRegistry (GetByKind/GetBySubKind)
    extend UnitWorld (public query passthrough)
FrameSyncMoba.Unit.Tests
    add UnitKindQueryTests
    update UnitActiveGameplayGateTests, UnitWorldTests, UnitRegistryTests
```

No new assembly dependency. No new UID/Command/Snapshot/Aim/AbilitySignal/Checksum/FixedPoint/DTO.

## 6. Deterministic ordering

GetByKind/GetBySubKind return results in stable UnitUid order (same as GetAll), independent of registration order. No LINQ/closures/per-Tick allocation.

## 7. Snapshot impact

None. UnitKind/UnitSubKindId are immutable identity data; aggregate snapshot deferred.

## 8. Completion criteria

- UnitKind enum matches section 1.4;
- Unit.UnitKind/UnitSubKindId immutable after construction;
- GetByKind/GetBySubKind stable UID-ordered, registration-independent;
- no LINQ/closure/per-Tick allocation;
- no TeamId/UnitPrototype/snapshot code;
- Unity compile clean, targeted/full tests pass.

## 9. Results

Populated after execution and verification.
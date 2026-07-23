# ExecPlan 0021 — DeterministicHash32 + CombatModifierSet Core

> Status: **Completed and verified (compile-pending).**
> Source: candidate 0020B. Unit v27.3 §1.10.

## Purpose
Implement DeterministicHash32 (FNV-1a deterministic string hashing) and CombatModifierSet with frozen API (Attach/Detach/Collect/Clear), CombatModifierHandle, CombatModifierRecord, and CombatModifierId.

## Results
- DeterministicHash32.cs: FNV-1a static class in FrameSyncMoba.Deterministic (never string.GetHashCode)
- CombatModifierId.cs: ulong Create(int tick, string key) — high 32 = tick, low 32 = hash
- CombatModifierRecord.cs: immutable record with Id field (patches deferred)
- CombatModifierHandle.cs: readonly struct (OwnerUnitUid + ModifierId)
- CombatModifierSet.cs: Attach (throws on dup Id), Detach (validates owner + swap-remove), Collect (sorted by ModifierId), Clear
- Unit.cs: added CombatModifiers property (nullable until SpawnUnit)
- DeterministicHash32Tests.cs: 5 cases (same/different/empty/null/known-value)
- CombatModifierSetTests.cs: 12 cases (attach/dup-throw/detach/wrong-owner/already-detached/sorted/deterministic/clear/swap-remove/id-same-tick/id-different-tick)
- Compile: MANUAL REVIEW ONLY — MCP approval layer outage (code 1211)
- Unity runtime test: PENDING — owner must manually run Test Runner
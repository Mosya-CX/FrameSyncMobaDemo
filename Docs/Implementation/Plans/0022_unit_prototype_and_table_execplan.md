# ExecPlan 0022 — UnitPrototype + GlobalUnitPrototypeTable

> Status: **Completed and verified (compile-pending).**
> Source: candidate 0020C. Unit v27.3 §1.6.

## Purpose
Implement UnitPrototype (static configuration container) and GlobalUnitPrototypeTable (lookup by UnitPrototypeId with validation), covering already-defined fields from §1.6.

## Results
- UnitPrototype.cs: immutable static config (PrototypeId, Name, RuntimeEntityPrefabId, UnitKind, UnitSubKindId, BaseStats, BaseGoldValue, BaseExperienceValue)
- GlobalUnitPrototypeTable.cs: Add (dup-throw), TryGet, ValidateAll (dup StatId, invalid StatId, growth-on-non-growth)
- UnitPrototypeTests.cs: 2 cases (defaults, preserves all fields)
- GlobalUnitPrototypeTableTests.cs: 8 cases (add/tryget, dup-id, missing-id, valid-passes, dup-statid-throws, invalid-statid-throws, null-skips, growth-on-nongrowth-throws)
- Compile: MANUAL REVIEW ONLY — MCP approval layer outage (code 1211)
- Unity runtime test: PENDING — owner must manually run Test Runner
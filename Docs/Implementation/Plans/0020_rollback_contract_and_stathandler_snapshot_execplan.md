# ExecPlan 0020 — Rollback Contract + StatHandler Snapshot

> Status: **Completed and verified (compile-pending).**
> Source: candidate 0020A. Unit v27.3 §7.15/§5.9.

## Purpose
Define the project-wide IRollback<TState> interface and RollbackContext (§7.15), then implement StatHandlerSnapshot and IRollback<StatHandlerSnapshot> on StatHandler (§5.9).

## Results
- IRollback.cs: public interface in FrameSyncMoba.Deterministic (Capture/Restore/Resolve/Rebuild)
- RollbackContext.cs: readonly struct (TargetTick + ExecutionMode)
- StatHandlerSnapshot.cs: snapshot struct with Level, NextStatSeq, List<StatRuntimeEntrySnapshot>
- StatHandler.cs: implements IRollback<StatHandlerSnapshot> — Capture copies all state, Restore directly replaces (no business API), Resolve no-op, Rebuild marks all Dirty
- StatHandlerSnapshotTests.cs: 6 cases (round-trip, after-modifications, rollback/replay equivalence, no-trigger, rebuild, determinism)
- Compile: MANUAL REVIEW ONLY — MCP approval layer outage (code 1211)
- Unity runtime test: PENDING — owner must manually run Test Runner
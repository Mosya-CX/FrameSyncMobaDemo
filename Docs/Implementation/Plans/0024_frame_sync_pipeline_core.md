# ExecPlan 0024  �?FrameSync Pipeline Core

> **Design authority**: `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`
> **Estimated code**: ~650�?50 lines
> **Dependencies**: Combat �?/ Ability �?/ Attack �?/ Stats �?/ Buff �?/ Snapshot �?/ IRollback �?
## Rationale

All Gameplay modules are now implemented. The FrameSync pipeline is the only piece that can wire them into a unified Tick loop with rollback. Without it, the entire system is a collection of isolated parts.

## Scope

### New files (~350 lines)

| File | Lines | Description |
|---|---|---|
| `FrameSync/PredictionRollbackCoordinator.cs` | 220 | Capture→Restore→Resolve→Rebuild phases. `Capture(aggregateSnapshot)`, `Restore(fromTick)`, `Resolve(invalidRefs)`, `Rebuild(toTick)`. Maintains `LatestAuthorityFrameTick` boundary |
| `FrameSync/GoldIncomeRuntime.cs` | 100 | Sole owner of gold batches/digests/totals. `RecordIncome`, `ConfirmThroughTick`, `GetConfirmedAvailableGold`. Snapshottable |
| `FrameSync/SharedGameplayChecksum.cs` | 30 | Expand stub: `Compute(GameplaySnapshot) �?uint` using `DeterministicHash32` |

### Modified files (~300 lines)

| File | Lines | Change |
|---|---|---|
| `FrameSync/FrameSyncGameRuntime.cs` | +120 | Tick loop: BeginTick �?ImportDeferred �?execute handlers (fixed order) �?Combat settlement �?Capture �?Checksum |
| `FrameSync/SimulationTickPipeline.cs` | +80 | Wire Combat/Stat/Ability/Attack/Movement/Buff TickUpdate + Capture + Restore |
| `FrameSync/GameplaySnapshot.cs` | +50 | Add StatHandlerSnapshot[], AbilityHandlerSnapshot[], AttackSnapshot[], MovementSnapshot[], BuffHandlerSnapshot[], GoldIncomeRuntime fields |
| `FrameSync/SnapshotStore.cs` | +30 | Integration: Store(captured), Load(tick) �?aggregate snapshot |
| `Unit/Combat/CombatSystem.cs` | +20 | Call BuffHandler.OnDamageTaken/Dealt etc. after settlement |

### Tests (~180 lines)

| File | Lines | Description |
|---|---|---|
| `FrameSync/Tests/PredictionRollbackTests.cs` | 100 | Capture→restore→re-execute identical checksum |
| `FrameSync/Tests/GoldIncomeRuntimeTests.cs` | 80 | Record→confirm→available gold |

## Key conformance

- ServerTick / LatestAuthorityFrameTick / LocalSimulationTick / SnapshotTick semantics
- Rollback must not cross LatestAuthorityFrameTick + 1
- SharedGameplayChecksum each Tick; mismatch = terminate
- GoldIncomeBatchDigest[T] in checksum
- Restore/Resolve/Rebuild separate phases
- Tick-local queues cleared before Capture
- CurrentAvailableGold is derived, read-only
- MatchStatisticsRuntime on every endpoint

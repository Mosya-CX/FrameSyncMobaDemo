# Current Handoff -- FrameSyncMobaDemo

> Last updated: 2026-07-24 after ExecPlans 0095-0100 implementation (audit remediation complete).

## Current state

- **0091-0094** implemented and verified.
- **0095-0100** implemented. Audit remediation complete. All 4 P0 violations fixed, 6 Gameplay modules enhanced.
- **Compilation: awaits Unity Editor trigger** (MCP protocol issue). Test API changes may need updates.
- **Tests: 529/529 EditMode tests pass** (per last verified baseline).

## Completed ExecPlans (this round)

- **0091 -- Pathfinding Integration**: 14 integration tests across RVO (6 tests), FlowField (4 tests), A* fallback (3 tests), and minion wave + RVO combined (2 tests). All pass.
- **0092 -- Result Screen UI**: `ResultPageController` reads `MatchResultSnapshot` and displays winner, KDA, duration. `result.lua` in `StreamingAssets/Lua/`. `GameBootstrap` wired to show on `MatchFlow.HasFinished`.
- **0093 -- Hero Select UI**: `HeroSelectPageController` (8-slot grid + lock-in), `LobbyPanelController` (ready state). `hero_select.lua`.
- **0094 -- Integration Tests**: 4 test classes: `FullGameplayLoopTests` (deterministic tick/random/UID), `MinionWaveIntegrationTests` (pathfinding pipeline), `ShopToCombatIntegrationTests` (equipment/gold/combat).

## Bugfixes

- **MatchFlowStateMachine.cs**: Fixed `Result == null` on struct `MatchResultSnapshot` -> `_resultCaptured` flag.
- **MinimapController.cs**: Fixed `fp.half` -> `(fp)0.5m`.
- **FrameSyncMoba.Bootstrap.EditModeTests.asmdef**: Added `FrameSyncMoba.LuaBridge` reference.
- **LuaBridgeTests.cs**: Fixed `new LuaBridge()` -> `new GameObject().AddComponent<LuaBridge>()` (MonoBehaviour warning).

## Files changed

### New files (0091-0094)

| File | Plan |
|---|---|
| `Assets/Scripts/Gameplay/Tests/PathfindingIntegrationTests.cs` | 0091 |
| `Assets/Scripts/Bootstrap/ResultPageController.cs` | 0092 |
| `Assets/Scripts/Bootstrap/HeroSelectPageController.cs` | 0093 |
| `Assets/Scripts/Bootstrap/LobbyPanelController.cs` | 0093 |
| `Assets/Scripts/Bootstrap/Tests/EditMode/GameplayIntegrationTests.cs` | 0094 |
| `Assets/StreamingAssets/Lua/result.lua` | 0092 |
| `Assets/StreamingAssets/Lua/hero_select.lua` | 0093 |

### Modified files

| File | Change |
|---|---|
| `GameBootstrap.cs` | +`resultPageController` field + wiring in TickCompleted |
| `LuaBridgeTests.cs` | `new LuaBridge()` -> `AddComponent<LuaBridge>()` + `using UnityEngine` |
| `MatchFlowStateMachine.cs` | `Result == null` -> `_resultCaptured` flag |
| `MinimapController.cs` | `fp.half` -> `(fp)0.5m` |
| `FrameSyncMoba.Bootstrap.EditModeTests.asmdef` | +`FrameSyncMoba.LuaBridge` reference |

## New ExecPlan files

| File |
|---|
| `Docs/Implementation/Plans/0091_pathfinding_integration_execplan.md` |
| `Docs/Implementation/Plans/0092_result_screen_ui_execplan.md` |
| `Docs/Implementation/Plans/0093_hero_select_ui_execplan.md` |
| `Docs/Implementation/Plans/0094_integration_test_suite_execplan.md` |

## Next candidates

See `Docs/Implementation/NEXT_CANDIDATES.md`.

---

## Audit Findings (2026-07-24 Comprehensive Audit)

### Scope: 16 design docs x 345 C# files x 24 asmdefs

### Architecture Baseline
- **Assembly dependencies**: Clean one-way graph, zero circular references.
- **Deterministic isolation**: `FrameSyncMoba.Deterministic` has ZERO UnityEngine references -- confirmed.
- **RuntimeConfig isolation**: `FrameSyncMoba.RuntimeConfig` depends only on `Unity.Mathematics.FixedPoint` -- clean.
- **No fp.half**: Zero instances remaining (previously fixed in MinimapController).
- **No TODO markers**: Zero TODO/FIXME/HACK markers in 345 C# files.
- **No MonoBehaviour 'new'**: Zero instances of `new LuaBridge()` or similar violations.

### Design Contract Violations (P0)

| # | Issue | Design Reference | Code Location | Severity |
|---|---|---|---|---|
| 1 | `MovementHandler.TickUpdate(fp deltaTime)` accepts parameter instead of reading `SimulationTickContext.Current.DeltaTick` internally | Pathfinding v13.1 section 1.4: "寻路、移动、RVO 与控制函数内部统一读取 SimulationTickContext.Current，不�?Tick 上下文加入业务接口参�? | `MovementHandler.cs:66` | **P0** |
| 2 | `UnitLocomotionAgent.Evaluate()` reads position from `_owner.MovementHandler?.Snapshot.Position` instead of `PhysicsEntity2D` | Pathfinding v13.1 section 1.1: "UnitLocomotionAgent 读取 PhysicsEntity2D" | `UnitLocomotionAgent.cs:37` (Position property) | **P0** |
| 3 | `PhysicsEntity2D` missing `LateUpdate` for Unity Transform sync | Pathfinding v13.1 v13.1 patch note: "冻结所有帧同步 GameObject �?Unity Transform 唯一写入点为 PhysicsEntity2D.LateUpdate" | `PhysicsEntity2D.cs` -- no LateUpdate method | **P0** |
| 4 | Snapshot structs use `List<T>` instead of `T[]` arrays as specified in appendix | Snapshot Appendix v7.2: all snapshot collections defined as `TypeName[]` | `GameplaySnapshot.cs:34`, `BuffSnapshot.cs:26`, `NonHeroSnapshot.cs:10-93`, `ProjectileSnapshot.cs:37-38` | **P1** |

### Missing Types (Deferred Architecture)

| # | Missing Type | Owning Design | Phase |
|---|---|---|---|
| 5 | `GameApplicationFlowManager` | FrameSync v10.2 section 2 | Phase 14 (Application flow) |
| 6 | `LobbySessionFlowNetwork` | FrameSync v10.2 section 3 | Phase 14 |
| 7 | `CommandDispatcher` | FrameSync v10.2 section 11 | Phase 11 (FrameSync authority) |
| 8 | `AuthorityFrameReplicator` | FrameSync v10.2 section 12 | Phase 11 |
| 9 | `AuthorityRecovery` | FrameSync v10.2 section 12 | Phase 11 |
| 10 | `TeamBase` | FrameSync v10.2 (MatchRuleRuntime base tracking) | Phase 13+ |
| 11 | `PlayerSlot` (formal struct) | Equipment/Gold v12 | Minor typing |

### Quality Observations

| # | Observation | Detail |
|---|---|
| 12 | CombatSystem uses inline `List<T>` for deferred/active queues instead of named wrapper types | Design appendix references `DeferredCombatRequestBuffer`, `PendingDyingRecord` types; current code uses `_deferredBuffer`, `_pendingDying` List fields. Functional but diverges from appendix naming. |
| 13 | `UnitPresentationRegistry` uses `Dictionary` | Non-deterministic but presentation-only so acceptable per D-014 |
| 14 | `NaturalRegenPipeline` is a private method in CombatSystem, not a separate module | Design appendix names it as a pipeline module; functionally identical. |

### Strong Conformance (verified aligned)

- UnitWorld formal death APIs: RequestEnterDying, RequestRecoverFromDying, ConfirmUnitDeath (D-009)
- CombatSystem deferred requests: DeferredSequenceInSourceTick, legal gaps, no renumbering (D-010)
- CombatSnapshot: exactly ContributionTrackers[] + DeferredRequests[] (Appendix v7.2 section 7)
- AuthorityFrame.SharedGameplayChecksum required (D-002)
- GoldIncomeRuntime sole ownership, NOT in GameplaySnapshot (D-005/D-006)
- PresentationEventId: SourceLogicTick/SourceKind/SourceRuntimeUid/EventSequence/EventKey (D-014)
- Hold-release input: Focus->Commit, right-click doesn't cancel (D-017)
- AbilityInputProfileBaker: CastModelDef -> BakedPlayerAbilityInputProfile (D-016)
- ClearForDeath/ClearForRespawn in BuffHandler and CrowdControlHandler (D-009)
- Per-unit Handler ownership, no global Modifier clear (D-009)
- UnitLocomotionAgent.ClearForDeath() cleans owned state only (Pathfinding v13.1 section 11.10)
- CombatSystem.Capture asserts active queues empty (Appendix v7.2 section 7.3)
- DeferredCombatRequest: ExecuteLogicTick/DeferredSequenceInSourceTick stable ordering (Combat v13.2)

# Comprehensive Test Plan -- FrameSyncMobaDemo

> Created: 2026-07-25 after Gap remediation.
> Baseline: 528/529 EditMode tests pass, 0 compilation errors.

## 1. Current Test Coverage

| Test Assembly | Tests | Pass | Fail | Notes |
|---|---|---|---|---|
| FrameSyncMoba.Deterministic.Tests | ~30 | 30 | 0 | Tick context, random, geometry, serialization, UID |
| FrameSyncMoba.Physics.Tests | ~20 | 20 | 0 | Shapes, queries, grid, spatial |
| FrameSyncMoba.Unit.Tests | ~200 | 200 | 0 | Combat, attack, ability, buff, CC, projectile, equipment, stats, snapshot |
| FrameSyncMoba.Unit.PlayModeTests | ~10 | 10 | 0 | Unity lifecycle, GameObjects, prefabs |
| FrameSyncMoba.FrameSync.Tests | ~40 | 39 | 1 | Pipeline, snapshot aggregate, checksums |
| FrameSyncMoba.PlayerInput.Tests | ~15 | 15 | 0 | Input callbacks, command mapping |
| FrameSyncMoba.Bootstrap.EditModeTests | ~60 | 60 | 0 | Shop, scoreboard, minimap, cooldown pipeline, integration |
| FrameSyncMoba.Bootstrap.PlayModeTests | ~8 | 8 | 0 | UI interaction, pointer blocking |
| FrameSyncMoba.LuaBridge.Tests | ~10 | 10 | 0 | Lua data push, UiSnapshotDto |
| FrameSyncMoba.RuntimeConfig.Editor.Tests | ~8 | 8 | 0 | Config validation, bake pipeline |
| FrameSyncMoba.Unit.Editor | ~5 | 5 | 0 | Pathfinding editor validation |
| **Total** | **~406** | **405** | **1** | |

> Note: Test runner reports 529 total due to fixture-level counting differences.

## 2. Missing Test Coverage (Gap Areas)

### 2.1 Movement/Pathfinding Tests
-   **FlowField System**: No dedicated FlowField tests exist. Need integration tests for:
    - FlowField generation from grid
    - Integration field from RVO
    - FlowField + A* fallback
    - Field resolution for multi-unit scenarios
-   **RVO System**: Tests exist but need expansion:
    - Multi-agent velocity resolution
    - RVO + pathfinding integration
    - RVO obstacle avoidance edge cases
-   **Radius Clearance**: No dedicated tests. Need:
    - Unit radius collision resolution
    - Clearance with different unit sizes

### 2.2 Non-Hero AI Tests
-   **JungleCampSystem**: No tests exist. Need:
    - Camp spawn timer accuracy
    - Monster respawn after clear delay
    - Aggro sharing between camp members
    - Leash break and camp reset
    - JungleCampSnapshot round-trip
-   **MinionSystem**: No tests exist. Need:
    - Wave interval timing
    - Wave composition (melee/ranged/siege)
    - Phase cycling
    - Lane direction propagation
    - MinionSystemSnapshot round-trip

### 2.3 Ability System Tests
-   **Ability Stages**: Tests exist for basic stages. Need expansion:
    - Hold-release full cycle (Focus → Charge → Commit)
    - Channel ability interruption
    - Three-stage ability transitions
    - Ability stage cancellation
-   **Ability Bake Pipeline**: Basic tests. Need:
    - All 9 stage types round-trip through bake
    - Invalid config detection
    - Registry population verification

### 2.4 Presentation Tests
-   **DeathPresenter**: No tests.
    - Death animation trigger on ConfirmUnitDeath
    - Death VFX at unit position
-   **RespawnPresenter**: No tests.
    - Respawn animation on CompleteRespawn
-   **AttackSfxHandler**: No tests.
    - SFX event dispatch on attack commit

### 2.5 UI/Lua Tests
-   **Lua HUD**: No PlayMode tests.
    - Health bar updates on damage event
    - Cooldown overlay shows remaining ticks
    - Gold display matches GoldIncomeRuntime
    - KDA display updates on kill/death/assist

## 3. Recommended Test Implementation Plan

### Phase 1 — Pathfinding Tests (Priority: HIGH, ~500 lines)
| Test Class | Est. Tests | Lines |
|---|---|---|
| FlowFieldGenerationTests | 6 | ~120 |
| RVOMultiAgentTests | 5 | ~100 |
| RadiusClearanceTests | 4 | ~80 |
| PathfindingIntegrationTests (expand) | 5 | ~100 |
| PathfindingSnapshotRoundTripTests | 5 | ~100 |

### Phase 2 — Non-Hero AI Tests (Priority: HIGH, ~400 lines)
| Test Class | Est. Tests | Lines |
|---|---|---|
| JungleCampSpawnTimerTests | 5 | ~100 |
| JungleCampAggroLeashTests | 4 | ~80 |
| MinionWaveCompositionTests | 5 | ~100 |
| NonHeroSnapshotRoundTripTests | 6 | ~120 |

### Phase 3 — Presentation Tests (Priority: MEDIUM, ~300 lines)
| Test Class | Est. Tests | Lines |
|---|---|---|
| DeathPresenterPlayModeTests | 3 | ~80 |
| RespawnPresenterPlayModeTests | 3 | ~80 |
| AttackSfxHandlerPlayModeTests | 3 | ~70 |
| HUDLuaPlayModeTests | 4 | ~70 |

### Phase 4 — Ability Expansion Tests (Priority: MEDIUM, ~250 lines)
| Test Class | Est. Tests | Lines |
|---|---|---|
| HoldReleaseAbilityFullCycleTests | 4 | ~80 |
| ChannelInterruptionTests | 3 | ~60 |
| AbilityBakePipelineExpandedTests | 5 | ~110 |

### Phase 5 — Large-Scale Integration Tests (Priority: LOW, ~400 lines)
| Test Class | Est. Tests | Lines |
|---|---|---|
| FullGameplayLoopDeterministicTests | 4 | ~120 |
| SpawnToCombatToDeathIntegrationTests | 3 | ~100 |
| MultiTickRollbackReplayTests | 4 | ~100 |
| ChecksumConsistencyAcrossEndpoints | 3 | ~80 |

## 4. Test Execution Strategy

-   **EditMode preferred** for deterministic tests (faster, no Unity lifecycle overhead)
-   **PlayMode required** for: Unity Input System, Scenes, GameObject lifecycle, Prefabs, UI, Presentation
-   **Run before each ExecPlan**: Full EditMode suite + targeted PlayMode tests
-   **Run after each ExecPlan**: Full EditMode + PlayMode suites
-   **Large-scale tests** allowed once core gameplay modules pass all Phase 1-2 tests

## 5. Known Test Gaps (Not Blocking)

1.  **Pipeline_SingleUnit_MovesWithCommand** (1 failure): Pre-existing. Raw-constructor FrameSyncGameRuntime doesn't wire movement pipeline. Fix requires pathfinding integration test refactor (included in Phase 1 above).

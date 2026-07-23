# Superseded historical draft — do not execute

> **Status: Superseded / Do not execute.** The 2026-07-22 design-conformance re-audit found that this proposal assumed plans 0009–0045 were design-complete, while current Unit and FrameSync tests fail and several deterministic public contracts are incomplete. Candidate C also invents Early/Mid/Late phase scaling that is absent from the current formal MatchRule design. Candidates A and B remain possible future topics only after the deterministic recovery gate. The executable 0046 is `0046_unit_lifecycle_and_physics_identity_recovery_execplan.md`.

# Candidate Plans — Batch 0046–0048 (Historical)

> Created: 2026-07-22 (post 0045 Gold & Equipment Completion)
> Based on: MODULE_STATUS.md Known Gaps, DESIGN_INDEX.md, Roadmap
> Recommendation: A → B → C

---

## Candidate A: A* Pathfinding — Deterministic Search + PathFollower (~650 lines)

**Gap**: 0041 built the grid map, data model, and agent skeleton. `UnitLocomotionAgent.Evaluate()` returns Idle every tick — no actual pathfinding happens. A* search, the indexed min-heap, path smoothing, and waypoint following are all absent. This is the **single largest remaining framework gap**.

**Design authority**: `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` §7, §9, §14, §15

### What this unlocks
- Right-click ground → compute path → follow waypoints → reach destination
- Minion lane movement with repath
- AI chase/retreat with obstacle avoidance
- Validates the entire locomotion pipeline (0041) end-to-end

### New files — Unit/Pathfinding/ (~350 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `IndexedMinHeap.cs` | ~95 | Deterministic indexed binary min-heap for A* open-set. SearchId-pattern reuse. No boxing, no LINQ. |
| 2 | `AStarPathService.cs` | ~180 | Deterministic A*: SearchId reuse, octile heuristic, LOS smoothing, 8-dir neighbor expansion. |
| 3 | `PathFollower2D.cs` | ~110 | Waypoint consumption: cursor advance, corridor check, arrival detection, `LocomotionResult` output. |

### Modified files (~300 lines)

| # | File | Lines | Key changes |
|---|---|---|---|
| 4 | `UnitLocomotionAgent.cs` | +120 | Full `Evaluate()`: call A*, store path indices, create/reuse PathFollower2D, advance cursor, check arrival |
| 5 | `MovementHandler.cs` | +55 | `ApplyRouteMovement()`: consume LocomotionResult, compute velocity, apply to PhysicsEntity2D |
| 6 | `UnitWorld.cs` | +25 | Add `AStarPathService` singleton, pass to LocomotionAgent on spawn |
| 7 | `SimulationTickPipeline.cs` | +45 | ExecuteTick ordering: locomotion evaluate → movement apply → collision resolve |
| 8 | `MovementSnapshot.cs` | +15 | Capture PathFollower2D state |
| 9 | `RouteRuntime.cs` | +25 | Add RouteFinished, PathCursor for restore |
| 10 | `LocomotionAgentSnapshot.cs` | +15 | Add PathCellIndices for full restore |

### Design conformance
- §7 AStarPathService: SearchId reuse, indexed heap, octile heuristic, LOS smoothing
- §9 PathFollower2D: cursor advance, corridor check, arrival detection
- §14 LocomotionResult: tick-local struct, no cross-tick storage
- §15 Snapshot: PathFollower state captured; A* open/closed sets rebuildable

### RVO — deferred
First pass uses direct LocomotionResult without RVO blending. DeterministicRVOSystem (§10) deferred to follow-up.

### Tests (~120 lines)
- `AStarPathServiceTests.cs`: straight line, obstacle detour, unreachable target, stable ordering
- `PathFollower2DTests.cs`: cursor advance, arrival, corridor deviation, capture/restore

---

## Candidate B: Presentation Bridge Completion (~500 lines)

**Gap**: 0042 built the data pipeline (VisualEventOutput, PresentationEventId, stubbed VfxManager/AudioManager) and UnitAnimationDriver skeleton. But no actual VFX/SFX instantiation, no asset binding, and no animation parameter mapping. The bridge exists but doesn't produce visual output.

**Design authority**: `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`

### What this unlocks
- Attack impacts produce visible VFX
- Ability casts play SFX
- Death animations trigger correctly
- Buff visual indicators appear on units
- Complete Gameplay→Visual feedback loop

### New files (~150 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `VfxDefinitionTable.cs` | ~50 | Maps VfxDefId to prefab pool config (prefab path, pool size, duration) |
| 2 | `SfxDefinitionTable.cs` | ~50 | Maps SfxDefId to AudioClip config (clip, volume, pitch, OneShotNoReplay policy) |
| 3 | `PresentationAssetBinding.cs` | ~50 | Links Gameplay event types to VfxDefId/SfxDefId via static lookup |

### Modified files (~350 lines)

| # | File | Lines | Change |
|---|---|---|---|
| 4 | `VfxManager.cs` | +100 | Full implementation: ParticleSystem pool, PlayOrReconcile with rollback support |
| 5 | `AudioManager.cs` | +100 | Full implementation: AudioSource pool, OneShotNoReplay dedup, rollback reconciliation |
| 6 | `UnitAnimationDriver.cs` | +80 | Read ActionStateView, map to Animator parameters, handle death/respawn transitions |
| 7 | `PresentationSyncManager.cs` | +70 | Wire VfxManager/AudioManager consumption, handle rollback event reconciliation |

### Tests (~100 lines)
- `VfxManagerPoolTests.cs`: pool allocation, deallocation, reconciliation
- `AudioManagerTests.cs`: OneShotNoReplay dedup, pitch/volume application

---

## Candidate C: MatchRuleRuntime Foundation (~400 lines)

**Gap**: No match timer, no win/loss conditions, no game phase transitions. The match runs indefinitely with no structure. Gold income, respawn timers, and minion wave power should all scale with match phase.

**Design authority**: `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`

### What this unlocks
- Match timer with visible countdown
- Game phases: Early (0–10min) / Mid (10–25min) / Late (25min+)
- Win/loss condition checking (nexus destruction, surrender)
- Gold income passive scaling by phase
- Respawn timer scaling by phase + level
- Minion wave power scaling by phase

### New files — FrameSync/ (~200 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `MatchRuleRuntime.cs` | ~100 | Phase transitions, timer, win condition checking |
| 2 | `MatchPhase.cs` | ~10 | Enum: Early, Mid, Late, Finished |
| 3 | `PhaseScalingTarget.cs` | ~15 | Enum + configurable per-phase multipliers |
| 4 | `PhaseScalingTable.cs` | ~40 | Static lookup: GetMultiplier(phase, target) |
| 5 | `MatchRuleSnapshot.cs` | ~35 | Snapshot: Phase, ElapsedTicks, WinningTeam |

### Modified files (~200 lines)

| # | File | Lines | Change |
|---|---|---|---|
| 6 | `SimulationTickPipeline.cs` | +30 | MatchRuleRuntime.TickUpdate() first each tick |
| 7 | `FrameSyncGameRuntime.cs` | +40 | Add MatchRuleRuntime, initialize with config |
| 8 | `RespawnTimer.cs` | +20 | Phase multiplier for respawn time |
| 9 | `GoldIncomeRuntime.cs` | +25 | Phase multiplier for passive gold |
| 10 | `MinionSystem.cs` | +25 | Phase multiplier for minion stats |
| 11 | `UnitWorld.cs` | +15 | Pass MatchRule reference |
| 12 | `GameplaySnapshot.cs` | +20 | Add MatchRuleSnapshot |
| 13 | `PredictionRollbackCoordinator.cs` | +15 | Restore MatchRule on rollback |

### Tests (~80 lines)
- `MatchRuleRuntimeTests.cs`: phase transitions, timer, win conditions
- `PhaseScalingTableTests.cs`: multiplier lookup

---

## Comparison Matrix

| Dimension | A: A* Pathfinding | B: Presentation Bridge | C: MatchRuleRuntime |
|---|---|---|---|
| **Lines (new + modified)** | ~650 | ~500 | ~400 |
| **New files** | 3 | 3 | 5 |
| **Modified files** | 7 | 4 | 8 |
| **Assemblies touched** | Unit, FrameSync | Unit, FrameSync | Unit, FrameSync |
| **Design doc** | Pathfinding v13.1 | Presentation v13.2 | FrameSync v10.2 |
| **Risk** | Medium (algorithm correctness) | Low (pool management) | Low (state machine) |
| **Unlocks** | Real MOBA movement, AI pathing | Visual/sound feedback loop | Match structure, phase scaling |
| **Blocked by** | 0041 grid (done) | 0042 pipeline (done) | FrameSync pipeline (done) |
| **Vertical slice** | Click-to-move end-to-end | Hit→VFX+SFX feedback | Match start→phase→victory |

## Recommendation

**A → B → C**

1. **A* Pathfinding first** — Largest remaining framework gap. Validates all locomotion infrastructure from 0041. A working click-to-move loop is the single most impactful gameplay milestone.

2. **Presentation Bridge second** — Once pathfinding gives units meaningful movement, visual feedback closes the Gameplay→Player feedback loop. The stubbed pipeline from 0042 makes this a natural next step.

3. **MatchRuleRuntime third** — Provides match structure. Without it, the match is an infinite sandbox. Lightest of the three but completes the match-flow skeleton.

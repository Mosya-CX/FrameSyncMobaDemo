# Candidate Plans — Batch 0045–0046

> Created: 2026-07-22 (post 0044 XP & Level-Up System)
> Based on: MODULE_STATUS.md Known Gaps, DESIGN_INDEX.md, Roadmap
> Remaining from previous batch: A (A* Pathfinding), C (MatchRuleRuntime)

---

## Candidate A: A* Pathfinding — Deterministic Search + PathFollower (~650 lines)

**Gap**: 0041 built the grid, data model, and agent skeleton. But `UnitLocomotionAgent.Evaluate()` returns Idle — no actual pathfinding. A* search, indexed min-heap, path smoothing, and waypoint following are all absent. Units can't navigate around obstacles.

**Design authority**: `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` §7, §9

### What this unlocks
- Right-click ground → compute A* path → follow waypoints → reach destination
- Minion lane movement with repath
- AI chase/retreat with obstacle avoidance
- Validates the entire locomotion pipeline (0041) end-to-end — **the single biggest gameplay milestone still missing**

### New files — Unit/Pathfinding/ (~350 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `IndexedMinHeap.cs` | ~95 | Deterministic indexed binary min-heap for A* open-set. SearchId-pattern reuse. |
| 2 | `AStarPathService.cs` | ~180 | Deterministic A*: SearchId reuse, octile heuristic, LOS smoothing, 8-dir neighbor expansion. |
| 3 | `PathFollower2D.cs` | ~110 | Waypoint consumption: cursor advance, corridor check, arrival detection, LocomotionResult output. |

### Modified files (~300 lines)

| # | File | Lines | Key changes |
|---|---|---|---|
| 4 | `UnitLocomotionAgent.cs` | +120 | Full Evaluate(): call A*, store path indices, create/reuse PathFollower2D, advance cursor, check arrival/blocked |
| 5 | `MovementHandler.cs` | +55 | ApplyRouteMovement(): consume LocomotionResult, compute velocity, apply to PhysicsEntity2D |
| 6 | `UnitWorld.cs` | +25 | Add AStarPathService singleton, pass to LocomotionAgent on spawn |
| 7 | `SimulationTickPipeline.cs` | +45 | ExecuteTick: locomotion evaluate → movement apply → collision resolve ordering |
| 8 | `MovementSnapshot.cs` | +15 | Capture PathFollower2D state |
| 9 | `RouteRuntime.cs` | +25 | Add RouteFinished, PathCursor for restore |
| 10 | `LocomotionAgentSnapshot.cs` | +15 | Add PathCellIndices for full restore |

### Tests needed (~120 lines)
- AStarPathServiceTests: straight line, obstacle detour, unreachable target, stable ordering, LOS smoothing
- PathFollower2DTests: cursor advance, arrival, corridor deviation, capture/restore

---

## Candidate B: Gold & Equipment Completion (~400 lines)

**Gap**: Equipment/Shop (0028) and GoldIncomeRuntime have foundations but the purchase→apply→stat loop is incomplete. Equipment effects (passives) don't fire on purchase, shop undo doesn't work properly, and the gold ledger doesn't integrate with the kill-reward loop from 0044.

**Design authority**: `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`

### What this unlocks
- Buy equipment → apply stat modifiers → visible stat changes
- Equipment passive effects on purchase
- Sell/undo equipment with proper stat cleanup
- Gold reward on kill (wired to 0044's DeathEffectDispatcher)
- Complete gold→shop→stats loop

### New files (~120 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `EquipmentPassiveApplier.cs` | ~70 | On purchase: read EquipmentDef passives, create Buff/Modifier handles |
| 2 | `ShopTransactionValidator.cs` | ~50 | Validate purchase (gold, slot, unique constraints) |

### Modified files (~280 lines)

| # | File | Lines | Key changes |
|---|---|---|---|
| 3 | `EquipmentHandler.cs` | +60 | Purchase: validate → deduct gold → apply passives → store in inventory |
| 4 | `EquipmentShopRuntime.cs` | +80 | Sell: remove passives → refund gold; Undo: revert last transaction |
| 5 | `GoldIncomeRuntime.cs` | +40 | Wire kill gold bounty from DeathEffectDispatcher |
| 6 | `DeathEffectDispatcher.cs` | +20 | Add gold bounty distribution alongside XP |
| 7 | `CombatSystem.cs` | +30 | GoldAllocation production on death |
| 8 | `EquipmentDatabase.cs` | +30 | Unique-item constraint validation |
| 9 | `EquipmentDefinition.cs` | +20 | Add passive effect definitions, unique flags |

---

## Comparison Matrix

| Dimension | A: A* Pathfinding | B: Gold & Equipment |
|---|---|---|
| **Lines (new + modified)** | ~650 | ~400 |
| **New files** | 3 | 2 |
| **Modified files** | 7 | 7 |
| **Assemblies touched** | Unit, FrameSync | Unit, FrameSync |
| **Design doc** | Pathfinding v13.1 | Equipment/Gold v12 |
| **Risk** | Medium (algorithm) | Low (data plumbing) |
| **Unlocks** | Real MOBA movement | Complete shop loop |
| **Blocked by** | 0041 grid (done) | 0028 shop (done), 0044 XP (done) |

## Recommendation

**A → B**

A* Pathfinding is the largest remaining framework gap and unlocks the most visible gameplay capability. Gold & Equipment completion is a natural follow-up that closes the kill→reward→shop→stats loop, building on 0044's XP system.

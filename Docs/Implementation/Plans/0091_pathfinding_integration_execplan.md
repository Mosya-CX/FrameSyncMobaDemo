# ExecPlan 0091: Pathfinding Integration -- RVO + FlowField + A* Interop

> Status: in_progress
> Started: 2026-07-24
> Priority: HIGH (Gameplay core)

## 1. Purpose

Verify and harden the pathfinding integration layer: RVO multi-agent avoidance, FlowField lane-marching, and A* fallback when FlowField is unavailable. The individual systems (A*, FlowField, RVO, WallPenetration, Radius Clearance) exist and compile; this plan ensures they interoperate correctly under MOBA-scale scenarios and writes integration tests that prove the contracts.

## 2. Progress

- [ ] 2.1 RVO multi-agent avoidance integration tests
- [ ] 2.2 FlowField lane-direction verification tests
- [ ] 2.3 A* + FlowField fallback logic + tests
- [ ] 2.4 Minion wave FlowField movement integration test
- [ ] 2.5 Compilation verification
- [ ] 2.6 Test execution and pass
- [ ] 2.7 Update MODULE_STATUS and NEXT_CANDIDATES

## 3. Surprises and discoveries

- (none yet)

## 4. Decision log

- Integration tests are placed in `Assets/Scripts/Gameplay/Tests/` alongside existing `FlowFieldBuildTests.cs`, `RVOSystemTests.cs`, `WallPenetrationTests.cs`.
- Tests are EditMode (pure deterministic, no Unity lifecycle needed for pathfinding logic).
- RVO queue ordering follows existing stable iteration: units ordered by UnitUid before RVO step (RvoOrchestrator already does this via handler/agent array order).
- FlowField fallback: when `_flowFieldRegistry` is null or key not found, UnitLocomotionAgent.EvaluateFlowField returns NoRoute, and Evaluate() falls through to A* pathfinding. This is already implemented; test verifies the behavior.

## 5. Current repository context

| Item | Path |
|---|---|
| Pathfinding design | `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` |
| A* service | `Assets/Scripts/Gameplay/Pathfinding/AStarPathService.cs` |
| FlowField service | `Assets/Scripts/Gameplay/Pathfinding/TeamFlowFieldService.cs` |
| FlowField registry | `Assets/Scripts/Gameplay/Pathfinding/FlowFieldRegistry.cs` |
| FlowField data | `Assets/Scripts/Gameplay/Pathfinding/TeamFlowFieldData.cs` |
| FlowField key | `Assets/Scripts/Gameplay/Pathfinding/FlowFieldKey.cs` |
| RVO system | `Assets/Scripts/Gameplay/Pathfinding/DeterministicRVOSystem.cs` |
| RVO orchestrator | `Assets/Scripts/Gameplay/Pathfinding/RvoOrchestrator.cs` |
| RVO config | `Assets/Scripts/Gameplay/Pathfinding/RVOConfig.cs` |
| RVO input/result | `Assets/Scripts/Gameplay/Pathfinding/RVOInput.cs`, `RvoResult.cs` |
| Wall penetration | `Assets/Scripts/Gameplay/Pathfinding/WallPenetrationResolver.cs` |
| Radius class | `Assets/Scripts/Gameplay/Pathfinding/RadiusClass.cs` |
| Path follower | `Assets/Scripts/Gameplay/Pathfinding/PathFollower2D.cs` |
| Locomotion agent | `Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs` |
| Movement handler | `Assets/Scripts/Gameplay/Movement/MovementHandler.cs` |
| Path grid map | `Assets/Scripts/Gameplay/Pathfinding/PathGridMap2D.cs` |
| Locomotion result | `Assets/Scripts/Gameplay/Pathfinding/LocomotionResult.cs` |
| Route move request | `Assets/Scripts/Gameplay/Pathfinding/RouteMoveRequest.cs` |
| Dir8 enum | `Assets/Scripts/Gameplay/Pathfinding/Dir8.cs` |
| Existing RVO tests | `Assets/Scripts/Gameplay/Tests/RVOSystemTests.cs` |
| Existing FlowField tests | `Assets/Scripts/Gameplay/Tests/FlowFieldBuildTests.cs` |
| Existing Wall tests | `Assets/Scripts/Gameplay/Tests/WallPenetrationTests.cs` |
| Existing locomotion tests | `Assets/Scripts/Gameplay/Tests/LocomotionSnapshotTests.cs` |
| Unit tests asmdef | `Assets/Scripts/Gameplay/Tests/FrameSyncMoba.Unit.Tests.asmdef` |
| Non-hero AI | `Assets/Scripts/Gameplay/NonHero/UnitAIController.cs` |

## 6. Design sources

| Document | Sections |
|---|---|
| `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` | sections 6 (RouteResolver), 7 (A*), 8 (FlowField), 9 (PathFollower), 10 (RVO), 11 (MovementHandler), 12 (Wall), 13 (Tick order), 14 (Data structures) |

## 7. Scope

**In scope:**
- RVO multi-agent avoidance test (5+ agents, no deadlock)
- FlowField lane-direction verification (minion lane marching)
- A* fallback when FlowField unavailable
- Minion wave FlowField movement integration test
- FlowField + RVO combined test (agents use FlowField direction + RVO avoidance)
- Any minor bugfixes discovered during integration testing

**Out of scope:**
- New pathfinding algorithms
- Production map data or lane configurations
- Visual/presentation verification
- Performance profiling
- Changes to public contracts (unless bugs found)

## 8. Implementation plan

1. Create `Assets/Scripts/Gameplay/Tests/PathfindingIntegrationTests.cs` with the following test classes:
   - `RVOIntegrationTests`: multi-agent RVO scenarios
   - `FlowFieldLaneTests`: lane-direction verification
   - `AStarFlowFieldFallbackTests`: fallback behavior
   - `MinionWaveFlowFieldTests`: minion-with-FlowField integration
2. Add any missing helper methods to existing pathfinding types (if needed for testability)
3. Trigger compilation via MCP
4. Run EditMode tests via MCP
5. Fix any failures
6. Update status documents

## 9. Public contracts

No new public contracts. All integration tests use existing public APIs:
- `DeterministicRVOSystem.Step(RVOInput[])`
- `TeamFlowFieldService.GetFlowDirection(TeamFlowFieldData, fp2)`
- `TeamFlowFieldData` builder from test helpers
- `AStarPathService.FindPath(...)`
- `UnitLocomotionAgent.Evaluate()`
- `PathFollower2D.BuildLocomotionResult(...)`

## 10. Validation

- Unity compilation: zero new errors
- EditMode tests: all new pathfinding integration tests pass
- Existing RVO tests still pass
- Existing FlowField tests still pass
- Existing WallPenetration tests still pass

## 11. Failure and recovery

- If a test reveals a bug in the pathfinding runtime, fix the runtime code in the same ExecPlan
- If a test cannot pass due to missing infrastructure, record the limitation and descope that test
- New files can be safely deleted to revert

## 12. Results

(To be filled after completion)

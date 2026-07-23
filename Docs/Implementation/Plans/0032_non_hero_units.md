# ExecPlan 0032 — Non-Hero Unit System

> **Design authority**: `Docs/Design/moba_non_hero_unit_modules_design_v5.md`
> **Estimated code**: ~450–600 lines
> **Dependencies**: Unit lifecycle ✓ / Combat ✓ / Physics ✓ / AI framework

## Rationale

The Unit framework supports identity, spawning, and lifecycle. Non-hero units (minions, jungle monsters, towers) are autonomous Gameplay entities that follow fixed rules. They need their own management systems: wave spawning, camp state machines, and simple AI controllers.

## Scope — New files

| File | Lines | Description |
|---|---|---|
| `Unit/NonHero/MinionSystem.cs` | ~120 | Wave management: WaveIndex, NextWaveLogicTick, PendingTickets[], NextTicketCursor. ManagedMinionUids[] tracking. Spawn wave processing. Death cleanup (remove from ManagedMinionUids, unregister AIController). |
| `Unit/NonHero/JungleCampSystem.cs` | ~130 | Camp state machine: Idle→Combat→Reset→Dead. MemberUidsBySlot[], MemberAliveBySlot[], MainMonsterDead. PrimaryTargetUid, LastHostileActionLogicTick, NextRespawnLogicTick, ResetBeginLogicTick. |
| `Unit/NonHero/UnitAIController.cs` | ~90 | Abstract base: ControllerKind (Minion/Monster/Tower), OwnerUnitUid. MinionState (MovingToLane/AttackingTarget), MonsterState (Idle/Chasing/Returning/Dead), TowerState. Register/unregister from UnitWorld. |
| `Unit/NonHero/NonHeroSnapshot.cs` | ~60 | MinionSystemSnapshot (WaveIndex/NextWaveLogicTick/PendingTickets/ManagedMinionUids), JungleCampSnapshot (CampId/State/MemberUids/MemberAlive/MainMonsterDead/PrimaryTargetUid/LastHostileActionTick/NextRespawnTick/ResetBeginTick), UnitAIControllerSnapshot (ControllerKind/OwnerUnitUid/MinionState/MonsterState/TowerState) |

## Scope — Modified files

| File | Lines | Change |
|---|---|---|
| `FrameSync/GameplaySnapshot.cs` | +30 | Add NonHeroState field aggregating MinionSystemSnapshot + JungleCampSnapshot[] + UnitAIControllerSnapshot[] |
| `Unit/Core/UnitWorld.cs` | +40 | PendingUnitLifecycleQueue. Non-hero death: remove AIController from registry, update ManagedMinionUids/CampMemberAlive. SpawnUnit integration with AIController. |
| `FrameSync/SimulationTickPipeline.cs` | +15 | Tick: MinionSystem.ProcessWave + JungleCampSystem.Tick + AIControllers.AIThink |

## Key conformance

- AI does NOT use player-input module or simulate keyboard/mouse (design §14)
- AI directly reads Ability definitions and runtime state
- Non-hero death removes AIController from UnitWorld registry — snapshot no longer contains active Controller
- MinionSystemSnapshot reflects unregistered ManagedMinionUids after death
- JungleCampSnapshot reflects MemberAliveBySlot, MainMonsterDead, State, NextRespawnLogicTick after death
- AI active Tick derived from SpawnLogicTick (not independent FirstAITickLogicTick field)

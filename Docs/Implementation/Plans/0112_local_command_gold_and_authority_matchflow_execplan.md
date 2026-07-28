# ExecPlan 0112: Local command, gold, and authority match-flow correctness

> Status: Implemented and MCP-validated; focused Test Runner remains pending
> because the user's open `ClientBootstrap` scene is dirty.
> Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 3.
> Design conformance: Strict -- no deviation.
> Estimated production/test change: 650-1100 lines.

## Purpose and observable production behavior

Local input Commands receive a legal future `TargetTick` from the FrameSync
request layer and are retained until that Tick. Cast Commands preserve their
existing `AbilitySignalVerb` and `AimSnapshot` through Intent and Action
translation. Natural gold is requested while the current Tick batch is open,
and only authority execution can finalize match end.

## Progress

- [x] Read the current formal FrameSync, PlayerInput, Ability and Gold designs.
- [x] Inspect current requester, command collector, pipeline, gold and match flow.
- [x] Add the formal TargetTick resolver and baked settings.
- [x] Retain and consume future Commands by exact TargetTick.
- [x] Preserve Ability verb/Aim through Command, Order, Unit Intent and Cast
  Action paths.
- [x] Derive natural-gold cadence from deterministic Tick/RunningStartTick.
- [x] Remove Bootstrap authority mutation.
- [x] Add focused tests and validate with Unity MCP.

## Surprises and discoveries

- The same Ability verb/Aim loss existed in the AI/script `Order` translation
  path, not only in local `GameplayCommand` dispatch. Extending the existing
  `Order` discriminated union avoided a second AI input protocol.
- Future Commands were already serialized with `TargetTick`; the defect was
  collector lifecycle, because the pipeline executed and cleared all pending
  commands every Tick.
- The existing natural-income counter was unnecessary snapshot state. The
  formal cadence is derived entirely from `logicTick - RunningStartTick`.
- The existing `MatchFlowStateMachine` was a second match-state writer.
  Authority mutation now remains in the execution-mode-aware pipeline, while
  the bootstrap state machine only observes and captures the final result.

## Decision log

- `CommandTargetTickResolver` owns the formal local target formula:
  `max(LocalSimulationTick + 1,
  LatestSynchronizedServerTick + MinCommandLeadTicks)`.
- Pending Commands remain in `CommandCollector` until exact-Tick consumption;
  canonical ordering inside a Tick is unchanged.
- `UnitIntent` stores the existing `AbilitySignalVerb` and `AimSnapshot`.
  GameplaySnapshot schema is 6 and checksum writes both members.
- Natural gold has no independent counter. It requests one canonical batch
  before Combat and before `GoldIncomeRuntime.SealTick`.
- ClientPrediction observes match state but cannot enter Ending or Finished.

## Current repository context

- `PlayerCommandRequester` accepts arbitrary build/target Tick delegates.
- Bootstrap currently supplies `Runtime.CurrentTick` for both, producing a
  Command for the Tick being executed rather than the formal future Tick.
- `CommandCollector` stores TargetTick but the pipeline executes every collected
  Command immediately and clears the whole collector after each Tick.
- `GameplayCommand` already owns the formal `AbilitySignalVerb` and
  `AimSnapshot`; the Planner path discards the verb and later hard-codes Focus.
- `NaturalGoldIncomeSystem` exists but is not composed by
  `FrameSyncGameRuntime`; its pipeline call occurs after `SealTick`.
- `MatchFlowStateMachine.AdvanceTick` repeats deterministic phase advancement
  and authority end evaluation outside the execution-mode-aware pipeline.

## Exact design sources

- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`, sections
  5, 7, 9.4, 13, 14 and 17.2.
- `Docs/Design/MOBA_Player_Input_Command_Module_Design_v1_1.md`, sections
  1, 2, 11, 13, 17, 21.3 and 21.6.
- `Docs/Design/moba_ability_system_design_v15_2.md`, sections 1.1-1.2.
- `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`, sections
  7.8-7.10.
- `Docs/Architecture/DECISION_LOG.md`, authority, gold and input decisions.

## In scope

- Existing Command Header/Collector/request path and future-Tick buffering.
- Baked `MinCommandLeadTicks` and `MaxFutureCommandTicks`.
- Existing `UnitIntent`, `CastActionRequest`, aggregate schema and checksum.
- Existing `NaturalGoldIncomeSystem`, pipeline order and runtime composition.
- Existing `MatchFlowStateMachine`/Bootstrap observation boundary.
- Focused EditMode tests and direct Unity MCP behavior validation.

## Out of scope

Attack source semantics, Projectile settlement, Ability authoring/runtime-view
completion, physical InputAction lifecycle, network transport/recovery,
production content, packages and unrelated P2 cleanup.

## Affected assemblies and exact production types

- RuntimeConfig: `GlobalGameplayData`, `BakedGlobalGameplayData`.
- FrameSync: `CommandTargetTickResolver`, `CommandCollector`,
  `SimulationTickPipeline`, `FrameSyncGameRuntime`,
  `NaturalGoldIncomeSystem`, `MatchFlowStateMachine`, snapshot schema/checksum.
- Unit: `UnitIntent`, `BehaviorPlanner`, `CastActionRequest`.
- PlayerInput: `PlayerCommandRequester`.
- Bootstrap: `GameBootstrap`.

## Public contracts

Reuse `GameplayCommand`, `CommandHeader`, `AbilitySignalVerb`, `AimSnapshot`,
`UnitIntent`, `GoldIncomeRuntime` and `MatchRuleRuntime`. Add only the
design-owned TargetTick resolver. Do not create a second Command, signal, Aim,
gold ledger, match state or runtime DTO.

## Ownership and dependency direction

```text
RuntimeConfig -> baked lead/window values
FrameSync resolver -> TargetTick
PlayerInput requester -> existing GameplayCommand
CommandCollector -> pending Commands partitioned by TargetTick
SimulationTickPipeline -> exact-Tick consume and deterministic execution
Unit Planner/Action -> existing AbilitySignal
Gold producers -> unique GoldIncomeRuntime open batch
Pipeline execution mode -> MatchRule authority evaluation
Bootstrap/MatchFlow -> read-only observation
```

## Deterministic ordering

Commands for a Tick retain the current canonical sort. Future ticks are never
executed early. Focus/Commit on one Tick remain ordered by `CommandSeq`.
Natural income iterates ascending PlayerSlot and uses only Tick,
`RunningStartTick`, interval and amount.

## Snapshot and serialization impact

Adding Ability verb/Aim to `UnitIntent` changes future-affecting state, so
`GameplaySnapshot.SchemaVersion` increments from 5 to 6 and checksum writes the
new members. Pending local/network Command buffers and `GoldIncomeRuntime`
remain outside GameplaySnapshot per design. Natural gold owns no counter after
the cadence becomes Tick-derived.

## Implementation steps

1. Bake and validate command lead/window values.
2. Add resolver formula and inject it into `PlayerCommandRequester`.
3. Retain future Commands and consume only exact Tick commands.
4. Carry Ability verb/Aim through Command -> Intent -> Action -> Signal.
5. Compose Tick-derived natural gold and run it after `BeginTick`, before Combat.
6. Keep `SealTick` at Tick end before digest/checksum.
7. Make MatchFlow a result observer; leave phase/end mutation in the pipeline.
8. Add focused deterministic tests, compile, inspect Console and directly
   validate the actual neutral runtime.

## EditMode tests

- Target resolver formula, lead/window rejection and actual BuildLocalTick.
- Future Command is not executed early and is consumed at its TargetTick.
- Focus and Commit preserve verb/Aim and same-Tick sequence order.
- Natural gold cadence, ascending PlayerSlot order, replay/digest equality.
- ClientPrediction cannot enter Ending; ServerAuthority can.

No PlayMode test is required because this slice does not change InputAction,
scene or presentation lifecycle.

## Unity MCP validation

Refresh/compile and inspect Console. Run focused tests only if the dirty-scene
precondition permits; otherwise execute the same deterministic scenarios through
MCP dynamic C# without saving `ClientBootstrap.unity`.

## Failure conditions

Stop only if the formal Command formula conflicts with current Command
serialization, if Ability verb preservation requires a second protocol, or if
authority-only match transition cannot be enforced without Gate 10 transport
work.

## Completion criteria

- Legal future Commands are retained and execute only at TargetTick.
- Focus/Commit reaches `AbilityHandler` unchanged.
- Natural gold is inside the open batch with replay-stable digest.
- Client prediction cannot finalize match end.
- Schema/checksum, tests, Unity compile, status and parent plan are current.

## Production-content exclusion

Only neutral framework fixtures may be used. No formal hero, concrete ability,
Buff, equipment, map or balance content is added.

## Results

Implemented on 2026-07-26.

- Legal future Commands remain buffered and execute only on their exact
  `TargetTick`.
- Local Command and AI/script Order paths preserve Focus/Commit/Cancel and the
  canonical Aim through Intent, ActionRequest and AbilitySignal.
- Natural gold is composed into the runtime, derives cadence from deterministic
  Tick state, and emits ascending PlayerSlot records while the batch is open.
- Bootstrap no longer advances or finalizes match state; only
  ServerAuthority execution can transition a running match toward completion.
- Added focused EditMode coverage in
  `LocalCommandGoldMatchFlowTests`, `PlayerCommandRequesterTests`, and
  `MatchFlowStateMachineTests`.
- Unity MCP refresh/compile completed with zero errors. The only Console entry
  is the pre-existing `VfxManager._defaultPoolSize` unused-field warning.
- Direct MCP behavior validation against the real neutral catalog/prefab passed:
  `target=1`, `build=0`, a future command was retained then executed at Tick 1,
  Commit plus Direction Aim survived, natural income produced 10 ascending
  records, schema 6 restored, ClientPrediction stayed Running, and
  ServerAuthority entered Ending.
- No PlayMode test is required by this pure deterministic slice. Focused
  Test Runner execution remains deferred only to avoid saving or discarding the
  user's dirty open scene.

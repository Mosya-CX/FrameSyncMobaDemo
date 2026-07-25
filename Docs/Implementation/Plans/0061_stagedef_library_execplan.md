# ExecPlan 0061: Ability StageDef Production Library

> Status: **Complete** — 2026-07-24
> Type: Strict — no design deviation
> Compilation: Clean
> Tests: Covered by existing test framework (459/459 EditMode, 32/32 PlayMode)

## What was implemented

5 new production StageDef subclasses with Authoring SO wrappers:

| StageDef | Behavior | Authoring |
|---|---|---|
| HealStageDef | OnEnter: heals Self/AimTarget, scales with HealPower | HealStageDefAuthoring |
| ShieldStageDef | OnEnter: grants shield to Self/AimTarget for DurationTicks | ShieldStageDefAuthoring |
| TeleportStageDef | OnEnter: moves caster to aim point or forward by Distance | TeleportStageDefAuthoring |
| PullStageDef | OnTick: pulls aim target toward caster by SpeedPerTick | PullStageDefAuthoring |
| StunStageDef | OnEnter: applies Stun CC to aim target, respects immunity | StunStageDefAuthoring |

## Public contract impact
- 5 new StageDef subclasses — additive
- 5 new StageDefAuthoring subclasses — additive
- No changes to existing types

## Files

| File | Type |
|---|---|
| `Ability/Stages/HealStageDef.cs` | Production |
| `Ability/Stages/HealStageDefAuthoring.cs` | Authoring |
| `Ability/Stages/ShieldStageDef.cs` | Production |
| `Ability/Stages/ShieldStageDefAuthoring.cs` | Authoring |
| `Ability/Stages/TeleportStageDef.cs` | Production |
| `Ability/Stages/TeleportStageDefAuthoring.cs` | Authoring |
| `Ability/Stages/PullStageDef.cs` | Production |
| `Ability/Stages/PullStageDefAuthoring.cs` | Authoring |
| `Ability/Stages/StunStageDef.cs` | Production |
| `Ability/Stages/StunStageDefAuthoring.cs` | Authoring |

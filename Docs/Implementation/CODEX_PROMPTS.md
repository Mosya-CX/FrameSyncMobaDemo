# Codex Task Prompts for FrameSyncMobaDemo

## 1. First repository scan

```text
Read AGENTS.md, .agent/PLANS.md, DESIGN_INDEX.md, DECISION_LOG.md, ROADMAP.md, MODULE_STATUS.md, and ExecPlan 0000.

Execute ExecPlan 0000 exactly.

This task is inspection and documentation only. Do not modify production code, packages, scenes, prefabs, ScriptableObjects, Input Actions, or public contracts.

Use Unity MCP to inspect the project, compile, read the Console, and run the current test baseline.

Fill REPOSITORY_MAP.md and MODULE_STATUS.md using observed evidence. Create the next implementation ExecPlan for the first safe vertical slice.

Do not guess missing repository facts.
```

## 2. Implement one vertical slice

```text
Implement the current ExecPlan: <plan path>.

Authoritative designs:
- <exact design path>
- <exact design path>

Observable goal:
<one sentence>

In scope:
- <item>
- <item>

Out of scope:
- <item>
- <item>

Before editing:
1. Inspect existing types and asmdefs.
2. Record current compilation baseline.
3. Update the ExecPlan with actual paths and contracts.

Requirements:
- Do not create duplicate UID, Command, Snapshot, Aim, AbilitySignal, or DTO types.
- Preserve deterministic ordering.
- Add or update tests.
- Use Unity MCP to compile and run tests.
- Update MODULE_STATUS.md and the ExecPlan.

Complete the implementation, not only an explanation.
```

## 3. Design-conformance review

```text
Do not add new features.

Review the current branch against:
- <design path>
- <design path>
- DECISION_LOG.md

Use separate read-only analysis passes for:
1. Public contract ownership and duplicate types.
2. Determinism and stable ordering.
3. Snapshot / rollback correctness.
4. Tests and failure handling.

Classify findings as P0, P1, or P2.

Then fix all P0 and P1 findings in one coordinated implementation pass, compile through Unity MCP, run relevant tests, and update MODULE_STATUS.md and the current ExecPlan.

Do not silently change design documents.
```

## 4. Player Move vertical slice

```text
Implement the player-input Move vertical slice.

Design sources:
- Docs/Design/Input/MOBA_Player_Input_Command_Module_Design_v1_1.md
- Docs/Design/FrameSync/FrameSync_Flow_Integrated_System_Design_v10_2.md
- Docs/Design/Unit/unit_behavior_framework_design_v27_2.md
- Docs/Design/Physics/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md

Goal:
Right-clicking valid ground creates one canonical Move Gameplay Command that reaches the controlled Unit's Move intent through the existing formal pipeline.

In scope:
- PointerPosition and SecondaryClick Gameplay actions
- UnityGameplayInputSource
- LocalInputEventBuffer
- UI pointer gate interface
- GroundPoint resolver
- RequestMove facade
- Existing Move Command / Order / Intent integration
- EditMode determinism tests
- Minimal PlayMode Input System integration test

Out of scope:
- Attack
- QWER
- Ability Aim
- Network transport
- AI
- UI pages

Do not calculate TargetTick in the input module.
Do not put screen coordinates in Gameplay Command.
Do not reread Input System during rollback.
Do not create a second Move Command schema.

Use Unity MCP to compile and run tests.
```

## 5. Hold-release ability vertical slice

```text
Implement the hold-release player ability input slice.

Design sources:
- Docs/Design/Input/MOBA_Player_Input_Command_Module_Design_v1_1.md
- Docs/Design/Ability/moba_ability_system_design_v15_1.md
- Docs/Design/FrameSync/FrameSync_Flow_Integrated_System_Design_v10_2.md

Goal:
For a baked HoldRelease ability, key press creates Focus; key release or primary click creates the same Commit; right click does not Cancel; duplicate Commit input creates only one Command.

Requirements:
- Input mode is baked from CastModelDef.
- Do not duplicate MinFocusTicks, MaxFocusTicks, range, damage, cooldown, or charge curves.
- Focus and Commit may share TargetTick but Focus CommandSeq must be lower.
- Request layer returns or exposes equivalent TargetTick + CommandSeq receipt.
- Local states support FocusRequested, GameplayFocusing, and CommitRequested.
- Ability Runtime remains the authority for Session, Stage, charge timing, projectile creation, completion, and cooldown.
- AI does not use this input path.

Add EditMode state-machine tests and PlayMode Input System tests.
Compile and run all relevant tests through MCP.
```

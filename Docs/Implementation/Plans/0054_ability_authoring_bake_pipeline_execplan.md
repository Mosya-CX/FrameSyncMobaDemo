# ExecPlan 0054 — Ability Authoring Bake Pipeline: SO-Based Authoring with Editor Validation

> Parent: NEXT_CANDIDATES.md Candidate 0054
> Created: 2026-07-23
> Design authority: `moba_ability_system_design_v15_2.md`, `MOBA_Player_Input_Command_Module_Design_v1_1.md` §3, `Docs/Architecture/DECISION_LOG.md`

## Purpose

Complete the ability authoring pipeline so designers can create abilities through ScriptableObject assets instead of writing C#. Adds `AbilityAsset` (SO), `AbilityAssetBakeValidator` (Editor-only validation), and `AbilityRegistryPopulator` (automatic registry population at Editor time).

## Observable behavior

- Designer creates `AbilityAsset` via `Create > FrameSyncMoba > Ability > Ability Asset`
- Configures ability ID, name, cast model (Commit/HoldRelease/Channel/ActiveSignal), AimKind, stages, cooldown, and resource cost
- `AbilityAssetBakeValidator` runs on domain reload and asset import, reporting configuration errors to the Console
- `FrameSyncMoba > Bake All Ability Assets` menu item bakes all valid assets and populates `AbilityDefinitionRegistry`
- Invalid stage chains, missing stage definitions, and AimKind/CastModelDef mismatches are caught at Editor time

## In scope

1. `AbilityAsset` ScriptableObject in `FrameSyncMoba.Unit` assembly
2. `CastModelAuthoring` hierarchy: `CommitCastModelAuthoring`, `HoldReleaseCastModelAuthoring`, `ChannelCastModelAuthoring`, `ActiveSignalCastModelAuthoring`
3. `StageDefAuthoring` — serializable stage definition with `Bake()` producing runtime `StageDef`
4. `FrameSyncMoba.RuntimeConfig.Editor` assembly with:
   - `AbilityAssetBakeValidator` — validates stage chains, AimKind consistency, resource integrity
   - `AbilityRegistryPopulator` — `[InitializeOnLoad]` auto-validation, `[MenuItem]` batch bake, `AssetPostprocessor` on-import validation
5. `AbilityDefinitionRegistry.TryRegisterFromAsset(AbilityAsset)` — new public method
6. Tests: `AbilityAssetBakeTests`, `AbilityAssetValidationTests`, `AbilityRegistryPopulationTests`

## Out of scope

- Custom `StageDef` subclasses — default `RuntimePlaceholderStageDef` used
- Per-ability custom `StageDefAuthoring` subclasses
- `GlobalGameplayData.AbilityAssets` list
- Integration with `GameBootstrap.Awake()` for automatic runtime population

## New files (~700 lines)

| File | Lines |
|---|---|
| `Gameplay/Ability/AbilityAsset.cs` | ~260 |
| `RuntimeConfig/Editor/FrameSyncMoba.RuntimeConfig.Editor.asmdef` | ~20 |
| `RuntimeConfig/Editor/AbilityAssetBakeValidator.cs` | ~170 |
| `RuntimeConfig/Editor/AbilityRegistryPopulator.cs` | ~150 |
| `RuntimeConfig/Editor/Tests/FrameSyncMoba.RuntimeConfig.Editor.Tests.asmdef` | ~20 |
| `RuntimeConfig/Editor/Tests/AbilityBakeTests.cs` | ~140 |

## Modified files (~10 lines)

| File | Change |
|---|---|
| `Gameplay/Ability/AbilityDefinitionRegistry.cs` | +`TryRegisterFromAsset(AbilityAsset)` method |

## Public contract impact

- `AbilityAsset` — new public ScriptableObject type in `FrameSyncMoba.Unit`
- `CastModelAuthoring` and subclasses — new public serializable types
- `StageDefAuthoring` — new public serializable type
- `AbilityDefinitionRegistry.TryRegisterFromAsset(AbilityAsset)` — new public method
- `AbilityAssetBakeValidator.Validate(AbilityAsset)` — Editor-only public static
- `AbilityRegistryPopulator` — Editor-only public static class

No changes to existing public contracts. `AbilityDef`, `CastModelDef`, `StageDef`, `AimKind` unchanged.

## Snapshot / Checksum impact

None. Authoring pipeline only; no runtime serialization changes. Bake output (`AbilityDef`) already has existing snapshot path.

## Verification

- Unity compilation: PASSED
- EditMode tests: 427/427 passed (all existing + new AbilityBake tests)
- No existing tests modified or removed

## Design conformance

Strict — no deviation from `moba_ability_system_design_v15_2`.

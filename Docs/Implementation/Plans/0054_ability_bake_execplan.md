# ExecPlan 0054 — Ability Authoring Bake Pipeline Completion

> Parent: NEXT_CANDIDATES.md Candidate 0054
> Created: 2026-07-23
> Design authority: `moba_ability_system_design_v15_2.md` §5 (AbilityDef), §3 (CastModelDef), §Bake; `MOBA_Player_Input_Command_Module_Design_v1_1.md` §3; `DECISION_LOG.md` (fail early)

## Purpose

Complete the ability authoring pipeline so designers can create abilities via ScriptableObject without writing C#. Add Editor-time validation that catches invalid configurations at Bake time, and automatic `AbilityDefinitionRegistry` population.

## Observable behavior

- Designer creates `AbilityAsset` via CreateAssetMenu → fills CastModelDef, AimKind, stages, cooldown, cost
- `OnValidate` / Bake-time: circular stage transitions, missing resources, AimKind/CastModelDef mismatches logged as errors
- `AbilityDefinitionRegistry` automatically populated from all `AbilityAsset` instances at Editor time
- Valid ability → runtime `AbilityDef` correctly populated with all fields

## In scope

1. `AbilityAsset` ScriptableObject (RuntimeConfig asmdef) — authoring surface with CastModelDef, AimKind, stage definitions, cooldown, cost, display name.
2. `AbilityAssetBakeValidator` (RuntimeConfig.Editor asmdef, Editor-only) — validates stage chain integrity, AimKind/CastModelDef consistency, resource requirements.
3. `AbilityRegistryPopulator` (RuntimeConfig.Editor asmdef) — `[InitializeOnLoad]` populator that registers all `AbilityAsset` instances.
4. `AbilityDefinitionRegistry.RegisterFromAsset(AbilityAsset)` — new method.
5. `GlobalGameplayData` — add `AbilityAssets` list field for Bake-time collection.
6. Tests: EditMode validation (valid + invalid configs), registry population.

## Out of scope

- Visual ability curve editor (use Unity AnimationCurve)
- Final ability content — neutral test abilities only
- Per-hero ability assignment UI
- Stage-specific resource validation (deferred)

## New files (~380 lines production + ~120 lines test)

| File | Lines | Assembly |
|---|---|---|
| `RuntimeConfig/AbilityAsset.cs` | ~80 | FrameSyncMoba.RuntimeConfig |
| `RuntimeConfig/Editor/AbilityAssetBakeValidator.cs` | ~120 | FrameSyncMoba.RuntimeConfig.Editor |
| `RuntimeConfig/Editor/AbilityRegistryPopulator.cs` | ~80 | FrameSyncMoba.RuntimeConfig.Editor |
| `RuntimeConfig/Editor/FrameSyncMoba.RuntimeConfig.Editor.asmdef` | ~10 | — |
| `RuntimeConfig/Tests/AbilityAssetTests.cs` | ~120 | FrameSyncMoba.RuntimeConfig.Tests |

## Modified files (~90 lines)

| File | Change |
|---|---|
| `RuntimeConfig/GlobalGameplayData.cs` | +40: add `AbilityAssets` list, bake them into data |
| `Unit/AbilityDefinitionRegistry.cs` | +30: add `RegisterFromAsset(AbilityAsset)`, `TryGet` already exists |
| `RuntimeConfig/FrameSyncMoba.RuntimeConfig.asmdef` | +2: add `UnityEngine.UI` reference if needed for SO attribute |

## Public contracts

| Contract | Owner |
|---|---|
| `AbilityAsset` ScriptableObject | FrameSyncMoba.RuntimeConfig |
| `AbilityDefinitionRegistry.RegisterFromAsset(AbilityAsset)` | FrameSyncMoba.Unit |
| `AbilityAssetBakeValidator.Validate(AbilityAsset)` (Editor-only) | FrameSyncMoba.RuntimeConfig.Editor |

## Snapshot / Serialization / Checksum

None. Authoring pipeline only. Bake output (`AbilityDef`) already has existing snapshot path. No runtime serialization changes.

## Assembly strategy

- `AbilityAsset` in RuntimeConfig asmdef (references `Unity.Mathematics.FixedPoint` and `FrameSyncMoba.Unit` via existing reference chain... but wait — RuntimeConfig currently only references `Unity.Mathematics.FixedPoint`. Let me check if `AbilityDef` is accessible from RuntimeConfig.)
  
  Actually, `AbilityDef` is in `FrameSyncMoba.Unit` namespace. RuntimeConfig asmdef does NOT currently reference `FrameSyncMoba.Unit`. So `AbilityAsset` CANNOT reference `AbilityDef` directly from RuntimeConfig.

  Solution: `AbilityAsset` stores authoring data in RuntimeConfig-friendly types (enums, floats for fp conversion, serializable structs). The Bake step (which can reference both RuntimeConfig and Unit) converts to `AbilityDef`. But actually, looking at the existing pattern — `GlobalGameplayData` is in RuntimeConfig and doesn't reference Unit types at all. It uses its own authoring types.

  Revised: `AbilityAsset` stores `AbilityAssetAuthoring` data. Bake happens elsewhere. OR — put `AbilityAsset` in a different assembly that references both RuntimeConfig and Unit.

  Even simpler: put `AbilityAsset` in `FrameSyncMoba.Unit` since that's where `AbilityDef` lives, and Unit asmdef already references `Unity.Mathematics.FixedPoint`. But Unit asmdef might not have `UnityEngine.UI` for ScriptableObject.

  Actually, checking: Unit asmdef has `noEngineReferences: false` so it can reference UnityEngine types including ScriptableObject. Yes, this works.

  Revised: `AbilityAsset` goes in Unit asmdef (alongside `AbilityDef`). The Editor-only types go in a new Editor asmdef.

- `FrameSyncMoba.RuntimeConfig.Editor` — new Editor-only asmdef, references RuntimeConfig + Unit + UnityEditor.
- `AbilityRegistryPopulator` and `AbilityAssetBakeValidator` live here.

## Tests

- `AbilityAsset_ValidConfig_BakesCorrectly` (EditMode) — valid config → `AbilityDef` populated with all fields.
- `AbilityAsset_CircularStage_ValidationFails` (EditMode) — circular transition → error.
- `AbilityAsset_AimKindMismatch_ValidationFails` (EditMode) — Direction aim + SelfTarget cast model → error.
- `AbilityRegistry_PopulatesFromAssets` (EditMode) — registry contains entry after populate.

## Design conformance

Strict — no deviation. Adds authoring layer without modifying `AbilityDef`/`CastModelDef`/`AimKind` contracts. Validation fails early per DECISION_LOG.md.

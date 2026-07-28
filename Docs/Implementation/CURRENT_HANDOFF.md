# Current Handoff -- FrameSyncMobaDemo

> Last updated: 2026-07-28 after ExecPlan 0109 remediation.

## Current truth

- Unity 2022.3.62f1c1 is connected through MCP and compiles with 0 Console errors.
- The generic local runtime, snapshot/checksum path, Combat/Attack/Ability/
  Projectile/movement/non-hero slices and Equipment Shop transaction path are
  implemented.
- Authority bundle/relay/recovery and prediction limits are transport-neutral
  FrameSync code; NGO/UOS references remain Bootstrap-only.
- Shop UI submits canonical Equipment Commands and no longer writes Gameplay.
- Bootstrap EditMode passed 36/36 and the real FrameworkSmoke scene PlayMode
  check passed 1/1.
- Ability catalog/loadout ScriptableObjects were split into matching source
  files, their broken `m_Script: 0` references were repaired, and the smoke
  scene now has a valid serialized Ability catalog.

## Remaining boundary

- Live UOS/NGO service validation needs external provider configuration.
- Do not invent `EquipmentTargetPolicy`: the current design names it but does
  not define its enum values or exact target matching contract.
- Continue building generic framework capabilities only; production heroes,
  abilities, Buffs, equipment and map content remain excluded.

## Active plans

- Parent remediation: `0109_design_conformance_remediation_program_execplan.md`
- Authority/recovery: `0119_authority_frame_recovery_and_prediction_limits_execplan.md`
- UOS/NGO application: `0120_uos_ngo_application_and_lobby_flow_execplan.md`

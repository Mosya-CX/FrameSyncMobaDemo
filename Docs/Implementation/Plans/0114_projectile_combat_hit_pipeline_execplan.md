# 0114 Projectile-to-Combat Hit Pipeline

> Status: Complete. Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 5.

## Purpose

Make generic projectiles move and query deterministically, filter targets through
Projectile v19 rules, emit formal Combat requests, apply registered Buff/CC
effects, and end only through the staged lifecycle.

## Sources

- `Docs/Design/MOBA_FrameSync_Unity_Projectile_System_Design_v19.md`
- `Docs/Design/moba_combat_system_design_v13_2.md`
- `Docs/Design/FrameSync_MOBA_Physics_and_RangeQuery_System_Design_v13_1.md`

## Scope

- `ProjectileDef`, spawn/source state, hit filter/policy and snapshot state.
- `ProjectileWorld` motion/lifecycle/end phases.
- `ProjectileHitResolver` stable candidate ordering and shape-aware narrow phase.
- `ProjectileEffectDispatcher` Combat/Buff/CC routing.
- Pipeline composition, neutral authoring fixture and focused tests.

Out of scope: production abilities/content, advanced bounce modules, final VFX,
network transport and unrelated movement work.

## Invariants

- Order is ProjectileUid, then hit distance, then UnitUid.
- Hit modules never change HP directly.
- SourceDescriptor survives spawn/snapshot and drives Combat on-hit semantics.
- Pending hit/end buffers are consumed before Tick-end snapshot.
- Restore rejects missing definitions, owners and hit-memory targets.

## Implementation and validation

1. Freeze minimum target-filter/hit-policy/source contracts and strict registry validation.
2. Split motion, lifecycle, resolve, emit and destroy phases.
3. Route damage to Combat and Buff lookup to `UnitWorld.BuffDefinitions`.
4. Add shape-aware filtering, stable tie handling and snapshot fields.
5. Add Inspector-backed neutral projectile definition/prefab binding.
6. Compile with Unity MCP and run one focused behavior validation group.

Completion requires clean compilation, no direct projectile HP mutation, focused
filter/order/end/Combat/snapshot checks passing, and no production content.

## Progress / Results

- [x] Confirmed current designs and inspected the real runtime/pipeline.
- [x] Implemented the staged Projectile lifecycle, formal filters/hit policy,
  Combat/Buff/CC routing, snapshot/checksum state and neutral prefab/catalog.
- [x] Unity MCP compilation passed; six focused checks passed for stable order,
  filtering, end/pierce policy, shielded Combat damage and snapshot round trip.

No direct Projectile HP mutation remains. Advanced bounce/motion modules remain
out of scope.

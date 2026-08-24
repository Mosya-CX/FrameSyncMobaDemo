# ExecPlan 0140 — Action-keyed Crit and neutral Projectile ties

Plan ID: 0140
Status: Completed
Created: 2026-08-24
Completed: 2026-08-24
Risk: High
Design conformance: Strict (user-approved D-050 follow-up to completed ExecPlan 0139)
Estimated code delta: 1,000–1,800 lines including tests and formal docs
Actual code delta: implemented in the shared D-049/D-050 working tree; not isolated from ExecPlan 0139 by Git commit
Affected assemblies: FrameSyncMoba.Deterministic; FrameSyncMoba.Unit; FrameSyncMoba.FrameSync; FrameSyncMoba.Bootstrap/RuntimeConfig integration and focused tests
Design sources: Combat v13.2; Combat v13.3 fairness amendment; Combat v13.4 action-identity amendment; Projectile v19; Snapshot Appendix v7.2; DESIGN_INDEX.md
Decision dependencies: D-001; D-004; D-011; D-012; D-023; D-049; D-050
Validation basis: Unity 2022.3.62f1c1 through Unity MCP; focused deterministic/Unit/FrameSync EditMode tests; required projectile lifecycle PlayMode regression; independent High-risk review

## 1. Purpose

Make probabilistic Crit sampling and equal-distance Projectile target arbitration
independent of technical UnitUid/Prefab/registration order. Preserve the same
selected gameplay participant when only technical UIDs change, without consuming
the global random stream or weakening Snapshot/rollback equivalence.

## 2. Progress

- [x] Receive explicit user approval for the stronger identity-based fairness scope.
- [x] Resolve current Combat/Projectile/Snapshot authority and record a clean Unity baseline.
- [x] Freeze D-050/v13.4 and register this follow-up plan.
- [x] Add immutable GameplayParticipantId to authoritative Unit spawn/runtime/snapshot/checksum state.
- [x] Add OriginActionId/EffectOrdinal to probabilistic Damage and Projectile provenance.
- [x] Replace global-stream Crit sampling with pure action-keyed sampling.
- [x] Replace equal-distance TargetUnitUid priority with seeded participant scoring.
- [x] Update Deferred/Projectile snapshots, checksums, schema/data versions and focused tests.
- [x] Compile/test through Unity MCP and complete independent read-only review.
- [x] Update affected status/handoff documents and close this plan.

## 3. Repository facts and discoveries

- Initial authoring already has unique `StableSpawnOrder`; minion tickets have
  `SpawnLogicTick`, Team, Lane and `StableEntryIndex`; jungle members have Camp,
  respawn Tick and slot. These are sufficient formal spawn provenance inputs.
- Ability sessions already have per-caster snapshot-restored `SessionUid` and
  `StartLogicTick`. Attack state already has `AttackStartLogicTick` and a local
  sequence index. No global action allocator is required.
- Current Crit consumes `UnitWorld.RandomService` while Damage requests are
  evaluated in UID-canonical order. Current moving/AoE Projectile ties use
  TargetUnitUid after distance.
- Deferred Damage stores the complete DamageRequest, so extending the Header
  preserves identity without a second deferred protocol type.
- Unit and Projectile runtime identities are already exact Snapshot/checksum
  members. The new gameplay/action identity belongs beside them, not in
  presentation or Unity assets.

## 4. Design traceability

- D-050/v13.4 §2: participant provenance and uniqueness.
- D-050/v13.4 §3–4: action/effect identity and keyed Crit.
- D-050/v13.4 §5: distance-first seeded participant tie score.
- D-004/D-011/D-012 and v13.4 §6: exact Unit/Combat/Projectile Snapshot and checksum membership.
- v13.4 §7: UID relabel, random-stream isolation, seed corpus and failure tests.

## 5. Scope

### In scope

- Pure gameplay participant/action identity value types.
- Production Unit spawn provenance for initial units, minions, jungle members
  and current derived spawns.
- Basic attack and ability projectile action provenance; projectile damage
  effect ordinals.
- Keyed Crit and moving/AoE Projectile equal-distance scoring.
- Deferred, Unit and Projectile Snapshot/checksum/version changes.
- Focused deterministic, insertion/relabel, restore/replay and integration tests.

### Out of scope

- Changing non-equal geometric target priorities.
- Presentation identity, UI, networking transport or new packages.
- Replacing the global random service for mechanics other than Crit.
- New production heroes/equipment/content.

## 6. Public contracts and ownership

- `GameplayParticipantId`, `OriginActionId` and their domains are owned by
  FrameSyncMoba.Unit as Gameplay semantics.
- `UnitWorld` validates participant uniqueness; Unit owns the immutable runtime value.
- `CombatRequestHeader` owns action/effect provenance for Damage/Deferred Damage.
- Projectile spawn/runtime/snapshot owns inherited OriginActionId.
- `CombatSystem` owns keyed Crit sampling and exposes only immutable configured seed state needed by the Projectile resolver.
- Snapshot schema becomes 24 and GameplayDataVersion becomes 4; bootstrap wire 4 and Command schema 1 remain unchanged.

## 7. Implementation plan

1. Add identity value types and participant validation; wire formal spawn provenance.
2. Capture/restore/checksum Unit participant state and bump Snapshot schema.
3. Extend Combat headers, Deferred checksum and producer call sites with action/effect identity.
4. Implement allocation-free keyed Crit and prove isolation from global RNG position.
5. Carry action identity through Projectile pending/active runtime snapshots.
6. Apply distance-first seeded participant scoring to moving and AoE hit candidates.
7. Add identity/relabel/seed-corpus/deferred/restore/checksum tests and run regressions.
8. Complete independent review and update status evidence.

## 8. Validation

- Unity refresh/compilation and empty new Error/Exception Console results.
- Deterministic hash/identity tests.
- Unit Combat fairness, Crit, Deferred snapshot/checksum and participant failure tests.
- FrameSync moving Projectile, AoE, capped/piercing/falloff, Snapshot/Restore/Replay and checksum tests.
- Focused GameScene projectile/combat lifecycle PlayMode if source changes reach scene composition.
- Full affected EditMode assemblies after focused tests stabilize.
- No build command is part of this plan.

## 9. Independent review

A separate read-only review sub-agent receives D-050/v13.4, this plan, the final
diff and validation results. All P0/P1 determinism, identity collision,
Snapshot/checksum, lifecycle and fairness findings must be resolved or reported
before completion.

## 10. Failure and recovery

Identity fields are additive until version migration is complete. Missing or
duplicate restored identities fail visibly; restore never regenerates them from
UID. If a current production spawner lacks stable provenance, stop that producer
and record the exact source instead of silently using registration order.

## 11. Results

Implemented immutable gameplay participant identities, action/effect provenance,
pure match-seeded Crit sampling and participant-scored equal-distance Projectile
arbitration. Unit, Deferred Combat and pending/active Projectile Snapshot/checksum
membership now carries the new identities; Gameplay Snapshot schema is 24 and
GameplayDataVersion is 4. Event-derived damage folds the parent EffectOrdinal into
its child key, negative ordinals fail at Damage/Deferred/Restore boundaries, and
pending tracked projectiles preserve their target through rollback.

Final Unity evidence: clean compilation and empty Error/Exception Console query;
Deterministic 53/53, FrameSync 98/98, RuntimeConfig Editor 47/47, focused action
identity 8/8 and focused Projectile pipeline 13/13. Full Unit is 545 passed with
the same 10 retained unrelated baseline failures. Earlier in-plan PlayMode evidence
remains ClientContent Projectile binding 2/2, Unit prefab 1 passed/1 retained fixture
failure, and Bootstrap 27 passed/3 retained fixture failures. No build or upload was
performed.

The independent High-risk review found three P1 issues; all were fixed, covered by
tests and confirmed closed in a second read-only review. Remaining P2 risk is
theoretical 31-bit hash collision in compressed derived/effect identities and legacy
test helpers that still offer UID/order-derived fallback identities; production
participant registration fails visibly on an actual duplicate, and new fairness
tests use explicit identities.

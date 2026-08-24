# ExecPlan 0139 — Same-Tick Combat fairness and killer attribution

Plan ID: 0139
Status: Completed
Created: 2026-08-24
Completed: 2026-08-24
Risk: High
Design conformance: Approval required (approved by the 2026-08-24 user request; D-049 freezes the replacement semantics)
Estimated code delta: 1,500–2,500 lines including focused tests and formal documentation
Actual code delta: approximately 2,700 inserted / 222 removed lines including formal docs, tests and Unity `.meta` files
Affected assemblies: FrameSyncMoba.Deterministic; FrameSyncMoba.Unit; FrameSyncMoba.FrameSync; their EditMode tests; focused Bootstrap PlayMode tests if lifecycle coverage requires them
Design sources: Docs/Design/moba_combat_system_design_v13_2.md plus Docs/Design/moba_combat_system_design_v13_3_same_tick_fairness_amendment.md; Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md section 13.2; Docs/Design/unit_behavior_framework_design_v27_3.md; Docs/Architecture/DESIGN_INDEX.md
Decision dependencies: D-001; D-009; D-010; D-011; D-023; D-035 superseded in part; D-041 amended; D-049
Validation basis: Unity 2022.3.62f1c1 through Unity MCP; focused Unit and FrameSync EditMode tests; necessary lifecycle PlayMode tests; non-random insertion/technical-identity independence, rollback/replay and checksum equivalence

## 1. Purpose

Make accepted, non-random same-LogicTick Combat batches independent of implicit
first-writer priority from UnitUid, RuntimeEntityPrefabId, registration order,
team-side content numbering and the incidental order in which unit Handlers call
Combat submission APIs. UID remains valid for identity, canonical records and
explicit complete ties. Players receive the same health, shield, death and
non-tied killer result when only non-semantic technical ordering changes.

Killer ownership is the enemy hero with the greatest proportional effective life
damage in the lethal settlement batch. An exact tie uses a pure seeded hash of
immutable match facts and never consumes the authoritative random stream.

## 2. Progress

- [x] Confirm the user-approved fairness direction and killer scheme A with scheme C fallback.
- [x] Resolve Combat v13.2, D-035, D-041, current pipeline and assembly ownership.
- [x] Record a clean Unity MCP compilation/Console baseline and clean worktree.
- [x] Freeze the D-049 amendment and register this plan.
- [x] Add and run pre-change mirrored characterization coverage.
- [x] Split the unit-bundled Handler loop into global semantic subphases.
- [x] Introduce collect/seal/settle Combat waves and assign SequenceInTick only after sealing.
- [x] Add order-independent target-batch shield/heal/damage settlement and proportional effective-life allocation.
- [x] Replace last-event killer authority with lethal-batch highest effective life damage and seeded exact-tie resolution.
- [x] Migrate reactions, contribution logs and restore/capture guards; no deferred/Snapshot/checksum shape change was required.
- [x] Compile through Unity MCP and pass focused EditMode/required PlayMode validation.
- [x] Complete independent read-only High-risk review and resolve the Crit/Projectile scope boundaries by explicit user approval.
- [x] Update module status, current handoff and plan results.

## 3. Repository facts and discoveries

- `SimulationTickPipeline` currently advances every Handler for one UID before
  moving to the next UID. `UnitRegistry` orders by `UnitUid`; `UnitUid` compares
  spawn Tick, RuntimeEntityPrefabId and spawn sequence.
- Formal blue minion/tower PrefabIds are lower than their red equivalents, so the
  current order can correlate with team-side content numbering.
- `CombatSystem.SubmitShield`, `SubmitDamage` and `SubmitHeal` allocate the shared
  `SequenceInTick` immediately. Settlement therefore inherits call order.
- All ordinary unit Handler emission and projectile hit emission occur before the
  existing final Combat settlement. Already-submitted requests remain valid when
  their source enters Dying, which preserves same-Tick mutual lethal trades.
- Combat v13.2 and D-035 currently make the last effective Damage event the killer.
  D-041 grants last-hitting heroes the preferential gold share. D-049 replaces
  only this attribution rule; assist-window membership and reward ownership remain.
- `InitialRandomSeed` is already authoritative bootstrap/runtime configuration.
  Exact-tie scoring must use it as immutable input and must not call
  `DeterministicRandomService.Next*`.
- Active Combat queues and batch state must be empty before the one-Tick Snapshot
  boundary. Deferred requests remain the only cross-Tick request payload.
- The user approved the final scope boundary: random Crit sample-to-label mapping
  and Projectile v19's explicit equal-distance UID tie-break are not mirror
  invariants. UID is prohibited only as an implicit first-writer authority after
  Combat requests have entered the sealed settlement boundary.

## 4. Design sources and traceability

- Combat v13.2 sections 1.2, 2, 6–8, 10 and 12: request lifecycle,
  shield/damage/heal pipelines, Dying and Snapshot boundary.
- Combat v13.2 section 7.14 and D-035: contribution events and superseded
  last-effective-Damage killer clause.
- D-041: integer reward settlement; preferential killer share now consumes the
  D-049-selected killer.
- FrameSync v10.2 section 13.2: fixed global Tick order.
- Unit Framework v27.3: UnitWorld lifecycle and Handler ownership.
- D-049: global Handler subphases, sealed waves, batch-start evaluation,
  proportional effective life damage, seeded exact ties and the approved UID/
  random/Projectile boundary.

Critical tests:

- `CombatSameTickFairnessTests` protects insertion order, mirrored non-tied
  attribution, shield/heal/damage batches, mutual lethal/drain reactions,
  fixed-point raw-unit conservation, configured match seed and exact ties.
- `CombatSystemTests`, `AssistEventIntegrationTests` and
  `CombatContributionEventLogTests` protect formal death, assistant ordering
  and the event-log audit boundary.
- the complete `FrameSyncMoba.FrameSync.Tests` assembly protects pipeline,
  projectile, Snapshot/Restore/Replay and checksum behavior.

## 5. Scope

### In scope

- Global per-Handler Tick subphases in `SimulationTickPipeline`.
- Combat-local pending request envelopes, settlement waves and reusable buffers.
- Batch-start formula evaluation for same-target, same-wave requests.
- Explicit shield/heal/damage commit and reaction-after-batch semantics.
- Proportional attribution when a batch exceeds remaining health.
- Killer scheme A; scheme C exact-tie fallback.
- Contribution, death, reward, deferred, Snapshot/checksum and handshake changes
  proven necessary by the final public shape.
- Focused EditMode and lifecycle PlayMode verification.

### Out of scope

- New heroes, equipment, presentation, UI or network transport behavior.
- New packages or authored Unity assets.
- Dynamic rotating side priority or consumption of the Gameplay random stream.
- Unrelated Handler, modifier, projectile or gold refactors.

### Contract implications

- Combat request acceptance remains synchronous, but final active
  `SequenceInTick` becomes a sealed-settlement identity rather than submission order.
- Damage/heal/shield settlement and killer semantics change formally under D-049.
- `FormalDeathResult.KillerHeroUid`, rewards and KDA keep their shapes but consume
  the new killer resolver.
- Snapshot schema is bumped only if deferred or contribution snapshot semantics
  cannot remain canonical without a shape/meaning change.

## 6. Implementation plan

1. Add mirrored characterization fixtures without preserving biased expectations
   as final contracts. Record the pre-change matrix in this plan.
2. Replace the bundled Phase 3 unit loop with stable global loops for tags, Buff,
   Equipment, HitReaction, Ability, Movement and Attack; run focused regressions.
3. Add Combat-internal request metadata for wave, origin identity and effect ordinal.
   Submit APIs validate and collect; a seal operation creates target batches and
   assigns final SequenceInTick.
4. Keep NaturalRegen before active waves. Imported deferred and current ordinary
   requests form wave 0; reactions produced by a completed wave enter the next wave.
   UnitDeath/UnitKill ordinary requests remain deferred to T+1.
5. Freeze per-target batch-start health, shields and relevant stat/modifier reads.
   Evaluate shield/heal/damage results without observing sibling request writes.
6. Apply effective healing capped at MaxHealth, add eligible shields, allocate
   shield absorption by explicit shield policy, and apply aggregate life damage.
   Emit per-request results after commit in canonical origin order.
7. When candidate life damage exceeds available life, allocate actual life damage
   proportionally by fixed-point weight; distribute representational remainder by
   the seeded neutral score so conservation is exact and traversal-neutral.
8. Aggregate lethal-batch actual life damage per resolved owner hero. Select the
   maximum; exact ties use the non-consuming 64-bit seeded neutral score. No valid
   hero candidate yields an invalid killer as before.
9. Update contribution-event facts, death freezing, rewards, checksums, deferred
   serialization and schema/version guards. Preserve assist-window semantics.
10. Run full scoped validation, independent review and documentation/status updates.

## 7. Public contracts and ownership

- `CombatSystem` in `FrameSyncMoba.Unit` remains the only Combat request sequence,
  settlement, effective-damage and killer authority.
- Pending envelopes, target batches and proportional allocators are internal pure
  C# types owned by `FrameSyncMoba.Unit`.
- A pure integer hash/mix helper, if the existing `DeterministicHash32` surface is
  insufficient, belongs to `FrameSyncMoba.Deterministic` and consumes no RNG state.
- `FrameSyncGameRuntime` passes the immutable initial match seed into Combat
  composition. Bootstrap owns the source; Combat only reads the injected value.
- `UnitWorld` keeps LifeState transitions; `GoldIncomeRuntime` keeps currency writes;
  `MatchStatisticsRuntime` keeps KDA state.

## 8. Validation

- Unity MCP AssetDatabase refresh and script compilation; inspect Console errors
  and warnings after every coherent script batch.
- Focused EditMode: existing Combat, Assist, Attack, Buff, Equipment, Projectile,
  UnitRegistry/Uid, FrameSync pipeline, Snapshot and checksum tests.
- New fairness matrix: repeated equivalence; non-random technical UID/prefab/team/insertion independence;
  shield+damage; heal+damage; mutual lethal; reaction wave; proportional overkill;
  highest contributor; summon/projectile owner; no-hero source; exact tie.
- Exact-tie distribution over fixed seed corpus must not be permanently side- or
  Prefab-oriented; each individual seed remains replay-identical.
- Continuous versus Snapshot/Restore/Replay and command insertion-order equivalence.
- Necessary PlayMode: formal death/respawn/event lifecycle and minion/tower/projectile
  integration. No build command is part of this source plan.

## 9. Independent review

A separate review sub-agent received the D-049 amendment, this plan, source diff
and validation results. Its read-only audit found no remaining P0/P1 in the
non-random batch core after fixes for seed wiring, event/Dying ordering,
mutual-drain handling, fixed-point remainder capacity, tie identity and
shield-only drain. It identified Crit sample remapping and Projectile v19's
equal-distance UID rule only under the earlier universal relabel invariant. The
user-approved scope decision classifies both as valid deterministic random/tie
behavior, so no P0/P1 remains for this plan.

## 10. Failure and recovery

Each milestone remains compilable and testable. If sealed waves or batch commits
cannot preserve a current formal mechanic, stop that affected mechanic, record the
exact conflict and keep unaffected characterization/global-subphase work. Do not
silently restore submission-order authority. Active queues and target-batch scratch
must fail Capture if nonempty; no partial settlement is snapshotted.

## 11. Results

Implemented and verified deterministic batch core:

- Pre-change characterization proved the bias: at 50 Health, same-Tick
  Damage 80 + Heal 50 ended at either 50 or 20 by Submit order; Damage 80 +
  Shield 50 ended Dead or Alive at 20. Both are now insertion-order invariant
  and end Alive at 20 under the frozen Shield -> Heal -> Damage wave stages.
- Pipeline Handler execution is global by subsystem. Combat collects active
  requests, canonically seals bounded waves, commits target state before
  publishing results, and sends Result/Dying reactions to the next wave.
- Damage reads the frozen wave-start target-health operand. Shield, heal and
  life allocation conserve fixed-point values; raw-unit tests prove no request
  receives more effective value than its capacity.
- LifeSteal/Omnivamp now consume `ActualLifeDamage` as required by Combat
  v13.2; shield-only damage cannot create drain healing. The first authoritative
  bootstrap seed overrides the constructor fallback, repeated identical
  configuration is idempotent and a later different seed fails visibly.
- Scheme A owns `DeathResult.KillerHeroUid`: highest summed proportional
  `ActualLifeDamage` wins. Shield-only, zero and pure-overkill values cannot
  win. Assists remain event-window based and rewards consume the unchanged
  DeathResult contract.
- Scheme C uses the runtime-configured match seed and Prefab-neutral Spawn
  identities, consumes no deterministic random-stream state, replays stably
  for one seed and is not permanently UID-side biased across the fixed corpus.
- New wave, emission, allocation and lethal-candidate state is transient,
  cleared on Restore/Tick end and asserted empty before Capture. The retained
  `LastHitContributorUid` remains an audit fact. Snapshot schema 23,
  GameplayDataVersion 3 and bootstrap wire 4 are unchanged.

UnityMCP evidence:

- compilation succeeded; final Console Error query is empty;
- Deterministic EditMode 53/53;
- FrameSync EditMode 91/91;
- same-Tick fairness 15/15;
- CombatSystem 14/14, contribution log 5/5, assist integration 2/2 and gold
  reward 5/5;
- full Unit EditMode 536 passed with the exact 10 retained baseline failures;
- GameScene first-wave/tower combat/match closure and map-root PlayMode 2/2.

Independent High-risk review closed its seed wiring, Dying/event ordering,
mutual-drain and fixed-point remainder findings. The user then approved the
final scope decision: Crit keeps its snapshot-restorable global random stream
and only promises replay equivalence for the same actual match state;
Projectile v19 keeps `TargetUnitUid` as its explicit equal-distance target
tie-breaker. Neither requires invariance under artificial UID relabeling.

After those mechanics submit accepted Combat requests, UID cannot become an
implicit first-writer advantage: sealed waves, batch commits, scheme A killer
selection and scheme C exact-tie fallback remain order-neutral within their
declared scope. No new public identity contract, Snapshot/wire/schema change or
remaining P0/P1 is required. ExecPlan 0139 is complete.

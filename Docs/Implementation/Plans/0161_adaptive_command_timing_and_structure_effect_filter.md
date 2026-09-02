# ExecPlan 0161 — Adaptive Command timing and structure effect filter

Plan ID: 0161
Status: Completed
Created: 2026-09-01
Completed: 2026-09-02
Risk: High
Design conformance: User-approved amendments required
Estimated code delta: 350–650 lines across runtime, focused tests, and current design evidence
Actual code delta: 1,917 added / 101 removed text lines plus Unity `.meta` files
Affected assemblies: FrameSyncMoba.FrameSync; FrameSyncMoba.Bootstrap; FrameSyncMoba.PlayerInput.Tests; FrameSyncMoba.Bootstrap.EditModeTests; FrameSyncMoba.Unit; FrameSyncMoba.Unit.Tests
Design sources: `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md` §8/§9.4; `Docs/Design/moba_combat_system_design_v13_2.md`; `Docs/Design/BuffSystem_Design_v14_2_PermanentBuffRespawnPatch.md`; `Docs/Design/moba_attack_module_design_v6_2.md`
Decision dependencies: D-001; D-045; D-049; D-050; current user approval on 2026-09-01
Validation basis: Unity compilation/Console, focused EditMode timing/PlayerInput/Gameplay tests, relevant PlayMode input integration, independent read-only review

## 1. Purpose

Make client-created Commands select one stable TargetTick per local build Tick
from integer smoothed RTT, integer RTT variation, an estimated current server
Tick and bounded safety margins, while retaining the current static formula as
a cold-start/stale-sample fallback. Separately, enforce the player-visible rule
that structures accept ordinary attack damage but reject externally sourced
ability, on-hit, equipment, Buff, heal and shield effects, including Varus W
Blight and its presentation mark.

## 2. Progress

- [x] Resolve current formal authority, repository state, assembly direction and Unity baseline.
- [x] Add integer network timing sample/estimate contracts and tests.
- [x] Carry a server Gameplay Tick anchor in the presentation ping response and wire the estimator to client Command creation.
- [x] Extend TargetTick resolution with fallback, bounds and one-decision-per-build-Tick caching.
- [x] Add the central structure external-effect policy and update conflicting Varus/formal structure tests.
- [x] Update the user-approved formal amendments and current state evidence.
- [x] Compile in Unity, inspect Console and run focused EditMode/PlayMode tests.
- [x] Complete independent read-only High-risk review and resolve all P0/P1 findings.

## 3. Repository facts and discoveries

- `PresentationPingTracker` currently records only the latest raw RTT; Ping is
  `NetworkDelivery.Unreliable`, while Command bundles use
  `ReliableSequenced`.
- Ping response v1 echoes only the request sequence. It has no server Gameplay
  Tick anchor, sample count, smoothed RTT or variation.
- `CommandTargetTickResolver` currently implements only
  `max(LocalSimulationTick + 1, LatestSynchronizedServerTick + MinCommandLeadTicks)`.
- The server retargets late Commands to its current Tick and throws when a
  Command exceeds `serverTick + MaxFutureCommandTicks`.
- Formal runtime configuration is 50 Tick/s, minimum lead 1, local/server
  future window 12 and maximum prediction lead 6.
- Varus W applies bonus damage as `CombatSourceType.AttackEffect` and applies
  Blight directly through `BuffHandler`; the existing test explicitly expects
  both to affect structures.
- Existing formal structure tests also expect selected abilities/projectiles
  to target structures. The current user request explicitly supersedes that
  behavior for externally sourced non-ordinary-attack effects.
- The worktree contains unrelated Addressables and temporary-PDF changes. They
  are excluded from this plan and must remain untouched.

## 4. Design sources and traceability

- FrameSync v10.2 §8 owns Tick meanings. New estimates must not redefine
  `ServerTick`, `LatestAuthorityFrameTick` or `LocalSimulationTick`.
- FrameSync v10.2 §9.4 owns TargetTick. The user-approved amendment retains
  both existing lower bounds and adds a client transport estimate candidate.
- D-045 keeps Commands Tick-based and permits monotonic integer milliseconds
  for Ping/network timing. RTT state never enters Snapshot or checksum.
- Combat v13.2 and D-049/D-050 retain canonical request identity/order. The
  structure gate runs before queue insertion, so rejected effects cannot alter
  settlement ordering.
- Buff v14.2 retains owner/self initial Buff lifecycle. Only external sources
  are rejected for structures.
- Timing proofs: `PresentationPingTrackerTests`, TargetTick resolver tests and
  PlayerInput same-build-Tick tests.
- Structure proofs: central structure policy tests plus updated Varus Blight
  regression tests.

## 5. Scope

In scope:

- integer EWMA SRTT (`1/8`) and RTT variation (`1/4`);
- minimum sample count, freshness, jitter and processing margins;
- ping response server-Tick anchor with an explicit message-version bump;
- loaded/ready launch-barrier sampling before the first Gameplay Command;
- adaptive TargetTick candidate, explicit local/server future-window capping
  and stable caching;
- cold-start/stale timing fallback to the current static formula;
- external Damage/Heal/Shield/Buff/CrowdControl filtering for
  `UnitKind.Structure`, with only canonical basic-attack damage allowed;
- preservation of structure self-owned initialization/lifecycle effects;
- focused tests and current formal/status evidence.

Out of scope:

- Snapshot/checksum membership changes;
- Command canonical-byte/schema changes;
- RTT state replication or replay;
- transport delivery-mode changes;
- a full reliable Command arrival-slack acknowledgement controller;
- unrelated hero/content balance or Addressables repair;
- Player builds and live UOS multi-process acceptance.

## 6. Implementation plan

1. Add transport-neutral integer timing snapshot/provider contracts in the
   FrameSync assembly and extend `CommandTargetTickResolver` without changing
   existing constructor call sites.
2. Extend the Bootstrap ping tracker and response payload to sample SRTT,
   variation and a server-Tick anchor, then implement the timing provider on
   `FrameSyncNetworkBridge` and wire it from `GameBootstrap`.
3. Add focused tracker/resolver tests for initialization, EWMA, stale samples,
   integer Tick ceiling, fallback, bounds and same-build-Tick stability.
4. Add one central deterministic structure effect policy, call it at Combat
   queue and Buff admission boundaries, and update the Varus W regression.
5. Update current formal design/decision evidence and project status.
6. Compile, inspect Console, run focused tests, then request independent review.

## 7. Public contracts and ownership

- FrameSync owns the transport-neutral Command timing snapshot/provider
  contract consumed by `CommandTargetTickResolver`.
- Bootstrap owns Ping cadence, wall-clock sampling, server-Tick anchoring and
  the NGO message payload. No NGO type enters FrameSync or Gameplay.
- Gameplay owns the structure effect-admission policy because it applies to
  all effect producers, not only Varus.
- Ping message v2 is incompatible with v1 by named-message version. Matching
  client/server builds are required.
- No Command, Snapshot, checksum, UID or fixed-point wire type changes.

## 8. Validation

- Unity forced script refresh/compilation and final Error/Exception Console query.
- Bootstrap EditMode timing tracker tests.
- PlayerInput EditMode TargetTick/focus-commit tests.
- Unit EditMode structure effect and Varus Blight tests.
- Relevant Bootstrap PlayMode input simulation if the affected fixture remains healthy.
- Diff review for integer-only authoritative Tick computation, stable bounds,
  no per-Tick allocation and unchanged canonical Command bytes.

## 9. Independent review

The separate read-only High-risk review completed on 2026-09-02 with no open
P0/P1. Its findings led to: a real cache-validity flag; consumed-no-op Damage
admission; handlerless Buff/CC safety; explicit invalid projectile Buff/CC
failure; deferred filtering before sequence/Snapshot mutation; adaptive-bound
wording/tests; launch-barrier server-Tick freeze clamping; and Bind-before/
after-commit lifecycle caching. Remaining non-blocking coverage gaps are a full
NGO multi-process warmup lifecycle run and a rebuilt client/server latency run.

## 10. Failure and recovery

Changes are source/assets-only and remain recoverable through Git. If the new
timing estimator is unavailable, under-sampled or stale, TargetTick falls back
to the existing formal static formula. If Unity MCP reload interrupts a test,
resume with the same focused test filter after compilation becomes idle. Live
UOS and rebuilt-player acceptance remain external.

## 11. Results

- Ping named messages advance to v2 and the response now includes the server
  Gameplay Tick. Client-only integer SRTT/RTT variation produces a fresh,
  minimum-sample timing snapshot; FrameSync uses it only while creating a new
  Command header.
- Connected clients collect those samples during the loaded/ready launch
  barrier. Command send, recovery and Gameplay simulation remain behind their
  original active-game gates, while the first Command can use warm samples.
- TargetTick preserves the static v10.2 lower bound, reuses one decision per
  local build Tick, and deliberately caps the adaptive candidate to local and
  estimated-server future windows. Command/Snapshot/checksum schemas did not
  change.
- Structures accept external damage only for the canonical
  `Attack + BasicAttack` source pair. Other external Damage, Heal, Shield, Buff
  and CrowdControl/forced movement are rejected centrally; self-owned effects
  remain legal. Formal Varus E and ability projectiles 106/107/108 exclude
  structures in both catalogs. Deferred rejections occur before sequence
  allocation/Snapshot storage, and Corruption Vine tag writes share the same
  structure policy.
- Unity forced compilation completed with an empty Error Console. EditMode:
  FrameSync `132/132`, Bootstrap `131/131`, PlayerInput `42/42`, Unit
  `570 passed / 10 unchanged retained failures`; focused estimator/resolver
  `12/12`, structure policy `14/14`, projectile pipeline `16/16`, formal
  structure assets `7/7`, Blight/on-hit `8/8`. PlayMode: rejected structure
  Blight mark `1/1`, PlayerInput simulation `5/5`. Broad Unit PlayMode remains
  `2 passed / 1 retained PrefabId-range failure` outside this change.
- Matching rebuilt client/server latency behavior and live UOS observation are
  external acceptance; no Player build was requested or sent.

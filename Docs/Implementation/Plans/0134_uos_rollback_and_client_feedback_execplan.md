# ExecPlan 0134 - UOS rollback correction and client feedback

> Status: Complete in source (updated 2026-08-14 with D-044 two-phase launch);
> rebuilt two-client UOS acceptance remains pending.

## Goal

Repair the two-client failure observed after both controlled heroes had died and
client 2 purchased two equipment items, then complete the requested client
feedback fixes without introducing new authoritative presentation state.

The slice covers:

- rollback Restore/Resolve/Rebuild ownership after a remote shop command;
- checksum-segment diagnostics for future shop-command desyncs;
- Aatrox basic-attack presentation after an empowered attack;
- attack reach against the target collision boundary;
- live matchmaking state/time, staged loading feedback, and presentation-only
  round-trip ping at a configurable cadence;
- a compile-selectable bounded asynchronous diagnostic channel shared by UOS
  clients and the Dedicated Server.

No package is part of this plan.

## 2026-08-14 startup follow-up

Timestamped UOS logs proved the server ran normally at 30 Hz and began Tick 3
within about 15 ms of the client. The client then advanced from Tick 3 to about
Tick 1080 in roughly 2.6 seconds, so the visible 30-second jump was local
prediction runaway rather than network latency.

The startup flow now uses `SceneLoaded/Ready -> GameBootstrapPayload ->
BootstrapApplied -> MatchLaunchCommit -> Tick`. The server's five-second delay
starts only after all frozen clients have applied the bootstrap. Clients derive
their remaining wait from the absolute commit time and begin up to the small
configured prediction lead early. An independent wall-clock Tick ceiling makes
the startup burst impossible even if prediction-lead bookkeeping regresses.

Focused verification added on 2026-08-14:

- Bootstrap EditMode namespace: 85 passed, 0 failed.
- FrameSync EditMode namespace: 87 passed, 0 failed.
- `MatchLaunchProtocolTests`: canonical codec, frozen-roster barrier, duplicate
  idempotence and invalid confirmation rejection passed.
- `FrameSyncLaunchScheduleTests`: transit/lead wait, exact early boundary and
  runaway ceiling passed.
- `GameBootstrapPlayModeTests.ClientComposition_InitializesFromProjectAssets`:
  passed.
- Full Bootstrap PlayMode: 24 passed and 3 failed because the existing formal
  scene configuration reports `Initial spawn 1 team disagrees with SpawnPoint
  1`; that unrelated map/spawn fixture issue is not changed by this slice.

## Authoritative inputs

- `Docs/Architecture/DECISION_LOG.md`
- `Docs/Architecture/DESIGN_INDEX.md`
- `Docs/Design/moba_attack_module_design_v6_2.md`
- `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`
- `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`
- `Docs/Design/MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md`
- retained UOS server/client logs from the 2026-08-13 two-client run.

## Diagnosis

The retained logs show that client 2's first purchase was accepted at Tick
5845. At Tick 5881 client 1 had canonical command bytes identical to the server
but retained a different shared Gameplay checksum. Its world dump contained the
two expected items and their stat handles, so the failure was not a missing
purchase or a zero-price command.

`SimulationTickPipeline.RestoreFromSnapshot` already owns all three formal
restore phases, including `NonHeroRestoreHelper.ResolveNonHero` and
`RebuildNonHero`. `PredictionRollbackCoordinator.CorrectAndReplay` then invoked
those two non-hero phases a second time. That made correction replay execute a
different lifecycle from continuous authority simulation and was especially
relevant after both hero death/respawn paths had run. The coordinator duplicate
calls are removed; its explicitly registered external restore participants are
still invoked once.

The old package did not emit server-side per-segment checksums. Shop-command
Ticks now log every formal checksum segment on authority and replay endpoints,
and a persistent mismatch writes the local segment set with the world dump.
This makes any remaining live mismatch attributable to an exact snapshot
owner instead of another whole-world guess.

## Implementation decisions

- Gameplay restore remains `Restore -> Resolve -> Rebuild`, exactly once per
  participant. The coordinator does not repeat phases already owned by the
  pipeline.
- Attack range is `center distance - target radius <= attack range`; source
  radius is intentionally excluded.
- Animation attack-start detection uses the snapshotted attack start Tick.
  The deterministic sequence is mapped modulo the finite authored attack-state
  count, so empowered attacks do not leave the Animator on a non-existent
  monotonically increasing variant.
- Match and loading clocks are presentation-only and never enter Gameplay
  snapshots or checksums.
- Ping uses a named unreliable request/echo message and Unity realtime only.
  Its default refresh interval is 0.5 seconds and is serialized/configurable.
- `FRAME_SYNC_MOBA_DIAGNOSTICS` is supplied per build rather than through
  global PlayerSettings. With it absent, Conditional call sites disappear and
  no diagnostic worker or Unity-log subscription starts.
- Enabled endpoints enqueue into bounded normal/priority queues. A dedicated
  below-normal worker batches owned-file output; explicit diagnostics mirror to
  stdout for UOS server collection. Queue pressure drops and counts messages
  rather than blocking Gameplay.

## Verification

- Unity MCP compilation: passing; final Console error query is empty.
- `EquipmentShopTransactionTests.SequentialPurchases_AfterSnapshotRestore_MatchContinuousState`: passed.
- `AuthorityReplicationTests.Replicator_AndClientCoordinator_AcceptContinuousTick`: passed.
- `AttackHandlerTests.AttackRange_IncludesTargetCollisionRadiusOnly`: passed.
- `UnitPrefabAnimatorTopologyTests.AttackSequence_MapsOntoAuthoredAnimationVariantCount`: passed.
- `PresentationPingTrackerTests`: 2/2 passed.
- `GameBootstrapPlayModeTests.ClientComposition_InitializesFromProjectAssets`: passed.
- `FrameSyncDiagnosticsTests`: 4/4 passed.
- `FrameSyncDiagnosticBuildOptionsTests`: 1/1 passed.
- Client and Dedicated Server bootstrap lifecycle PlayMode tests: 2/2 passed;
  both created, populated and cleanly flushed endpoint diagnostic files.
- Full EditMode baseline: 875 discovered, 865 passed, 10 known pre-existing failures unrelated to this slice.

## Remaining acceptance

A new client/server build and the exact two-client sequence are required to
prove the old live Tick-5881 mismatch is gone. If it remains, compare
`[ChecksumSegment]` lines for that shop-command Tick between the server and the
failing client. The retained old server log cannot provide this comparison
retroactively.

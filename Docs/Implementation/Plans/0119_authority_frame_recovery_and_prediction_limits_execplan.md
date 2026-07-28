# ExecPlan 0119: AuthorityFrame recovery and prediction limits

> Status: Completed.
> Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 10A.

## 1. Purpose

Deliver a transport-independent FrameSync authority loop: canonical command
bundles become stable per-Tick relays, the Dedicated Server produces and retains
one `AuthorityFrame` per Tick, clients accept frames continuously, recover gaps
with sequence-checked requests, and stop prediction at configured lead or match
end boundaries.

## 2. Progress

- [x] Re-read FrameSync v10.2 sections 7-12 and 15.
- [x] Inspect current command codec, coordinator, pipeline and baked config.
- [x] Implement command bundle/relay and server authority-frame archive.
- [x] Enforce prediction lead, match candidate and recovery retry/sequence rules.
- [x] Add focused EditMode behavior tests.
- [x] Compile through Unity MCP, inspect Console, run focused tests and close.

## 3. Surprises and discoveries

- `FrameSyncSettingsAuthoring` declares prediction/recovery settings, but
  `BakedGlobalGameplayData` currently drops them.
- Recovery responses sort the caller-owned array in place and accept any
  `RequestSequence`.
- No production authority-frame replicator or focused authority/recovery tests
  exist.
- The formal Command enum is present, but payload/dispatch support beyond
  Move/Attack/Cast/Cancel is incomplete. This slice will not fabricate
  placeholder Gameplay behavior; completion of those Gameplay-owned operations
  remains a separate touched-contract repair.

## 4. Decision log

- Keep all transport APIs behind delegates/interfaces in FrameSync. NGO/UOS
  references belong only to the following application-flow child slice.
- Clone all wire arrays on construction and never mutate caller-owned buffers.
- Recovery only returns retained `AuthorityFrame` values; absence of a requested
  frame is a terminal recovery failure, not a base-snapshot fallback.

## 5. Current repository context

- Assembly: `FrameSyncMoba.FrameSync`.
- Existing contracts: `GameplayCommand`, `CommandCollector`, `AuthorityFrame`,
  `PredictionRollbackCoordinator`, `SimulationTickPipeline`, `SnapshotStore`.
- Config: `Assets/Scripts/RuntimeConfig/GlobalGameplayData.cs`.
- Tests: `Assets/Scripts/FrameSync/Tests`.

## 6. Design sources

- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`,
  sections 7-12, 15 and 17.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`, snapshot Tick and
  non-snapshot network-state rules.

## 7. Scope

In scope:

- `GameplayCommandBundle`, `AcceptedCommandRelay`, relay buffer and
  `AuthorityFrameReplicator`.
- Recovery archive/coordinator with retry, attempt and request-sequence checks.
- Prediction lead and predicted match-end pause control.
- Bake/validation of existing FrameSync settings.
- Pure/EditMode tests.

Out of scope:

- NGO serialization/RPCs, UOS allocation/matchmaking, scenes and account UI.
- New Packages or transport dependencies in Gameplay/FrameSync.
- New Gameplay semantics for unimplemented equipment/skill commands.
- Production content.

## 8. Implementation plan

1. Add immutable cloned wire contracts and deterministic relay aggregation.
2. Add the server Tick owner/replicator and bounded authority archive.
3. Correct coordinator recovery ownership, response sequencing and prediction
   pause behavior.
4. Bake the existing prediction/recovery configuration and wire it into
   `FrameSyncGameRuntime`.
5. Add focused protocol, recovery and prediction-boundary tests.

## 9. Public contracts

Add `GameplayCommandBundle`, `AcceptedCommandRelay`,
`AuthorityFrameReplicator`, `AuthorityRecoveryArchive` and
`AuthorityRecoveryCoordinator`. Extend `BakedGlobalGameplayData` only with
already-authored FrameSync fields. No UID, Command, Snapshot, Aim,
AbilitySignal, Checksum or fixed-point type is duplicated.

## 10. Validation

- Unity MCP refresh/compile and Console inspection.
- EditMode: canonical relay replacement, continuous authority acceptance,
  missing-frame recovery and response-sequence rejection, lead-limit pause,
  caller-array immutability and unavailable-anchor failure.
- No PlayMode test: this slice has no GameObject, scene, Input or presentation
  behavior.

## 11. Failure and recovery

The new services are transport-independent files. If compilation fails, resume
from the first incomplete Progress item. Do not add SDK references or weaken
protocol validation.

## 12. Results

Completed on 2026-07-28.

- Added cloned canonical `GameplayCommandBundle` and
  revisioned `AcceptedCommandRelay` contracts.
- Added stable per-Tick relay replacement, server AuthorityFrame production,
  bounded recovery archive and request-sequence/retry/attempt enforcement.
- Baked the existing prediction/recovery settings and exposed the limits through
  `FrameSyncGameRuntime`.
- Expanded the one authoritative `GameplayCommand` union and codec for skill
  allocation, shop, slot swap and item use; no parallel Command schema exists.
- Unity MCP compilation finished with 0 Console errors.
- `AuthorityReplicationTests`: 8/8 passed. No PlayMode test was required.

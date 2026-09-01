# ExecPlan 0152 — Command Bundle Idempotency and Send Gating

Plan ID: 0152
Status: Completed
Created: 2026-08-31
Completed: 2026-08-31
Risk: High
Design conformance: Strict
Estimated code delta: 180-360 lines across FrameSync, Bootstrap, focused tests
and current implementation documentation
Actual code delta: approximately 520 task-scoped lines across eight source/test
files plus two Unity meta files; several tracked files also contain preserved
earlier worktree changes, so repository-wide diff totals are not attributed to
this plan
Affected assemblies: FrameSync, Bootstrap, FrameSync EditMode tests, Bootstrap
EditMode tests
Design sources: FrameSync v10.2 §§10.2-10.6; Player Input v1.1
§§9.1-9.3, 17.4
Decision dependencies: D-029 and the frozen canonical Command-byte authority
rules
Validation basis: 2026-08-31 UOS ClientA/ClientB command transport and Toggle
traces; Unity compilation/Console; focused EditMode tests; final independent
read-only review

## 1. Purpose

Ensure one physical input can produce at most one authoritative Gameplay effect
even when its reliable Bundle is observed more than once or arrives after its
original TargetTick, while preserving two intentional consecutive inputs with
different CommandSeq values. Stop the client from creating a new reliable
Bundle every Unity Update when the canonical pending-command content has not
changed.

## 2. Progress

- [x] Correlate the live W reproduction across input, Bundle, Relay,
  AuthorityFrame and Ability Toggle transitions.
- [x] Confirm the server currently scopes `ClientId + CommandSeq` deduplication
  to one Tick and that client sending is scheduled per Unity Update.
- [x] Introduce one FrameSync-owned command identity and match-scoped server
  acceptance ledger.
- [x] Gate client Bundle construction by CommandCollector content changes and
  send only identities not already queued successfully.
- [x] Add focused duplicate-after-freeze, adjacent-input and send-gate tests.
- [x] Compile through Unity, run focused/broad regressions, review the final
  diff and update current implementation state.

## 3. Repository facts and discoveries

- In the 11:39 UOS ClientB log, one physical W press created seq 2 for Tick
  136. The client emitted that identity in BundleSeq 11 through 21. Authority
  executed seq 2 at Tick 136 and again at Tick 138, toggling Varus W on and off.
  Both clients received the same two AuthorityFrames, so this is authoritative
  duplication rather than a HUD-only projection defect.
- `GameBootstrap.Update` calls `FrameSyncNetworkBridge.SendLocalCommands`
  before elapsed-time Gameplay advancement. Multiple Unity Updates can occur
  while `Runtime.CurrentTick` is unchanged, especially while prediction lead
  pacing blocks another local Tick.
- Command Bundles use NGO `ReliableSequenced`; repeated identical application-
  level sends are not required for loss recovery.
- `CommandRelayBuffer.TickRelayState` owns the current accepted-identity set.
  Once a Tick is frozen and removed, a later Bundle can retarget the same
  identity to the current server Tick and accept it again.
- FrameSync v10.2 §10.2 requires duplicate network packets to be deduplicated
  by `ClientId + CommandSeq`, independent of Payload or TargetTick.
- The first independent review found one P1: if duplicate recognition happened
  after authorization, a previously accepted command could throw after its
  owner died or despawned. The final implementation checks the match-scoped
  identity ledger before retargeting and authorization; a focused regression
  verifies that the duplicate performs zero authorization calls.

## 4. Design sources and traceability

- FrameSync v10.2 §10.2: `ClientId + CommandSeq` is the duplicate network input
  identity. Protected by a same-identity/new-Bundle/after-Freeze regression.
- FrameSync v10.2 §§9.4 and 10.5-10.6: a genuinely late, not-yet-accepted
  command may be retargeted and relayed; a previously accepted identity may
  not be retargeted into a second effect. Protected by retaining the existing
  late-command test beside the new duplicate test.
- FrameSync v10.2 §10.3: distinct CommandSeq values remain canonical commands
  even on adjacent Ticks. Protected by an adjacent W Commit identity test.
- Player Input v1.1: every accepted physical action uses the shared monotonic
  CommandSeq owner. No QWER-specific server branch or ability-specific dedupe
  is introduced.

## 5. Scope

### In scope

- FrameSync-owned immutable `ClientId + CommandSeq` identity value.
- Match-scoped server accepted-identity ledger.
- Client-side command-content revision/send ledger and unchanged-send skip.
- Focused deterministic and transport-boundary unit tests.
- Current handoff/module-status evidence.

### Out of scope

- Wire/schema, Snapshot, checksum or rollback-boundary changes.
- Changing TargetTick calculation, Ability Toggle semantics or same-Tick cast
  merge policy.
- NGO delivery-mode changes, packaging or unrelated network refactors.

Snapshot/serialization/checksum implications: none. The ledgers are transport-
application state, reset with their owning runtime/bridge and excluded from
authoritative Gameplay state.

## 6. Implementation plan

1. Add the authoritative immutable Gameplay command identity in FrameSync and
   reuse it in both server and client ledgers.
2. Move server acceptance memory from `TickRelayState` to
   `CommandRelayBuffer`; check it before late retargeting and retain it after
   `FreezeTick`.
3. Add a monotonic content revision to `CommandCollector`, advancing only when
   its canonical pending content is mutated.
4. Add a Bootstrap-owned send ledger that skips an unchanged revision and,
   after successful reliable send, suppresses already-sent identities if the
   collector is rebuilt by prediction/rollback.
5. Test cross-Tick duplicate suppression, legitimate consecutive sequences,
   genuine first-arrival late retargeting and unchanged/rebuilt client send
   behavior.

## 7. Public contracts and ownership

- `GameplayCommandIdentity` is owned by FrameSync and consists only of
  `ClientId` and `CommandSeq`; it is not serialized independently.
- `CommandCollector.ContentRevision` is a local mutation observation token. It
  is not Gameplay time, Snapshot state, checksum input or wire data.
- `GameplayCommandSendLedger` is Bootstrap-internal and owns only client
  application-send history.
- `CommandRelayBuffer` remains the sole server acceptance owner.

## 8. Validation

- Unity synchronous refresh/compilation and isolated final Console Error
  query.
- FrameSync EditMode: duplicate same Bundle, late first arrival, accepted then
  frozen then repeated late identity, and distinct adjacent CommandSeq values.
- Bootstrap EditMode: unchanged collector produces one candidate only; a new
  CommandSeq produces one new candidate; consume/restore of a sent identity is
  not resent.
- Existing authority replication and affected command collector suites.
- No PlayMode test is required because no scene, asset or MonoBehaviour
  lifecycle contract changes; the bridge helper is pure and tested in
  EditMode.

## 9. Independent review

Run one independent read-only review after implementation and all fixes. Per
the user's review policy, run a second review only if the first reports a P0 or
multiple P1 findings.

The first review reported no P0, one P1 and no other P1/P2. The P1 was fixed by
moving accepted-identity recognition ahead of retargeting and authorization,
and its regression passes. A second review was therefore not run under the
user-approved threshold.

## 10. Failure and recovery

All edits are source-only and additive around existing collection/relay paths.
If validation fails, retain the UOS evidence and revert only the focused ledger
integration; do not change wire contracts or Ability Toggle behavior as a
workaround. Rebuilt UOS acceptance remains user-owned.

## 11. Results

- Added the FrameSync-owned `GameplayCommandIdentity(ClientId, CommandSeq)` and
  retained it for the lifetime of `CommandRelayBuffer`. Repeated copies are
  ignored before late retargeting or authorization, while adjacent presses with
  different `CommandSeq` values both reach authority.
- Added `CommandCollector.ContentRevision` and a Bootstrap send ledger. The
  bridge creates a Bundle only when canonical content changes and transmits
  only identities not already sent successfully; NGO `ReliableSequenced`
  remains responsible for transport retransmission.
- No wire bytes, Bundle/Relay schema, Snapshot, checksum, rollback boundary,
  TargetTick policy or Ability Toggle semantics changed.
- Unity forced synchronous refresh compiled cleanly; the Editor was idle and
  the isolated Console Error query was empty.
- Focused regressions passed, including duplicate after Freeze, duplicate after
  owner invalidation, genuine late first arrival, adjacent sequences, content
  revision and all three send-ledger cases. Full `FrameSyncMoba.FrameSync.Tests`
  passed `121/121`; full `FrameSyncMoba.Bootstrap.EditModeTests` passed
  `123/123`.
- Packaging and rebuilt UOS acceptance remain user-owned. The required live
  acceptance is one physical W press producing one authoritative Toggle, while
  two presses with distinct sequences still produce two Toggles.

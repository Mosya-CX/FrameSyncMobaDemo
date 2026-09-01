# ExecPlan 0151 — Varus Input, Indicator and Hero-Test Regression Closure

Plan ID: 0151
Status: Completed
Created: 2026-08-30
Completed: 2026-08-30
Risk: High
Design conformance: Strict
Estimated code delta: 250-650 lines across source, focused tests and current
implementation documentation
Actual code delta: approximately 330 changed lines across the focused
PlayerInput, FrameSync, Gameplay Ability and Bootstrap source/test files; the
shared worktree still contains earlier 0148-0150 edits, so a plan-only Git
split is not mechanically exact.
Affected assemblies: PlayerInput, Gameplay Ability/Unit, FrameSync, Bootstrap
PlayMode tests and HeroTestScene presentation setup
Design sources: FrameSync v10.2 §§10.2-10.3, 13.2; Player Input v1.1
§§15.3, 17.2-17.4, 18.1; Ability v15.2 §§1.5-1.7 and HoldRelease/Toggle
models; Unit Framework v27.4 §§4.2, 5; Presentation v13.2
Decision dependencies: D-029, D-047, D-051, D-052
Validation basis: supplied rebuilt UOS observations and latest client logs;
Unity compilation/Console; focused EditMode and PlayMode tests; one final
independent read-only review attempt plus a local read-only checklist after all
implementation and verification

## 1. Purpose

Restore visible non-Aatrox-Q skill indicators in a rebuilt Player, keep Varus Q
left-click Commit functional after route movement during Hold, allow ordinary
attacks while the pure Varus W Toggle is active, make future W state jumps
diagnosable without guessing, and leave HeroTestScene ready to test Varus.
Packaging remains user-owned.

## 2. Progress

- [x] Resolve current design authority, inspect the latest UOS evidence and
  confirm a clean Unity compilation baseline.
- [x] Identify the generic-indicator Player-risk path and the pure-Toggle
  attack-gate defect.
- [x] Reproduce the Q Hold -> route Move -> primary Commit path through focused
  EditMode and real Input System tests. The route passes and preserves Focus,
  Move and Commit at one TargetTick, so make no speculative pipeline change.
- [x] Preserve the formal cast-command stream and add targeted W command,
  execution and toggle-transition diagnostics instead of speculative merging.
- [x] Bind generic indicator runtime materials from the loaded Addressables
  material/shader object and verify texture-alpha geometry survives.
- [x] Allow attacks during a pure Toggle while continuing to reject attacks
  during real active cast stages.
- [x] Inspect/configure HeroTestScene for Varus through Unity MCP and leave the
  scene open for manual testing.
- [x] Compile and run focused EditMode/PlayMode verification, perform the
  required read-only review attempt, and update current status documentation.

## 3. Repository facts and discoveries

- `CommandCollector` retains every CastAbility/CancelAbility Command and sorts
  them canonically; it currently merges only Move, Attack and per-slot UseItem.
  Therefore W state jumps are not explained by an existing same-skill
  first-command merge.
- FrameSync v10.2 permits skill-specific Cast merging and Player Input v1.1
  explicitly permits Focus followed by Commit in one TargetTick. A blanket
  "keep the first same-skill Command" rule would discard that legal Commit.
- The current pipeline first replaces a Unit's single Intent for every
  canonical action Command and only then asks Planner for one request. Unit
  Framework v27.4 also states that Planner submits at most one request per
  Tick. Changing this boundary would be a public Tick/Command semantic change;
  this plan will not guess at it unless the focused Q reproduction proves it is
  the active defect and current authorities can be reconciled without a design
  deviation.
- Latest client diagnostics show local W Commit requests and Q indicator
  Show/Hide edges but omit TargetTick, execution mode, canonical order and
  Toggle before/after state. Those fields are required to distinguish duplicate
  input, prediction/replay and presentation-only flips.
- Generic indicator source materials now use built-in `Sprites/Default`, but
  `SkillIndicatorDriver` discards their already resolved Shader objects and
  performs a fresh `Shader.Find` in the Player. The loaded source material is
  the reliable Addressables dependency boundary.
- `GameplayInputGate.IsAttackAllowed` blocks every active AbilitySession,
  including a pure Toggle. D-047 explicitly says Toggles retain sessions but do
  not own Main/Base actions and never block another action.
- `HeroTestScene` currently serializes `heroPrototypeId: 1001`; Unity MCP must
  confirm that this is the active Varus fixture before the scene is handed off.
- Both the requester-level and real Input System Q tests pass with Focus,
  right-click Move and left-click Commit ordered at one TargetTick. This does
  not reproduce the live refusal and therefore supplies no authority for a
  Command merge or single-Intent pipeline rewrite. The new request/execution/
  session/local-state/restore logs are the selected next live evidence.
- Unity MCP confirmed that `HeroTestScene` is open, valid and clean and that its
  `HeroTestDriver.heroPrototypeId` and `dummyPrototypeId` are both 1001. The
  formal catalog identifies 1001 as Varus, so no serialized scene mutation was
  necessary.
- One MCP component inspection requested three stale field names; the tool
  logged its own path errors and returned the valid prototype fields. Console
  clear later hit the MCP log-file lock. These are tool-side failures, not
  project compile/runtime errors; final refresh remained idle and all focused
  tests reported zero captured Error logs.

## 4. Design sources and traceability

- Player Input v1.1 §§15.3 and 17.2-17.4: Q Focus/Commit is a deterministic
  Command fact and local state only observes Gameplay. Protected by the new
  Hold -> secondary Move -> primary Commit input test and, if applicable, a
  FrameSync command-order regression.
- FrameSync v10.2 §§10.2-10.3: Cast commands follow the skill input rule and
  canonical CommandSeq ordering. Protected by existing
  `HoldRelease_AllocatesFocusBeforeCommitAtSameTargetTick` plus new diagnostic
  assertions; no blanket first-command merge is introduced.
- Unit Framework v27.4 §4.2 and D-047: Varus Q Hold may coexist with Base Move;
  Release preempts the Move. Existing
  `MovableHold_AllowsMove_ReleasePreemptsMove` and the new end-to-end Q test
  protect the route.
- Unit Framework v27.4 and D-047 pure-Toggle rule: W must not reserve or block
  ordinary action resources. A focused `GameplayInputGate` test protects
  Toggle-active attack acceptance and real Hold-stage rejection.
- Ability v15.2 §§1.5-1.7 and Presentation v13.2: indicators are client-only
  projections and do not write Gameplay. The Addressables material binding and
  framebuffer PlayMode test protect the visible textured shape.

## 5. Scope

### In scope

- Generic Direction, RangeCircle and GroundTarget indicator material binding.
- Varus Q input continuity after a route Move and only a proven root-cause fix.
- Varus W attack gating and transition diagnostics.
- Focused tests, HeroTestScene Varus setup and current implementation docs.

### Out of scope

- A blanket same-Tick first-command merge that would discard legal
  Focus -> Commit signals.
- Resolving a formal Tick/Planner contract conflict without explicit design
  authority.
- Ability balance, a new input model, unrelated presentation refactors, or
  Client/Dedicated Server packaging.
- Unrelated cleanup in the already-dirty working tree.

Snapshot/serialization/checksum implications: none planned. Diagnostics read
existing command/runtime state and never enter Snapshot, checksum or wire data.
Unity assets: source indicator materials are inspected but need no migration if
their built-in shader and texture references remain valid. HeroTestScene changes
only if MCP inspection proves the serialized Varus setup is incomplete.

## 6. Implementation plan

1. Add focused input/simulation coverage for Q Focus, route Move and primary
   Commit; use the failing boundary to identify any local-state, gate or Intent
   defect.
2. Clone each already-loaded source indicator Material instead of performing a
   new global Shader lookup; preserve texture, tint, render state and lifetime.
3. Gate ordinary attacks on `HasActiveActionStage`, not any persistent ability
   session, so pure Toggles remain action-neutral.
4. Log Cast request receipt, canonical execution and Toggle transition edges
   with Tick, mode, owner, slot, verb, sequence/session and before/after state.
5. Inspect/open HeroTestScene with Unity MCP and confirm Varus prototype 1001.
6. Compile through Unity, run focused EditMode/PlayMode tests, review the final
   diff once, and update plan/status/handoff evidence.

## 7. Public contracts and ownership

- No new protocol, Snapshot, UID, Command or AbilitySignal type is planned.
- `GameplayInputGate` remains PlayerInput-owned and reads Gameplay's existing
  `HasActiveActionStage` observation.
- Diagnostic formatting remains non-authoritative; it may expose additional
  read-only receipt/execution/session facts without changing command bytes.
- `SkillIndicatorDriver` continues to own runtime clone lifetime; Addressables
  source Materials remain loader-owned.

## 8. Validation

- Unity synchronous refresh/compilation and final zero-new-error Console check.
- Focused PlayerInput EditMode: Q Focus -> secondary Move -> primary Commit,
  legal same-Tick Focus/Commit retention, Toggle-active attack allowed, active
  Hold attack rejected.
- Focused Gameplay/FrameSync tests only if the reproduction reaches those
  layers; deterministic canonical/replay coverage is required for any command
  execution change.
- Focused Bootstrap PlayMode: actual Addressables generic indicator runtime
  materials preserve the loaded shader/texture/tint and render a visible blue
  non-magenta textured shape.
- HeroTestScene inspection through Unity MCP and a focused PlayMode smoke test
  when the scene fixture can run without unrelated baseline failures.
- No packaging/build command will be sent.

Recorded evidence so far:

- PlayerInput EditMode assembly: `38 passed / 0 failed`.
- Exact Q requester and real Input System Focus -> Move -> Commit cases:
  `2 passed / 0 failed`.
- Exact pure-Toggle attack-allowed and real Hold-stage attack-blocked cases:
  `2 passed / 0 failed`.
- Existing movable-Hold arbitration plus Toggle activation/deactivation and
  Hold concurrency cases: `5 passed / 0 failed`.
- Actual Addressables material/framebuffer PlayMode and source-material shader
  guard: `2 passed / 0 failed`; W-then-Q and one-shot W Input System cases:
  `2 passed / 0 failed`.
- Unity force refresh completes with the Editor idle and no compile errors.
  Every focused test run captured zero project Error logs. After the test-run
  cleanup, the MCP Console cache was cleared successfully and the final Error
  query returned an empty result; earlier entries were only recorded MCP tool
  errors from overlapping test requests.

## 9. Independent review

The required first independent review was dispatched twice after all fixes and
tests, including once with a lightweight reviewer, but both reviewer turns were
rejected by the host usage limit before producing findings. I therefore
performed the documented read-only review checklist locally after the final
Unity refresh and test runs. It found no P0 or P1 issue; no second review was
requested or run. The limitation is recorded explicitly rather than presented
as an independent-agent approval.

## 10. Failure and recovery

All changes remain ordinary working-tree edits and preserve unrelated user
changes. If the Q reproduction requires choosing between the current formal
same-Tick Focus/Commit rule and the one-request-per-Tick Planner rule, stop that
contract change, record the exact conflict and keep the unaffected indicator,
W attack, diagnostics and HeroTest work moving. Rebuilt Player acceptance
remains external and user-owned.

## 11. Results

Completed. Generic Direction, RangeCircle and GroundTarget indicators now clone
the complete Shader/texture/tint/render-state object resolved through
Addressables, so the runtime does not perform a second global `Shader.Find` or
lose the texture-alpha silhouette in a rebuilt Player. PlayerInput now keeps a
Varus Q Focus/Commit latch through a route Move and a later primary-click
Commit; the focused requester and real Input System tests preserve all three
commands at the intended TargetTick. The pipeline was deliberately not given a
same-Tick first-command merge because the current input contract allows legal
Focus -> Commit ordering. Pure Toggle sessions no longer block ordinary attacks,
while real action-owning Hold stages remain blocked. Request, canonical
execution, signal, local-state and Toggle-restore diagnostics now include Tick,
mode, slot, verb, sequence/session and before/after state for the next live W
investigation. Unity MCP confirmed `HeroTestScene` is open and clean with both
hero and dummy prototype `1001` (Varus). PlayerInput EditMode is `38/38`; the
latest focused Q route, W-then-Q, one-shot W, source-material and framebuffer
PlayMode checks all pass, and the final Unity Console Error query is empty after
clearing the MCP cache. No Snapshot, checksum, wire, build or package change
was made. Rebuilt Windows/UOS visual acceptance remains user-owned, and a live
W state-jump reproduction is still needed if it recurs.

# FrameSyncMobaDemo AI Direct-Request Workflow

> Document class: Current Workflow
> Workflow version: 4
> Major revision: 2026-08-22
> Update policy: replace current procedure; use Git for history

This workflow implements concrete user requests directly. The former A/B/C
candidate-generation and approval loop is retired.

## 1. Core loop

```text
User provides design/context plus a concrete request
    -> resolve current authority and repository facts
    -> classify scope and risk
    -> create an ExecPlan only when required
    -> implement the smallest complete slice
    -> compile and run focused validation through Unity MCP
    -> perform an independent review when risk is High
    -> update current evidence and finish
```

Do not automatically generate future candidates or ask the user to select the
next task. After completing the request, stop with a concise result and any real
remaining limitation.

## 2. Information sources

### 2.1 What the project is now

Use, in order of evidential strength:

1. current code and Unity assets;
2. Git status and the current diff;
3. live Unity compile, Console and test results;
4. `Docs/Implementation/CURRENT_HANDOFF.md`;
5. affected rows in `Docs/Implementation/MODULE_STATUS.md`;
6. the active ExecPlan, when one exists;
7. completed plans and archived reports only when investigating history.

Historical documents never override live code or current-state documents.

### 2.2 What the project must become

Use this authority order:

1. current user request;
2. relevant entries in `Docs/Architecture/DECISION_LOG.md`;
3. `Docs/Architecture/DESIGN_INDEX.md`;
4. Current formal designs listed by the index;
5. the active ExecPlan for scope and sequencing;
6. existing implementation.

An ExecPlan cannot change formal design semantics. Code proves current state but
does not override formal design.

## 3. Layered reading

At first intake, read only:

```text
AGENTS.md
Docs/Implementation/CURRENT_HANDOFF.md
Docs/Implementation/MODULE_STATUS.md
Docs/Architecture/DESIGN_INDEX.md
```

Then narrow by the concrete request:

```text
search relevant Decision IDs/headings
read only relevant Current design sections
inspect affected code, asmdefs and tests
read .agents/PLANS.md only if the task may require an ExecPlan
read operational guides only if their procedures are in scope
```

Do not default-read the entire Decision Log, all formal designs, all completed
plans, archived audits, all scenes/prefabs or the full test suite.

Expand inspection only when current state is missing, stale, contradicted by
code, or insufficient to implement safely.

## 4. Request intake and scope

For every concrete request, establish:

- observable result;
- exact Current design sources and sections;
- affected assemblies and authoritative public types;
- in-scope and out-of-scope behavior;
- Snapshot/serialization/checksum/lifecycle implications;
- Unity asset or scene implications;
- relevant focused tests;
- risk: Low, Medium or High.

There is no artificial target around 500 lines. Scope is determined by the
smallest complete and testable behavior that satisfies the user. If the request
is large, preserve the user goal and split execution into explicit milestones
or linked plans rather than inventing unrelated candidate tasks.

## 5. Risk classification

### Low

- private/localized behavior;
- one assembly;
- no public protocol, serialization, lifecycle or Unity asset change;
- focused tests provide a direct proof.

### Medium

- several related files or assemblies;
- Unity asset/scene/lifecycle integration;
- public API addition that follows an already frozen design;
- meaningful migration or compatibility risk.

### High

Any change involving:

- Tick, Command, CommandSeq, UID or canonical bytes;
- Snapshot, Restore/Resolve/Rebuild, rollback or checksum;
- FrameSync authority, recovery or prediction;
- Combat settlement, formal death or respawn lifecycle;
- runtime/data ownership;
- network Bootstrap or application-flow protocol;
- broad asmdef dependency changes;
- a public contract spanning several system designs.

High risk requires a formal ExecPlan and an independent read-only review. Use a
separate review sub-agent when collaboration tools are available; otherwise
record that independent review was unavailable instead of claiming it occurred.

## 6. ExecPlan decision

Create or update an ExecPlan when the task:

- spans more than one assembly;
- changes a public contract;
- changes serialization, Snapshot or checksum;
- changes FrameSync/network flow;
- spans several implementation sessions;
- touches several formal system designs;
- has meaningful migration or compatibility risk;
- is classified High.

Use `.agents/PLANS.md` and register the active plan in
`Docs/Implementation/Plans/INDEX.md`.

For a clear request with no design deviation, creating an ExecPlan is an
execution step, not a new user-approval gate. Create it and proceed. Stop for
approval only when section 7 requires it.

Small localized tasks may use the task plan in chat instead of adding a formal
repository plan.

## 7. Design conformance and approval

Production behavior, public contracts, ownership, lifecycle, stable order,
Snapshot, serialization, checksum, Command, UID, AbilitySignal and assembly
direction must follow the Current formal designs.

The agent may independently decide private names, file splitting, local helpers,
cache implementation, focused test organization and code style when they do not
change observable contracts.

Stop the affected work and ask the user before:

- deviating from a Current formal design;
- resolving a real conflict between Current designs;
- adding a new package;
- changing a public protocol or data owner beyond the user request;
- changing Snapshot semantics or restore boundaries beyond the user request;
- deleting/merging a required architecture layer;
- performing large-scale deletion not explicitly requested.

Use this concise format:

```text
Decision/Deviation request
Design and section:
Current requirement:
Proposed decision or deviation:
Affected contracts/assemblies:
Snapshot/serialization/checksum impact:
Alternative that does not deviate:
Recommendation:
```

Continue all unaffected work while the conflicting contract is pending.

## 8. Pre-edit checks

Before editing production code:

1. inspect Git status and preserve unrelated user changes;
2. search for existing equivalent public/protocol types;
3. inspect affected asmdefs and one-way dependency direction;
4. inspect the current Unity Editor compilation and Console state;
5. identify the focused test baseline;
6. record real repository facts in the active plan when one exists.

Do not run full test suites or scan all assets by default.

## 9. Implementation rules

- Implement the complete requested behavior, not pseudocode.
- Keep the slice inside its explicit scope.
- Do not create duplicate protocol or DTO types.
- Preserve deterministic stable ordering in code.
- Add tests while implementing rather than as a separate future task.
- Do not modify formal designs to make a shortcut appear conformant.
- Do not introduce placeholder success, ignored failures, TODO behavior or
  disabled tests.
- Preserve unrelated dirty-worktree changes.

If implementation discovers a larger unrelated problem, record and report it;
do not silently absorb it into the current task.

## 10. Unity MCP and fallback

Prefer Unity MCP for supported Unity operations.

After C# changes, always perform:

```text
Unity compile
Console inspection
relevant focused tests
```

For Unity asset changes also perform:

```text
asset reload/inspection
serialized-reference validation
relevant Editor or PlayMode asset/lifecycle tests
```

Do not default-run every EditMode/PlayMode test or scan every scene/prefab.

When MCP is unavailable or an operation fails, record:

```text
MCP operation:
Failure:
Fallback:
Risk:
Required final Unity verification:
```

Do not retry indefinitely. A fallback does not make Unity asset/lifecycle work
fully verified until it is rechecked in Unity.

## 11. Validation levels

Do not collapse validation into a single “done” state. Record the highest
evidence actually reached:

- Implemented;
- Compiled;
- Focused Tested;
- Integration Tested;
- Live Verified.

Every test result records its date and source commit/worktree basis. Reuse a
recent reliable baseline when unrelated code has not changed.

Consider a full suite only for broad assembly changes, Snapshot schema changes,
core FrameSync protocol changes, composition-root changes, release/stage
acceptance or large merges.

## 12. Independent High-risk review

The implementation pass must not be the only proof for a High-risk change.

Run one independent read-only review, preferably through a separate review
sub-agent, using only:

- exact formal design sections and Decision entries;
- the active ExecPlan public-contract/ownership summary;
- the current diff;
- the design-to-test mapping and test results.

The reviewer:

- reviews from the design, not the implementer's narrative;
- does not add features or rewrite design;
- reports only evidence-backed P0/P1/P2 findings;
- covers public ownership, determinism/stable order, Snapshot/serialization and
  test adequacy in one pass unless the diff is exceptionally broad.

Fix scope-local P0/P1 findings and rerun relevant validation. Record scope-external
findings without expanding the task. Low/Medium tasks do not require an
independent reviewer unless their discovered risk changes.

## 13. Design-to-test traceability

For important contracts, plans and final validation use explicit mappings:

```text
Design/Decision section
    -> exact test fixture and behavior
```

Do not claim that tests prove design conformance without stating which behavior
they protect. Add mappings incrementally for touched critical contracts; do not
rewrite every historical design at once.

## 14. Documentation updates

Update only what the task changes:

- active ExecPlan progress/results, when present;
- affected `MODULE_STATUS.md` rows;
- `CURRENT_HANDOFF.md` current save state;
- `DECISION_LOG.md` only for a new frozen architecture decision;
- `DESIGN_INDEX.md` only when a Current design changes;
- build/test guides only when their procedure changes.

Do not create a future-candidate list. Do not append task history to current
state documents. Git and Completed ExecPlans own engineering history.

### Current Handoff contract

`CURRENT_HANDOFF.md` is replaced, not appended. Target:

- 100-180 lines;
- no more than 15 KB;
- recent completed work limited to 1-3 items;
- no copied logs or daily development timeline.

It contains only:

```text
branch/base/worktree state
current Unity and reliable test baseline
current implemented/live state summary
current P0/P1/P2 findings
frozen contract references
unfinished user-requested work or external acceptance
special continuation/build constraints
```

## 15. Workflow health gates

The first workflow-guard implementation round reports violations as Warning.
After the baseline is corrected, the second round promotes high-confidence
checks to Blocking.

Initial guard targets:

- document class, path and size contracts;
- Decision/Plan ID uniqueness;
- active-plan count and metadata;
- Current design index paths and status;
- stale references to archived workflow documents;
- asmdef direction/cycles and public-protocol ownership;
- high-confidence deterministic forbidden APIs.

Warnings must be visible and assigned; they are not silently ignored.

## 16. Allowed blockers

Stop and request user direction only when:

- Current formal designs conflict on a public contract;
- the requested behavior requires an unapproved design deviation;
- an unapproved package is necessary;
- required authority, external data or destructive permission is missing;
- an external compiler/project failure prevents safe progress inside scope.

Ordinary private implementation choices are not blockers.

## 17. Final report

Lead with the delivered result. Include only:

- observable behavior;
- files and public contracts changed;
- tests changed and exact results;
- Unity compilation/Console result;
- design sections verified and any Reviewer findings;
- remaining limitations or required live acceptance;
- MCP fallbacks or unverified Unity behavior.

Do not repeat the project background, full design text, complete logs or propose
automatic next candidates.

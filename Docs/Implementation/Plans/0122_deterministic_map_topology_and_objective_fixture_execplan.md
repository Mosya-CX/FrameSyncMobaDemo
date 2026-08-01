# ExecPlan 0122: Deterministic map topology and objective fixture

> Status: Completed on 2026-07-29.

## Purpose

Turn the restored visual GameScene into a neutral deterministic match fixture:
explicit map bounds and walkability, stable player spawn points, two team bases
and a base-destruction victory path. Unity colliders and transforms remain
authoring/presentation inputs, never Gameplay authority.

## Progress

- [x] Audit existing Physics/Pathfinding authoring and bake APIs.
- [x] Define the smallest map bake owned by existing semantics.
- [x] Author neutral structure/player fixture assets and stable topology.
- [x] Prove movement, collision, spawn and base-victory behavior.

## Surprises and discoveries

- `GameScene` currently has visual geometry plus a Unity `BoxCollider`, but no
  deterministic PhysicsEntity/pathfinding map data.
- `MapRuntimeData` and `PathfindingBakeData` are named by FrameSync v10.2 but
  have no production implementation.
- TeamBase victory logic exists, but no structure prototype or scene supplies
  two bases.
- No lane, wave or jungle asset is assigned in current bootstrap scenes.

## Decision log

- Use the current map as authoring geometry, not as Unity-physics authority.
- Implement only the map data required for a two-team logic loop. Lanes, waves
  and camps may be empty but explicit; their full content is not required to
  prove base victory.
- All created units and visuals use neutral fixture names.

## Current repository context

Relevant code is in `FrameSyncMoba.Physics`, `FrameSyncMoba.Gameplay`,
`FrameSyncMoba.Unit` and Bootstrap scene composition.

## Exact design sources

- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`.
- `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md`.
- `Docs/Design/unit_behavior_framework_design_v27_3.md`, UnitKind,
  UnitSubKind and TeamBase rules.
- `Docs/Design/moba_combat_system_design_v13_2.md`, TeamBase destroyed signal.
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`, global map
  data, initial composition and match end.

## Scope

In scope:

- Deterministic map bounds, walkability/obstacles and stable spawn-point bake.
- Neutral hero-like test unit and neutral structure/base prototypes.
- Two teams, one base per team, explicit stable initial spawn ordering.
- Match end from authoritative base destruction.
- GameScene authoring components and fixture prefabs/assets via Unity MCP.

Out of scope:

- Production map art, balance, fog of war, towers, minion waves and jungle
  content.
- Client input/UI, live NGO transport and UOS.
- Using `BoxCollider`, Unity Rigidbody or Transform as Gameplay authority.

## Affected assemblies and exact production types

- Physics/Pathfinding: reuse `PhysicsEntity2D`, `PhysicsWorld`,
  `PathGridMap2D` and existing fixed-point shapes.
- Unit/Combat/FrameSync: reuse `UnitKind.Structure`,
  `MatchTopologyRole`, `TeamBaseDestroyedSignal` and `MatchRuleRuntime`.
- RuntimeConfig/Bootstrap: add the smallest validated map/spawn authoring and
  baked runtime data needed by those existing types.

Expected production-code change: 1,000-2,000 lines, plus neutral Unity fixture
assets.

## Public contracts, ownership and dependency direction

Do not invent a second physics shape, map UID, TeamId or spawn command. Static
map bake belongs below Bootstrap; Bootstrap selects a map by `MapConfigId` and
feeds immutable baked data into Gameplay. Presentation may read the result but
must not write it.

## Deterministic ordering

Sort map objects and spawn points by explicit stable IDs. Sort initial units by
stable spawn order before UID allocation. Path walkability bytes and objective
registration must not depend on hierarchy, component or dictionary order.

## Snapshot and serialization impact

Immutable baked map data does not enter each snapshot; its version participates
in the critical version handshake. Spawned units, physics state and
MatchRuleRuntime use their existing snapshot/checksum ownership.

## Implementation steps

1. Reuse or minimally extend current physics/pathfinding bake types for static
   walkability and bounds; add validation for overlap, duplicate IDs and
   out-of-bounds spawns.
2. Create neutral player and TeamBase fixture prefabs/configs using the existing
   MonoBehaviour Unit/Handler authoring model.
3. Author two team spawns and two bases in GameScene with explicit IDs and
   fixed-point bake.
4. Feed this composition into ExecPlan 0121's payload builder.
5. Verify deterministic movement around an obstacle and base-destruction match
   completion.

## Tests

EditMode tests cover stable bake bytes/order, invalid geometry, path
walkability, spawn UID stability, snapshot/replay equality and base-victory
signals. One PlayMode scene test loads the fixture, verifies serialized
references, runs the neutral objective loop and confirms Presentation cannot
change Gameplay.

## Unity MCP validation

Use MCP to modify/inspect GameScene and fixture prefabs, compile, inspect
Console, and run only the focused EditMode plus one GameScene PlayMode test.

## Failure conditions, completion criteria and recovery

Stop if current designs require an unresolved public `MapRuntimeData` schema
choice that changes FrameSync contracts. Otherwise use the smallest immutable
bake. Completion requires identical bake output independent of hierarchy,
authoritative fixed-point collision/pathing, two stable bases and deterministic
match end.

## Production-content exclusion

Geometry primitives and neutral prefabs are test fixtures, not a final map,
hero, tower, minion, monster or balance configuration.

## Results

The neutral map fixture now supplies deterministic bounds, walkability, stable
player spawns and two team bases. Unity geometry remains presentation/authoring
data; authoritative positions and topology are baked fixed-point data.
Focused topology/configuration tests and Unity compilation passed.

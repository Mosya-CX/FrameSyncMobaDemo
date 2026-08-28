# Match-scoped content partition manifest

> Status: Implemented (ExecPlan 0141)
> Runtime: Unity 2022.3.62f1c1 / Addressables 1.22.3
> Distribution: local installed content only; no remote catalog or runtime update

## Root contract

`GlobalPrefabTable` remains the sole `PrefabKind + PrefabId` authority. The
formal root serializes no logical `GameObject`; it contains four child-table
descriptors with an expected version and Unity dependency-backed content hash.

| Partition | Owner | Child-table address | Loaded when |
|---|---:|---|---|
| Core | 0 | `content/table/core` | Every match |
| Map | 1 | `content/table/map/1` | MapConfigId = 1 |
| Hero | 1001 | `content/table/hero/1001` | Varus appears in the roster |
| Hero | 1002 | `content/table/hero/1002` | Aatrox appears in the roster |

Child entries use their local Unity asset path as the Addressables address.
This makes every reference path-only while retaining an exact source-asset
audit trail. Client-view keys keep the existing semantic `view/*` and `vfx/*`
addresses.

## Implemented ownership

| Partition | Prefab entries | Deterministic catalogs |
|---|---|---|
| Core | Units 1201, 1202, 1211, 1212, 1301, 1302; Projectiles 2201, 2202 | Core Unit (24 stats, 8 non-hero prototypes), Core Projectile, Core Buff, full CC, full Equipment |
| Map 1 | Misc 5001 | `FullMatchDeterministicMapConfig` |
| Hero 1001 | Unit 1101; Projectiles 2101–2104 | Varus Unit 1001, abilities 10011–10014, passive 10010, Varus Projectile and Buff catalogs |
| Hero 1002 | Unit 1102; Projectiles 2105–2106; Q VFX metadata 3101–3103 | Aatrox Unit 1002, abilities 10021–10024, passive 10020, Aatrox Projectile and Buff catalogs |

The original full catalogs remain non-runtime migration evidence. `GameScene`
no longer serializes them; production composition comes only from the selected
child tables.

## Addressables groups

| Group | Roots | Client build | Server build |
|---|---:|:---:|:---:|
| `Logic-Core` | 14 | Yes | Yes |
| `Logic-Map-1` | 3 | Yes | Yes |
| `Logic-Hero-1001` | 10 | Yes | Yes |
| `Logic-Hero-1002` | 8 | Yes | Yes |
| `Client-Hero-1001` | 8 | Yes | No |
| `Client-Hero-1002` | 6 | Yes | No |
| Existing `Client-*` groups | UI, audio, shared/common views | Yes | No |

All groups use local build/load paths, `PackTogether` per ownership group,
static content, no cache update, no remote catalog and no startup catalog
update. The Dedicated Server build scope enables only `Logic-*` groups and
restores all group flags after the build.

## Runtime closure

1. Lobby freezes MapConfigId and the complete locked hero roster before scene
   load; hero IDs are sorted and deduplicated.
2. `GameBootstrap` loads Core, the selected Map and selected Hero tables
   asynchronously.
3. Every child version/hash, address and duplicate key is validated.
4. Loaded partitions are canonicalized by `(PartitionKind, OwnerConfigId)`;
   prefab entries remain keyed by `(PrefabKind, PrefabId)`.
5. Unit, Ability, Projectile and Buff partitions are merged into match-local
   registries. Completion order cannot affect their order.
6. Only after the resolved table is sealed may initial spawns, bootstrap
   Snapshot application or Tick advancement occur.
7. The later authoritative `GameStartConfig` must describe the same Map/Hero
   closure or bootstrap fails visibly.
8. One `AddressableMatchContentScope` owns and releases every acquired handle.

## Acceptance properties

- A Varus-only scope has UnitPrototype 1001 and Ability 10011, but not 1002 or
  10021; Aatrox-only has the inverse property.
- The root plus four children cover exactly the original 20 prefab mappings and
  contain zero direct logical prefab references.
- GameScene contains zero legacy Unit/Ability/Projectile/Map/Equipment/Buff/CC
  catalog references.
- Client builds contain logic plus presentation groups; server builds contain
  logic groups and reject any `Client-*` bundle.
- Partition hashes include Unity dependency hashes, so changing referenced
  content changes the expected hash even when its address is unchanged.

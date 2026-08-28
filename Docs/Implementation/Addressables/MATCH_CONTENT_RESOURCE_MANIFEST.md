# Match content resource manifest — post migration

Generated and verified through Unity AssetDatabase on 2026-08-27. Compare with
`MATCH_CONTENT_PRE_MIGRATION_DEPENDENCIES.*`,
`MATCH_CONTENT_PRE_MIGRATION_ROOTS.*` and
`MATCH_CONTENT_PRE_MIGRATION_FORMAL_ROOTS.*` for the before state.

| Partition | Hash | Prefab rows | Catalog rows | Logic roots | Client hero roots |
|---|---:|---:|---:|---:|---:|
| Core / 0 | 5233413462223469856 | 8 | 5 | 14 | 0 |
| Map / 1 | 3901336983848524330 | 1 | 1 | 3 | 0 |
| Hero / 1001 | 15379179350999139146 | 5 | 4 | 10 | 8 |
| Hero / 1002 | 8362605082240316254 | 6 | 4 | 8 | 6 |

Aggregate checks:

- formal root direct prefab groups: 0;
- child partitions: 4;
- path-only prefab rows: 20 (same stable IDs as before migration);
- direct logical `GameObject` references in root/children: 0;
- Core Unit catalog: 24 stat definitions and 8 common prototypes;
- Core Unit catalog is the sole owner of the shared dispose-policy table;
- Varus/Aatrox Unit catalogs: one hero prototype each and no shared
  dispose-policy reference, preventing the same implicit dependency from being
  deserialized as different objects in separate bundles;
- Aatrox split Ability catalog: 4 active abilities, 4 slots, 1 passive;
- remote catalog, remote load path and catalog update: disabled;
- server inclusion: 4 Logic groups only;
- client hero bundle roots: Varus 8, Aatrox 6.

The counts above are root counts, not transitive bundle dependency counts.
Unity still includes dependencies referenced by each selected root, but an
unselected hero root is no longer pulled in through a combined catalog or a
shared PackTogether hero group.

# Map prefab Addressables migration

| Role | Asset | GUID/address |
|---|---|---|
| Formal deterministic authoring | `Assets/Config/Formal/Prefabs/Logic/Map/Map.prefab` | `58908e293ad4aea44a8dbe5b7d9abd59` |
| Client view | `Assets/ClientContent/Views/Map/MapView.prefab` | `view/map/main` |
| Historical monolithic copy | `Assets/Archive/LegacyMonolithicMapPrefab/Map.prefab` | `ee68d472fc505e84d8357654486fc328` |

The logic prefab retains FlowFieldSceneAuthoring/LaneAuthoring and no Renderer, MeshFilter, Collider or material dependency. The client view contains render data and no deterministic map authoring components.

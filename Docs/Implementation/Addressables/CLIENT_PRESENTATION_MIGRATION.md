# Shared presentation and UI migration

All moves preserve asset GUIDs. Animation, material and indicator assets are bundled as dependencies of Addressable presentation roots.

## Folder moves

| Source | Destination |
|---|---|
| `Assets/Resources/Animation` | `Assets/ClientContent/Animation` |
| `Assets/Resources/Material` | `Assets/ClientContent/Materials` |
| `Assets/Resources/Prefab/Indicators` | `Assets/ClientContent/Indicators` |
| `Assets/Resources/Prefab/UI` | `Assets/ClientContent/UI` |
| `Assets/Config/Formal/Animation` | `Assets/ClientContent/Animation/Profiles` |
| `Assets/Resources/MiniMap.renderTexture` | `Assets/ClientContent/UI/MiniMap.renderTexture` |
| `Assets/Config/Formal/UnitOutlineRim.mat` | `Assets/ClientContent/Materials/UnitOutlineRim.mat` |

Additional roots: `ui/indicator/direction`, `ui/indicator/range-circle`, `ui/indicator/ground-target`. UIManager is a lightweight scene-resident composition shell; its seven page prefabs are Addressable roots.

## UI page roots

| Page | Address | Asset |
|---|---|---|
| Main | `ui/page/main` | `Assets/ClientContent/UI/MainPanel.prefab` |
| Match | `ui/page/match` | `Assets/ClientContent/UI/MatchPanel.prefab` |
| Select | `ui/page/select` | `Assets/ClientContent/UI/SelectPanel.prefab` |
| Load | `ui/page/load` | `Assets/ClientContent/UI/LoadingPanel.prefab` |
| HUD | `ui/page/hud` | `Assets/ClientContent/UI/GameplayHUD.prefab` |
| Shop | `ui/page/shop` | `Assets/ClientContent/UI/ShopPanel.prefab` |
| Result | `ui/page/result` | `Assets/ClientContent/UI/ResultPanel.prefab` |

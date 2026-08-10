-- Lua UI initialization (design v9.1 section 6.2)
GameObject = CS.UnityEngine.GameObject
Transform = CS.UnityEngine.Transform
RectTransform = CS.UnityEngine.RectTransform
Vector2 = CS.UnityEngine.Vector2
Vector3 = CS.UnityEngine.Vector3
Color = CS.UnityEngine.Color

UI = CS.UnityEngine.UI
TMP = CS.TMPro
TMP_Text = CS.TMPro.TextMeshProUGUI

UIDisplayConvert = CS.FrameSyncMoba.LuaBridge.UIDisplayConvert
GameFlow = CS.FrameSyncMoba.Bootstrap.GameFlowLuaBridge
UIPageId = CS.FrameSyncMoba.Bootstrap.UIPageId

function import(moduleName)
    return require(moduleName)
end

UIBase = require("UI.Core.UIBase")
UICellBase = require("UI.Core.UICellBase")
UIFormat = require("UI.Core.UIFormat")

_G._LuaUiInitialized = 1

print("Lua UI initialized")

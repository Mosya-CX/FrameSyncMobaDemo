-- 注册Unity类型
GameObject = CS.UnityEngine.GameObject
Transform = CS.UnityEngine.Transform
Vector2 = CS.UnityEngine.Vector2
Vector3 = CS.UnityEngine.Vector3
Quaternion = CS.UnityEngine.Quaternion
Time = CS.UnityEngine.Time
Input = CS.UnityEngine.Input
Resources = CS.UnityEngine.Resources
Debug = CS.UnityEngine.Debug
UI = CS.UnityEngine.UI
TMP = CS.TMPro
TMP_Text = TMP.TextMeshProUGUI

-- 注册管理器
UIManager = CS.UIManager.Instance
GameManager = CS.GameManager.Instance

-- 工具函数
function import(moduleName)
    return require(moduleName)
end

print("初始化完成")
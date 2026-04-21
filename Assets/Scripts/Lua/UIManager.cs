using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using XLua;

public class UIManager : MonoSingleton<UIManager>
{
    public Canvas uiRoot;
    [SerializeField, LabelText("UI预制体列表")]
    private SerializedDictionary<string, UIBehaviour> uiPanels = new();

    public Dictionary<string, UIBehaviour> activePanels = new();

    protected override void Awake()
    {
        if (uiRoot == null)
            if (!TryGetComponent(out uiRoot))
                uiRoot = gameObject.AddComponent<Canvas>();
    }

    private UIBehaviour CreatePanel(string panelName)
    {
        if (uiPanels.TryGetValue(panelName, out var prefab))
        {
            var panel = Instantiate(prefab, uiRoot.transform);
            panel.InitLua();
            activePanels.Add(panelName, panel);
            return panel;
        }
        else
        {
            Debug.LogError($"未找到{panelName}的加载路径");
            return null;
        }
        
    }

    public UIBehaviour OpenPanel(string panelName)
    {
        if (!activePanels.TryGetValue(panelName, out var panel))
            panel = CreatePanel(panelName);

        if (panel != null)
        {
            panel.gameObject.SetActive(true);
            return panel;
        }
        else
        {
            Debug.LogError("UI打开失败： " + panelName);
            return null;
        }
    }

    public void ClosePanel(string panelName)
    {
        if (activePanels.TryGetValue(panelName, out UIBehaviour panel))
            panel.gameObject.SetActive(false);
        else
            Debug.LogWarning("UI关闭失败:" + panelName + "\n原因:该UI未加载");
    }

    public void ClosePanel(UIBehaviour panel)
    {
        panel.gameObject.SetActive(false);
    }

    public void DestroyPanel(string panelName)
    {
        if (activePanels.TryGetValue(panelName, out UIBehaviour panel))
        {
            activePanels.Remove(panelName);
            Destroy(panel.gameObject);
        }
        else
        {
            Debug.LogWarning("UI销毁失败:" + panelName + "\n原因:该UI未加载");
        }
    }

    public void DestroyPanel(UIBehaviour panel)
    {
        activePanels.Remove(panel.uiName);
        Destroy(panel.gameObject);
    }

    public void CallLuaMethodInTargetPanel(string panelName, string methodName, params object[] args)
    {
        if (activePanels.TryGetValue(panelName, out UIBehaviour panel))
        {
            panel.CallLuaMethod(methodName, args);
        }
        else
        {
            Debug.LogWarning("UI方法调用失败:" + panelName + "\n原因:该UI未加载");
        }
    }

    public void CallLuaMethodInTargetPanel<T>(string panelName, string methodName, T arg)
    {
        if (activePanels.TryGetValue(panelName, out UIBehaviour panel))
        {
            panel.CallLuaMethod(methodName, arg);
        }
        else
        {
            Debug.LogWarning("UI方法调用失败:" + panelName + "\n原因:该UI未加载");
        }
    }

    public void CallLuaMethodInTargetPanel<T1, T2>(string panelName, string methodName, T1 arg1, T2 arg2)
    {
        if (activePanels.TryGetValue(panelName, out UIBehaviour panel))
        {
            panel.CallLuaMethod<T1, T2>(methodName, arg1, arg2);
        }
        else
        {
            Debug.LogWarning("UI方法调用失败:" + panelName + "\n原因:该UI未加载");
        }
    }

    #region 生命周期函数
    private void Update()
    {
        foreach (var panel in activePanels.Values)
            if (panel.gameObject.activeSelf)
                panel.CallLuaMethod("Update");
    }
    #endregion
}

public class UIConst
{
    public const string MainPanel = "MainPanel";
    public const string LoadingPanel = "LoadingPanel";
    public const string LobbyPanel = "LobbyPanel";
    public const string MobaPlayingPanel = "MobaPlayingPanel";
    public const string FPSPlayingPanel = "FPSPlayingPanel";
}

[Serializable]
[LuaCallCSharp]
public struct UIComponent
{
    public string luaName;
    public Component component;
}
using System.Collections.Generic;
using UnityEngine;
using XLua;
using System;

[LuaCallCSharp]
public class UIBehaviour : MonoBehaviour
{
    public string uiName;
    public string luaName;

    public List<UIComponent> uiComponents = new();
    public List<UICell> cellPrefabs = new();

    private Dictionary<string, UICell> cellPrefabDict = new();
    private LuaTable table;

    private void Awake()
    {
        //RectTransform rectTransform = GetComponent<RectTransform>();
        //rectTransform.anchorMin = Vector2.zero;
        //rectTransform.anchorMax = Vector2.one;
        //rectTransform.offsetMin = Vector2.zero;
        //rectTransform.offsetMax = Vector2.zero;
    }

    public void InitLua()
    {
        table = LuaManager.Instance.DoScript(luaName);

        if (table == null)
        {
            Debug.LogError($"加载'{gameObject.name}'Lua模块失败");
            return;
        }

        table.Set("gameObject", gameObject);
        table.Set("transform", transform);
        table.Set("behaviour", this);

        foreach (var uiComponent in uiComponents)
            table.Set(uiComponent.luaName, uiComponent.component);
        foreach (var cellPrefab in cellPrefabs)
            cellPrefabDict.Add(cellPrefab.uiName, cellPrefab);

        CallLuaMethod("Init");
    }
    #region C#调用Lua函数
    public object[] CallLuaMethod(string methodName)
    {
        if (table == null) return null;
        try
        {
            LuaFunction func = table.Get<LuaFunction>(methodName);
            if (func == null)
            {
                Debug.LogWarning($"'{gameObject.name}'Lua模块中不存在方法'{methodName}'");
                return null;
            }
            return func.Call(table);
        }
        catch (Exception e)
        {
            Debug.LogError($"函数|{methodName}|调用失败，报错信息:{e}");
            return null;
        }
    }

    public object[] CallLuaMethod<T>(string methodName, T arg1)
    {
        try
        {
            if (table == null) return null;
            LuaFunction func = table.Get<LuaFunction>(methodName);
            if (func == null)
            {
                Debug.LogWarning($"'{gameObject.name}'Lua模块中不存在方法'{methodName}'");
                return null;
            }
            return func.Call(table, arg1);
        }
        catch (Exception e)
        {
            Debug.LogError($"函数|{methodName}|调用失败，报错信息:{e}");
            return null;
        }
    }
    public object[] CallLuaMethod<T1, T2>(string methodName, T1 arg1, T2 arg2)
    {
        if (table == null) return null;
        try
        {
            LuaFunction func = table.Get<LuaFunction>(methodName);
            if (func == null)
            {
                Debug.LogWarning($"'{gameObject.name}'Lua模块中不存在方法'{methodName}'");
                return null;
            }
            return func.Call(table, arg1, arg2);
        }
        catch (Exception e)
        {
            Debug.LogError($"函数|{methodName}|调用失败，报错信息:{e}");
            return null;
        }
    }
    #endregion

    private void OnEnable()
    {
        CallLuaMethod("OnEnable");
    }

    private void OnDisable()
    {
        CallLuaMethod("OnDisable");
    }

    private void OnDestroy()
    {
        CallLuaMethod("OnDestroy");
        table?.Dispose();
        table = null;
    }  
    
    public LuaTable CreateCell(string cellName, Transform parent)
    {
        if (cellPrefabDict.TryGetValue(cellName, out var cellPrefab))
        {
            var cell = Instantiate(cellPrefab, parent);
            return cell.InitLua();
        }
        return null;
    }
}


using System;
using System.Collections.Generic;
using UnityEngine;
using XLua;

[LuaCallCSharp]
public class UICell : MonoBehaviour
{
    public string uiName;
    public string luaName;

    public List<UIComponent> uiComponents = new();

    private LuaTable luaTable;

    public LuaTable InitLua()
    {
        luaTable = LuaManager.Instance.DoScript(luaName);

        if (luaTable == null)
        {
            Debug.LogError($"加载'{gameObject.name}'Lua模块失败");
            return null;
        }

        luaTable.Set("gameObject", gameObject);
        luaTable.Set("transform", transform);
        luaTable.Set("behaviour", this);

        foreach (var comp in uiComponents)
            luaTable.Set(comp.luaName, comp.component);

        CallLuaMethod("Init");
        return luaTable;
    }

    #region C#调用Lua函数
    public object[] CallLuaMethod(string methodName)
    {
        if (luaTable == null) return null;
        try
        {
            LuaFunction func = luaTable.Get<LuaFunction>(methodName);
            if (func == null)
            {
                Debug.LogWarning($"'{gameObject.name}'Lua模块中不存在方法'{methodName}'");
                return null;
            }
            return func.Call(luaTable);
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
            if (luaTable == null) return null;
            LuaFunction func = luaTable.Get<LuaFunction>(methodName);
            if (func == null)
            {
                Debug.LogWarning($"'{gameObject.name}'Lua模块中不存在方法'{methodName}'");
                return null;
            }
            return func.Call(luaTable, arg1);
        }
        catch (Exception e)
        {
            Debug.LogError($"函数|{methodName}|调用失败，报错信息:{e}");
            return null;
        }
    }

    public object[] CallLuaMethod<T1, T2>(string methodName, T1 arg1, T2 arg2)
    {
        if (luaTable == null) return null;
        try
        {
            LuaFunction func = luaTable.Get<LuaFunction>(methodName);
            if (func == null)
            {
                Debug.LogWarning($"'{gameObject.name}'Lua模块中不存在方法'{methodName}'");
                return null;
            }
            return func.Call(luaTable, arg1, arg2);
        }
        catch (Exception e)
        {
            Debug.LogError($"函数|{methodName}|调用失败，报错信息:{e}");
            return null;
        }
    }
    #endregion
}

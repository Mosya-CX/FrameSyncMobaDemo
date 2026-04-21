using System;
using XLua;

// C# 端定义扩展方法
[LuaCallCSharp]
public static class LuaExtensions
{
    public static int RoundToInt(this double value)
    {
        return (int)Math.Round(value);
    }

    public static int FloorToInt(this double value)
    {
        return (int)Math.Floor(value);
    }

    public static int CeilToInt(this double value)
    {
        return (int)Math.Ceiling(value);
    }
}

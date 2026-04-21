using System.IO;
using TMPro;
using UnityEngine;
using XLua;

public class LuaManager : MonoSingleton<LuaManager>
{
    private LuaEnv luaEnv;
    public float gcInterval = 2;
    private float lastGCTime = 0;

    protected override void Awake()
    {
        base.Awake();
        luaEnv = new LuaEnv();
    }

    public void Init()
    {
        luaEnv.AddLoader(MyCustomLoader);
        DoScript("LuaInit");
    }

    public LuaTable DoScript(string moduleName)
    {
        moduleName = moduleName.Replace("/", ".");
        string script = string.Format("return require '{0}'", moduleName);
        return luaEnv.DoString(script)[0] as LuaTable;
    }

    public object[] DoString(string sentence)
    {
        return luaEnv.DoString(sentence);
    }

    public static byte[] MyCustomLoader(ref string fileName)
    {
        fileName = fileName.Replace(".", "/");
        string path = Application.streamingAssetsPath + "/Lua/" + fileName + ".lua";
        if (File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }
        else
        {
            Debug.LogWarning("找不到文件：" + path);
            return null;
        }
    }

    private void Update()
    {
        if (Time.time - lastGCTime > gcInterval)
        {
            lastGCTime = Time.time;
            luaEnv.Tick();
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        luaEnv.Dispose();
    }
}
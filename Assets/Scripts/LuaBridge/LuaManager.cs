using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// Owns the single LuaEnv for UI pages: registers the StreamingAssets/Lua
    /// loader, executes LuaInit.lua, requires page modules, creates page/cell
    /// instances through module.New(refs) and ticks/disposes the environment.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 4.2-4.3.
    /// </summary>
    public sealed class LuaManager : IDisposable
    {
        private const string LuaRoot = "Lua";

        private LuaEnv _env;
        private readonly List<LuaHost> _hosts =
            new List<LuaHost>();
        private bool _disposed;

        public bool IsReady => _env != null && !_disposed;

        public static LuaManager CreateDefault()
        {
            var manager = new LuaManager();
            manager.Initialize();
            return manager;
        }

        private void Initialize()
        {
            _env = new LuaEnv();
            _env.AddLoader(LoadLuaFile);
            _env.DoString(
                "require('Core.LuaInit')",
                "Core.LuaInit");
        }

        /// <summary>
        /// Creates a page instance from luaModule.New(refs). Returns a host that
        /// owns the instance lifecycle; Dispose it when the page is released.
        /// </summary>
        public LuaHost CreatePageHost(
            string luaModule,
            UIRef[] refs)
        {
            EnsureReady();
            if (string.IsNullOrEmpty(luaModule))
                throw new ArgumentException(
                    "Page Lua module path is required.",
                    nameof(luaModule));

            LuaTable module = RequireModule(luaModule);
            try
            {
                LuaFunction newFunction =
                    module.Get<LuaFunction>("New");
                if (newFunction == null)
                    throw new InvalidOperationException(
                        $"Lua module '{luaModule}' must expose New(refs).");
                using (LuaTable refsTable = BuildRefsTable(refs))
                {
                    object[] result =
                        newFunction.Call(refsTable);
                    if (result == null ||
                        result.Length == 0 ||
                        !(result[0] is LuaTable instance))
                    {
                        throw new InvalidOperationException(
                            $"Lua module '{luaModule}' New(refs) must return a page instance table.");
                    }
                    var host = new LuaHost();
                    host.BindPage((LuaTable)instance);
                    _hosts.Add(host);
                    return host;
                }
            }
            finally
            {
                module.Dispose();
            }
        }

        /// <summary>
        /// Creates a cell instance from luaModule.New(refs). Each cell must have
        /// its own Lua instance; Dispose the host when the cell is recycled away.
        /// </summary>
        public LuaHost CreateCellHost(
            string luaModule,
            UIRef[] refs)
        {
            EnsureReady();
            if (string.IsNullOrEmpty(luaModule))
                throw new ArgumentException(
                    "Cell Lua module path is required.",
                    nameof(luaModule));

            LuaTable module = RequireModule(luaModule);
            try
            {
                LuaFunction newFunction =
                    module.Get<LuaFunction>("New");
                if (newFunction == null)
                    throw new InvalidOperationException(
                        $"Lua module '{luaModule}' must expose New(refs).");
                using (LuaTable refsTable = BuildRefsTable(refs))
                {
                    object[] result =
                        newFunction.Call(refsTable);
                    if (result == null ||
                        result.Length == 0 ||
                        !(result[0] is LuaTable instance))
                    {
                        throw new InvalidOperationException(
                            $"Lua module '{luaModule}' New(refs) must return a cell instance table.");
                    }
                    var host = new LuaHost();
                    host.BindCell((LuaTable)instance);
                    _hosts.Add(host);
                    return host;
                }
            }
            finally
            {
                module.Dispose();
            }
        }

        public void Tick()
        {
            if (IsReady)
                _env.Tick();
        }

        /// <summary>
        /// Diagnostic read used by tests and debugging tools. Production pages
        /// must query C# read-only views instead of reading Lua globals.
        /// </summary>
        public int ReadGlobalInt(string key)
        {
            if (!IsReady || string.IsNullOrEmpty(key))
                return 0;
            return _env.Global.Get<int>(key);
        }

        public string ReadGlobalString(string key)
        {
            if (!IsReady || string.IsNullOrEmpty(key))
                return null;
            return _env.Global.Get<string>(key);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            for (int i = _hosts.Count - 1;
                 i >= 0;
                 i--)
            {
                _hosts[i].Dispose();
            }
            _hosts.Clear();
            _env?.Dispose();
            _env = null;
        }

        private LuaTable RequireModule(string luaModule)
        {
            object[] result =
                _env.DoString(
                    $"return require('{luaModule}')",
                    luaModule);
            if (result == null ||
                result.Length == 0 ||
                !(result[0] is LuaTable module))
            {
                throw new InvalidOperationException(
                    $"Failed to require Lua module '{luaModule}'.");
            }
            return (LuaTable)module;
        }

        private LuaTable BuildRefsTable(UIRef[] refs)
        {
            LuaTable table = _env.NewTable();
            if (refs == null)
                return table;
            for (int i = 0; i < refs.Length; i++)
            {
                UIRef entry = refs[i];
                if (string.IsNullOrEmpty(entry.Name))
                    continue;
                table.Set(entry.Name, entry.Value);
            }
            return table;
        }

        private static byte[] LoadLuaFile(ref string filepath)
        {
            string module = filepath;
            if (module.StartsWith(
                    "UI.",
                    StringComparison.Ordinal))
                module = module.Substring("UI.".Length);
            string relative =
                module.Replace('.', '/');
            string fullPath = Path.Combine(
                Application.streamingAssetsPath,
                LuaRoot,
                relative + ".lua");
            if (!File.Exists(fullPath))
                return null;
            return File.ReadAllBytes(fullPath);
        }

        private void EnsureReady()
        {
            if (!IsReady)
                throw new InvalidOperationException(
                    "LuaManager is not initialized.");
        }
    }
}

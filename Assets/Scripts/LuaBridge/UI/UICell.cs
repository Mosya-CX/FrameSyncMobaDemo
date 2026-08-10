using System;
using UnityEngine;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// Cell Prefab Lua host (UI design v9.1 7.3): holds the cell module
    /// instance and forwards SetIndex/Bind/Dispose to Lua.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UICell : MonoBehaviour
    {
        [SerializeField] private string luaModule;
        [SerializeField] private UIRef[] refs =
            Array.Empty<UIRef>();

        private LuaHost host;

        public bool HasLuaHost => host != null;

        public void Build(LuaManager manager)
        {
            if (string.IsNullOrEmpty(luaModule) ||
                manager == null)
                return;
            host?.Dispose();
            host = null;
            host = manager.CreateCellHost(
                luaModule,
                refs);
        }

        public void SetIndex(int index)
        {
            host?.SetIndex(index);
        }

        public void Bind(object data)
        {
            host?.Bind(data);
        }

        public void DisposeHost()
        {
            host?.Dispose();
            host = null;
        }

        private void OnDestroy()
        {
            DisposeHost();
        }
    }
}

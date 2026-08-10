using System;
using XLua;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// Lightweight C# proxy over one Lua page/cell instance LuaTable. It caches
    /// the fixed lifecycle delegates so UIPanel/UICell never touch LuaTable or
    /// LuaFunction call details.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 4.4-4.7.
    /// </summary>
    public sealed class LuaHost : IDisposable
    {
        private LuaTable _instance;
        private Action<LuaTable> _show;
        private Action<LuaTable> _refresh;
        private Action<LuaTable> _hide;
        private Action<LuaTable> _dispose;
        private LuaFunction _disposeFn;
        private LuaFunction _setIndex;
        private LuaFunction _bind;
        private bool _disposed;

        public bool IsBound => _instance != null;
        public bool IsDisposed => _disposed;

        public void BindPage(LuaTable value)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LuaHost));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            _instance = value;
            _show = value.Get<Action<LuaTable>>("Show");
            _refresh = value.Get<Action<LuaTable>>("Refresh");
            _hide = value.Get<Action<LuaTable>>("Hide");
            _dispose = value.Get<Action<LuaTable>>("Dispose");
        }

        public void BindCell(LuaTable value)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LuaHost));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            _instance = value;
            _setIndex = value.Get<LuaFunction>("SetIndex");
            _bind = value.Get<LuaFunction>("Bind");
            _disposeFn = value.Get<LuaFunction>("Dispose");
        }

        public void Show()
        {
            _show?.Invoke(_instance);
        }

        public void Refresh()
        {
            _refresh?.Invoke(_instance);
        }

        public void Hide()
        {
            _hide?.Invoke(_instance);
        }

        public void SetIndex(int index)
        {
            _setIndex?.Call(_instance, index);
        }

        public void Bind(object data)
        {
            _bind?.Call(_instance, data);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                _dispose?.Invoke(_instance);
                _disposeFn?.Call(_instance);
            }
            finally
            {
                _show = null;
                _refresh = null;
                _hide = null;
                _dispose = null;
                _disposeFn?.Dispose();
                _disposeFn = null;
                _setIndex?.Dispose();
                _setIndex = null;
                _bind?.Dispose();
                _bind = null;
                _instance?.Dispose();
                _instance = null;
            }
        }
    }
}

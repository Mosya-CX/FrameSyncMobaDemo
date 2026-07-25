using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// Lightweight managed Lua-like global state.
    /// Stores typed values in a hierarchical table structure
    /// that mirrors the Lua global namespace.
    ///
    /// At tick-end, LuaBridge pushes read-only UI data into this
    /// state. Lua scripts in StreamingAssets/Lua consume the data
    /// when a real Lua VM (MoonSharp/XLua) is integrated.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 section 1.6-1.9
    /// </summary>
    public sealed class LuaRuntime
    {
        private readonly Dictionary<string, object> _globals = new Dictionary<string, object>();
        private readonly Dictionary<string, Dictionary<string, object>> _tables
            = new Dictionary<string, Dictionary<string, object>>();

        /// <summary>
        /// Set a top-level global value.
        /// </summary>
        public void SetGlobal(string key, int value)
            => _globals[key] = value;

        public void SetGlobal(string key, fp value)
            => _globals[key] = value;

        public void SetGlobal(string key, float value)
            => _globals[key] = value;

        public void SetGlobal(string key, string value)
            => _globals[key] = value;

        /// <summary>
        /// Set a field inside a named table (e.g. "HUD.Gold").
        /// </summary>
        public void SetTableField(string tableName, string field, int value)
        {
            var table = GetOrCreateTable(tableName);
            table[field] = value;
        }

        public void SetTableField(string tableName, string field, fp value)
        {
            var table = GetOrCreateTable(tableName);
            table[field] = value;
        }

        public void SetTableField(string tableName, string field, float value)
        {
            var table = GetOrCreateTable(tableName);
            table[field] = value;
        }

        public void SetTableField(string tableName, string field, string value)
        {
            var table = GetOrCreateTable(tableName);
            table[field] = value;
        }

        /// <summary>
        /// Get a global value. Returns false if key does not exist.
        /// </summary>
        public bool TryGetGlobal<T>(string key, out T value)
        {
            if (_globals.TryGetValue(key, out object obj) && obj is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Get a field from a named table.
        /// </summary>
        public bool TryGetTableField<T>(string tableName, string field, out T value)
        {
            if (_tables.TryGetValue(tableName, out var table)
                && table.TryGetValue(field, out object obj)
                && obj is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Clear all globals and tables for a fresh tick.
        /// </summary>
        public void Clear()
        {
            _globals.Clear();
            _tables.Clear();
        }

        /// <summary>
        /// Set a named array of integers in a table.
        /// </summary>
        public void SetTableArray(string tableName, string field, int[] values)
        {
            var table = GetOrCreateTable(tableName);
            table[field] = values ?? Array.Empty<int>();
        }

        /// <summary>
        /// Set a named array of strings in a table.
        /// </summary>
        public void SetTableArray(string tableName, string field, string[] values)
        {
            var table = GetOrCreateTable(tableName);
            table[field] = values ?? Array.Empty<string>();
        }

        private Dictionary<string, object> GetOrCreateTable(string name)
        {
            if (!_tables.TryGetValue(name, out var table))
            {
                table = new Dictionary<string, object>();
                _tables[name] = table;
            }
            return table;
        }
    }
}

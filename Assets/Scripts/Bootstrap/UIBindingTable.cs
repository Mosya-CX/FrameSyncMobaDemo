using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    [CreateAssetMenu(fileName = "UIBindingTable", menuName = "FrameSyncMoba/UI/Binding Table")]
    public sealed class UIBindingTable : ScriptableObject
    {
        [SerializeField] private List<BindingEntry> entries = new List<BindingEntry>();
        public IReadOnlyList<BindingEntry> Entries => entries;

        public bool TryGetLuaPath(string gameplayField, out string luaPath)
        {
            foreach (var entry in entries)
            {
                if (entry.GameplayField == gameplayField)
                {
                    luaPath = entry.LuaGlobalPath;
                    return true;
                }
            }
            luaPath = null;
            return false;
        }

        public string[] ValidateRequired(params string[] requiredFields)
        {
            var missing = new List<string>();
            foreach (var field in requiredFields)
            {
                if (!TryGetLuaPath(field, out _))
                    missing.Add(field);
            }
            return missing.ToArray();
        }

        [Serializable]
        public struct BindingEntry
        {
            public string GameplayField;
            public string LuaGlobalPath;
        }
    }
}

using System;
using UnityEngine;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// Inspector-configured binding from a stable name to a Unity object on the
    /// page Prefab. UIPanel converts these into the Lua "refs" table so page
    /// Lua can access components by name.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 section 3.2.
    /// </summary>
    [Serializable]
    public struct UIRef
    {
        public string Name;
        public UnityEngine.Object Value;

        public UIRef(string name, UnityEngine.Object value)
        {
            Name = name;
            Value = value;
        }
    }
}

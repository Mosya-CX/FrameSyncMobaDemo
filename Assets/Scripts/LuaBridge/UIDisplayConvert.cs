using Unity.Mathematics.FixedPoint;
using UnityEngine;
using XLua;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// Single authority for converting deterministic fp values into UI display
    /// values. Lua must never parse fp raw values itself.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 section 8.
    /// </summary>
    [LuaCallCSharp]
    public static class UIDisplayConvert
    {
        public static float Float(fp value)
        {
            return (float)value;
        }

        /// <summary>
        /// Resources (HP, mana, experience) round down.
        /// </summary>
        public static int ResourceInt(fp value)
        {
            return Mathf.Max(0, Mathf.FloorToInt((float)value));
        }

        /// <summary>
        /// Ordinary stats round to the nearest integer.
        /// </summary>
        public static int StatInt(fp value)
        {
            return Mathf.RoundToInt((float)value);
        }

        public static float Decimal2(fp value)
        {
            return Mathf.Round((float)value * 100f) / 100f;
        }

        public static int PercentInt(fp rate)
        {
            return Mathf.RoundToInt((float)rate * 100f);
        }

        public static float Rate01(fp current, fp max)
        {
            if (max <= fp.zero)
                return 0f;
            return Mathf.Clamp01((float)(current / max));
        }
    }
}

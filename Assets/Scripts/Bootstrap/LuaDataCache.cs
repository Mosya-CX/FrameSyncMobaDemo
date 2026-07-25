using FrameSyncMoba.LuaBridge;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Thread-safe static cache for the latest UiSnapshotDto.
    /// Populated by GameBootstrap at tick-end, consumed by UI
    /// controllers each Unity frame.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 section 10
    /// This cache is presentation-only and not restored during
    /// rollback.
    /// </summary>
    public static class LuaDataCache
    {
        private static readonly object _lock = new object();
        private static UiSnapshotDto _latest = UiSnapshotDto.Empty;

        public static UiSnapshotDto Latest
        {
            get { lock (_lock) { return _latest; } }
            set { lock (_lock) { _latest = value; } }
        }

        public static fp CurrentHealth
        {
            get { lock (_lock) { return _latest.CurrentHealth; } }
        }

        public static fp MaxHealth
        {
            get { lock (_lock) { return _latest.MaxHealth; } }
        }

        public static int CurrentGold
        {
            get { lock (_lock) { return _latest.CurrentGold; } }
        }

        public static int CooldownRemaining(int slot)
        {
            lock (_lock)
            {
                switch (slot)
                {
                    case 0: return _latest.CooldownRemaining0;
                    case 1: return _latest.CooldownRemaining1;
                    case 2: return _latest.CooldownRemaining2;
                    case 3: return _latest.CooldownRemaining3;
                    default: return 0;
                }
            }
        }

        public static int CooldownTotal(int slot)
        {
            lock (_lock)
            {
                switch (slot)
                {
                    case 0: return _latest.CooldownTotal0;
                    case 1: return _latest.CooldownTotal1;
                    case 2: return _latest.CooldownTotal2;
                    case 3: return _latest.CooldownTotal3;
                    default: return 1;
                }
            }
        }

        public static bool HasValidData
        {
            get { lock (_lock) { return _latest.MaxHealth > fp.zero; } }
        }
    }
}

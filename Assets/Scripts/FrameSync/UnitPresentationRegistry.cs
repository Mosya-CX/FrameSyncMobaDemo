using System.Collections.Generic;

namespace FrameSyncMoba.FrameSync
{
    public static class UnitPresentationRegistry
    {
        private static readonly Dictionary<Unit.UnitUid, UnitPresentationHost> Hosts
            = new Dictionary<Unit.UnitUid, UnitPresentationHost>();

        public static void Register(Unit.UnitUid uid, UnitPresentationHost host)
        {
            Hosts[uid] = host;
        }

        public static void Unregister(Unit.UnitUid uid)
        {
            Hosts.Remove(uid);
        }

        public static bool TryGetHost(Unit.UnitUid uid, out UnitPresentationHost host)
        {
            return Hosts.TryGetValue(uid, out host);
        }
    }
}

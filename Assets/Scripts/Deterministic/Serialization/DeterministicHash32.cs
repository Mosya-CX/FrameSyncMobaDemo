using System.Text;

namespace FrameSyncMoba.Deterministic
{
    public static class DeterministicHash32
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint Utf8(string key)
        {
            if (key == null)
            {
                return 0u;
            }

            uint hash = FnvOffsetBasis;
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= FnvPrime;
            }

            return hash;
        }

        public static uint Compute(byte[] data, int offset, int count)
        {
            uint hash = FnvOffsetBasis;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                hash ^= data[i];
                hash *= FnvPrime;
            }

            return hash;
        }
    }
}

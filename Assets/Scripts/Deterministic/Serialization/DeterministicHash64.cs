namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Pure, allocation-free 64-bit integer hashing for deterministic keyed
    /// choices. This never reads or mutates DeterministicRandomService state.
    /// </summary>
    public static class DeterministicHash64
    {
        private const ulong InitialState = 0x9E3779B97F4A7C15UL;

        public static ulong Compute(
            ulong first,
            ulong second,
            ulong third,
            ulong fourth,
            ulong fifth)
        {
            ulong state = InitialState;
            state = Mix(state, first);
            state = Mix(state, second);
            state = Mix(state, third);
            state = Mix(state, fourth);
            return Mix(state, fifth);
        }

        private static ulong Mix(ulong state, ulong value)
        {
            unchecked
            {
                ulong mixed = value + InitialState;
                mixed = (mixed ^ (mixed >> 30)) *
                        0xBF58476D1CE4E5B9UL;
                mixed = (mixed ^ (mixed >> 27)) *
                        0x94D049BB133111EBUL;
                mixed ^= mixed >> 31;

                state ^= mixed;
                state = (state << 27) | (state >> 37);
                state = (state * 5UL) + 0x52DCE729UL;
                return state;
            }
        }
    }
}

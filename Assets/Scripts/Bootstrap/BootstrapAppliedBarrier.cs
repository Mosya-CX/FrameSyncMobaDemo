using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Application-flow barrier for the frozen match roster. PlayerSlot order
    /// is the canonical order; no collection enumeration affects completion.
    /// </summary>
    internal sealed class BootstrapAppliedBarrier
    {
        private string matchId;
        private int startTick;
        private ulong[] expectedClientIds = Array.Empty<ulong>();
        private bool[] applied = Array.Empty<bool>();
        private int appliedCount;

        public bool IsInitialized => expectedClientIds.Length > 0;
        public bool IsComplete =>
            IsInitialized &&
            appliedCount == expectedClientIds.Length;
        public int AppliedCount => appliedCount;
        public int ExpectedCount => expectedClientIds.Length;

        public void Initialize(
            in GameStartConfig config)
        {
            config.ValidateOrThrow();
            PlayerSlotConfig[] slots = config.PlayerSlots;
            matchId = config.MatchId;
            startTick = config.StartTick;
            expectedClientIds = new ulong[slots.Length];
            applied = new bool[slots.Length];
            appliedCount = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].PlayerSlot != i)
                    throw new DeterministicSimulationException(
                        "Bootstrap barrier requires ascending PlayerSlot order.");
                expectedClientIds[i] =
                    slots[i].ControllerClientId;
            }
        }

        /// <summary>
        /// Marks one expected client. Returns true only on the transition from
        /// incomplete to complete; an identical duplicate is idempotent.
        /// </summary>
        public bool MarkApplied(
            ulong senderClientId,
            in BootstrapAppliedConfirmation confirmation)
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "Bootstrap barrier is not initialized.");
            confirmation.ValidateOrThrow();
            if (!string.Equals(
                    confirmation.MatchId,
                    matchId,
                    StringComparison.Ordinal) ||
                confirmation.StartTick != startTick)
                throw new DeterministicSimulationException(
                    "BootstrapAppliedConfirmation does not match the pending bootstrap.");

            int slot = FindClientSlot(senderClientId);
            if (slot < 0)
                throw new DeterministicSimulationException(
                    "BootstrapAppliedConfirmation sender is not in the frozen roster.");
            if (applied[slot])
                return false;

            bool wasComplete = IsComplete;
            applied[slot] = true;
            appliedCount++;
            return !wasComplete && IsComplete;
        }

        private int FindClientSlot(
            ulong senderClientId)
        {
            for (int i = 0;
                 i < expectedClientIds.Length;
                 i++)
            {
                if (expectedClientIds[i] ==
                    senderClientId)
                    return i;
            }
            return -1;
        }
    }
}

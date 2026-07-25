using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Produces periodic natural gold income for each player during the Running phase.
    /// Design: FrameSync_Flow_Integrated_System_Design_v10_2 section 5, 14.
    /// Uses configurable interval (ticks) and amount per interval.
    /// Only activates during MatchPhase.Running.
    /// </summary>
    public sealed class NaturalGoldIncomeSystem
    {
        private int _tickCounter;
        private int _intervalTicks;
        private int _amountPerInterval;
        private GoldIncomeRuntime _goldIncome;
        private Func<bool> _isRunningPhase;

        /// <summary>Interval in logic ticks between gold distributions.</summary>
        public int IntervalTicks
        {
            get => _intervalTicks;
            set => _intervalTicks = Math.Max(1, value);
        }

        /// <summary>Gold amount distributed per interval per player.</summary>
        public int AmountPerInterval
        {
            get => _amountPerInterval;
            set => _amountPerInterval = Math.Max(0, value);
        }

        /// <summary>Max player slots to distribute to.</summary>
        public int MaxPlayers { get; set; }

        public NaturalGoldIncomeSystem(
            GoldIncomeRuntime goldIncome,
            Func<bool> isRunningPhase,
            int intervalTicks = 15,
            int amountPerInterval = 2,
            int maxPlayers = 10)
        {
            _goldIncome = goldIncome ?? throw new ArgumentNullException(nameof(goldIncome));
            _isRunningPhase = isRunningPhase ?? throw new ArgumentNullException(nameof(isRunningPhase));
            _intervalTicks = Math.Max(1, intervalTicks);
            _amountPerInterval = Math.Max(0, amountPerInterval);
            MaxPlayers = Math.Max(0, maxPlayers);
            _tickCounter = 0;
        }

        /// <summary>
        /// Advance one logic tick. When the interval elapses, requests natural gold
        /// for each player slot in ascending order.
        /// Only produces gold during the Running phase.
        /// </summary>
        public void Tick()
        {
            if (!_isRunningPhase())
            {
                _tickCounter = 0;
                return;
            }

            _tickCounter++;
            if (_tickCounter < _intervalTicks)
                return;

            _tickCounter = 0;

            if (_amountPerInterval <= 0 || MaxPlayers <= 0)
                return;

            // Per design: distribute in ascending PlayerSlot order
            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                _goldIncome.RequestGoldIncome(
                    slot,
                    _amountPerInterval,
                    GoldIncomeReason.NaturalIncome);
            }
        }

        /// <summary>Reset internal counter (e.g. on match restart).</summary>
        public void Reset()
        {
            _tickCounter = 0;
        }
    }
}

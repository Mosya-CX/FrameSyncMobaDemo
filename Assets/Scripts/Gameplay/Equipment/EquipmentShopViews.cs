using System;

namespace FrameSyncMoba.Unit
{
    public interface IConfirmedGoldIncomeView
    {
        int GetConfirmedEarnedGoldTotal(
            int playerSlot);
    }

    public interface IEquipmentShopView
    {
        int GetCurrentAvailableGold();
        int CalculatePurchasePrice(
            int targetEquipmentId);
        bool CanUndo();
    }

    public sealed class EquipmentShopView :
        IEquipmentShopView
    {
        private readonly EquipmentShopRuntime runtime;
        private readonly IConfirmedGoldIncomeView income;
        private readonly int playerSlot;

        public EquipmentShopView(
            EquipmentShopRuntime runtime,
            IConfirmedGoldIncomeView income,
            int playerSlot)
        {
            this.runtime = runtime ??
                throw new ArgumentNullException(
                    nameof(runtime));
            this.income = income ??
                throw new ArgumentNullException(
                    nameof(income));
            if (playerSlot < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(playerSlot));
            this.playerSlot = playerSlot;
        }

        public int GetCurrentAvailableGold()
        {
            return checked(
                income.GetConfirmedEarnedGoldTotal(
                    playerSlot) +
                runtime
                    .ComputeEffectiveShopGoldDelta(
                        playerSlot));
        }

        public int CalculatePurchasePrice(
            int targetEquipmentId)
        {
            return runtime.CalculatePurchasePrice(
                playerSlot,
                targetEquipmentId);
        }

        public bool CanUndo()
        {
            return runtime.CanUndo(
                playerSlot,
                GetCurrentAvailableGold(),
                out _);
        }
    }
}

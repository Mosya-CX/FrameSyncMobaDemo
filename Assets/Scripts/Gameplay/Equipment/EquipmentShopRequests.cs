namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Local RequestCheck result (Equipment/Gold v12, UI design v9.1 12.1/12.7).
    /// Allowed == true means the canonical Command was submitted, not that the
    /// transaction succeeded at the target LogicTick.
    /// </summary>
    public struct EquipmentShopRequestCheck
    {
        public bool Allowed;
        public EquipmentShopFailureReason FailureReason;

        public static EquipmentShopRequestCheck Allow()
        {
            return new EquipmentShopRequestCheck
            {
                Allowed = true,
                FailureReason = EquipmentShopFailureReason.None,
            };
        }

        public static EquipmentShopRequestCheck Reject(
            EquipmentShopFailureReason reason)
        {
            return new EquipmentShopRequestCheck
            {
                Allowed = false,
                FailureReason = reason,
            };
        }
    }

    /// <summary>
    /// Command submission port owned by the composition root. UI never obtains
    /// this port (UI design v9.1 12.5); the runtime calls it only after its
    /// local RequestCheck passes.
    /// </summary>
    public interface IEquipmentShopCommandSubmitter
    {
        void SubmitPurchase(
            int playerSlot,
            int targetEquipmentId);

        void SubmitSell(
            int playerSlot,
            int sourceSlot);

        void SubmitUndo(
            int playerSlot);
    }
}

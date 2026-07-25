namespace FrameSyncMoba.FrameSync
{
    public enum GameplayCommandKind : byte
    {
        None = 0,
        Move = 1,
        Attack = 2,
        CastAbility = 3,
        CancelAbility = 4,
        AllocateAbilitySkillPoint = 5,
        EquipmentShop = 6,
        SwapEquipmentSlot = 7,
        UseItem = 8,
        EquipmentUndo = 9,
    }
}

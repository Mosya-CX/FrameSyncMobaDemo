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
        Debug = 9,
    }

    /// <summary>
    /// Deterministic debug operations (GameScene testing only). Each op is
    /// applied by the simulation on the command's controlled unit so every
    /// endpoint stays in sync.
    /// </summary>
    public enum DebugCommandOp : byte
    {
        Heal = 0,
        RestoreMana = 1,
        Revive = 2,
        LevelUp = 3,
        AddGold = 4,
        /// <summary>Instantly kill the command's target unit through the
        /// normal Combat death settlement (debug/test scenes only). The
        /// caller is responsible for excluding structures when desired.</summary>
        Kill = 5,
    }
}

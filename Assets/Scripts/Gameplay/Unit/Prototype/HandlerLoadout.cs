using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit Framework v27.3 §1.6 — which Handlers a Unit prefab is expected
    /// to carry. The presence of a Handler drives the UnitAbilityMask and
    /// determines which action categories are available at runtime.
    ///
    /// This is static configuration loaded from UnitPrototype.
    /// </summary>
    [Serializable]
    public struct HandlerLoadout
    {
        /// <summary>Whether the Unit has a MovementHandler.</summary>
        public bool HasMovement;

        /// <summary>Whether the Unit has an AttackHandler.</summary>
        public bool HasAttack;

        /// <summary>Whether the Unit has an AbilityHandler.</summary>
        public bool HasAbility;

        /// <summary>Whether the Unit has a BuffHandler.</summary>
        public bool HasBuff;

        /// <summary>Whether the Unit has a CrowdControlHandler.</summary>
        public bool HasCrowdControl;

        /// <summary>Whether the Unit has an EquipmentHandler.</summary>
        public bool HasEquipment;

        public static readonly HandlerLoadout DefaultHero = new HandlerLoadout
        {
            HasMovement = true,
            HasAttack = true,
            HasAbility = true,
            HasBuff = true,
            HasCrowdControl = true,
            HasEquipment = true,
        };

        public static readonly HandlerLoadout DefaultMinion = new HandlerLoadout
        {
            HasMovement = true,
            HasAttack = true,
            HasAbility = false,
            HasBuff = true,
            HasCrowdControl = true,
            HasEquipment = false,
        };

        public static readonly HandlerLoadout DefaultMonster = new HandlerLoadout
        {
            HasMovement = true,
            HasAttack = true,
            HasAbility = false,
            HasBuff = true,
            HasCrowdControl = true,
            HasEquipment = false,
        };

        public static readonly HandlerLoadout DefaultTower = new HandlerLoadout
        {
            HasMovement = false,
            HasAttack = true,
            HasAbility = false,
            HasBuff = false,
            HasCrowdControl = false,
            HasEquipment = false,
        };

        public UnitAbilityMask BuildAbilityMask()
        {
            return new UnitAbilityMask(HasMovement, HasAttack, HasAbility);
        }
    }
}

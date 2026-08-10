using System;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Authoring asset for a hero's fixed passive ability (design v15.2
    /// section 6.5). Bakes into a PassiveAbilityDef registered in the
    /// AbilityDefinitionRegistry and applied to a unit through its loadout.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FixedPassiveDefinition",
        menuName = "FrameSyncMoba/Ability/Fixed Passive Definition")]
    public sealed class FixedPassiveDefinitionAsset :
        ScriptableObject
    {
        [SerializeField] private int abilityId;
        [SerializeField] private string abilityName = "Passive";
        [SerializeField] private Sprite icon;
        [SerializeReference]
        [SerializeField] private PassiveAbilityEffectDef passiveEffect;
        [SerializeField] private int[] cooldownByUnitLevel =
            Array.Empty<int>();

        public int AbilityId => abilityId;
        public string AbilityName => abilityName;
        public Sprite Icon => icon;
        public PassiveAbilityEffectDef PassiveEffect =>
            passiveEffect;

        public PassiveAbilityDef Bake()
        {
            if (abilityId <= 0)
                throw new InvalidOperationException(
                    $"FixedPassive '{name}' requires a positive AbilityId.");
            if (string.IsNullOrWhiteSpace(abilityName))
                throw new InvalidOperationException(
                    $"FixedPassive '{name}' requires a name.");
            if (passiveEffect == null)
                throw new InvalidOperationException(
                    $"FixedPassive '{name}' requires a passive effect.");
            var definition = new PassiveAbilityDef
            {
                AbilityId = abilityId,
                Name = abilityName,
                Icon = icon,
                PassiveEffect = passiveEffect,
                CooldownByUnitLevel =
                    cooldownByUnitLevel != null
                        ? (int[])cooldownByUnitLevel.Clone()
                        : Array.Empty<int>(),
            };
            if (!definition.IsValid)
                throw new InvalidOperationException(
                    $"FixedPassive '{name}' baked an invalid definition.");
            return definition;
        }
    }
}

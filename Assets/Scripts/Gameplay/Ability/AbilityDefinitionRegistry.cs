using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    public sealed class AbilityDefinitionRegistry
    {
        private readonly Dictionary<int, AbilityDef> definitions =
            new Dictionary<int, AbilityDef>();
        private readonly Dictionary<int, PassiveAbilityDef> passiveDefinitions =
            new Dictionary<int, PassiveAbilityDef>();

        public void Register(AbilityDef definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definition.IsValid) throw new ArgumentException("Ability definition is invalid.", nameof(definition));
            if (definitions.ContainsKey(definition.AbilityId) ||
                passiveDefinitions.ContainsKey(definition.AbilityId))
                throw new InvalidOperationException($"Duplicate AbilityId {definition.AbilityId}.");
            definitions.Add(definition.AbilityId, definition);
        }

        public bool TryGet(int abilityId, out AbilityDef definition) =>
            definitions.TryGetValue(abilityId, out definition);

        public void Register(PassiveAbilityDef definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definition.IsValid)
                throw new ArgumentException("Passive Ability definition is invalid.", nameof(definition));
            if (definitions.ContainsKey(definition.AbilityId) ||
                passiveDefinitions.ContainsKey(definition.AbilityId))
                throw new InvalidOperationException($"Duplicate AbilityId {definition.AbilityId}.");
            definition.PassiveEffect.ValidateOrThrow();
            passiveDefinitions.Add(definition.AbilityId, definition);
        }

        public bool TryGetPassive(int abilityId, out PassiveAbilityDef definition) =>
            passiveDefinitions.TryGetValue(abilityId, out definition);
    }
}

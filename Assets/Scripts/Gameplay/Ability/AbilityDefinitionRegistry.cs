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
        private readonly Dictionary<byte, AbilitySlotDef> slotDefinitions =
            new Dictionary<byte, AbilitySlotDef>();

        public void Register(AbilityDef definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            ValidateDefinition(definition);
            if (definitions.ContainsKey(definition.AbilityId) ||
                passiveDefinitions.ContainsKey(definition.AbilityId))
                throw new InvalidOperationException($"Duplicate AbilityId {definition.AbilityId}.");
            definitions.Add(definition.AbilityId, definition);
        }

        /// <summary>
        /// Register an AbilityDef baked from an AbilityAsset (ScriptableObject).
        /// This is the Editor-time entry point called by AbilityRegistryPopulator.
        /// </summary>
        public bool TryRegisterFromAsset(AbilityAsset asset)
        {
            if (asset == null) return false;
            AbilityDef def = asset.Bake();
            Register(def);
            return true;
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

        public void RegisterSlot(AbilitySlotDef definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (slotDefinitions.ContainsKey(definition.SlotId))
                throw new InvalidOperationException(
                    $"Duplicate Ability SlotId {definition.SlotId}.");
            definition.ValidateOrThrow(this);
            slotDefinitions.Add(definition.SlotId, definition);
        }

        public bool TryGetSlot(
            byte slotId,
            out AbilitySlotDef definition) =>
            slotDefinitions.TryGetValue(slotId, out definition);

        public IReadOnlyList<AbilityDef> GetAllOrdered()
        {
            var result =
                new List<AbilityDef>(definitions.Values);
            result.Sort((left, right) =>
                left.AbilityId.CompareTo(right.AbilityId));
            return result;
        }

        private static void ValidateDefinition(
            AbilityDef definition)
        {
            if (!definition.IsValid)
                throw new ArgumentException(
                    "Ability definition is invalid.",
                    nameof(definition));
            if (!Enum.IsDefined(
                    typeof(AimKind),
                    definition.AimKind))
                throw new ArgumentException(
                    "Ability AimKind is invalid.",
                    nameof(definition));
            if (definition.CastConditions == null)
                throw new ArgumentException(
                    "Ability CastConditions must not be null.",
                    nameof(definition));
            for (int i = 0;
                 i < definition.CastConditions.Length;
                 i++)
            {
                if (definition.CastConditions[i] == null)
                    throw new ArgumentException(
                        $"Ability CastCondition {i} is null.",
                        nameof(definition));
            }

            var stages = new List<CastStage>(3);
            CollectStages(definition.CastModel, stages);
            if (stages.Count == 0)
                throw new ArgumentException(
                    "Ability CastModel has no stages.",
                    nameof(definition));
            var stageKeys = new HashSet<byte>();
            for (int i = 0; i < stages.Count; i++)
            {
                if (!stages[i].IsValid ||
                    !stageKeys.Add(stages[i].StageKey))
                {
                    throw new ArgumentException(
                        "Ability stages must be valid and have unique keys.",
                        nameof(definition));
                }
            }
        }

        private static void CollectStages(
            CastModelDef model,
            List<CastStage> output)
        {
            switch (model)
            {
                case CommitCastModelDef commit:
                    output.Add(commit.Cast);
                    break;
                case HoldReleaseCastModelDef hold:
                    output.Add(hold.Hold);
                    output.Add(hold.Release);
                    break;
                case ChannelCastModelDef channel:
                    output.Add(channel.Channel);
                    break;
                case ActiveSignalCastModelDef active:
                    output.Add(active.Active);
                    break;
                case ToggleCastModelDef toggle:
                    output.Add(toggle.Active);
                    break;
                case GroundTargetCastModelDef ground:
                    output.Add(ground.Aim);
                    output.Add(ground.Execute);
                    break;
                case VectorTargetCastModelDef vector:
                    output.Add(vector.Aim);
                    output.Add(vector.Execute);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unsupported CastModel {model?.GetType().Name}.");
            }
        }
    }
}

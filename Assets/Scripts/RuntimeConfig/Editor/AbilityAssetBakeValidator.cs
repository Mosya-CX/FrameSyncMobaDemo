using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Editor-time validator for AbilityAsset configuration.
    /// Validates stage chains, AimKind/CastModelDef mismatches,
    /// and required resource integrity.
    /// Reports errors via Debug.LogError so they are visible in
    /// the Unity Console and Inspector.
    ///
    /// Design: moba_ability_system_design_v15_2 section 5
    /// </summary>
    public static class AbilityAssetBakeValidator
    {
        public struct ValidationResult
        {
            public bool IsValid;
            public string[] Errors;

            public static ValidationResult Success => new ValidationResult
            {
                IsValid = true,
                Errors = System.Array.Empty<string>(),
            };

            public static ValidationResult Failure(params string[] errors) => new ValidationResult
            {
                IsValid = false,
                Errors = errors ?? System.Array.Empty<string>(),
            };
        }

        public static ValidationResult Validate(AbilityAsset asset)
        {
            if (asset == null)
                return ValidationResult.Failure("AbilityAsset is null.");

            var errors = new List<string>();

            if (asset.AbilityId <= 0)
                errors.Add(string.Format("AbilityAsset '{0}': AbilityId must be positive.", asset.name));

            if (string.IsNullOrWhiteSpace(asset.AbilityName))
                errors.Add(string.Format("AbilityAsset '{0}': AbilityName is required.", asset.name));

            var castModel = asset.CastModel;
            if (castModel == null)
            {
                errors.Add(string.Format("AbilityAsset '{0}': CastModel is required.", asset.name));
            }
            else
            {
                ValidateCastModel(asset, castModel, errors);
            }

            ValidateAimKindConsistency(asset, castModel, errors);
            ValidateStages(asset, errors);
            ValidatePassiveEffect(asset, errors);

            ValidateLevelValues(
                asset,
                asset.CastResourceCostByLevel,
                "CastResourceCost",
                errors);
            ValidateLevelValues(
                asset,
                asset.HealthCostByLevel,
                "HealthCost",
                errors);

            if (errors.Count == 0)
                return ValidationResult.Success;

            return ValidationResult.Failure(errors.ToArray());
        }

        private static void ValidateCastModel(
            AbilityAsset asset,
            CastModelAuthoring castModel,
            List<string> errors)
        {
            switch (castModel.Kind)
            {
                case CastModelKind.Commit:
                {
                    var m = castModel as CommitCastModelAuthoring;
                    if (m == null)
                    {
                        errors.Add(string.Format("AbilityAsset '{0}': Commit cast model is null.", asset.name));
                        break;
                    }
                    if (!HasStage(asset, m.CastStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': Commit stage key {1} " +
                            "has no matching StageDef.", asset.name, m.CastStageKey));
                    if (m.DurationTicks < 0)
                        errors.Add(string.Format("AbilityAsset '{0}': DurationTicks must be non-negative.", asset.name));
                    break;
                }

                case CastModelKind.HoldRelease:
                {
                    var m = castModel as HoldReleaseCastModelAuthoring;
                    if (m == null)
                    {
                        errors.Add(string.Format("AbilityAsset '{0}': HoldRelease cast model is null.", asset.name));
                        break;
                    }
                    if (m.HoldStageKey == m.ReleaseStageKey)
                        errors.Add(string.Format("AbilityAsset '{0}': Hold and Release stage keys must differ.", asset.name));
                    if (m.HoldDurationTicks < 0)
                        errors.Add(string.Format("AbilityAsset '{0}': HoldDurationTicks must be non-negative.", asset.name));
                    if (m.ReleaseDurationTicks < 0)
                        errors.Add(string.Format("AbilityAsset '{0}': ReleaseDurationTicks must be non-negative.", asset.name));

                    if (!HasStage(asset, m.HoldStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': Hold stage key {1} " +
                            "has no matching StageDef.", asset.name, m.HoldStageKey));
                    if (!HasStage(asset, m.ReleaseStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': Release stage key {1} " +
                            "has no matching StageDef.", asset.name, m.ReleaseStageKey));
                    break;
                }

                case CastModelKind.Channel:
                {
                    var m = castModel as ChannelCastModelAuthoring;
                    if (m == null)
                    {
                        errors.Add(string.Format("AbilityAsset '{0}': Channel cast model is null.", asset.name));
                        break;
                    }
                    if (m.DurationTicks < 0)
                        errors.Add(string.Format("AbilityAsset '{0}': DurationTicks must be non-negative.", asset.name));
                    if (!HasStage(asset, m.ChannelStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': Channel stage key {1} " +
                            "has no matching StageDef.", asset.name, m.ChannelStageKey));
                    break;
                }

                case CastModelKind.ActiveSignal:
                {
                    var m = castModel as ActiveSignalCastModelAuthoring;
                    if (m == null)
                    {
                        errors.Add(string.Format("AbilityAsset '{0}': ActiveSignal cast model is null.", asset.name));
                        break;
                    }
                    if (m.DurationTicks < 0)
                        errors.Add(string.Format("AbilityAsset '{0}': DurationTicks must be non-negative.", asset.name));
                    if (!HasStage(asset, m.ActiveStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': ActiveSignal stage key {1} " +
                            "has no matching StageDef.", asset.name, m.ActiveStageKey));
                    break;
                }

                case CastModelKind.Toggle:
                {
                    var m = castModel as ToggleCastModelAuthoring;
                    if (m == null)
                    {
                        errors.Add(string.Format("AbilityAsset '{0}': Toggle cast model is null.", asset.name));
                        break;
                    }
                    if (m.DurationTicks < 0)
                        errors.Add(string.Format("AbilityAsset '{0}': DurationTicks must be non-negative.", asset.name));
                    if (m.ResourcePerTick < 0f)
                        errors.Add(string.Format("AbilityAsset '{0}': ResourcePerTick must be non-negative.", asset.name));
                    if (!HasStage(asset, m.ActiveStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': Toggle stage key {1} " +
                            "has no matching StageDef.", asset.name, m.ActiveStageKey));
                    break;
                }

                case CastModelKind.GroundTarget:
                {
                    var m = castModel as GroundTargetCastModelAuthoring;
                    if (m == null)
                    {
                        errors.Add(string.Format("AbilityAsset '{0}': GroundTarget cast model is null.", asset.name));
                        break;
                    }
                    if (m.AimStageKey == m.ExecuteStageKey)
                        errors.Add(string.Format("AbilityAsset '{0}': Aim and Execute stage keys must differ.", asset.name));
                    if (m.AimDurationTicks < 0 || m.ExecuteDurationTicks < 0)
                        errors.Add(string.Format("AbilityAsset '{0}': Aim/Execute DurationTicks must be non-negative.", asset.name));
                    if (m.MaxRange < 0f)
                        errors.Add(string.Format("AbilityAsset '{0}': MaxRange must be non-negative.", asset.name));
                    if (!HasStage(asset, m.AimStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': Aim stage key {1} " +
                            "has no matching StageDef.", asset.name, m.AimStageKey));
                    if (!HasStage(asset, m.ExecuteStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': Execute stage key {1} " +
                            "has no matching StageDef.", asset.name, m.ExecuteStageKey));
                    break;
                }

                case CastModelKind.VectorTarget:
                {
                    var m = castModel as VectorTargetCastModelAuthoring;
                    if (m == null)
                    {
                        errors.Add(string.Format("AbilityAsset '{0}': VectorTarget cast model is null.", asset.name));
                        break;
                    }
                    if (m.AimStageKey == m.ExecuteStageKey)
                        errors.Add(string.Format("AbilityAsset '{0}': Aim and Execute stage keys must differ.", asset.name));
                    if (m.AimDurationTicks < 0 || m.ExecuteDurationTicks < 0)
                        errors.Add(string.Format("AbilityAsset '{0}': Aim/Execute DurationTicks must be non-negative.", asset.name));
                    if (m.MaxRange < 0f || m.MinRange < 0f)
                        errors.Add(string.Format("AbilityAsset '{0}': MaxRange/MinRange must be non-negative.", asset.name));
                    if (!HasStage(asset, m.AimStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': Aim stage key {1} " +
                            "has no matching StageDef.", asset.name, m.AimStageKey));
                    if (!HasStage(asset, m.ExecuteStageKey))
                        errors.Add(string.Format("AbilityAsset '{0}': Execute stage key {1} " +
                            "has no matching StageDef.", asset.name, m.ExecuteStageKey));
                    break;
                }
            }
        }

        private static void ValidateAimKindConsistency(
            AbilityAsset asset,
            CastModelAuthoring castModel,
            List<string> errors)
        {
            if (castModel == null) return;

            if (asset.AimKind == AimKind.Self && castModel.Kind == CastModelKind.HoldRelease)
                errors.Add(string.Format("AbilityAsset '{0}': Self-target abilities should not use HoldRelease.", asset.name));

            if (asset.AimKind == AimKind.Direction && castModel.Kind == CastModelKind.Channel)
                errors.Add(string.Format("AbilityAsset '{0}': Directional aim is unusual with Channel cast model.", asset.name));
        }

        private static void ValidateStages(AbilityAsset asset, List<string> errors)
        {
            var stages = asset.Stages;
            if (stages == null || stages.Length == 0)
            {
                errors.Add(string.Format(
                    "AbilityAsset '{0}': at least one explicit StageDef is required.",
                    asset.name));
                return;
            }

            var seenKeys = new HashSet<byte>();
            for (int i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                if (stage == null)
                {
                    errors.Add(string.Format("AbilityAsset '{0}': Stage at index {1} is null.", asset.name, i));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(stage.DebugName))
                    errors.Add(string.Format("AbilityAsset '{0}': Stage at index {1} has no debug name.", asset.name, i));

                if (!seenKeys.Add(stage.StageKey))
                    errors.Add(string.Format("AbilityAsset '{0}': Duplicate stage key {1}.", asset.name, stage.StageKey));

                ValidateStageDefAuthoring(asset, stage, i, errors);
            }

            if (stages.Length > 1 && seenKeys.Contains(0))
                errors.Add(string.Format("AbilityAsset '{0}': Stage key 0 is reserved; " +
                    "assign explicit non-zero keys for all stages.", asset.name));
        }

        private static void ValidateStageDefAuthoring(
            AbilityAsset asset,
            StageDefAuthoring stage,
            int index,
            List<string> errors)
        {
            switch (stage)
            {
                case AreaDamageStageDefAuthoring a:
                    if (a.Radius <= 0f)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "AreaDamage radius must be positive.", asset.name, index));
                    if (a.BaseDamage <= 0f)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "AreaDamage baseDamage must be positive.", asset.name, index));
                    break;

                case SpawnProjectileStageDefAuthoring p:
                    if (p.ProjectileDefId <= 0)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "SpawnProjectile projectileDefId must be positive.", asset.name, index));
                    if (p.SpawnOffsetDistance < 0f)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "SpawnProjectile spawnOffsetDistance must be non-negative.", asset.name, index));
                    break;

                case ApplyBuffStageDefAuthoring b:
                    if (b.BuffConfigId <= 0)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "ApplyBuff buffConfigId must be positive.", asset.name, index));
                    break;

                case DashStageDefAuthoring d:
                    if (d.SpeedPerTick <= 0f)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "Dash speedPerTick must be positive.", asset.name, index));
                    if (d.TotalDistance <= 0f)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "Dash totalDistance must be positive.", asset.name, index));
                    break;

                case ChargeStageDefAuthoring c:
                    if (c.MaxChargeTicks <= 0)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "Charge maxChargeTicks must be positive.", asset.name, index));
                    if (c.ChargeRatioBlackboardKeyId <= 0)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "Charge chargeRatioBlackboardKeyId must be positive.", asset.name, index));
                    break;

                case ChargeProjectileStageDefAuthoring q:
                    if (q.ProjectileDefId <= 0)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "ChargeProjectile projectileDefId must be positive.", asset.name, index));
                    if (q.MaxRange <= 0f)
                        errors.Add(string.Format("AbilityAsset '{0}' Stage[{1}]: " +
                            "ChargeProjectile maxRange must be positive.", asset.name, index));
                    ValidateLevelValues(
                        asset,
                        q.MinBaseDamageByLevel,
                        "MinBaseDamage",
                        errors);
                    ValidateLevelValues(
                        asset,
                        q.MaxBaseDamageByLevel,
                        "MaxBaseDamage",
                        errors);
                    break;
            }
        }

        private static void ValidatePassiveEffect(
            AbilityAsset asset,
            List<string> errors)
        {
            AbilityPassiveEffectAuthoring passive =
                asset.PassiveEffect;
            if (passive == null) return;
            try
            {
                ActiveAbilityPassiveEffectDef baked =
                    passive.Bake();
                baked.ValidateOrThrow();
            }
            catch (System.Exception exception)
            {
                errors.Add(string.Format(
                    "AbilityAsset '{0}': passive effect is invalid: {1}",
                    asset.name,
                    exception.Message));
            }
        }

        private static bool HasStage(AbilityAsset asset, byte stageKey)
        {
            var stages = asset.Stages;
            if (stages == null) return false;
            for (int i = 0; i < stages.Length; i++)
                if (stages[i] != null && stages[i].StageKey == stageKey)
                    return true;
            return false;
        }

        private static void ValidateLevelValues(
            AbilityAsset asset,
            float[] values,
            string label,
            List<string> errors)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) ||
                    float.IsInfinity(values[i]) ||
                    values[i] < 0f)
                {
                    errors.Add(string.Format(
                        "AbilityAsset '{0}': {1}[{2}] must be finite and nonnegative.",
                        asset.name,
                        label,
                        i));
                }
            }
        }
    }
}

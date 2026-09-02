using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class FormalAbilityStructureTargetTests
    {
        [Test]
        public void FormalAreaAbility_ExcludesStructures()
        {
            AbilityAsset asset =
                AssetDatabase.LoadAssetAtPath<AbilityAsset>(
                    "Assets/Config/Formal/Abilities/VarusE.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.Stages, Has.Length.GreaterThan(0));

            var stage = asset.Stages[0].Bake()
                as AreaDamageStageDef;
            Assert.That(stage, Is.Not.Null);
            Assert.That(
                stage.TargetFilter.TeamRule,
                Is.EqualTo(TeamQueryRule.EnemyOnly));
            Assert.That(
                stage.TargetFilter.UnitKindMask.Contains(
                    UnitKind.Structure),
                Is.False);
            Assert.That(
                stage.TargetFilter.LifeStateMask.Contains(
                    LifeState.Alive),
                Is.True);
            Assert.That(stage.TargetFilter.RequireTargetable, Is.True);
        }

        [TestCase(
            "Assets/Config/Formal/FullMatchProjectileRuntimeCatalog.asset",
            106)]
        [TestCase(
            "Assets/Config/Formal/FullMatchProjectileRuntimeCatalog.asset",
            107)]
        [TestCase(
            "Assets/Config/Formal/FullMatchProjectileRuntimeCatalog.asset",
            108)]
        [TestCase(
            "Assets/Config/Formal/MatchContent/VarusProjectileRuntimeCatalog.asset",
            106)]
        [TestCase(
            "Assets/Config/Formal/MatchContent/VarusProjectileRuntimeCatalog.asset",
            107)]
        [TestCase(
            "Assets/Config/Formal/MatchContent/VarusProjectileRuntimeCatalog.asset",
            108)]
        public void FormalAbilityProjectile_ExcludesStructures(
            string catalogPath,
            int projectileDefId)
        {
            ProjectileRuntimeCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<
                    ProjectileRuntimeCatalogAsset>(
                    catalogPath);
            Assert.That(catalog, Is.Not.Null);

            FieldInfo definitionsField =
                typeof(ProjectileRuntimeCatalogAsset).GetField(
                    "definitions",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(definitionsField, Is.Not.Null);
            var definitions =
                definitionsField.GetValue(catalog)
                    as List<ProjectileDefinitionAuthoring>;
            Assert.That(definitions, Is.Not.Null);

            ProjectileDefinitionAuthoring definition = null;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].DefId == projectileDefId)
                {
                    definition = definitions[i];
                    break;
                }
            }

            Assert.That(definition, Is.Not.Null);
            Assert.That(
                definition.TargetFilter.TeamRule,
                Is.EqualTo(ProjectileTeamRule.Enemy));
            Assert.That(
                definition.TargetFilter.UnitKindMask &
                    ProjectileUnitKindMask.Structure,
                Is.EqualTo(ProjectileUnitKindMask.None));
            Assert.That(
                definition.TargetFilter.AllowedLifeStates &
                    ProjectileLifeStateMask.Alive,
                Is.Not.EqualTo(ProjectileLifeStateMask.None));
            Assert.That(
                definition.TargetFilter.RequireTargetable,
                Is.True);
        }
    }
}

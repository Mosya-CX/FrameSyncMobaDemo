using NUnit.Framework;
using UnityEditor;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Guards the packaged-runtime path for CrowdControlDefinition assets
    /// (CC v6.2 2.3/2.6): the catalog must exist on disk with every
    /// definition's baked hidden fields serialized, and RegisterAll must
    /// accept the full catalog without duplicate or invalid definitions.
    /// This catches the failure where definitions were never re-baked or
    /// where baked structs could not be serialized into the asset.
    /// </summary>
    [TestFixture]
    public sealed class CrowdControlCatalogAssetTests
    {
        private const string CatalogPath =
            "Assets/Config/Formal/CrowdControl/CrowdControlCatalog.asset";

        [Test]
        public void CatalogDefinitions_AreBakedAndValid_OnDisk()
        {
            CrowdControlCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<CrowdControlCatalogAsset>(
                    CatalogPath);
            Assert.That(
                catalog,
                Is.Not.Null,
                "CrowdControl catalog asset must exist at the runtime path.");
            Assert.That(
                catalog.Definitions,
                Is.Not.Null.And.Not.Empty,
                "CrowdControl catalog must contain definitions.");

            for (int i = 0;
                 i < catalog.Definitions.Length;
                 i++)
            {
                CrowdControlDefinition definition =
                    catalog.Definitions[i];
                Assert.That(
                    definition,
                    Is.Not.Null,
                    $"definition {i} must not be null.");
                Assert.That(
                    definition.IsBaked,
                    Is.True,
                    $"definition '{definition.name}' must be baked " +
                    "(serialized hidden fields must survive reload).");
                Assert.That(
                    definition.IsValid,
                    Is.True,
                    $"definition '{definition.name}' must be valid " +
                    "after bake.");
            }
        }

        [Test]
        public void CatalogRegisterAll_Succeeds_WithAllDefinitionsResolvable()
        {
            CrowdControlCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<CrowdControlCatalogAsset>(
                    CatalogPath);
            Assert.That(
                catalog,
                Is.Not.Null,
                "CrowdControl catalog asset must exist at the runtime path.");

            var registry =
                new CrowdControlDefinitionRegistry();
            Assert.DoesNotThrow(
                () => catalog.RegisterAll(registry),
                "RegisterAll must accept every baked definition " +
                "(duplicate ids or invalid definitions throw).");

            for (int i = 0;
                 i < catalog.Definitions.Length;
                 i++)
            {
                CrowdControlDefinition definition =
                    catalog.Definitions[i];
                if (definition == null)
                {
                    continue;
                }
                Assert.That(
                    registry.TryGet(
                        definition.ControlId,
                        out _),
                    Is.True,
                    $"definition '{definition.name}' " +
                    $"(id {definition.ControlId.Value}) must be registered.");
            }
        }
    }
}

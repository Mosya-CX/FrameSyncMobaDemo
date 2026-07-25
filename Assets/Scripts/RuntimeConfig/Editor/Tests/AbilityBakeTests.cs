using FrameSyncMoba.RuntimeConfig.Editor;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor.Tests
{
    [TestFixture]
    public class AbilityAssetBakeTests
    {
        [Test]
        public void Bake_CommitModel_ProducesValidAbilityDef()
        {
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            var def = asset.Bake();

            Assert.That(def, Is.Not.Null);
            Assert.That(def.AbilityId, Is.GreaterThan(0));
            Assert.That(def.CastModel, Is.Not.Null);
            Assert.That(def.CastModel.Kind, Is.EqualTo(CastModelKind.Commit));
            Assert.That(def.IsValid, Is.True);
        }

        [Test]
        public void Bake_HoldReleaseModel_ProducesCorrectKind()
        {
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            var holdRelease = new HoldReleaseCastModelAuthoring();

            var field = typeof(AbilityAsset).GetField("castModel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(asset, holdRelease);

            var def = asset.Bake();
            Assert.That(def.CastModel.Kind, Is.EqualTo(CastModelKind.HoldRelease));
        }

        [Test]
        public void Bake_WithStages_AssignsStageDefs()
        {
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            var commitModel = new CommitCastModelAuthoring();
            var field = typeof(AbilityAsset).GetField("castModel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(asset, commitModel);

            typeof(CommitCastModelAuthoring).GetField("castStageKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(commitModel, (byte)1);

            var stagesField = typeof(AbilityAsset).GetField("stageDefs",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stageAuthoring = new StageDefAuthoring();
            typeof(StageDefAuthoring).GetField("stageKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(stageAuthoring, (byte)1);
            typeof(StageDefAuthoring).GetField("debugName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(stageAuthoring, "Cast Stage");
            stagesField.SetValue(asset, new StageDefAuthoring[] { stageAuthoring });

            var def = asset.Bake();
            Assert.That(def.CastModel, Is.Not.Null);
            Assert.That(def.IsValid, Is.True);
        }

        [Test]
        public void Bake_NegativeAbilityId_Throws()
        {
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            var field = typeof(AbilityAsset).GetField("abilityId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(asset, -1);

            Assert.That(() => asset.Bake(), Throws.InvalidOperationException);
        }
    }

    [TestFixture]
    public class AbilityAssetValidationTests
    {
        [Test]
        public void Validate_ValidAsset_ReturnsSuccess()
        {
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            var result = AbilityAssetBakeValidator.Validate(asset);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors.Length, Is.EqualTo(0));
        }

        [Test]
        public void Validate_EmptyName_ReturnsError()
        {
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            var nameField = typeof(AbilityAsset).GetField("abilityName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            nameField.SetValue(asset, "");

            var result = AbilityAssetBakeValidator.Validate(asset);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Length, Is.GreaterThan(0));
        }

        [Test]
        public void Validate_HoldReleaseDuplicateKeys_ReturnsError()
        {
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            var holdRelease = new HoldReleaseCastModelAuthoring();

            typeof(HoldReleaseCastModelAuthoring).GetField("holdStageKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(holdRelease, (byte)5);
            typeof(HoldReleaseCastModelAuthoring).GetField("releaseStageKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(holdRelease, (byte)5);

            var field = typeof(AbilityAsset).GetField("castModel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(asset, holdRelease);

            var result = AbilityAssetBakeValidator.Validate(asset);
            Assert.That(result.IsValid, Is.False);
        }
    }

    [TestFixture]
    public class AbilityRegistryPopulationTests
    {
        [Test]
        public void RegisterFromAsset_ValidAsset_Succeeds()
        {
            var registry = new AbilityDefinitionRegistry();
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            var idField = typeof(AbilityAsset).GetField("abilityId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            idField.SetValue(asset, 1001);

            bool registered = registry.TryRegisterFromAsset(asset);
            Assert.That(registered, Is.True);
            Assert.That(registry.TryGet(1001, out var def), Is.True);
            Assert.That(def.AbilityId, Is.EqualTo(1001));
        }

        [Test]
        public void RegisterFromAsset_NullAsset_ReturnsFalse()
        {
            var registry = new AbilityDefinitionRegistry();
            Assert.That(registry.TryRegisterFromAsset(null), Is.False);
        }

        [Test]
        public void Register_DuplicateId_Throws()
        {
            var registry = new AbilityDefinitionRegistry();
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            var idField = typeof(AbilityAsset).GetField("abilityId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            idField.SetValue(asset, 2001);

            Assert.That(registry.TryRegisterFromAsset(asset), Is.True);
            Assert.That(registry.TryRegisterFromAsset(asset), Is.False);
        }
    }
}

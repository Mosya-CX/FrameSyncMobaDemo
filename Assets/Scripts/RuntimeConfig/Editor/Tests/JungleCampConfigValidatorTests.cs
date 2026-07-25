using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace FrameSyncMoba.RuntimeConfig.Editor.Tests
{
    /// <summary>
    /// Tests for JungleCampConfigValidator and bake pipeline (ExecPlan 0088).
    /// </summary>
    public class JungleCampConfigValidatorTests
    {
        [Test]
        public void Validate_NullConfig_ReturnsErrors()
        {
            var errors = JungleCampConfigValidator.Validate(null);
            Assert.That(errors.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Validate_EmptyCamps_ReturnsErrors()
        {
            var config = ScriptableObject.CreateInstance<JungleCampConfig>();
            var errors = JungleCampConfigValidator.Validate(config);
            Assert.That(errors.Count, Is.GreaterThan(0));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Validate_ValidConfig_Passes()
        {
            var config = ScriptableObject.CreateInstance<JungleCampConfig>();
            config.SetEntries(new List<JungleCampEntry>
            {
                new JungleCampEntry
                {
                    CampId = 1,
                    MonsterPrototypeIds = new[] { 1001, 1002 },
                    RespawnDelaySeconds = 30f,
                    GoldRewards = new[] { 10, 20 },
                    XpRewards = new[] { 50, 60 },
                },
                new JungleCampEntry
                {
                    CampId = 2,
                    MonsterPrototypeIds = new[] { 2001 },
                    RespawnDelaySeconds = 60f,
                    GoldRewards = new[] { 50 },
                    XpRewards = new[] { 100 },
                },
            });

            var errors = JungleCampConfigValidator.Validate(config);
            Assert.That(errors.Count, Is.EqualTo(0));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void Validate_DuplicateCampId_Fails()
        {
            var config = ScriptableObject.CreateInstance<JungleCampConfig>();
            config.SetEntries(new List<JungleCampEntry>
            {
                new JungleCampEntry { CampId = 1, MonsterPrototypeIds = new[] { 1001 }, RespawnDelaySeconds = 30f },
                new JungleCampEntry { CampId = 1, MonsterPrototypeIds = new[] { 1002 }, RespawnDelaySeconds = 30f },
            });

            var errors = JungleCampConfigValidator.Validate(config);
            Assert.That(errors.Count, Is.GreaterThan(0));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void Validate_NegativeRespawnDelay_Fails()
        {
            var config = ScriptableObject.CreateInstance<JungleCampConfig>();
            config.SetEntries(new List<JungleCampEntry>
            {
                new JungleCampEntry { CampId = 1, MonsterPrototypeIds = new[] { 1001 }, RespawnDelaySeconds = -1f },
            });

            var errors = JungleCampConfigValidator.Validate(config);
            Assert.That(errors.Count, Is.GreaterThan(0));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void Validate_MismatchedRewardLengths_Fails()
        {
            var config = ScriptableObject.CreateInstance<JungleCampConfig>();
            config.SetEntries(new List<JungleCampEntry>
            {
                new JungleCampEntry
                {
                    CampId = 1,
                    MonsterPrototypeIds = new[] { 1001, 1002 },
                    RespawnDelaySeconds = 30f,
                    GoldRewards = new[] { 10 }, // Only 1 reward for 2 monsters
                    XpRewards = new[] { 50, 60 },
                },
            });

            var errors = JungleCampConfigValidator.Validate(config);
            Assert.That(errors.Count, Is.GreaterThan(0));

            Object.DestroyImmediate(config);
        }
    }
}

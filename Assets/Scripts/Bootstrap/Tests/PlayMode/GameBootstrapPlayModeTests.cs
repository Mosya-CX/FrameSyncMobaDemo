using System.Collections;
using System.Reflection;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class GameBootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator ClientComposition_InitializesFromProjectAssets()
        {
            var root = new GameObject("TestClientBootstrap");
            root.SetActive(false);
            try
            {
                var camera = root.AddComponent<Camera>();
                var input = root.AddComponent<PlayerInputController>();
                var bootstrap = root.AddComponent<GameBootstrap>();
                SetReference(
                    bootstrap,
                    "globalGameplayData",
                    LoadAsset<GlobalGameplayData>(
                        "8b0cdcd39dbb2964baebdd8475f1e60e"));
                SetReference(
                    bootstrap,
                    "unitRuntimeCatalog",
                    LoadAsset<UnitRuntimeCatalogAsset>(
                        "cf6a213803fa81b4cb7ac2699f40045b"));
                SetReference(
                    bootstrap,
                    "abilityRuntimeCatalog",
                    LoadAsset<AbilityRuntimeCatalogAsset>(
                        "e09025f013ae7a8449335c6356fee5fb"));
                SetReference(
                    bootstrap,
                    "projectileRuntimeCatalog",
                    LoadAsset<ProjectileRuntimeCatalogAsset>(
                        "e548718fd0a6b7d4b87db7539574720f"));
                SetReference(bootstrap, "playerInputController", input);
                SetReference(bootstrap, "gameplayCamera", camera);
                root.SetActive(true);
                yield return null;

                Assert.IsTrue(bootstrap.IsInitialized);
                Assert.NotNull(bootstrap.Runtime);
                Assert.NotNull(bootstrap.UnitWorld);
                Assert.NotNull(bootstrap.PhysicsWorld);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        private static T LoadAsset<T>(
            string guid)
            where T : Object
        {
            string path =
                UnityEditor.AssetDatabase.GUIDToAssetPath(
                    guid);
            T asset =
                UnityEditor.AssetDatabase
                    .LoadAssetAtPath<T>(path);
            Assert.NotNull(
                asset,
                $"Project asset {guid} must exist.");
            return asset;
        }

        private static void SetReference(
            Object target,
            string field,
            Object value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo, $"Missing serialized field {field}.");
            fieldInfo.SetValue(target, value);
        }
    }
}

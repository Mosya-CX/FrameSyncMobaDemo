using System.Collections;
using System.Reflection;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
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
            GlobalPrefabTable prefabTable =
                ScriptableObject.CreateInstance<GlobalPrefabTable>();
            GlobalGameplayData config =
                ScriptableObject.CreateInstance<GlobalGameplayData>();
            SetReference(config, "globalPrefabTable", prefabTable);

            var root = new GameObject("TestClientBootstrap");
            root.SetActive(false);
            try
            {
                var camera = root.AddComponent<Camera>();
                var input = root.AddComponent<PlayerInputController>();
                var bootstrap = root.AddComponent<GameBootstrap>();
                SetReference(bootstrap, "globalGameplayData", config);
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
                Object.Destroy(config);
                Object.Destroy(prefabTable);
            }
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

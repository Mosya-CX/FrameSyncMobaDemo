using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class LocalNgoSceneConfigurationTests
    {
        [TestCase("Assets/Scenes/ClientBootstrap.unity")]
        [TestCase("Assets/Scenes/ServerBootstrap.unity")]
        public void EndpointScene_UsesApplicationOwnedSceneAndPlayerLifecycle(
            string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Additive);
            try
            {
                Component manager = FindNetworkManager(scene);
                Assert.That(manager, Is.Not.Null);

                var serialized = new SerializedObject(manager);
                Assert.That(
                    serialized.FindProperty(
                            "NetworkConfig.EnableSceneManagement")
                        .boolValue,
                    Is.False);
                Assert.That(
                    serialized.FindProperty(
                            "NetworkConfig.PlayerPrefab")
                        .objectReferenceValue,
                    Is.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Component FindNetworkManager(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Component component in
                     root.GetComponentsInChildren<Component>(true))
                if (component != null &&
                    component.GetType().FullName ==
                    "Unity.Netcode.NetworkManager")
                    return component;
            return null;
        }
    }
}

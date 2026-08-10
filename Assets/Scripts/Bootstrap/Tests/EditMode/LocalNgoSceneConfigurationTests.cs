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

        /// <summary>
        /// Guards the local C/S start contract: every lobby server slot must
        /// resolve to a PlayerControlled initial spawn in GameScene by
        /// SpawnPointId + UnitPrototypeId + TeamId. A mismatch surfaces at
        /// match start as "PlayerSlot X has no matching initial spawn".
        /// </summary>
        [Test]
        public void LocalServerSlots_MatchGameSceneInitialSpawns()
        {
            Scene lobby = EditorSceneManager.OpenScene(
                "Assets/Scenes/Lobby.unity",
                OpenSceneMode.Additive);
            Scene game = EditorSceneManager.OpenScene(
                "Assets/Scenes/GameScene.unity",
                OpenSceneMode.Additive);
            try
            {
                LocalNgoEndpointDriver driver =
                    FindComponent<LocalNgoEndpointDriver>(lobby);
                Assert.That(driver, Is.Not.Null);
                GameBootstrap bootstrap =
                    FindComponent<GameBootstrap>(game);
                Assert.That(bootstrap, Is.Not.Null);

                var driverSerialized =
                    new SerializedObject(driver);
                SerializedProperty slots =
                    driverSerialized.FindProperty("serverSlots");
                var bootstrapSerialized =
                    new SerializedObject(bootstrap);
                SerializedProperty spawns =
                    bootstrapSerialized.FindProperty(
                        "initialUnitSpawns");
                Assert.That(slots, Is.Not.Null);
                Assert.That(spawns, Is.Not.Null);
                Assert.That(
                    slots.arraySize,
                    Is.GreaterThan(0));

                for (int slotIndex = 0;
                     slotIndex < slots.arraySize;
                     slotIndex++)
                {
                    SerializedProperty slot =
                        slots.GetArrayElementAtIndex(slotIndex);
                    int playerSlot =
                        slot.FindPropertyRelative("PlayerSlot")
                            .intValue;
                    int teamId =
                        slot.FindPropertyRelative("TeamId")
                            .intValue;
                    int spawnPointId =
                        slot.FindPropertyRelative("SpawnPointId")
                            .intValue;
                    int heroConfigId =
                        slot.FindPropertyRelative("HeroConfigId")
                            .intValue;

                    bool match = false;
                    for (int i = 0;
                         i < spawns.arraySize;
                         i++)
                    {
                        SerializedProperty spawn =
                            spawns.GetArrayElementAtIndex(i);
                        if (!spawn
                                .FindPropertyRelative(
                                    "PlayerControlled")
                                .boolValue)
                        {
                            continue;
                        }
                        bool useMap =
                            spawn
                                .FindPropertyRelative(
                                    "UseMapSpawnPoint")
                                .boolValue;
                        int resolved =
                            useMap
                                ? spawn
                                    .FindPropertyRelative(
                                        "SpawnPointId")
                                    .intValue
                                : spawn
                                    .FindPropertyRelative(
                                        "StableSpawnOrder")
                                    .intValue;
                        if (resolved == spawnPointId &&
                            spawn
                                .FindPropertyRelative(
                                    "UnitPrototypeId")
                                .intValue ==
                            heroConfigId &&
                            spawn
                                .FindPropertyRelative("TeamId")
                                .intValue ==
                            teamId)
                        {
                            match = true;
                            break;
                        }
                    }

                    Assert.That(
                        match,
                        Is.True,
                        $"PlayerSlot {playerSlot} " +
                        $"(team {teamId}, spawnPoint {spawnPointId}, " +
                        $"hero {heroConfigId}) has no matching initial " +
                        "spawn in GameScene.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(game, true);
                EditorSceneManager.CloseScene(lobby, true);
            }
        }

        private static T FindComponent<T>(Scene scene)
            where T : Component
        {
            foreach (GameObject root in
                     scene.GetRootGameObjects())
            {
                T component =
                    root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }
            return null;
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

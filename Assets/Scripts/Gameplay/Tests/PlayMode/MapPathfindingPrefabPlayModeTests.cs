using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Unit.PlayModeTests
{
    public sealed class
        MapPathfindingPrefabPlayModeTests
    {
        [UnityTest]
        public IEnumerator
            MapPrefab_InstantiatesWithBoundReadOnlyPathfindingView()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Config/Formal/Prefabs/Logic/Map/Map.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance =
                Object.Instantiate(prefab);
            yield return null;

            FlowFieldSceneAuthoring source =
                instance.GetComponent<
                    FlowFieldSceneAuthoring>();
            FlowFieldVisualizer visualizer =
                instance.GetComponent<
                    FlowFieldVisualizer>();
            Assert.That(source, Is.Not.Null);
            Assert.That(visualizer, Is.Not.Null);
            Assert.That(
                visualizer.Source,
                Is.SameAs(source));
            Assert.That(
                source.Lanes.Length,
                Is.EqualTo(3));
            for (int i = 0;
                 i < source.Lanes.Length;
                 i++)
            {
                Assert.That(
                    source.Lanes[i]
                        .transform.IsChildOf(
                            instance.transform),
                    Is.True);
            }
            Assert.That(
                source.TryGetField(
                    1,
                    RadiusClass.Small,
                    out FlowFieldBakeAsset field),
                Is.True);
            Assert.That(field.IsValid, Is.True);

            Object.Destroy(instance);
            yield return null;
        }
    }
}

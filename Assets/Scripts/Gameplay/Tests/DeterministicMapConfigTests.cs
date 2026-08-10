using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class DeterministicMapConfigTests
    {
        [Test]
        public void FullMatchMap_BakesStableWalkabilityAndSpawnPoints()
        {
            DeterministicMapConfig config =
                AssetDatabase.LoadAssetAtPath<
                    DeterministicMapConfig>(
                    "Assets/Config/Formal/FullMatchDeterministicMapConfig.asset");
            Assert.That(config, Is.Not.Null);

            BakedDeterministicMapData first =
                config.BakeOrThrow();
            BakedDeterministicMapData second =
                config.BakeOrThrow();
            PathGridMap2D firstGrid =
                first.CreatePathGrid();
            PathGridMap2D secondGrid =
                second.CreatePathGrid();

            Assert.That(first.MapConfigId, Is.EqualTo(1));
            Assert.That(first.MapDataVersion, Is.EqualTo(1u));
            Assert.That(
                first.SpawnPoints.Count,
                Is.EqualTo(18));
            Assert.That(
                first.GetRequiredSpawnPoint(0).TeamId,
                Is.EqualTo(new TeamId(1)));
            Assert.That(
                first.GetRequiredSpawnPoint(11).TeamId,
                Is.EqualTo(new TeamId(2)));
            // FullMatch map center is open terrain; obstacles sit near the
            // lanes and edges.
            Assert.That(
                firstGrid.IsCircleWalkable(
                    fp2.zero,
                    (fp)0.5m),
                Is.True);
            Assert.That(
                firstGrid.IsCircleWalkable(
                    first.GetRequiredSpawnPoint(0)
                        .Position,
                    (fp)0.5m),
                Is.True);
            Assert.That(
                secondGrid.IsCircleWalkable(
                    second.GetRequiredSpawnPoint(0)
                        .Position,
                    (fp)0.5m),
                Is.True);
        }
    }
}

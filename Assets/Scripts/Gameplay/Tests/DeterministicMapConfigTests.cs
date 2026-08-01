using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class DeterministicMapConfigTests
    {
        [Test]
        public void NeutralMap_BakesStableWalkabilityAndSpawnPoints()
        {
            DeterministicMapConfig config =
                AssetDatabase.LoadAssetAtPath<
                    DeterministicMapConfig>(
                    "Assets/Fixtures/Framework/Config/NeutralDeterministicMapConfig.asset");
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
                Is.EqualTo(4));
            Assert.That(
                first.GetRequiredSpawnPoint(0).TeamId,
                Is.EqualTo(new TeamId(1)));
            Assert.That(
                first.GetRequiredSpawnPoint(11).TeamId,
                Is.EqualTo(new TeamId(2)));
            Assert.That(
                firstGrid.IsCircleWalkable(
                    fp2.zero,
                    (fp)0.5m),
                Is.False);
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

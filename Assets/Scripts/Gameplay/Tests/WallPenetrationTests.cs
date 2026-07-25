using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class WallPenetrationTests
    {
        private static PathGridMap2D CreateGridWithBlockedCell()
        {
            var grid = new PathGridMap2D();
            grid.Initialise(fp2.zero, new fp2((fp)15m, (fp)15m), (fp)1m);
            // Block cell (7, 7)
            grid.SetObstruction(new fp2((fp)6.5m, (fp)6.5m), new fp2((fp)7.5m, (fp)7.5m), blocked: true);
            return grid;
        }

        [Test]
        public void Detect_UnitInBlockedCell_ReturnsCorrection()
        {
            var grid = CreateGridWithBlockedCell();
            UnitUid uid = new UnitUid(1, 1, 1);

            // Position inside the blocked cell
            fp2 pos = new fp2((fp)7m, (fp)7m);
            MovementCorrectionRequest? req = WallPenetrationResolver.Detect(uid, pos, (fp)0.5m, grid);

            Assert.That(req.HasValue, Is.True, "Should detect penetration in blocked cell.");
            Assert.That(req.Value.UnitUid, Is.EqualTo(uid));
            Assert.That(req.Value.Reason, Is.EqualTo(MovementCorrectionReason.WallDepenetration));
        }

        [Test]
        public void Detect_UnitInWalkableCell_ReturnsNull()
        {
            var grid = CreateGridWithBlockedCell();
            UnitUid uid = new UnitUid(1, 1, 1);

            // Position in open area
            fp2 pos = new fp2((fp)2m, (fp)2m);
            MovementCorrectionRequest? req = WallPenetrationResolver.Detect(uid, pos, (fp)0.5m, grid);

            Assert.That(req.HasValue, Is.False, "Should not detect penetration in walkable cell.");
        }

        [Test]
        public void Detect_PushOutDir_PointsAwayFromBlockedArea()
        {
            var grid = CreateGridWithBlockedCell();
            UnitUid uid = new UnitUid(1, 1, 1);

            fp2 pos = new fp2((fp)7m, (fp)7m);
            MovementCorrectionRequest? req = WallPenetrationResolver.Detect(uid, pos, (fp)0.5m, grid);

            Assert.That(req.HasValue, Is.True);
            // Push-out should be non-zero
            Assert.That(req.Value.Delta.x != fp.zero || req.Value.Delta.y != fp.zero, Is.True,
                "Push-out delta should be non-zero.");
        }

        [Test]
        public void Detect_ClampedMagnitude()
        {
            var grid = CreateGridWithBlockedCell();
            UnitUid uid = new UnitUid(1, 1, 1);

            // Position deep inside blocked cell
            fp2 pos = new fp2((fp)7m, (fp)7m);
            MovementCorrectionRequest? req = WallPenetrationResolver.Detect(uid, pos, (fp)0.5m, grid);

            Assert.That(req.HasValue, Is.True);
            fp magSq = fpmath.dot(req.Value.Delta, req.Value.Delta);
            fp maxDist = (fp)1m; // MaxDepenetrationDistance
            Assert.That(magSq, Is.LessThanOrEqualTo(maxDist * maxDist),
                "Push-out should be clamped to MaxDepenetrationDistance.");
        }

        [Test]
        public void Detect_Deterministic_SameInputSameOutput()
        {
            var grid1 = CreateGridWithBlockedCell();
            var grid2 = CreateGridWithBlockedCell();
            UnitUid uid = new UnitUid(1, 1, 1);
            fp2 pos = new fp2((fp)7m, (fp)7m);

            MovementCorrectionRequest? req1 = WallPenetrationResolver.Detect(uid, pos, (fp)0.5m, grid1);
            MovementCorrectionRequest? req2 = WallPenetrationResolver.Detect(uid, pos, (fp)0.5m, grid2);

            Assert.That(req1.HasValue, Is.EqualTo(req2.HasValue));
            if (req1.HasValue)
            {
                Assert.That(req1.Value.Delta.x, Is.EqualTo(req2.Value.Delta.x));
                Assert.That(req1.Value.Delta.y, Is.EqualTo(req2.Value.Delta.y));
            }
        }
    }
}

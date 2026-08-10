using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync.Tests
{
    /// <summary>
    /// Verifies that the deterministic states added for charged abilities and
    /// blight (per-instance projectile on-hit override, pending lifetime
    /// override, and passive ability level) participate in
    /// SharedGameplayChecksum, so a mismatch between endpoints is detected.
    /// </summary>
    [TestFixture]
    public sealed class ChecksumNewStateCoverageTests
    {
        [Test]
        public void Checksum_ChangesWhenProjectileOnHitOverrideDiffers()
        {
            GameplaySnapshot withAmount =
                CreateMinimalSnapshot();
            GameplaySnapshot otherAmount =
                CreateMinimalSnapshot();

            withAmount.ProjectileState =
                CreateProjectileState((fp)100);
            otherAmount.ProjectileState =
                CreateProjectileState((fp)120);

            Assert.AreNotEqual(
                Compute(withAmount),
                Compute(otherAmount),
                "OnHitDamageOverride must participate in the checksum.");
        }

        [Test]
        public void Checksum_ChangesWhenPendingLifetimeOverrideDiffers()
        {
            GameplaySnapshot shortLifetime =
                CreateMinimalSnapshot();
            GameplaySnapshot longLifetime =
                CreateMinimalSnapshot();

            shortLifetime.ProjectileState =
                CreatePendingState(10);
            longLifetime.ProjectileState =
                CreatePendingState(20);

            Assert.AreNotEqual(
                Compute(shortLifetime),
                Compute(longLifetime),
                "Pending MaxLifetimeTicksOverride must participate in the checksum.");
        }

        [Test]
        public void Checksum_ChangesWhenPassiveAbilityLevelDiffers()
        {
            GameplaySnapshot levelOne =
                CreateMinimalSnapshot();
            GameplaySnapshot levelTwo =
                CreateMinimalSnapshot();

            levelOne.UnitWorldState =
                CreateUnitWithPassiveLevel(1);
            levelTwo.UnitWorldState =
                CreateUnitWithPassiveLevel(2);

            Assert.AreNotEqual(
                Compute(levelOne),
                Compute(levelTwo),
                "AbilityPassiveRuntimeState.AbilityLevel must participate in the checksum.");
        }

        [Test]
        public void
            Checksum_ChangesWhenMinionThreatRefreshTickDiffers()
        {
            GameplaySnapshot refreshed =
                CreateMinimalSnapshot();
            GameplaySnapshot reset =
                CreateMinimalSnapshot();

            refreshed.UnitWorldState =
                CreateMinionAIState(11, 800);
            reset.UnitWorldState =
                CreateMinionAIState(-1, 800);

            Assert.AreNotEqual(
                Compute(refreshed),
                Compute(reset),
                "MinionLastThreatRefreshLogicTick must participate in the checksum.");
        }

        [Test]
        public void
            Checksum_ChangesWhenMinionThreatTableDiffers()
        {
            GameplaySnapshot low =
                CreateMinimalSnapshot();
            GameplaySnapshot high =
                CreateMinimalSnapshot();

            low.UnitWorldState =
                CreateMinionAIState(11, 800);
            high.UnitWorldState =
                CreateMinionAIState(11, 810);

            Assert.AreNotEqual(
                Compute(low),
                Compute(high),
                "MinionThreatTable must participate in the checksum.");
        }

        private static GameplaySnapshot CreateMinimalSnapshot()
        {
            return new GameplaySnapshot
            {
                SchemaVersion =
                    GameplaySnapshot.CurrentSchemaVersion,
                RandomState = default,
                UnitWorldState =
                    UnitWorldSnapshot.CreateEmpty(),
                ProjectileState =
                    new ProjectileWorldSnapshot(),
            };
        }

        private static UnitWorldSnapshot
            CreateMinionAIState(
                int lastRefreshTick,
                int threat)
        {
            return new UnitWorldSnapshot
            {
                AIControllerStates = new[]
                {
                    new UnitAIControllerSnapshot
                    {
                        ControllerKind =
                            UnitAIControllerKind.Minion,
                        OwnerUnitUid =
                            new UnitUid(20, 1202, 0),
                        MinionState =
                            MinionAIState.EngageTarget,
                        LaneId = 1,
                        MinionLastThreatRefreshLogicTick =
                            lastRefreshTick,
                        MinionNextDecisionLogicTick = 25,
                        MinionTargetLockUntilLogicTick = 30,
                        MinionThreatTable = new[]
                        {
                            new MinionThreatSnapshotEntry
                            {
                                Uid =
                                    new UnitUid(21, 1201, 0),
                                Threat = threat,
                            },
                        },
                    },
                },
            };
        }

        private static ProjectileWorldSnapshot
            CreateProjectileState(fp amount)
        {
            return new ProjectileWorldSnapshot
            {
                ActiveProjectiles = new[]
                {
                    new ProjectileRuntimeSnapshot
                    {
                        Uid = new ProjectileUid(
                            20,
                            2001,
                            0),
                        DefId = 2001,
                        OwnerUnitUid =
                            new UnitUid(20, 1001, 0),
                        TeamSnapshot = new TeamId(1),
                        Source = CreateSource(),
                        Position = new fp2(
                            fp.one,
                            fp.zero),
                        RemainingLifetimeTicks = 10,
                        OnHitDamageOverride = new[]
                        {
                            new ProjectileOnHitDamage
                            {
                                Amount = amount,
                                DamageType =
                                    DamageType.Physical,
                                RecipeId = 1,
                            },
                        },
                    },
                },
            };
        }

        private static ProjectileWorldSnapshot
            CreatePendingState(int lifetime)
        {
            return new ProjectileWorldSnapshot
            {
                PendingSpawns = new[]
                {
                    new PendingSpawnRecordSnapshot
                    {
                        Uid = new ProjectileUid(
                            20,
                            2001,
                            0),
                        DefId = 2001,
                        OwnerUnitUid =
                            new UnitUid(20, 1001, 0),
                        TeamSnapshot = new TeamId(1),
                        Source = CreateSource(),
                        StartPosition = new fp2(
                            fp.one,
                            fp.zero),
                        Direction = new fp2(
                            fp.one,
                            fp.zero),
                        MaxLifetimeTicksOverride = lifetime,
                    },
                },
            };
        }

        private static UnitWorldSnapshot
            CreateUnitWithPassiveLevel(int level)
        {
            return new UnitWorldSnapshot
            {
                Units = new[]
                {
                    new UnitSnapshot
                    {
                        UnitUid =
                            new UnitUid(20, 1001, 0),
                        UnitKind = UnitKind.Hero,
                        TeamId = new TeamId(1),
                        UnitPrototypeId = 1001,
                        AbilityState =
                            new AbilityHandlerSnapshot
                            {
                                HasFixedPassive = true,
                                FixedPassiveAbilityId = 9001,
                                FixedPassiveRuntimeState =
                                    new AbilityPassiveRuntimeState
                                    {
                                        AbilityLevel = level,
                                    },
                            },
                    },
                },
            };
        }

        private static SourceDescriptor CreateSource()
        {
            return new SourceDescriptor
            {
                SourceType =
                    CombatSourceType.Ability,
                SourceId = 10011,
                OwnerUnitUid =
                    new UnitUid(20, 1001, 0),
                EmitterUnitUid =
                    new UnitUid(20, 1001, 0),
            };
        }

        private static uint Compute(
            GameplaySnapshot snapshot)
        {
            return SharedGameplayChecksum.Compute(
                snapshot,
                default,
                new CanonicalByteWriter(
                    new byte[65536]));
        }
    }
}

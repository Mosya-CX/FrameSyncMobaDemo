using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class AuthorityReplicationTests
    {
        [Test]
        public void CommandBundle_ProducesStablePerTickReplacementRelays()
        {
            UnitUid unitUid = new UnitUid(0, 10, 1);
            GameplayCommand first = GameplayCommand.CreateMove(
                Header(unitUid, 3, 1),
                new fp2(fp.one, fp.zero));
            GameplayCommand replacement = GameplayCommand.CreateMove(
                Header(unitUid, 3, 2),
                new fp2((fp)2, fp.zero));
            var buffer = new CommandRelayBuffer();

            AcceptedCommandRelay[] relays = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { replacement, first }),
                0,
                12,
                command => command.ControlledUnitUid == unitUid);

            Assert.AreEqual(1, relays.Length);
            Assert.AreEqual(3, relays[0].TargetTick);
            Assert.AreEqual(1u, relays[0].RelayRevision);
            GameplayCommand[] canonical = relays[0].DecodeCommands();
            Assert.AreEqual(1, canonical.Length);
            Assert.AreEqual(2u, canonical[0].CommandSeq);
            Assert.AreEqual(new fp2((fp)2, fp.zero),
                canonical[0].MoveTargetPoint);

            AcceptedCommandRelay[] duplicate = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { replacement, first }),
                0,
                12,
                null);
            Assert.AreEqual(0, duplicate.Length);
        }

        [Test]
        public void LateCommand_IsRetargetedToCurrentServerTick_NotRejected()
        {
            UnitUid unitUid = new UnitUid(0, 10, 1);
            GameplayCommand late = GameplayCommand.CreateMove(
                Header(unitUid, 3, 1),
                new fp2(fp.one, fp.zero));
            var buffer = new CommandRelayBuffer();

            AcceptedCommandRelay[] relays = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { late }),
                5,
                12,
                null);

            Assert.AreEqual(1, relays.Length);
            Assert.AreEqual(5, relays[0].TargetTick);
            GameplayCommand[] canonical =
                relays[0].DecodeCommands();
            Assert.AreEqual(1, canonical.Length);
            Assert.AreEqual(5, canonical[0].TargetTick);
            Assert.AreEqual(1u, canonical[0].CommandSeq);
            Assert.AreEqual(
                new fp2(fp.one, fp.zero),
                canonical[0].MoveTargetPoint);
        }

        [Test]
        public void AcceptedCommand_AfterTickFreeze_LateDuplicateIsIgnored()
        {
            UnitUid unitUid = new UnitUid(0, 10, 1);
            GameplayCommand toggle =
                GameplayCommand.CreateCastAbility(
                    Header(unitUid, 3, 1),
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None);
            var buffer = new CommandRelayBuffer();

            AcceptedCommandRelay[] first = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    2,
                    new[] { toggle }),
                3,
                12,
                null);
            Assert.AreEqual(1, first.Length);
            Assert.AreEqual(3, first[0].TargetTick);
            buffer.FreezeTick(3);

            AcceptedCommandRelay[] repeated = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    2,
                    3,
                    new[] { toggle }),
                4,
                12,
                null);

            Assert.AreEqual(0, repeated.Length);
            Assert.AreEqual(
                0,
                buffer.GetCurrentRelay(4).DecodeCommands().Length,
                "An accepted input identity must not be retargeted into a " +
                "second authoritative Tick after its original Tick freezes.");
        }

        [Test]
        public void AcceptedCommand_AfterOwnerInvalidation_DuplicateSkipsAuthorization()
        {
            UnitUid unitUid = new UnitUid(0, 10, 1);
            GameplayCommand toggle =
                GameplayCommand.CreateCastAbility(
                    Header(unitUid, 3, 1),
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None);
            var buffer = new CommandRelayBuffer();

            AcceptedCommandRelay[] first = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    2,
                    new[] { toggle }),
                3,
                12,
                _ => true);
            Assert.AreEqual(1, first.Length);
            buffer.FreezeTick(3);

            int authorizationCalls = 0;
            Assert.DoesNotThrow(() =>
            {
                AcceptedCommandRelay[] repeated = buffer.AcceptBundle(
                    GameplayCommandBundle.Create(
                        7,
                        2,
                        3,
                        new[] { toggle }),
                    4,
                    12,
                    _ =>
                    {
                        authorizationCalls++;
                        return false;
                    });
                Assert.AreEqual(0, repeated.Length);
            });
            Assert.AreEqual(
                0,
                authorizationCalls,
                "An already accepted identity is idempotent even if its " +
                "former controlled unit can no longer be authorized.");
        }

        [Test]
        public void DistinctCommandSequences_OnAdjacentTicks_AreBothAccepted()
        {
            UnitUid unitUid = new UnitUid(0, 10, 1);
            GameplayCommand firstToggle =
                GameplayCommand.CreateCastAbility(
                    Header(unitUid, 3, 1),
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None);
            GameplayCommand secondToggle =
                GameplayCommand.CreateCastAbility(
                    Header(unitUid, 4, 2),
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None);
            var buffer = new CommandRelayBuffer();

            AcceptedCommandRelay[] first = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    1,
                    2,
                    new[] { firstToggle }),
                3,
                12,
                null);
            buffer.FreezeTick(3);
            AcceptedCommandRelay[] second = buffer.AcceptBundle(
                GameplayCommandBundle.Create(
                    7,
                    2,
                    3,
                    new[] { secondToggle }),
                4,
                12,
                null);

            Assert.AreEqual(1, first.Length);
            Assert.AreEqual(1, second.Length);
            Assert.AreEqual(
                1u,
                first[0].DecodeCommands()[0].CommandSeq);
            Assert.AreEqual(
                2u,
                second[0].DecodeCommands()[0].CommandSeq);
        }

        [Test]
        public void DirectionAim_CastAbility_RoundTripsCanonically()
        {
            UnitUid uid = new UnitUid(0, 12, 1);
            fp2 direction = fpmath.normalize(
                new fp2((fp)3, (fp)4));
            GameplayCommand cast =
                GameplayCommand.CreateCastAbility(
                    Header(uid, 10, 1),
                    3,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.ForDirection(direction));

            GameplayCommand[] decoded =
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { cast }).DecodeCommands();

            Assert.AreEqual(1, decoded.Length);
            Assert.AreEqual(
                AimKind.Direction,
                decoded[0].Aim.Kind);
            Assert.AreEqual(
                cast.Aim,
                decoded[0].Aim);
        }

        [Test]
        public void WireContracts_DoNotExposeCallerOwnedByteArrays()
        {
            UnitUid uid = new UnitUid(0, 11, 1);
            GameplayCommand command = GameplayCommand.CreateMove(
                Header(uid, 1, 1),
                new fp2(fp.one, fp.one));
            GameplayCommandBundle original =
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    new[] { command });
            byte[] bytes = original.CanonicalCommandBytes;
            var reconstructed = new GameplayCommandBundle(
                original.ClientId,
                original.BundleSequence,
                original.SendLocalTick,
                original.MinTargetTick,
                original.MaxTargetTick,
                original.CommandCount,
                bytes);

            bytes[0] ^= 0xff;
            byte[] exposed = reconstructed.CanonicalCommandBytes;
            exposed[0] ^= 0xff;

            Assert.AreEqual(1,
                reconstructed.DecodeCommands().Length);
        }

        [Test]
        public void CanonicalCodec_RoundTripsEveryFormalCommandKind()
        {
            UnitUid uid = new UnitUid(0, 12, 1);
            GameplayCommand[] source =
            {
                GameplayCommand.CreateAllocateAbilitySkillPoint(
                    Header(uid, 0, 1),
                    2),
                GameplayCommand.CreateEquipmentPurchase(
                    Header(uid, 0, 2),
                    101),
                GameplayCommand.CreateEquipmentSell(
                    Header(uid, 0, 3),
                    4),
                GameplayCommand.CreateEquipmentUndo(
                    Header(uid, 0, 4)),
                GameplayCommand.CreateSwapEquipmentSlot(
                    Header(uid, 0, 5),
                    1,
                    5),
                GameplayCommand.CreateUseItem(
                    Header(uid, 0, 6),
                    3,
                    AimSnapshot.ForPoint(
                        new fp2((fp)7, (fp)9))),
            };

            GameplayCommand[] decoded =
                GameplayCommandBundle.Create(
                    7,
                    1,
                    0,
                    source).DecodeCommands();

            Assert.AreEqual(source.Length, decoded.Length);
            Assert.AreEqual(
                EquipmentShopCommandOperationType.Purchase,
                decoded[1].ShopOperationType);
            Assert.AreEqual(101, decoded[1].EquipmentId);
            Assert.AreEqual(
                EquipmentShopCommandOperationType.Sell,
                decoded[2].ShopOperationType);
            Assert.AreEqual(4, decoded[2].SourceSlot);
            Assert.AreEqual(
                EquipmentShopCommandOperationType.Undo,
                decoded[3].ShopOperationType);
            Assert.AreEqual(1, decoded[4].SourceSlot);
            Assert.AreEqual(5, decoded[4].TargetSlot);
            Assert.AreEqual(3, decoded[5].SourceSlot);
            Assert.AreEqual(
                AimSnapshot.ForPoint(
                    new fp2((fp)7, (fp)9)),
                decoded[5].Aim);
        }

        [Test]
        public void Replicator_AndClientCoordinator_AcceptContinuousTick()
        {
            var serverPipeline =
                new SimulationTickPipeline(new UnitWorld());
            var archive = new AuthorityRecoveryArchive(8);
            var replicator = new AuthorityFrameReplicator(
                serverPipeline,
                new SimulationTickContextController(),
                new CommandRelayBuffer(),
                archive);
            var clientPipeline =
                new SimulationTickPipeline(new UnitWorld());
            var client = new PredictionRollbackCoordinator(
                new SnapshotStore(8),
                clientPipeline,
                new SimulationTickContextController(),
                3);

            Assert.IsTrue(client.ExecutePredictionTick());
            AuthorityFrame frame = replicator.ExecuteNextTick();
            client.OnAuthorityFrameReceived(frame);

            Assert.AreEqual(0, client.LatestAuthorityFrameTick);
            Assert.AreEqual(0, client.PredictedTickCount);
            Assert.AreEqual(PredictionPauseReason.None,
                client.PauseReasons);
        }

        [Test]
        public void AcceptedRelayReplacingExecutedLocalMove_ReplaysFromFrozenAnchor()
        {
            FrameSyncGameRuntime server =
                CreateMovingRuntime(out FrameSyncMoba.Unit.Unit serverUnit);
            FrameSyncGameRuntime client =
                CreateMovingRuntime(out FrameSyncMoba.Unit.Unit clientUnit);

            Assert.IsTrue(client.Prediction.ExecutePredictionTick());
            client.ReceiveAuthorityFrame(server.ExecuteAuthorityTick());

            fp2 establishedTarget = new fp2((fp)10, fp.zero);
            GameplayCommand serverMove = GameplayCommand.CreateMove(
                Header(serverUnit.UnitUid, 1, 1),
                establishedTarget);
            AcceptedCommandRelay[] initialRelays =
                server.AcceptCommandBundle(
                    GameplayCommandBundle.Create(
                        7,
                        1,
                        0,
                        new[] { serverMove }));
            Assert.That(initialRelays, Has.Length.EqualTo(1));
            client.ApplyAcceptedCommandRelay(initialRelays[0]);
            Assert.IsTrue(client.Prediction.ExecutePredictionTick());
            client.ReceiveAuthorityFrame(server.ExecuteAuthorityTick());

            GameplayCommand supersededLocalMove =
                GameplayCommand.CreateMove(
                    Header(clientUnit.UnitUid, 2, 3),
                    new fp2((fp)(-10), fp.zero));
            client.SubmitCommand(supersededLocalMove);
            Assert.IsTrue(client.Prediction.ExecutePredictionTick());

            GameplayCommand acceptedRepeatedMove =
                GameplayCommand.CreateMove(
                    Header(serverUnit.UnitUid, 2, 2),
                    establishedTarget);
            AcceptedCommandRelay[] replacementRelays =
                server.AcceptCommandBundle(
                    GameplayCommandBundle.Create(
                        7,
                        2,
                        1,
                        new[] { acceptedRepeatedMove }));
            Assert.That(replacementRelays, Has.Length.EqualTo(1));
            client.ApplyAcceptedCommandRelay(replacementRelays[0]);
            AuthorityFrame authority = server.ExecuteAuthorityTick();
            GameplayCommand futureLocalMove =
                GameplayCommand.CreateMove(
                    Header(clientUnit.UnitUid, 3, 4),
                    new fp2((fp)4, (fp)2));
            client.SubmitCommand(futureLocalMove);

            Assert.DoesNotThrow(
                () => client.ReceiveAuthorityFrame(authority));
            Assert.That(client.LastChecksum,
                Is.EqualTo(authority.SharedGameplayChecksum));
            Assert.That(client.Prediction.LatestAuthorityFrameTick,
                Is.EqualTo(2));
            var pending =
                client.CommandCollector.GetCanonicalCommands();
            Assert.That(pending, Has.Count.EqualTo(1));
            Assert.That(pending[0], Is.EqualTo(futureLocalMove),
                "Rollback must reinsert commands at or beyond the replay " +
                "end Tick after authoritative replacement.");
        }

        [Test]
        public void AuthorityBeforePrediction_BuffersUntilLocalTickCompletes()
        {
            var serverPipeline =
                new SimulationTickPipeline(
                    new UnitWorld());
            var server =
                new AuthorityFrameReplicator(
                    serverPipeline,
                    new SimulationTickContextController(),
                    new CommandRelayBuffer(),
                    new AuthorityRecoveryArchive(8));
            var clientPipeline =
                new SimulationTickPipeline(
                    new UnitWorld());
            var client =
                new PredictionRollbackCoordinator(
                    new SnapshotStore(8),
                    clientPipeline,
                    new SimulationTickContextController(),
                    3);
            AuthorityFrame authority =
                server.ExecuteNextTick();

            Assert.DoesNotThrow(
                () =>
                    client.OnAuthorityFrameReceived(
                        authority));
            Assert.AreEqual(
                -1,
                client.LatestAuthorityFrameTick);

            Assert.IsTrue(
                client.ExecutePredictionTick());
            Assert.AreEqual(
                0,
                client.LatestAuthorityFrameTick);
            Assert.AreEqual(
                PredictionPauseReason.None,
                client.PauseReasons);
        }

        [Test]
        public void AuthorityRuntime_ReleasesClientRollbackHistory()
        {
            var runtime = new FrameSyncGameRuntime(
                new UnitWorld { TickRate = 20 },
                null,
                2,
                0,
                3,
                1,
                1,
                fp.one,
                1,
                snapshotWindowTicks: 2);

            Assert.DoesNotThrow(
                () =>
                {
                    for (int i = 0; i < 8; i++)
                        runtime.ExecuteAuthorityTick();
                });
            Assert.AreEqual(8, runtime.CurrentTick);
            Assert.AreEqual(
                7,
                runtime.LatestSynchronizedServerTick);
        }

        [Test]
        public void RestoreInitialSnapshot_SetsAuthorityBaseline()
        {
            var runtime = new FrameSyncGameRuntime(
                new UnitWorld { TickRate = 20 },
                null,
                2,
                0,
                3,
                1,
                1,
                fp.one,
                1,
                snapshotWindowTicks: 4,
                maxPredictionLeadTicks: 2);
            GameplaySnapshot snapshot =
                runtime.TickPipeline
                    .CaptureAggregateSnapshot();

            runtime.RestoreInitialSnapshot(
                snapshot,
                3,
                ExecutionMode.ClientPrediction);

            Assert.AreEqual(3, runtime.CurrentTick);
            Assert.AreEqual(
                2,
                runtime.Prediction
                    .LatestAuthorityFrameTick);
            Assert.AreEqual(
                0,
                runtime.Prediction
                    .PredictedTickCount);
            Assert.IsFalse(
                runtime.Prediction
                    .HasMissingAuthorityFrames);
        }

        [Test]
        public void PredictionLeadLimit_PausesWithoutExecutingAnotherTick()
        {
            var pipeline = new SimulationTickPipeline(new UnitWorld());
            var client = new PredictionRollbackCoordinator(
                new SnapshotStore(8),
                pipeline,
                new SimulationTickContextController(),
                1);

            Assert.IsTrue(client.ExecutePredictionTick());
            Assert.IsFalse(client.ExecutePredictionTick());
            Assert.AreEqual(1, pipeline.LocalSimulationTick);
            Assert.That(
                client.PauseReasons &
                PredictionPauseReason.PredictionLeadLimit,
                Is.Not.EqualTo(PredictionPauseReason.None));
        }

        [Test]
        public void Recovery_UsesRequestedSequenceAndDoesNotMutateResponseArray()
        {
            var pipeline = new SimulationTickPipeline(new UnitWorld());
            var client = new PredictionRollbackCoordinator(
                new SnapshotStore(8),
                pipeline,
                new SimulationTickContextController(),
                3);
            AuthorityFrame later = AuthorityFrame.Create(
                1,
                2,
                0,
                System.Array.Empty<GameplayCommand>(),
                AuthorityFrameFlags.None,
                0);
            client.OnAuthorityFrameReceived(later);
            AuthorityRecoveryRequest request =
                client.BuildRecoveryRequest();
            AuthorityFrame missing = AuthorityFrame.Create(
                0,
                1,
                0,
                System.Array.Empty<GameplayCommand>(),
                AuthorityFrameFlags.None,
                0);
            var callerFrames = new[] { later, missing };
            var response = new AuthorityRecoveryResponse(
                request.RequestSequence,
                callerFrames);

            Assert.IsFalse(client.ApplyRecoveryResponse(
                new AuthorityRecoveryResponse(
                    request.RequestSequence + 1,
                    new[] { missing })));
            AuthorityFrame[] exposed = response.AuthorityFrames;
            System.Array.Reverse(exposed);
            Assert.AreEqual(1, callerFrames[0].Tick);
            Assert.AreEqual(0, response.AuthorityFrames[1].Tick);
        }

        [Test]
        public void RecoveryArchive_FailsWhenRequestedFrameWasEvicted()
        {
            var archive = new AuthorityRecoveryArchive(2);
            archive.Add(EmptyFrame(0, 1));
            archive.Add(EmptyFrame(1, 2));
            archive.Add(EmptyFrame(2, 3));
            var request = new AuthorityRecoveryRequest(
                1,
                new[] { new MissingAuthorityFrameRange(0, 0) });

            AuthorityRecoveryUnavailableException exception =
                Assert.Throws<AuthorityRecoveryUnavailableException>(
                    () => archive.BuildResponse(request));
            Assert.AreEqual(0, exception.MissingTick);
        }

        [Test]
        public void Recovery_FillsGapThenAcceptsBufferedFrameSequentially()
        {
            var serverPipeline =
                new SimulationTickPipeline(new UnitWorld());
            var archive = new AuthorityRecoveryArchive(8);
            var server = new AuthorityFrameReplicator(
                serverPipeline,
                new SimulationTickContextController(),
                new CommandRelayBuffer(),
                archive);
            AuthorityFrame frame0 = server.ExecuteNextTick();
            AuthorityFrame frame1 = server.ExecuteNextTick();

            var clientPipeline =
                new SimulationTickPipeline(new UnitWorld());
            var client = new PredictionRollbackCoordinator(
                new SnapshotStore(8),
                clientPipeline,
                new SimulationTickContextController(),
                3);
            Assert.IsTrue(client.ExecutePredictionTick());
            Assert.IsTrue(client.ExecutePredictionTick());
            client.OnAuthorityFrameReceived(frame1);
            Assert.IsTrue(client.HasMissingAuthorityFrames);

            AuthorityRecoveryRequest request =
                client.BuildRecoveryRequest();
            AuthorityRecoveryResponse response =
                archive.BuildResponse(request);
            Assert.AreEqual(1, response.AuthorityFrames.Length);
            Assert.AreEqual(frame0.Tick,
                response.AuthorityFrames[0].Tick);

            Assert.IsTrue(client.ApplyRecoveryResponse(response));
            Assert.AreEqual(1, client.LatestAuthorityFrameTick);
            Assert.IsFalse(client.HasMissingAuthorityFrames);
        }

        private static AuthorityFrame EmptyFrame(
            int tick,
            uint sequence)
        {
            return AuthorityFrame.Create(
                tick,
                sequence,
                0,
                System.Array.Empty<GameplayCommand>(),
                AuthorityFrameFlags.None,
                0);
        }

        private static CommandHeader Header(
            UnitUid uid,
            int targetTick,
            uint sequence)
        {
            return new CommandHeader(
                sequence,
                7,
                0,
                uid,
                targetTick,
                GameplayCommandKind.None,
                0,
                0);
        }

        private static FrameSyncGameRuntime CreateMovingRuntime(
            out FrameSyncMoba.Unit.Unit movingUnit)
        {
            var world = new UnitWorld
            {
                StatDefinitionTable = new StatDefinitionTable(),
                PhysicsWorld = new Physics.PhysicsWorld(),
                PathGrid = new PathGridMap2D(),
            };
            world.PathGrid.Initialise(
                new fp2((fp)(-20), (fp)(-20)),
                new fp2((fp)20, (fp)20),
                fp.one);
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "MovingHero",
                RuntimeEntityPrefabId = 99,
                UnitKind = UnitKind.Hero,
                BaseStats = new StatPreset(),
            };
            var spawnTick = new SimulationTickContextController();
            spawnTick.BeginTick(0, ExecutionMode.ServerAuthority);
            try
            {
                movingUnit = world.SpawnUnit(
                    prototype,
                    TeamId.Neutral,
                    0,
                    fp.zero,
                    fp.zero);
            }
            finally
            {
                spawnTick.EndTick();
            }
            movingUnit.MovementHandler.SetMoveSpeed((fp)4);
            return new FrameSyncGameRuntime(
                world,
                world.PhysicsWorld,
                0,
                0,
                180,
                300,
                60,
                (fp)7 / (fp)10,
                42u,
                snapshotWindowTicks: 16,
                maxPredictionLeadTicks: 3);
        }
    }
}

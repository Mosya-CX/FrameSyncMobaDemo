using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.PlayerInput.Tests
{
    public sealed class PlayerCommandRequesterTests
    {
        [Test]
        public void EventBuffer_AssignsStableSequenceAndRejectsOverflow()
        {
            var buffer = new LocalInputEventBuffer();
            for (int i = 0; i < LocalInputEventBuffer.MaxLocalInputEventsPerUnityFrame; i++)
            {
                Assert.IsTrue(buffer.Push(
                    LocalGameplayInputEventKind.AbilityKeyPressed,
                    (byte)(i % 4),
                    new Vector2(i, i)));
            }
            Assert.IsFalse(buffer.Push(
                LocalGameplayInputEventKind.PrimaryClick, 0, Vector2.zero));

            ulong previous = 0;
            while (buffer.TryDequeue(out LocalGameplayInputEvent inputEvent))
            {
                Assert.Greater(inputEvent.LocalEventSequence, previous);
                previous = inputEvent.LocalEventSequence;
            }
        }

        [Test]
        public void HoldRelease_AllocatesFocusBeforeCommitAtSameTargetTick()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new HoldReleaseTemplateProvider());

            Assert.IsTrue(requester.RequestCastAbility(
                0,
                AbilitySignalVerb.Focus,
                AimSnapshot.None,
                out GameplayCommandRequestReceipt focus));
            Assert.IsTrue(requester.RequestCastAbility(
                0,
                AbilitySignalVerb.Commit,
                AimSnapshot.ForDirection(new Unity.Mathematics.FixedPoint.fp2(1, 0)),
                out GameplayCommandRequestReceipt commit));

            var commands = collector.GetCanonicalCommands();
            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual(15, focus.TargetTick);
            Assert.AreEqual(focus.TargetTick, commit.TargetTick);
            Assert.Less(focus.CommandSeq, commit.CommandSeq);
            Assert.AreEqual(2, commands[0].PlayerSlot);
            Assert.AreEqual(unit.UnitUid, commands[0].ControlledUnitUid);
            Assert.AreEqual(AimKind.Direction, commands[1].Aim.Kind);
        }

        [Test]
        public void ControlledUnitChange_ClearsLocalAbilityState()
        {
            UnitType first = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0), UnitKind.Hero, 0, new TeamId(1));
            UnitType second = UnitTestFactory.CreateUnit(
                new UnitUid(10, 5, 0), UnitKind.Hero, 0, new TeamId(1));
            var requester = new PlayerCommandRequester(
                first,
                new GameplayInputGate(),
                new CommandCollector(),
                0,
                1,
                new CommandTargetTickResolver(
                    () => 10,
                    () => 10,
                    1,
                    12),
                new HoldReleaseTemplateProvider());
            var buffer = new LocalInputEventBuffer();
            buffer.Push(
                LocalGameplayInputEventKind.AbilityKeyPressed,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);
            Assert.AreEqual(
                LocalAbilityInputStateKind.FocusRequested,
                requester.GetAbilityState(0).Kind);

            requester.SetControlledUnit(second);

            Assert.AreEqual(
                LocalAbilityInputStateKind.Idle,
                requester.GetAbilityState(0).Kind);
        }

        [Test]
        public void TargetTickResolver_UsesFormalLeadFormulaAndBuildTick()
        {
            var resolver = new CommandTargetTickResolver(
                () => 20,
                () => 23,
                3,
                12);

            int targetTick = resolver.ResolveTargetTick(out int buildTick);

            Assert.That(buildTick, Is.EqualTo(20));
            Assert.That(targetTick, Is.EqualTo(26));
        }

        [Test]
        public void ShopRequests_UseCanonicalCommandsAndSharedSequence()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12));

            Assert.IsTrue(requester.RequestEquipmentPurchase(101));
            Assert.IsTrue(requester.RequestEquipmentSell(3));
            Assert.IsTrue(requester.RequestEquipmentUndo());

            var commands = collector.GetCanonicalCommands();
            Assert.That(commands, Has.Count.EqualTo(3));
            Assert.That(commands[0].Kind, Is.EqualTo(GameplayCommandKind.EquipmentShop));
            Assert.That(
                commands[0].ShopOperationType,
                Is.EqualTo(EquipmentShopCommandOperationType.Purchase));
            Assert.That(commands[0].EquipmentId, Is.EqualTo(101));
            Assert.That(
                commands[1].ShopOperationType,
                Is.EqualTo(EquipmentShopCommandOperationType.Sell));
            Assert.That(commands[1].SourceSlot, Is.EqualTo(3));
            Assert.That(
                commands[2].ShopOperationType,
                Is.EqualTo(EquipmentShopCommandOperationType.Undo));
            Assert.That(commands[0].CommandSeq, Is.EqualTo(1));
            Assert.That(commands[1].CommandSeq, Is.EqualTo(2));
            Assert.That(commands[2].CommandSeq, Is.EqualTo(3));
            Assert.That(commands[0].TargetTick, Is.EqualTo(15));
            Assert.That(commands[1].TargetTick, Is.EqualTo(15));
            Assert.That(commands[2].TargetTick, Is.EqualTo(15));
        }

        [Test]
        public void SkillPointRequest_UsesCanonicalCommandAndSharedSequence()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 5, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12));

            Assert.IsTrue(
                requester.RequestAllocateAbilitySkillPoint(2));
            Assert.IsFalse(
                requester.RequestAllocateAbilitySkillPoint(4));
            Assert.IsFalse(
                requester.RequestAllocateAbilitySkillPoint(9));

            var commands = collector.GetCanonicalCommands();
            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(
                commands[0].Kind,
                Is.EqualTo(
                    GameplayCommandKind
                        .AllocateAbilitySkillPoint));
            Assert.That(
                commands[0].AbilitySlot,
                Is.EqualTo(2));
            Assert.That(
                commands[0].CommandSeq,
                Is.EqualTo(1));
            Assert.That(
                commands[0].TargetTick,
                Is.EqualTo(15));
        }

        private sealed class HoldReleaseTemplateProvider :
            IPlayerAbilityInputProfileProvider
        {
            public bool TryGetTemplate(
                byte slot,
                out InputMappingTemplate template)
            {
                template =
                    AbilityInputMapping
                        .BuildHoldReleaseDefault();
                return true;
            }

            public bool TryGetAimKind(byte slot, out AimKind aimKind)
            {
                aimKind = AimKind.Point;
                return true;
            }
        }

        private sealed class LocalAimTemplateProvider :
            IPlayerAbilityInputProfileProvider
        {
            public bool TryGetTemplate(
                byte slot,
                out InputMappingTemplate template)
            {
                template =
                    AbilityInputMapping
                        .BuildLocalAimDefault();
                return true;
            }

            public bool TryGetAimKind(
                byte slot,
                out AimKind aimKind)
            {
                aimKind = AimKind.Point;
                return true;
            }
        }

        private sealed class PressCommitTemplateProvider :
            IPlayerAbilityInputProfileProvider
        {
            public bool TryGetTemplate(
                byte slot,
                out InputMappingTemplate template)
            {
                template =
                    AbilityInputMapping
                        .DefaultPressCommit;
                return true;
            }

            public bool TryGetAimKind(
                byte slot,
                out AimKind aimKind)
            {
                aimKind = AimKind.None;
                return true;
            }
        }

        private static Camera CreateDownwardCamera()
        {
            var go = new GameObject(
                "TestCamera",
                typeof(Camera));
            go.transform.position =
                new Vector3(0f, 10f, 0f);
            go.transform.rotation =
                Quaternion.Euler(90f, 0f, 0f);
            return go.GetComponent<Camera>();
        }

        [Test]
        public void HoldReleaseDefault_PressFocus_ReleaseNoOp_LeftClickCommitsAndDedups()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new HoldReleaseTemplateProvider());
            Camera camera = CreateDownwardCamera();
            var resolver =
                new MouseWorldResolver(
                    camera,
                    Unity.Mathematics.FixedPoint.fp.zero,
                    null);
            try
            {
                var buffer = new LocalInputEventBuffer();
                buffer.Push(
                    LocalGameplayInputEventKind
                        .AbilityKeyPressed,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind
                            .FocusRequested));
                Assert.That(
                    collector.GetCanonicalCommands(),
                    Has.Count.EqualTo(1));
                Assert.That(
                    collector.GetCanonicalCommands()[0]
                        .AbilityVerb,
                    Is.EqualTo(AbilitySignalVerb.Focus));

                // Default mapping: release is None -> no signal, no state change.
                buffer.Push(
                    LocalGameplayInputEventKind
                        .AbilityKeyReleased,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    collector.GetCanonicalCommands(),
                    Has.Count.EqualTo(1),
                    "Release must not Commit under the default template.");
                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind
                            .FocusRequested));

                buffer.Push(
                    LocalGameplayInputEventKind
                        .PrimaryClick,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    collector.GetCanonicalCommands(),
                    Has.Count.EqualTo(2));
                Assert.That(
                    collector.GetCanonicalCommands()[1]
                        .AbilityVerb,
                    Is.EqualTo(AbilitySignalVerb.Commit));
                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind
                            .CommitRequested));

                buffer.Push(
                    LocalGameplayInputEventKind
                        .PrimaryClick,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    collector.GetCanonicalCommands(),
                    Has.Count.EqualTo(2),
                    "Duplicate left-click must be suppressed after CommitRequested.");
            }
            finally
            {
                Object.DestroyImmediate(
                    camera.gameObject);
            }
        }

        [Test]
        public void LocalAimDefault_PressOpensAim_LeftCommits_RightAndEscapeCloseWithoutCancel()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new LocalAimTemplateProvider());
            Camera camera = CreateDownwardCamera();
            var resolver =
                new MouseWorldResolver(
                    camera,
                    Unity.Mathematics.FixedPoint.fp.zero,
                    null);
            try
            {
                var buffer = new LocalInputEventBuffer();
                buffer.Push(
                    LocalGameplayInputEventKind
                        .AbilityKeyPressed,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind
                            .LocalAiming));
                Assert.That(
                    collector.GetCanonicalCommands(),
                    Is.Empty,
                    "Press under LocalAim must not produce a Command.");

                buffer.Push(
                    LocalGameplayInputEventKind
                        .SecondaryClick,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind.Idle));
                Assert.That(
                    collector.GetCanonicalCommands(),
                    Is.Empty,
                    "Right-click must only close local aim, with no Cancel and no Move/Attack.");

                buffer.Push(
                    LocalGameplayInputEventKind
                        .AbilityKeyPressed,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);
                buffer.Push(
                    LocalGameplayInputEventKind.Cancel,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind.Idle));
                Assert.That(
                    collector.GetCanonicalCommands(),
                    Is.Empty,
                    "Escape must only close local aim.");

                buffer.Push(
                    LocalGameplayInputEventKind
                        .AbilityKeyPressed,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);
                buffer.Push(
                    LocalGameplayInputEventKind
                        .PrimaryClick,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    collector.GetCanonicalCommands(),
                    Has.Count.EqualTo(1));
                Assert.That(
                    collector.GetCanonicalCommands()[0]
                        .AbilityVerb,
                    Is.EqualTo(AbilitySignalVerb.Commit));
                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind
                            .CommitRequested));
            }
            finally
            {
                Object.DestroyImmediate(
                    camera.gameObject);
            }
        }

        [Test]
        public void PressCommitDefault_PressCommitsImmediately()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new PressCommitTemplateProvider());
            var buffer = new LocalInputEventBuffer();
            buffer.Push(
                LocalGameplayInputEventKind
                    .AbilityKeyPressed,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);

            Assert.That(
                collector.GetCanonicalCommands(),
                Has.Count.EqualTo(1));
            Assert.That(
                collector.GetCanonicalCommands()[0]
                    .AbilityVerb,
                Is.EqualTo(AbilitySignalVerb.Commit));
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind
                        .CommitRequested));
        }

        [Test]
        public void UnmappedReleaseEvent_DoesNothing()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new PressCommitTemplateProvider());
            var buffer = new LocalInputEventBuffer();
            buffer.Push(
                LocalGameplayInputEventKind
                    .AbilityKeyPressed,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);
            buffer.Push(
                LocalGameplayInputEventKind
                    .AbilityKeyReleased,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);

            Assert.That(
                collector.GetCanonicalCommands(),
                Has.Count.EqualTo(1),
                "Unmapped release must not add a Command.");
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind
                        .CommitRequested));
        }

        [Test]
        public void RightClickAndEscapeDuringHoldRelease_NoCancel_StatePreserved()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new HoldReleaseTemplateProvider());
            var buffer = new LocalInputEventBuffer();
            buffer.Push(
                LocalGameplayInputEventKind
                    .AbilityKeyPressed,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);
            buffer.Push(
                LocalGameplayInputEventKind
                    .SecondaryClick,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);
            buffer.Push(
                LocalGameplayInputEventKind.Cancel,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);

            Assert.That(
                collector.GetCanonicalCommands(),
                Has.Count.EqualTo(1),
                "Right-click and Escape must not send Cancel.");
            Assert.That(
                collector.GetCanonicalCommands()[0]
                    .AbilityVerb,
                Is.EqualTo(AbilitySignalVerb.Focus));
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind
                        .FocusRequested));
        }

        [Test]
        public void UnlearnedAbilityKey_ProducesNoIndicatorAndNoCommand()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1),
                learnTestAbilities: false);
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new HoldReleaseTemplateProvider());

            var buffer = new LocalInputEventBuffer();
            buffer.Push(
                LocalGameplayInputEventKind
                    .AbilityKeyPressed,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);

            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind.Idle),
                "Unlearned ability must not enter LocalAiming/Focus state.");
            Assert.That(
                collector.GetCanonicalCommands(),
                Is.Empty,
                "Unlearned ability must not produce a cast Command.");
        }

        [Test]
        public void LocalAim_PressBlockedWhenRuntimeCannotOpenAim()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new LocalAimTemplateProvider(),
                new FakeAbilityRuntimeView
                {
                    CanOpenAim = false,
                });

            var buffer = new LocalInputEventBuffer();
            buffer.Push(
                LocalGameplayInputEventKind
                    .AbilityKeyPressed,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);

            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind.Idle),
                "Cooldown/lockout must not open a local aim indicator.");
            Assert.That(
                collector.GetCanonicalCommands(),
                Is.Empty);
        }

        [Test]
        public void
            SequentialRecast_CommitAcceptedMovesLocalStateBackToIdle()
        {
            SetCurrentTick(10);
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var view = new FakeAbilityRuntimeView
            {
                CanOpenAim = true,
            };
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new LocalAimTemplateProvider(),
                view);

            Camera camera = CreateDownwardCamera();
            var resolver =
                new MouseWorldResolver(
                    camera,
                    fp.zero,
                    null);
            try
            {
                var buffer = new LocalInputEventBuffer();
                buffer.Push(
                    LocalGameplayInputEventKind
                        .AbilityKeyPressed,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);
                buffer.Push(
                    LocalGameplayInputEventKind
                        .PrimaryClick,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind
                            .CommitRequested));
                Assert.That(
                    collector.GetCanonicalCommands(),
                    Has.Count.EqualTo(1));

                // The Runtime executed the Commit and advanced the session
                // into a recast window: still active, no longer waiting for
                // Commit.
                SetCurrentTick(15);
                view.HasSession = true;
                view.WaitingForCommit = false;
                requester.ProcessFrame(buffer, resolver);

                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind.Idle),
                    "After a recast window opens the local state must " +
                    "return to Idle so the next key press can aim the " +
                    "next stage.");

                // A fresh key press can now open the next stage's local aim.
                buffer.Push(
                    LocalGameplayInputEventKind
                        .AbilityKeyPressed,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);
                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind
                            .LocalAiming));
            }
            finally
            {
                Object.DestroyImmediate(
                    camera.gameObject);
            }
        }

        [Test]
        public void
            FocusRequested_ReturnsIdleAfterTargetReachedWithNoSession()
        {
            SetCurrentTick(10);
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var collector = new CommandCollector();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new HoldReleaseTemplateProvider(),
                new FakeAbilityRuntimeView());

            var buffer = new LocalInputEventBuffer();
            buffer.Push(
                LocalGameplayInputEventKind
                    .AbilityKeyPressed,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind
                        .FocusRequested));

            // The Runtime executed the Focus but no Session exists (e.g. the
            // ability was on cooldown): the local state must return to Idle.
            SetCurrentTick(15);
            requester.ProcessFrame(buffer, null);
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind.Idle));
            Assert.That(
                collector.GetCanonicalCommands(),
                Has.Count.EqualTo(1));
        }

        [Test]
        public void
            FocusRequested_BecomesGameplayFocusingWhenSessionObserved()
        {
            SetCurrentTick(10);
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var view = new FakeAbilityRuntimeView();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                new CommandCollector(),
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new HoldReleaseTemplateProvider(),
                view);

            var buffer = new LocalInputEventBuffer();
            buffer.Push(
                LocalGameplayInputEventKind
                    .AbilityKeyPressed,
                0,
                Vector2.zero);
            requester.ProcessFrame(buffer, null);

            SetCurrentTick(15);
            view.HasSession = true;
            view.WaitingForCommit = true;
            requester.ProcessFrame(buffer, null);
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind
                        .GameplayFocusing));
        }

        [Test]
        public void
            CommitRequested_RecoversToFocusingWhenSessionStillWaits()
        {
            SetCurrentTick(10);
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            var view = new FakeAbilityRuntimeView();
            var requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                new CommandCollector(),
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                new HoldReleaseTemplateProvider(),
                view);

            Camera camera = CreateDownwardCamera();
            var resolver =
                new MouseWorldResolver(
                    camera,
                    fp.zero,
                    null);
            try
            {
                var buffer = new LocalInputEventBuffer();
                buffer.Push(
                    LocalGameplayInputEventKind
                        .AbilityKeyPressed,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);
                buffer.Push(
                    LocalGameplayInputEventKind
                        .PrimaryClick,
                    0,
                    Vector2.zero);
                requester.ProcessFrame(buffer, resolver);
                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind
                            .CommitRequested));

                // Commit was not accepted by Gameplay while the Session
                // still waits in the Hold stage: recover to Focusing.
                SetCurrentTick(15);
                view.HasSession = true;
                view.WaitingForCommit = true;
                requester.ProcessFrame(buffer, resolver);
                Assert.That(
                    requester.GetAbilityState(0).Kind,
                    Is.EqualTo(
                        LocalAbilityInputStateKind
                            .GameplayFocusing));
            }
            finally
            {
                Object.DestroyImmediate(
                    camera.gameObject);
            }
        }

        [Test]
        public void
            CanOpenLocalAim_SequentialRecast_GatesByReadyAndRecastLockout()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1),
                learnTestAbilities: false);
            AbilityHandler handler = unit.AbilityHandler;
            Assert.That(handler, Is.Not.Null);
            var slot = new AbilitySlotRuntime
            {
                SlotIndex = 0,
                AllocatedPoints = 1,
                MaxAllocatedPoints = 5,
                ActiveAbilityId = 200,
            };
            var runtime = new AbilityRuntime
            {
                Definition = new AbilityDef
                {
                    AbilityId = 200,
                    CastModel =
                        CreateSequentialRecastModel(),
                    AimKind = AimKind.Direction,
                    CastRange = (fp)6m,
                    CostPlan = default,
                    CooldownByLevel = default,
                },
                Level = 1,
            };
            slot.AddAbility(runtime);
            handler.AddSlot(slot);

            SetCurrentTick(50);

            // No session and not on cooldown: ready to open local aim.
            Assert.That(
                handler.CanOpenLocalAim(0),
                Is.True);

            // Q1 impact is playing: a Commit cannot advance the session, so
            // local aim must stay closed.
            runtime.BeginSession(
                1,
                0,
                AimSnapshot.None);
            runtime.ActiveSession.CurrentStageKey = 1;
            runtime.ActiveSession.StageElapsedTicks = 10;
            Assert.That(
                handler.CanOpenLocalAim(0),
                Is.False);

            // Recast window before the minimum recast delay: still blocked.
            runtime.ActiveSession.CurrentStageKey = 2;
            runtime.ActiveSession.StageElapsedTicks = 10;
            Assert.That(
                handler.CanOpenLocalAim(0),
                Is.False);

            // Recast window after the minimum recast delay: Q2 may be aimed.
            runtime.ActiveSession.StageElapsedTicks = 30;
            Assert.That(
                handler.CanOpenLocalAim(0),
                Is.True);

            // Ability on cooldown with no session: blocked.
            runtime.EndSession(0, 0);
            runtime.StartCooldown(0, 100);
            Assert.That(
                handler.CanOpenLocalAim(0),
                Is.False);
        }

        private static SequentialRecastCastModelDef
            CreateSequentialRecastModel()
        {
            CastStage impact =
                new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 27,
                    LockMovement = true,
                };
            CastStage window =
                new CastStage
                {
                    StageKey = 2,
                    DurationTicks = 120,
                    Interruptible = true,
                };
            CastStage secondImpact =
                new CastStage
                {
                    StageKey = 3,
                    DurationTicks = 27,
                    LockMovement = true,
                };
            CastStage secondWindow =
                new CastStage
                {
                    StageKey = 4,
                    DurationTicks = 120,
                    Interruptible = true,
                };
            CastStage finalImpact =
                new CastStage
                {
                    StageKey = 5,
                    DurationTicks = 27,
                    LockMovement = true,
                };
            return new SequentialRecastCastModelDef
            {
                FirstImpact = impact,
                FirstRecastWindow = window,
                SecondImpact = secondImpact,
                SecondRecastWindow = secondWindow,
                FinalImpact = finalImpact,
                FirstMinimumRecastDelayTicks = 30,
                SecondMinimumRecastDelayTicks = 30,
            };
        }

        private static void SetCurrentTick(int tick)
        {
            var controller =
                new SimulationTickContextController();
            controller.BeginTick(
                tick,
                ExecutionMode.ClientPrediction);
            controller.EndTick();
        }

        private sealed class FakeAbilityRuntimeView :
            ILocalAbilityRuntimeView
        {
            public bool HasSession;
            public bool WaitingForCommit;
            public bool CanOpenAim = true;

            public bool HasActiveSession(
                UnitUid ownerUid,
                byte slot) =>
                HasSession;

            public bool IsWaitingForCommit(
                UnitUid ownerUid,
                byte slot) =>
                WaitingForCommit;

            public bool CanOpenLocalAim(
                UnitUid ownerUid,
                byte slot) =>
                CanOpenAim;
        }

        [Test]
        public void
            InputGate_DuringMovableCastSession_MoveAllowedButAttackBlocked()
        {
            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            AbilityHandler handler = unit.AbilityHandler;
            Assert.IsNotNull(handler);

            // A session whose cast model does not lock movement (equivalent
            // to a charge Hold stage): HasActiveCastSession is true while
            // IsCastMovementLocked stays false. Move must remain allowed,
            // attack must be rejected while any cast/charge session is live.
            var slot = new AbilitySlotRuntime
            {
                SlotIndex = 4,
                MaxAllocatedPoints = 1,
                ActiveAbilityId = 99,
            };
            slot.AddAbility(
                new AbilityRuntime
                {
                    Definition =
                        new AbilityDef
                        {
                            AbilityId = 99,
                        },
                });
            handler.AddSlot(slot);
            slot.GetActiveAbility()
                .BeginSession(
                    1,
                    0,
                    AimSnapshot.None);

            var gate =
                new GameplayInputGate();
            Assert.IsTrue(
                handler.HasActiveCastSession());
            Assert.IsFalse(
                handler.IsCastMovementLocked());
            Assert.IsTrue(
                gate.IsMoveAllowed(unit),
                "Move must stay allowed during a movable " +
                "cast/charge session.");
            Assert.IsFalse(
                gate.IsAttackAllowed(unit),
                "Attack must be blocked while any cast/charge " +
                "session is active.");
        }
    }
}

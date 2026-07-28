using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEngine;
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
                new HoldReleaseProfileProvider());

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
                new HoldReleaseProfileProvider());
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

        private sealed class HoldReleaseProfileProvider : IPlayerAbilityInputProfileProvider
        {
            public bool TryGetProfile(
                byte slot,
                out BakedPlayerAbilityInputProfile profile)
            {
                profile = new BakedPlayerAbilityInputProfile(
                    BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit);
                return true;
            }

            public bool TryGetAimKind(byte slot, out AimKind aimKind)
            {
                aimKind = AimKind.Direction;
                return true;
            }
        }
    }
}

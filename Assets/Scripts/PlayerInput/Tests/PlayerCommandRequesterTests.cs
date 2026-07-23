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
                () => 12,
                () => 15,
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
                () => 10,
                () => 11,
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

using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class StatHandlerSnapshotTests
    {
        private StatHandler CreateHandler()
        {
            return UnitTestFactory.CreateStatHandler(
                StatTestHelpers.CreateDefaultTable(),
                StatTestHelpers.CreateSimplePreset(),
                StatTestHelpers.DefaultOwnerUid,
                level: 1,
                statGrowthC: 0.5m,
                statGrowthD: 0m);
        }

        [Test]
        public void CaptureRestore_RoundTrip_PreservesAllState()
        {
            StatHandler h = CreateHandler();
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.AddModifier(StatId.MaxHealth, StatModifierOperation.FlatAdd, (fp)100m);
            h.FinalizeTick();

            StatHandlerSnapshot snapshot = default;
            h.Capture(ref snapshot);

            // Modify after capture
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)999m);
            h.Level = 5;
            h.ClearModifiers();

            // Restore
            h.Restore(in snapshot);

            Assert.AreEqual(1, h.Level);
            Assert.AreEqual((fp)150m, h.GetStat(StatId.AttackDamage));
            Assert.AreEqual((fp)600m, h.GetStat(StatId.MaxHealth));
        }

        [Test]
        public void CaptureRestore_AfterModifications_ReturnsToCapturedState()
        {
            StatHandler h = CreateHandler();
            StatModifierHandle handle = h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)30m);
            h.FinalizeTick();

            StatHandlerSnapshot snapshot = default;
            h.Capture(ref snapshot);

            h.SetModifierValue(handle, (fp)500m);
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)200m);
            h.FinalizeTick();

            h.Restore(in snapshot);

            Assert.AreEqual((fp)130m, h.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void RollbackReplay_Equivalence()
        {
            // Execute 3 ticks, capture, execute 3 more, restore, re-execute 3
            StatHandler h1 = CreateHandler();

            // Tick 1-3: add modifiers
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            h1.FinalizeTick();
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)20m);
            h1.FinalizeTick();
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)30m);
            h1.FinalizeTick();

            // Capture at tick 3
            StatHandlerSnapshot snapshot = default;
            h1.Capture(ref snapshot);

            // Execute 3 more ticks
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)40m);
            h1.FinalizeTick();
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h1.FinalizeTick();
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)60m);
            h1.FinalizeTick();

            fp afterContinue = h1.GetStat(StatId.AttackDamage);

            // Now replay from snapshot
            StatHandler h2 = CreateHandler();
            h2.Restore(in snapshot);

            h2.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)40m);
            h2.FinalizeTick();
            h2.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h2.FinalizeTick();
            h2.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)60m);
            h2.FinalizeTick();

            fp afterReplay = h2.GetStat(StatId.AttackDamage);

            Assert.AreEqual(afterContinue, afterReplay);
        }

        [Test]
        public void Restore_DoesNotTriggerDirtyRecompute_ValuesMatchSnapshot()
        {
            StatHandler h = CreateHandler();
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.FinalizeTick();

            StatHandlerSnapshot snapshot = default;
            h.Capture(ref snapshot);

            h.ClearModifiers();
            h.Restore(in snapshot);

            // After restore, the FinalValue should match what was captured
            // (not recomputed unless we call Rebuild)
            Assert.AreEqual((fp)150m, h.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void Rebuild_MarksAllEntriesDirty()
        {
            StatHandler h = CreateHandler();
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.FinalizeTick();

            StatHandlerSnapshot snapshot = default;
            h.Capture(ref snapshot);

            // Change level, then restore + rebuild
            h.Level = 3;
            h.Restore(in snapshot);
            h.Rebuild(default);

            // Level should be restored to 1
            Assert.AreEqual(1, h.Level);
            Assert.AreEqual((fp)150m, h.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void Determinism_SameSequence_SameSnapshot()
        {
            StatHandler h1 = CreateHandler();
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h1.FinalizeTick();
            StatHandlerSnapshot s1 = default;
            h1.Capture(ref s1);

            StatHandler h2 = CreateHandler();
            h2.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h2.FinalizeTick();
            StatHandlerSnapshot s2 = default;
            h2.Capture(ref s2);

            Assert.AreEqual(s1.Level, s2.Level);
            Assert.AreEqual(s1.NextStatSeq, s2.NextStatSeq);
            Assert.AreEqual(s1.Entries.Length, s2.Entries.Length);

            for (int i = 0; i < s1.Entries.Length; i++)
            {
                Assert.AreEqual(s1.Entries[i].StatId, s2.Entries[i].StatId);
                Assert.AreEqual(s1.Entries[i].FinalValue, s2.Entries[i].FinalValue);
                Assert.AreEqual(s1.Entries[i].Modifiers.Length, s2.Entries[i].Modifiers.Length);
            }
        }
    }
}

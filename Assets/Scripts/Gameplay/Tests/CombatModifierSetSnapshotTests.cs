using System.Collections.Generic;
using System;
using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class CombatModifierSetSnapshotTests
    {
        private Unit CreateUnit()
        {
            return UnitTestFactory.CreateUnit(
                new UnitUid(100, 1, 0),
                UnitKind.Hero,
                0,
                TeamId.Neutral);
        }

        private static CombatModifierRecord CreateRecord(int tick, string key)
        {
            return new CombatModifierRecord
            {
                Id = CombatModifierId.Create(tick, key),
            };
        }

        [Test]
        public void CaptureRestore_RoundTrip_PreservesAllRecords()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);

            var r1 = CreateRecord(1000, "Buff.A");
            var r2 = CreateRecord(1001, "Buff.B");
            set.Attach(r1);
            set.Attach(r2);

            CombatModifierSetSnapshot snapshot = default;
            set.Capture(ref snapshot);

            // Modify after capture
            set.Attach(CreateRecord(1002, "Buff.C"));
            set.Detach(new CombatModifierHandle(unit.UnitUid, r1.Id));

            // Restore
            set.Restore(in snapshot);

            Assert.AreEqual(2, set.Count);
            Assert.AreEqual(r1.Id, snapshot.Records[0].Id);
            Assert.AreEqual(r2.Id, snapshot.Records[1].Id);
        }

        [Test]
        public void Restore_DoesNotCallAttachDetachClear()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            set.Attach(CreateRecord(1000, "Buff.A"));
            set.Attach(CreateRecord(1001, "Buff.B"));

            CombatModifierSetSnapshot snapshot = default;
            set.Capture(ref snapshot);

            // Clear then restore — should restore without triggering side effects
            set.Clear();
            Assert.AreEqual(0, set.Count);

            set.Restore(in snapshot);
            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void RestoreAfterDetach_PreservesOriginalSet()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);

            var h1 = set.Attach(CreateRecord(1000, "A"));
            var h2 = set.Attach(CreateRecord(1001, "B"));
            var h3 = set.Attach(CreateRecord(1002, "C"));

            CombatModifierSetSnapshot snapshot = default;
            set.Capture(ref snapshot);

            // Detach middle record
            set.Detach(h2);
            Assert.AreEqual(2, set.Count);

            // Restore should bring back all 3
            set.Restore(in snapshot);
            Assert.AreEqual(3, set.Count);
        }

        [Test]
        public void RollbackReplay_Equivalence()
        {
            // Execute 3 ticks: attach 3 records, capture
            var unit1 = CreateUnit();
            var set1 = new CombatModifierSet(unit1);
            set1.Attach(CreateRecord(100, "A"));
            set1.Attach(CreateRecord(200, "B"));
            set1.Attach(CreateRecord(300, "C"));

            CombatModifierSetSnapshot snapshot = default;
            set1.Capture(ref snapshot);

            // Execute 3 more ticks
            set1.Attach(CreateRecord(400, "D"));
            set1.Detach(new CombatModifierHandle(unit1.UnitUid, CombatModifierId.Create(100, "A")));

            // Now replay from snapshot
            var unit2 = CreateUnit();
            var set2 = new CombatModifierSet(unit2);
            set2.Restore(in snapshot);

            set2.Attach(CreateRecord(400, "D"));
            set2.Detach(new CombatModifierHandle(unit2.UnitUid, CombatModifierId.Create(100, "A")));

            // Both should end up with the same set
            var out1 = new List<CombatModifierRecord>();
            var out2 = new List<CombatModifierRecord>();
            set1.Collect(out1);
            set2.Collect(out2);

            Assert.AreEqual(out1.Count, out2.Count);
            for (int i = 0; i < out1.Count; i++)
            {
                Assert.AreEqual(out1[i].Id, out2[i].Id);
            }
        }

        [Test]
        public void Determinism_SameSequence_SameSnapshot()
        {
            var unit1 = CreateUnit();
            var set1 = new CombatModifierSet(unit1);
            set1.Attach(CreateRecord(100, "A"));
            set1.Attach(CreateRecord(200, "B"));

            var unit2 = CreateUnit();
            var set2 = new CombatModifierSet(unit2);
            set2.Attach(CreateRecord(100, "A"));
            set2.Attach(CreateRecord(200, "B"));

            CombatModifierSetSnapshot s1 = default;
            CombatModifierSetSnapshot s2 = default;
            set1.Capture(ref s1);
            set2.Capture(ref s2);

            Assert.AreEqual(s1.Records.Length, s2.Records.Length);
            for (int i = 0; i < s1.Records.Length; i++)
            {
                Assert.AreEqual(s1.Records[i].Id, s2.Records[i].Id);
                Assert.AreEqual(s1.Ids[i], s2.Ids[i]);
            }
        }

        [Test]
        public void Capture_EmptySet_ProducesEmptySnapshot()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);

            CombatModifierSetSnapshot snapshot = default;
            set.Capture(ref snapshot);

            Assert.AreEqual(0, snapshot.Records.Length);
            Assert.AreEqual(0, snapshot.Ids.Length);
        }

        [Test]
        public void Restore_EmptySnapshot_ClearsSet()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            set.Attach(CreateRecord(100, "A"));

            var emptySnapshot = default(CombatModifierSetSnapshot);
            emptySnapshot.Records = Array.Empty<CombatModifierRecord>();
            emptySnapshot.Ids = Array.Empty<ulong>();

            set.Restore(in emptySnapshot);
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Capture_AfterDetach_OnlyCapturesRemaining()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            var h1 = set.Attach(CreateRecord(100, "A"));
            set.Attach(CreateRecord(200, "B"));
            set.Detach(h1);

            CombatModifierSetSnapshot snapshot = default;
            set.Capture(ref snapshot);

            Assert.AreEqual(1, snapshot.Records.Length);
            Assert.AreEqual(CombatModifierId.Create(200, "B"), snapshot.Records[0].Id);
        }

        [Test]
        public void ResolveAndRebuild_AreNoOps()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            set.Attach(CreateRecord(100, "A"));

            var ctx = new RollbackContext(1, ExecutionMode.ClientReplay);
            set.Resolve(in ctx);
            set.Rebuild(in ctx);

            // State should be unchanged
            Assert.AreEqual(1, set.Count);
        }
    }
}

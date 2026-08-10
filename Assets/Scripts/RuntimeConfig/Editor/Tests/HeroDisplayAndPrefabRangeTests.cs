using System.Collections.Generic;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor.Tests
{
    [TestFixture]
    public sealed class HeroDisplayTableSyncTests
    {
        private readonly List<Object> createdObjects =
            new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0;
                 i < createdObjects.Count;
                 i++)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(
                        createdObjects[i]);
            }
            createdObjects.Clear();
        }

        private HeroDisplayTable NewTable()
        {
            var table =
                ScriptableObject.CreateInstance<HeroDisplayTable>();
            createdObjects.Add(table);
            return table;
        }

        private static UnitPrototypeAuthoring
            Hero(int prototypeId, int prefabId, string name)
        {
            return new UnitPrototypeAuthoring
            {
                UnitPrototypeId = prototypeId,
                Name = name,
                RuntimeEntityPrefabId = prefabId,
                UnitKind = UnitKind.Hero,
            };
        }

        [Test]
        public void Sync_AutoCreatesRow_ForEachHeroPrototype()
        {
            var table = NewTable();
            var prototypes = new List<UnitPrototypeAuthoring>
            {
                Hero(1001, 1101, "Test Hero"),
            };

            bool changed =
                HeroDisplayTableSync.Sync(table, prototypes);

            Assert.That(changed, Is.True);
            Assert.That(table.Count, Is.EqualTo(1));
            Assert.That(
                table.GetEntry(0).UnitPrototypeId,
                Is.EqualTo(1001));
            Assert.That(
                table.GetEntry(0).HeroPrefabId,
                Is.EqualTo(1101));
            Assert.That(
                table.GetEntry(0).DisplayName,
                Is.EqualTo("Test Hero"));
        }

        [Test]
        public void Sync_IgnoresNonHeroPrototypes()
        {
            var table = NewTable();
            var prototypes = new List<UnitPrototypeAuthoring>
            {
                Hero(2001, 1201, "Blue Melee Minion"),
            };
            prototypes[0].UnitKind =
                UnitKind.Minion;

            HeroDisplayTableSync.Sync(table, prototypes);

            Assert.That(table.Count, Is.EqualTo(0));
        }

        [Test]
        public void Sync_IsIdempotent()
        {
            var table = NewTable();
            var prototypes = new List<UnitPrototypeAuthoring>
            {
                Hero(1001, 1101, "Test Hero"),
            };

            HeroDisplayTableSync.Sync(table, prototypes);
            bool second =
                HeroDisplayTableSync.Sync(table, prototypes);

            Assert.That(second, Is.False);
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void Sync_PreservesAvatarAndManualName()
        {
            var table = NewTable();
            var prototypes = new List<UnitPrototypeAuthoring>
            {
                Hero(1001, 1101, "Auto Name"),
            };
            HeroDisplayTableSync.Sync(table, prototypes);
            table.GetEntry(0).DisplayName =
                "Manual Name";
            var avatar = Sprite.Create(
                Texture2D.blackTexture,
                new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f));
            createdObjects.Add(avatar);
            table.GetEntry(0).Avatar = avatar;

            bool changed =
                HeroDisplayTableSync.Sync(table, prototypes);

            Assert.That(changed, Is.False);
            Assert.That(
                table.GetEntry(0).DisplayName,
                Is.EqualTo("Manual Name"));
            Assert.That(
                table.GetEntry(0).Avatar,
                Is.Not.Null);
        }

        [Test]
        public void Sync_RemapsPrefabId_WhenPrototypeChanges()
        {
            var table = NewTable();
            var prototypes = new List<UnitPrototypeAuthoring>
            {
                Hero(1001, 1101, "Test Hero"),
            };
            HeroDisplayTableSync.Sync(table, prototypes);

            prototypes[0].RuntimeEntityPrefabId =
                1102;
            bool changed =
                HeroDisplayTableSync.Sync(table, prototypes);

            Assert.That(changed, Is.True);
            Assert.That(
                table.GetEntry(0).HeroPrefabId,
                Is.EqualTo(1102));
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void Sync_RemovesRow_WhenHeroPrototypeDisappears()
        {
            var table = NewTable();
            var prototypes = new List<UnitPrototypeAuthoring>
            {
                Hero(1001, 1101, "Test Hero"),
            };
            HeroDisplayTableSync.Sync(table, prototypes);

            prototypes.Clear();
            bool changed =
                HeroDisplayTableSync.Sync(table, prototypes);

            Assert.That(changed, Is.True);
            Assert.That(table.Count, Is.EqualTo(0));
        }
    }

    [TestFixture]
    public sealed class GlobalPrefabTableRangeTests
    {
        private readonly List<Object> createdObjects =
            new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0;
                 i < createdObjects.Count;
                 i++)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(
                        createdObjects[i]);
            }
            createdObjects.Clear();
        }

        private GlobalPrefabTable NewTable()
        {
            var table =
                ScriptableObject.CreateInstance<GlobalPrefabTable>();
            createdObjects.Add(table);
            return table;
        }

        private PrefabGroup UnitGroup(
            params PrefabEntry[] entries)
        {
            return new PrefabGroup(
                PrefabKind.Unit,
                entries);
        }

        private PrefabEntry Entry(int prefabId)
        {
            var go =
                new GameObject("Prefab" + prefabId);
            createdObjects.Add(go);
            return new PrefabEntry(prefabId, go);
        }

        [Test]
        public void DefaultUnitRange_AcceptsConfiguredIds()
        {
            GlobalPrefabTable table = NewTable();
            table.ReplaceGroupsForTests(
                new List<PrefabGroup>
                {
                    UnitGroup(Entry(1001), Entry(1302)),
                });

            Assert.DoesNotThrow(
                () => table.ValidateOrThrow());
        }

        [Test]
        public void DefaultUnitRange_RejectsOutOfRangeId()
        {
            GlobalPrefabTable table = NewTable();
            table.ReplaceGroupsForTests(
                new List<PrefabGroup>
                {
                    UnitGroup(Entry(999)),
                });

            Assert.Throws<System.InvalidOperationException>(
                () => table.ValidateOrThrow());
        }

        [Test]
        public void ConfiguredRange_IsEnforced()
        {
            GlobalPrefabTable table = NewTable();
            table.ReplaceGroupsForTests(
                new List<PrefabGroup>
                {
                    UnitGroup(Entry(1101)),
                });
            table.ReplaceRangesForTests(
                new List<PrefabKindRangeConfig>
                {
                    new PrefabKindRangeConfig
                    {
                        Kind = PrefabKind.Unit,
                        IdRangeStart = 1000,
                        IdRangeEnd = 1099,
                    },
                });

            Assert.Throws<System.InvalidOperationException>(
                () => table.ValidateOrThrow());
        }

        [Test]
        public void OverlappingKindRanges_AreRejected()
        {
            GlobalPrefabTable table = NewTable();
            table.ReplaceGroupsForTests(
                new List<PrefabGroup>
                {
                    UnitGroup(Entry(1001)),
                });
            table.ReplaceRangesForTests(
                new List<PrefabKindRangeConfig>
                {
                    new PrefabKindRangeConfig
                    {
                        Kind = PrefabKind.Unit,
                        IdRangeStart = 1000,
                        IdRangeEnd = 1999,
                    },
                    new PrefabKindRangeConfig
                    {
                        Kind = PrefabKind.Projectile,
                        IdRangeStart = 1500,
                        IdRangeEnd = 2500,
                    },
                });

            Assert.Throws<System.InvalidOperationException>(
                () => table.ValidateOrThrow());
        }
    }
}

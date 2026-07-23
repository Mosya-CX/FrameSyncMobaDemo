using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics.Tests
{
    public sealed class PhysicsEntityQueryInfoTests
    {
        #region PhysicsEntityKind

        [Test]
        public void PhysicsEntityKind_HasExactlyUnitAndProjectile()
        {
            Assert.That((int)PhysicsEntityKind.Unit, Is.EqualTo(0));
            Assert.That((int)PhysicsEntityKind.Projectile, Is.EqualTo(1));
        }

        #endregion

        #region RuntimeUidQueryValue

        [Test]
        public void RuntimeUidQueryValue_StoresAllThreeComponents()
        {
            var uid = new RuntimeUidQueryValue(100, 7, 3);

            Assert.That(uid.SpawnLogicTick, Is.EqualTo(100));
            Assert.That(uid.RuntimeEntityPrefabId, Is.EqualTo(7));
            Assert.That(uid.SpawnSequenceInTick, Is.EqualTo(3));
        }

        [Test]
        public void RuntimeUidQueryValue_Equality()
        {
            var a = new RuntimeUidQueryValue(100, 7, 3);
            var b = new RuntimeUidQueryValue(100, 7, 3);
            var c = new RuntimeUidQueryValue(100, 7, 4);

            Assert.That(a == b, Is.True);
            Assert.That(a != c, Is.True);
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.Equals(c), Is.False);
        }

        [Test]
        public void RuntimeUidQueryValue_GetHashCode_Stable()
        {
            var a = new RuntimeUidQueryValue(50, 2, 1);
            var b = new RuntimeUidQueryValue(50, 2, 1);

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void RuntimeUidQueryValue_Default_IsAllZero()
        {
            var uid = default(RuntimeUidQueryValue);

            Assert.That(uid.SpawnLogicTick, Is.EqualTo(0));
            Assert.That(uid.RuntimeEntityPrefabId, Is.EqualTo(0));
            Assert.That(uid.SpawnSequenceInTick, Is.EqualTo(0));
        }

        #endregion

        #region PhysicsEntityQueryInfo

        [Test]
        public void QueryInfo_StoresAllFourFields()
        {
            var uid = new RuntimeUidQueryValue(100, 7, 3);
            var owner = new object();

            var info = new PhysicsEntityQueryInfo(uid, PhysicsEntityKind.Unit, 1, owner);

            Assert.That(info.UidSnapshot, Is.EqualTo(uid));
            Assert.That(info.Kind, Is.EqualTo(PhysicsEntityKind.Unit));
            Assert.That(info.TeamSnapshot, Is.EqualTo(1));
            Assert.That(info.Owner, Is.SameAs(owner));
        }

        [Test]
        public void QueryInfo_IsSet_ReturnsTrueWhenInitialized()
        {
            var uid = new RuntimeUidQueryValue(100, 7, 3);
            var info = new PhysicsEntityQueryInfo(uid, PhysicsEntityKind.Unit, 0, null);

            Assert.That(info.IsSet, Is.True);
        }

        [Test]
        public void QueryInfo_IsSet_ReturnsFalseWhenDefault()
        {
            var info = default(PhysicsEntityQueryInfo);

            Assert.That(info.IsSet, Is.False);
        }

        [Test]
        public void QueryInfo_CanStoreNullOwner()
        {
            var uid = new RuntimeUidQueryValue(100, 7, 3);
            var info = new PhysicsEntityQueryInfo(uid, PhysicsEntityKind.Projectile, 0, null);

            Assert.That(info.Owner, Is.Null);
            Assert.That(info.Kind, Is.EqualTo(PhysicsEntityKind.Projectile));
        }

        #endregion

        #region PhysicsEntity2D QueryInfo (PlayMode)

        [Test]
        public void PhysicsEntity2D_SetQueryInfo_StoresAndExposesReadonly()
        {
            var go = new GameObject("TestEntity");
            var entity = go.AddComponent<PhysicsEntity2D>();
            try
            {
                var uid = new RuntimeUidQueryValue(200, 5, 1);
                var owner = new object();
                var info = new PhysicsEntityQueryInfo(uid, PhysicsEntityKind.Unit, 2, owner);

                entity.SetQueryInfo(info);

                Assert.That(entity.QueryInfo.UidSnapshot, Is.EqualTo(uid));
                Assert.That(entity.QueryInfo.Kind, Is.EqualTo(PhysicsEntityKind.Unit));
                Assert.That(entity.QueryInfo.TeamSnapshot, Is.EqualTo(2));
                Assert.That(entity.QueryInfo.Owner, Is.SameAs(owner));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PhysicsEntity2D_QueryInfo_IsDefaultBeforeSet()
        {
            var go = new GameObject("TestEntity2");
            var entity = go.AddComponent<PhysicsEntity2D>();
            try
            {
                Assert.That(entity.QueryInfo.IsSet, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        #endregion
    }
}
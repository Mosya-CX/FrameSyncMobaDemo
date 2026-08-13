using System.Collections;
using System.Reflection;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine.TestTools;
using GameplayUnit = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class TowerTargetLinePresenterPlayModeTests
    {
        [UnityTest]
        public IEnumerator DisplayTarget_SwitchesWithIntent_AndStopsForDeadTarget()
        {
            var world = new UnitWorld();
            GameplayUnit tower = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(8101, 9101, UnitKind.Structure),
                new TeamId(1),
                10,
                fp.zero,
                fp.zero);
            GameplayUnit first = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(8102, 9102, UnitKind.Hero),
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            GameplayUnit second = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(8103, 9103, UnitKind.Hero),
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);

            try
            {
                tower.ApplyOrder(
                    Order.CreateAttack(first.UnitUid, false));
                AssertDisplayTarget(tower, first);

                tower.ApplyOrder(
                    Order.CreateAttack(second.UnitUid, false));
                AssertDisplayTarget(tower, second);

                world.RequestEnterDying(second);
                AssertNoDisplayTarget(tower);

                yield return null;
            }
            finally
            {
                UnitTestFactory.DestroyCreatedObjects();
            }
        }

        private static void AssertDisplayTarget(
            GameplayUnit tower,
            GameplayUnit expected)
        {
            object[] arguments = { tower, null };
            bool resolved = (bool)GetResolver().Invoke(
                null,
                arguments);
            Assert.That(resolved, Is.True);
            Assert.That(arguments[1], Is.SameAs(expected));
        }

        private static void AssertNoDisplayTarget(
            GameplayUnit tower)
        {
            object[] arguments = { tower, null };
            bool resolved = (bool)GetResolver().Invoke(
                null,
                arguments);
            Assert.That(resolved, Is.False);
            Assert.That(arguments[1], Is.Null);
        }

        private static MethodInfo GetResolver()
        {
            MethodInfo method =
                typeof(TowerTargetLinePresenter).GetMethod(
                    "TryResolveDisplayTarget",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static UnitPrototype CreatePrototype(
            int prototypeId,
            int prefabId,
            UnitKind kind)
        {
            return new UnitPrototype
            {
                UnitPrototypeId = prototypeId,
                RuntimeEntityPrefabId = prefabId,
                UnitKind = kind,
                BaseStats = UnitTestFactory.CreateDefaultPreset(),
                Loadout = HandlerLoadout.DefaultHero,
            };
        }
    }
}

using System.Collections;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.TestTools;
using GameplayUnit = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class BlightStackMarkStructurePlayModeTests
    {
        [UnityTest]
        public IEnumerator ExternalBlightOnStructure_CreatesNoPresentationMark()
        {
            var world = new UnitWorld();
            GameplayUnit attacker = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(8201, 9201, UnitKind.Hero),
                new TeamId(1),
                10,
                fp.zero,
                fp.zero);
            GameplayUnit structure = UnitTestFactory.SpawnUnit(
                world,
                CreatePrototype(8202, 9202, UnitKind.Structure),
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            var presenterRoot = new GameObject(
                "BlightStructurePresenterProbe");
            var markPrefab = new GameObject(
                "BlightStructureMarkProbe");
            BuffDefinition blight =
                ScriptableObject.CreateInstance<BuffDefinition>();
            blight.ConfigId = new BuffConfigId(9001);
            blight.Display = new BuffDisplayInfo
            {
                Name = "Blight",
            };
            blight.Life = new BuffLifeRuleConfig
            {
                DurationSeconds = 1f,
            };
            blight.Stack = new BuffStackRuleConfig
            {
                MaxStacks = 3,
                AddMode = BuffAddMode.Add,
                ReduceMode = BuffReduceMode.Reduce,
            };

            try
            {
                Assert.That(
                    structure.BuffHandler.Apply(
                        blight.ConfigId,
                        blight,
                        attacker.UnitUid),
                    Is.False);

                BlightStackMarkPresenter presenter =
                    presenterRoot.AddComponent<
                        BlightStackMarkPresenter>();
                presenter.Initialize(
                    markPrefab,
                    () => new[] { structure });

                yield return null;

                Assert.That(
                    presenterRoot.transform.childCount,
                    Is.Zero,
                    "A rejected structure Blight must not create a client mark.");
            }
            finally
            {
                Object.DestroyImmediate(presenterRoot);
                Object.DestroyImmediate(markPrefab);
                Object.DestroyImmediate(blight);
                UnitTestFactory.DestroyCreatedObjects();
            }
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

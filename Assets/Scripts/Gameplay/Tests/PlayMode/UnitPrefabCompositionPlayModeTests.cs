using System.Collections;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Unit.PlayModeTests
{
    public sealed class UnitPrefabCompositionPlayModeTests
    {
        [UnityTest]
        public IEnumerator FormalSpawn_InstantiatesLiveComponentGraphAndRegistersPhysicsIdentity()
        {
            GameObject template = CreateTemplate();
            GlobalPrefabTable prefabTable = ScriptableObject.CreateInstance<GlobalPrefabTable>();
            prefabTable.ReplaceGroupsForTests(new[]
            {
                new PrefabGroup(PrefabKind.Unit, new[] { new PrefabEntry(9, template, 4) }),
            });

            var prototype = new UnitPrototype
            {
                UnitPrototypeId = 4,
                RuntimeEntityPrefabId = 9,
                UnitKind = UnitKind.Minion,
                BaseStats = CreatePreset(),
            };
            var prototypes = new GlobalUnitPrototypeTable();
            prototypes.Add(prototype);
            var physicsWorld = new PhysicsWorld();
            var world = new UnitWorld
            {
                UnitPrototypeTable = prototypes,
                GlobalPrefabTable = prefabTable,
                StatDefinitionTable = CreateStatTable(),
                PhysicsWorld = physicsWorld,
                TickRate = 30,
            };

            var controller = new SimulationTickContextController();
            controller.BeginTick(20, ExecutionMode.ServerAuthority);
            Unit spawned = null;
            try
            {
                UnitUid uid = world.SpawnUnit(new UnitSpawnRequest(
                    4,
                    GameplayParticipantId.Explicit(4),
                    new TeamId(6),
                    new fp2(3, 8),
                    new fp2(fp.one, fp.zero)));
                Assert.IsTrue(world.TryGetUnit(uid, out spawned));
                Assert.AreEqual(1, physicsWorld.UnitEntities.Count);
                Assert.AreSame(spawned, physicsWorld.UnitEntities[0].QueryInfo.Owner);
                Assert.AreEqual((byte)6, physicsWorld.UnitEntities[0].QueryInfo.TeamSnapshot);
                Assert.AreEqual(uid.SpawnLogicTick, physicsWorld.UnitEntities[0].QueryInfo.UidSnapshot.SpawnLogicTick);
                Assert.AreSame(spawned, spawned.AttackHandler.Owner);
            }
            finally
            {
                controller.EndTick();
                if (spawned != null) Object.Destroy(spawned.gameObject);
                Object.Destroy(template);
                Object.Destroy(prefabTable);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FormalDeathInvalidation_UpdatesAttackOwnersTogether()
        {
            GameObject template = CreateTemplate();
            GlobalPrefabTable prefabTable =
                ScriptableObject.CreateInstance<GlobalPrefabTable>();
            prefabTable.ReplaceGroupsForTests(new[]
            {
                new PrefabGroup(PrefabKind.Unit, new[]
                {
                    new PrefabEntry(1098, template, 98),
                    new PrefabEntry(1099, template, 99),
                }),
            });

            var prototypes = new GlobalUnitPrototypeTable();
            prototypes.Add(new UnitPrototype
            {
                UnitPrototypeId = 98,
                RuntimeEntityPrefabId = 1098,
                UnitKind = UnitKind.Hero,
                BaseStats = CreatePreset(),
                Loadout = HandlerLoadout.DefaultHero,
            });
            prototypes.Add(new UnitPrototype
            {
                UnitPrototypeId = 99,
                RuntimeEntityPrefabId = 1099,
                UnitKind = UnitKind.Hero,
                BaseStats = CreatePreset(),
                Loadout = HandlerLoadout.DefaultHero,
            });
            var world = new UnitWorld
            {
                UnitPrototypeTable = prototypes,
                GlobalPrefabTable = prefabTable,
                StatDefinitionTable = CreateStatTable(),
                PhysicsWorld = new PhysicsWorld(),
                TickRate = 30,
            };

            var controller = new SimulationTickContextController();
            Unit attacker = null;
            Unit target = null;
            controller.BeginTick(20, ExecutionMode.ServerAuthority);
            try
            {
                UnitUid attackerUid = world.SpawnUnit(new UnitSpawnRequest(
                    98,
                    GameplayParticipantId.Explicit(98),
                    new TeamId(1),
                    fp2.zero,
                    new fp2(fp.one, fp.zero)));
                UnitUid targetUid = world.SpawnUnit(new UnitSpawnRequest(
                    99,
                    GameplayParticipantId.Explicit(99),
                    new TeamId(2),
                    fp2.zero,
                    new fp2(-fp.one, fp.zero)));
                Assert.That(world.TryGetUnit(attackerUid, out attacker),
                    Is.True);
                Assert.That(world.TryGetUnit(targetUid, out target),
                    Is.True);
                Assert.That(attacker.Arbiter.Submit(
                        new AttackActionRequest(targetUid)).IsGranted,
                    Is.True);

                world.RequestEnterDying(target);
                world.ConfirmUnitDeath(target);
                world.ApplyFormalDeathActionInvalidations(new[]
                {
                    new DeathResult
                    {
                        VictimUid = targetUid,
                        DeathSequenceInTick = 0,
                        DeathLogicTick = 20,
                    },
                });

                Assert.That(
                    attacker.AttackHandler.CurrentTargetUid.IsValid(),
                    Is.False);
                Assert.That(
                    attacker.ActionRuntimes.Main.IsOccupied,
                    Is.False);
            }
            finally
            {
                controller.EndTick();
                if (attacker != null) Object.Destroy(attacker.gameObject);
                if (target != null) Object.Destroy(target.gameObject);
                Object.Destroy(template);
                Object.Destroy(prefabTable);
            }

            yield return null;
        }

        private static GameObject CreateTemplate()
        {
            var root = new GameObject("TestUnitPrefab");
            root.AddComponent<PhysicsEntity2D>();
            root.AddComponent<StatHandler>();
            root.AddComponent<MovementHandler>();
            root.AddComponent<AttackHandler>();
            root.AddComponent<AbilityHandler>();
            root.AddComponent<BuffHandler>();
            root.AddComponent<CrowdControlHandler>();
            root.AddComponent<EquipmentHandler>();
            root.AddComponent<Unit>();
            return root;
        }

        private static StatPreset CreatePreset()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = 100,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = 100,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackSpeed,
                BaseValue = 30,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackRange,
                BaseValue = 200,
            });
            return preset;
        }

        private static StatDefinitionTable CreateStatTable()
        {
            var table = new StatDefinitionTable();
            table.Add(new StatDefinition
            {
                Id = StatId.MaxHealth,
                DebugName = "MaxHealth",
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.AttackDamage,
                DebugName = "AttackDamage",
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.AttackSpeed,
                DebugName = "AttackSpeed",
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.AttackRange,
                DebugName = "AttackRange",
                SupportsLevelGrowth = true,
            });
            return table;
        }
    }
}

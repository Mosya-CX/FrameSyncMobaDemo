using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.ClientContent.Tests
{
    /// <summary>
    /// Guards the projectile-view lease lifecycle: the view asset must stay
    /// resident after a short-lived projectile dies so the next missile does
    /// not race an Addressables reload (previously only occasional missiles
    /// rendered a view).
    /// </summary>
    public sealed class ProjectileViewBinderPlayModeTests
    {
        private const int AttackProjectileDefId = 101;

        [UnityTest]
        public IEnumerator ProjectileViewLeaseStaysResidentAcrossLifetimes()
        {
            var service = new AddressablesClientContentService();
            ClientProjectileViewBinder binder = null;
            GlobalPrefabTable runtimeTable = null;
            try
            {
                Task init = service.InitializeAsync(
                    CancellationToken.None);
                yield return WaitFor(init);
                Assert.That(init.Exception, Is.Null);

                GlobalPrefabTable rootTable =
                    AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(
                        "Assets/Config/Formal/GlobalPrefabTable.asset");
                Assert.That(rootTable, Is.Not.Null);
                GlobalPrefabSubTableAsset[] subTables =
                {
                    AssetDatabase.LoadAssetAtPath<GlobalPrefabSubTableAsset>(
                        "Assets/Config/Formal/MatchContent/CoreGlobalPrefabSubTable.asset"),
                    AssetDatabase.LoadAssetAtPath<GlobalPrefabSubTableAsset>(
                        "Assets/Config/Formal/MatchContent/VarusGlobalPrefabSubTable.asset"),
                    AssetDatabase.LoadAssetAtPath<GlobalPrefabSubTableAsset>(
                        "Assets/Config/Formal/MatchContent/AatroxGlobalPrefabSubTable.asset"),
                };
                var resolvedPrefabs =
                    new Dictionary<string, GameObject>();
                for (int tableIndex = 0;
                     tableIndex < subTables.Length;
                     tableIndex++)
                {
                    Assert.That(subTables[tableIndex], Is.Not.Null);
                    IReadOnlyList<PrefabGroup> groups =
                        subTables[tableIndex].PrefabGroups;
                    for (int groupIndex = 0;
                         groupIndex < groups.Count;
                         groupIndex++)
                    {
                        IReadOnlyList<PrefabEntry> entries =
                            groups[groupIndex].Entries;
                        for (int entryIndex = 0;
                             entryIndex < entries.Count;
                             entryIndex++)
                        {
                            string address =
                                entries[entryIndex].LogicAssetAddress;
                            if (string.IsNullOrEmpty(address) ||
                                resolvedPrefabs.ContainsKey(address))
                            {
                                continue;
                            }
                            GameObject prefab =
                                AssetDatabase.LoadAssetAtPath<GameObject>(address);
                            Assert.That(prefab, Is.Not.Null, address);
                            resolvedPrefabs.Add(address, prefab);
                        }
                    }
                }
                runtimeTable = rootTable.CreateResolvedRuntimeTable(
                    subTables,
                    resolvedPrefabs);
                ProjectileRuntimeCatalogAsset catalog =
                    AssetDatabase.LoadAssetAtPath<
                        ProjectileRuntimeCatalogAsset>(
                        "Assets/Config/Formal/FullMatchProjectileRuntimeCatalog.asset");
                Assert.That(catalog, Is.Not.Null);

                var projectileWorld = new ProjectileWorld
                {
                    DefRegistry =
                        catalog.BakeOrThrow(runtimeTable),
                    PhysicsWorld =
                        new PhysicsWorld
                        {
                            Settings =
                                new PhysicsWorldSettings
                                {
                                    GridCellSize =
                                        (fp)1m,
                                },
                        },
                    PrefabTable = runtimeTable,
                    LogicSecondsPerTick = fp.one,
                };
                binder = new ClientProjectileViewBinder(
                    projectileWorld,
                    runtimeTable,
                    service);

                ProjectileUid first = SpawnProjectile(
                    projectileWorld,
                    runtimeTable);
                binder.Reconcile();
                yield return WaitForViews(1);
                Assert.That(
                    binder.BindingCount,
                    Is.EqualTo(1),
                    "The live projectile must own a view binding.");

                EndProjectile(
                    projectileWorld,
                    first);
                binder.Reconcile();
                yield return null;
                Assert.That(
                    binder.BindingCount,
                    Is.EqualTo(0),
                    "The dead projectile's view binding must be removed.");
                Assert.That(
                    GetLeaseCount(binder),
                    Is.EqualTo(1),
                    "The projectile view asset must stay cached after the " +
                    "projectile dies so the next missile is instant.");

                SpawnProjectile(
                    projectileWorld,
                    runtimeTable);
                binder.Reconcile();
                yield return WaitForViews(1);
                Assert.That(
                    binder.BindingCount,
                    Is.EqualTo(1),
                    "A second projectile must rebind from the cached asset.");
                Assert.That(
                    GetLeaseCount(binder),
                    Is.EqualTo(1),
                    "No new Addressables lease may be opened for the same view.");
            }
            finally
            {
                binder?.Dispose();
                service.Dispose();
                if (runtimeTable != null)
                    Object.Destroy(runtimeTable);
            }
        }

        private static ProjectileUid SpawnProjectile(
            ProjectileWorld projectileWorld,
            GlobalPrefabTable table)
        {
            var ownerUid = new UnitUid(
                100,
                1001,
                0);
            var source = new SourceDescriptor
            {
                SourceType = CombatSourceType.Attack,
                SourceId = CombatBuiltinSourceId.BasicAttack,
                OwnerUnitUid = ownerUid,
                EmitterUnitUid = ownerUid,
            };
            var tickController =
                new SimulationTickContextController();
            tickController.BeginTick(
                100,
                ExecutionMode.ServerAuthority);
            try
            {
                ProjectileUid uid =
                    projectileWorld.RequestSpawn(
                        new ProjectileSpawnRequest(
                            AttackProjectileDefId,
                            ownerUid,
                            new TeamId(1),
                            source,
                            new OriginActionId(
                                GameplayParticipantId.Explicit(1),
                                CombatSourceType.Attack,
                                CombatBuiltinSourceId.BasicAttack,
                                100,
                                0),
                            fp2.zero,
                            new fp2(
                                fp.one,
                                fp.zero)));
                projectileWorld.CommitSpawns();
                return uid;
            }
            finally
            {
                tickController.EndTick();
            }
        }

        private static void EndProjectile(
            ProjectileWorld projectileWorld,
            ProjectileUid uid)
        {
            var tickController =
                new SimulationTickContextController();
            tickController.BeginTick(
                101,
                ExecutionMode.ServerAuthority);
            try
            {
                projectileWorld.RequestEnd(
                    uid,
                    ProjectileEndReason.LifetimeExpired);
                projectileWorld.FlushDestroy();
            }
            finally
            {
                tickController.EndTick();
            }
        }

        private static IEnumerator WaitForViews(
            int count)
        {
            int guard = 0;
            while (CountClientViews() < count &&
                   guard++ < 600)
            {
                yield return null;
            }
        }

        private static int CountClientViews()
        {
            int count = 0;
            foreach (Transform t in
                     Object.FindObjectsOfType<Transform>(true))
            {
                if (t.name.StartsWith(
                        "ClientView_"))
                {
                    count++;
                }
            }
            return count;
        }

        private static int GetLeaseCount(
            ClientProjectileViewBinder binder)
        {
            FieldInfo field =
                typeof(ClientProjectileViewBinder)
                    .GetField(
                        "addressLeases",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var leases =
                field.GetValue(binder) as
                    System.Collections.IDictionary;
            Assert.That(leases, Is.Not.Null);
            return leases.Count;
        }

        private static IEnumerator WaitFor(
            Task task)
        {
            while (!task.IsCompleted)
                yield return null;
        }
    }
}

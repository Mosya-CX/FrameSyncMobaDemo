using System.Collections;
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
            try
            {
                Task init = service.InitializeAsync(
                    CancellationToken.None);
                yield return WaitFor(init);
                Assert.That(init.Exception, Is.Null);

                GlobalPrefabTable table =
                    AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(
                        "Assets/Config/Formal/GlobalPrefabTable.asset");
                Assert.That(table, Is.Not.Null);
                ProjectileRuntimeCatalogAsset catalog =
                    AssetDatabase.LoadAssetAtPath<
                        ProjectileRuntimeCatalogAsset>(
                        "Assets/Config/Formal/FullMatchProjectileRuntimeCatalog.asset");
                Assert.That(catalog, Is.Not.Null);

                var projectileWorld = new ProjectileWorld
                {
                    DefRegistry =
                        catalog.BakeOrThrow(table),
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
                    PrefabTable = table,
                    LogicSecondsPerTick = fp.one,
                };
                binder = new ClientProjectileViewBinder(
                    projectileWorld,
                    table,
                    service);

                ProjectileUid first = SpawnProjectile(
                    projectileWorld,
                    table);
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
                    table);
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

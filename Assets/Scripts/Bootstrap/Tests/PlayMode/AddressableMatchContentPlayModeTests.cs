using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class AddressableMatchContentPlayModeTests
    {
        [UnityTest]
        public IEnumerator VarusSelection_LoadsOnlyVarusAndReleasesEveryHandle()
        {
            GlobalPrefabTable root = AssetDatabase.LoadAssetAtPath<
                GlobalPrefabTable>(
                "Assets/Config/Formal/GlobalPrefabTable.asset");
            Assert.That(root, Is.Not.Null);
            Task<AddressableMatchContentScope> task =
                AddressableMatchContentService.LoadAsync(
                    root,
                    new MatchContentSelection(1, new[] { 1001 }),
                    CancellationToken.None);
            yield return Wait(task);
            AddressableMatchContentScope scope = task.Result;
            Assert.That(scope.SubTables.Count, Is.EqualTo(3));
            Assert.That(scope.UnitCatalogs.Count, Is.EqualTo(2));
            Assert.That(scope.AbilityCatalogs.Count, Is.EqualTo(1));
            Assert.That(scope.ProjectileCatalogs.Count, Is.EqualTo(2));
            Assert.That(scope.BuffCatalogs.Count, Is.EqualTo(2));
            Assert.That(
                scope.PrefabTable.TryGetEntry(
                    PrefabKind.Unit,
                    1101,
                    out _),
                Is.True);
            Assert.That(
                scope.PrefabTable.TryGetEntry(
                    PrefabKind.Unit,
                    1102,
                    out _),
                Is.False);
            Assert.That(
                scope.PrefabTable.TryGetEntry(
                    PrefabKind.ParticleVfx,
                    3101,
                    out _),
                Is.False,
                "Varus-only content must exclude Aatrox Q VFX metadata.");
            Assert.That(scope.OwnedHandleCount, Is.GreaterThan(0));
            scope.Dispose();
            scope.Dispose();
            Assert.That(scope.OwnedHandleCount, Is.Zero);
            Assert.That(
                AddressableMatchContentScope.ActiveScopeCount,
                Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AatroxSelection_LoadsItsQMetadataWithoutVarus()
        {
            GlobalPrefabTable root = AssetDatabase.LoadAssetAtPath<
                GlobalPrefabTable>(
                "Assets/Config/Formal/GlobalPrefabTable.asset");
            Task<AddressableMatchContentScope> task =
                AddressableMatchContentService.LoadAsync(
                    root,
                    new MatchContentSelection(1, new[] { 1002 }),
                    CancellationToken.None);
            yield return Wait(task);
            AddressableMatchContentScope scope = task.Result;
            try
            {
                Assert.That(
                    scope.PrefabTable.TryGetEntry(
                        PrefabKind.Unit,
                        1101,
                        out _),
                    Is.False);
                Assert.That(
                    scope.PrefabTable.TryGetEntry(
                        PrefabKind.ParticleVfx,
                        3101,
                        out PrefabEntry q1),
                    Is.True);
                Assert.That(q1.ClientViewAddress, Is.EqualTo("vfx/3101"));
            }
            finally
            {
                scope.Dispose();
            }
            Assert.That(
                AddressableMatchContentScope.ActiveScopeCount,
                Is.Zero);
        }

        private static IEnumerator Wait(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                throw task.Exception?.InnerException ?? task.Exception;
            if (task.IsCanceled)
                throw new System.OperationCanceledException();
        }
    }
}

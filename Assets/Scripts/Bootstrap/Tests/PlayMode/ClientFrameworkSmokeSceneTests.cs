using System.Collections;
using NUnit.Framework;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.Unit;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class ClientFrameworkSmokeSceneTests
    {
        [UnityTest]
        public IEnumerator ClientFixture_BindsAssignedUnitAndAdvances()
        {
            AsyncOperation load =
                SceneManager.LoadSceneAsync(
                    "ClientFrameworkSmoke",
                    LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            Scene scene =
                SceneManager.GetSceneByName(
                    "ClientFrameworkSmoke");
            Assert.That(
                scene.IsValid() && scene.isLoaded,
                Is.True);
            GameBootstrap bootstrap = null;
            GameObject[] roots =
                scene.GetRootGameObjects();
            for (int i = 0;
                 i < roots.Length &&
                 bootstrap == null;
                 i++)
                bootstrap =
                    roots[i]
                        .GetComponentInChildren<GameBootstrap>(
                            true);

            Assert.That(bootstrap, Is.Not.Null);
            yield return null;

            Assert.That(
                bootstrap.IsMatchReady,
                Is.True);
            Assert.That(
                bootstrap.IsLocalPlayerBound,
                Is.True);
            Assert.That(
                bootstrap.LocalPlayerSlot,
                Is.EqualTo(0));
            Assert.That(
                bootstrap.LocalControlledUnitUid.IsValid(),
                Is.True);
            Assert.That(
                bootstrap.Runtime.CurrentTick,
                Is.GreaterThanOrEqualTo(0));

            PlayerInputController input =
                bootstrap.GetComponentInChildren<
                    PlayerInputController>(true);
            Assert.That(input, Is.Not.Null);
            Assert.That(
                input.CommandRequester.ControlledUnit.UnitUid,
                Is.EqualTo(
                    bootstrap.LocalControlledUnitUid));
            UnitType target = null;
            var units =
                bootstrap.UnitWorld.GetAllUnits();
            for (int i = 0;
                 i < units.Count;
                 i++)
                if (units[i].TeamId !=
                    input.CommandRequester
                        .ControlledUnit.TeamId &&
                    units[i].UnitKind ==
                    UnitKind.Hero)
                {
                    target = units[i];
                    break;
                }
            Assert.That(target, Is.Not.Null);
            Assert.That(
                input.CommandRequester.RequestAttack(
                    target.UnitUid),
                Is.True);
            Assert.That(
                input.CommandRequester.RequestCastAbility(
                    0,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None,
                    out GameplayCommandRequestReceipt receipt),
                Is.True);
            Assert.That(
                receipt.CommandSeq,
                Is.GreaterThan(0));
            Assert.That(
                bootstrap.Runtime.CommandCollector
                    .GetCanonicalCommands().Count,
                Is.EqualTo(2));

            AsyncOperation unload =
                SceneManager.UnloadSceneAsync(scene);
            if (unload != null)
                yield return unload;
        }
    }
}

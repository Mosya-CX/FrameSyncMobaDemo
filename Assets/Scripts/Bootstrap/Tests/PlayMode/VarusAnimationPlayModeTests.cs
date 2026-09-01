using System.Collections;
using System.Reflection;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using GameplayUnit = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class VarusAnimationPlayModeTests
    {
        [UnityTest]
        public IEnumerator BoundDriver_QFocusMovementChangesResolveLoopSameFrame()
        {
            GameObject logicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Config/Formal/Prefabs/Logic/Unit/VarusRuntime.prefab");
            GameObject viewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ClientContent/Views/Unit/VarusRuntimeView.prefab");
            AbilityAsset qAsset = AssetDatabase.LoadAssetAtPath<AbilityAsset>(
                "Assets/Config/Formal/Abilities/VarusQ.asset");
            Assert.That(logicPrefab, Is.Not.Null);
            Assert.That(viewPrefab, Is.Not.Null);
            Assert.That(qAsset, Is.Not.Null);

            GameObject logic = Object.Instantiate(logicPrefab);
            GameObject view = Object.Instantiate(viewPrefab);
            var world = new UnitWorld { TickRate = 30 };
            try
            {
                GameplayUnit unit = logic.GetComponent<GameplayUnit>();
                AbilityHandler abilityHandler = unit.AbilityHandler;
                MovementHandler movement = unit.MovementHandler;
                UnitPresentationHost host =
                    view.GetComponent<UnitPresentationHost>();
                UnitAnimationDriver driver =
                    view.GetComponent<UnitAnimationDriver>();
                Animator animator =
                    view.GetComponentInChildren<Animator>(true);
                Assert.That(unit, Is.Not.Null);
                Assert.That(abilityHandler, Is.Not.Null);
                Assert.That(movement, Is.Not.Null);
                Assert.That(host, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);
                Assert.That(animator, Is.Not.Null);

                typeof(GameplayUnit).GetProperty(
                        nameof(GameplayUnit.World),
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    ?.SetValue(unit, world);
                host.Bind(unit);
                movement.SetMoveSpeed((fp)5);

                FieldInfo movingField = typeof(MovementHandler).GetField(
                    "_isMoving",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo bookField = typeof(AbilityHandler).GetField(
                    "_book",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo captureActiveCasts =
                    typeof(AbilityHandler).GetMethod(
                        "CaptureActiveCasts",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo driverUpdate = typeof(UnitAnimationDriver).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(movingField, Is.Not.Null);
                Assert.That(bookField, Is.Not.Null);
                Assert.That(captureActiveCasts, Is.Not.Null);
                Assert.That(driverUpdate, Is.Not.Null);

                animator.Rebind();
                animator.Update(0f);
                movingField.SetValue(movement, true);
                PublishAndDrive(world, driver, driverUpdate, 9, .25d);
                Assert.That(
                    IsCurrentOrNext(animator, "Base Layer.Move"),
                    Is.True);

                AbilityDef qDefinition = qAsset.Bake(world.TickRate);
                var qRuntime = new AbilityRuntime
                {
                    Definition = qDefinition,
                    Level = 1,
                    World = world,
                    CasterUnitUid = unit.UnitUid,
                };
                AbilitySession qSession = qRuntime.BeginSession(
                    1,
                    9,
                    default);
                qSession.CurrentStageKey = 1;
                qSession.StageElapsedTicks = 3;
                var qSlot = new AbilitySlotRuntime
                {
                    SlotIndex = 1,
                    ActiveAbilityId = qDefinition.AbilityId,
                };
                qSlot.AddAbility(qRuntime);
                ((AbilityBook)bookField.GetValue(abilityHandler)).AddSlot(qSlot);
                captureActiveCasts.Invoke(abilityHandler, null);

                PublishAndDrive(world, driver, driverUpdate, 9, .35d);
                Advance(animator, 6);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).IsName(
                        "Base Layer.VarusSpellQChanneling_Walk"),
                    Is.True,
                    "A moving Q Focus session must enter its walk loop.");

                movingField.SetValue(movement, false);
                PublishAndDrive(world, driver, driverUpdate, 9, .55d);
                Assert.That(
                    IsCurrentOrNext(animator, "Base Layer.CastQFocus"),
                    Is.True,
                    "Stopping during Q Focus must resolve the idle channel " +
                    "route in the same presentation update.");
                Advance(animator, 6);

                movingField.SetValue(movement, true);
                PublishAndDrive(world, driver, driverUpdate, 9, .75d);
                Assert.That(
                    IsCurrentOrNext(
                        animator,
                        "Base Layer.VarusSpellQChanneling_Walk"),
                    Is.True,
                    "Restarting movement during Q Focus must resolve the walk " +
                    "loop in the same presentation update.");

                float firstLoopTime = animator.GetFloat("LoopMotionTime");
                PublishAndDrive(world, driver, driverUpdate, 10, .35d);
                animator.Update(.05f);
                Assert.That(
                    animator.GetFloat("LoopMotionTime"),
                    Is.Not.EqualTo(firstLoopTime).Within(.0001f),
                    "The active Q movement loop must continue sampling the " +
                    "presentation clock instead of freezing on route changes.");
            }
            finally
            {
                AnimationPresentationClock.Clear(world);
                Object.Destroy(view);
                Object.Destroy(logic);
            }
            yield return null;
        }

        private static void PublishAndDrive(
            UnitWorld world,
            UnitAnimationDriver driver,
            MethodInfo driverUpdate,
            int logicTick,
            double fractionalTick)
        {
            AnimationPresentationClock.Publish(
                world,
                logicTick,
                world.TickRate,
                fractionalTick);
            driverUpdate.Invoke(driver, null);
        }

        private static bool IsCurrentOrNext(
            Animator animator,
            string stateName)
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName) ||
                   animator.IsInTransition(0) &&
                   animator.GetNextAnimatorStateInfo(0).IsName(stateName);
        }

        private static void Advance(Animator animator, int steps)
        {
            for (int i = 0; i < steps; i++)
                animator.Update(.05f);
        }
    }
}

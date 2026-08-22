using System.Collections.Generic;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.PlayerInput.Tests
{
    [TestFixture]
    public sealed class AbilityInputMappingTests
    {
        private static InputBinding Expect(
            InputMappingTemplate template,
            InputTrigger trigger)
        {
            Assert.That(
                template.TryGet(
                    trigger,
                    out InputBinding binding),
                Is.True,
                $"Template must bind {trigger}.");
            return binding;
        }

        private static void AssertTemplateEquals(
            InputMappingTemplate actual,
            InputMappingTemplate expected)
        {
            Assert.That(
                actual.Bindings.Count,
                Is.EqualTo(expected.Bindings.Count));
            for (int i = 0;
                 i < expected.Bindings.Count;
                 i++)
            {
                InputBinding e = expected.Bindings[i];
                Assert.That(
                    actual.TryGet(
                        e.Trigger,
                        out InputBinding a),
                    Is.True);
                Assert.That(
                    a.Translation,
                    Is.EqualTo(e.Translation));
                Assert.That(
                    a.CaptureAim,
                    Is.EqualTo(e.CaptureAim));
            }
        }

        [Test]
        public void HoldReleaseDefault_PressFocus_LeftCommit_ReleaseNone()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildDefault(
                    new HoldReleaseCastModelDef(),
                    AimKind.Direction);

            Assert.That(
                Expect(template, InputTrigger.AbilityKeyPressed)
                    .Translation,
                Is.EqualTo(InputTranslation.Focus));
            Assert.That(
                Expect(template, InputTrigger.PrimaryClick)
                    .Translation,
                Is.EqualTo(InputTranslation.Commit));
            Assert.That(
                Expect(template, InputTrigger.PrimaryClick)
                    .CaptureAim,
                Is.True);
            Assert.That(
                Expect(template, InputTrigger.AbilityKeyReleased)
                    .Translation,
                Is.EqualTo(InputTranslation.None));
            Assert.That(
                Expect(template, InputTrigger.SecondaryClick)
                    .Translation,
                Is.EqualTo(InputTranslation.None));
            Assert.That(
                Expect(template, InputTrigger.Cancel)
                    .Translation,
                Is.EqualTo(InputTranslation.None));
            Assert.That(
                AbilityInputMapping.Validate(
                    template,
                    AimKind.Direction,
                    requiresCommitSource: true),
                Is.Empty);
        }

        [Test]
        public void Channel_DefaultsLikeHoldRelease()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildDefault(
                    new ChannelCastModelDef(),
                    AimKind.None);

            Assert.That(
                Expect(template, InputTrigger.AbilityKeyPressed)
                    .Translation,
                Is.EqualTo(InputTranslation.Focus));
            Assert.That(
                Expect(template, InputTrigger.AbilityKeyReleased)
                    .Translation,
                Is.EqualTo(InputTranslation.None));
        }

        [Test]
        public void CommitNoAim_ReturnsPressCommit()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildDefault(
                    new CommitCastModelDef(),
                    AimKind.None);

            Assert.That(
                Expect(template, InputTrigger.AbilityKeyPressed)
                    .Translation,
                Is.EqualTo(InputTranslation.Commit));
            Assert.That(
                template.TryGet(
                    InputTrigger.AbilityKeyReleased,
                    out _),
                Is.False,
                "Unbound events must be absent from the template (no action).");
        }

        [Test]
        public void ToggleNoAim_ReturnsImmediatePressCommit()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildDefault(
                    new ToggleCastModelDef(),
                    AimKind.None);

            InputBinding pressed = Expect(
                template,
                InputTrigger.AbilityKeyPressed);
            Assert.That(
                pressed.Translation,
                Is.EqualTo(InputTranslation.Commit));
            Assert.That(pressed.CaptureAim, Is.False);
            Assert.That(
                template.TryGet(
                    InputTrigger.PrimaryClick,
                    out _),
                Is.False,
                "A no-aim Toggle must not wait for a mouse click.");
        }

        [Test]
        public void CommitWithAim_ReturnsLocalAim()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildDefault(
                    new CommitCastModelDef(),
                    AimKind.Point);

            Assert.That(
                Expect(template, InputTrigger.AbilityKeyPressed)
                    .Translation,
                Is.EqualTo(InputTranslation.LocalAimOnly));
            Assert.That(
                Expect(template, InputTrigger.PrimaryClick)
                    .Translation,
                Is.EqualTo(InputTranslation.Commit));
            Assert.That(
                Expect(template, InputTrigger.SecondaryClick)
                    .Translation,
                Is.EqualTo(InputTranslation.CancelLocalAim));
            Assert.That(
                Expect(template, InputTrigger.Cancel)
                    .Translation,
                Is.EqualTo(InputTranslation.CancelLocalAim));
        }

        [Test]
        public void CommitWithSelfAim_ReturnsPressCommit()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildDefault(
                    new CommitCastModelDef(),
                    AimKind.Self);

            Assert.That(
                Expect(template, InputTrigger.AbilityKeyPressed)
                    .Translation,
                Is.EqualTo(InputTranslation.Commit));
        }

        [Test]
        public void ActiveSignal_ReturnsPressCommit()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildDefault(
                    new ActiveSignalCastModelDef(),
                    AimKind.None);

            Assert.That(
                Expect(template, InputTrigger.AbilityKeyPressed)
                    .Translation,
                Is.EqualTo(InputTranslation.Commit));
        }

        [Test]
        public void NullCastModel_ReturnsPressCommit()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildDefault(
                    null,
                    AimKind.None);

            Assert.That(
                Expect(template, InputTrigger.AbilityKeyPressed)
                    .Translation,
                Is.EqualTo(InputTranslation.Commit));
        }

        [Test]
        public void Template_DuplicateTrigger_Throws()
        {
            Assert.That(
                () => new InputMappingTemplate(new[]
                {
                    new InputBinding(
                        InputTrigger.PrimaryClick,
                        InputTranslation.Commit),
                    new InputBinding(
                        InputTrigger.PrimaryClick,
                        InputTranslation.Focus),
                }),
                Throws.ArgumentException);
        }

        [Test]
        public void Template_TryGet_UnknownTrigger_ReturnsFalse()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildHoldReleaseDefault();

            Assert.That(
                template.TryGet(
                    (InputTrigger)99,
                    out _),
                Is.False);
        }

        [Test]
        public void Validate_HoldReleaseWithoutCommit_ReportsError()
        {
            var template = new InputMappingTemplate(new[]
            {
                new InputBinding(
                    InputTrigger.AbilityKeyPressed,
                    InputTranslation.Focus),
                new InputBinding(
                    InputTrigger.AbilityKeyReleased,
                    InputTranslation.None),
            });

            IReadOnlyList<string> errors =
                AbilityInputMapping.Validate(
                    template,
                    AimKind.None,
                    requiresCommitSource: true);

            Assert.That(
                errors,
                Does.Contain(
                    "Hold/guide templates must contain at least one Commit source."));
        }

        [Test]
        public void Validate_CommitCaptureAimWithoutAimableKind_ReportsError()
        {
            var template = new InputMappingTemplate(new[]
            {
                new InputBinding(
                    InputTrigger.PrimaryClick,
                    InputTranslation.Commit,
                    captureAim: true),
            });

            IReadOnlyList<string> errors =
                AbilityInputMapping.Validate(
                    template,
                    AimKind.Self,
                    requiresCommitSource: true);

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Provider_TryGetTemplate_ValidSlot_ReturnsTemplate()
        {
            var templates =
                new InputMappingTemplate[4];
            templates[2] =
                AbilityInputMapping
                    .BuildHoldReleaseDefault();
            var provider =
                new AbilityInputMappingProvider(
                    templates);

            bool found =
                provider.TryGetTemplate(
                    2,
                    out InputMappingTemplate template);
            Assert.That(found, Is.True);
            AssertTemplateEquals(
                template,
                AbilityInputMapping
                    .BuildHoldReleaseDefault());
            Assert.That(
                provider.TryGetTemplate(
                    10,
                    out _),
                Is.False);
        }

        [Test]
        public void Provider_CreateEmpty_AllSlotsPressCommit()
        {
            AbilityInputMappingProvider provider =
                AbilityInputMappingProvider
                    .CreateEmpty();

            for (byte slot = 0;
                 slot < 4;
                 slot++)
            {
                Assert.That(
                    provider.TryGetTemplate(
                        slot,
                        out InputMappingTemplate template),
                    Is.True);
                AssertTemplateEquals(
                    template,
                    AbilityInputMapping
                        .DefaultPressCommit);
            }
        }

        [Test]
        public void Provider_FromHandler_UsesFormalAimAndRange()
        {
            var gameObject =
                new GameObject(
                    "AbilityMappingFixture");
            try
            {
                AbilityHandler handler =
                    gameObject.AddComponent<AbilityHandler>();
                var runtime = new AbilityRuntime
                {
                    Level = 1,
                    Definition = new AbilityDef
                    {
                        AbilityId = 700,
                        CastModel =
                            new HoldReleaseCastModelDef(),
                        AimKind = AimKind.Direction,
                        CastRange = (fp)7,
                    },
                };
                var slot = new AbilitySlotRuntime
                {
                    SlotIndex = 0,
                    ActiveAbilityId = 700,
                };
                slot.AddAbility(runtime);
                handler.AddSlot(slot);

                AbilityInputMappingProvider provider =
                    AbilityInputMappingProvider
                        .CreateFromAbilityHandler(
                            handler);

                Assert.That(
                    provider.TryGetTemplate(
                        0,
                        out InputMappingTemplate template),
                    Is.True);
                AssertTemplateEquals(
                    template,
                    AbilityInputMapping
                        .BuildHoldReleaseDefault());
                Assert.That(
                    provider.TryGetAimConfiguration(
                        0,
                        out AimKind kind,
                        out fp range),
                    Is.True);
                Assert.That(
                    kind,
                    Is.EqualTo(AimKind.Direction));
                Assert.That(
                    range,
                    Is.EqualTo((fp)7));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SequentialRecastDirection_DefaultsToLeftClickCommit()
        {
            InputMappingTemplate template =
                AbilityInputMapping.BuildDefault(
                    new SequentialRecastCastModelDef(),
                    AimKind.Direction);
            InputBinding press = Expect(
                template,
                InputTrigger.AbilityKeyPressed);
            InputBinding click = Expect(
                template,
                InputTrigger.PrimaryClick);

            Assert.That(
                press.Translation,
                Is.EqualTo(InputTranslation.LocalAimOnly));
            Assert.That(
                click.Translation,
                Is.EqualTo(InputTranslation.Commit));
            Assert.That(click.CaptureAim, Is.True);
        }

        [Test]
        public void DirectionalZoneResolver_FollowsSequentialRecastStage()
        {
            var first = new DirectionalMultiZoneDamageStageDef
            {
                StageDefId = 1,
                Shape = DirectionalZoneShape.Rectangle,
            };
            var second = new DirectionalMultiZoneDamageStageDef
            {
                StageDefId = 3,
                Shape = DirectionalZoneShape.Trapezoid,
            };
            var final = new DirectionalMultiZoneDamageStageDef
            {
                StageDefId = 5,
                Shape = DirectionalZoneShape.OffsetCircle,
            };
            var model = new SequentialRecastCastModelDef
            {
                FirstImpact = new CastStage
                {
                    StageKey = 1,
                    Def = first,
                },
                FirstRecastWindow = new CastStage
                {
                    StageKey = 2,
                },
                SecondImpact = new CastStage
                {
                    StageKey = 3,
                    Def = second,
                },
                SecondRecastWindow = new CastStage
                {
                    StageKey = 4,
                },
                FinalImpact = new CastStage
                {
                    StageKey = 5,
                    Def = final,
                },
            };
            var runtime = new AbilityRuntime
            {
                Level = 1,
                Definition = new AbilityDef
                {
                    AbilityId = 10021,
                    CastModel = model,
                    AimKind = AimKind.Direction,
                },
            };
            var holder = new GameObject("IndicatorResolverFixture");
            try
            {
                AbilityHandler handler =
                    holder.AddComponent<AbilityHandler>();
                var slot = new AbilitySlotRuntime
                {
                    SlotIndex = 0,
                    ActiveAbilityId = 10021,
                };
                slot.AddAbility(runtime);
                handler.AddSlot(slot);

                Assert.That(
                    AbilityIndicatorGeometryResolver
                        .TryResolveDirectionalZone(
                            handler,
                            0,
                            out DirectionalMultiZoneDamageStageDef
                                resolved),
                    Is.True);
                Assert.That(resolved, Is.SameAs(first));

                AbilitySession session = runtime.BeginSession(
                    1,
                    0,
                    AimSnapshot.None);
                session.CurrentStageKey = 2;
                Assert.That(
                    AbilityIndicatorGeometryResolver
                        .TryResolveDirectionalZone(
                            handler,
                            0,
                            out resolved),
                    Is.True);
                Assert.That(resolved, Is.SameAs(second));
                session.CurrentStageKey = 4;
                AbilityIndicatorGeometryResolver
                    .TryResolveDirectionalZone(
                        handler,
                        0,
                        out resolved);
                Assert.That(resolved, Is.SameAs(final));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}

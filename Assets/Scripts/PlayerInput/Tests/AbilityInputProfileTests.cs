using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.Unit;
using NUnit.Framework;

namespace FrameSyncMoba.PlayerInput.Tests
{
    [TestFixture]
    public class AbilityInputProfileTests
    {
        [Test]
        public void Bake_HoldRelease_ReturnsPressFocusReleaseOrPrimaryCommit()
        {
            var castModel = new HoldReleaseCastModelDef();
            var profile = AbilityInputProfileBaker.Bake(castModel);

            Assert.That(profile.Mode,
                Is.EqualTo(BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit));
        }

        [Test]
        public void Bake_CommitCast_ReturnsPressCommit()
        {
            var castModel = new CommitCastModelDef();
            var profile = AbilityInputProfileBaker.Bake(castModel);

            Assert.That(profile.Mode,
                Is.EqualTo(BakedPlayerAbilityInputMode.PressCommit));
        }

        [Test]
        public void Bake_Channel_ReturnsPressFocusReleaseOrPrimaryCommit()
        {
            var castModel = new ChannelCastModelDef();
            var profile = AbilityInputProfileBaker.Bake(castModel);

            Assert.That(profile.Mode,
                Is.EqualTo(BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit));
        }

        [Test]
        public void Bake_ActiveSignal_ReturnsPressCommit()
        {
            var castModel = new ActiveSignalCastModelDef();
            var profile = AbilityInputProfileBaker.Bake(castModel);

            Assert.That(profile.Mode,
                Is.EqualTo(BakedPlayerAbilityInputMode.PressCommit));
        }

        [Test]
        public void Bake_NullCastModel_ReturnsPressCommit()
        {
            var profile = AbilityInputProfileBaker.Bake(null);

            Assert.That(profile.Mode,
                Is.EqualTo(BakedPlayerAbilityInputMode.PressCommit));
        }

        [Test]
        public void Bake_CommitWithAim_ReturnsLocalAimPrimaryCommit()
        {
            var castModel = new CommitCastModelDef();
            var profile = AbilityInputProfileBaker.Bake(castModel, AimKind.Point);

            Assert.That(profile.Mode,
                Is.EqualTo(BakedPlayerAbilityInputMode.LocalAimPrimaryCommit));
        }

        [Test]
        public void Bake_HoldReleaseWithAim_ReturnsPressFocusReleaseOrPrimaryCommit()
        {
            var castModel = new HoldReleaseCastModelDef();
            var profile = AbilityInputProfileBaker.Bake(castModel, AimKind.Direction);

            Assert.That(profile.Mode,
                Is.EqualTo(BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit));
        }

        [Test]
        public void Bake_CommitWithSelfAim_ReturnsPressCommit()
        {
            var castModel = new CommitCastModelDef();
            var profile = AbilityInputProfileBaker.Bake(castModel, AimKind.Self);

            Assert.That(profile.Mode,
                Is.EqualTo(BakedPlayerAbilityInputMode.PressCommit));
        }

        [Test]
        public void Provider_TryGetProfile_ValidSlot_ReturnsProfile()
        {
            var profiles = new BakedPlayerAbilityInputProfile[4];
            profiles[2] = new BakedPlayerAbilityInputProfile(
                BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit);
            var provider = new AbilityInputProfileProvider(profiles);

            bool found = provider.TryGetProfile(2, out var profile);
            Assert.That(found, Is.True);
            Assert.That(profile.Mode,
                Is.EqualTo(BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit));
        }

        [Test]
        public void Provider_TryGetProfile_OutOfRangeSlot_ReturnsFalse()
        {
            var provider = AbilityInputProfileProvider.CreateEmpty();

            bool found = provider.TryGetProfile(10, out _);
            Assert.That(found, Is.False);
        }

        [Test]
        public void Provider_TryGetAimKind_ValidSlot_ReturnsAimKind()
        {
            var profiles = new BakedPlayerAbilityInputProfile[4];
            var aimKinds = new AimKind[4];
            aimKinds[1] = AimKind.Unit;
            var provider = new AbilityInputProfileProvider(profiles, aimKinds);

            bool found = provider.TryGetAimKind(1, out var aimKind);
            Assert.That(found, Is.True);
            Assert.That(aimKind, Is.EqualTo(AimKind.Unit));
        }

        [Test]
        public void Provider_CreateEmpty_AllSlotsPressCommit()
        {
            var provider = AbilityInputProfileProvider.CreateEmpty();

            for (byte slot = 0; slot < 4; slot++)
            {
                bool found = provider.TryGetProfile(slot, out var profile);
                Assert.That(found, Is.True);
                Assert.That(profile.Mode,
                    Is.EqualTo(BakedPlayerAbilityInputMode.PressCommit));
            }
        }
    }
}

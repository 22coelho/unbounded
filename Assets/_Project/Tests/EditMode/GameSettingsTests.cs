using NUnit.Framework;
using Unbounded.Core;

namespace Unbounded.Tests.EditMode
{
    public sealed class GameSettingsTests
    {
        [Test]
        public void DefaultsArePlayable()
        {
            GameSettings settings = GameSettings.Default;

            Assert.That(settings.MasterVolume, Is.EqualTo(GameSettings.DefaultMasterVolume));
            Assert.That(settings.SteeringSensitivity, Is.EqualTo(GameSettings.DefaultSteeringSensitivity));
        }

        [Test]
        public void ConstructionClampsBothValues()
        {
            var loud = new GameSettings(4f, 99f);
            var quiet = new GameSettings(-2f, 0f);

            Assert.That(loud.MasterVolume, Is.EqualTo(GameSettings.MaxMasterVolume));
            Assert.That(loud.SteeringSensitivity, Is.EqualTo(GameSettings.MaxSteeringSensitivity));
            Assert.That(quiet.MasterVolume, Is.EqualTo(GameSettings.MinMasterVolume));
            Assert.That(quiet.SteeringSensitivity, Is.EqualTo(GameSettings.MinSteeringSensitivity));
        }

        [Test]
        public void ChangingOneValueLeavesTheOtherAlone()
        {
            GameSettings settings = GameSettings.Default
                .WithMasterVolume(0.25f)
                .WithSteeringSensitivity(1.5f);

            Assert.That(settings.MasterVolume, Is.EqualTo(0.25f));
            Assert.That(settings.SteeringSensitivity, Is.EqualTo(1.5f));
        }

        [Test]
        public void NaNVolumeFallsBackToSilenceInsteadOfFullScale()
        {
            var settings = new GameSettings(float.NaN, 1f);

            Assert.That(settings.MasterVolume, Is.EqualTo(GameSettings.MinMasterVolume));
        }
    }
}

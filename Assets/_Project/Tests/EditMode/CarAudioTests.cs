using System;
using NUnit.Framework;
using Unbounded.App;

namespace Unbounded.Tests.EditMode
{
    public sealed class CarAudioTests
    {
        private const float Step = 1f / 60f;

        private static void Run(CarAudioModel model, float forwardSpeed, float lateralSpeed,
            float throttle, float seconds)
        {
            int steps = Math.Max(1, (int)MathF.Round(seconds / Step));
            for (int i = 0; i < steps; i++)
            {
                model.Step(forwardSpeed, lateralSpeed, throttle, 60f, Step);
            }
        }

        [Test]
        public void EnginePitchAndVolumeRiseWithSpeedAndThrottle()
        {
            var slow = new CarAudioModel();
            var fast = new CarAudioModel();

            Run(slow, 10f, 0f, 1f, 2f);
            Run(fast, 55f, 0f, 1f, 2f);

            Assert.That(fast.EnginePitch, Is.GreaterThan(slow.EnginePitch + 0.3f),
                "A faster car should sit at a higher engine pitch.");
            Assert.That(fast.EngineVolume, Is.GreaterThan(slow.EngineVolume + 0.05f),
                "A faster car should be louder.");
            Assert.That(fast.EnginePitch, Is.GreaterThan(CarAudioModel.IdlePitch),
                "Speed must lift the engine off idle.");
            Assert.That(fast.EngineVolume, Is.LessThanOrEqualTo(CarAudioModel.FullEngineVolume + 0.001f));
        }

        [Test]
        public void ThrottleRaisesTheEngineNoteAtTheSameSpeed()
        {
            var coasting = new CarAudioModel();
            var accelerating = new CarAudioModel();

            Run(coasting, 40f, 0f, 0f, 2f);
            Run(accelerating, 40f, 0f, 1f, 2f);

            Assert.That(accelerating.EnginePitch, Is.GreaterThan(coasting.EnginePitch),
                "Throttle should raise the note even at the same road speed.");
            Assert.That(accelerating.EngineVolume, Is.GreaterThan(coasting.EngineVolume));
        }

        [Test]
        public void EnginePitchMovesSmoothlyInsteadOfJumping()
        {
            var model = new CarAudioModel();
            model.Step(60f, 0f, 1f, 60f, Step);

            float halfRange = (CarAudioModel.IdlePitch + CarAudioModel.TopPitch) * 0.5f;
            Assert.That(model.EnginePitch, Is.LessThan(halfRange),
                "One frame of full throttle must not snap the engine to its top pitch.");
            Assert.That(model.EnginePitch, Is.GreaterThan(CarAudioModel.IdlePitch),
                "The pitch should still start moving straight away.");
        }

        [Test]
        public void TyreScreechFollowsLateralSlipAndStopsWhenGripReturns()
        {
            var model = new CarAudioModel();

            Run(model, 30f, 8f, 0.5f, 0.5f);
            Assert.That(model.TyreVolume, Is.GreaterThan(0.3f),
                "A car sliding sideways at speed should be screeching.");
            Assert.That(model.IsSliding, Is.True);

            Run(model, 30f, 0f, 0.5f, 1f);
            Assert.That(model.TyreVolume, Is.LessThan(0.01f),
                "The screech must fall away when the tyres find grip again.");
            Assert.That(model.IsSliding, Is.False);
        }

        [Test]
        public void StraightDrivingAndAStationaryCarMakeNoTyreNoise()
        {
            var straight = new CarAudioModel();
            var stationary = new CarAudioModel();

            Run(straight, 45f, 1f, 1f, 1f);
            Run(stationary, 0f, 8f, 0f, 1f);

            Assert.That(straight.TyreVolume, Is.LessThan(0.01f),
                "Driving straight should not trigger the screech.");
            Assert.That(stationary.TyreVolume, Is.LessThan(0.01f),
                "A car with no forward motion is not sliding.");
        }

        [Test]
        public void ImpactVolumeScalesWithSpeedAndIgnoresGentleTouches()
        {
            Assert.That(CarAudioModel.ImpactVolume(1f), Is.Zero,
                "A gentle touch should not play an impact.");
            Assert.That(CarAudioModel.ImpactVolume(CarAudioModel.ImpactMinSpeed), Is.Zero);

            float medium = CarAudioModel.ImpactVolume(8f);
            float heavy = CarAudioModel.ImpactVolume(20f);
            float maximum = CarAudioModel.ImpactVolume(30f);

            Assert.That(medium, Is.GreaterThan(0.2f));
            Assert.That(heavy, Is.GreaterThan(medium));
            Assert.That(maximum, Is.EqualTo(1f).Within(0.001f));
            Assert.That(CarAudioModel.ImpactPitch(8f), Is.GreaterThan(CarAudioModel.ImpactPitch(20f)),
                "A heavy hit should land lower than a scrape.");
        }
    }
}

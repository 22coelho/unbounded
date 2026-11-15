using NUnit.Framework;
using Unbounded.Track;

namespace Unbounded.Tests.EditMode
{
    public sealed class WrongWayDetectorTests
    {
        private const float Step = 0.1f;

        private static void StepFor(WrongWayDetector detector, float alignment, float speed, float seconds)
        {
            int steps = (int)(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                detector.Step(alignment, speed, Step);
            }
        }

        [Test]
        public void WarningAppearsAfterSustainedWrongWayTravel()
        {
            var detector = new WrongWayDetector();

            StepFor(detector, -1f, 20f, WrongWayDetector.WarningDelay * 0.5f);
            Assert.That(detector.IsWrongWay, Is.False, "A brief wrong heading should not flash the warning.");

            StepFor(detector, -1f, 20f, WrongWayDetector.WarningDelay);
            Assert.That(detector.IsWrongWay, Is.True);
        }

        [Test]
        public void WarningClearsOnceTheCarFacesTheRightWay()
        {
            var detector = new WrongWayDetector();
            StepFor(detector, -1f, 20f, WrongWayDetector.WarningDelay + Step);
            Assert.That(detector.IsWrongWay, Is.True);

            StepFor(detector, 1f, 20f, WrongWayDetector.ClearingDelay * 0.5f);
            Assert.That(detector.IsWrongWay, Is.True, "The warning should not blink away the instant the car rotates.");

            StepFor(detector, 1f, 20f, WrongWayDetector.ClearingDelay + Step);
            Assert.That(detector.IsWrongWay, Is.False);
        }

        [Test]
        public void StandingStillAgainstTheTrackNeverWarns()
        {
            var detector = new WrongWayDetector();

            StepFor(detector, -1f, 0f, 2f);

            Assert.That(detector.IsWrongWay, Is.False);
        }

        [Test]
        public void ARecoveredHeadingRestartsTheWarningDelay()
        {
            var detector = new WrongWayDetector();

            StepFor(detector, -1f, 20f, WrongWayDetector.WarningDelay * 0.75f);
            StepFor(detector, 1f, 20f, Step);
            StepFor(detector, -1f, 20f, WrongWayDetector.WarningDelay * 0.75f);

            Assert.That(detector.IsWrongWay, Is.False,
                "Breaking the wrong-way heading should restart the clock, not add to it.");
        }
    }
}

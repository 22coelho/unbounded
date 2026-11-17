using NUnit.Framework;
using Unbounded.Vehicle;

namespace Unbounded.Tests.EditMode
{
    public sealed class SteeringCurveTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void FullLockStaysFullLockAtEverySensitivity()
        {
            float[] sensitivities = { SteeringCurve.MinSensitivity, 0.75f, 1f, 1.5f, SteeringCurve.MaxSensitivity };

            foreach (float sensitivity in sensitivities)
            {
                Assert.That(SteeringCurve.Apply(1f, sensitivity), Is.EqualTo(1f).Within(Tolerance));
                Assert.That(SteeringCurve.Apply(-1f, sensitivity), Is.EqualTo(-1f).Within(Tolerance));
            }
        }

        [Test]
        public void LowerSensitivityGentlesTheMiddleOfTheStick()
        {
            float soft = SteeringCurve.Apply(0.5f, 0.5f);
            float neutral = SteeringCurve.Apply(0.5f, 1f);
            float sharp = SteeringCurve.Apply(0.5f, 2f);

            Assert.That(soft, Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(neutral, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(sharp, Is.GreaterThan(neutral));
            Assert.That(sharp, Is.LessThan(1f));
        }

        [Test]
        public void CurveIsSymmetricAroundCentre()
        {
            float right = SteeringCurve.Apply(0.4f, 1.5f);
            float left = SteeringCurve.Apply(-0.4f, 1.5f);

            Assert.That(left, Is.EqualTo(-right).Within(Tolerance));
        }

        [Test]
        public void CentredStickAndBadReadingsProduceNoSteer()
        {
            Assert.That(SteeringCurve.Apply(0f, 1.5f), Is.EqualTo(0f));
            Assert.That(SteeringCurve.Apply(float.NaN, 1.5f), Is.EqualTo(0f));
        }

        [Test]
        public void OutOfRangeSensitivityIsClampedToTheEnds()
        {
            Assert.That(
                SteeringCurve.Apply(0.5f, 0f),
                Is.EqualTo(SteeringCurve.Apply(0.5f, SteeringCurve.MinSensitivity)).Within(Tolerance));
            Assert.That(
                SteeringCurve.Apply(0.5f, 99f),
                Is.EqualTo(SteeringCurve.Apply(0.5f, SteeringCurve.MaxSensitivity)).Within(Tolerance));
        }
    }
}

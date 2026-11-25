using NUnit.Framework;
using Unbounded.Vehicle;

namespace Unbounded.Tests.EditMode
{
    public sealed class WheelRollTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void ForwardSpeedTurnsTheWheelForward()
        {
            float delta = WheelRoll.DeltaDegrees(10f, 0.25f, 1f);

            Assert.That(delta, Is.EqualTo(2291.831f).Within(Tolerance),
                "Ten metres per second on a quarter-metre wheel is forty radians per second.");
        }

        [Test]
        public void ReverseSpinsTheWheelBackward()
        {
            float forward = WheelRoll.DeltaDegrees(10f, 0.25f, 1f);
            float reverse = WheelRoll.DeltaDegrees(-10f, 0.25f, 1f);

            Assert.That(reverse, Is.EqualTo(-forward).Within(Tolerance));
        }

        [Test]
        public void SmallerWheelSpinsFaster()
        {
            Assert.That(
                WheelRoll.DeltaDegrees(10f, 0.2f, 1f),
                Is.GreaterThan(WheelRoll.DeltaDegrees(10f, 0.4f, 1f)));
        }

        [Test]
        public void DeltaTimeScalesTheAdvance()
        {
            float oneStep = WheelRoll.DeltaDegrees(10f, 0.25f, 0.5f);
            float twoSteps = WheelRoll.DeltaDegrees(10f, 0.25f, 1f);

            Assert.That(twoSteps, Is.EqualTo(oneStep * 2f).Within(Tolerance));
        }

        [Test]
        public void AngleWrapsIntoHalfTurn()
        {
            Assert.That(WheelRoll.Normalize(190f), Is.EqualTo(-170f).Within(Tolerance));
            Assert.That(WheelRoll.Normalize(-190f), Is.EqualTo(170f).Within(Tolerance));
            Assert.That(WheelRoll.Normalize(180f), Is.EqualTo(-180f).Within(Tolerance));
            Assert.That(WheelRoll.Normalize(720f), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ZeroSpeedHoldsTheAngle()
        {
            Assert.That(WheelRoll.Advance(42f, 0f, 0.25f, 1f), Is.EqualTo(42f).Within(Tolerance));
        }

        [Test]
        public void ZeroRadiusIsClampedInsteadOfDividingByZero()
        {
            Assert.That(
                WheelRoll.DeltaDegrees(10f, 0f, 1f),
                Is.EqualTo(WheelRoll.DeltaDegrees(10f, WheelRoll.MinRadius, 1f)).Within(Tolerance));
        }

        [Test]
        public void BadInputsDoNotProduceNaN()
        {
            float angle = WheelRoll.Advance(float.NaN, float.PositiveInfinity, 0f, float.NaN);

            Assert.That(float.IsNaN(angle), Is.False);
            Assert.That(float.IsInfinity(angle), Is.False);
        }
    }
}

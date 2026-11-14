using System;
using NUnit.Framework;
using Unbounded.Vehicle;

namespace Unbounded.Tests.EditMode
{
    public sealed class ArcadeHandlingModelTests
    {
        private const float Mass = 1200f;
        private const float Step = 1f / 50f;

        [Test]
        public void ThrottleAcceleratesFromRest()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);

            VehicleMotion result = Simulate(
                model,
                default,
                new HandlingInput { Throttle = 1f },
                1f);

            Assert.That(result.ForwardSpeed, Is.GreaterThan(5f));
            Assert.That(result.ForwardSpeed, Is.LessThan(HandlingConfig.Default.MaxSpeed));
        }

        [Test]
        public void BrakeSlowsForwardMotion()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);
            var moving = new VehicleMotion { ForwardSpeed = 20f };

            VehicleMotion result = Simulate(
                model,
                moving,
                new HandlingInput { Brake = 1f },
                0.5f);

            Assert.That(result.ForwardSpeed, Is.LessThan(20f));
            Assert.That(result.ForwardSpeed, Is.GreaterThan(0f));
        }

        [Test]
        public void HoldingBrakeAtStandstillReverses()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);

            VehicleMotion result = Simulate(
                model,
                default,
                new HandlingInput { Brake = 1f },
                1f);

            Assert.That(result.ForwardSpeed, Is.LessThan(-2f));
        }

        [Test]
        public void SteeringAtStandstillProducesNoMotion()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);
            var input = new HandlingInput { Steer = 1f };
            HandlingForces forces = default;

            for (int i = 0; i < 50; i++)
            {
                forces = model.Step(default, input, Step);
            }

            Assert.That(forces.DriveForce, Is.EqualTo(0f).Within(0.001f));
            Assert.That(forces.LateralForce, Is.EqualTo(0f).Within(0.001f));
            Assert.That(forces.YawAcceleration, Is.EqualTo(0f).Within(0.001f));
            Assert.That(model.SteeringAngleDegrees, Is.GreaterThan(30f));
        }

        [Test]
        public void SteeringWhileMovingYawsTheCar()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);
            var moving = new VehicleMotion { ForwardSpeed = 10f };

            VehicleMotion result = Simulate(
                model,
                moving,
                new HandlingInput { Throttle = 0.5f, Steer = 1f },
                1f);

            Assert.That(result.YawRate, Is.GreaterThan(0.2f));
            Assert.That(result.LateralSpeed, Is.LessThan(0f));
        }

        [Test]
        public void GripIsStrongerAtLowSpeedThanHighSpeed()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);
            var lowSpeed = new VehicleMotion { ForwardSpeed = 5f, LateralSpeed = 2f };
            var highSpeed = new VehicleMotion { ForwardSpeed = 55f, LateralSpeed = 2f };

            HandlingForces lowForces = model.Step(lowSpeed, default, Step);
            HandlingForces highForces = model.Step(highSpeed, default, Step);

            Assert.That(MathF.Abs(lowForces.LateralForce), Is.GreaterThan(10000f));
            Assert.That(
                MathF.Abs(highForces.LateralForce),
                Is.LessThan(MathF.Abs(lowForces.LateralForce) * 0.8f));
        }

        [Test]
        public void FullSteerAtSpeedStaysBoundedInsteadOfSpinning()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);
            VehicleMotion motion = Simulate(
                model,
                default,
                new HandlingInput { Throttle = 1f },
                8f);

            Assert.That(motion.ForwardSpeed, Is.GreaterThan(25f));

            float maxYawRate = 0f;
            var steeringInput = new HandlingInput { Throttle = 1f, Steer = 1f };

            for (int i = 0; i < 250; i++)
            {
                HandlingForces forces = model.Step(motion, steeringInput, Step);
                motion.ForwardSpeed += (forces.DriveForce / Mass + motion.YawRate * motion.LateralSpeed) * Step;
                motion.LateralSpeed += (forces.LateralForce / Mass - motion.YawRate * motion.ForwardSpeed) * Step;
                motion.YawRate += forces.YawAcceleration * Step;
                maxYawRate = MathF.Max(maxYawRate, MathF.Abs(motion.YawRate));
            }

            Assert.That(maxYawRate, Is.LessThan(1.5f));
            Assert.That(float.IsNaN(motion.ForwardSpeed), Is.False);
            Assert.That(float.IsNaN(motion.LateralSpeed), Is.False);
        }

        private static VehicleMotion Simulate(
            ArcadeHandlingModel model,
            VehicleMotion motion,
            HandlingInput input,
            float seconds)
        {
            int steps = (int)MathF.Round(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                HandlingForces forces = model.Step(motion, input, Step);
                motion.ForwardSpeed += (forces.DriveForce / Mass + motion.YawRate * motion.LateralSpeed) * Step;
                motion.LateralSpeed += (forces.LateralForce / Mass - motion.YawRate * motion.ForwardSpeed) * Step;
                motion.YawRate += forces.YawAcceleration * Step;
            }

            return motion;
        }
    }
}

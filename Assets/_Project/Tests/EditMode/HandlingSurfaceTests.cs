using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unbounded.Vehicle;

namespace Unbounded.Tests.EditMode
{
    public sealed class HandlingSurfaceTests
    {
        private const float Mass = 1200f;
        private const float Step = 1f / 50f;

        [Test]
        public void GrassSlowsTheCarNoticeably()
        {
            VehicleMotion road = Simulate(HandlingSurface.Road, new HandlingInput { Throttle = 1f }, 6f);
            VehicleMotion grass = Simulate(HandlingSurface.Grass, new HandlingInput { Throttle = 1f }, 6f);

            Assert.That(road.ForwardSpeed, Is.GreaterThan(20f));
            Assert.That(grass.ForwardSpeed, Is.LessThan(road.ForwardSpeed * 0.75f),
                "Driving on grass should cost noticeably more speed than driving on the road.");
        }

        [Test]
        public void GrassAddsRollingResistanceWhenCoasting()
        {
            VehicleMotion road = Simulate(
                HandlingSurface.Road,
                default,
                1f,
                new VehicleMotion { ForwardSpeed = 30f });
            VehicleMotion grass = Simulate(
                HandlingSurface.Grass,
                default,
                1f,
                new VehicleMotion { ForwardSpeed = 30f });

            Assert.That(road.ForwardSpeed, Is.LessThan(30f));
            Assert.That(grass.ForwardSpeed, Is.LessThan(road.ForwardSpeed - 1f),
                "Grass should bleed speed faster than asphalt.");
        }

        [Test]
        public void GrassReducesLateralGrip()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);
            var motion = new VehicleMotion { ForwardSpeed = 25f, LateralSpeed = 2f };

            HandlingForces road = model.Step(motion, default, HandlingSurface.Road, Step);
            HandlingForces grass = model.Step(motion, default, HandlingSurface.Grass, Step);

            Assert.That(MathF.Abs(grass.LateralForce), Is.LessThan(MathF.Abs(road.LateralForce)));
        }

        private static VehicleMotion Simulate(
            HandlingSurface surface,
            HandlingInput input,
            float seconds,
            VehicleMotion? start = null)
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);
            VehicleMotion motion = start ?? default;
            int steps = (int)MathF.Round(seconds / Step);

            for (int i = 0; i < steps; i++)
            {
                HandlingForces forces = model.Step(motion, input, surface, Step);
                motion.ForwardSpeed += (forces.DriveForce / Mass + motion.YawRate * motion.LateralSpeed) * Step;
                motion.LateralSpeed += (forces.LateralForce / Mass - motion.YawRate * motion.ForwardSpeed) * Step;
                motion.YawRate += forces.YawAcceleration * Step;
            }

            return motion;
        }
    }
}

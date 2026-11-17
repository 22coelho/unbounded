using System;
using NUnit.Framework;
using Unbounded.AI;
using Unbounded.Core;
using Unbounded.Track;
using Unbounded.Vehicle;
using UnityEngine;

namespace Unbounded.Tests.EditMode
{
    public sealed class NitroTests
    {
        private const float Mass = 1200f;
        private const float Step = 1f / 50f;

        private static TrackPath CreateCentreline() => TrackPath.FromControlPoints(TrackLayout.Default(), 32);

        private static RacingLine CreateLine() => RacingLine.FromCentreline(CreateCentreline(), TrackShape.Default);

        /// <summary>A car sitting on the line at a distance, with a given speed and tank.</summary>
        private static AiSituation At(
            RacingLine line,
            float distance,
            float speed,
            float nitroCharge,
            bool lockedOut = false)
        {
            Vector3 forward = line.SampleDirection(distance);

            return new AiSituation
            {
                Position = line.SamplePosition(distance),
                Forward = forward,
                ForwardSpeed = speed,
                NitroCharge = nitroCharge,
                NitroLockedOut = lockedOut
            };
        }

        private static float TightestCorner(IRacingLine line)
        {
            float tightest = 0f;
            float at = 0f;

            for (float distance = 0f; distance < line.Length; distance += 2f)
            {
                const float probe = 6f;
                Vector3 before = line.SampleDirection(distance - probe);
                Vector3 after = line.SampleDirection(distance + probe);
                float angle = Mathf.Atan2(
                    Vector3.Dot(Vector3.Cross(before, after), Vector3.up),
                    Vector3.Dot(before, after));
                float curvature = Mathf.Abs(angle) / (probe * 2f);

                if (curvature > tightest)
                {
                    tightest = curvature;
                    at = distance;
                }
            }

            return at;
        }

        [Test]
        public void NitroAddsAccelerationWhileChargeLasts()
        {
            var boosted = new ArcadeHandlingModel(HandlingConfig.Default);
            var plain = new ArcadeHandlingModel(HandlingConfig.Default);

            VehicleMotion withNitro = Simulate(
                boosted,
                default,
                new HandlingInput { Throttle = 1f, Nitro = true },
                1.5f);
            VehicleMotion withoutNitro = Simulate(
                plain,
                default,
                new HandlingInput { Throttle = 1f },
                1.5f);

            Assert.That(withNitro.ForwardSpeed, Is.GreaterThan(withoutNitro.ForwardSpeed + 3f),
                "Nitro should be worth noticeably more than holding the throttle.");
            Assert.That(boosted.NitroCharge, Is.LessThan(1f),
                "The tank should be spending charge while it boosts.");
        }

        [Test]
        public void NitroDrainsWhileBoostingAndStopsWhenEmpty()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);

            Simulate(
                model,
                default,
                new HandlingInput { Nitro = true },
                HandlingConfig.Default.NitroDuration + 0.25f);

            Assert.That(model.NitroCharge, Is.EqualTo(0f).Within(0.001f));
            Assert.That(model.IsNitroActive, Is.False, "An empty tank must stop boosting.");
            Assert.That(model.IsNitroLockedOut, Is.True,
                "Running dry should lock the Nitro out until it has recharged.");
        }

        [Test]
        public void NitroRechargesOnlyAfterTheDelayOnceReleased()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);

            Simulate(model, default, new HandlingInput { Nitro = true }, 1f);
            float afterBoost = model.NitroCharge;
            Assert.That(afterBoost, Is.LessThan(1f));

            Simulate(model, default, default, 0.5f);
            Assert.That(model.NitroCharge, Is.EqualTo(afterBoost).Within(0.001f),
                "The tank should stay put for a moment after the burst ends.");

            Simulate(model, default, default, 1f);
            Assert.That(model.NitroCharge, Is.GreaterThan(afterBoost + 0.02f),
                "The tank should recharge once the delay has passed.");
        }

        [Test]
        public void EmptyNitroCannotBeHeldDownAndUnlocksAfterRecharging()
        {
            var model = new ArcadeHandlingModel(HandlingConfig.Default);

            Simulate(
                model,
                default,
                new HandlingInput { Nitro = true },
                HandlingConfig.Default.NitroDuration + 1f);

            float lockedCharge = model.NitroCharge;
            Assert.That(model.IsNitroLockedOut, Is.True);

            bool boostedWhileLocked = false;
            for (int i = 0; i < 50; i++)
            {
                model.Step(default, new HandlingInput { Nitro = true }, Step);
                boostedWhileLocked |= model.IsNitroActive;
            }

            Assert.That(boostedWhileLocked, Is.False,
                "Holding the button through an empty tank must not produce a boost.");
            Assert.That(model.NitroCharge, Is.GreaterThan(lockedCharge),
                "Holding the button should still let the tank recharge.");

            // Release and let the tank fill: the lockout has to clear, or the
            // Nitro would be dead for the rest of the Race.
            Simulate(
                model,
                default,
                default,
                HandlingConfig.Default.NitroRechargeSeconds + HandlingConfig.Default.NitroRechargeDelay + 1f);

            Assert.That(model.NitroCharge, Is.EqualTo(1f).Within(0.001f));
            Assert.That(model.IsNitroLockedOut, Is.False);
        }

        [Test]
        public void TheOpponentFiresNitroOnAStraightAndHoldsItInACorner()
        {
            RacingLine line = CreateLine();
            var follower = new RacingLineFollower(AiConfig.Default);

            AiControls straight = follower.Step(At(line, 40f, 55f, 1f), line, Step);
            Assert.That(straight.Nitro, Is.True,
                "An opponent on a straight with a full tank should use it.");
            Assert.That(straight.Throttle, Is.GreaterThan(0f));

            AiControls corner = new RacingLineFollower(AiConfig.Default)
                .Step(At(line, TightestCorner(line) - 20f, 30f, 1f), line, Step);
            Assert.That(corner.Nitro, Is.False,
                "Nitro in a corner would only push the car wide.");
        }

        [Test]
        public void TheOpponentDoesNotWasteNitroWithoutChargeOrWhileLockedOut()
        {
            RacingLine line = CreateLine();

            AiControls empty = new RacingLineFollower(AiConfig.Default)
                .Step(At(line, 40f, 55f, 0f), line, Step);
            AiControls lockedOut = new RacingLineFollower(AiConfig.Default)
                .Step(At(line, 40f, 55f, 1f, lockedOut: true), line, Step);
            AiControls tooSlow = new RacingLineFollower(AiConfig.Default)
                .Step(At(line, 40f, 10f, 1f), line, Step);

            Assert.That(empty.Nitro, Is.False, "An empty tank cannot fire.");
            Assert.That(lockedOut.Nitro, Is.False, "A locked-out tank cannot fire.");
            Assert.That(tooSlow.Nitro, Is.False, "Nitro off the line is wasted before the car is up to speed.");
        }

        [Test]
        public void TheOpponentKeepsPushingPastItsNormalTopSpeedWhileBoosting()
        {
            // A synthetic straight, so the check is about the Nitro ceiling and
            // not about whether the circuit happens to have a corner in range.
            var line = new StraightLine();
            var follower = new RacingLineFollower(AiConfig.Default);

            AiControls boosting = follower.Step(
                new AiSituation
                {
                    Position = new Vector3(0f, 0f, 40f),
                    Forward = Vector3.forward,
                    ForwardSpeed = 68f,
                    NitroCharge = 1f
                },
                line,
                Step);

            Assert.That(boosting.Nitro, Is.True);
            Assert.That(boosting.Brake, Is.Zero,
                "A boosting opponent must not brake back down to its unboosted pace.");
            Assert.That(boosting.Throttle, Is.GreaterThan(0f));
        }

        /// <summary>A dead-straight line, for decisions that must not depend on Track geometry.</summary>
        private sealed class StraightLine : IRacingLine
        {
            public float Length => 1000f;

            public Vector3 SamplePosition(float distance) => new Vector3(0f, 0f, distance);

            public Vector3 SampleDirection(float distance) => Vector3.forward;

            public float Project(Vector3 position) => position.z;
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

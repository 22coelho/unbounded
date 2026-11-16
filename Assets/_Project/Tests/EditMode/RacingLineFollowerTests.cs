using NUnit.Framework;
using Unbounded.AI;
using Unbounded.Core;
using Unbounded.Track;
using Unbounded.Vehicle;
using UnityEngine;

namespace Unbounded.Tests.EditMode
{
    public sealed class RacingLineFollowerTests
    {
        private const float Mass = 1200f;
        private const float Step = 1f / 50f;

        private static TrackPath CreateCentreline() => TrackPath.FromControlPoints(TrackLayout.Default(), 32);

        private static RacingLine CreateLine() => RacingLine.FromCentreline(CreateCentreline(), TrackShape.Default);

        /// <summary>A car sitting at a distance along the line, optionally displaced sideways.</summary>
        private static AiSituation At(
            RacingLine line,
            float distance,
            float speed,
            float lateral = 0f,
            float playerAhead = 0f)
        {
            Vector3 forward = line.SampleDirection(distance);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            return new AiSituation
            {
                Position = line.SamplePosition(distance) + right * lateral,
                Forward = forward,
                ForwardSpeed = speed,
                PlayerProgressAhead = playerAhead
            };
        }

        private static float DistanceFromLine(RacingLine line, Vector3 position)
        {
            Vector3 nearest = line.SamplePosition(line.Project(position));
            position.y = 0f;
            nearest.y = 0f;
            return Vector3.Distance(position, nearest);
        }

        private static float Curvature(IRacingLine line, float distance)
        {
            const float probe = 6f;
            Vector3 before = line.SampleDirection(distance - probe);
            Vector3 after = line.SampleDirection(distance + probe);
            float angle = Mathf.Atan2(
                Vector3.Dot(Vector3.Cross(before, after), Vector3.up),
                Vector3.Dot(before, after));
            return Mathf.Abs(angle) / (probe * 2f);
        }

        /// <summary>The distance along the line of its tightest corner.</summary>
        private static float TightestCorner(IRacingLine line)
        {
            float tightest = 0f;
            float at = 0f;

            for (float distance = 0f; distance < line.Length; distance += 2f)
            {
                float curvature = Curvature(line, distance);
                if (curvature > tightest)
                {
                    tightest = curvature;
                    at = distance;
                }
            }

            return at;
        }

        private static void Drive(
            RacingLineFollower follower,
            ArcadeHandlingModel model,
            IRacingLine line,
            ref Vector3 position,
            ref Quaternion rotation,
            ref VehicleMotion motion)
        {
            AiControls controls = follower.Step(
                new AiSituation
                {
                    Position = position,
                    Forward = rotation * Vector3.forward,
                    ForwardSpeed = motion.ForwardSpeed
                },
                line,
                Step);

            HandlingForces forces = model.Step(
                motion,
                new HandlingInput
                {
                    Throttle = controls.Throttle,
                    Brake = controls.Brake,
                    Steer = controls.Steer
                },
                Step);

            motion.ForwardSpeed += (forces.DriveForce / Mass + motion.YawRate * motion.LateralSpeed) * Step;
            motion.LateralSpeed += (forces.LateralForce / Mass - motion.YawRate * motion.ForwardSpeed) * Step;
            motion.YawRate += forces.YawAcceleration * Step;

            rotation *= Quaternion.Euler(0f, motion.YawRate * Step * Mathf.Rad2Deg, 0f);
            position += rotation * new Vector3(motion.LateralSpeed, 0f, motion.ForwardSpeed) * Step;
        }

        [Test]
        public void TheOpponentSteersBackTowardsTheLineWhenDisplaced()
        {
            RacingLine line = CreateLine();
            var follower = new RacingLineFollower(AiConfig.Default);

            AiControls pushedRight = follower.Step(At(line, 40f, 20f, lateral: 6f), line, Step);
            AiControls pushedLeft = follower.Step(At(line, 40f, 20f, lateral: -6f), line, Step);

            Assert.That(pushedRight.Steer, Is.LessThan(-0.05f),
                "A car to the right of the line should steer left, back towards it.");
            Assert.That(pushedLeft.Steer, Is.GreaterThan(0.05f),
                "A car to the left of the line should steer right, back towards it.");
        }

        [Test]
        public void TheOpponentFindsTheLineAgainAfterBeingDisplaced()
        {
            RacingLine line = CreateLine();
            var follower = new RacingLineFollower(AiConfig.Default);
            var model = new ArcadeHandlingModel(HandlingConfig.Default);

            const float start = 40f;
            Vector3 forward = line.SampleDirection(start);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 position = line.SamplePosition(start) + right * 6f;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            var motion = new VehicleMotion { ForwardSpeed = 18f };

            float first = DistanceFromLine(line, position);
            float last = first;

            for (int step = 0; step < 250; step++)
            {
                Drive(follower, model, line, ref position, ref rotation, ref motion);
                last = DistanceFromLine(line, position);
            }

            Assert.That(first, Is.EqualTo(6f).Within(0.2f));
            Assert.That(last, Is.LessThan(1f),
                $"The car was still {last:0.0}m off the line after five seconds.");
            Assert.That(last, Is.LessThan(first * 0.25f));
            Assert.That(motion.ForwardSpeed, Is.GreaterThan(5f),
                "Recovering should not mean stopping.");
        }

        [Test]
        public void TheOpponentBrakesBeforeATightCornerAndThrottlesOnAStraight()
        {
            RacingLine line = CreateLine();
            var follower = new RacingLineFollower(AiConfig.Default);
            float corner = TightestCorner(line);

            AiControls approaching = follower.Step(At(line, corner - 40f, 45f), line, Step);
            Assert.That(approaching.Brake, Is.GreaterThan(0.2f),
                "The car should already be on the brakes 40 metres before the tightest corner.");
            Assert.That(approaching.Throttle, Is.Zero);

            // The start straight is flat, so nothing ahead of it asks for brakes.
            AiControls straight = follower.Step(At(line, 40f, 20f), line, Step);
            Assert.That(straight.Throttle, Is.GreaterThan(0.3f),
                "The car should be on the throttle down a straight.");
            Assert.That(straight.Brake, Is.Zero);
        }

        [Test]
        public void PaceOffsetsGiveTheFieldDifferentSpeeds()
        {
            RacingLine line = CreateLine();
            AiConfig slowConfig = AiConfig.Default;
            slowConfig.Pace = 0.92f;
            AiConfig fastConfig = AiConfig.Default;
            fastConfig.Pace = 1f;

            var slow = new RacingLineFollower(slowConfig);
            var fast = new RacingLineFollower(fastConfig);

            // Between the two targets: the slow car is already over its pace,
            // the fast one still has room to run.
            const float speed = 57f;
            AiControls slowControls = slow.Step(At(line, 40f, speed), line, Step);
            AiControls fastControls = fast.Step(At(line, 40f, speed), line, Step);

            Assert.That(slowControls.Brake, Is.GreaterThan(0f),
                "The slowest opponent should hold its lower top speed.");
            Assert.That(fastControls.Throttle, Is.GreaterThan(0f),
                "The fastest opponent should still be accelerating.");
            Assert.That(fast.EffectivePace(0f), Is.GreaterThan(slow.EffectivePace(0f)));
        }

        [Test]
        public void CatchUpOnlyAppliesWhenThePlayerIsFarAheadAndNeverPastTheCar()
        {
            RacingLine line = CreateLine();
            var follower = new RacingLineFollower(AiConfig.Default);

            Assert.That(follower.EffectivePace(0f), Is.EqualTo(AiConfig.Default.Pace).Within(0.0001f),
                "Catch-up must not fire while the player is alongside.");
            Assert.That(follower.EffectivePace(-400f), Is.EqualTo(AiConfig.Default.Pace).Within(0.0001f),
                "An opponent that leads must never be slowed to keep the Race close.");
            Assert.That(follower.EffectivePace(1000f), Is.LessThanOrEqualTo(RacingLineFollower.CatchUpCeiling),
                "Catch-up must not turn an opponent into a faster car than the player's.");

            // Just over its own pace: brakes normally, but throttles when the
            // player is a long way up the road.
            const float speed = 59f;
            AiControls alongside = follower.Step(At(line, 40f, speed), line, Step);
            AiControls chasing = follower.Step(At(line, 40f, speed, playerAhead: 400f), line, Step);

            Assert.That(alongside.Brake, Is.GreaterThan(0f));
            Assert.That(chasing.Throttle, Is.GreaterThan(0f),
                "Catch-up should let an opponent running behind the player push on.");
        }

        [Test]
        public void TheOpponentRunsThreeLapsWithoutStallingOrLeavingTheRoad()
        {
            RacingLine line = CreateLine();
            TrackPath centre = CreateCentreline();
            var follower = new RacingLineFollower(AiConfig.Default);
            var model = new ArcadeHandlingModel(HandlingConfig.Default);

            Vector3 position = line.SamplePosition(0f);
            Quaternion rotation = Quaternion.LookRotation(line.SampleDirection(0f), Vector3.up);
            var motion = new VehicleMotion();

            float previous = line.Project(position);
            float progress = 0f;
            float furthestFromCentre = 0f;
            float furthestFromLine = 0f;
            int stalledSteps = 0;
            float longestStall = 0f;
            int steps = 0;

            const int maxSteps = 50 * 240;
            for (; steps < maxSteps && progress < line.Length * 3f; steps++)
            {
                Drive(follower, model, line, ref position, ref rotation, ref motion);

                float distance = line.Project(position);
                float advance = distance - previous;
                if (advance > line.Length * 0.5f)
                {
                    advance -= line.Length;
                }
                else if (advance < -line.Length * 0.5f)
                {
                    advance += line.Length;
                }

                progress += advance;
                previous = distance;

                furthestFromCentre = Mathf.Max(
                    furthestFromCentre,
                    Mathf.Abs(centre.Project(position).LateralOffset));

                furthestFromLine = Mathf.Max(furthestFromLine, DistanceFromLine(line, position));

                bool stalled = steps > 100 && Mathf.Abs(motion.ForwardSpeed) < 1f;
                stalledSteps = stalled ? stalledSteps + 1 : 0;
                longestStall = Mathf.Max(longestStall, stalledSteps * Step);
            }

            float seconds = steps * Step;

            Assert.That(progress, Is.GreaterThanOrEqualTo(line.Length * 3f),
                $"The opponent only covered {progress:0} of {line.Length * 3f:0} metres in {seconds:0} seconds.");
            Assert.That(furthestFromCentre, Is.LessThan(TrackShape.Default.HalfWidth),
                $"The opponent reached {furthestFromCentre:0.0}m from the Centreline, past the Road.");
            Assert.That(furthestFromLine, Is.LessThan(2f),
                $"The opponent wandered {furthestFromLine:0.0}m off the Racing line instead of following it.");
            Assert.That(longestStall, Is.LessThan(2f),
                $"The opponent sat still for {longestStall:0.0} seconds.");
            Assert.That(seconds, Is.LessThan(180f),
                $"Three laps took {seconds:0} seconds, which is too slow for a Race.");
        }
    }
}

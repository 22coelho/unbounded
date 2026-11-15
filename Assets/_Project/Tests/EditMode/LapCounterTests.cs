using NUnit.Framework;
using Unbounded.Track;

namespace Unbounded.Tests.EditMode
{
    public sealed class LapCounterTests
    {
        private const float LapLength = 1000f;
        private const int CheckpointCount = 4;

        private static CheckpointRoute CreateRoute() => new CheckpointRoute(LapLength, CheckpointCount);

        private static LapCounter CreateCounter(out CheckpointRoute route)
        {
            route = CreateRoute();
            return new LapCounter(route);
        }

        private static LapUpdate DriveForward(LapCounter counter, float from, float to, float step = 1f)
        {
            LapUpdate update = LapUpdate.None;
            for (float distance = from + step; distance <= to + 0.001f; distance += step)
            {
                update = counter.Advance(distance);
            }

            return update;
        }

        [Test]
        public void CheckpointsAreOrderedGatesAtFixedFractionsOfTheLap()
        {
            CheckpointRoute route = CreateRoute();

            Assert.That(route.Count, Is.EqualTo(CheckpointCount));
            Assert.That(route.LapLength, Is.EqualTo(LapLength));

            float previous = -1f;
            for (int i = 0; i < route.Count; i++)
            {
                Assert.That(route.FractionAt(i), Is.EqualTo((i + 1f) / (CheckpointCount + 1f)).Within(0.0001f),
                    $"Checkpoint {i} is not at its fixed fraction of the Lap.");
                Assert.That(route.DistanceAt(i), Is.GreaterThan(previous),
                    "Checkpoints must be ordered along the Track.");
                previous = route.DistanceAt(i);
            }
        }

        [Test]
        public void EveryCheckpointInOrderMakesTheLineCountOneLap()
        {
            LapCounter counter = CreateCounter(out CheckpointRoute route);
            counter.Advance(0f);

            LapUpdate update = DriveForward(counter, 0f, LapLength);

            Assert.That(counter.CheckpointsPassed, Is.EqualTo(0), "The next Lap must start with no Checkpoints.");
            Assert.That(counter.Lap, Is.EqualTo(1));
            Assert.That(update.LapCompleted, Is.True, "Crossing the line with the full set must complete a Lap.");
            Assert.That(update.LapRejected, Is.False);
            Assert.That(counter.LastCheckpoint, Is.EqualTo(LapCounter.NoCheckpoint));
            Assert.That(counter.ResetDistance, Is.EqualTo(0f).Within(0.001f));
            Assert.That(route.Count, Is.EqualTo(CheckpointCount), "The route must still carry every Checkpoint.");
        }

        [Test]
        public void CrossingTheLineWithoutEveryCheckpointDoesNotCount()
        {
            LapCounter counter = CreateCounter(out CheckpointRoute route);
            counter.Advance(0f);

            // Take the first two gates, cut across the infield past the last
            // two, and rejoin the Track before the line.
            DriveForward(counter, 0f, 500f);
            counter.Advance(route.DistanceAt(3) + 50f);
            LapUpdate update = DriveForward(counter, route.DistanceAt(3) + 50f, LapLength);

            Assert.That(counter.Lap, Is.Zero);
            Assert.That(update.LapRejected, Is.True);
            Assert.That(update.LapCompleted, Is.False);
            Assert.That(counter.CheckpointsPassed, Is.Zero, "The rejected crossing must begin a fresh Lap attempt.");
            Assert.That(counter.LapAttemptValid, Is.True);
        }

        [Test]
        public void ARejectedCrossingStartsAFreshAttemptThatCanCount()
        {
            LapCounter counter = CreateCounter(out CheckpointRoute route);
            counter.Advance(0f);

            DriveForward(counter, 0f, 300f);
            counter.Advance(route.DistanceAt(3) + 50f);
            DriveForward(counter, route.DistanceAt(3) + 50f, LapLength);
            Assert.That(counter.Lap, Is.Zero);

            DriveForward(counter, 0f, LapLength);

            Assert.That(counter.Lap, Is.EqualTo(1), "A clean Lap after a rejected one must count.");
        }

        [Test]
        public void CuttingACheckpointStopsThatLapCounting()
        {
            LapCounter counter = CreateCounter(out CheckpointRoute route);
            counter.Advance(0f);

            // Leave the Track before the first Checkpoint and rejoin past it.
            // The projection jumps further than any car could drive, so the
            // gate is never taken.
            counter.Advance(route.DistanceAt(0) + 50f);
            Assert.That(counter.CheckpointsPassed, Is.Zero,
                "A cut across the infield must not pass the Checkpoint it skipped.");

            LapUpdate update = DriveForward(counter, route.DistanceAt(0) + 50f, LapLength);

            Assert.That(update.LapRejected, Is.True);
            Assert.That(counter.Lap, Is.Zero, "A Lap that cut a Checkpoint cannot count.");
        }

        [Test]
        public void ReversingOverTheLineDoesNotCountALap()
        {
            LapCounter counter = CreateCounter(out _);
            counter.Advance(0f);

            // Take every Checkpoint, then drive back across the line.
            DriveForward(counter, 0f, 850f);
            Assert.That(counter.CheckpointsPassed, Is.EqualTo(CheckpointCount));

            for (float distance = 849f; distance >= 0f; distance -= 1f)
            {
                counter.Advance(distance);
            }

            for (float distance = LapLength; distance > 990f; distance -= 1f)
            {
                counter.Advance(distance);
            }

            Assert.That(counter.Lap, Is.Zero, "A backwards crossing must never count a Lap.");
        }

        [Test]
        public void ReverseTravelCannotPassCheckpoints()
        {
            LapCounter counter = CreateCounter(out CheckpointRoute route);
            counter.Advance(0f);

            DriveForward(counter, 0f, 300f);
            Assert.That(counter.CheckpointsPassed, Is.EqualTo(1));

            for (float distance = 299f; distance >= 100f; distance -= 1f)
            {
                counter.Advance(distance);
            }

            Assert.That(counter.CheckpointsPassed, Is.EqualTo(1),
                "Driving backwards over a Checkpoint must not take it.");

            DriveForward(counter, 100f, 500f);
            Assert.That(counter.CheckpointsPassed, Is.EqualTo(2));
            Assert.That(counter.LastCheckpoint, Is.EqualTo(1));
            Assert.That(counter.ResetDistance, Is.EqualTo(route.DistanceAt(1)).Within(0.001f));
        }

        [Test]
        public void ResetReturnsToTheLastCheckpointThatWasPassed()
        {
            LapCounter counter = CreateCounter(out CheckpointRoute route);
            counter.Advance(0f);

            Assert.That(counter.LastCheckpoint, Is.EqualTo(LapCounter.NoCheckpoint));
            Assert.That(counter.ResetDistance, Is.EqualTo(0f).Within(0.001f),
                "With no Checkpoint passed, reset returns to the start/finish line.");

            DriveForward(counter, 0f, route.DistanceAt(0) + 5f);
            Assert.That(counter.LastCheckpoint, Is.Zero);
            Assert.That(counter.ResetDistance, Is.EqualTo(route.DistanceAt(0)).Within(0.001f));

            DriveForward(counter, route.DistanceAt(0) + 5f, route.DistanceAt(1) + 5f);
            Assert.That(counter.LastCheckpoint, Is.EqualTo(1));
            Assert.That(counter.ResetDistance, Is.EqualTo(route.DistanceAt(1)).Within(0.001f));
        }

        [Test]
        public void JumpingAcrossTheTrackPassesNoGates()
        {
            LapCounter counter = CreateCounter(out CheckpointRoute route);
            counter.Advance(0f);

            LapUpdate update = counter.Advance(route.DistanceAt(2));

            Assert.That(update.CheckpointPassed, Is.False);
            Assert.That(update.LapCompleted, Is.False);
            Assert.That(counter.CheckpointsPassed, Is.Zero);
            Assert.That(counter.LastCheckpoint, Is.EqualTo(LapCounter.NoCheckpoint));
        }

        [Test]
        public void MovingForwardsOverTheSeamCountsTheLineOnce()
        {
            LapCounter counter = CreateCounter(out _);
            counter.Advance(LapLength - 10f);
            counter.Advance(LapLength - 5f);

            Assert.That(counter.Lap, Is.Zero);

            LapUpdate update = counter.Advance(5f);

            Assert.That(update.LapRejected, Is.True, "Crossing the line without Checkpoints must be rejected.");
            Assert.That(counter.Lap, Is.Zero);

            counter.Advance(6f);
            Assert.That(counter.Lap, Is.Zero, "The line must be counted only once.");
        }

        [Test]
        public void ACheckpointMustHaveAPositiveCountAndLapLength()
        {
            Assert.That(() => new CheckpointRoute(0f, 4), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => new CheckpointRoute(LapLength, 0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}

using NUnit.Framework;
using Unbounded.Race;

namespace Unbounded.Tests.EditMode
{
    public sealed class StandingsTests
    {
        private static RacerProgress Racer(int laps, int checkpoints, float distance) =>
            new RacerProgress(laps, checkpoints, distance);

        [Test]
        public void CarsAreOrderedByLapsThenCheckpointsThenDistance()
        {
            var field = new[]
            {
                Racer(1, 0, 500f),
                Racer(0, 3, 900f),
                Racer(1, 2, 100f),
                Racer(1, 2, 800f)
            };

            Assert.That(Standings.Order(field), Is.EqualTo(new[] { 3, 2, 0, 1 }),
                "The field must order by Laps, then Checkpoints, then distance along the Track.");
        }

        [Test]
        public void MoreCheckpointsBeatMoreDistanceWithinTheSameLap()
        {
            var field = new[]
            {
                Racer(1, 1, 900f),
                Racer(1, 3, 100f)
            };

            Assert.That(Standings.Order(field), Is.EqualTo(new[] { 1, 0 }),
                "A car that has taken more Checkpoints is ahead even if it sits further back on the Track.");
        }

        [Test]
        public void DistanceBreaksATieOnLapsAndCheckpoints()
        {
            var field = new[]
            {
                Racer(2, 2, 40f),
                Racer(2, 2, 400f)
            };

            Assert.That(Standings.Order(field), Is.EqualTo(new[] { 1, 0 }),
                "With equal Laps and Checkpoints, the car further along the Track is ahead.");
        }

        [Test]
        public void ACompletedLapBeatsAnyNumberOfCheckpoints()
        {
            var field = new[]
            {
                Racer(0, 4, 900f),
                Racer(1, 0, 10f)
            };

            Assert.That(Standings.Order(field), Is.EqualTo(new[] { 1, 0 }),
                "A completed Lap must outweigh every Checkpoint of the next Lap.");
        }

        [Test]
        public void PassOnTrackChangesPositionWithoutACheckpoint()
        {
            var before = new[]
            {
                Racer(0, 1, 300f),
                Racer(0, 1, 320f)
            };
            var afterPass = new[]
            {
                Racer(0, 1, 330f),
                Racer(0, 1, 320f)
            };

            Assert.That(Standings.PositionOf(before, 0), Is.EqualTo(2));
            Assert.That(Standings.PositionOf(afterPass, 0), Is.EqualTo(1),
                "Passing on the Track must change the position without a Checkpoint being crossed.");
        }

        [Test]
        public void PositionOfCountsTheCarsAhead()
        {
            var field = new[]
            {
                Racer(1, 1, 500f),
                Racer(2, 0, 100f),
                Racer(1, 3, 100f),
                Racer(1, 1, 100f),
                Racer(0, 0, 700f)
            };

            Assert.That(Standings.PositionOf(field, 0), Is.EqualTo(3),
                "The player must be placed behind every car that is genuinely ahead and ahead of the rest.");
            Assert.That(Standings.PositionOf(field, 1), Is.EqualTo(1));
            Assert.That(Standings.PositionOf(field, 4), Is.EqualTo(5));
        }

        [Test]
        public void IdenticalProgressKeepsTheFieldOrder()
        {
            var field = new[]
            {
                Racer(1, 2, 300f),
                Racer(1, 2, 300f),
                Racer(1, 2, 300f)
            };

            Assert.That(Standings.Order(field), Is.EqualTo(new[] { 0, 1, 2 }),
                "Exact ties must keep their field order instead of flickering between cars.");
            Assert.That(Standings.PositionOf(field, 0), Is.EqualTo(1));
            Assert.That(Standings.PositionOf(field, 2), Is.EqualTo(3));
        }

        [Test]
        public void PositionOfAnIndexOutsideTheFieldIsZero()
        {
            var field = new[] { Racer(0, 0, 0f) };

            Assert.That(Standings.PositionOf(field, -1), Is.Zero);
            Assert.That(Standings.PositionOf(field, 1), Is.Zero);
        }

        [Test]
        public void ProgressRejectsNegativeCounts()
        {
            Assert.That(() => Racer(-1, 0, 0f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => Racer(0, -1, 0f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}

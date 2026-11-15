using NUnit.Framework;
using Unbounded.Race;

namespace Unbounded.Tests.EditMode
{
    public sealed class RaceStateTests
    {
        private const int Laps = 3;
        private const float Countdown = 3f;

        private static RaceState Create() => new RaceState(Laps, Countdown);

        /// <summary>Runs the Countdown out to its end with a single step.</summary>
        private static RaceState CreateRacing()
        {
            RaceState race = Create();
            race.Step(Countdown);
            return race;
        }

        [Test]
        public void TheCountdownHoldsTheFieldBeforeTheStart()
        {
            RaceState race = Create();

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Countdown));
            Assert.That(race.CountdownRemaining, Is.EqualTo(Countdown).Within(0.0001f));
            Assert.That(race.RaceTime, Is.Zero);
            Assert.That(race.LapsCompleted, Is.Zero);

            race.Step(1.5f);

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Countdown),
                "The Race must not start before the Countdown ends.");
            Assert.That(race.CountdownRemaining, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(race.RaceTime, Is.Zero, "The Race timer must not run during the Countdown.");
        }

        [Test]
        public void TheCountdownEndsAndStartsTheRaceTimer()
        {
            RaceState race = Create();

            race.Step(Countdown);

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Racing));
            Assert.That(race.CountdownRemaining, Is.Zero);
            Assert.That(race.RaceTime, Is.Zero, "The clock starts at zero when the Countdown ends.");

            race.Step(2.5f);

            Assert.That(race.RaceTime, Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void TimeLeftOverFromTheFinalCountdownStepCountsTowardsTheRace()
        {
            RaceState race = Create();

            race.Step(Countdown + 0.5f);

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Racing));
            Assert.That(race.RaceTime, Is.EqualTo(0.5f).Within(0.0001f),
                "The part of the step after the Countdown is racing time.");
        }

        [Test]
        public void AZeroCountdownStartsTheRaceAtOnce()
        {
            var race = new RaceState(Laps, 0f);

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Racing));
            Assert.That(race.CountdownRemaining, Is.Zero);
        }

        [Test]
        public void ALapCompletedDuringTheCountdownIsIgnored()
        {
            RaceState race = Create();

            race.CompleteLap();

            Assert.That(race.LapsCompleted, Is.Zero);
            Assert.That(race.LapTimes, Is.Empty);
            Assert.That(race.Phase, Is.EqualTo(RacePhase.Countdown));
        }

        [Test]
        public void ACompletedLapIsRecordedWithItsTime()
        {
            RaceState race = CreateRacing();

            race.Step(30f);
            race.CompleteLap();

            Assert.That(race.LapsCompleted, Is.EqualTo(1));
            Assert.That(race.LapTimes.Count, Is.EqualTo(1));
            Assert.That(race.LapTimes[0], Is.EqualTo(30f).Within(0.0001f));
            Assert.That(race.LastLapTime, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(race.BestLapTime, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(race.HasLapTimes, Is.True);
            Assert.That(race.Phase, Is.EqualTo(RacePhase.Racing));
        }

        [Test]
        public void EachLapIsTimedFromThePreviousLineCrossing()
        {
            RaceState race = CreateRacing();

            race.Step(30f);
            race.CompleteLap();
            race.Step(5f);
            race.CompleteLap();

            Assert.That(race.LapTimes.Count, Is.EqualTo(2));
            Assert.That(race.LapTimes[1], Is.EqualTo(5f).Within(0.0001f),
                "A Lap is timed from the previous crossing, not from the start of the Race.");
            Assert.That(race.LastLapTime, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(race.BestLapTime, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void TheBestLapIsTheFastestOne()
        {
            RaceState race = CreateRacing();

            race.Step(30f);
            race.CompleteLap();
            race.Step(25f);
            race.CompleteLap();
            race.Step(27f);
            race.CompleteLap();

            Assert.That(race.BestLapTime, Is.EqualTo(25f).Within(0.0001f));
            Assert.That(race.LastLapTime, Is.EqualTo(27f).Within(0.0001f));
        }

        [Test]
        public void ThreeLapsFinishTheRaceAndStopTheClock()
        {
            RaceState race = CreateRacing();

            for (int lap = 0; lap < Laps; lap++)
            {
                race.Step(30f);
                race.CompleteLap();
            }

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished));
            Assert.That(race.LapsCompleted, Is.EqualTo(Laps));
            Assert.That(race.LapTimes.Count, Is.EqualTo(Laps));

            float finishedAt = race.RaceTime;
            race.Step(10f);

            Assert.That(race.RaceTime, Is.EqualTo(finishedAt).Within(0.0001f),
                "The Race timer must stop when the Race finishes.");
        }

        [Test]
        public void LapsAfterTheFinishAreIgnored()
        {
            RaceState race = CreateRacing();

            for (int lap = 0; lap < Laps; lap++)
            {
                race.Step(30f);
                race.CompleteLap();
            }

            race.CompleteLap();

            Assert.That(race.LapsCompleted, Is.EqualTo(Laps));
            Assert.That(race.LapTimes.Count, Is.EqualTo(Laps));
            Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished));
        }

        [Test]
        public void RestartLeavesNoLapsOrTimesBehind()
        {
            RaceState race = CreateRacing();

            race.Step(20f);
            race.CompleteLap();
            race.Step(11f);
            race.CompleteLap();

            race.Restart();

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Countdown), "Restart must return to the Countdown.");
            Assert.That(race.CountdownRemaining, Is.EqualTo(Countdown).Within(0.0001f));
            Assert.That(race.RaceTime, Is.Zero);
            Assert.That(race.LapsCompleted, Is.Zero);
            Assert.That(race.LapTimes, Is.Empty);
            Assert.That(race.LastLapTime, Is.Zero);
            Assert.That(race.BestLapTime, Is.Zero);
            Assert.That(race.HasLapTimes, Is.False);
        }

        [Test]
        public void RestartFromTheFinishScreenStartsAFreshRace()
        {
            RaceState race = CreateRacing();

            for (int lap = 0; lap < Laps; lap++)
            {
                race.Step(30f);
                race.CompleteLap();
            }

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Finished));

            race.Restart();
            race.Step(Countdown);
            race.Step(15f);
            race.CompleteLap();

            Assert.That(race.Phase, Is.EqualTo(RacePhase.Racing));
            Assert.That(race.LapsCompleted, Is.EqualTo(1));
            Assert.That(race.LapTimes.Count, Is.EqualTo(1));
            Assert.That(race.LapTimes[0], Is.EqualTo(15f).Within(0.0001f));
        }

        [Test]
        public void ARaceNeedsALapAndANonNegativeCountdown()
        {
            Assert.That(() => new RaceState(0, Countdown),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => new RaceState(Laps, -0.5f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}

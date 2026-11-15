using System;
using System.Collections.Generic;

namespace Unbounded.Race
{
    /// <summary>Where a Race is in its life: held on the Grid, driving, or over.</summary>
    public enum RacePhase
    {
        /// <summary>The Countdown is running and the cars cannot move.</summary>
        Countdown,

        /// <summary>The cars are racing and the clock is running.</summary>
        Racing,

        /// <summary>The Race is over; the clock has stopped.</summary>
        Finished
    }

    /// <summary>
    /// The state of one Race: the Countdown before the start, the clock, the
    /// Laps completed, and the finish. It owns no scene objects, so it can be
    /// driven to the end in a test with explicit time steps.
    /// </summary>
    public sealed class RaceState
    {
        public const int DefaultLaps = 3;
        public const float DefaultCountdown = 3f;

        private readonly float _countdownSeconds;
        private readonly List<float> _lapTimes = new List<float>();

        private float _lapStartTime;

        public RaceState(int totalLaps = DefaultLaps, float countdownSeconds = DefaultCountdown)
        {
            if (totalLaps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(totalLaps), totalLaps,
                    "A Race needs at least one Lap.");
            }

            if (countdownSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(countdownSeconds), countdownSeconds,
                    "A Countdown cannot run backwards.");
            }

            TotalLaps = totalLaps;
            _countdownSeconds = countdownSeconds;
            Restart();
        }

        /// <summary>How many Laps make this Race.</summary>
        public int TotalLaps { get; }

        /// <summary>Whether the Race is counting down, running, or over.</summary>
        public RacePhase Phase { get; private set; }

        /// <summary>Seconds left on the Countdown; zero once the Race is running.</summary>
        public float CountdownRemaining { get; private set; }

        /// <summary>Seconds since the Countdown ended. Stops when the Race finishes.</summary>
        public float RaceTime { get; private set; }

        /// <summary>Validated Laps taken since the Race started.</summary>
        public int LapsCompleted { get; private set; }

        /// <summary>The time each validated Lap took, in the order they were driven.</summary>
        public IReadOnlyList<float> LapTimes => _lapTimes;

        /// <summary>The Lap just completed, or zero before the first one.</summary>
        public float LastLapTime { get; private set; }

        /// <summary>The fastest Lap of the Race, or zero before the first one.</summary>
        public float BestLapTime { get; private set; }

        /// <summary>Whether any Lap has been completed, so a zero time is not mistaken for one.</summary>
        public bool HasLapTimes => _lapTimes.Count > 0;

        /// <summary>
        /// Advances the Countdown or the clock. After the Countdown ends the
        /// leftover of the step is credited to the Race, so the clock starts
        /// exactly where the Countdown stopped. A finished Race ignores time.
        /// </summary>
        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (Phase == RacePhase.Countdown)
            {
                float remaining = CountdownRemaining - deltaTime;
                if (remaining > 0f)
                {
                    CountdownRemaining = remaining;
                    return;
                }

                Phase = RacePhase.Racing;
                CountdownRemaining = 0f;
                RaceTime = -remaining;
                _lapStartTime = 0f;
                return;
            }

            if (Phase == RacePhase.Racing)
            {
                RaceTime += deltaTime;
            }
        }

        /// <summary>
        /// Records a validated Lap. Passing the last Lap of the Race finishes
        /// it and stops the clock. Laps crossed outside the racing phase, such
        /// as while the Countdown is still running, are ignored.
        /// </summary>
        public void CompleteLap()
        {
            if (Phase != RacePhase.Racing)
            {
                return;
            }

            float lapTime = RaceTime - _lapStartTime;
            _lapStartTime = RaceTime;

            LastLapTime = lapTime;
            _lapTimes.Add(lapTime);
            if (_lapTimes.Count == 1 || lapTime < BestLapTime)
            {
                BestLapTime = lapTime;
            }

            LapsCompleted++;
            if (LapsCompleted >= TotalLaps)
            {
                Phase = RacePhase.Finished;
            }
        }

        /// <summary>
        /// Puts the Race back on the Grid: a fresh Countdown, a zeroed clock,
        /// and no Laps or times left from the previous Race.
        /// </summary>
        public void Restart()
        {
            Phase = _countdownSeconds > 0f ? RacePhase.Countdown : RacePhase.Racing;
            CountdownRemaining = _countdownSeconds;
            RaceTime = 0f;
            LapsCompleted = 0;
            LastLapTime = 0f;
            BestLapTime = 0f;
            _lapStartTime = 0f;
            _lapTimes.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// What one distance sample did to the Lap state. A single sample normally
    /// does nothing; it only reports when a Checkpoint or the line was crossed.
    /// </summary>
    public readonly struct LapUpdate
    {
        public LapUpdate(bool checkpointPassed, int checkpointIndex, bool lapCompleted, bool lapRejected)
        {
            CheckpointPassed = checkpointPassed;
            CheckpointIndex = checkpointIndex;
            LapCompleted = lapCompleted;
            LapRejected = lapRejected;
        }

        /// <summary>A Checkpoint was passed in order.</summary>
        public readonly bool CheckpointPassed;

        /// <summary>Which Checkpoint was passed, or -1 when none was.</summary>
        public readonly int CheckpointIndex;

        /// <summary>The Lap counted and the next Lap began.</summary>
        public readonly bool LapCompleted;

        /// <summary>The line was crossed without a full Checkpoint set.</summary>
        public readonly bool LapRejected;

        public static LapUpdate None => new LapUpdate(false, -1, false, false);

        internal LapUpdate Merge(LapUpdate other) => new LapUpdate(
            CheckpointPassed || other.CheckpointPassed,
            other.CheckpointPassed ? other.CheckpointIndex : CheckpointIndex,
            LapCompleted || other.LapCompleted,
            LapRejected || other.LapRejected);
    }

    /// <summary>
    /// Validates a Lap from successive distances along the Track. A Lap counts
    /// only when every Checkpoint was crossed in order and the car then crosses
    /// the start/finish line moving forwards. Cutting a Checkpoint, reversing
    /// over the line, or jumping across the infield cannot count a Lap.
    /// </summary>
    public sealed class LapCounter
    {
        /// <summary>No Checkpoint has been passed, so reset returns to the line.</summary>
        public const int NoCheckpoint = -1;

        /// <summary>
        /// The most ground one sample can cover and still be driving. Faster
        /// than a car at any speed, slow enough to reject a cut across the
        /// infield, where the projection jumps many metres at once.
        /// </summary>
        public const float DefaultMaxAdvance = 25f;

        private readonly struct Crossing
        {
            public Crossing(float delta, int gate)
            {
                Delta = delta;
                Gate = gate;
            }

            public readonly float Delta;
            public readonly int Gate;
        }

        private readonly CheckpointRoute _route;
        private readonly float _maxAdvance;
        private readonly List<Crossing> _crossings = new List<Crossing>();

        private bool _hasSample;
        private float _previous;
        private int _nextCheckpoint;
        private bool _attemptValid = true;
        private int _lastCheckpoint = NoCheckpoint;

        public LapCounter(CheckpointRoute route, float maxAdvance = DefaultMaxAdvance)
        {
            _route = route ?? throw new ArgumentNullException(nameof(route));
            _maxAdvance = Mathf.Max(0.001f, maxAdvance);
        }

        /// <summary>Laps completed since the counter was created.</summary>
        public int Lap { get; private set; }

        public CheckpointRoute Route => _route;

        /// <summary>How many Checkpoints the current Lap has taken in order.</summary>
        public int CheckpointsPassed => _nextCheckpoint;

        /// <summary>False once the current Lap has missed a Checkpoint.</summary>
        public bool LapAttemptValid => _attemptValid;

        /// <summary>The last Checkpoint passed, or <see cref="NoCheckpoint"/> on the line.</summary>
        public int LastCheckpoint => _lastCheckpoint;

        /// <summary>Where a reset should return the car.</summary>
        public float ResetDistance => _lastCheckpoint == NoCheckpoint ? 0f : _route.DistanceAt(_lastCheckpoint);

        /// <summary>
        /// Feeds the car's distance along the Track. Moving backwards, standing
        /// still, and jumps larger than <c>maxAdvance</c> pass no gates.
        /// </summary>
        public LapUpdate Advance(float distance)
        {
            float wrapped = _route.Wrap(distance);
            if (!_hasSample)
            {
                _hasSample = true;
                _previous = wrapped;
                return LapUpdate.None;
            }

            float forward = _route.Wrap(wrapped - _previous);
            float backward = _route.Wrap(_previous - wrapped);
            float start = _previous;
            _previous = wrapped;

            // Equal forward and backward means half a Lap apart: ambiguous, so
            // it is not driving. Too large a jump is a cut, not a crossing.
            if (forward <= 0f || forward > _maxAdvance || forward >= backward)
            {
                return LapUpdate.None;
            }

            return CrossForward(start, forward);
        }

        private LapUpdate CrossForward(float start, float forward)
        {
            _crossings.Clear();

            for (int i = 0; i < _route.Count; i++)
            {
                float delta = _route.Wrap(_route.DistanceAt(i) - start);
                if (delta > 0f && delta <= forward)
                {
                    _crossings.Add(new Crossing(delta, i));
                }
            }

            float lineDelta = _route.Wrap(-start);
            if (lineDelta > 0f && lineDelta <= forward)
            {
                _crossings.Add(new Crossing(lineDelta, _route.Count));
            }

            _crossings.Sort((left, right) => left.Delta.CompareTo(right.Delta));

            LapUpdate update = LapUpdate.None;
            foreach (Crossing crossing in _crossings)
            {
                update = update.Merge(crossing.Gate == _route.Count
                    ? CrossLine()
                    : PassCheckpoint(crossing.Gate));
            }

            return update;
        }

        private LapUpdate PassCheckpoint(int index)
        {
            if (index < _nextCheckpoint)
            {
                // Already taken this Lap; passing it again changes nothing.
                return LapUpdate.None;
            }

            if (index == _nextCheckpoint)
            {
                _nextCheckpoint++;
            }
            else
            {
                // A later gate was reached first, so an earlier Checkpoint was
                // cut and this Lap can no longer count.
                _attemptValid = false;
                _nextCheckpoint = index + 1;
            }

            _lastCheckpoint = index;
            return new LapUpdate(true, index, false, false);
        }

        private LapUpdate CrossLine()
        {
            bool completed = _attemptValid && _nextCheckpoint == _route.Count;

            _nextCheckpoint = 0;
            _attemptValid = true;
            _lastCheckpoint = NoCheckpoint;

            if (completed)
            {
                Lap++;
                return new LapUpdate(false, -1, true, false);
            }

            return new LapUpdate(false, -1, false, true);
        }
    }
}

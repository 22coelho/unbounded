using System;
using Unbounded.Core;
using Unbounded.Track;
using UnityEngine;

namespace Unbounded.App
{
    /// <summary>
    /// Projects the car onto the Track once per physics step and feeds the pure
    /// LapCounter, watches for wrong-way travel, and owns the reset. The HUD
    /// reads the result through <see cref="IRaceReadout"/>.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class LapProgressTracker : MonoBehaviour, IRaceReadout
    {
        /// <summary>How far past the start/finish line a car is placed on the Grid.</summary>
        public const float StartDistance = 1.5f;

        [SerializeField] private TrackRoot _track;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private int _totalLaps = 3;

        private readonly WrongWayDetector _wrongWay = new WrongWayDetector();
        private LapCounter _counter;

        /// <summary>A validated Lap was completed and the next one has begun.</summary>
        public event Action LapCompleted;

        /// <summary>Wires the tracker when the scene is built from the editor.</summary>
        public void Configure(TrackRoot track, Rigidbody body, int totalLaps = 3)
        {
            _track = track;
            _body = body;
            _totalLaps = Mathf.Max(1, totalLaps);
            BuildCounter();
        }

        public int CurrentLap
        {
            get
            {
                int total = Mathf.Max(1, _totalLaps);
                return _counter == null ? 1 : Mathf.Clamp(_counter.Lap + 1, 1, total);
            }
        }

        public int TotalLaps => Mathf.Max(1, _totalLaps);

        public bool IsWrongWay => _wrongWay.IsWrongWay;

        /// <summary>Validated Laps completed this Race.</summary>
        public int LapsCompleted => _counter?.Lap ?? 0;

        /// <summary>Where a reset would return the car.</summary>
        public float ResetDistance => _counter?.ResetDistance ?? 0f;

        private void Awake()
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            BuildCounter();
        }

        private void FixedUpdate()
        {
            BuildCounter();
            if (_counter == null || _track == null || _track.Path == null || _body == null)
            {
                return;
            }

            float distance = _track.Project(_body.position).Distance;
            LapUpdate update = _counter.Advance(distance);
            if (update.LapCompleted)
            {
                LapCompleted?.Invoke();
            }

            Vector3 trackForward = _track.transform.TransformDirection(
                _track.Path.SampleDirection(distance));
            Vector3 carForward = _body.rotation * Vector3.forward;

            _wrongWay.Step(
                Vector3.Dot(carForward, trackForward),
                _body.linearVelocity.magnitude,
                Time.fixedDeltaTime);
        }

        /// <summary>
        /// Returns the car to the last Checkpoint it passed, facing along the
        /// Track and at rest.
        /// </summary>
        public void ResetCar()
        {
            BuildCounter();
            PlaceOnTrack(ResetDistance);
        }

        /// <summary>
        /// Starts the Lap counter again and puts the car back on the Grid, so a
        /// restarted Race leaves no previous Laps behind. Does not reload the scene.
        /// </summary>
        public void RestartLap()
        {
            if (_track == null || _track.Checkpoints == null)
            {
                return;
            }

            _counter = new LapCounter(_track.Checkpoints);
            _wrongWay.Reset();
            PlaceOnTrack(StartDistance);
        }

        private void PlaceOnTrack(float distance)
        {
            if (_track == null || _track.Path == null || _body == null)
            {
                return;
            }

            Pose pose = _track.SamplePose(distance);

            _body.position = pose.position + Vector3.up * 0.05f;
            _body.rotation = pose.rotation;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.WakeUp();

            _wrongWay.Reset();
        }

        private void BuildCounter()
        {
            if (_counter != null || _track == null || _track.Checkpoints == null)
            {
                return;
            }

            _counter = new LapCounter(_track.Checkpoints);
        }
    }
}

using Unbounded.AI;
using Unbounded.Track;
using Unbounded.Vehicle;
using UnityEngine;

namespace Unbounded.App
{
    /// <summary>
    /// Drives one opponent. It reads the Racing line and the car's own motion,
    /// steps the pure <see cref="RacingLineFollower"/>, and feeds the result to
    /// the same <see cref="CarController"/> a human drives, so opponents share
    /// the handling model and the collision rules rather than a parallel path.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class AiDriver : MonoBehaviour
    {
        [SerializeField] private TrackRoot _track;
        [SerializeField] private CarController _car;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private LapProgressTracker _player;
        [SerializeField] private AiConfig _config = AiConfig.Default;
        [SerializeField] private float _gridDistance;
        [SerializeField] private float _gridLateral;

        private RacingLineFollower _driver;
        private LapCounter _lapCounter;
        private float _progress;
        private float _lastTrackDistance;
        private bool _gridPending;

        /// <summary>Whether driving input reaches the car; off during the Countdown and after the finish.</summary>
        public bool ControlsEnabled { get; set; }

        /// <summary>Metres covered along the Track this Race, signed from the start/finish line.</summary>
        public float Progress => _progress;

        /// <summary>Validated Laps completed this Race.</summary>
        public int LapsCompleted => _lapCounter?.Lap ?? 0;

        /// <summary>Checkpoints taken in order on the current Lap.</summary>
        public int CheckpointsPassed => _lapCounter?.CheckpointsPassed ?? 0;

        /// <summary>Metres along the current Lap at the last physics step.</summary>
        public float TrackDistance => _lastTrackDistance;

        /// <summary>Wires the driver when the scene is built from the editor.</summary>
        public void Configure(
            TrackRoot track,
            CarController car,
            Rigidbody body,
            LapProgressTracker player,
            AiConfig config,
            float gridDistance,
            float gridLateral)
        {
            _track = track;
            _car = car;
            _body = body;
            _player = player;
            _config = config;
            _gridDistance = gridDistance;
            _gridLateral = gridLateral;
            _driver = new RacingLineFollower(_config);
            ResetProgress();
        }

        /// <summary>
        /// Seeds the distance of the car's Grid slot once the Track exists, so
        /// progress is correct before the first fixed step runs.
        /// </summary>
        public void SeedGridProgress()
        {
            ResetProgress();
        }

        /// <summary>
        /// Puts the car back on its Grid slot, at rest and facing along the
        /// Track, ready for a fresh Race without a scene reload.
        /// </summary>
        public void ReturnToGrid()
        {
            if (_body != null && _track != null && _track.Path != null)
            {
                Pose pose = _track.SamplePose(_gridDistance, _gridLateral);
                _body.position = pose.position + Vector3.up * 0.05f;
                _body.rotation = pose.rotation;
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.WakeUp();
            }

            _car?.ResetNitro();
            _driver?.Reset();
            ResetProgress();
        }

        private void Awake()
        {
            if (_car == null)
            {
                _car = GetComponent<CarController>();
            }

            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            if (_driver == null)
            {
                _driver = new RacingLineFollower(_config);
            }

            ResetProgress();
        }

        private void FixedUpdate()
        {
            if (_track == null || _track.Path == null || _track.RacingLine == null ||
                _car == null || _body == null || _driver == null)
            {
                return;
            }

            if (_gridPending)
            {
                // The Track builds after this component wakes, so the Grid
                // distance can only be seeded once its path exists.
                ResetProgress();
            }

            if (!ControlsEnabled)
            {
                _car.SetInput(default);
                return;
            }

            TrackProjection projection = _track.Project(_body.position);
            TrackProgress(projection.Distance);

            if (_lapCounter == null && _track.Checkpoints != null)
            {
                _lapCounter = new LapCounter(_track.Checkpoints);
            }

            _lapCounter?.Advance(projection.Distance);

            Vector3 localVelocity = transform.InverseTransformDirection(_body.linearVelocity);
            AiControls controls = _driver.Step(
                new AiSituation
                {
                    Position = _body.position,
                    Forward = _body.rotation * Vector3.forward,
                    ForwardSpeed = localVelocity.z,
                    PlayerProgressAhead = _player != null ? _player.Progress - _progress : 0f,
                    NitroCharge = _car.NitroCharge,
                    NitroLockedOut = _car.IsNitroLockedOut
                },
                _track.RacingLine,
                Time.fixedDeltaTime);

            _car.SetInput(new HandlingInput
            {
                Throttle = controls.Throttle,
                Brake = controls.Brake,
                Steer = controls.Steer,
                Nitro = controls.Nitro
            });
        }

        /// <summary>Accumulates arc length along the Track, unwrapping at the seam.</summary>
        private void TrackProgress(float distance)
        {
            float length = _track.Path.Length;
            float advance = distance - _lastTrackDistance;

            if (advance > length * 0.5f)
            {
                advance -= length;
            }
            else if (advance < -length * 0.5f)
            {
                advance += length;
            }

            _progress += advance;
            _lastTrackDistance = distance;
        }

        private void ResetProgress()
        {
            _progress = 0f;
            _lastTrackDistance = 0f;
            _lapCounter = null;

            if (_track == null || _track.Path == null)
            {
                // The Grid slot is authored data, so the car has a place in the
                // Standings before the Track finishes building. The first
                // physics step wraps it once the path exists.
                _lastTrackDistance = _gridDistance;
                _progress = _gridDistance;
                _gridPending = true;
                return;
            }

            _gridPending = false;
            if (_track.Checkpoints != null)
            {
                _lapCounter = new LapCounter(_track.Checkpoints);
            }

            float length = _track.Path.Length;
            _lastTrackDistance = _track.Path.WrapDistance(_gridDistance);
            _progress = _lastTrackDistance > length * 0.5f ? _lastTrackDistance - length : _lastTrackDistance;
        }
    }
}

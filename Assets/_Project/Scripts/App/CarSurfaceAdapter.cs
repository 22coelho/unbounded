using Unbounded.Track;
using Unbounded.Vehicle;
using UnityEngine;

namespace Unbounded.App
{
    /// <summary>
    /// Tells the car which surface it is on. Sampling the TrackPath keeps the
    /// question off the physics loop, and keeps the Vehicle assembly free of
    /// any knowledge of the Track.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CarController))]
    public sealed class CarSurfaceAdapter : MonoBehaviour
    {
        [SerializeField] private TrackRoot _track;

        private CarController _car;

        /// <summary>Wires the adapter when the scene is built from the editor.</summary>
        public void Configure(TrackRoot track)
        {
            _track = track;
        }

        private void Awake()
        {
            _car = GetComponent<CarController>();
        }

        private void FixedUpdate()
        {
            if (_car == null || _track == null || _track.Path == null)
            {
                return;
            }

            _car.Surface = _track.SurfaceAt(transform.position) == TrackSurface.Grass
                ? HandlingSurface.Grass
                : HandlingSurface.Road;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unbounded.Vehicle
{
    /// <summary>
    /// Animates the imported wheels: every wheel rolls with the car's forward
    /// speed, and the front pair yaws with the steering angle the handling
    /// model is already applying. The scene builder places the pivots in car
    /// space, so the spin axis is the car's right and the steer axis is its up,
    /// independent of the source model's import rotation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WheelVisuals : MonoBehaviour
    {
        /// <summary>
        /// One corner: a steer pivot with a spin pivot beneath it, and whether
        /// the front pair turns it.
        /// </summary>
        [Serializable]
        public struct Wheel
        {
            [SerializeField] private Transform _steerPivot;
            [SerializeField] private Transform _spinPivot;
            [SerializeField] private bool _steers;

            public Wheel(Transform steerPivot, Transform spinPivot, bool steers)
            {
                _steerPivot = steerPivot;
                _spinPivot = spinPivot;
                _steers = steers;
            }

            public Transform SteerPivot => _steerPivot;

            public Transform SpinPivot => _spinPivot;

            public bool Steers => _steers;
        }

        [SerializeField] private CarController _car;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private Wheel[] _wheels = new Wheel[0];
        [SerializeField] private float _radius = 0.25f;

        private float _spinAngle;
        private float _steerAngle;

        /// <summary>How many corners this car's visuals drive.</summary>
        public int WheelCount => _wheels?.Length ?? 0;

        /// <summary>The measured wheel radius, in metres.</summary>
        public float Radius => _radius;

        /// <summary>The rolling angle applied to every wheel this frame, wrapped to -180..180.</summary>
        public float SpinAngleDegrees => _spinAngle;

        /// <summary>The steering angle applied to the front wheels this frame.</summary>
        public float SteerAngleDegrees => _steerAngle;

        /// <summary>The four corners, for inspection and tests.</summary>
        public IReadOnlyList<Wheel> Wheels => _wheels;

        /// <summary>Wires the visuals when the scene is built from the editor.</summary>
        public void Configure(CarController car, Rigidbody body, Wheel[] wheels, float radius)
        {
            _car = car;
            _body = body;
            _wheels = wheels ?? new Wheel[0];
            _radius = radius;
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
        }

        private void Update()
        {
            if (_car == null || _body == null || _wheels == null || _wheels.Length == 0)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(_body.linearVelocity);
            _spinAngle = WheelRoll.Advance(_spinAngle, localVelocity.z, _radius, deltaTime);
            _steerAngle = _car.LastForces.SteeringAngleDegrees;

            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel wheel = _wheels[i];

                if (wheel.SteerPivot != null)
                {
                    wheel.SteerPivot.localRotation = wheel.Steers
                        ? Quaternion.Euler(0f, _steerAngle, 0f)
                        : Quaternion.identity;
                }

                if (wheel.SpinPivot != null)
                {
                    wheel.SpinPivot.localRotation = Quaternion.Euler(_spinAngle, 0f, 0f);
                }
            }
        }
    }
}

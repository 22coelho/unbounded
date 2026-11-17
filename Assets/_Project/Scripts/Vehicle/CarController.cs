using Unbounded.Core;
using UnityEngine;

namespace Unbounded.Vehicle
{
    /// <summary>
    /// Thin adapter between the pure handling model and a Rigidbody. It reads
    /// the car's motion, steps the model on the fixed clock, and applies the
    /// resulting forces and yaw acceleration at the axles.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarController : MonoBehaviour, INitroReadout
    {
        [SerializeField] private HandlingConfig _config = HandlingConfig.Default;
        [SerializeField] private float _frontAxleOffset = 1.4f;
        [SerializeField] private float _rearAxleOffset = 1.4f;
        [SerializeField] private float _contactHeight = 0f;

        private Rigidbody _body;
        private ArcadeHandlingModel _model;
        private HandlingInput _input;

        public HandlingConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                _model = new ArcadeHandlingModel(_config);
            }
        }

        /// <summary>Set by a track-aware adapter; defaults to asphalt.</summary>
        public HandlingSurface Surface { get; set; } = HandlingSurface.Road;

        public HandlingInput CurrentInput => _input;

        public HandlingForces LastForces { get; private set; }

        public float NitroCharge => _model?.NitroCharge ?? 0f;

        public bool IsBoosting => _model != null && _model.IsNitroActive;

        public bool IsNitroLockedOut => _model != null && _model.IsNitroLockedOut;

        /// <summary>Fills the Nitro tank, for a restarted Race or a car put back on its Grid slot.</summary>
        public void ResetNitro()
        {
            _model?.ResetNitro();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _model = new ArcadeHandlingModel(_config);
        }

        public void SetInput(HandlingInput input)
        {
            _input = input;
        }

        private void FixedUpdate()
        {
            if (_body == null || _model == null)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(_body.linearVelocity);
            var motion = new VehicleMotion
            {
                ForwardSpeed = localVelocity.z,
                LateralSpeed = localVelocity.x,
                YawRate = _body.angularVelocity.y
            };

            HandlingForces forces = _model.Step(motion, _input, Surface, Time.fixedDeltaTime);
            LastForces = forces;

            _body.AddForce(transform.forward * forces.DriveForce, ForceMode.Force);

            Vector3 halfLateralForce = transform.right * (forces.LateralForce * 0.5f);
            Vector3 frontContact = transform.TransformPoint(new Vector3(0f, _contactHeight, _frontAxleOffset));
            Vector3 rearContact = transform.TransformPoint(new Vector3(0f, _contactHeight, -_rearAxleOffset));
            _body.AddForceAtPosition(halfLateralForce, frontContact, ForceMode.Force);
            _body.AddForceAtPosition(halfLateralForce, rearContact, ForceMode.Force);

            _body.AddTorque(Vector3.up * forces.YawAcceleration, ForceMode.Acceleration);
        }
    }
}

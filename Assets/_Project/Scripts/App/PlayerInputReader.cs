using Unbounded.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unbounded.App
{
    /// <summary>
    /// Reads the Drive action map and feeds the car. Kept in the composition
    /// assembly so the Vehicle assembly stays free of input-system details.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private CarController _car;

        private InputActionMap _driveMap;
        private InputAction _steer;
        private InputAction _throttle;
        private InputAction _brake;

        /// <summary>Wires the reader when the scene is built from the editor.</summary>
        public void Configure(InputActionAsset actions, CarController car)
        {
            _actions = actions;
            _car = car;
        }

        private void Awake()
        {
            if (_actions == null || _car == null)
            {
                enabled = false;
                return;
            }

            _driveMap = _actions.FindActionMap("Drive", false);
            if (_driveMap == null)
            {
                Debug.LogError("The driving action asset is missing its Drive map.", this);
                enabled = false;
                return;
            }

            _steer = _driveMap.FindAction("Steer", true);
            _throttle = _driveMap.FindAction("Throttle", true);
            _brake = _driveMap.FindAction("Brake", true);
        }

        private void OnEnable()
        {
            _driveMap?.Enable();
        }

        private void OnDisable()
        {
            _driveMap?.Disable();
            if (_car != null)
            {
                _car.SetInput(default);
            }
        }

        private void Update()
        {
            if (_car == null)
            {
                return;
            }

            _car.SetInput(new HandlingInput
            {
                Steer = Mathf.Clamp(_steer.ReadValue<float>(), -1f, 1f),
                Throttle = Mathf.Clamp01(_throttle.ReadValue<float>()),
                Brake = Mathf.Clamp01(_brake.ReadValue<float>())
            });
        }
    }
}

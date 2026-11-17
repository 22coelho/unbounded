using Unbounded.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unbounded.App
{
    /// <summary>
    /// Reads the Drive action map and feeds the car. Kept in the composition
    /// assembly so the Vehicle assembly stays free of input-system details.
    /// Driving input can be gated off, so the Countdown can hold the car and
    /// the finish or pause screens can take the controls away.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private CarController _car;
        [SerializeField] private LapProgressTracker _lapProgress;

        private InputActionMap _driveMap;
        private InputActionMap _menuMap;
        private InputAction _steer;
        private InputAction _throttle;
        private InputAction _brake;
        private InputAction _nitro;
        private InputAction _reset;
        private InputAction _pause;
        private InputAction _quit;
        private InputAction _navigate;
        private InputAction _clearBest;

        /// <summary>The car this reader drives.</summary>
        public CarController Car => _car;

        /// <summary>Whether throttle, steering, brake, and reset reach the car.</summary>
        public bool ControlsEnabled { get; set; } = true;

        /// <summary>
        /// Shapes the steering input curve without touching the handling
        /// parameters. One is the raw stick, below one is softer near centre,
        /// above one is sharper.
        /// </summary>
        public float SteeringSensitivity { get; set; } = 1f;

        /// <summary>The Pause button was pressed this frame. Read while paused too.</summary>
        public bool PausePressed { get; private set; }

        /// <summary>The Quit button was pressed this frame. Read while paused too.</summary>
        public bool QuitPressed { get; private set; }

        /// <summary>The Reset button was pressed this frame.</summary>
        public bool ResetPressed { get; private set; }

        /// <summary>Menu movement this frame: X adjusts a value, Y changes the row.</summary>
        public Vector2 MenuNavigate { get; private set; }

        /// <summary>The clear-best-Lap button was pressed this frame.</summary>
        public bool ClearBestPressed { get; private set; }

        /// <summary>Wires the reader when the scene is built from the editor.</summary>
        public void Configure(InputActionAsset actions, CarController car, LapProgressTracker lapProgress)
        {
            _actions = actions;
            _car = car;
            _lapProgress = lapProgress;
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
            _nitro = _driveMap.FindAction("Nitro", true);
            _reset = _driveMap.FindAction("Reset", true);
            _pause = _driveMap.FindAction("Pause", true);
            _quit = _driveMap.FindAction("Quit", true);

            _menuMap = _actions.FindActionMap("Menu", false);
            if (_menuMap != null)
            {
                _navigate = _menuMap.FindAction("Navigate", true);
                _clearBest = _menuMap.FindAction("ClearBest", true);
            }
        }

        private void OnEnable()
        {
            _driveMap?.Enable();
            _menuMap?.Enable();
        }

        private void OnDisable()
        {
            _driveMap?.Disable();
            _menuMap?.Disable();
            if (_car != null)
            {
                _car.SetInput(default);
            }
        }

        private void Update()
        {
            PausePressed = _pause != null && _pause.WasPressedThisFrame();
            QuitPressed = _quit != null && _quit.WasPressedThisFrame();
            ResetPressed = _reset != null && _reset.WasPressedThisFrame();
            MenuNavigate = _navigate?.ReadValue<Vector2>() ?? default;
            ClearBestPressed = _clearBest != null && _clearBest.WasPressedThisFrame();

            if (_car == null)
            {
                return;
            }

            if (!ControlsEnabled)
            {
                _car.SetInput(default);
                return;
            }

            if (ResetPressed && _lapProgress != null)
            {
                _lapProgress.ResetCar();
            }

            _car.SetInput(new HandlingInput
            {
                Steer = SteeringCurve.Apply(
                    Mathf.Clamp(_steer.ReadValue<float>(), -1f, 1f), SteeringSensitivity),
                Throttle = Mathf.Clamp01(_throttle.ReadValue<float>()),
                Brake = Mathf.Clamp01(_brake.ReadValue<float>()),
                Nitro = _nitro != null && _nitro.IsPressed()
            });
        }
    }
}

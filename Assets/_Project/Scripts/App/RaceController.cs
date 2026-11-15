using System.Globalization;
using Unbounded.Race;
using Unbounded.UI;
using UnityEngine;

namespace Unbounded.App
{
    /// <summary>
    /// Runs the Race flow around the pure <see cref="RaceState"/>: the Countdown
    /// that holds the car, the race clock, the finish screen, restart without a
    /// scene reload, and pause with resume or quit. It is the one object that
    /// knows the Race, the car's Lap progress, the drive input, and the HUD.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class RaceController : MonoBehaviour
    {
        private const float GoDisplaySeconds = 0.8f;

        [SerializeField] private LapProgressTracker _lapProgress;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private RaceHud _hud;
        [SerializeField] private int _totalLaps = RaceState.DefaultLaps;
        [SerializeField] private float _countdownSeconds = RaceState.DefaultCountdown;

        private RaceState _race;
        private RacePhase _lastPhase;
        private bool _paused;
        private float _goRemaining;

        /// <summary>The Race being run.</summary>
        public RaceState Race => _race;

        /// <summary>Whether the simulation is frozen on the pause screen.</summary>
        public bool IsPaused => _paused;

        /// <summary>Wires the flow when the scene is built from the editor.</summary>
        public void Configure(LapProgressTracker lapProgress, PlayerInputReader input, RaceHud hud,
            int totalLaps = RaceState.DefaultLaps)
        {
            _lapProgress = lapProgress;
            _input = input;
            _hud = hud;
            _totalLaps = Mathf.Max(1, totalLaps);
            BuildRace();
        }

        /// <summary>
        /// Starts the Race again from the Countdown, returning the car to the
        /// Grid and clearing every Lap and time. The scene is not reloaded.
        /// </summary>
        public void RestartRace()
        {
            if (_race == null)
            {
                return;
            }

            _race.Restart();
            _paused = false;
            _goRemaining = 0f;
            _lastPhase = _race.Phase;
            Time.timeScale = 1f;

            _lapProgress?.RestartLap();
            if (_input != null)
            {
                _input.ControlsEnabled = false;
            }

            _hud?.HideFinish();
            _hud?.SetPaused(false);
            RefreshHud();
        }

        private void Awake()
        {
            if (_lapProgress == null)
            {
                _lapProgress = FindAnyObjectByType<LapProgressTracker>();
            }

            if (_input == null)
            {
                _input = FindAnyObjectByType<PlayerInputReader>();
            }

            if (_hud == null)
            {
                _hud = FindAnyObjectByType<RaceHud>();
            }

            BuildRace();
        }

        private void OnEnable()
        {
            if (_lapProgress != null)
            {
                _lapProgress.LapCompleted += OnLapCompleted;
            }
        }

        private void OnDisable()
        {
            if (_lapProgress != null)
            {
                _lapProgress.LapCompleted -= OnLapCompleted;
            }

            if (_paused)
            {
                _paused = false;
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (_race == null)
            {
                return;
            }

            if (_input != null && _input.PausePressed && _race.Phase != RacePhase.Finished)
            {
                SetPaused(!_paused);
            }

            if (_paused)
            {
                if (_input != null && _input.QuitPressed)
                {
                    Quit();
                    return;
                }

                RefreshHud();
                return;
            }

            _race.Step(Time.deltaTime);

            if (_lastPhase == RacePhase.Countdown && _race.Phase == RacePhase.Racing)
            {
                _goRemaining = GoDisplaySeconds;
            }

            _lastPhase = _race.Phase;
            if (_goRemaining > 0f)
            {
                _goRemaining = Mathf.Max(0f, _goRemaining - Time.deltaTime);
            }

            // The same button resets to the Track while racing and restarts the
            // Race when the finish screen is up, where there is nothing to reset.
            if (_race.Phase == RacePhase.Finished && _input != null && _input.ResetPressed)
            {
                RestartRace();
                return;
            }

            if (_input != null)
            {
                _input.ControlsEnabled = _race.Phase == RacePhase.Racing;
            }

            RefreshHud();
        }

        private void OnLapCompleted()
        {
            _race?.CompleteLap();
        }

        /// <summary>
        /// Freezes or resumes the simulation. Pausing leaves the Race state
        /// untouched, so resuming continues from the same moment.
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (_race == null || paused == _paused)
            {
                return;
            }

            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;

            if (_input != null)
            {
                _input.ControlsEnabled = !paused && _race.Phase == RacePhase.Racing;
            }

            _hud?.SetPaused(paused);
        }

        private void Quit()
        {
            Time.timeScale = 1f;
            _paused = false;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildRace()
        {
            _race = new RaceState(Mathf.Max(1, _totalLaps), _countdownSeconds);
            _lastPhase = _race.Phase;
            _paused = false;
            _goRemaining = 0f;

            if (_input != null)
            {
                _input.ControlsEnabled = _race.Phase == RacePhase.Racing;
            }
        }

        private void RefreshHud()
        {
            if (_hud == null || _race == null)
            {
                return;
            }

            if (_race.Phase == RacePhase.Countdown)
            {
                int seconds = Mathf.Max(1, Mathf.CeilToInt(_race.CountdownRemaining));
                _hud.ShowCountdown(seconds.ToString(CultureInfo.InvariantCulture));
            }
            else if (_goRemaining > 0f)
            {
                _hud.ShowCountdown("GO!");
            }
            else
            {
                _hud.HideCountdown();
            }

            _hud.SetRaceTime(_race.RaceTime);
            _hud.SetPaused(_paused);

            if (_race.Phase == RacePhase.Finished)
            {
                _hud.ShowFinish(_race.RaceTime, _race.BestLapTime, _race.HasLapTimes);
            }
            else
            {
                _hud.HideFinish();
            }
        }
    }
}

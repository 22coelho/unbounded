using System;
using System.Globalization;
using Unbounded.Core;
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
        private const int PauseRowCount = 2;
        private const float PauseThreshold = 0.5f;
        private const float VolumeStep = 0.05f;
        private const float SensitivityStep = 0.05f;
        private const float PauseRepeatDelay = 0.35f;
        private const float PauseRepeatInterval = 0.1f;

        [SerializeField] private LapProgressTracker _lapProgress;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private RaceHud _hud;
        [SerializeField] private AiDriver[] _opponents = new AiDriver[0];
        [SerializeField] private int _totalLaps = RaceState.DefaultLaps;
        [SerializeField] private float _countdownSeconds = RaceState.DefaultCountdown;

        private RaceState _race;
        private RacePhase _lastPhase;
        private bool _paused;
        private float _goRemaining;
        private RacerProgress[] _field;
        private string[] _racerNames;
        private string[] _finishOrder;
        private int _playerPosition = 1;

        private GameSettings _settings = GameSettings.Default;
        private BestLapRecord _bestLap = new BestLapRecord();
        private int _pauseRow;
        private bool _pauseAxisHeld;
        private bool _pauseVerticalHeld;
        private float _pauseRepeat;

        /// <summary>The Race being run.</summary>
        public RaceState Race => _race;

        /// <summary>Whether the simulation is frozen on the pause screen.</summary>
        public bool IsPaused => _paused;

        /// <summary>Wires the flow when the scene is built from the editor.</summary>
        public void Configure(LapProgressTracker lapProgress, PlayerInputReader input, RaceHud hud,
            int totalLaps = RaceState.DefaultLaps, AiDriver[] opponents = null)
        {
            _lapProgress = lapProgress;
            _input = input;
            _hud = hud;
            _opponents = opponents ?? new AiDriver[0];
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
            _finishOrder = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            _lapProgress?.RestartLap();
            _input?.Car?.ResetNitro();
            ResetCarAudio();
            ReturnOpponentsToTheGrid();
            ApplyControls(false);

            _hud?.HideFinish();
            _hud?.SetPaused(false);
            RefreshHud();
        }

        private void Awake()
        {
            AudioListener.pause = false;
            _settings = PlayerSettingsStore.LoadSettings();
            _bestLap = new BestLapRecord(PlayerSettingsStore.LoadBestLap());

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

            ApplySettings();

            if (_opponents == null || _opponents.Length == 0)
            {
                _opponents = FindObjectsByType<AiDriver>();
            }

            // The opponents wake before the Track builds its path, so their
            // Grid distance is seeded here, once the Track is ready.
            if (_opponents != null)
            {
                foreach (AiDriver opponent in _opponents)
                {
                    if (opponent != null)
                    {
                        opponent.SeedGridProgress();
                    }
                }
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
                AudioListener.pause = false;
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

                HandlePauseMenu();
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

            ApplyControls(_race.Phase == RacePhase.Racing);

            RefreshHud();
        }

        private void OnLapCompleted()
        {
            if (_race == null)
            {
                return;
            }

            _race.CompleteLap();

            // The player's Lap event is the only one that reaches here, so a
            // faster Lap replaces the stored record and is written out at once.
            if (_race.LastLapTime > 0f && _bestLap.TryRecord(_race.LastLapTime))
            {
                PlayerSettingsStore.SaveBestLap(_bestLap.Time);
            }
        }

        private void OnApplicationQuit()
        {
            PlayerSettingsStore.SaveSettings(_settings);
            PlayerSettingsStore.Flush();
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
            AudioListener.pause = paused;

            if (paused)
            {
                _pauseAxisHeld = false;
                _pauseVerticalHeld = false;
                _pauseRepeat = 0f;
            }
            else
            {
                PlayerSettingsStore.SaveSettings(_settings);
                PlayerSettingsStore.Flush();
            }

            ApplyControls(!paused && _race.Phase == RacePhase.Racing);

            _hud?.SetPaused(paused);
        }

        private void Quit()
        {
            Time.timeScale = 1f;
            _paused = false;
            AudioListener.pause = false;
            PlayerSettingsStore.SaveSettings(_settings);
            PlayerSettingsStore.Flush();

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
            _finishOrder = null;

            BuildRacerField();
            ApplyControls(_race.Phase == RacePhase.Racing);
        }

        private void BuildRacerField()
        {
            int opponents = _opponents?.Length ?? 0;
            _field = new RacerProgress[opponents + 1];
            _racerNames = new string[opponents + 1];
            _racerNames[0] = "You";

            for (int i = 0; i < opponents; i++)
            {
                AiDriver opponent = _opponents[i];
                _racerNames[i + 1] = opponent != null ? opponent.name : "Opponent " + (i + 1);
            }

            _playerPosition = 1;
        }

        /// <summary>
        /// Reads every car's live progress into the field and works out the
        /// player's place. Read every frame, so a place changes as cars pass
        /// each other rather than only when a Checkpoint is crossed.
        /// </summary>
        private void RefreshStandings()
        {
            if (_field == null || _lapProgress == null || _race == null)
            {
                return;
            }

            _field[0] = new RacerProgress(
                Mathf.Min(_lapProgress.LapsCompleted, _race.TotalLaps),
                _lapProgress.CheckpointsPassed,
                _lapProgress.TrackDistance);

            int opponents = Mathf.Min(_opponents?.Length ?? 0, _field.Length - 1);
            for (int i = 0; i < opponents; i++)
            {
                AiDriver opponent = _opponents[i];
                _field[i + 1] = opponent == null
                    ? default
                    : new RacerProgress(
                        Mathf.Min(opponent.LapsCompleted, _race.TotalLaps),
                        opponent.CheckpointsPassed,
                        opponent.TrackDistance);
            }

            _playerPosition = Standings.PositionOf(_field, 0);
        }

        private string[] BuildFinishOrder()
        {
            int[] order = Standings.Order(_field);
            var names = new string[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                names[i] = _racerNames[order[i]];
            }

            return names;
        }

        private void ApplyControls(bool enabled)
        {
            if (_input != null)
            {
                _input.ControlsEnabled = enabled;
            }

            if (_opponents == null)
            {
                return;
            }

            foreach (AiDriver opponent in _opponents)
            {
                if (opponent != null)
                {
                    opponent.ControlsEnabled = enabled;
                }
            }
        }

        private void ReturnOpponentsToTheGrid()
        {
            if (_opponents == null)
            {
                return;
            }

            foreach (AiDriver opponent in _opponents)
            {
                if (opponent != null)
                {
                    opponent.ReturnToGrid();
                }
            }
        }

        /// <summary>
        /// Drops every car back to its idle engine note, so a restart does not
        /// carry the previous Lap's pitch into the Countdown.
        /// </summary>
        private void ResetCarAudio()
        {
            _input?.Car?.GetComponent<CarAudio>()?.ResetSound();

            if (_opponents == null)
            {
                return;
            }

            foreach (AiDriver opponent in _opponents)
            {
                if (opponent?.Car != null)
                {
                    opponent.Car.GetComponent<CarAudio>()?.ResetSound();
                }
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
            _hud.SetBestLap(_bestLap.Time, _bestLap.HasTime);
            _hud.SetPaused(_paused);

            if (_paused)
            {
                _hud.ShowPauseMenu(_settings, _pauseRow, _bestLap.HasTime, _bestLap.Time);
            }

            RefreshStandings();
            _hud.SetPosition(_playerPosition, _field.Length);

            if (_race.Phase == RacePhase.Finished)
            {
                if (_finishOrder == null)
                {
                    _finishOrder = BuildFinishOrder();
                }

                _hud.ShowFinish(_race.RaceTime, _bestLap.Time, _bestLap.HasTime, _finishOrder);
            }
            else
            {
                _hud.HideFinish();
            }
        }

        /// <summary>
        /// Moves between the two settings and nudges the selected one. The
        /// vertical axis steps the row on each push; the horizontal axis
        /// adjusts immediately and then repeats while held.
        /// </summary>
        private void HandlePauseMenu()
        {
            if (_input == null)
            {
                return;
            }

            if (_input.ClearBestPressed)
            {
                ClearBestLap();
            }

            Vector2 navigate = _input.MenuNavigate;

            if (Mathf.Abs(navigate.y) > PauseThreshold)
            {
                if (!_pauseVerticalHeld)
                {
                    _pauseVerticalHeld = true;
                    int step = navigate.y > 0f ? PauseRowCount - 1 : 1;
                    _pauseRow = (_pauseRow + step) % PauseRowCount;
                }
            }
            else
            {
                _pauseVerticalHeld = false;
            }

            if (Mathf.Abs(navigate.x) <= PauseThreshold)
            {
                _pauseAxisHeld = false;
                _pauseRepeat = 0f;
                return;
            }

            if (!_pauseAxisHeld)
            {
                _pauseAxisHeld = true;
                AdjustPauseRow(Mathf.Sign(navigate.x));
                _pauseRepeat = PauseRepeatDelay;
                return;
            }

            _pauseRepeat -= Time.unscaledDeltaTime;
            if (_pauseRepeat <= 0f)
            {
                AdjustPauseRow(Mathf.Sign(navigate.x));
                _pauseRepeat = PauseRepeatInterval;
            }
        }

        private void AdjustPauseRow(float direction)
        {
            _settings = _pauseRow == 0
                ? _settings.WithMasterVolume(_settings.MasterVolume + direction * VolumeStep)
                : _settings.WithSteeringSensitivity(
                    _settings.SteeringSensitivity + direction * SensitivityStep);

            ApplySettings();
            PlayerSettingsStore.SaveSettings(_settings);
        }

        private void ApplySettings()
        {
            AudioListener.volume = _settings.MasterVolume;

            if (_input != null)
            {
                _input.SteeringSensitivity = _settings.SteeringSensitivity;
            }
        }

        /// <summary>Drops the stored best Lap, but only when the player asks for it.</summary>
        private void ClearBestLap()
        {
            if (!_bestLap.Clear())
            {
                return;
            }

            PlayerSettingsStore.ClearBestLap();
        }
    }
}

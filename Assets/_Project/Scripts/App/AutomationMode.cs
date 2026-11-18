using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unbounded.AI;
using Unbounded.Race;
using Unbounded.Track;
using Unbounded.Vehicle;
using UnityEngine;

namespace Unbounded.App
{
    /// <summary>
    /// Unattended runs of the shipped build, enabled only by command-line flags
    /// so a normal player never sees it. It exists to make the definition of
    /// done checkable: drive the player's car with the same AI racer the
    /// opponents use, restart races without a scene reload, and log frame-time
    /// summaries, so a ten-minute soak and a gameplay capture are repeatable.
    ///
    /// -autopilot        drive the player's car with the AI racing line
    /// -fpsLog           log frame-time averages every ten seconds and at exit
    /// -sessionMinutes N quit after N minutes, so a soak run ends by itself
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class AutomationMode : MonoBehaviour
    {
        private const string AutopilotFlag = "-autopilot";
        private const string FrameLogFlag = "-fpsLog";
        private const string SessionMinutesFlag = "-sessionMinutes";
        private const string CaptureFlag = "-captureClip";
        private const string CapturePathFlag = "-capturePath";
        private const string CaptureSecondsFlag = "-captureSeconds";
        private const int CaptureFps = 30;
        private const float ReportInterval = 10f;
        private const float FinishWaitSeconds = 2.5f;

        private readonly List<float> _frameTimes = new List<float>();
        private readonly List<float> _windowTimes = new List<float>();

        private RaceController _race;
        private AiDriver _autopilot;
        private bool _logFrames;
        private bool _captureClip;
        private string _capturePath;
        private float _captureSeconds = 60f;
        private float _sessionSeconds;
        private float _reportAt;
        private float _finishWait;
        private int _racesStarted;
        private bool _finished;

        /// <summary>
        /// Installs the harness after the scene has wired itself, so every
        /// component it needs already exists and the Track has built its path.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool capture = HasFlag(args, CaptureFlag);
            bool autopilot = HasFlag(args, AutopilotFlag) || capture;
            bool logFrames = HasFlag(args, FrameLogFlag);
            float minutes = ReadMinutes(args);

            if (!autopilot && !logFrames && minutes <= 0f)
            {
                return;
            }

            var host = new GameObject("Automation Mode");
            var mode = host.AddComponent<AutomationMode>();
            mode.Open(autopilot, logFrames, minutes, capture, args);
        }

        private void Open(bool autopilot, bool logFrames, float minutes, bool capture, string[] args)
        {
            _logFrames = logFrames;
            _captureClip = capture;
            _sessionSeconds = minutes * 60f;

            if (capture)
            {
                _capturePath = ReadValue(args, CapturePathFlag) ??
                               Path.Combine(Application.persistentDataPath, "clip");
                _captureSeconds = ReadNumber(args, CaptureSecondsFlag, _captureSeconds);
            }

            // An unattended run is usually not the frontmost window, and the
            // project deliberately stops rendering in the background.
            Application.runInBackground = true;

            _race = FindAnyObjectByType<RaceController>();
            if (_race == null)
            {
                Debug.LogWarning("[automation] No RaceController in the scene; the harness is idle.");
                return;
            }

            if (!autopilot)
            {
                return;
            }

            TrackRoot track = FindAnyObjectByType<TrackRoot>();
            PlayerInputReader input = FindAnyObjectByType<PlayerInputReader>();
            LapProgressTracker lapProgress = FindAnyObjectByType<LapProgressTracker>();
            CarController car = input != null ? input.Car : FindAnyObjectByType<CarController>();
            Rigidbody body = car != null ? car.GetComponent<Rigidbody>() : null;

            if (track == null || track.Path == null || car == null || body == null || lapProgress == null)
            {
                Debug.LogWarning("[automation] The scene is missing a track or a player car; autopilot is off.");
                return;
            }

            // The player's input reader would fight the autopilot for the car,
            // so it is switched off at the component, not just gated.
            if (input != null)
            {
                input.ControlsEnabled = false;
                input.enabled = false;
            }

            _autopilot = car.gameObject.AddComponent<AiDriver>();
            _autopilot.Configure(
                track,
                car,
                body,
                lapProgress,
                AiConfig.Default,
                LapProgressTracker.StartDistance,
                0f);
            _autopilot.ControlsEnabled = false;

            Debug.Log("[automation] Autopilot on; the player's car is driving the racing line.");

            if (_captureClip)
            {
                StartCoroutine(CaptureClip());
            }
        }

        private void Update()
        {
            if (_finished)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;

            bool racing = _race != null && _race.Race != null &&
                          _race.Race.Phase == RacePhase.Racing && !_race.IsPaused;

            if (_logFrames && racing)
            {
                _frameTimes.Add(dt);
                _windowTimes.Add(dt);
                _reportAt += dt;
                if (_reportAt >= ReportInterval)
                {
                    _reportAt = 0f;
                    LogWindow();
                }
            }

            if (_autopilot != null && _race != null && _race.Race != null)
            {
                _autopilot.ControlsEnabled = racing;

                if (_race.Race.Phase == RacePhase.Finished)
                {
                    _finishWait += dt;
                    if (_finishWait >= FinishWaitSeconds)
                    {
                        RestartRace();
                    }
                }
            }

            if (_sessionSeconds > 0f)
            {
                _sessionSeconds -= dt;
                if (_sessionSeconds <= 0f)
                {
                    Finish();
                }
            }
        }

        private void RestartRace()
        {
            _finishWait = 0f;
            _racesStarted++;
            _race.RestartRace();

            // RaceController resets the player's Lap progress; the extra AI
            // driver has its own follower and stall state to forget.
            _autopilot?.ReturnToGrid();

            Debug.Log("[automation] Restarted the Race without a scene reload (" +
                      _racesStarted.ToString(CultureInfo.InvariantCulture) + " restarts).");
        }

        /// <summary>
        /// Writes a short clip as a numbered JPEG sequence, which an external
        /// encoder turns into the gameplay video linked from the README. The
        /// capture waits for the Countdown to end, then samples at a fixed 30
        /// frames per second regardless of how fast the game is running.
        /// </summary>
        private IEnumerator CaptureClip()
        {
            while (!_finished && (_race == null || _race.Race == null ||
                                  _race.Race.Phase != RacePhase.Racing))
            {
                yield return null;
            }

            if (_finished)
            {
                yield break;
            }

            // Let the field clear the Grid, so the clip opens on racing rather
            // than on the last frame of the Countdown.
            yield return new WaitForSecondsRealtime(1f);

            Directory.CreateDirectory(_capturePath);
            var frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            float interval = 1f / CaptureFps;
            float carried = 0f;
            float elapsed = 0f;
            int frames = 0;

            while (!_finished && elapsed < _captureSeconds)
            {
                yield return new WaitForEndOfFrame();

                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                carried += dt;
                if (carried < interval)
                {
                    continue;
                }

                carried %= interval;
                frame.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
                frame.Apply(false);
                File.WriteAllBytes(
                    Path.Combine(_capturePath, "frame-" + frames.ToString("0000") + ".jpg"),
                    frame.EncodeToJPG(90));
                frames++;
            }

            Destroy(frame);
            Debug.Log("[automation] Captured " + frames.ToString(CultureInfo.InvariantCulture) +
                      " frames to " + _capturePath + ".");
            Finish();
        }

        private void Finish()
        {
            _finished = true;

            if (_logFrames && _frameTimes.Count > 0)
            {
                LogSummary();
            }

            Debug.Log("[automation] Session complete after " +
                      (_racesStarted + 1).ToString(CultureInfo.InvariantCulture) +
                      " races. Raising the player log marker and quitting.");

            if (!Application.isEditor)
            {
                Application.Quit();
            }
        }

        private void LogWindow()
        {
            if (_windowTimes.Count == 0)
            {
                return;
            }

            float total = 0f;
            float worst = 0f;
            foreach (float frame in _windowTimes)
            {
                total += frame;
                worst = Mathf.Max(worst, frame);
            }

            float averageMs = total / _windowTimes.Count;
            Debug.Log("[perf] last " + ReportInterval.ToString("0") + "s of racing: " +
                      (1f / averageMs).ToString("0.0") + " fps average, " +
                      (1f / Mathf.Max(worst, 0.0001f)).ToString("0.0") + " fps worst frame.");
            _windowTimes.Clear();
        }

        private void LogSummary()
        {
            _frameTimes.Sort();
            float total = 0f;
            foreach (float frame in _frameTimes)
            {
                total += frame;
            }

            int onePercent = Mathf.Clamp(
                Mathf.FloorToInt(_frameTimes.Count * 0.99f), 0, _frameTimes.Count - 1);

            float averageMs = total / _frameTimes.Count;
            float lowMs = _frameTimes[onePercent];
            float worstMs = _frameTimes[_frameTimes.Count - 1];

            Debug.Log("[perf] session summary over " + total.ToString("0.0") + "s of racing (" +
                      _frameTimes.Count.ToString(CultureInfo.InvariantCulture) + " frames): " +
                      (1f / averageMs).ToString("0.0") + " fps average, " +
                      (1f / Mathf.Max(lowMs, 0.0001f)).ToString("0.0") + " fps 1% low, " +
                      (1f / Mathf.Max(worstMs, 0.0001f)).ToString("0.0") + " fps worst frame.");
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static float ReadMinutes(string[] args) => ReadNumber(args, SessionMinutesFlag, 0f);

        private static float ReadNumber(string[] args, string flag, float fallback)
        {
            string value = ReadValue(args, flag);
            if (value != null &&
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return Mathf.Max(0f, parsed);
            }

            return fallback;
        }

        private static string ReadValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }

            string prefix = flag + "=";
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(prefix.Length);
                }
            }

            return null;
        }
    }
}

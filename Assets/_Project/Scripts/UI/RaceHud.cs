using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unbounded.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Unbounded.UI
{
    /// <summary>
    /// The HUD: lap counter, wrong-way warning, Countdown, Race timer, finish
    /// screen, and pause screen. The lap numbers are polled through the race
    /// readout; the Race flow pushes the Countdown and overlay state in, so the
    /// HUD needs no knowledge of who computes the Race.
    /// </summary>
    public sealed class RaceHud : MonoBehaviour
    {
        private static readonly Color NitroReadyColor = new Color(0.28f, 0.82f, 1f);
        private static readonly Color NitroBoostColor = new Color(1f, 0.63f, 0.1f);
        private static readonly Color NitroEmptyColor = new Color(0.9f, 0.18f, 0.14f);

        [SerializeField] private Text _lapText;
        [SerializeField] private Text _positionText;
        [SerializeField] private Text _wrongWayText;
        [SerializeField] private Text _countdownText;
        [SerializeField] private Text _timerText;
        [SerializeField] private GameObject _finishScreen;
        [SerializeField] private Text _finishSummaryText;
        [SerializeField] private GameObject _pauseScreen;
        [SerializeField] private MonoBehaviour _readoutSource;
        [SerializeField] private RectTransform _nitroFill;
        [SerializeField] private Image _nitroFillImage;
        [SerializeField] private Text _nitroLabel;
        [SerializeField] private MonoBehaviour _nitroSource;

        private IRaceReadout _readout;
        private INitroReadout _nitro;
        private int _shownLap = -1;
        private int _shownTotal = -1;
        private int _shownPosition = -1;
        private int _shownField = -1;
        private int _shownWrongWay = -1;
        private int _shownTime = -1;
        private int _shownNitro = -1;
        private string _shownCountdown;
        private bool _finishShown;

        /// <summary>Wires the permanent HUD labels when the scene is built from the editor.</summary>
        public void Configure(Text lapText, Text positionText, Text wrongWayText)
        {
            _lapText = lapText;
            _positionText = positionText;
            _wrongWayText = wrongWayText;
            Refresh();
        }

        /// <summary>Wires the Nitro meter when the scene is built from the editor.</summary>
        public void ConfigureNitro(
            RectTransform fill, Image fillImage, Text label, MonoBehaviour nitroSource)
        {
            _nitroFill = fill;
            _nitroFillImage = fillImage;
            _nitroLabel = label;
            _nitroSource = nitroSource;
            _nitro = nitroSource as INitroReadout;
            _shownNitro = -1;
            RefreshNitro();
        }

        /// <summary>Wires the Race flow labels and overlays when the scene is built from the editor.</summary>
        public void ConfigureRace(Text countdownText, Text timerText, GameObject finishScreen,
            Text finishSummaryText, GameObject pauseScreen)
        {
            _countdownText = countdownText;
            _timerText = timerText;
            _finishScreen = finishScreen;
            _finishSummaryText = finishSummaryText;
            _pauseScreen = pauseScreen;

            if (_countdownText != null)
            {
                _countdownText.gameObject.SetActive(false);
            }

            if (_finishScreen != null)
            {
                _finishScreen.SetActive(false);
            }

            if (_pauseScreen != null)
            {
                _pauseScreen.SetActive(false);
            }
        }

        /// <summary>Points the HUD at the object that computes the race.</summary>
        public void Bind(MonoBehaviour readoutSource)
        {
            _readoutSource = readoutSource;
            _readout = readoutSource as IRaceReadout;
            _shownLap = -1;
            _shownTotal = -1;
            _shownWrongWay = -1;
            Refresh();
        }

        /// <summary>Shows the Countdown, or a start message, over the Track.</summary>
        public void ShowCountdown(string label)
        {
            if (_countdownText == null)
            {
                return;
            }

            if (!_countdownText.gameObject.activeSelf)
            {
                _countdownText.gameObject.SetActive(true);
            }

            if (label != _shownCountdown)
            {
                _shownCountdown = label;
                _countdownText.text = label;
            }
        }

        public void HideCountdown()
        {
            _shownCountdown = null;
            if (_countdownText != null && _countdownText.gameObject.activeSelf)
            {
                _countdownText.gameObject.SetActive(false);
            }
        }

        /// <summary>Shows seconds since the Countdown ended, to hundredths.</summary>
        public void SetRaceTime(float seconds)
        {
            if (_timerText == null)
            {
                return;
            }

            int hundredths = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 100f);
            if (hundredths == _shownTime)
            {
                return;
            }

            _shownTime = hundredths;
            _timerText.text = FormatTime(hundredths / 100f);
        }

        /// <summary>Shows the player's place out of the field, counting from one.</summary>
        public void SetPosition(int position, int field)
        {
            if (_positionText == null)
            {
                return;
            }

            if (position == _shownPosition && field == _shownField)
            {
                return;
            }

            _shownPosition = position;
            _shownField = field;
            _positionText.text = "POS " + position + "/" + field;
        }

        /// <summary>
        /// Shows the finish screen with the total time, the best Lap, and every
        /// car in final order. The standings are ordered by the Race flow.
        /// </summary>
        public void ShowFinish(float totalTime, float bestLapTime, bool hasBestLap,
            IReadOnlyList<string> standings)
        {
            if (_finishScreen == null || _finishSummaryText == null || _finishShown)
            {
                return;
            }

            _finishShown = true;
            _finishScreen.SetActive(true);

            var summary = new StringBuilder();
            summary.Append("TIME   ").Append(FormatTime(totalTime));
            if (hasBestLap)
            {
                summary.Append("\nBEST LAP   ").Append(FormatTime(bestLapTime));
            }

            summary.Append("\n\n");
            if (standings != null)
            {
                for (int i = 0; i < standings.Count; i++)
                {
                    summary.Append(i + 1).Append("   ").Append(standings[i].ToUpperInvariant());
                    if (i < standings.Count - 1)
                    {
                        summary.Append('\n');
                    }
                }
            }

            summary.Append("\n\nPRESS R / Y TO RESTART");
            _finishSummaryText.text = summary.ToString();
        }

        public void HideFinish()
        {
            _finishShown = false;
            if (_finishScreen != null && _finishScreen.activeSelf)
            {
                _finishScreen.SetActive(false);
            }
        }

        /// <summary>Shows or hides the pause screen.</summary>
        public void SetPaused(bool paused)
        {
            if (_pauseScreen != null && _pauseScreen.activeSelf != paused)
            {
                _pauseScreen.SetActive(paused);
            }
        }

        private void Awake()
        {
            _readout = _readoutSource as IRaceReadout;
            _nitro = _nitroSource as INitroReadout;
            if (_readout == null)
            {
                Debug.LogWarning("The HUD has no race readout to display.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_readout == null || _lapText == null || _wrongWayText == null)
            {
                return;
            }

            int lap = _readout.CurrentLap;
            int total = _readout.TotalLaps;
            if (lap != _shownLap || total != _shownTotal)
            {
                _shownLap = lap;
                _shownTotal = total;
                _lapText.text = "LAP " + lap + "/" + total;
            }

            int wrongWay = _readout.IsWrongWay ? 1 : 0;
            if (wrongWay != _shownWrongWay)
            {
                _shownWrongWay = wrongWay;
                _wrongWayText.gameObject.SetActive(wrongWay == 1);
            }

            RefreshNitro();
        }

        /// <summary>
        /// Draws the Nitro charge as a bar that drains and recharges, with a
        /// colour and label for the three states a player has to tell apart:
        /// ready, spending, and locked out after running dry.
        /// </summary>
        private void RefreshNitro()
        {
            if (_nitro == null || _nitroFill == null)
            {
                return;
            }

            float charge = Mathf.Clamp01(_nitro.NitroCharge);
            int thousandths = Mathf.RoundToInt(charge * 1000f);
            if (thousandths != _shownNitro)
            {
                _shownNitro = thousandths;
                _nitroFill.anchorMax = new Vector2(charge, 1f);
            }

            bool boosting = _nitro.IsBoosting;
            bool lockedOut = _nitro.IsNitroLockedOut;

            Color color = lockedOut ? NitroEmptyColor : (boosting ? NitroBoostColor : NitroReadyColor);
            if (_nitroFillImage != null)
            {
                _nitroFillImage.color = color;
            }

            if (_nitroLabel != null)
            {
                _nitroLabel.color = color;
                _nitroLabel.text = lockedOut
                    ? "NITRO EMPTY"
                    : (boosting ? "NITRO BOOST" : (charge >= 0.999f ? "NITRO READY" : "NITRO"));
            }
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" +
                   rest.ToString("00.00", CultureInfo.InvariantCulture);
        }
    }
}

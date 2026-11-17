using Unbounded.Vehicle;
using UnityEngine;

namespace Unbounded.App
{
    /// <summary>
    /// Makes a Nitro burst readable without looking at the meter: exhaust
    /// flames and a glow light while boosting, and a looping hiss that starts
    /// and stops with the burst. The sound is synthesised so the effect has
    /// audio before the diegetic sound pass.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class NitroBoostEffect : MonoBehaviour
    {
        [SerializeField] private CarController _car;
        [SerializeField] private GameObject _flames;
        [SerializeField] private Light _glow;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private float _volume = 0.4f;

        private float _seed;

        /// <summary>Wires the effect when the scene is built from the editor.</summary>
        public void Configure(CarController car, GameObject flames, Light glow, AudioSource audio)
        {
            _car = car;
            _flames = flames;
            _glow = glow;
            _audio = audio;
        }

        private void Awake()
        {
            if (_car == null)
            {
                _car = GetComponent<CarController>();
            }

            _seed = Random.value * 10f;

            if (_audio != null)
            {
                _audio.clip = CreateBoostClip();
                _audio.loop = true;
                _audio.playOnAwake = false;
                _audio.volume = _volume;
            }

            SetVisible(false);
        }

        private void Update()
        {
            bool boosting = _car != null && _car.IsBoosting && Time.timeScale > 0f;
            SetVisible(boosting);

            if (!boosting)
            {
                return;
            }

            if (_flames != null)
            {
                float pulse = 0.85f + 0.35f * Mathf.PerlinNoise(Time.time * 22f, _seed);
                _flames.transform.localScale = new Vector3(1f, 1f, pulse);
            }

            if (_glow != null)
            {
                _glow.intensity = 2.2f + 1.2f * Mathf.PerlinNoise(Time.time * 26f, _seed + 1f);
            }
        }

        private void SetVisible(bool boosting)
        {
            if (_flames != null && _flames.activeSelf != boosting)
            {
                _flames.SetActive(boosting);
            }

            if (_glow != null)
            {
                _glow.enabled = boosting;
            }

            if (_audio == null)
            {
                return;
            }

            if (boosting && !_audio.isPlaying)
            {
                _audio.Play();
            }
            else if (!boosting && _audio.isPlaying)
            {
                _audio.Stop();
            }
        }

        /// <summary>
        /// A short filtered-noise hiss with a low rumble under it. Noise loops
        /// without a seam, so the burst cuts in and out cleanly.
        /// </summary>
        private static AudioClip CreateBoostClip()
        {
            const int sampleRate = 44100;
            const int sampleCount = sampleRate;
            var samples = new float[sampleCount];
            var random = new System.Random(1337);
            float lowPass = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                lowPass += (white - lowPass) * 0.18f;
                float rumble = Mathf.Sin(time * Mathf.PI * 2f * 84f) * 0.25f +
                               Mathf.Sin(time * Mathf.PI * 2f * 168f) * 0.1f;
                samples[i] = Mathf.Clamp(lowPass * 0.75f + rumble, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Nitro Burst", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

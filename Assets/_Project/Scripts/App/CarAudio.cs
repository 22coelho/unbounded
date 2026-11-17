using Unbounded.Vehicle;
using UnityEngine;

namespace Unbounded.App
{
    /// <summary>
    /// Turns one car into something audible: an engine loop pitched by speed
    /// and throttle, a tyre loop that fades in with lateral slip, and a
    /// one-shot on collisions scaled by the closing speed. The clips are
    /// synthesised, so the build carries no licensed audio.
    /// Opponents play through 3D sources with a distance-muffling filter; the
    /// player's own car is centred so the chase camera never fades it out.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class CarAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float MinDistance = 3f;
        private const float MaxDistance = 60f;
        private const float ImpactCooldown = 0.08f;

        [SerializeField] private CarController _car;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private AudioSource _engine;
        [SerializeField] private AudioSource _tyre;
        [SerializeField] private AudioSource _impact;
        [SerializeField] private AudioLowPassFilter[] _distanceFilters = new AudioLowPassFilter[0];
        [SerializeField] private bool _isPlayer;

        private static AudioClip _engineClip;
        private static AudioClip _tyreClip;
        private static AudioClip _impactClip;

        private CarAudioModel _model;
        private AudioListener _listener;
        private float _filterTimer;
        private float _nextImpactAt;

        /// <summary>Whether this is the car the player drives.</summary>
        public bool IsPlayer => _isPlayer;

        /// <summary>Current engine playback pitch, for inspection in a running Race.</summary>
        public float EnginePitch => _model?.EnginePitch ?? CarAudioModel.IdlePitch;

        /// <summary>Current engine level, before the source's gain.</summary>
        public float EngineVolume => _model?.EngineVolume ?? CarAudioModel.IdleVolume;

        /// <summary>Current tyre level, before the source's gain.</summary>
        public float TyreVolume => _model?.TyreVolume ?? 0f;

        /// <summary>Closing speed of the last collision loud enough to play.</summary>
        public float LastImpactSpeed { get; private set; }

        public AudioSource EngineSource => _engine;

        public AudioSource TyreSource => _tyre;

        public AudioSource ImpactSource => _impact;

        /// <summary>Wires the audio when the scene is built from the editor.</summary>
        public void Configure(
            CarController car,
            Rigidbody body,
            AudioSource engine,
            AudioSource tyre,
            AudioSource impact,
            AudioLowPassFilter[] distanceFilters,
            bool isPlayer)
        {
            _car = car;
            _body = body;
            _engine = engine;
            _tyre = tyre;
            _impact = impact;
            _distanceFilters = distanceFilters ?? new AudioLowPassFilter[0];
            _isPlayer = isPlayer;
        }

        /// <summary>Cuts the loops back to idle and silences the tyres for a restarted Race.</summary>
        public void ResetSound()
        {
            _model?.Reset();
            ApplyLevels();
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

            _model = new CarAudioModel();

            if (_engine != null)
            {
                _engine.clip = EngineClip;
                _engine.loop = true;
                _engine.playOnAwake = false;
            }

            if (_tyre != null)
            {
                _tyre.clip = TyreClip;
                _tyre.loop = true;
                _tyre.playOnAwake = false;
            }

            if (_impact != null)
            {
                _impact.clip = ImpactClip;
                _impact.loop = false;
                _impact.playOnAwake = false;
                _impact.volume = 1f;
            }

            foreach (AudioLowPassFilter filter in _distanceFilters)
            {
                if (filter != null)
                {
                    filter.enabled = !_isPlayer;
                }
            }

            ApplyLevels();

            if (_engine != null && _engine.isActiveAndEnabled && !_engine.isPlaying)
            {
                _engine.Play();
            }
        }

        private void Update()
        {
            if (_car == null || _body == null)
            {
                return;
            }

            _model ??= new CarAudioModel();

            if (Time.timeScale <= 0f)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(_body.linearVelocity);
            _model.Step(
                localVelocity.z,
                localVelocity.x,
                _car.CurrentInput.Throttle,
                _car.Config.MaxSpeed,
                Time.deltaTime);

            ApplyLevels();
            UpdateDistanceFilter();
        }

        private void ApplyLevels()
        {
            if (_model == null)
            {
                return;
            }

            // Engine and tyres run together for most of a Lap, so both sit
            // below full scale and leave room for an impact and the Nitro hiss.
            float engineGain = _isPlayer ? 1f : 0.6f;
            float tyreGain = _isPlayer ? 1f : 0.65f;

            if (_engine != null)
            {
                _engine.pitch = _model.EnginePitch;
                _engine.volume = Mathf.Clamp01(_model.EngineVolume * engineGain);
            }

            if (_tyre == null)
            {
                return;
            }

            _tyre.pitch = _model.TyrePitch;
            _tyre.volume = Mathf.Clamp01(_model.TyreVolume * tyreGain);

            bool audible = _model.TyreVolume > 0.005f;
            if (audible && !_tyre.isPlaying)
            {
                _tyre.Play();
            }
            else if (!audible && _tyre.isPlaying)
            {
                _tyre.Stop();
            }
        }

        /// <summary>
        /// Closes down the top end of a distant opponent's loops, so the field
        /// reads as cars around the Track rather than a wall of engines. The
        /// volume rolloff itself is the 3D source's job.
        /// </summary>
        private void UpdateDistanceFilter()
        {
            if (_isPlayer || _distanceFilters == null || _distanceFilters.Length == 0)
            {
                return;
            }

            _filterTimer -= Time.deltaTime;
            if (_filterTimer > 0f)
            {
                return;
            }

            _filterTimer = 0.1f;

            if (_listener == null)
            {
                _listener = FindAnyObjectByType<AudioListener>();
            }

            if (_listener == null)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, _listener.transform.position);
            float near = Mathf.InverseLerp(MinDistance, MaxDistance, distance);
            float cutoff = Mathf.Lerp(22000f, 700f, near);

            foreach (AudioLowPassFilter filter in _distanceFilters)
            {
                if (filter != null)
                {
                    filter.cutoffFrequency = cutoff;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_impact == null || Time.time < _nextImpactAt)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            float volume = CarAudioModel.ImpactVolume(speed) * (_isPlayer ? 0.85f : 0.55f);
            if (volume <= 0.02f)
            {
                return;
            }

            _impact.pitch = CarAudioModel.ImpactPitch(speed) * Random.Range(0.92f, 1.08f);
            _impact.PlayOneShot(ImpactClip, volume);
            _nextImpactAt = Time.time + ImpactCooldown;
            LastImpactSpeed = speed;
        }

        private static AudioClip EngineClip
        {
            get
            {
                if (_engineClip == null)
                {
                    _engineClip = CreateEngineClip();
                }

                return _engineClip;
            }
        }

        private static AudioClip TyreClip
        {
            get
            {
                if (_tyreClip == null)
                {
                    _tyreClip = CreateTyreClip();
                }

                return _tyreClip;
            }
        }

        private static AudioClip ImpactClip
        {
            get
            {
                if (_impactClip == null)
                {
                    _impactClip = CreateImpactClip();
                }

                return _impactClip;
            }
        }

        /// <summary>
        /// A one-second additive engine note. Every partial completes a whole
        /// number of cycles in the clip, so the loop seam is continuous.
        /// </summary>
        private static AudioClip CreateEngineClip()
        {
            const float baseFrequency = 68f;
            const int sampleCount = SampleRate;
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / SampleRate;
                float sample = 0f;

                for (int harmonic = 1; harmonic <= 7; harmonic++)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * baseFrequency * harmonic * time) / harmonic;
                }

                // A slow beat stops the tone sounding like a test signal.
                sample *= 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * time);
                samples[i] = sample;
            }

            Normalise(samples, 0.8f);
            return MakeClip("Engine Loop", samples);
        }

        /// <summary>
        /// A two-second tyre squeal: filtered noise for the rasp and a pair of
        /// tones with a wobble for the squeal. The noise tail is crossfaded with
        /// its head, so the loop does not click.
        /// </summary>
        private static AudioClip CreateTyreClip()
        {
            const int sampleCount = SampleRate * 2;
            const int fade = 2205;
            var raw = new float[sampleCount + fade];
            var random = new System.Random(8123);
            float lowPass = 0f;

            for (int i = 0; i < raw.Length; i++)
            {
                float time = (float)i / SampleRate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                lowPass += (white - lowPass) * 0.32f;

                float wobble = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 5f * time);
                float squeal = Mathf.Sin(2f * Mathf.PI * 720f * time) * 0.32f +
                               Mathf.Sin(2f * Mathf.PI * 1080f * time) * 0.14f;

                raw[i] = lowPass * 0.55f + squeal * wobble;
            }

            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = raw[i];
            }

            // Blend the head into the noise that continues past the end, so
            // the last sample joins the first one seamlessly.
            for (int i = 0; i < fade; i++)
            {
                float blend = (float)i / fade;
                samples[i] = Mathf.Lerp(raw[sampleCount + i], raw[i], blend);
            }

            Normalise(samples, 0.75f);
            return MakeClip("Tyre Screech", samples);
        }

        /// <summary>A short thump and noise burst; pitch is varied per collision.</summary>
        private static AudioClip CreateImpactClip()
        {
            const float duration = 0.6f;
            int sampleCount = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[sampleCount];
            var random = new System.Random(4242);
            float lowPass = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / SampleRate;
                float envelope = Mathf.Exp(-time * 9f);
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                lowPass += (white - lowPass) * 0.12f;

                // A falling sine reads as mass; the noise reads as the hit.
                float thump = Mathf.Sin(2f * Mathf.PI * (95f * time - 30f * time * time));
                float metal = Mathf.Sin(2f * Mathf.PI * 240f * time) * Mathf.Exp(-time * 22f) * 0.35f;

                samples[i] = (thump * 0.8f + lowPass * 0.9f + metal) * envelope;
            }

            Normalise(samples, 0.95f);
            return MakeClip("Impact", samples);
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void Normalise(float[] samples, float peak)
        {
            float loudest = 0f;
            foreach (float sample in samples)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(sample));
            }

            if (loudest <= 0f)
            {
                return;
            }

            float scale = peak / loudest;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }
    }
}

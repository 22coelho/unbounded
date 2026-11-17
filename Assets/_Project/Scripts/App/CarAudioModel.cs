using System;

namespace Unbounded.App
{
    /// <summary>
    /// Pure mapping from a car's motion and throttle to the levels the
    /// diegetic loops should sit at. Engine and tyre levels are smoothed with
    /// separate attack and release times, so a step in speed or grip fades
    /// rather than clicking. <see cref="CarAudio"/> owns the AudioSources and
    /// turns these numbers into playback.
    /// </summary>
    public sealed class CarAudioModel
    {
        /// <summary>Engine pitch at idle, as a multiplier of the clip's base pitch.</summary>
        public const float IdlePitch = 0.8f;

        /// <summary>Engine pitch at top speed with the throttle pinned.</summary>
        public const float TopPitch = 2.4f;

        /// <summary>Engine level at idle, relative to the source's gain.</summary>
        public const float IdleVolume = 0.18f;

        /// <summary>Engine level when both speed and throttle are at their maximum.</summary>
        public const float FullEngineVolume = 0.55f;

        /// <summary>Lateral speed at which a car starts to squeal.</summary>
        public const float SlipStart = 2f;

        /// <summary>Lateral speed at which the tyres are fully lit.</summary>
        public const float SlipFull = 7f;

        /// <summary>Tyre level at full slip, relative to the source's gain.</summary>
        public const float FullTyreVolume = 0.45f;

        /// <summary>A collision slower than this is a nudge, not an impact.</summary>
        public const float ImpactMinSpeed = 2f;

        /// <summary>A collision at this speed gets the full impact one-shot.</summary>
        public const float ImpactFullSpeed = 26f;

        private const float PitchAttack = 0.07f;
        private const float PitchRelease = 0.16f;
        private const float EngineAttack = 0.06f;
        private const float EngineRelease = 0.18f;
        private const float TyreAttack = 0.04f;
        private const float TyreRelease = 0.22f;

        public CarAudioModel()
        {
            EnginePitch = IdlePitch;
            EngineVolume = IdleVolume;
            TyrePitch = 1f;
        }

        /// <summary>Playback pitch of the engine loop.</summary>
        public float EnginePitch { get; private set; }

        /// <summary>Level of the engine loop, before the source's gain.</summary>
        public float EngineVolume { get; private set; }

        /// <summary>Level of the tyre loop, before the source's gain.</summary>
        public float TyreVolume { get; private set; }

        /// <summary>Playback pitch of the tyre loop.</summary>
        public float TyrePitch { get; private set; }

        /// <summary>Whether the tyres are loud enough to be heard.</summary>
        public bool IsSliding => TyreVolume > 0.01f;

        /// <summary>
        /// Steps the audio response. Speeds are the car's local velocities in
        /// metres per second, throttle is 0 to 1, and <paramref name="deltaTime"/>
        /// is the frame time. Frame-rate independent smoothing keeps the pitch
        /// and levels continuous.
        /// </summary>
        public void Step(float forwardSpeed, float lateralSpeed, float throttle, float maxSpeed, float deltaTime)
        {
            float dt = MathF.Max(deltaTime, 0f);
            if (dt <= 0f)
            {
                return;
            }

            float speed01 = Clamp01(MathF.Abs(forwardSpeed) / MathF.Max(1f, maxSpeed));
            float gas = Clamp01(throttle);
            float load = Clamp01(speed01 * 0.7f + gas * 0.3f);

            float pitchTarget = IdlePitch + (TopPitch - IdlePitch) * load;
            EnginePitch = Smooth(EnginePitch, pitchTarget, PitchAttack, PitchRelease, dt);

            // A closed throttle at speed is still the engine turning over, so
            // the level keeps some speed weight instead of falling silent.
            float engineTarget = IdleVolume + (FullEngineVolume - IdleVolume) * (speed01 * 0.45f + gas * 0.55f);
            EngineVolume = Smooth(EngineVolume, engineTarget, EngineAttack, EngineRelease, dt);

            float slip = Clamp01((MathF.Abs(lateralSpeed) - SlipStart) / (SlipFull - SlipStart));
            float rolling = Clamp01((MathF.Abs(forwardSpeed) - 2f) / 8f);
            float tyreTarget = slip * rolling * FullTyreVolume;
            TyreVolume = Smooth(TyreVolume, tyreTarget, TyreAttack, TyreRelease, dt);

            float tyrePitchTarget = 0.95f + 0.3f * slip;
            TyrePitch = Smooth(TyrePitch, tyrePitchTarget, TyreAttack, TyreRelease, dt);
        }

        /// <summary>Returns the loops to idle, for a restarted Race.</summary>
        public void Reset()
        {
            EnginePitch = IdlePitch;
            EngineVolume = IdleVolume;
            TyreVolume = 0f;
            TyrePitch = 1f;
        }

        /// <summary>
        /// One-shot level for a collision, normalised to 0 to 1. Gentle touches
        /// are silent, medium hits are audible, and the square root curve keeps
        /// the difference between a scrape and a heavy hit readable without the
        /// top of the range dominating the mix.
        /// </summary>
        public static float ImpactVolume(float relativeSpeed)
        {
            float t = Clamp01((relativeSpeed - ImpactMinSpeed) / (ImpactFullSpeed - ImpactMinSpeed));
            return MathF.Sqrt(t);
        }

        /// <summary>Playback pitch for an impact: light taps ring higher than heavy hits.</summary>
        public static float ImpactPitch(float relativeSpeed)
        {
            float t = Clamp01((relativeSpeed - ImpactMinSpeed) / (ImpactFullSpeed - ImpactMinSpeed));
            return Lerp(1.25f, 0.82f, t);
        }

        private static float Smooth(float current, float target, float attack, float release, float dt)
        {
            float time = target > current ? attack : release;
            float blend = 1f - MathF.Exp(-dt / MathF.Max(0.0001f, time));
            return current + (target - current) * blend;
        }

        private static float Clamp01(float value) => Clamp(value, 0f, 1f);

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}

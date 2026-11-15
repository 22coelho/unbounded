using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// Decides when a car is driving against the Track direction. The warning
    /// waits for a sustained wrong-way heading so a spin does not flash it, and
    /// clears once the car has faced the right way for a moment.
    /// </summary>
    public sealed class WrongWayDetector
    {
        public const float MinimumSpeed = 3f;
        public const float WrongAlignment = -0.35f;
        public const float RightAlignment = 0.25f;
        public const float WarningDelay = 0.4f;
        public const float ClearingDelay = 0.2f;

        private float _wrongFor;
        private float _rightFor;

        public bool IsWrongWay { get; private set; }

        /// <summary>
        /// <paramref name="alignment"/> is the dot product of the car's forward
        /// and the Track direction at its position.
        /// </summary>
        public void Step(float alignment, float speed, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            bool moving = speed >= MinimumSpeed;

            if (moving && alignment <= WrongAlignment)
            {
                _rightFor = 0f;
                _wrongFor += deltaTime;
                if (_wrongFor >= WarningDelay)
                {
                    IsWrongWay = true;
                }

                return;
            }

            if (alignment >= RightAlignment || !moving)
            {
                _wrongFor = 0f;
                _rightFor += deltaTime;
                if (_rightFor >= ClearingDelay)
                {
                    IsWrongWay = false;
                }
            }
        }

        public void Reset()
        {
            _wrongFor = 0f;
            _rightFor = 0f;
            IsWrongWay = false;
        }
    }
}

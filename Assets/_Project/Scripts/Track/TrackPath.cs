using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// The closed Centreline sampled by arc length, with projection of world
    /// positions back onto it. Every distance wraps at the start/finish seam so
    /// a Lap can be measured as a single running number.
    /// </summary>
    public sealed class TrackPath
    {
        private const float MinimumKnot = 0.0001f;

        private readonly Vector3[] _points;
        private readonly float[] _cumulative;
        private readonly float[] _segmentLengths;
        private readonly Vector3[] _directions;

        private TrackPath(Vector3[] points)
        {
            _points = points;
            int count = points.Length;
            _cumulative = new float[count + 1];
            _segmentLengths = new float[count];
            _directions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 next = points[(i + 1) % count];
                Vector3 delta = next - points[i];
                float length = delta.magnitude;
                _segmentLengths[i] = length;
                _directions[i] = length > MinimumKnot ? delta / length : Vector3.forward;
                _cumulative[i + 1] = _cumulative[i] + length;
            }

            if (_cumulative[count] <= MinimumKnot)
            {
                throw new ArgumentException("A track path needs a non-zero length.", nameof(points));
            }
        }

        /// <summary>Total arc length in metres.</summary>
        public float Length => _cumulative[_points.Length];

        public static TrackPath FromControlPoints(Vector3[] controlPoints, int samplesPerControlPoint = 32)
        {
            if (controlPoints == null || controlPoints.Length < 3)
            {
                throw new ArgumentException("A closed centreline needs at least three control points.",
                    nameof(controlPoints));
            }

            int samples = Mathf.Max(2, samplesPerControlPoint);
            int controlCount = controlPoints.Length;
            var points = new List<Vector3>(controlCount * samples);

            for (int i = 0; i < controlCount; i++)
            {
                Vector3 p0 = controlPoints[(i - 1 + controlCount) % controlCount];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[(i + 1) % controlCount];
                Vector3 p3 = controlPoints[(i + 2) % controlCount];
                AppendSegment(points, p0, p1, p2, p3, samples);
            }

            return new TrackPath(points.ToArray());
        }

        /// <summary>Normalises a distance into [0, Length), wrapping at the seam.</summary>
        public float WrapDistance(float distance)
        {
            float wrapped = distance % Length;
            if (wrapped < 0f)
            {
                wrapped += Length;
            }

            return wrapped >= Length ? wrapped - Length : wrapped;
        }

        public Vector3 SamplePosition(float distance)
        {
            int segment = FindSegment(WrapDistance(distance), out float t);
            Vector3 start = _points[segment];
            Vector3 end = _points[(segment + 1) % _points.Length];
            return Vector3.Lerp(start, end, t);
        }

        public Vector3 SampleDirection(float distance)
        {
            int segment = FindSegment(WrapDistance(distance), out _);
            return _directions[segment];
        }

        public Vector3 SampleRight(float distance) =>
            Vector3.Cross(Vector3.up, SampleDirection(distance)).normalized;

        public TrackProjection Project(Vector3 position)
        {
            int count = _points.Length;
            float bestSqrDistance = float.MaxValue;
            float bestDistance = 0f;
            float bestLateral = 0f;
            Vector3 bestPoint = _points[0];

            for (int i = 0; i < count; i++)
            {
                Vector3 start = _points[i];
                Vector3 end = _points[(i + 1) % count];
                Vector3 delta = end - start;
                float lengthSquared = delta.sqrMagnitude;
                float t = lengthSquared > MinimumKnot
                    ? Mathf.Clamp01(Vector3.Dot(position - start, delta) / lengthSquared)
                    : 0f;

                Vector3 point = start + delta * t;
                float sqrDistance = (position - point).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                bestPoint = point;
                bestDistance = _cumulative[i] + _segmentLengths[i] * t;
                Vector3 right = Vector3.Cross(Vector3.up, _directions[i]).normalized;
                bestLateral = Vector3.Dot(position - point, right);
            }

            // The seam is both 0 and Length; the contract says [0, Length).
            if (bestDistance >= Length - MinimumKnot)
            {
                bestDistance = 0f;
            }

            return new TrackProjection(bestDistance, bestLateral, bestPoint);
        }

        private int FindSegment(float distance, out float t)
        {
            int low = 0;
            int high = _points.Length;
            while (low + 1 < high)
            {
                int middle = (low + high) / 2;
                if (_cumulative[middle] <= distance)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            float length = _segmentLengths[low];
            t = length > MinimumKnot ? Mathf.Clamp01((distance - _cumulative[low]) / length) : 0f;
            return low;
        }

        private static void AppendSegment(
            List<Vector3> points,
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            int samples)
        {
            float t0 = 0f;
            float t1 = t0 + Knot(p0, p1);
            float t2 = t1 + Knot(p1, p2);
            float t3 = t2 + Knot(p2, p3);

            for (int s = 0; s < samples; s++)
            {
                float t = Mathf.Lerp(t1, t2, (float)s / samples);
                points.Add(Evaluate(p0, p1, p2, p3, t0, t1, t2, t3, t));
            }
        }

        private static Vector3 Evaluate(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            float t0,
            float t1,
            float t2,
            float t3,
            float t)
        {
            Vector3 a1 = Vector3.LerpUnclamped(p0, p1, (t - t0) / (t1 - t0));
            Vector3 a2 = Vector3.LerpUnclamped(p1, p2, (t - t1) / (t2 - t1));
            Vector3 a3 = Vector3.LerpUnclamped(p2, p3, (t - t2) / (t3 - t2));
            Vector3 b1 = Vector3.LerpUnclamped(a1, a2, (t - t0) / (t2 - t0));
            Vector3 b2 = Vector3.LerpUnclamped(a2, a3, (t - t1) / (t3 - t1));
            return Vector3.LerpUnclamped(b1, b2, (t - t1) / (t2 - t1));
        }

        private static float Knot(Vector3 from, Vector3 to) =>
            Mathf.Max(MinimumKnot, Mathf.Sqrt((to - from).sqrMagnitude));
    }
}

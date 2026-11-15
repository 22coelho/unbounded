using NUnit.Framework;
using Unbounded.Track;
using UnityEngine;

namespace Unbounded.Tests.EditMode
{
    public sealed class TrackPathTests
    {
        private const float Tolerance = 0.05f;

        private static TrackPath CreatePath() => TrackPath.FromControlPoints(TrackLayout.Circuit(), 32);

        [Test]
        public void SamplingWrapsAcrossTheStartFinishSeam()
        {
            TrackPath path = CreatePath();

            Assert.That(path.Length, Is.GreaterThan(500f));

            Assert.That(Vector3.Distance(path.SamplePosition(path.Length), path.SamplePosition(0f)),
                Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(path.SamplePosition(path.Length + 17.5f), path.SamplePosition(17.5f)),
                Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(path.SamplePosition(-17.5f), path.SamplePosition(path.Length - 17.5f)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void DirectionIsContinuousAcrossTheSeam()
        {
            TrackPath path = CreatePath();

            Vector3 before = path.SampleDirection(path.Length - 0.5f);
            Vector3 after = path.SampleDirection(0.5f);

            Assert.That(Vector3.Dot(before, after), Is.GreaterThan(0.999f));
        }

        [Test]
        public void SamplingByDistanceMeasuresUniformArcLength()
        {
            TrackPath path = CreatePath();
            const float step = 5f;

            for (float start = 0f; start < path.Length; start += path.Length / 16f)
            {
                float measured = MeasuredArcLength(path, start, start + step);

                Assert.That(measured, Is.EqualTo(step).Within(step * 0.01f),
                    $"The five metres from {start}m are not five metres along the Centreline.");
            }
        }

        [Test]
        public void ProjectingCentrelineSamplesRecoversTheirDistance()
        {
            TrackPath path = CreatePath();
            const int sampleCount = 64;

            for (int i = 0; i < sampleCount; i++)
            {
                float distance = path.Length * i / sampleCount;
                TrackProjection projection = path.Project(path.SamplePosition(distance));

                Assert.That(projection.Distance, Is.EqualTo(distance).Within(Tolerance),
                    $"Projection of the sample at {distance}m returned {projection.Distance}m.");
                Assert.That(projection.LateralOffset, Is.EqualTo(0f).Within(0.01f));
            }
        }

        [Test]
        public void ProjectionIsMonotonicAlongTheCentreline()
        {
            TrackPath path = CreatePath();
            const int sampleCount = 64;
            float previous = -1f;

            for (int i = 0; i < sampleCount; i++)
            {
                float distance = path.Length * i / sampleCount;
                TrackProjection projection = path.Project(path.SamplePosition(distance));

                Assert.That(projection.Distance, Is.GreaterThan(previous),
                    $"Projection at {distance}m did not advance past {previous}m.");
                previous = projection.Distance;
            }
        }

        [Test]
        public void LateralOffsetIsSignedAndMatchesTheOffsetFromTheCentreline()
        {
            TrackPath path = CreatePath();
            const int sampleCount = 16;

            for (int i = 0; i < sampleCount; i++)
            {
                float distance = path.Length * i / sampleCount;
                Vector3 centre = path.SamplePosition(distance);
                Vector3 right = path.SampleRight(distance);

                TrackProjection toTheRight = path.Project(centre + right * 2.5f);
                TrackProjection toTheLeft = path.Project(centre - right * 3.5f);

                Assert.That(LoopGap(path, toTheRight.Distance, distance), Is.LessThan(Tolerance),
                    $"Projection of the point beside {distance}m landed at {toTheRight.Distance}m.");
                Assert.That(toTheRight.LateralOffset, Is.EqualTo(2.5f).Within(0.02f));
                Assert.That(LoopGap(path, toTheLeft.Distance, distance), Is.LessThan(Tolerance),
                    $"Projection of the point beside {distance}m landed at {toTheLeft.Distance}m.");
                Assert.That(toTheLeft.LateralOffset, Is.EqualTo(-3.5f).Within(0.02f));
            }
        }

        /// <summary>How far apart two distances are, measuring the short way round the loop.</summary>
        private static float LoopGap(TrackPath path, float a, float b)
        {
            float gap = path.WrapDistance(a - b);
            return Mathf.Min(gap, path.Length - gap);
        }

        [Test]
        public void ProjectionOfAPointBehindTheStartFinishesTheLap()
        {
            TrackPath path = CreatePath();
            Vector3 position = path.SamplePosition(3f) - path.SampleDirection(3f) * 6f;

            TrackProjection projection = path.Project(position);

            Assert.That(projection.Distance, Is.EqualTo(path.Length - 3f).Within(Tolerance));
        }

        [Test]
        public void ProjectionOfAForeignPointKeepsDistanceAndLateralConsistent()
        {
            TrackPath path = CreatePath();
            const float distance = 60f;
            Vector3 centre = path.SamplePosition(distance);
            Vector3 forward = path.SampleDirection(distance);
            Vector3 right = path.SampleRight(distance);

            TrackProjection projection = path.Project(centre + forward * 10f + right * 20f);

            Assert.That(projection.Distance, Is.EqualTo(distance + 10f).Within(0.1f));
            Assert.That(projection.LateralOffset, Is.EqualTo(20f).Within(0.1f));
        }

        [Test]
        public void ProjectionRebuildsTheClosestPointItReports()
        {
            TrackPath path = CreatePath();

            for (int i = 0; i < 48; i++)
            {
                float distance = path.Length * i / 48f;
                Vector3 point = path.SamplePosition(distance) +
                                path.SampleDirection(distance) * 3f +
                                path.SampleRight(distance) * 4f;

                TrackProjection projection = path.Project(point);
                Vector3 rebuilt = path.SamplePosition(projection.Distance) +
                                  path.SampleRight(projection.Distance) * projection.LateralOffset;

                Assert.That(Vector3.Distance(rebuilt, point), Is.LessThan(0.25f),
                    $"The projection at {distance}m does not agree with its own distance and offset.");
            }
        }

        [Test]
        public void TheAuthoredCircuitStartsOnANorthboundStraight()
        {
            TrackPath path = CreatePath();

            Assert.That(path.SampleDirection(0f).z, Is.GreaterThan(0.99f));
            Assert.That(path.SampleDirection(0f).x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(Mathf.Abs(path.SamplePosition(0f).x), Is.LessThan(0.001f));

            for (float distance = 0f; distance <= 60f; distance += 5f)
            {
                Vector3 position = path.SamplePosition(distance);

                Assert.That(Mathf.Abs(position.x), Is.LessThan(0.01f),
                    $"The start straight drifts sideways by {position.x}m at {distance}m.");
                Assert.That(position.z, Is.EqualTo(distance).Within(0.01f));
                Assert.That(path.SampleDirection(distance).z, Is.GreaterThan(0.999f),
                    $"The car would not be facing north at {distance}m.");
            }
        }

        private static float MeasuredArcLength(TrackPath path, float from, float to)
        {
            const int subdivisions = 40;
            float total = 0f;
            Vector3 previous = path.SamplePosition(from);

            for (int i = 1; i <= subdivisions; i++)
            {
                Vector3 current = path.SamplePosition(Mathf.Lerp(from, to, (float)i / subdivisions));
                total += Vector3.Distance(previous, current);
                previous = current;
            }

            return total;
        }
    }
}

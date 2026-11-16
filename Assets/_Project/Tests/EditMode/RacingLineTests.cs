using NUnit.Framework;
using Unbounded.Track;
using UnityEngine;

namespace Unbounded.Tests.EditMode
{
    public sealed class RacingLineTests
    {
        private static TrackPath CreateCentreline() => TrackPath.FromControlPoints(TrackLayout.Default(), 32);

        private static RacingLine Create() => RacingLine.FromCentreline(CreateCentreline(), TrackShape.Default);

        [Test]
        public void TheLineStaysOnTheRoadInsideTheCentreline()
        {
            TrackPath centre = CreateCentreline();
            RacingLine line = Create();
            const int samples = 240;
            float furthest = 0f;
            int cutting = 0;

            for (int i = 0; i < samples; i++)
            {
                Vector3 point = line.SamplePosition(line.Length * i / samples);
                float lateral = centre.Project(point).LateralOffset;
                furthest = Mathf.Max(furthest, Mathf.Abs(lateral));

                if (Mathf.Abs(lateral) > 1f)
                {
                    cutting++;
                }
            }

            // The line never reaches the Kerbs, let alone the Barriers.
            Assert.That(furthest, Is.LessThanOrEqualTo(TrackShape.Default.HalfWidth - RacingLine.EdgeMargin + 0.1f),
                $"The Racing line runs {furthest:0.0}m from the Centreline, past the road margin.");

            // It is a line through the corners, not a copy of the Centreline.
            Assert.That(cutting, Is.GreaterThan(samples / 4),
                "The Racing line is indistinguishable from the Centreline.");
        }

        [Test]
        public void TheLineIsActuallyShorterThanTheCentreline()
        {
            TrackPath centre = CreateCentreline();
            RacingLine line = Create();

            Assert.That(line.Length, Is.LessThan(centre.Length * 0.995f),
                $"Cutting the corners should shorten the lap ({line.Length:0} vs {centre.Length:0} metres).");
            Assert.That(line.Length, Is.GreaterThan(centre.Length * 0.9f),
                "The line should follow the Track, not cut across the infield.");
        }

        [Test]
        public void TheLineIsClosedAndSmooth()
        {
            RacingLine line = Create();
            const float step = RacingLine.SampleSpacing;

            float across = Vector3.Distance(line.SamplePosition(line.Length - step), line.SamplePosition(0f));
            Assert.That(across, Is.EqualTo(step).Within(0.2f),
                $"The line jumps {across:0.0}m at the seam instead of closing.");

            Assert.That(Vector3.Distance(line.SamplePosition(line.Length + 12f), line.SamplePosition(12f)),
                Is.LessThan(0.001f), "Sampling past the seam must wrap.");

            for (float distance = 0f; distance < line.Length; distance += step)
            {
                float dot = Vector3.Dot(
                    line.SampleDirection(distance),
                    line.SampleDirection(distance + step));
                // The hairpin is genuinely tight, so allow the line to turn
                // hard there; a kink or a reversed segment still fails.
                Assert.That(dot, Is.GreaterThan(0.93f),
                    $"The line kinks at {distance:0}m (direction dot {dot:0.00}).");
            }
        }

        [Test]
        public void TheLineTakesTheInsideOfTheHairpin()
        {
            TrackPath centre = CreateCentreline();
            RacingLine line = Create();

            float tightest = 0f;
            float at = 0f;
            float side = 0f;
            for (float distance = 0f; distance < centre.Length; distance += 2f)
            {
                Vector3 before = centre.SampleDirection(distance - 4f);
                Vector3 after = centre.SampleDirection(distance + 4f);
                float angle = Mathf.Atan2(
                    Vector3.Dot(Vector3.Cross(before, after), Vector3.up),
                    Vector3.Dot(before, after));

                if (Mathf.Abs(angle) > tightest)
                {
                    tightest = Mathf.Abs(angle);
                    at = distance;
                    side = Mathf.Sign(angle);
                }
            }

            Vector3 apex = centre.SamplePosition(at);
            Vector3 onLine = line.SamplePosition(line.Project(apex));
            TrackProjection projection = centre.Project(onLine);

            Assert.That(tightest, Is.GreaterThan(0.3f), "The circuit should have a real corner to line up for.");
            Assert.That(projection.Distance, Is.EqualTo(at).Within(15f),
                "The closest point on the line to the hairpin should be the hairpin.");
            Assert.That(Mathf.Abs(projection.LateralOffset), Is.GreaterThan(2f),
                "The line should be well off the Centreline at the hairpin.");
            Assert.That(Mathf.Sign(projection.LateralOffset), Is.EqualTo(side),
                "The line should cut to the inside of the hairpin, not the outside.");
        }
    }
}

using NUnit.Framework;
using Unbounded.Track;

namespace Unbounded.Tests.EditMode
{
    public sealed class TrackSurfaceClassifierTests
    {
        [Test]
        public void SurfacesClassifyRoadKerbsAndGrass()
        {
            TrackShape shape = TrackShape.Default;

            Assert.That(TrackSurfaceClassifier.Classify(0f, shape), Is.EqualTo(TrackSurface.Road));
            Assert.That(TrackSurfaceClassifier.Classify(shape.HalfWidth - 0.1f, shape),
                Is.EqualTo(TrackSurface.Road));
            Assert.That(TrackSurfaceClassifier.Classify(-(shape.HalfWidth + shape.KerbWidth * 0.5f), shape),
                Is.EqualTo(TrackSurface.Kerb));
            Assert.That(TrackSurfaceClassifier.Classify(shape.BarrierOffset + 1f, shape),
                Is.EqualTo(TrackSurface.Grass));
        }
    }
}

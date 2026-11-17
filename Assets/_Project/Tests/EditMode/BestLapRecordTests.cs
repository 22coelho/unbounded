using NUnit.Framework;
using Unbounded.Race;

namespace Unbounded.Tests.EditMode
{
    public sealed class BestLapRecordTests
    {
        [Test]
        public void AStoredTimeIsCarriedInAndCountsAsARecord()
        {
            var record = new BestLapRecord(75.5f);

            Assert.That(record.HasTime, Is.True);
            Assert.That(record.Time, Is.EqualTo(75.5f));
        }

        [Test]
        public void OnlyAFasterLapReplacesTheRecord()
        {
            var record = new BestLapRecord();

            Assert.That(record.TryRecord(80f), Is.True);
            Assert.That(record.TryRecord(90f), Is.False);
            Assert.That(record.TryRecord(80f), Is.False);
            Assert.That(record.TryRecord(70f), Is.True);
            Assert.That(record.Time, Is.EqualTo(70f));
        }

        [Test]
        public void InvalidLapsAreNotRecords()
        {
            var record = new BestLapRecord();

            Assert.That(record.TryRecord(0f), Is.False);
            Assert.That(record.TryRecord(-3f), Is.False);
            Assert.That(record.TryRecord(float.NaN), Is.False);
            Assert.That(record.TryRecord(float.PositiveInfinity), Is.False);
            Assert.That(record.HasTime, Is.False);
        }

        [Test]
        public void ACorruptStoredTimeIsIgnored()
        {
            Assert.That(new BestLapRecord(-1f).HasTime, Is.False);
            Assert.That(new BestLapRecord(float.NaN).HasTime, Is.False);
        }

        [Test]
        public void ClearEmptiesTheRecordOnlyOnce()
        {
            var record = new BestLapRecord(65f);

            Assert.That(record.Clear(), Is.True);
            Assert.That(record.HasTime, Is.False);
            Assert.That(record.Time, Is.EqualTo(0f));
            Assert.That(record.Clear(), Is.False);
        }
    }
}

using System;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Deterministic.Tests
{
    public sealed class CanonicalByteWriterTests
    {
        [Test]
        public void MixedPrimitives_MatchCanonicalLittleEndianGoldenBytes()
        {
            var buffer = new byte[35];
            var writer = new CanonicalByteWriter(buffer);

            writer.WriteByte(0xAB);
            writer.WriteBoolean(false);
            writer.WriteBoolean(true);
            writer.WriteInt32(unchecked((int)0x89ABCDEFu));
            writer.WriteUInt32(0x01234567u);
            writer.WriteInt64(unchecked((long)0xFEDCBA9876543210UL));
            writer.WriteUInt64(0x0123456789ABCDEFUL);
            writer.WriteFp(fp.FromRaw(unchecked((long)0x8877665544332211UL)));

            byte[] expected =
            {
                0xAB,
                0x00,
                0x01,
                0xEF, 0xCD, 0xAB, 0x89,
                0x67, 0x45, 0x23, 0x01,
                0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE,
                0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01,
                0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
            };

            Assert.That(writer.WrittenCount, Is.EqualTo(expected.Length));
            Assert.That(writer.RemainingCapacity, Is.Zero);
            AssertWrittenBytes(writer, expected);
        }

        [Test]
        public void SignedBoundaries_PreserveTwosComplementBits()
        {
            var writer = new CanonicalByteWriter(new byte[12]);

            writer.WriteInt32(int.MinValue);
            writer.WriteInt64(long.MinValue);

            AssertWrittenBytes(
                writer,
                new byte[]
                {
                    0x00, 0x00, 0x00, 0x80,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80,
                });
        }

        [Test]
        public void Reset_ReusesCallerBufferFromOffsetZero()
        {
            var buffer = new byte[8];
            var writer = new CanonicalByteWriter(buffer);

            writer.WriteUInt64(ulong.MaxValue);
            writer.Reset();
            writer.WriteUInt32(0x12345678u);

            ArraySegment<byte> segment = writer.GetWrittenSegment();
            Assert.That(segment.Array, Is.SameAs(buffer));
            Assert.That(segment.Offset, Is.Zero);
            Assert.That(segment.Count, Is.EqualTo(4));
            Assert.That(writer.Capacity, Is.EqualTo(buffer.Length));
            Assert.That(writer.RemainingCapacity, Is.EqualTo(4));
            AssertWrittenBytes(writer, new byte[] { 0x78, 0x56, 0x34, 0x12 });
        }

        [Test]
        public void InsufficientCapacity_LeavesCursorAndBufferUnchanged()
        {
            var buffer = new byte[] { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC };
            var writer = new CanonicalByteWriter(buffer);
            writer.WriteByte(0x11);
            var expectedBuffer = (byte[])buffer.Clone();

            Assert.Throws<InvalidOperationException>(() => writer.WriteUInt64(0x0102030405060708UL));

            Assert.That(writer.WrittenCount, Is.EqualTo(1));
            Assert.That(buffer, Is.EqualTo(expectedBuffer));
            AssertWrittenBytes(writer, new byte[] { 0x11 });
        }

        [Test]
        public void NullBuffer_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new CanonicalByteWriter(null));
        }

        private static void AssertWrittenBytes(CanonicalByteWriter writer, byte[] expected)
        {
            ArraySegment<byte> segment = writer.GetWrittenSegment();
            var actual = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, actual, 0, segment.Count);

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}

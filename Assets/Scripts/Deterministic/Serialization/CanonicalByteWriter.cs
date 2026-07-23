using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Deterministic
{
    /// <summary>
    /// Writes deterministic primitive values into caller-owned fixed-capacity storage.
    /// </summary>
    public sealed class CanonicalByteWriter
    {
        private readonly byte[] buffer;
        private int writtenCount;

        public CanonicalByteWriter(byte[] buffer)
        {
            this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public int Capacity => buffer.Length;

        public int WrittenCount => writtenCount;

        public int RemainingCapacity => buffer.Length - writtenCount;

        public ArraySegment<byte> GetWrittenSegment()
        {
            return new ArraySegment<byte>(buffer, 0, writtenCount);
        }

        public void Reset()
        {
            writtenCount = 0;
        }

        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            buffer[writtenCount] = value;
            writtenCount++;
        }

        public void WriteBoolean(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteInt32(int value)
        {
            WriteUInt32(unchecked((uint)value));
        }

        public void WriteUInt32(uint value)
        {
            EnsureCapacity(4);

            int offset = writtenCount;
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            writtenCount = offset + 4;
        }

        public void WriteInt64(long value)
        {
            WriteUInt64(unchecked((ulong)value));
        }

        public void WriteUInt64(ulong value)
        {
            EnsureCapacity(8);

            int offset = writtenCount;
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            buffer[offset + 4] = (byte)(value >> 32);
            buffer[offset + 5] = (byte)(value >> 40);
            buffer[offset + 6] = (byte)(value >> 48);
            buffer[offset + 7] = (byte)(value >> 56);
            writtenCount = offset + 8;
        }

        public void WriteFp(fp value)
        {
            WriteInt64(value.RawValue);
        }

        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteInt32(-1);
                return;
            }
            WriteInt32(value.Length);
            for (int i = 0; i < value.Length; i++)
                WriteUInt32(value[i]);
        }

        private void EnsureCapacity(int byteCount)
        {
            if (RemainingCapacity < byteCount)
            {
                throw new InvalidOperationException(
                    $"The canonical byte buffer needs {byteCount} bytes but only {RemainingCapacity} remain.");
            }
        }
    }
}

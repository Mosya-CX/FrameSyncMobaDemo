using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public enum CrowdControlParamType : byte
    {
        Byte = 0,
        Short = 1,
        Int = 2,
        Long = 3,
        Bool = 4,
        Fp = 5,
        UnitUid = 6,
        Fp2 = 7,
        Mask32 = 8,
        Mask64 = 9,
    }

    public static class CrowdControlParamTypes
    {
        public static int GetSize(CrowdControlParamType type)
        {
            switch (type)
            {
                case CrowdControlParamType.Byte:
                case CrowdControlParamType.Bool:
                    return 1;
                case CrowdControlParamType.Short:
                    return 2;
                case CrowdControlParamType.Int:
                case CrowdControlParamType.Mask32:
                    return 4;
                case CrowdControlParamType.Long:
                case CrowdControlParamType.Fp:
                case CrowdControlParamType.Mask64:
                    return 8;
                case CrowdControlParamType.UnitUid:
                    return 12;
                case CrowdControlParamType.Fp2:
                    return 16;
                default:
                    return 0;
            }
        }

        public static int GetAlignment(CrowdControlParamType type)
        {
            switch (type)
            {
                case CrowdControlParamType.Byte:
                case CrowdControlParamType.Bool:
                    return 1;
                case CrowdControlParamType.Short:
                    return 2;
                case CrowdControlParamType.Int:
                case CrowdControlParamType.Mask32:
                case CrowdControlParamType.UnitUid:
                    return 4;
                default:
                    return 8;
            }
        }
    }

    /// <summary>One compiled parameter slot inside a Definition (CC v6.2 4.7).</summary>
    [System.Serializable]
    public struct CrowdControlParamLayoutEntry
    {
        // CrowdControlParamKey is a readonly struct per CC v6.2 4.5; Unity's
        // serializer drops fields of readonly-struct type, so the stable key
        // value is serialized as a plain uint and re-wrapped on access.
        [UnityEngine.SerializeField]
        private uint keyValue;

        public CrowdControlParamKey Key
        {
            get => new CrowdControlParamKey(keyValue);
            set => keyValue = value.Value;
        }

        public CrowdControlParamType Type;
        public int Offset;
        public int Size;
    }

    /// <summary>
    /// Baked parameter layout of one Definition. Offsets are assigned by the
    /// baker with per-type alignment; runtime reads use compiled offsets.
    /// </summary>
    public struct CrowdControlParamLayout
    {
        public CrowdControlParamLayoutEntry[] Entries;
        public byte RequiredMask;
        public int TotalSize;

        public bool IsValid => Entries != null;

        public bool TryGet(
            CrowdControlParamKey key,
            out CrowdControlParamLayoutEntry entry)
        {
            CrowdControlParamLayoutEntry[] entries =
                Entries;
            if (entries != null)
            {
                for (int i = 0;
                     i < entries.Length;
                     i++)
                {
                    if (entries[i].Key == key)
                    {
                        entry = entries[i];
                        return true;
                    }
                }
            }
            entry = default;
            return false;
        }
    }

    /// <summary>
    /// Project-owned fixed 64-byte block (CC v6.2 4.9). Stored as 8 x ulong;
    /// byte offsets are little-endian. No unsafe, no allocation.
    /// </summary>
    public struct FixedBytes64
    {
        private ulong w0;
        private ulong w1;
        private ulong w2;
        private ulong w3;
        private ulong w4;
        private ulong w5;
        private ulong w6;
        private ulong w7;

        public void Clear()
        {
            w0 = 0; w1 = 0; w2 = 0; w3 = 0;
            w4 = 0; w5 = 0; w6 = 0; w7 = 0;
        }

        private ulong GetWord(int offset)
        {
            switch (offset >> 3)
            {
                case 0: return w0;
                case 1: return w1;
                case 2: return w2;
                case 3: return w3;
                case 4: return w4;
                case 5: return w5;
                case 6: return w6;
                default: return w7;
            }
        }

        private void SetWord(int offset, ulong value)
        {
            switch (offset >> 3)
            {
                case 0: w0 = value; break;
                case 1: w1 = value; break;
                case 2: w2 = value; break;
                case 3: w3 = value; break;
                case 4: w4 = value; break;
                case 5: w5 = value; break;
                case 6: w6 = value; break;
                default: w7 = value; break;
            }
        }

        public byte ReadByte(int offset)
        {
            return (byte)((GetWord(offset) >>
                ((offset & 7) << 3)) & 0xFFUL);
        }

        public void WriteByte(int offset, byte value)
        {
            int shift = (offset & 7) << 3;
            ulong word = GetWord(offset);
            word &= ~(0xFFUL << shift);
            word |= (ulong)value << shift;
            SetWord(offset, word);
        }

        public short ReadShort(int offset)
        {
            return (short)ReadUInt16(offset);
        }

        public ushort ReadUInt16(int offset)
        {
            int shift = (offset & 7) << 3;
            if (shift <= 48)
            {
                return (ushort)((GetWord(offset) >>
                    shift) & 0xFFFFUL);
            }
            ulong lo = GetWord(offset) >> shift;
            ulong hi = GetWord(offset + 8) << (64 - shift);
            return (ushort)((lo | hi) & 0xFFFFUL);
        }

        public void WriteUInt16(int offset, ushort value)
        {
            int shift = (offset & 7) << 3;
            if (shift <= 48)
            {
                ulong word = GetWord(offset);
                word &= ~(0xFFFFUL << shift);
                word |= (ulong)value << shift;
                SetWord(offset, word);
                return;
            }
            ulong lowWord = GetWord(offset);
            ulong highWord = GetWord(offset + 8);
            lowWord &= ~(0xFFFFUL << shift);
            lowWord |= (ulong)value << shift;
            SetWord(offset, lowWord);
            int highShift = shift - 64;
            highWord &= ~(0xFFFFUL >> (16 - highShift));
            highWord |= (ulong)value >> (16 - highShift);
            SetWord(offset + 8, highWord);
        }

        public int ReadInt32(int offset) =>
            unchecked((int)ReadUInt32(offset));

        public uint ReadUInt32(int offset)
        {
            int shift = (offset & 7) << 3;
            if (shift <= 32)
            {
                return (uint)((GetWord(offset) >>
                    shift) & 0xFFFFFFFFUL);
            }
            ulong lo = GetWord(offset) >> shift;
            ulong hi = GetWord(offset + 8) << (64 - shift);
            return (uint)((lo | hi) & 0xFFFFFFFFUL);
        }

        public void WriteUInt32(int offset, uint value)
        {
            int shift = (offset & 7) << 3;
            if (shift <= 32)
            {
                ulong word = GetWord(offset);
                word &= ~(0xFFFFFFFFUL << shift);
                word |= (ulong)value << shift;
                SetWord(offset, word);
                return;
            }
            ulong lowWord = GetWord(offset);
            ulong highWord = GetWord(offset + 8);
            lowWord &= ~(0xFFFFFFFFUL << shift);
            lowWord |= (ulong)value << shift;
            SetWord(offset, lowWord);
            int highShift = shift - 64;
            highWord &= ~(0xFFFFFFFFUL >> (32 - highShift));
            highWord |= (ulong)value >> (32 - highShift);
            SetWord(offset + 8, highWord);
        }

        public long ReadInt64(int offset)
        {
            int shift = (offset & 7) << 3;
            if (shift == 0)
            {
                return unchecked((long)GetWord(offset));
            }
            ulong lo = GetWord(offset) >> shift;
            ulong hi = GetWord(offset + 8) << (64 - shift);
            return unchecked((long)(lo | hi));
        }

        public void WriteInt64(int offset, long value)
        {
            int shift = (offset & 7) << 3;
            if (shift == 0)
            {
                SetWord(offset, unchecked((ulong)value));
                return;
            }
            ulong lowWord = GetWord(offset);
            ulong highWord = GetWord(offset + 8);
            lowWord &= ~(0xFFFFFFFFFFFFFFFFUL << shift);
            lowWord |= unchecked((ulong)value) << shift;
            SetWord(offset, lowWord);
            int highShift = shift - 64;
            highWord &= ~(0xFFFFFFFFFFFFFFFFUL >> (64 - highShift));
            highWord |= unchecked((ulong)value) >> (64 - highShift);
            SetWord(offset + 8, highWord);
        }

        public bool ReadBool(int offset) =>
            ReadByte(offset) != 0;

        public void WriteBool(int offset, bool value) =>
            WriteByte(offset, value ? (byte)1 : (byte)0);

        public fp ReadFp(int offset) =>
            fp.FromRaw(ReadInt64(offset));

        public void WriteFp(int offset, fp value) =>
            WriteInt64(offset, value.RawValue);

        public fp2 ReadFp2(int offset) =>
            new fp2(
                fp.FromRaw(ReadInt64(offset)),
                fp.FromRaw(ReadInt64(offset + 8)));

        public void WriteFp2(int offset, in fp2 value)
        {
            WriteInt64(offset, value.x.RawValue);
            WriteInt64(offset + 8, value.y.RawValue);
        }

        public UnitUid ReadUnitUid(int offset)
        {
            return new UnitUid(
                ReadInt32(offset),
                ReadInt32(offset + 4),
                (byte)ReadUInt16(offset + 8));
        }

        public void WriteUnitUid(
            int offset,
            UnitUid value)
        {
            WriteUInt32(offset,
                unchecked((uint)value.SpawnLogicTick));
            WriteUInt32(offset + 4,
                unchecked(
                    (uint)value.RuntimeEntityPrefabId));
            WriteByte(offset + 8,
                value.SpawnSequenceInTick);
        }

        public void WriteInt32(
            int offset,
            int value)
        {
            WriteUInt32(offset,
                unchecked((uint)value));
        }
    }

    /// <summary>
    /// Fixed 64-byte parameter payload of a control instance (CC v6.2 4.9).
    /// </summary>
    public struct CrowdControlParamBlock
    {
        public FixedBytes64 Data;

        public void Clear() => Data.Clear();
        public byte ReadByte(int offset) => Data.ReadByte(offset);
        public short ReadShort(int offset) => Data.ReadShort(offset);
        public int ReadInt(int offset) => Data.ReadInt32(offset);
        public long ReadLong(int offset) => Data.ReadInt64(offset);
        public bool ReadBool(int offset) => Data.ReadBool(offset);
        public fp ReadFp(int offset) => Data.ReadFp(offset);
        public fp2 ReadFp2(int offset) => Data.ReadFp2(offset);
        public UnitUid ReadUnitUid(int offset) => Data.ReadUnitUid(offset);
        public uint ReadMask32(int offset) => Data.ReadUInt32(offset);
        public ulong ReadMask64(int offset) => unchecked((ulong)Data.ReadInt64(offset));
    }

    /// <summary>
    /// Short-lived value-type parameter writer (CC v6.2 4.8). Max 8 entries,
    /// each up to 16 bytes. Explicit typed setters avoid boxing and keep the
    /// type registry local.
    /// </summary>
    public struct CrowdControlParamWriter
    {
        private struct Entry
        {
            public CrowdControlParamKey Key;
            public CrowdControlParamType Type;
            public byte Size;
            public ulong Lo;
            public ulong Hi;
        }

        public const int MaxEntries = 8;

        private Entry e0;
        private Entry e1;
        private Entry e2;
        private Entry e3;
        private Entry e4;
        private Entry e5;
        private Entry e6;
        private Entry e7;
        private int count;

        public int Count => count;

        private int FindOrAdd(
            CrowdControlParamKey key,
            CrowdControlParamType type,
            int size)
        {
            for (int i = 0; i < count; i++)
            {
                if (GetEntry(i).Key == key)
                {
                    return i;
                }
            }
            if (count >= MaxEntries)
            {
                throw new System.InvalidOperationException(
                    "CrowdControlParamWriter entry limit exceeded.");
            }
            var entry = new Entry
            {
                Key = key,
                Type = type,
                Size = (byte)size,
            };
            SetEntry(count, in entry);
            count++;
            return count - 1;
        }

        private Entry GetEntry(int index)
        {
            switch (index)
            {
                case 0: return e0;
                case 1: return e1;
                case 2: return e2;
                case 3: return e3;
                case 4: return e4;
                case 5: return e5;
                case 6: return e6;
                default: return e7;
            }
        }

        private void SetEntry(int index, in Entry entry)
        {
            switch (index)
            {
                case 0: e0 = entry; break;
                case 1: e1 = entry; break;
                case 2: e2 = entry; break;
                case 3: e3 = entry; break;
                case 4: e4 = entry; break;
                case 5: e5 = entry; break;
                case 6: e6 = entry; break;
                default: e7 = entry; break;
            }
        }

        public void SetByte(
            CrowdControlParamKey key,
            byte value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.Byte, 1);
            Entry entry = GetEntry(index);
            entry.Lo = value;
            SetEntry(index, in entry);
        }

        public void SetShort(
            CrowdControlParamKey key,
            short value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.Short, 2);
            Entry entry = GetEntry(index);
            entry.Lo = unchecked((ushort)value);
            SetEntry(index, in entry);
        }

        public void SetInt(
            CrowdControlParamKey key,
            int value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.Int, 4);
            Entry entry = GetEntry(index);
            entry.Lo = unchecked((uint)value);
            SetEntry(index, in entry);
        }

        public void SetLong(
            CrowdControlParamKey key,
            long value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.Long, 8);
            Entry entry = GetEntry(index);
            entry.Lo = unchecked((ulong)value);
            SetEntry(index, in entry);
        }

        public void SetBool(
            CrowdControlParamKey key,
            bool value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.Bool, 1);
            Entry entry = GetEntry(index);
            entry.Lo = value ? 1UL : 0UL;
            SetEntry(index, in entry);
        }

        public void SetFp(
            CrowdControlParamKey key,
            fp value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.Fp, 8);
            Entry entry = GetEntry(index);
            entry.Lo = unchecked((ulong)value.RawValue);
            SetEntry(index, in entry);
        }

        public void SetUnitUid(
            CrowdControlParamKey key,
            UnitUid value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.UnitUid, 12);
            Entry entry = GetEntry(index);
            entry.Lo = unchecked(
                (uint)value.SpawnLogicTick);
            entry.Hi = unchecked(
                (uint)value.RuntimeEntityPrefabId) |
                ((ulong)value.SpawnSequenceInTick << 32);
            SetEntry(index, in entry);
        }

        public void SetFp2(
            CrowdControlParamKey key,
            in fp2 value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.Fp2, 16);
            Entry entry = GetEntry(index);
            entry.Lo = unchecked((ulong)value.x.RawValue);
            entry.Hi = unchecked((ulong)value.y.RawValue);
            SetEntry(index, in entry);
        }

        public void SetMask32(
            CrowdControlParamKey key,
            uint value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.Mask32, 4);
            Entry entry = GetEntry(index);
            entry.Lo = value;
            SetEntry(index, in entry);
        }

        public void SetMask64(
            CrowdControlParamKey key,
            ulong value)
        {
            int index = FindOrAdd(
                key, CrowdControlParamType.Mask64, 8);
            Entry entry = GetEntry(index);
            entry.Lo = value;
            SetEntry(index, in entry);
        }

        /// <summary>
        /// Materialize this writer into a Definition's ParamLayout
        /// (CC v6.2 4.9). Returns false on unknown key, type/size mismatch
        /// or missing required key.
        /// </summary>
        public bool Materialize(
            in CrowdControlParamLayout layout,
            out CrowdControlParamBlock block)
        {
            block = default;
            block.Clear();
            if (!layout.IsValid)
            {
                return false;
            }

            byte requiredMask = layout.RequiredMask;
            for (int i = 0; i < count; i++)
            {
                Entry entry = GetEntry(i);
                if (!layout.TryGet(
                        entry.Key,
                        out CrowdControlParamLayoutEntry slot))
                {
                    return false;
                }
                if (slot.Type != entry.Type ||
                    slot.Size != entry.Size ||
                    slot.Offset + slot.Size > 64)
                {
                    return false;
                }

                WriteEntry(
                    ref block,
                    entry,
                    slot.Offset);
                if (slot.Size > 0 &&
                    slot.Offset + slot.Size <= 64)
                {
                    // mark required slot as written (key index <= 8)
                    for (int k = 0;
                         k < layout.Entries.Length;
                         k++)
                    {
                        if (layout.Entries[k].Key ==
                            entry.Key &&
                            (layout.RequiredMask &
                             (1 << k)) != 0)
                        {
                            requiredMask &= (byte)~(1 << k);
                        }
                    }
                }
            }

            return requiredMask == 0;
        }

        private static void WriteEntry(
            ref CrowdControlParamBlock block,
            in Entry entry,
            int offset)
        {
            switch (entry.Type)
            {
                case CrowdControlParamType.Byte:
                    block.Data.WriteByte(
                        offset, (byte)entry.Lo);
                    break;
                case CrowdControlParamType.Short:
                    block.Data.WriteUInt16(
                        offset, (ushort)entry.Lo);
                    break;
                case CrowdControlParamType.Int:
                case CrowdControlParamType.Mask32:
                    block.Data.WriteUInt32(
                        offset, (uint)entry.Lo);
                    break;
                case CrowdControlParamType.Long:
                case CrowdControlParamType.Mask64:
                    block.Data.WriteInt64(
                        offset, unchecked((long)entry.Lo));
                    break;
                case CrowdControlParamType.Bool:
                    block.Data.WriteBool(
                        offset, entry.Lo != 0);
                    break;
                case CrowdControlParamType.Fp:
                    block.Data.WriteFp(
                        offset, fp.FromRaw(
                            unchecked((long)entry.Lo)));
                    break;
                case CrowdControlParamType.UnitUid:
                    block.Data.WriteUnitUid(
                        offset,
                        new UnitUid(
                            unchecked((int)entry.Lo),
                            unchecked((int)(uint)entry.Hi),
                            (byte)(entry.Hi >> 32)));
                    break;
                case CrowdControlParamType.Fp2:
                    block.Data.WriteFp2(
                        offset,
                        new fp2(
                            fp.FromRaw(unchecked(
                                (long)entry.Lo)),
                            fp.FromRaw(unchecked(
                                (long)entry.Hi))));
                    break;
                default:
                    break;
            }
        }
    }
}

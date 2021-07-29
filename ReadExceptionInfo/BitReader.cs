using System;
using System.Diagnostics;

namespace ArcticFox.Codec.Binary
{
    public ref partial struct BitReader
    {        
        private readonly ReadOnlySpan<byte> m_data;
        public int m_dataOffset { get; private set; }
        public int m_dataLength => m_data.Length;

        private byte m_bitValue;
        public byte m_bitPositionInByte { get; private set; }

        private bool m_notReadingBits => m_bitPositionInByte == 8;

        public byte m_bitsRemainingInCurrentByte => m_bitPositionInByte == 8 ? (byte)8 : (byte)(8 - m_bitPositionInByte);
        public uint m_fullBitOffset => (uint)m_dataOffset * 8u - (m_notReadingBits ? 0u : m_bitsRemainingInCurrentByte);

        public BitReader(ReadOnlySpan<byte> data)
        {
            m_data = data;
            m_dataOffset = 0;
            m_bitValue = 0;
            m_bitPositionInByte = 8;
        }

        private void ClearBit()
        {
            m_bitPositionInByte = 8;
        }

        public byte ReadByte()
        {
            ClearBit();
            return m_data[m_dataOffset++];
        }

        public sbyte ReadSByte() => (sbyte) ReadByte();

        public void ReadBytesTo(Span<byte> output, int count)
        {
            var readSpan = ReadBytes(count);
            readSpan.CopyTo(output);
        }
        
        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            ClearBit();
            var span = m_data.Slice(m_dataOffset, count);
            m_dataOffset += count;
            return span;
        }

        private void FetchNextBits()
        {
            m_bitValue = ReadByte();
            m_bitPositionInByte = 0;
        }

        public bool ReadBit()
        {
            if (m_notReadingBits) FetchNextBits();

            var value = (m_bitValue & (1 << m_bitPositionInByte)) != 0;
            
            m_bitPositionInByte++;
            return value;
        }

        public byte ReadBitsFromCurrent(byte count)
        {
            if (m_notReadingBits) FetchNextBits();

            var remainingBits = 8 - m_bitPositionInByte;
            Debug.Assert(remainingBits >= count);

            var remainingBitsMask = (byte)(0b11111111 << m_bitPositionInByte);
            var bitsToReadMask = (byte) (0b11111111 >> (byte)(remainingBits - count));
            var val = (byte)(m_bitValue & remainingBitsMask & bitsToReadMask);
            
            val >>= m_bitPositionInByte;
            m_bitPositionInByte += count;
            
            Debug.Assert(m_bitPositionInByte <= 8);

            return val;
        }

        public unsafe T ReadBits<T>(uint bitCount) where T : unmanaged
        {
            Debug.Assert(sizeof(T) * 8 >= bitCount);

            var obj = new T();

            var objSpan = new Span<byte>((byte*)&obj, sizeof(T));
            var writer = new BitWriter(objSpan);

            writer.WriteBits(ref this, bitCount);
            writer.FlushBit();

            return obj;
        }

        public void SkipBytes(int count)
        {
            ClearBit();
            m_dataOffset += count;
        }
    }
}
using System;
using System.Diagnostics;

namespace ArcticFox.Codec.Binary
{
    public ref partial struct BitWriter
    {
        public readonly Span<byte> m_output;
        public int m_dataOffset { get; private set; }

        private byte m_bitValue;
        public byte m_bitPositionInByte { get; private set; }

        public int m_effectiveByteOffset => m_dataOffset + (m_bitPositionInByte != 0 ? 1 : 0); // consider pending bits
        public byte m_bitsRemainingInCurrentByte => m_bitPositionInByte == 8 ? (byte)8 : (byte)(8 - m_bitPositionInByte);
        public uint m_fullBitOffset => (uint)m_dataOffset * 8u + m_bitPositionInByte;

        public BitWriter(Span<byte> output)
        {
            m_output = output;
            m_dataOffset = 0;
            m_bitValue = 0;
            m_bitPositionInByte = 0;
        }

        public void WriteBit(bool bit)
        {
            if (bit)
            {
                m_bitValue |= (byte) (1 << m_bitPositionInByte);
            }
            m_bitPositionInByte++;
            
            if (m_bitPositionInByte == 8) FlushBit();
        }

        public void WriteByte(byte value)
        {
            FlushBit();
            m_output[m_dataOffset++] = value;
        }

        public void WriteSByte(sbyte value) => WriteByte((byte)value); 

        private void WriteBitsToCurrent(byte bits, byte bitCount)
        {
            var remainingBitsThisByte = (byte)(8 - m_bitPositionInByte);
            Debug.Assert(remainingBitsThisByte >= bitCount);
            
            // todo: assumes caller is sane and doesn't pass garbage bits. could mask using bitcount
            
            m_bitValue |= (byte)(bits << m_bitPositionInByte);
            m_bitPositionInByte += bitCount;
            
            if (m_bitPositionInByte == 8) FlushBit();
        }

        public unsafe void WriteBits<T>(T obj, uint bitCount) where T : unmanaged
        {
            Debug.Assert(sizeof(T) * 8 >= bitCount);

            var objSpan = new Span<byte>((byte*)&obj, sizeof(T));
            var reader = new BitReader(objSpan);
            WriteBits(ref reader, bitCount);
        }
        
        public void WriteBits(ref BitReader buffer, uint bitCount)
        {
            while (bitCount > 0)
            {
                var bitsCanRead = buffer.m_bitsRemainingInCurrentByte;
                var bitsCanWrite = m_bitsRemainingInCurrentByte;

                var bitsThisOperation = (byte)Math.Min(bitCount, Math.Min(bitsCanRead, bitsCanWrite));
                Debug.Assert(bitsThisOperation != 0);

                if (bitsThisOperation == 8)
                {
                    var bytesThisOperation = bitCount >> 3;
                    buffer.ReadBytesTo(m_output.Slice(m_dataOffset), (int)bytesThisOperation);
                    m_dataOffset += (int)bytesThisOperation;
                    bitCount -= bytesThisOperation * 8;
                    continue;
                }
                var readBits = buffer.ReadBitsFromCurrent(bitsThisOperation);
                WriteBitsToCurrent(readBits, bitsThisOperation);

                bitCount -= bitsThisOperation;
            }
        }

        public void FlushBit()
        {
            if (m_bitPositionInByte == 0) return;

            m_output[m_dataOffset++] = m_bitValue;
            m_bitPositionInByte = 0;
            m_bitValue = 0;
        }

        public Span<byte> GetSpanOfNextBytes(int size)
        {
            var span = m_output.Slice(m_dataOffset, size);
            m_dataOffset += size;
            return span;
        }
        
        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            var outputSpan = GetSpanOfNextBytes(data.Length);
            data.CopyTo(outputSpan);
        }
    }
}
using System.Buffers.Binary;

namespace ArcticFox.Codec.Binary
{
    public ref partial struct BitReader
    {
        public double ReadDoubleBigEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadDoubleBigEndian(m_data.Slice(m_dataOffset, sizeof(double)));
            m_dataOffset += sizeof(double);
            return value;
        }

        public double ReadDoubleLittleEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadDoubleLittleEndian(m_data.Slice(m_dataOffset, sizeof(double)));
            m_dataOffset += sizeof(double);
            return value;
        }

        public short ReadInt16BigEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadInt16BigEndian(m_data.Slice(m_dataOffset, sizeof(short)));
            m_dataOffset += sizeof(short);
            return value;
        }

        public short ReadInt16LittleEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadInt16LittleEndian(m_data.Slice(m_dataOffset, sizeof(short)));
            m_dataOffset += sizeof(short);
            return value;
        }

        public int ReadInt32BigEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadInt32BigEndian(m_data.Slice(m_dataOffset, sizeof(int)));
            m_dataOffset += sizeof(int);
            return value;
        }

        public int ReadInt32LittleEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadInt32LittleEndian(m_data.Slice(m_dataOffset, sizeof(int)));
            m_dataOffset += sizeof(int);
            return value;
        }

        public long ReadInt64BigEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadInt64BigEndian(m_data.Slice(m_dataOffset, sizeof(long)));
            m_dataOffset += sizeof(long);
            return value;
        }

        public long ReadInt64LittleEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadInt64LittleEndian(m_data.Slice(m_dataOffset, sizeof(long)));
            m_dataOffset += sizeof(long);
            return value;
        }

        public float ReadSingleBigEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadSingleBigEndian(m_data.Slice(m_dataOffset, sizeof(float)));
            m_dataOffset += sizeof(float);
            return value;
        }

        public float ReadSingleLittleEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadSingleLittleEndian(m_data.Slice(m_dataOffset, sizeof(float)));
            m_dataOffset += sizeof(float);
            return value;
        }

        public ushort ReadUInt16BigEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadUInt16BigEndian(m_data.Slice(m_dataOffset, sizeof(ushort)));
            m_dataOffset += sizeof(ushort);
            return value;
        }

        public ushort ReadUInt16LittleEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadUInt16LittleEndian(m_data.Slice(m_dataOffset, sizeof(ushort)));
            m_dataOffset += sizeof(ushort);
            return value;
        }

        public uint ReadUInt32BigEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadUInt32BigEndian(m_data.Slice(m_dataOffset, sizeof(uint)));
            m_dataOffset += sizeof(uint);
            return value;
        }

        public uint ReadUInt32LittleEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadUInt32LittleEndian(m_data.Slice(m_dataOffset, sizeof(uint)));
            m_dataOffset += sizeof(uint);
            return value;
        }

        public ulong ReadUInt64BigEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadUInt64BigEndian(m_data.Slice(m_dataOffset, sizeof(ulong)));
            m_dataOffset += sizeof(ulong);
            return value;
        }

        public ulong ReadUInt64LittleEndian()
        {
            ClearBit();
            var value = BinaryPrimitives.ReadUInt64LittleEndian(m_data.Slice(m_dataOffset, sizeof(ulong)));
            m_dataOffset += sizeof(ulong);
            return value;
        }
    }
}
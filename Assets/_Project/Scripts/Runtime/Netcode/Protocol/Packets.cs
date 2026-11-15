using System;

namespace HEXBOUND.Netcode
{
    public static class Protocol
    {
        /// <summary>Bump when the packet format or simulation rules change in an incompatible way.</summary>
        public const int Version = 4;

        public const byte Hello = 1;
        public const byte CharacterSelect = 2;
        public const byte Start = 3;
        public const byte Input = 4;
        public const byte Checksum = 5;
        public const byte Ping = 6;
        public const byte Pong = 7;
        public const byte BackToSelect = 8;
        public const byte Leave = 9;
    }

    /// <summary>Little-endian writer into a reusable byte buffer.</summary>
    public sealed class PacketWriter
    {
        private readonly byte[] _buffer;
        private int _length;

        public PacketWriter(int capacity = 1024)
        {
            _buffer = new byte[capacity];
        }

        public byte[] Buffer
        {
            get { return _buffer; }
        }

        public int Length
        {
            get { return _length; }
        }

        public PacketWriter Begin(byte type)
        {
            _length = 0;
            WriteByte(type);
            return this;
        }

        public void WriteByte(byte value)
        {
            if (_length >= _buffer.Length) throw new InvalidOperationException("Packet too large");
            _buffer[_length++] = value;
        }

        public void WriteInt(int value)
        {
            WriteUInt((uint)value);
        }

        public void WriteUInt(uint value)
        {
            WriteByte((byte)value);
            WriteByte((byte)(value >> 8));
            WriteByte((byte)(value >> 16));
            WriteByte((byte)(value >> 24));
        }

        public void WriteULong(ulong value)
        {
            WriteUInt((uint)value);
            WriteUInt((uint)(value >> 32));
        }
    }

    /// <summary>Little-endian reader. Reading past the end returns zeros and sets Failed.</summary>
    public struct PacketReader
    {
        private readonly byte[] _buffer;
        private readonly int _length;
        private int _position;

        public bool Failed;

        public PacketReader(byte[] buffer, int length)
        {
            _buffer = buffer;
            _length = length;
            _position = 0;
            Failed = false;
        }

        public int Remaining
        {
            get { return _length - _position; }
        }

        public byte ReadByte()
        {
            if (_position >= _length)
            {
                Failed = true;
                return 0;
            }
            return _buffer[_position++];
        }

        public int ReadInt()
        {
            return (int)ReadUInt();
        }

        public uint ReadUInt()
        {
            uint b0 = ReadByte();
            uint b1 = ReadByte();
            uint b2 = ReadByte();
            uint b3 = ReadByte();
            return b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
        }

        public ulong ReadULong()
        {
            ulong lo = ReadUInt();
            ulong hi = ReadUInt();
            return lo | (hi << 32);
        }
    }
}

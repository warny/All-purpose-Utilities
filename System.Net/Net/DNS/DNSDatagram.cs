using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utils.Net.DNS
{
	public class DNSDatagram
	{
		public int Length { get; private set; } = 0;
		private readonly byte[] datagram;
		public int Position { get; private set; } = 0;

		private readonly Dictionary<string, ushort> StringsPositions = new Dictionary<string, ushort>();
		private readonly Dictionary<ushort, string> PositionsStrings = new Dictionary<ushort, string>();

		public DNSDatagram()
		{
			Position = 0;
			Length = 0;
			datagram = new byte[512];
		}

		public DNSDatagram(Stream datas)
		{
			datagram = new byte[512];
			Length = datas.Read(datagram, 0, 512);
			Position = 0;
		}

		public DNSDatagram(byte[] datas)
		{
			Position = 0;
			Length = datas.Length;
			datagram = datas;
		}

		public void ResetRead() => Position = 0;

		public byte[] ToBytes()
		{
			var result = new byte[Length];
			Array.Copy(datagram, result, Length);
			return result;
		}

		public void Write(byte b)
		{
			datagram[Length] = b;
			Length++;
		}

		public void Write(byte[] b)
		{
			Array.Copy(b, 0, datagram,Length, b.Length);
			Length+=b.Length;
		}

		public void Write(ushort s)
		{
			Write(Length, s);
			Length += 2;
		}

        public void Write(int position, ushort s)
		{
			datagram[position] = (byte)((s >> 8) & 0xFF);
			datagram[position + 1] = (byte)(s & 0xFF);
		}

		public void Write(uint i)
		{
			var position = Length;
			Write(position, i);
			Length += 4;
		}
        private void Write(int position, uint i)
		{
			datagram[position] = (byte)((i >> 24) & 0xFF);
			datagram[position + 1] = (byte)((i >> 16) & 0xFF);
			datagram[position + 2] = (byte)((i >> 8) & 0xFF);
			datagram[position + 3] = (byte)(i & 0xFF);
		}

		public void Write(string s)
		{
			if (StringsPositions.TryGetValue(s, out ushort position))
			{
				Write((ushort)(position | 0xC000));
				return;
			}

			var stringSplit = s.Split('.', 2);
			StringsPositions.Add(s, (ushort)Length);
			PositionsStrings.Add((ushort)Length, s);
			Write((byte)stringSplit[0].Length);
			Write(ASCIIEncoding.ASCII.GetBytes(stringSplit[0]));
			if (stringSplit.Length > 1)
			{
				Write(stringSplit[1]);
			}
			else
			{
				Write((byte)0x00);
			}

		}

		public byte ReadByte()
		{
			return datagram[Position++];
		}

		public byte[] ReadBytes(int length)
		{
			byte[] result = new byte[length];
			Array.Copy(datagram, Position, result, 0, length);
			Position += length;
			return result;
		}

		public ushort ReadUShort()
		{
			return (ushort)(
				(ReadByte() << 8)
				| ReadByte());
		}

        public uint ReadUInt()
		{
			return (uint)(
				(ReadByte() << 24)
				| (ReadByte() << 16)
				| (ReadByte() << 8)
				| (ReadByte()));
		}

        public string ReadString() => ReadString(Position);
	
		private string ReadString(int position)
		{
			bool restorePosition = position != this.Position;
			int temp = this.Position;
			this.Position = position;
 			ushort l = ReadByte();
			if (l == 0) return null;
			if ((l & 0xC0) != 0) {
				ushort p = (ushort)(((l & 0x3F) << 8) | ReadByte());
				if (PositionsStrings.TryGetValue(p, out string s))
				{
					if (restorePosition) this.Position = temp;
					return s;
				}
				return ReadString(p);
			}
			{
				var s = Encoding.ASCII.GetString(ReadBytes(l));
				var next = ReadString();
				if (next is not null) s = s + "." + next;
				if (restorePosition) this.Position = temp;
				PositionsStrings[(ushort)position] = s;
				StringsPositions[s] = (ushort)position;
				return s;
			}

		}
	}
}

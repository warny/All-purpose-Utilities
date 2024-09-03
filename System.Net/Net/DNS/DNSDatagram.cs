using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.Objects;

namespace Utils.Net.DNS
{
	/// <summary>
	/// Represents a DNS datagram, which provides methods for reading and writing DNS-related data.
	/// </summary>
	public class DNSDatagram
	{
		private readonly Encoding defaultEncoding = Encoding.ASCII;

		/// <summary>
		/// Gets the current length of the datagram.
		/// </summary>
		public int Length { get; private set; } = 0;

		/// <summary>
		/// Gets the current reading position in the datagram.
		/// </summary>
		public int Position { get; private set; } = 0;

		private readonly byte[] datagram;
		private readonly Dictionary<string, ushort> stringPositions = [];
		private readonly Dictionary<ushort, string> positionStrings = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSDatagram"/> class with a default buffer size of 512 bytes.
		/// </summary>
		public DNSDatagram()
		{
			datagram = new byte[512];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSDatagram"/> class by reading data from the provided stream.
		/// </summary>
		/// <param name="dataStream">The stream containing DNS data.</param>
		public DNSDatagram(Stream dataStream)
		{
			datagram = new byte[512];
			Length = dataStream.Read(datagram, 0, 512);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSDatagram"/> class using the provided byte array.
		/// </summary>
		/// <param name="data">The byte array containing DNS data.</param>
		public DNSDatagram(byte[] data)
		{
			datagram = data;
			Length = data.Length;
		}

		/// <summary>
		/// Resets the reading position to the beginning of the datagram.
		/// </summary>
		public void ResetRead() => Position = 0;

		/// <summary>
		/// Converts the datagram to a byte array.
		/// </summary>
		/// <returns>A byte array representing the datagram.</returns>
		public byte[] ToBytes()
		{
			var result = new byte[Length];
			Array.Copy(datagram, result, Length);
			return result;
		}

		/// <summary>
		/// Writes a single byte to the datagram.
		/// </summary>
		/// <param name="b">The byte to write.</param>
		public void Write(byte b)
		{
			datagram[Length++] = b;
		}

		/// <summary>
		/// Writes an array of bytes to the datagram.
		/// </summary>
		/// <param name="bytes">The byte array to write.</param>
		public void Write(byte[] bytes)
		{
			Array.Copy(bytes, 0, datagram, Length, bytes.Length);
			Length += bytes.Length;
		}

		/// <summary>
		/// Writes a 16-bit unsigned integer to the datagram.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public void Write(ushort value)
		{
			Write(Length, value);
			Length += 2;
		}

		/// <summary>
		/// Writes a 16-bit unsigned integer to the datagram at the specified position.
		/// </summary>
		/// <param name="position">The position in the datagram where the value should be written.</param>
		/// <param name="value">The value to write.</param>
		public void Write(int position, ushort value)
		{
			datagram[position] = (byte)((value >> 8) & 0xFF);
			datagram[position + 1] = (byte)(value & 0xFF);
		}

		/// <summary>
		/// Writes a 32-bit unsigned integer to the datagram.
		/// </summary>
		/// <param name="value">The value to write.</param>
		public void Write(uint value)
		{
			Write(Length, value);
			Length += 4;
		}

		/// <summary>
		/// Writes a 32-bit unsigned integer to the datagram at the specified position.
		/// </summary>
		/// <param name="position">The position in the datagram where the value should be written.</param>
		/// <param name="value">The value to write.</param>
		private void Write(int position, uint value)
		{
			datagram[position] = (byte)((value >> 24) & 0xFF);
			datagram[position + 1] = (byte)((value >> 16) & 0xFF);
			datagram[position + 2] = (byte)((value >> 8) & 0xFF);
			datagram[position + 3] = (byte)(value & 0xFF);
		}

		/// <summary>
		/// Writes a string to the datagram in DNS format (with label length and compression).
		/// </summary>
		/// <param name="s">The string to write.</param>
		public void Write(string s) => Write(s, defaultEncoding);
		
		/// <summary>
		/// Writes a string to the datagram in DNS format (with label length and compression).
		/// </summary>
		/// <param name="s">The string to write.</param>
		public void Write(string s, Encoding encoding)
		{
			if (stringPositions.TryGetValue(s, out ushort position))
			{
				Write((ushort)(position | 0xC000)); // Write a pointer to the string position
				return;
			}

			var labels = s.Split('.', 2);
			stringPositions[s] = (ushort)Length;
			positionStrings[(ushort)Length] = s;

			Write((byte)labels[0].Length); // Write the length of the label
			Write(encoding.GetBytes(labels[0])); // Write the label itself

			if (labels.Length > 1)
			{
				Write(labels[1]); // Recursively write the next label
			}
			else
			{
				Write((byte)0x00); // Write the null terminator for the domain name
			}
		}

		/// <summary>
		/// Reads a single byte from the datagram.
		/// </summary>
		/// <returns>The byte that was read.</returns>
		public byte ReadByte()
		{
			return datagram[Position++];
		}

		/// <summary>
		/// Reads a specified number of bytes from the datagram.
		/// </summary>
		/// <param name="length">The number of bytes to read.</param>
		/// <returns>A byte array containing the bytes that were read.</returns>
		public byte[] ReadBytes(int length)
		{
			var result = new byte[length];
			Array.Copy(datagram, Position, result, 0, length);
			Position += length;
			return result;
		}

		/// <summary>
		/// Reads a 16-bit unsigned integer from the datagram.
		/// </summary>
		/// <returns>The 16-bit unsigned integer that was read.</returns>
		public ushort ReadUShort()
		{
			return (ushort)((ReadByte() << 8) | ReadByte());
		}

		/// <summary>
		/// Reads a 32-bit unsigned integer from the datagram.
		/// </summary>
		/// <returns>The 32-bit unsigned integer that was read.</returns>
		public uint ReadUInt()
		{
			return (uint)((ReadByte() << 24) | (ReadByte() << 16) | (ReadByte() << 8) | ReadByte());
		}

		/// <summary>
		/// Reads a string from the datagram at the current position.
		/// </summary>
		/// <returns>The string that was read.</returns>
		public string ReadString() => ReadString(Position);

		/// <summary>
		/// Reads a string from the datagram at the current position.
		/// </summary>
		/// <returns>The string that was read.</returns>
		public string ReadString(Encoding encoding) => ReadString(Position, encoding);

		/// <summary>
		/// Reads a string from the datagram at the specified position.
		/// </summary>
		/// <param name="position">The position in the datagram where the string starts.</param>
		/// <returns>The string that was read.</returns>
		private string ReadString(int position) => ReadString(position, defaultEncoding);

		/// <summary>
		/// Reads a string from the datagram at the specified position.
		/// </summary>
		/// <param name="position">The position in the datagram where the string starts.</param>
		/// <returns>The string that was read.</returns>
		private string ReadString(int position, Encoding encoding)
		{
			encoding.ArgMustNotBeNull();

			bool restorePosition = position != this.Position;
			int tempPosition = this.Position;
			this.Position = position;

			ushort lengthOrPointer = ReadByte();
			if (lengthOrPointer == 0) return null;

			// Handle DNS name compression (pointer)
			if ((lengthOrPointer & 0xC0) != 0)
			{
				ushort pointerPosition = (ushort)(((lengthOrPointer & 0x3F) << 8) | ReadByte());
				if (positionStrings.TryGetValue(pointerPosition, out string s))
				{
					if (restorePosition) this.Position = tempPosition;
					return s;
				}
				return ReadString(pointerPosition);
			}

			// Read the actual string
			var label = encoding.GetString(ReadBytes(lengthOrPointer));
			var nextLabel = ReadString();
			var fullString = nextLabel is not null ? $"{label}.{nextLabel}" : label;

			// Cache the read string for future lookups
			positionStrings[(ushort)position] = fullString;
			stringPositions[fullString] = (ushort)position;

			if (restorePosition) this.Position = tempPosition;

			return fullString;
		}
	}
}

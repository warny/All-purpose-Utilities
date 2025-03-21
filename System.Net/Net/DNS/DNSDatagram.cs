using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.Objects;

namespace Utils.Net.DNS
{
	/// <summary>
	/// Represents a DNS datagram, which provides methods for reading and writing DNS-related data.
	/// It supports simple DNS name compression, reading and writing integers of various sizes,
	/// and handling string labels in DNS-compatible formats.
	/// </summary>
	public class DNSDatagram
	{
		/// <summary>
		/// The default <see cref="Encoding"/> used for reading and writing strings when none is specified.
		/// </summary>
		private readonly Encoding defaultEncoding = Encoding.ASCII;

		/// <summary>
		/// Gets the current length of the datagram, representing the total number of bytes written.
		/// </summary>
		public int Length { get; private set; } = 0;

		/// <summary>
		/// Gets the current reading position within the underlying byte buffer.
		/// </summary>
		/// <remarks>
		/// This is updated automatically as bytes or data structures are read from the buffer.
		/// </remarks>
		public int Position { get; private set; } = 0;

		/// <summary>
		/// The underlying byte array storing the DNS datagram.
		/// </summary>
		private readonly byte[] datagram;

		/// <summary>
		/// Maintains a mapping between strings (domain labels) and their positions in the datagram
		/// for enabling DNS compression references.
		/// </summary>
		private readonly Dictionary<string, ushort> stringPositions = new();

		/// <summary>
		/// Maintains a mapping between positions in the datagram and corresponding strings (domain labels).
		/// </summary>
		private readonly Dictionary<ushort, string> positionStrings = new();

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSDatagram"/> class with a default buffer size of 512 bytes.
		/// This constructor is suitable for constructing outgoing DNS packets.
		/// </summary>
		public DNSDatagram()
		{
			datagram = new byte[512];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSDatagram"/> class by reading data from the provided stream.
		/// </summary>
		/// <param name="dataStream">The stream containing DNS data.</param>
		/// <remarks>
		/// Reads up to 512 bytes from the <paramref name="dataStream"/> into the internal buffer.
		/// The <see cref="Length"/> property is set to the number of bytes actually read.
		/// </remarks>
		public DNSDatagram(Stream dataStream)
		{
			datagram = new byte[512];
			Length = dataStream.Read(datagram, 0, 512);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DNSDatagram"/> class using the provided byte array.
		/// </summary>
		/// <param name="data">The byte array containing DNS data.</param>
		/// <remarks>
		/// Sets the internal buffer to <paramref name="data"/> and the <see cref="Length"/> to the array's length.
		/// Useful for parsing existing DNS data.
		/// </remarks>
		public DNSDatagram(byte[] data)
		{
			datagram = data;
			Length = data.Length;
		}

		/// <summary>
		/// Resets the <see cref="Position"/> property to zero,
		/// allowing the datagram to be read again from the beginning.
		/// </summary>
		public void ResetRead() => Position = 0;

		/// <summary>
		/// Converts the content of this datagram to a new byte array containing all written bytes.
		/// </summary>
		/// <returns>A byte array representing the datagram's written data.</returns>
		public byte[] ToBytes()
		{
			var result = new byte[Length];
			Array.Copy(datagram, result, Length);
			return result;
		}

		/// <summary>
		/// Writes a single byte into the datagram's buffer, and advances <see cref="Length"/> by one.
		/// </summary>
		/// <param name="b">The byte to write.</param>
		public void Write(byte b)
		{
			datagram[Length++] = b;
		}

		/// <summary>
		/// Writes the entire contents of a byte array into the datagram's buffer, and advances <see cref="Length"/>.
		/// </summary>
		/// <param name="bytes">The byte array to write.</param>
		public void Write(byte[] bytes)
		{
			Array.Copy(bytes, 0, datagram, Length, bytes.Length);
			Length += bytes.Length;
		}

		/// <summary>
		/// Writes a 16-bit unsigned integer into the datagram at the current <see cref="Length"/> position.
		/// </summary>
		/// <param name="value">The 16-bit value to write.</param>
		public void Write(ushort value)
		{
			Write(Length, value);
			Length += 2;
		}

		/// <summary>
		/// Writes a 16-bit unsigned integer into the datagram at a specified position,
		/// without changing the <see cref="Length"/>.
		/// </summary>
		/// <param name="position">The position where the value should be written.</param>
		/// <param name="value">The 16-bit value to write.</param>
		public void Write(int position, ushort value)
		{
			datagram[position] = (byte)((value >> 8) & 0xFF);
			datagram[position + 1] = (byte)(value & 0xFF);
		}

		/// <summary>
		/// Writes a 32-bit unsigned integer into the datagram at the current <see cref="Length"/> position.
		/// </summary>
		/// <param name="value">The 32-bit value to write.</param>
		public void Write(uint value)
		{
			Write(Length, value);
			Length += 4;
		}

		/// <summary>
		/// Writes a 32-bit unsigned integer into the datagram at a specified position,
		/// without changing the <see cref="Length"/>.
		/// </summary>
		/// <param name="position">The position where the value should be written.</param>
		/// <param name="value">The 32-bit value to write.</param>
		private void Write(int position, uint value)
		{
			datagram[position] = (byte)((value >> 24) & 0xFF);
			datagram[position + 1] = (byte)((value >> 16) & 0xFF);
			datagram[position + 2] = (byte)((value >> 8) & 0xFF);
			datagram[position + 3] = (byte)(value & 0xFF);
		}

		/// <summary>
		/// Writes a string in DNS label format using ASCII encoding by default, supporting DNS name compression.
		/// </summary>
		/// <param name="s">The string to write (domain name in dot notation).</param>
		public void Write(string s) => Write(s, defaultEncoding);

		/// <summary>
		/// Writes a string in DNS label format using the specified <see cref="Encoding"/>,
		/// supporting DNS name compression.
		/// </summary>
		/// <param name="s">The string to write (domain name in dot notation).</param>
		/// <param name="encoding">The text encoding used to convert string labels to bytes.</param>
		public void Write(string s, Encoding encoding)
		{
			if (stringPositions.TryGetValue(s, out ushort position))
			{
				// If the string has already been written, write a pointer reference
				// (0xC0 indicates a compressed reference in DNS).
				Write((ushort)(position | 0xC000));
				return;
			}

			// Split the domain name into two parts: the first label and the remainder.
			var labels = s.Split('.', 2);
			stringPositions[s] = (ushort)Length;
			positionStrings[(ushort)Length] = s;

			// Write the length of the first label, followed by the label data.
			Write((byte)labels[0].Length);
			Write(encoding.GetBytes(labels[0]));

			// Recursively write the remainder of the string, if any, or terminate with 0x00.
			if (labels.Length > 1)
			{
				Write(labels[1]);
			}
			else
			{
				// Null terminator indicating the end of the domain name.
				Write((byte)0x00);
			}
		}

		/// <summary>
		/// Reads a single byte from the datagram at the current <see cref="Position"/>.
		/// </summary>
		/// <returns>The byte that was read.</returns>
		public byte ReadByte()
		{
			return datagram[Position++];
		}

		/// <summary>
		/// Reads a specified number of bytes from the datagram at the current <see cref="Position"/>.
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
		/// Reads a 16-bit unsigned integer from the datagram at the current <see cref="Position"/>.
		/// </summary>
		/// <returns>The 16-bit unsigned integer that was read.</returns>
		public ushort ReadUShort()
		{
			return (ushort)((ReadByte() << 8) | ReadByte());
		}

		/// <summary>
		/// Reads a 32-bit unsigned integer from the datagram at the current <see cref="Position"/>.
		/// </summary>
		/// <returns>The 32-bit unsigned integer that was read.</returns>
		public uint ReadUInt()
		{
			return (uint)((ReadByte() << 24) | (ReadByte() << 16) | (ReadByte() << 8) | ReadByte());
		}

		/// <summary>
		/// Reads a DNS-formatted string (domain name) from the datagram at the current <see cref="Position"/>,
		/// using the default encoding.
		/// </summary>
		/// <returns>The domain name that was read, or <c>null</c> if the string is empty.</returns>
		public string ReadString() => ReadString(Position);

		/// <summary>
		/// Reads a DNS-formatted string (domain name) from the datagram at the current <see cref="Position"/>,
		/// using the specified <paramref name="encoding"/>.
		/// </summary>
		/// <param name="encoding">The encoding used to parse label bytes into characters.</param>
		/// <returns>The domain name that was read, or <c>null</c> if the string is empty.</returns>
		public string ReadString(Encoding encoding) => ReadString(Position, encoding);

		/// <summary>
		/// Reads a DNS-formatted string (domain name) from the specified <paramref name="position"/>,
		/// using the default encoding.
		/// </summary>
		/// <param name="position">The byte position in the datagram at which to start reading.</param>
		/// <returns>The domain name that was read, or <c>null</c> if the string is empty.</returns>
		private string ReadString(int position) => ReadString(position, defaultEncoding);

		/// <summary>
		/// Reads a DNS-formatted string (domain name) from the specified <paramref name="position"/>,
		/// using the given <paramref name="encoding"/>.
		/// </summary>
		/// <param name="position">The byte position in the datagram at which to start reading.</param>
		/// <param name="encoding">The encoding used to parse label bytes into characters.</param>
		/// <returns>The domain name that was read, or <c>null</c> if the label length is 0.</returns>
		private string ReadString(int position, Encoding encoding)
		{
			encoding.Arg().MustNotBeNull();

			bool restorePosition = position != this.Position;
			int tempPosition = this.Position;
			this.Position = position;

			// Read the length or pointer.
			ushort lengthOrPointer = ReadByte();
			if (lengthOrPointer == 0)
				return null;

			// Check if the high bits indicate a pointer-based compression (0xC0).
			if ((lengthOrPointer & 0xC0) != 0)
			{
				// Calculate the pointer's offset, combining the lower 6 bits of the first byte with the next byte.
				ushort pointerPosition = (ushort)(((lengthOrPointer & 0x3F) << 8) | ReadByte());

				// If we've already resolved this string, return it.
				if (positionStrings.TryGetValue(pointerPosition, out string s))
				{
					if (restorePosition)
						this.Position = tempPosition;
					return s;
				}

				// Otherwise, read the string recursively from the pointer position.
				return ReadString(pointerPosition);
			}

			// At this point, lengthOrPointer holds the length of the label, so read those bytes.
			var label = encoding.GetString(ReadBytes(lengthOrPointer));

			// Recursively read the next label, if any.
			var nextLabel = ReadString();
			var fullString = nextLabel is not null ? $"{label}.{nextLabel}" : label;

			// Cache the string and its position for future pointer lookups or compression references.
			positionStrings[(ushort)position] = fullString;
			stringPositions[fullString] = (ushort)position;

			if (restorePosition)
				this.Position = tempPosition;

			return fullString;
		}
	}
}

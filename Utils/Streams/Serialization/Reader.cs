using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Utils.Arrays;
using Utils.Reflection;
using IO = System.IO;

namespace Utils.Streams.Serialization
{
	public class Reader
    {
		private static Dictionary<Type, FieldOrPropertyInfo[]> TypesAccessors = new Dictionary<Type, FieldOrPropertyInfo[]>();
		Stack<long> positionsStack = new Stack<long>();

		public IO.Stream Stream { get; }
		public long Position => Stream.Position;

		public Reader(IO.Stream s)
		{
			this.Stream = s;
			if (!this.Stream.CanRead) throw new NotSupportedException();
		}

		public void Seek( int offset , SeekOrigin origin)
		{
			this.Stream.Seek(offset, origin);
		}

		public void Push()
		{
			if (!this.Stream.CanSeek) throw new NotSupportedException();
			positionsStack.Push(this.Stream.Position);
		}

		public void Push( int offset, SeekOrigin origin )
		{
			if (!this.Stream.CanSeek) throw new NotSupportedException();
			positionsStack.Push(this.Stream.Position);
			this.Stream.Seek(offset, origin);
		}

		public void Pop()
		{
			if (!this.Stream.CanSeek) throw new NotSupportedException();
			this.Stream.Seek(positionsStack.Pop(), SeekOrigin.Begin);
		}

		public T Read<T>() where T : IReadable, new()
		{
			T result = new T();
			Read(result);
			return result;
		}

		public bool Read(Type t, out object result)
		{
			result = Activator.CreateInstance(t);
			return Read(result, t);
		}

		public bool Read( object result )
		{
			Type t = result.GetType();
			return Read(result, t);
		}

		private bool Read(object result, Type t)
		{
			if (!TypesAccessors.TryGetValue(t, out FieldOrPropertyInfo[] fields))
			{
				fields = t.GetMembers(BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					.Where(m => m.GetCustomAttribute<FieldAttribute>() != null)
					.Select(m => new FieldOrPropertyInfo(m))
					.OrderBy(m => m.GetCustomAttribute<FieldAttribute>().Order)
					.ToArray();
				TypesAccessors.Add(t, fields);
			}
			if (!fields.Any()) return false;
			foreach (var field in fields)
			{
				var attribute = field.GetCustomAttribute<FieldAttribute>();
				System.Diagnostics.Debug.WriteLine($"{attribute.Order} {field.ToString()} {attribute.FieldEncoding} {attribute.Length}");
				System.Diagnostics.Debug.WriteLine($"Start : {Stream.Position}");
				field.SetValue(result, ReadValue(field.Type, attribute.Length, attribute.BigIndian, attribute.Terminators, attribute.FieldEncoding, attribute.StringEncoding));
				System.Diagnostics.Debug.WriteLine($"End : {Stream.Position}");
			}
			return true;
		}

		private object ReadValue( Type type, int? length, bool bigIndian, byte[] terminators, FieldEncodingEnum fieldEncoding, Encoding stringEncoding)
		{
			if (typeof(IReadable).IsAssignableFrom(type)) {
				return Read(type);
			} else if (type == typeof(byte)) {
				return ReadByte();
			} else if (type == typeof(byte[])) {
				if (fieldEncoding == FieldEncodingEnum.VariableLength) {
					return ReadVariableLengthBytes(bigIndian, length ?? sizeof(Int32));
				} else if (fieldEncoding== FieldEncodingEnum.NullTerminated) {
					return ReadTerminatedBytes(terminators);
				} else {
					return ReadBytes(length.Value);
				}
			} else if (type == typeof(Int16)) {
				return ReadInt16(bigIndian, length ?? sizeof(Int16));
			} else if (type == typeof(Int32)) {
				return ReadInt32(bigIndian, length ?? sizeof(Int32));
			} else if (type == typeof(Int64)) {
				return ReadInt64(bigIndian, length ?? sizeof(Int64));
			} else if (type == typeof(UInt16)) {
				return ReadUInt16(bigIndian, length ?? sizeof(UInt16));
			} else if (type == typeof(UInt32)) {
				return ReadUInt32(bigIndian, length ?? sizeof(UInt32));
			} else if (type == typeof(UInt64)) {
				return ReadUInt64(bigIndian, length ?? sizeof(UInt64));
			} else if (type == typeof(Single)) {
				return ReadSingle(bigIndian);
			} else if (type == typeof(Double)) {
				return ReadDouble(bigIndian);
			} else if (type == typeof(string)) {
				if (fieldEncoding == FieldEncodingEnum.VariableLength) {
					return ReadVariableLengthString(stringEncoding, bigIndian, length ?? sizeof(Int32));
				} else if (fieldEncoding== FieldEncodingEnum.FixedLength) {
					return ReadFixedLengthString(length.Value, stringEncoding);
				} else {
					return ReadNullTerminatedString(stringEncoding, terminators);
				}
			} else if (type == typeof(DateTime)) {
				if (fieldEncoding == FieldEncodingEnum.TimeStamp) {
					return ReadTimeStamp(bigIndian, length ?? sizeof(Int32));
				} else if (fieldEncoding == FieldEncodingEnum.DateTime) {
					return ReadOleDateTime(bigIndian);
				} else {
					return ReadDateTime(bigIndian);
				}
			} else if (type == typeof(TimeSpan)) {
				return new TimeSpan(ReadInt64(bigIndian, length ?? sizeof(Int64)));
			} else if (type.IsArray) {
				if (fieldEncoding == FieldEncodingEnum.FixedLength) {
					return ReadArray(length.Value, type.GetElementType(), bigIndian, stringEncoding);
				} else {
					return ReadVariableLengthArray(type.GetElementType(), bigIndian, stringEncoding, length ?? sizeof(int));
				}
			} else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
				Type argumentType = type.GenericTypeArguments[0];
				if (argumentType==typeof(object)) throw new NotSupportedException();
				Array result;
				if (fieldEncoding == FieldEncodingEnum.FixedLength) {
					result = ReadArray(length.Value, argumentType, bigIndian, stringEncoding);
				}
				else {
					result = ReadVariableLengthArray(argumentType, bigIndian, stringEncoding, length ?? sizeof(int));
				}
				return type.GetConstructor(new Type[] { result.GetType() }).Invoke(new[] { result });
			}

			throw new NotSupportedException();
		}

		public byte[] ReadBytes(int length )
		{
			byte[] buffer = new byte[length];
			Stream.Read(buffer, 0, length);
			return buffer;
		}

		public byte[] ReadVariableLengthBytes( bool bigIndian = false, int sizeLength = sizeof(Int32) )
		{
			int length = ReadInt32(bigIndian, sizeLength);
			return ReadBytes(length);
		}

		public List<byte> ReadTerminatedBytes( params byte[] terminators )
		{
			if (terminators == null || terminators.Length == 0) terminators = new byte[] { 0x0 };
			var bytes = new List<byte>();
			for (int b = ReadByte() ; !terminators.Contains((byte)b) ; b = ReadByte()) {
				bytes.Add((byte)b);
			}

			return bytes;
		}

		public T[] ReadArray<T>(int length, bool bigIndian = false, Encoding stringEncoding = null) 
			=> (T[])ReadArray(length, typeof(T), bigIndian, stringEncoding);

		public Array ReadArray( int length, Type elementType, bool bigIndian = false, Encoding stringEncoding = null ) {
			Array result = Array.CreateInstance (elementType, length);
			for (int i = 0 ; i < length ; i++) {
				object value = ReadValue(elementType, null, bigIndian, new byte[0], FieldEncodingEnum.None, stringEncoding);
				result.SetValue(value,i);
			}
			return result;
		}

		public T[] ReadVariableLengthArray<T>(bool bigIndian = false, Encoding stringEncoding = null, int arraySizeLength = sizeof(Int32)) 
			=> (T[])ReadVariableLengthArray(typeof(T), bigIndian, stringEncoding, arraySizeLength);

		public Array ReadVariableLengthArray( Type elementType, bool bigIndian = false, Encoding stringEncoding = null, int arraySizeLength = sizeof(Int32) )
		{
			int length = ReadInt32(bigIndian, arraySizeLength);
			return ReadArray(length, elementType, bigIndian, stringEncoding);
		}

		public string ReadFixedLengthString(int length, Encoding encoding )
		{
			byte[] bytes = ReadBytes(length);
			return encoding.GetString(bytes).TrimEnd('\0', ' ');
		}

		public string ReadNullTerminatedString( Encoding encoding, params byte[] terminators )
		{
			List<byte> bytes = ReadTerminatedBytes(terminators);
			return encoding.GetString(bytes.ToArray());
		}

		public string ReadVariableLengthString(Encoding encoding, bool bigIndian = false, int sizeLength = sizeof(Int32) )
		{
			int stringLength = ReadInt32(bigIndian, sizeLength);
			return ReadFixedLengthString(stringLength, encoding);
		}

		public sbyte ReadSByte() => (sbyte)ReadByte();
		public byte ReadByte()
		{
			int result = Stream.ReadByte();
			if (result <0) throw new EndOfStreamException ();
			return (byte)result;
		}
		public short ReadInt16(bool bigIndian = false, int length = sizeof(Int16) )
		{
			byte[] buffer = ReadBytes(length);
			buffer=ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(Int16));
			return BitConverter.ToInt16(buffer, 0);
		}
		public int ReadInt32(bool bigIndian = false, int length = sizeof(Int32))
		{
			byte[] buffer = ReadBytes(length);
			buffer=ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(Int32));
			return BitConverter.ToInt32(buffer, 0);
		}

		public long ReadInt64(bool bigIndian = false, int length = sizeof(Int64) )
		{
			byte[] buffer = ReadBytes(length);
			buffer=ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(Int64));
			return BitConverter.ToInt64(buffer, 0);
		}

		public ushort ReadUInt16(bool bigIndian = false, int length = sizeof(UInt16) )
		{
			byte[] buffer = ReadBytes(length);
			buffer=ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(UInt16));
			return BitConverter.ToUInt16(buffer, 0);
		}
		public uint ReadUInt32(bool bigIndian = false, int length = sizeof(UInt32) )
		{
			byte[] buffer = ReadBytes(length);
			buffer=ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(UInt32));
			return BitConverter.ToUInt32(buffer, 0);
		}

		public ulong ReadUInt64(bool bigIndian = false, int length = sizeof(UInt64) )
		{
			byte[] buffer = ReadBytes(length);
			buffer=ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(UInt64));
			return BitConverter.ToUInt64(buffer, 0);
		}

		public float ReadSingle(bool bigIndian = false)
		{
			byte[] buffer = ReadBytes(sizeof(Single));
			buffer= ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(Single));
			return BitConverter.ToSingle(buffer, 0);
		}

		public double ReadDouble(bool bigIndian = false)
		{
			byte[] buffer = ReadBytes(sizeof(Double));
			buffer=ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(Double));
			return BitConverter.ToDouble(buffer, 0);
		}

		public DateTime ReadTimeStamp(bool bigIndian = false, int length = sizeof(UInt32) )
		{
			DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			return dtDateTime.AddSeconds(ReadInt64(bigIndian, length)).ToLocalTime();
		}

		public DateTime ReadOleDateTime(bool bigIndian = false)
		{
			return DateTime.FromOADate(ReadDouble(bigIndian));
		}

		public DateTime ReadDateTime(bool bigIndian = false )
		{
			return new DateTime(ReadInt64(bigIndian));
		}

	}
}

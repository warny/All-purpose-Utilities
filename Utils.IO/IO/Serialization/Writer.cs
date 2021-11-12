using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Utils.Arrays;
using Utils.Reflection;
using IO = System.IO;


namespace Utils.IO.Serialization
{
	public class Writer
	{
		private static Dictionary<Type, FieldOrPropertyInfo[]> TypesAccessors = new Dictionary<Type, FieldOrPropertyInfo[]>();
		private readonly Stack<long> positionsStack = new Stack<long>();
		private readonly Dictionary<Type, IObjectWriter> Writers = new Dictionary<Type, IObjectWriter>();

		public System.IO.Stream Stream { get; }
		public long Position {
			get => Stream.Position;
			set => Stream.Position = value;
		}
		public long BytesLeft => Stream.Length - Stream.Position;

		public Writer(System.IO.Stream s) : this(s, Enumerable.Empty<IObjectWriter>()) { }
		public Writer(System.IO.Stream s, params IObjectReader[] writers) : this(s, (IEnumerable<IObjectWriter>)writers) { }
		public Writer(System.IO.Stream s, params IEnumerable<IObjectWriter>[] writers) : this(s, writers.SelectMany(r => r)) { }
		public Writer(System.IO.Stream s, IEnumerable<IObjectWriter> writers)
		{
			this.Stream = s;
			if (!this.Stream.CanWrite) throw new NotSupportedException();
			foreach (var writer in writers)
			{
				foreach (var type in writer.Types)
				{
					Writers[type] = writer;
				}
			}
		}

		public void Seek(int offset, System.IO.SeekOrigin origin)
		{
			this.Stream.Seek(offset, origin);
		}

		public void Push()
		{
			if (!this.Stream.CanSeek) throw new NotSupportedException();
			positionsStack.Push(this.Stream.Position);
		}

		public void Push(int offset, System.IO.SeekOrigin origin)
		{
			if (!this.Stream.CanSeek) throw new NotSupportedException();
			positionsStack.Push(this.Stream.Position);
			this.Stream.Seek(offset, origin);
		}

		public void Pop()
		{
			if (!this.Stream.CanSeek) throw new NotSupportedException();
			this.Stream.Seek(positionsStack.Pop(), System.IO.SeekOrigin.Begin);
		}
		public Writer Slice(long position, long length)
		{
			PartialStream s = new PartialStream(Stream, position, length);
			return new Writer(s);
		}


		public void Write(object obj)
		{
			Type t = obj.GetType();
			if (Writers.TryGetValue(t, out var writer))
			{
				writer.Write(this, obj);
			}
			else
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

				foreach (var field in fields)
				{
					var attribute = field.GetCustomAttribute<FieldAttribute>();
					var value = field.GetValue(obj);
					System.Diagnostics.Debug.WriteLine($"{attribute.Order} {field.ToString()} {attribute.FieldEncoding} {attribute.Length}");
					System.Diagnostics.Debug.WriteLine($"Start : {Stream.Position}");
					WriteValue(value, field.Type, attribute.Length, null, attribute.BigIndian, attribute.Terminators, attribute.FieldEncoding, attribute.StringEncoding);
					System.Diagnostics.Debug.WriteLine($"End : {Stream.Position}");

				}
			}
		}

		public void WriteByte(byte @byte)
		{
			Stream.WriteByte(@byte);
		}
		public void WriteBytes(byte[] bytes)
		{
			Stream.Write(bytes, 0, bytes.Length);
		}

		public void WriteVariableLengthBytes(byte[] bytes, bool bigIndian = false, int sizeLength = sizeof(Int32))
		{
			WriteInt32(bytes.Length, bigIndian, sizeLength);
			WriteBytes(bytes);
		}

		public void WriteNullTerminatedBytes(byte[] bytes, byte[] terminators = null)
		{
			var terminator = terminators?.FirstOrDefault() ?? 0;
			WriteBytes(bytes);
			WriteByte(terminator);

		}

		public void WriteInt16(short value, bool bigIndian = false, int sizeLength = sizeof(short))
		{
			byte[] buffer = BitConverter.GetBytes(value);
			buffer = ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeLength);
			WriteBytes(buffer);
		}


		public void WriteInt32(int value, bool bigIndian = false, int sizeLength = sizeof(int))
		{
			byte[] buffer = BitConverter.GetBytes(value);
			buffer = ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeLength);
			WriteBytes(buffer);
		}

		public void WriteInt64(long value, bool bigIndian = false, int sizeLength = sizeof(long))
		{
			byte[] buffer = BitConverter.GetBytes(value);
			buffer = ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeLength);
			WriteBytes(buffer);
		}

		public void WriteUInt16(ushort value, bool bigIndian = false, int sizeLength = sizeof(ushort))
		{
			byte[] buffer = BitConverter.GetBytes(value);
			buffer = ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeLength);
			WriteBytes(buffer);
		}


		public void WriteUInt32(uint value, bool bigIndian = false, int sizeLength = sizeof(uint))
		{
			byte[] buffer = BitConverter.GetBytes(value);
			buffer = ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeLength);
			WriteBytes(buffer);
		}

		public void WriteUInt64(ulong value, bool bigIndian = false, int sizeLength = sizeof(ulong))
		{
			byte[] buffer = BitConverter.GetBytes(value);
			buffer = ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeLength);
			WriteBytes(buffer);
		}

		public void WriteSingle(float value, bool bigIndian = false)
		{
			byte[] buffer = BitConverter.GetBytes(value);
			buffer = ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(float));
			WriteBytes(buffer);
		}

		public void WriteDouble(double value, bool bigIndian = false)
		{
			byte[] buffer = BitConverter.GetBytes(value);
			buffer = ArrayUtils.Adjust(buffer, bigIndian ^ BitConverter.IsLittleEndian, sizeof(double));
			WriteBytes(buffer);
		}

		public void WriteTimeStamp(DateTime value, bool bigIndian = false)
		{
			DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			var timestamp = (long)(value.ToUniversalTime() - dtDateTime).TotalSeconds;
			WriteInt64(timestamp, bigIndian);
		}

		public void WriteOleDateTime(DateTime value, bool bigIndian = false)
		{
			WriteDouble(value.ToOADate(), bigIndian);
		}

		public void WriteDateTime(DateTime value, bool bigIndian = false)
		{
			WriteInt64(value.Ticks, bigIndian);
		}


		public void WriteVariableLengthArray(Array array, Type elementsType, bool bigIndian = false, Encoding stringEncoding = null, int arraySizeLength = sizeof(Int32))
		{
			WriteInt32(array.Length, bigIndian, arraySizeLength);
			WriteArray(array, array.Length, elementsType, bigIndian, stringEncoding);

		}
		public void WriteArray(Array array, int length, Type elementsType, bool bigIndian = false, Encoding stringEncoding = null)
		{
			for (int i = 0; i < length; i++)
			{
				WriteValue(array.GetValue(i), elementsType, null, null, bigIndian, null, FieldEncodingEnum.None, stringEncoding);
			}
		}

		public void WriteVariableLengthString(string value, Encoding stringEncoding, bool bigIndian = false, int sizeLength = sizeof(Int32))
		{
			byte[] bytes = stringEncoding.GetBytes(value);
			WriteVariableLengthBytes(bytes, bigIndian, sizeLength);
		}

		public void WriteFixedLengthString(string value, int length, Encoding stringEncoding)
		{
			byte[] bytes = stringEncoding.GetBytes(value);
			bytes = ArrayUtils.PadRight<byte>(bytes, length, 0);
			WriteBytes(bytes);
		}

		public void WriteNullTerminatedString(string value, Encoding stringEncoding, byte[] terminators)
		{
			terminators = terminators ?? new byte[]{ 0 };
			byte[] bytes = stringEncoding.GetBytes(value);
			WriteBytes(bytes);
			WriteByte(terminators.LastOrDefault());
		}

		private void WriteValue(object value, Type type, int? length, int? sizeLength, bool bigIndian, byte[] terminators, FieldEncodingEnum fieldEncoding, Encoding stringEncoding)
		{
			type = type ?? value.GetType();

			if (Writers.TryGetValue(type, out var writer))
			{
				writer.Write(this, value);
			}
			else if (typeof(IWritable).IsAssignableFrom(type))
			{
				Write(value);
				return;
			}
			else if (type == typeof(byte))
			{
				WriteByte((byte)value);
				return;
			}
			else if (type == typeof(byte[]))
			{
				if (fieldEncoding == FieldEncodingEnum.VariableLength)
				{
					WriteVariableLengthBytes((byte[])value, bigIndian, sizeLength ?? sizeof(int));
					return;
				}
				else if (fieldEncoding == FieldEncodingEnum.NullTerminated)
				{
					WriteNullTerminatedBytes((byte[])value, terminators);
					return;
				}
				else
				{
					WriteBytes((byte[])value);
					return;
				}
			}
			else if (type == typeof(Int16))
			{
				WriteInt16((short)value, bigIndian, sizeLength ?? sizeof(Int16));
				return;
			}
			else if (type == typeof(Int32))
			{
				WriteInt32((int)value, bigIndian, sizeLength ?? sizeof(Int32));
				return;
			}
			else if (type == typeof(Int64))
			{
				WriteInt64((long)value, bigIndian, sizeLength ?? sizeof(Int64));
				return;
			}
			else if (type == typeof(UInt16))
			{
				WriteUInt16((ushort)value, bigIndian, sizeLength ?? sizeof(UInt16));
				return;
			}
			else if (type == typeof(UInt32))
			{
				WriteUInt32((uint)value, bigIndian, sizeLength ?? sizeof(UInt32));
				return;
			}
			else if (type == typeof(UInt64))
			{
				WriteUInt64((ulong)value, bigIndian, sizeLength ?? sizeof(UInt64));
				return;
			}
			else if (type == typeof(Single))
			{
				WriteSingle((float)value, bigIndian);
				return;
			}
			else if (type == typeof(Double))
			{
				WriteDouble((double)value, bigIndian);
				return;
			}
			else if (type == typeof(string))
			{
				if (fieldEncoding == FieldEncodingEnum.VariableLength)
				{
					WriteVariableLengthString((string)value, stringEncoding, bigIndian, sizeLength ?? sizeof(Int32));
					return;
				}
				else if (fieldEncoding == FieldEncodingEnum.FixedLength)
				{
					WriteFixedLengthString((string)value, length.Value, stringEncoding);
					return;
				}
				else
				{
					WriteNullTerminatedString((string)value, stringEncoding, terminators);
					return;
				}
			}
			else if (type == typeof(DateTime))
			{
				if (fieldEncoding == FieldEncodingEnum.TimeStamp)
				{
					WriteTimeStamp((DateTime)value, bigIndian);
					return;
				}
				else if (fieldEncoding == FieldEncodingEnum.DateTime)
				{
					WriteOleDateTime((DateTime)value, bigIndian);
					return;
				}
				else
				{
					WriteDateTime((DateTime)value, bigIndian);
					return;
				}
			}
			else if (type == typeof(TimeSpan))
			{
				WriteInt64(((TimeSpan)value).Ticks, bigIndian, sizeLength ?? sizeof(long));
				return;
			}
			else if (type.IsArray)
			{
				if (fieldEncoding == FieldEncodingEnum.FixedLength)
				{
					WriteArray((object[])value, length ?? ((object[])value).Length, type.GetElementType(), bigIndian, stringEncoding);
					return;
				}
				else
				{
					WriteVariableLengthArray((object[])value, type.GetElementType(), bigIndian, stringEncoding, length ?? sizeof(int));
					return;
				}
			}
			else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
			{
				Type argumentType = type.GenericTypeArguments[0];
				if (argumentType == typeof(object)) throw new NotSupportedException();
				var toArrayMethodInfo = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(type.GenericTypeArguments);
				Array result = (Array)toArrayMethodInfo.Invoke(null, new object[] { value });
				if (fieldEncoding == FieldEncodingEnum.FixedLength)
				{
					WriteArray(result, length ?? result.Length, type.GetElementType(), bigIndian, stringEncoding);
					return;
				}
				else
				{
					WriteVariableLengthArray(result, type.GetElementType(), bigIndian, stringEncoding, length ?? sizeof(int));
					return;
				}
			}

			throw new NotSupportedException();
		}

	}
}

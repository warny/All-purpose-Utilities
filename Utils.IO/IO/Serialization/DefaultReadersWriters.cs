using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Arrays;

namespace Utils.IO.Serialization
{
	public class DefaultReadersWriters : IEnumerable<IObjectReader>, IEnumerable<IObjectWriter>
	{
		private readonly object[] rw;

		public DefaultReadersWriters() { }
		public DefaultReadersWriters(
			bool? bigIndian= null, 
			int sizeOfInt16 = sizeof(Int16), 
			int sizeOfInt32 = sizeof(Int32), 
			int sizeOfInt64 = sizeof(Int64),
			object dateTimeReaderWriter	= null
			) 
		{
			bigIndian = !BitConverter.IsLittleEndian;
			dateTimeReaderWriter = dateTimeReaderWriter ?? new DateTimeReaderWriter();

			if (!(dateTimeReaderWriter is IObjectReader && dateTimeReaderWriter is IObjectWriter))
			{
				throw new ArgumentException($"{nameof(dateTimeReaderWriter)} doit être à la fois {nameof(IObjectReader)} et {nameof(IObjectWriter)}", nameof(dateTimeReaderWriter));
			}

			rw = new object[]
			{
				new ByteReaderWriter(),
				new Int16ReaderWriter(sizeOfInt16, bigIndian.Value),
				new Int32ReaderWriter(sizeOfInt16, bigIndian.Value),
				new Int64ReaderWriter(sizeOfInt16, bigIndian.Value),
				new SingleReaderWriter(bigIndian.Value),
				new DoubleReaderWriter(bigIndian.Value),
				dateTimeReaderWriter
			};

		}

		public IEnumerator GetEnumerator() => rw.GetEnumerator();
		IEnumerator<IObjectReader> IEnumerable<IObjectReader>.GetEnumerator() => rw.OfType<IObjectReader>().GetEnumerator();
		IEnumerator<IObjectWriter> IEnumerable<IObjectWriter>.GetEnumerator() => rw.OfType<IObjectWriter>().GetEnumerator();
	}

	public class ByteReaderWriter : IObjectReader, IObjectWriter
	{
		public ByteReaderWriter() { }

		public Type[] Types { get; } = new[] { typeof(byte), typeof(sbyte) };

		public bool Read(Reader reader, out object result)
		{
			result = reader.ReadByte();
			return true;
		}

		public bool Write(Writer writer, object obj)
		{
			writer.WriteByte((byte)obj);
			return true;
		}
	}

	public class Int16ReaderWriter : IObjectReader, IObjectWriter
	{
		public int Size { get; }
		public bool BigIndian { get; }

		public Int16ReaderWriter() : this(sizeof(Int16), !BitConverter.IsLittleEndian) { }
		public Int16ReaderWriter(int size) : this(size, !BitConverter.IsLittleEndian) { }
		public Int16ReaderWriter(bool bigIndian) : this(sizeof(Int16), bigIndian) { }
		public Int16ReaderWriter(int size, bool bigIndian)
		{
			Size = size;
			BigIndian = bigIndian;
		}

		public Type[] Types { get; } = new[] { typeof(Int16), typeof(UInt16) };

		public bool Read(Reader reader, out object result)
		{
			byte[] buffer = reader.ReadBytes(Size);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Int16));
			result = BitConverter.ToInt16(buffer, 0);
			return true;
		}

		public bool Write(Writer writer, object obj)
		{
			byte[] buffer = BitConverter.GetBytes((Int16)obj);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, Size);
			writer.WriteBytes(buffer);
			return true;
		}
	}

	public class Int32ReaderWriter : IObjectReader, IObjectWriter
	{
		public int Size { get; }
		public bool BigIndian { get; }

		public Int32ReaderWriter() : this(sizeof(Int32), !BitConverter.IsLittleEndian) { }
		public Int32ReaderWriter(int size) : this(size, !BitConverter.IsLittleEndian) { }
		public Int32ReaderWriter(bool bigIndian) :	this(sizeof(Int32), bigIndian) { }
		public Int32ReaderWriter(int size, bool bigIndian)
		{
			Size = size;
			BigIndian = bigIndian;
		}

		public Type[] Types { get; } = new[] { typeof(Int32), typeof(UInt32) };

		public bool Read(Reader reader, out object result)
		{
			byte[] buffer = reader.ReadBytes(Size);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Int32));
			result = BitConverter.ToInt32(buffer, 0);
			return true;
		}

		public bool Write(Writer writer, object obj)
		{
			byte[] buffer = BitConverter.GetBytes((Int32)obj);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, Size);
			writer.WriteBytes(buffer);
			return true;
		}
	}
	public class Int64ReaderWriter : IObjectReader, IObjectWriter
	{
		public int Size { get; }
		public bool BigIndian { get; }

		public Int64ReaderWriter() : this(sizeof(Int64), !BitConverter.IsLittleEndian) { }
		public Int64ReaderWriter(int size) : this(size, !BitConverter.IsLittleEndian) { }
		public Int64ReaderWriter(bool bigIndian) : this(sizeof(Int64), bigIndian) { }
		public Int64ReaderWriter(int size, bool bigIndian)
		{
			Size = size;
			BigIndian = bigIndian;
		}

		public Type[] Types { get; } = new[] { typeof(Int64), typeof(UInt64) };

		public bool Read(Reader reader, out object result)
		{
			byte[] buffer = reader.ReadBytes(Size);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Int64));
			result = BitConverter.ToInt64(buffer, 0);
			return true;
		}

		public bool Write(Writer writer, object obj)
		{
			byte[] buffer = BitConverter.GetBytes((Int64)obj);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, Size);
			writer.WriteBytes(buffer);
			return true;
		}
	}
	public class SingleReaderWriter : IObjectReader, IObjectWriter
	{
		public bool BigIndian { get; }

		public SingleReaderWriter() : this(!BitConverter.IsLittleEndian) { }
		public SingleReaderWriter(bool bigIndian) 
		{
			BigIndian = bigIndian;
		}

		public Type[] Types { get; } = new[] { typeof(Single) };

		public bool Read(Reader reader, out object result)
		{
			byte[] buffer = reader.ReadBytes(sizeof(Single));
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Single));
			result = BitConverter.ToSingle(buffer, 0);
			return true;
		}

		public bool Write(Writer writer, object obj)
		{
			byte[] buffer = BitConverter.GetBytes((Single)obj);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Single));
			writer.WriteBytes(buffer);
			return true;
		}
	}
	public class DoubleReaderWriter : IObjectReader, IObjectWriter
	{
		public bool BigIndian { get; }

		public DoubleReaderWriter() : this(!BitConverter.IsLittleEndian) { }
		public DoubleReaderWriter(bool bigIndian)
		{
			BigIndian = bigIndian;
		}

		public Type[] Types { get; } = new[] { typeof(Double) };

		public bool Read(Reader reader, out object result)
		{
			byte[] buffer = reader.ReadBytes(sizeof(Double));
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Double));
			result = BitConverter.ToDouble(buffer, 0);
			return true;
		}

		public bool Write(Writer writer, object obj)
		{
			byte[] buffer = BitConverter.GetBytes((Double)obj);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Double));
			writer.WriteBytes(buffer);
			return true;
		}
	}

	public class OLEDateTimeReaderWriter : IObjectReader, IObjectWriter
	{
		public bool BigIndian { get; }

		public OLEDateTimeReaderWriter() : this(!BitConverter.IsLittleEndian) { }
		public OLEDateTimeReaderWriter(bool bigIndian)
		{
			BigIndian = bigIndian;
		}

		public Type[] Types { get; } = new[] { typeof(DateTime) };

		public bool Read(Reader reader, out object result)
		{
			byte[] buffer = reader.ReadBytes(sizeof(Double));
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Double));
			var temp = BitConverter.ToDouble(buffer, 0);
			result = DateTime.FromOADate(temp);
			return true;
		}

		public bool Write(Writer writer, object obj)
		{
			var temp = ((DateTime)obj).ToOADate();
			byte[] buffer = BitConverter.GetBytes(temp);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Double));
			writer.WriteBytes(buffer);
			return true;
		}
	}

	public class DateTimeReaderWriter : IObjectReader, IObjectWriter
	{
		public bool BigIndian { get; }

		public DateTimeReaderWriter() : this(!BitConverter.IsLittleEndian) { }
		public DateTimeReaderWriter(bool bigIndian)
		{
			BigIndian = bigIndian;
		}

		public Type[] Types { get; } = new[] { typeof(DateTime) };

		public bool Read(Reader reader, out object result)
		{
			byte[] buffer = reader.ReadBytes(sizeof(long));
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(long));
			var temp = BitConverter.ToInt64(buffer, 0);
			result = new DateTime(temp);
			return true;
		}

		public bool Write(Writer writer, object obj)
		{
			var temp = ((DateTime)obj).Ticks;
			byte[] buffer = BitConverter.GetBytes(temp);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Double));
			writer.WriteBytes(buffer);
			return true;
		}
	}

	public class TimeStampReaderWriter : IObjectReader, IObjectWriter
	{
		public int Size { get; }
		public bool BigIndian { get; }
		public DateTime BaseDateTime { get; }

		public TimeStampReaderWriter() : this(null, sizeof(Int64), !BitConverter.IsLittleEndian) { }
		public TimeStampReaderWriter(int size) : this(null, size, !BitConverter.IsLittleEndian) { }
		public TimeStampReaderWriter(bool bigIndian) : this(null, sizeof(Int64), bigIndian) { }
		public TimeStampReaderWriter(DateTime? baseDateTime) : this(baseDateTime, sizeof(Int64), !BitConverter.IsLittleEndian) { }
		public TimeStampReaderWriter(DateTime? baseDateTime, int size) : this(baseDateTime, size, !BitConverter.IsLittleEndian) { }
		public TimeStampReaderWriter(DateTime? baseDateTime, bool bigIndian) : this(baseDateTime, sizeof(Int64), bigIndian) { }
		public TimeStampReaderWriter(DateTime? baseDateTime, int size, bool bigIndian)
		{
			Size = size;
			BigIndian = bigIndian;
			BaseDateTime = baseDateTime ?? new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
		}

		public Type[] Types { get; } = new[] { typeof(DateTime) };

		public bool Read(Reader reader, out object result)
		{
			byte[] buffer = reader.ReadBytes(Size);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, sizeof(Int64));
			var temp = BitConverter.ToInt64(buffer, 0);
			result = BaseDateTime.AddSeconds(temp).ToLocalTime();
			return true;
		}

		public bool Write(Writer writer, object obj)
		{
			var timestamp = (long)(((DateTime)obj).ToUniversalTime() - BaseDateTime).TotalSeconds;
			byte[] buffer = BitConverter.GetBytes(timestamp);
			buffer = ArrayUtils.Adjust(buffer, BigIndian ^ BitConverter.IsLittleEndian, Size);
			writer.WriteBytes(buffer);
			return true;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Objects;

public static class Types
{
	public static Type String { get; } = typeof(string);

	public static Type Byte { get; } = typeof(byte);
	public static Type UInt16 { get; } = typeof(UInt16);
	public static Type UInt32 { get; } = typeof(UInt32);
	public static Type UInt64 { get; } = typeof(UInt64);

	public static Type SByte { get; } = typeof(sbyte);
	public static Type Int16 { get; } = typeof(Int16);
	public static Type Int32 { get; } = typeof(Int32);
	public static Type Int64 { get; } = typeof(Int64);

	public static Type Decimal { get; } = typeof(Decimal);
	public static Type Single { get; } = typeof(Single);
	public static Type Double { get; } = typeof(Double);

	public static Type DateTime { get; } = typeof(DateTime);
	public static Type TimeSpan { get; } = typeof(TimeSpan);
	public static Type DateTimeOffset { get; } = typeof(DateTimeOffset);

	public static Type Guid { get; } = typeof(Guid);

	public static Type UIntPtr { get; } = typeof(UIntPtr);

	public static Type[] Number { get; } = { Byte, UInt16, UInt32, UInt64, SByte, Int16, Int32, Int64, Decimal, Single, Double };
	public static Type[] UnsignedNumber { get; } = { Byte, UInt16, UInt32, UInt64 };
	public static Type[] SignedNumber { get; } = { SByte, Int16, Int32, Int64 };
	public static Type[] FloatingPointNumber { get; } = { Decimal, Single, Double };
	public static Type[] _8BitsNumberI { get; } = { SByte, Byte };
	public static Type[] _16BitsNumberI { get; } = { Int16, UInt16 };
	public static Type[] _32BitsNumberI { get; } = { Int32, UInt32 };
	public static Type[] _32BitsNumberF { get; } = { Int32, UInt32, Single };
	public static Type[] _64BitsNumberI { get; } = { Int64, UInt64 };
	public static Type[] _64BitsNumberIF { get; } = { Int64, UInt64, Double };
	public static Type[] _128BitsNumberIF { get; } = { Decimal };

}

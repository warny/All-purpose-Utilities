using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Objects;

/// <summary>
/// Provides cached <see cref="Type"/> instances for common .NET primitives and helper groups for numeric classifications.
/// </summary>
public static class Types
{
    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="string"/>.
    /// </summary>
    public static Type String { get; } = typeof(string);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="byte"/>.
    /// </summary>
    public static Type Byte { get; } = typeof(byte);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="ushort"/>.
    /// </summary>
    public static Type UInt16 { get; } = typeof(UInt16);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="uint"/>.
    /// </summary>
    public static Type UInt32 { get; } = typeof(UInt32);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="ulong"/>.
    /// </summary>
    public static Type UInt64 { get; } = typeof(UInt64);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="sbyte"/>.
    /// </summary>
    public static Type SByte { get; } = typeof(sbyte);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="short"/>.
    /// </summary>
    public static Type Int16 { get; } = typeof(Int16);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="int"/>.
    /// </summary>
    public static Type Int32 { get; } = typeof(Int32);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="long"/>.
    /// </summary>
    public static Type Int64 { get; } = typeof(Int64);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="decimal"/>.
    /// </summary>
    public static Type Decimal { get; } = typeof(Decimal);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="float"/>.
    /// </summary>
    public static Type Single { get; } = typeof(Single);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="double"/>.
    /// </summary>
    public static Type Double { get; } = typeof(Double);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="DateTime"/>.
    /// </summary>
    public static Type DateTime { get; } = typeof(DateTime);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="TimeSpan"/>.
    /// </summary>
    public static Type TimeSpan { get; } = typeof(TimeSpan);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="DateTimeOffset"/>.
    /// </summary>
    public static Type DateTimeOffset { get; } = typeof(DateTimeOffset);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="Guid"/>.
    /// </summary>
    public static Type Guid { get; } = typeof(Guid);

    /// <summary>
    /// Gets the <see cref="Type"/> instance that represents <see cref="UIntPtr"/>.
    /// </summary>
    public static Type UIntPtr { get; } = typeof(UIntPtr);

    /// <summary>
    /// Gets the collection of numeric types, both signed and unsigned.
    /// </summary>
    public static Type[] Number { get; } = { Byte, UInt16, UInt32, UInt64, SByte, Int16, Int32, Int64, Decimal, Single, Double };

    /// <summary>
    /// Gets the collection of unsigned numeric types.
    /// </summary>
    public static Type[] UnsignedNumber { get; } = { Byte, UInt16, UInt32, UInt64 };

    /// <summary>
    /// Gets the collection of signed numeric types.
    /// </summary>
    public static Type[] SignedNumber { get; } = { SByte, Int16, Int32, Int64 };

    /// <summary>
    /// Gets the collection of floating-point numeric types.
    /// </summary>
    public static Type[] FloatingPointNumber { get; } = { Decimal, Single, Double };

    /// <summary>
    /// Gets the collection of integer numeric types limited to eight bits.
    /// </summary>
    public static Type[] _8BitsNumberI { get; } = { SByte, Byte };

    /// <summary>
    /// Gets the collection of integer numeric types limited to sixteen bits.
    /// </summary>
    public static Type[] _16BitsNumberI { get; } = { Int16, UInt16 };

    /// <summary>
    /// Gets the collection of integer numeric types limited to thirty-two bits.
    /// </summary>
    public static Type[] _32BitsNumberI { get; } = { Int32, UInt32 };

    /// <summary>
    /// Gets the collection of numeric types that may represent thirty-two-bit integers or floats.
    /// </summary>
    public static Type[] _32BitsNumberF { get; } = { Int32, UInt32, Single };

    /// <summary>
    /// Gets the collection of integer numeric types limited to sixty-four bits.
    /// </summary>
    public static Type[] _64BitsNumberI { get; } = { Int64, UInt64 };

    /// <summary>
    /// Gets the collection of numeric types that may represent sixty-four-bit integers or floats.
    /// </summary>
    public static Type[] _64BitsNumberIF { get; } = { Int64, UInt64, Double };

    /// <summary>
    /// Gets the collection of numeric types that may represent one-hundred-and-twenty-eight-bit numbers or floats.
    /// </summary>
    public static Type[] _128BitsNumberIF { get; } = { Decimal };

    /// <summary>
    /// Creates a nullable wrapper type for the provided value type.
    /// </summary>
    /// <param name="type">Type that should be made nullable.</param>
    /// <returns>The nullable version of the provided type.</returns>
    /// <exception cref="ArgumentException">Thrown when the type cannot be made nullable.</exception>
    public static Type Nullable(this Type type)
    {
        type.Arg().MustNotBeNull();
        type.ArgMustBe(t => t.IsValueType || t.IsInterface, "Only a struct or an interface can be made nullable");
        return typeof(Nullable<>).MakeGenericType(type);
    }

}

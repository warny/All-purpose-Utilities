using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Collections;
using Utils.Objects;

namespace Utils.Arrays;

/// <summary>
/// Provides preconfigured array equality comparers for common types.
/// </summary>
public static class ArrayEqualityComparers
{
    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="bool"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<bool>> Boolean { get; } = EnumerableEqualityComparer<bool>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="byte"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<byte>> Byte { get; } = EnumerableEqualityComparer<byte>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="sbyte"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<sbyte>> SByte { get; } = EnumerableEqualityComparer<sbyte>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="short"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<short>> Int16 { get; } = EnumerableEqualityComparer<short>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="int"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<int>> Int32 { get; } = EnumerableEqualityComparer<int>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="long"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<long>> Int64 { get; } = EnumerableEqualityComparer<long>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="ushort"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<ushort>> UInt16 { get; } = EnumerableEqualityComparer<ushort>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="uint"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<uint>> UInt32 { get; } = EnumerableEqualityComparer<uint>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="ulong"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<ulong>> UInt64 { get; } = EnumerableEqualityComparer<ulong>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="float"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<float>> Single { get; } = EnumerableEqualityComparer<float>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="double"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<double>> Double { get; } = EnumerableEqualityComparer<double>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="decimal"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<decimal>> Decimal { get; } = EnumerableEqualityComparer<decimal>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="DateTime"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<DateTime>> DateTime { get; } = EnumerableEqualityComparer<DateTime>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="Guid"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<Guid>> Guid { get; } = EnumerableEqualityComparer<Guid>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="Type"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<Type>> Type { get; } = EnumerableEqualityComparer<Type>.Default;

    /// <summary>
    /// Gets an equality comparer for read-only collections of <see cref="string"/> values.
    /// </summary>
    public static IEqualityComparer<IReadOnlyCollection<string>> String { get; } = EnumerableEqualityComparer<string>.Default;
}

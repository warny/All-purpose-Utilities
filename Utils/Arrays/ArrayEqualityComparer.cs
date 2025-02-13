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
	public static IEqualityComparer<IReadOnlyCollection<bool>> Boolean { get; } = EnumerableEqualityComparer<bool>.Default;
	public static IEqualityComparer<IReadOnlyCollection<byte>> Byte { get; } = EnumerableEqualityComparer<byte>.Default;
	public static IEqualityComparer<IReadOnlyCollection<sbyte>> SByte { get; } = EnumerableEqualityComparer<sbyte>.Default;
	public static IEqualityComparer<IReadOnlyCollection<short>> Int16 { get; } = EnumerableEqualityComparer<short>.Default;
	public static IEqualityComparer<IReadOnlyCollection<int>> Int32 { get; } = EnumerableEqualityComparer<int>.Default;
	public static IEqualityComparer<IReadOnlyCollection<long>> Int64 { get; } = EnumerableEqualityComparer<long>.Default;
	public static IEqualityComparer<IReadOnlyCollection<ushort>> UInt16 { get; } = EnumerableEqualityComparer<ushort>.Default;
	public static IEqualityComparer<IReadOnlyCollection<uint>> UInt32 { get; } = EnumerableEqualityComparer<uint>.Default;
	public static IEqualityComparer<IReadOnlyCollection<ulong>> UInt64 { get; } = EnumerableEqualityComparer<ulong>.Default;
	public static IEqualityComparer<IReadOnlyCollection<float>> Single { get; } = EnumerableEqualityComparer<float>.Default;
	public static IEqualityComparer<IReadOnlyCollection<double>> Double { get; } = EnumerableEqualityComparer<double>.Default;
	public static IEqualityComparer<IReadOnlyCollection<decimal>> Decimal { get; } = EnumerableEqualityComparer<decimal>.Default;
	public static IEqualityComparer<IReadOnlyCollection<DateTime>> DateTime { get; } = EnumerableEqualityComparer<DateTime>.Default;
	public static IEqualityComparer<IReadOnlyCollection<Guid>> Guid { get; } = EnumerableEqualityComparer<Guid>.Default;
	public static IEqualityComparer<IReadOnlyCollection<Type>> Type { get; } = EnumerableEqualityComparer<Type>.Default;
	public static IEqualityComparer<IReadOnlyCollection<string>> String { get; } = EnumerableEqualityComparer<string>.Default;
}

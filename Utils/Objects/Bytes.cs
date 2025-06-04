using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Utils.Arrays;
using Utils.Collections;

namespace Utils.Objects;

/// <summary>
/// Represents a read-only view over a sequence of bytes.
/// </summary>
public readonly struct Bytes :
	IReadOnlyList<byte>,
	IReadOnlyCollection<byte>,
	IEnumerable<byte>,
	ICloneable,
	IEquatable<Bytes>,
	IEquatable<byte[]>,
	IComparable<Bytes>,
	IComparable<byte[]>,
	IComparable,
	IComparisonOperators<Bytes, Bytes, bool>,
	IComparisonOperators<Bytes, byte[], bool>,
	IEqualityOperators<Bytes, Bytes, bool>,
	IEqualityOperators<Bytes, byte[], bool>,
	IAdditionOperators<Bytes, Bytes, Bytes>,
	IAdditionOperators<Bytes, byte[], Bytes>
{
	private static readonly ArrayComparer<byte> _comparer = new ArrayComparer<byte>();

	private readonly byte[] _innerBytes;

	/// <summary>
	/// Gets an empty <see cref="Bytes"/> instance with no data.
	/// </summary>
	public static Bytes Empty { get; } = new Bytes(Array.Empty<byte>());

	/// <summary>
	/// Gets the number of bytes stored in this <see cref="Bytes"/> instance.
	/// </summary>
	public int Count => _innerBytes?.Length ?? 0;

	/// <summary>
	/// Initializes a new instance of the <see cref="Bytes"/> struct
	/// from a single <paramref name="byteArray"/>.
	/// </summary>
	/// <param name="byteArray">The byte array to store in the struct.</param>
	internal Bytes(params byte[] byteArray)
	{
		_innerBytes = byteArray ?? [];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Bytes"/> struct
	/// by concatenating multiple <paramref name="byteArrays"/>.
	/// </summary>
	/// <param name="byteArrays">An array of byte arrays to concatenate.</param>
	internal Bytes(params byte[][] byteArrays)
	{
		if (byteArrays is null)
		{
			_innerBytes = [];
			return;
		}

		int totalLength = 0;
		foreach (var arr in byteArrays)
		{
			if (arr is not null)
				totalLength += arr.Length;
		}

		_innerBytes = new byte[totalLength];
		int position = 0;

		foreach (var arr in byteArrays)
		{
			if (arr.IsNullOrEmptyCollection()) continue;
			Array.Copy(arr, 0, _innerBytes, position, arr.Length);
			position += arr.Length;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Bytes"/> struct
	/// by concatenating multiple <see cref="Bytes"/> instances.
	/// </summary>
	/// <param name="bytess">An array of <see cref="Bytes"/> objects to concatenate.</param>
	internal Bytes(params Bytes[] bytess)
	{
		if (bytess is null)
		{
			_innerBytes = Array.Empty<byte>();
			return;
		}

		int totalLength = 0;
		foreach (var b in bytess)
		{
			totalLength += b._innerBytes?.Length ?? 0;
		}

		_innerBytes = new byte[totalLength];
		int position = 0;

		foreach (var b in bytess)
		{
			if (b.Count == 0) continue;
			Array.Copy(b._innerBytes, 0, _innerBytes, position, b.Count);
			position += b.Count;
		}
	}

	/// <summary>
	/// Returns a copy of the underlying byte array.
	/// </summary>
	/// <returns>A new <see cref="byte"/> array with the same contents.</returns>
	public byte[] ToArray()
	{
		// If _innerBytes is null or empty, return an empty array
		if (_innerBytes is null || _innerBytes.Length == 0)
			return Array.Empty<byte>();

		var result = new byte[_innerBytes.Length];
		Array.Copy(_innerBytes, 0, result, 0, _innerBytes.Length);
		return result;
	}

	/// <summary>
	/// Returns an enumerator that iterates through the byte array.
	/// </summary>
	public IEnumerator<byte> GetEnumerator() =>
		(_innerBytes ?? Array.Empty<byte>()).AsEnumerable().GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>
	/// Gets the <see cref="byte"/> at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index of the element to get.</param>
	/// <returns>The byte at the specified <paramref name="index"/>.</returns>
	public byte this[int index] => _innerBytes[index];

	#region Operator Overloads

	/// <summary>
	/// Implicitly converts a <see cref="byte"/> array to a <see cref="Bytes"/> instance.
	/// </summary>
	public static implicit operator Bytes(byte[] bytes) => new Bytes(bytes);

	/// <summary>
	/// Implicitly converts a <see cref="Bytes"/> instance to a <see cref="byte"/> array.
	/// </summary>
	public static implicit operator byte[](Bytes bytes) => [.. bytes];

	public static bool operator ==(Bytes left, Bytes right) => left.Equals(right);
	public static bool operator ==(Bytes left, byte[] right) => left.Equals(right);
	public static bool operator ==(byte[] left, Bytes right) => right.Equals(left);

	public static bool operator !=(Bytes left, Bytes right) => !left.Equals(right);
	public static bool operator !=(Bytes left, byte[] right) => !left.Equals(right);
	public static bool operator !=(byte[] left, Bytes right) => !right.Equals(left);

	public static bool operator >(Bytes left, Bytes right) => left.CompareTo(right) > 0;
	public static bool operator >(Bytes left, byte[] right) => left.CompareTo(right) > 0;
	public static bool operator >(byte[] left, Bytes right) => right.CompareTo(left) < 0;

	public static bool operator <(Bytes left, Bytes right) => left.CompareTo(right) < 0;
	public static bool operator <(Bytes left, byte[] right) => left.CompareTo(right) < 0;
	public static bool operator <(byte[] left, Bytes right) => right.CompareTo(left) > 0;

	public static bool operator >=(Bytes left, Bytes right) => left.CompareTo(right) >= 0;
	public static bool operator >=(Bytes left, byte[] right) => left.CompareTo(right) >= 0;
	public static bool operator >=(byte[] left, Bytes right) => right.CompareTo(left) <= 0;

	public static bool operator <=(Bytes left, Bytes right) => left.CompareTo(right) <= 0;
	public static bool operator <=(Bytes left, byte[] right) => left.CompareTo(right) <= 0;
	public static bool operator <=(byte[] left, Bytes right) => right.CompareTo(left) >= 0;

	public static Bytes operator +(Bytes left, Bytes right) => new Bytes(left, right);
	public static Bytes operator +(Bytes left, byte[] right) => new Bytes(left._innerBytes, right);
	public static Bytes operator +(byte[] left, Bytes right) => new Bytes(left, right._innerBytes);

	#endregion

	#region Overrides

	/// <inheritdoc />
	public override int GetHashCode() => ObjectUtils.ComputeHash(_innerBytes);

	/// <inheritdoc />
	public override bool Equals(object obj)
	{
		return obj switch
		{
			Bytes b => Equals(b),
			byte[] arr => Equals(arr),
			_ => false
		};
	}

	#endregion

	#region Equality

	/// <summary>
	/// Determines whether the specified <see cref="Bytes"/> is equal to the current <see cref="Bytes"/>.
	/// </summary>
	/// <param name="other">Another <see cref="Bytes"/> to compare.</param>
	/// <returns><see langword="true"/> if both contain identical byte sequences; otherwise <see langword="false"/>.</returns>
	public bool Equals(Bytes other)
	{
		return _comparer.Compare(_innerBytes, other._innerBytes) == 0;
	}

	/// <summary>
	/// Determines whether the specified byte array is equal to the current <see cref="Bytes"/>.
	/// </summary>
	/// <param name="other">A <see cref="byte"/> array to compare.</param>
	/// <returns><see langword="true"/> if both contain identical byte sequences; otherwise <see langword="false"/>.</returns>
	public bool Equals(byte[] other)
	{
		if (other is null) return false;
		return _comparer.Compare(_innerBytes, other) == 0;
	}

	#endregion

	#region Comparison

	/// <summary>
	/// Compares the current instance with another object of the same type.
	/// </summary>
	/// <param name="obj">An object to compare, which can be <see cref="Bytes"/> or <see cref="byte[]"/>.</param>
	/// <returns>A value indicating the relative order of the objects.</returns>
	/// <exception cref="InvalidOperationException">If <paramref name="obj"/> is not a valid type.</exception>
	public int CompareTo(object obj)
	{
		return obj switch
		{
			Bytes b => CompareTo(b),
			byte[] arr => CompareTo(arr),
			_ => throw new InvalidOperationException($"Cannot compare {nameof(Bytes)} with this object.")
		};
	}

	/// <summary>
	/// Compares the current <see cref="Bytes"/> with another <see cref="Bytes"/>.
	/// </summary>
	/// <param name="other">A <see cref="Bytes"/> to compare.</param>
	/// <returns>
	/// A signed integer that indicates the relative order of the compared objects.
	/// 0 if they are equal, less than 0 if this is less than <paramref name="other"/>,
	/// and greater than 0 if this is greater.
	/// </returns>
	public int CompareTo(Bytes other)
	{
		return _comparer.Compare(_innerBytes, other._innerBytes);
	}

	/// <summary>
	/// Compares the current <see cref="Bytes"/> with a <see cref="byte"/> array.
	/// </summary>
	/// <param name="other">A <see cref="byte"/> array to compare.</param>
	/// <returns>
	/// A signed integer that indicates the relative order of the compared objects.
	/// 0 if they are equal, less than 0 if this is less than <paramref name="other"/>,
	/// and greater than 0 if this is greater.
	/// </returns>
	public int CompareTo(byte[] other)
	{
		if (other is null) return 1; // Consider null to be "less" than any Bytes
		return _comparer.Compare(_innerBytes, other);
	}

	#endregion

	#region Clone

	/// <summary>
	/// Creates a new copy of the current <see cref="Bytes"/> object.
	/// </summary>
	/// <returns>A new <see cref="Bytes"/> containing a copy of the internal array.</returns>
	public object Clone() => new Bytes(this);

	#endregion

	#region Copy Methods

	/// <summary>
	/// Copies the contents of this <see cref="Bytes"/> into the specified <paramref name="array"/>
	/// starting at <paramref name="index"/> and using an optional <paramref name="length"/>.
	/// </summary>
	/// <param name="array">The target array.</param>
	/// <param name="index">
	/// The destination index in <paramref name="array"/>.
	/// If negative, it is offset from the end of <paramref name="array"/>.
	/// </param>
	/// <param name="length">Number of bytes to copy. If null, copies as many as possible.</param>
	public void CopyTo(byte[] array, int index, int? length = null)
	{
		index.ArgMustBeLesserOrEqualsThan(array.Length);
		if (index < 0) index = array.Length + index;
		index.ArgMustBeGreaterOrEqualsThan(0);

		int actualLength = length ?? _innerBytes.Length;
		if (actualLength + index > array.Length)
		{
			actualLength = Math.Min(array.Length - index, _innerBytes.Length);
		}

		Array.Copy(_innerBytes, 0, array, index, actualLength);
	}

	/// <summary>
	/// Writes the contents of this <see cref="Bytes"/> to a <see cref="Stream"/>
	/// starting at the specified <paramref name="index"/> and using an optional <paramref name="length"/>.
	/// </summary>
	/// <param name="s">The stream to write to.</param>
	/// <param name="index">The source index in the <see cref="_innerBytes"/> array.</param>
	/// <param name="length">Number of bytes to write. If null, writes as many as possible.</param>
	public void CopyTo(Stream s, int index, int? length = null)
	{
		if (s is null) throw new ArgumentNullException(nameof(s));

		int actualLength = length ?? Count;
		if (index < 0 || index > Count || index + actualLength > Count)
			throw new ArgumentOutOfRangeException(nameof(index), "Index and length must refer to a valid range in the array.");

		s.Write(_innerBytes, index, actualLength);
	}

	#endregion

	#region Parsing / String Formatting

	/// <summary>
	/// Returns a string that represents the byte array in hexadecimal format.
	/// </summary>
	public override string ToString()
	{
		if (_innerBytes is null || _innerBytes.Length == 0)
			return string.Empty;

		return string.Join(" ", _innerBytes.Select(b => b.ToString("X2")));
	}

	/// <summary>
	/// Creates a <see cref="Bytes"/> object from a string of hexadecimal numbers
	/// separated by spaces or common delimiters.
	/// </summary>
	/// <param name="s">A string containing hexadecimal numbers to parse.</param>
	/// <returns>A <see cref="Bytes"/> instance containing the parsed data.</returns>
	/// <exception cref="FormatException">
	/// Thrown if the string contains invalid hexadecimal values.
	/// </exception>
	public static Bytes Parse(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return Empty;

                var values = s.Split(
                        [' ', '\n', '\t', '\r', ',', ';'],
                        StringSplitOptions.RemoveEmptyEntries);

		var bytes = values.Select(v => byte.Parse(v, System.Globalization.NumberStyles.HexNumber))
						  .ToArray();

		return new Bytes(bytes);
	}

	#endregion
}

/// <summary>
/// Extension methods for creating and concatenating <see cref="Bytes"/> instances.
/// </summary>
public static class BytesExtensions
{
	/// <summary>
	/// Creates a new <see cref="Bytes"/> that copies the data from the specified <paramref name="byteArray"/>.
	/// </summary>
	/// <param name="byteArray">The byte array to convert.</param>
	/// <returns>A <see cref="Bytes"/> instance containing a copy of <paramref name="byteArray"/>.</returns>
	public static Bytes ToBytes(this byte[] byteArray)
	{
		if (byteArray.IsNullOrEmptyCollection()) return Bytes.Empty;

		var copy = new byte[byteArray.Length];
		Array.Copy(byteArray, 0, copy, 0, byteArray.Length);
		return new Bytes(copy);
	}

	/// <summary>
	/// Creates a new <see cref="Bytes"/> that copies the data from the specified enumerable of bytes.
	/// </summary>
	/// <param name="byteArray">An enumerable of bytes to convert.</param>
	/// <returns>A <see cref="Bytes"/> instance containing a copy of the data.</returns>
	public static Bytes ToBytes(this IEnumerable<byte> byteArray)
	{
		if (byteArray is null) return Bytes.Empty;
		return new Bytes(byteArray.ToArray());
	}

	/// <summary>
	/// Creates a <see cref="Bytes"/> instance that references the specified <paramref name="byteArray"/>
	/// without creating an internal copy. (Be careful with mutations!)
	/// </summary>
	/// <param name="byteArray">The byte array to wrap in a <see cref="Bytes"/>.</param>
	/// <returns>A <see cref="Bytes"/> referencing the exact array.</returns>
	public static Bytes AsBytes(this byte[] byteArray)
	{
		return byteArray is not null ? new Bytes(byteArray) : Bytes.Empty;
	}

	/// <summary>
	/// Concatenates all the byte arrays into a single <see cref="Bytes"/> instance.
	/// </summary>
	/// <param name="byteArrays">A collection of byte arrays to join.</param>
	/// <returns>A <see cref="Bytes"/> that contains all data from <paramref name="byteArrays"/>.</returns>
	public static Bytes Join(IEnumerable<byte[]> byteArrays)
	{
		return Join(byteArrays?.ToArray() ?? []);
	}

	/// <summary>
	/// Concatenates all the byte arrays into a single <see cref="Bytes"/> instance.
	/// </summary>
	/// <param name="byteArrays">A list of byte arrays to join.</param>
	/// <returns>A <see cref="Bytes"/> that contains all data from <paramref name="byteArrays"/>.</returns>
	public static Bytes Join(params byte[][] byteArrays)
	{
		return new Bytes(byteArrays);
	}

	/// <summary>
	/// Concatenates all the <see cref="Bytes"/> instances into a single <see cref="Bytes"/>.
	/// </summary>
	/// <param name="byteArrays">A collection of <see cref="Bytes"/> instances to join.</param>
	/// <returns>A <see cref="Bytes"/> that contains all data.</returns>
	public static Bytes Join(IEnumerable<Bytes> byteArrays)
	{
		return Join(byteArrays?.ToArray() ?? []);
	}

	/// <summary>
	/// Concatenates all the <see cref="Bytes"/> instances into a single <see cref="Bytes"/>.
	/// </summary>
	/// <param name="byteArrays">A list of <see cref="Bytes"/> objects to join.</param>
	/// <returns>A <see cref="Bytes"/> that contains all data.</returns>
	public static Bytes Join(params Bytes[] byteArrays)
	{
		return new Bytes(byteArrays);
	}

	/// <summary>
	/// Concatenates multiple sequences of bytes into a single <see cref="Bytes"/> instance.
	/// </summary>
	/// <param name="byteArrays">An enumerable of byte sequences to join.</param>
	/// <returns>A <see cref="Bytes"/> that contains all data from <paramref name="byteArrays"/>.</returns>
	public static Bytes Join(IEnumerable<IEnumerable<byte>> byteArrays)
	{
		if (byteArrays is null)
			return Bytes.Empty;

		using var memory = new MemoryStream();
		foreach (var sequence in byteArrays)
		{
			if (sequence is null) continue;
			foreach (var b in sequence)
				memory.WriteByte(b);
		}

		return new Bytes(memory.ToArray());
	}
}

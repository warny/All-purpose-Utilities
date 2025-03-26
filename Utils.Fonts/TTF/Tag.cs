using System;
using System.Numerics;

namespace Utils.Fonts.TTF;

/// <summary>
/// Represents a 4-byte identifier used in font file formats such as TrueType (TTF).
/// Encodes a string of up to 4 ASCII characters into a 32-bit integer.
/// Provides implicit conversions and equality comparisons with strings, integers, and byte arrays.
/// </summary>
public readonly struct Tag :
	IEquatable<Tag>, IEqualityOperators<Tag, Tag, bool>,
	IEquatable<int>, IEqualityOperators<Tag, int, bool>,
	IEquatable<TableTypes.Tags>, IEqualityOperators<Tag, TableTypes.Tags, bool>,
	IEquatable<string>, IEqualityOperators<Tag, string, bool>,
	IEquatable<byte[]>, IEqualityOperators<Tag, byte[], bool>
{
	/// <summary>
	/// The 4-character string representation of the tag.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// The 32-bit integer value representing the tag.
	/// </summary>
	public int Value { get; }

	/// <summary>
	/// Initializes a tag from a 4-character string (padded with spaces if shorter).
	/// </summary>
	/// <param name="name">The tag name (max 4 characters).</param>
	/// <exception cref="ArgumentNullException">Thrown if name is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown if name is longer than 4 characters or if it contains des caractères non-ASCII.
	/// </exception>
	public Tag(string name)
	{
		if (name is null)
			throw new ArgumentNullException(nameof(name));
		if (name.Length > 4)
			throw new ArgumentException("Name must be 4 characters or fewer.", nameof(name));

		// Validation des caractères ASCII
		foreach (var c in name)
		{
			if (c > 127)
				throw new ArgumentException("All characters must be ASCII.", nameof(name));
		}

		name = name.PadRight(4, ' ');
		Name = name;
		Value = name[0] << 24 | name[1] << 16 | name[2] << 8 | name[3];
	}

	/// <summary>
	/// Initializes a tag from a 4-byte array.
	/// </summary>
	/// <param name="value">The byte array (must be exactly 4 bytes).</param>
	/// <exception cref="ArgumentNullException">Thrown if value is null.</exception>
	/// <exception cref="ArgumentException">Thrown if byte array is not exactly 4 bytes.</exception>
	public Tag(byte[] value)
	{
		ArgumentNullException.ThrowIfNull(value);
		if (value.Length != 4)
			throw new ArgumentException("Byte array must be exactly 4 bytes.", nameof(value));

		Value = value[0] << 24 | value[1] << 16 | value[2] << 8 | value[3];
		Name = new string(
		[
			(char)value[0],
			(char)value[1],
			(char)value[2],
			(char)value[3]
		]);
	}

	/// <summary>
	/// Initializes a tag from a 32-bit integer value.
	/// </summary>
	/// <param name="value">The integer value representing the tag.</param>
	public Tag(int value)
	{
		Value = value;
		Name = new string(
		[
			(char)(0xFF & value >> 24),
			(char)(0xFF & value >> 16),
			(char)(0xFF & value >> 8),
			(char)(0xFF & value)
		]);
	}

	/// <summary>
	/// Initializes a tag from a <see cref="TableTypes.Tags"/> value.
	/// </summary>
	/// <param name="value">The integer value representing the tag.</param>
	public Tag(TableTypes.Tags value)
		: this((int)value) { }
	

	/// <inheritdoc/>
	public override string ToString() => $"({Name},{Value:X8})";

	/// <inheritdoc/>
	public bool Equals(Tag other) => Value == other.Value;

	/// <inheritdoc/>
	public bool Equals(string other) => Name == other;

	/// <inheritdoc/>
	public bool Equals(int other) => Value == other;
	public bool Equals(TableTypes.Tags other) => Value == (int)other;

	/// <inheritdoc/>
	public bool Equals(byte[] other) =>
		other is { Length: 4 } &&
		Name[0] == (char)other[0] &&
		Name[1] == (char)other[1] &&
		Name[2] == (char)other[2] &&
		Name[3] == (char)other[3];

	/// <inheritdoc/>
	public override bool Equals(object obj) =>
		obj is Tag tag && Equals(tag) ||
		obj is string str && Equals(str) ||
		obj is int i && Equals(i) ||
		obj is TableTypes.Tags t && Equals(t) ||
		obj is byte[] b && Equals(b);

	/// <inheritdoc/>
	public override int GetHashCode() => Value;

	// Implicit conversions
	public static implicit operator string(Tag tag) => tag.Name;
	public static implicit operator int(Tag tag) => tag.Value;
	public static implicit operator TableTypes.Tags(Tag tag) => (TableTypes.Tags)tag.Value;
	public static implicit operator byte[](Tag tag) =>
		[
			(byte)tag.Name[0],
			(byte)tag.Name[1],
			(byte)tag.Name[2],
			(byte)tag.Name[3]
		];
	public static implicit operator Tag(string name) => new (name);
	public static implicit operator Tag(int value) => new (value);
	public static implicit operator Tag(TableTypes.Tags value) => new (value);
	public static implicit operator Tag(byte[] value) => new (value);

	// Operators
	public static bool operator ==(Tag tag1, Tag tag2) => tag1.Equals(tag2);
	public static bool operator !=(Tag tag1, Tag tag2) => !tag1.Equals(tag2);
	public static bool operator ==(Tag tag1, string tag2) => tag1.Equals(tag2);
	public static bool operator !=(Tag tag1, string tag2) => !tag1.Equals(tag2);
	public static bool operator ==(Tag tag1, int tag2) => tag1.Equals(tag2);
	public static bool operator !=(Tag tag1, int tag2) => !tag1.Equals(tag2);
	public static bool operator ==(Tag tag1, TableTypes.Tags tag2) => tag1.Equals(tag2);
	public static bool operator !=(Tag tag1, TableTypes.Tags tag2) => !tag1.Equals(tag2);
	public static bool operator ==(Tag tag1, byte[] tag2) => tag1.Equals(tag2);
	public static bool operator !=(Tag tag1, byte[] tag2) => !tag1.Equals(tag2);
	public static bool operator ==(string tag1, Tag tag2) => tag2.Equals(tag1);
	public static bool operator !=(string tag1, Tag tag2) => !tag2.Equals(tag1);
	public static bool operator ==(int tag1, Tag tag2) => tag2.Equals(tag1);
	public static bool operator !=(int tag1, Tag tag2) => !tag2.Equals(tag1);
	public static bool operator ==(TableTypes.Tags tag1, Tag tag2) => tag2.Equals(tag1);
	public static bool operator !=(TableTypes.Tags tag1, Tag tag2) => !tag2.Equals(tag1);
	public static bool operator ==(byte[] tag1, Tag tag2) => tag2.Equals(tag1);
	public static bool operator !=(byte[] tag1, Tag tag2) => !tag2.Equals(tag1);
}

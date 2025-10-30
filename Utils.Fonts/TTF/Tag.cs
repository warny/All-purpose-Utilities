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
    /// Thrown if <paramref name="name"/> is longer than four characters or contains non-ASCII characters.
    /// </exception>
    public Tag(string name)
    {
        if (name is null)
            throw new ArgumentNullException(nameof(name));
        if (name.Length > 4)
            throw new ArgumentException("Name must be 4 characters or fewer.", nameof(name));

        // Validate that only ASCII characters are provided.
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

    /// <inheritdoc/>
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

    /// <summary>
    /// Converts a <see cref="Tag"/> instance to its textual representation.
    /// </summary>
    /// <param name="tag">The tag to convert.</param>
    public static implicit operator string(Tag tag) => tag.Name;

    /// <summary>
    /// Converts a <see cref="Tag"/> instance to its numeric representation.
    /// </summary>
    /// <param name="tag">The tag to convert.</param>
    public static implicit operator int(Tag tag) => tag.Value;

    /// <summary>
    /// Converts a <see cref="Tag"/> instance to a <see cref="TableTypes.Tags"/> enumeration value.
    /// </summary>
    /// <param name="tag">The tag to convert.</param>
    public static implicit operator TableTypes.Tags(Tag tag) => (TableTypes.Tags)tag.Value;

    /// <summary>
    /// Converts a <see cref="Tag"/> instance to its raw byte representation.
    /// </summary>
    /// <param name="tag">The tag to convert.</param>
    public static implicit operator byte[](Tag tag) =>
            [
                    (byte)tag.Name[0],
                        (byte)tag.Name[1],
                        (byte)tag.Name[2],
                        (byte)tag.Name[3]
            ];

    /// <summary>
    /// Creates a <see cref="Tag"/> from its string representation.
    /// </summary>
    /// <param name="name">The source tag characters.</param>
    public static implicit operator Tag(string name) => new(name);

    /// <summary>
    /// Creates a <see cref="Tag"/> from its integer value.
    /// </summary>
    /// <param name="value">The 32-bit representation of the tag.</param>
    public static implicit operator Tag(int value) => new(value);

    /// <summary>
    /// Creates a <see cref="Tag"/> from a <see cref="TableTypes.Tags"/> enumeration value.
    /// </summary>
    /// <param name="value">The predefined table identifier.</param>
    public static implicit operator Tag(TableTypes.Tags value) => new(value);

    /// <summary>
    /// Creates a <see cref="Tag"/> from a four-byte buffer.
    /// </summary>
    /// <param name="value">The raw bytes representing the tag.</param>
    public static implicit operator Tag(byte[] value) => new(value);

    /// <summary>
    /// Determines whether two <see cref="Tag"/> instances represent the same identifier.
    /// </summary>
    /// <param name="tag1">The first tag to compare.</param>
    /// <param name="tag2">The second tag to compare.</param>
    public static bool operator ==(Tag tag1, Tag tag2) => tag1.Equals(tag2);

    /// <summary>
    /// Determines whether two <see cref="Tag"/> instances represent different identifiers.
    /// </summary>
    /// <param name="tag1">The first tag to compare.</param>
    /// <param name="tag2">The second tag to compare.</param>
    public static bool operator !=(Tag tag1, Tag tag2) => !tag1.Equals(tag2);

    /// <summary>
    /// Determines whether the <see cref="Tag"/> equals the supplied string identifier.
    /// </summary>
    /// <param name="tag1">The tag to evaluate.</param>
    /// <param name="tag2">The string identifier to compare.</param>
    public static bool operator ==(Tag tag1, string tag2) => tag1.Equals(tag2);

    /// <summary>
    /// Determines whether the <see cref="Tag"/> does not equal the supplied string identifier.
    /// </summary>
    /// <param name="tag1">The tag to evaluate.</param>
    /// <param name="tag2">The string identifier to compare.</param>
    public static bool operator !=(Tag tag1, string tag2) => !tag1.Equals(tag2);

    /// <summary>
    /// Determines whether the <see cref="Tag"/> equals the supplied integer identifier.
    /// </summary>
    /// <param name="tag1">The tag to evaluate.</param>
    /// <param name="tag2">The integer identifier to compare.</param>
    public static bool operator ==(Tag tag1, int tag2) => tag1.Equals(tag2);

    /// <summary>
    /// Determines whether the <see cref="Tag"/> does not equal the supplied integer identifier.
    /// </summary>
    /// <param name="tag1">The tag to evaluate.</param>
    /// <param name="tag2">The integer identifier to compare.</param>
    public static bool operator !=(Tag tag1, int tag2) => !tag1.Equals(tag2);

    /// <summary>
    /// Determines whether the <see cref="Tag"/> equals the supplied <see cref="TableTypes.Tags"/> identifier.
    /// </summary>
    /// <param name="tag1">The tag to evaluate.</param>
    /// <param name="tag2">The table identifier to compare.</param>
    public static bool operator ==(Tag tag1, TableTypes.Tags tag2) => tag1.Equals(tag2);

    /// <summary>
    /// Determines whether the <see cref="Tag"/> does not equal the supplied <see cref="TableTypes.Tags"/> identifier.
    /// </summary>
    /// <param name="tag1">The tag to evaluate.</param>
    /// <param name="tag2">The table identifier to compare.</param>
    public static bool operator !=(Tag tag1, TableTypes.Tags tag2) => !tag1.Equals(tag2);

    /// <summary>
    /// Determines whether the <see cref="Tag"/> equals the supplied byte-buffer identifier.
    /// </summary>
    /// <param name="tag1">The tag to evaluate.</param>
    /// <param name="tag2">The byte buffer to compare.</param>
    public static bool operator ==(Tag tag1, byte[] tag2) => tag1.Equals(tag2);

    /// <summary>
    /// Determines whether the <see cref="Tag"/> does not equal the supplied byte-buffer identifier.
    /// </summary>
    /// <param name="tag1">The tag to evaluate.</param>
    /// <param name="tag2">The byte buffer to compare.</param>
    public static bool operator !=(Tag tag1, byte[] tag2) => !tag1.Equals(tag2);

    /// <summary>
    /// Determines whether the supplied string identifier equals the <see cref="Tag"/>.
    /// </summary>
    /// <param name="tag1">The string identifier to evaluate.</param>
    /// <param name="tag2">The tag to compare.</param>
    public static bool operator ==(string tag1, Tag tag2) => tag2.Equals(tag1);

    /// <summary>
    /// Determines whether the supplied string identifier differs from the <see cref="Tag"/>.
    /// </summary>
    /// <param name="tag1">The string identifier to evaluate.</param>
    /// <param name="tag2">The tag to compare.</param>
    public static bool operator !=(string tag1, Tag tag2) => !tag2.Equals(tag1);

    /// <summary>
    /// Determines whether the supplied integer identifier equals the <see cref="Tag"/>.
    /// </summary>
    /// <param name="tag1">The integer identifier to evaluate.</param>
    /// <param name="tag2">The tag to compare.</param>
    public static bool operator ==(int tag1, Tag tag2) => tag2.Equals(tag1);

    /// <summary>
    /// Determines whether the supplied integer identifier differs from the <see cref="Tag"/>.
    /// </summary>
    /// <param name="tag1">The integer identifier to evaluate.</param>
    /// <param name="tag2">The tag to compare.</param>
    public static bool operator !=(int tag1, Tag tag2) => !tag2.Equals(tag1);

    /// <summary>
    /// Determines whether the supplied <see cref="TableTypes.Tags"/> identifier equals the <see cref="Tag"/>.
    /// </summary>
    /// <param name="tag1">The table identifier to evaluate.</param>
    /// <param name="tag2">The tag to compare.</param>
    public static bool operator ==(TableTypes.Tags tag1, Tag tag2) => tag2.Equals(tag1);

    /// <summary>
    /// Determines whether the supplied <see cref="TableTypes.Tags"/> identifier differs from the <see cref="Tag"/>.
    /// </summary>
    /// <param name="tag1">The table identifier to evaluate.</param>
    /// <param name="tag2">The tag to compare.</param>
    public static bool operator !=(TableTypes.Tags tag1, Tag tag2) => !tag2.Equals(tag1);

    /// <summary>
    /// Determines whether the supplied byte-buffer identifier equals the <see cref="Tag"/>.
    /// </summary>
    /// <param name="tag1">The byte buffer to evaluate.</param>
    /// <param name="tag2">The tag to compare.</param>
    public static bool operator ==(byte[] tag1, Tag tag2) => tag2.Equals(tag1);

    /// <summary>
    /// Determines whether the supplied byte-buffer identifier differs from the <see cref="Tag"/>.
    /// </summary>
    /// <param name="tag1">The byte buffer to evaluate.</param>
    /// <param name="tag2">The tag to compare.</param>
    public static bool operator !=(byte[] tag1, Tag tag2) => !tag2.Equals(tag1);
}

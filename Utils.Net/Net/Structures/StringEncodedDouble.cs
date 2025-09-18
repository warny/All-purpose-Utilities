using System;
using System.Numerics;
using System.Text;

namespace Utils.Net.Structures;


/// <summary>
/// Represents a double precision value that can be encoded to and decoded from string or byte representations.
/// </summary>
public struct StringEncodedDouble :
        IComparable<StringEncodedDouble>, IEquatable<StringEncodedDouble>,
        IComparable<double>, IEquatable<double>,
        IComparisonOperators<StringEncodedDouble, StringEncodedDouble, bool>,
        IComparisonOperators<StringEncodedDouble, double, bool>,
        IComparable
{
        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncodedDouble"/> struct with the specified value.
        /// </summary>
        /// <param name="value">The floating-point value to wrap.</param>
        public StringEncodedDouble(double value)
        {
                Value = value;
        }

        /// <summary>
        /// Gets or sets the underlying floating-point value.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Creates an instance from a culture invariant string representation.
        /// </summary>
        /// <param name="str">The textual representation to parse.</param>
        /// <returns>A new <see cref="StringEncodedDouble"/> initialized from the parsed value.</returns>
        public static StringEncodedDouble FromString(string str) => new StringEncodedDouble(double.Parse(str, System.Globalization.NumberFormatInfo.InvariantInfo));
        /// <summary>
        /// Creates an instance from a UTF-8 byte sequence that stores a culture invariant string representation.
        /// </summary>
        /// <param name="bytes">The encoded byte buffer to convert.</param>
        /// <returns>A new <see cref="StringEncodedDouble"/> initialized from the decoded value.</returns>
        public static StringEncodedDouble FromBytes(byte[] bytes) => FromString(Encoding.UTF8.GetString(bytes));

        /// <summary>
        /// Returns the culture specific string representation of the wrapped value.
        /// </summary>
        /// <returns>The string representation of <see cref="Value"/>.</returns>
        public override readonly string ToString() => Value.ToString();
        /// <summary>
        /// Encodes the wrapped value as a UTF-8 byte array using invariant culture formatting.
        /// </summary>
        /// <returns>The UTF-8 encoded representation.</returns>
        public readonly byte[] ToBytes() => Encoding.UTF8.GetBytes(Value.ToString(System.Globalization.NumberFormatInfo.InvariantInfo));

        /// <inheritdoc />
        public readonly int CompareTo(StringEncodedDouble other) => Value.CompareTo(other.Value);
        /// <inheritdoc />
        public readonly int CompareTo(double other) => Value.CompareTo(other);
        /// <inheritdoc />
        public readonly int CompareTo(object obj) =>
                obj switch
		{
			StringEncodedDouble sed => CompareTo(sed),
			double d => CompareTo(d),
			_ => throw new NotSupportedException()
		};

        /// <inheritdoc />
        public readonly bool Equals(StringEncodedDouble other) => this.Value.Equals(other.Value);
        /// <inheritdoc />
        public readonly bool Equals(double other) => this.Value.Equals(other);
        /// <inheritdoc />
        public override readonly bool Equals(object obj) =>
                obj switch
		{
			StringEncodedDouble sed => Equals(sed),
			double d => Equals(d),
			_ => false
		};


        /// <inheritdoc />
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Converts the specified <see cref="StringEncodedDouble"/> to its underlying double value.
        /// </summary>
        /// <param name="obj">The wrapped value to convert.</param>
        public static implicit operator double(StringEncodedDouble obj) => obj.Value;
        /// <summary>
        /// Converts the specified <see cref="double"/> to a <see cref="StringEncodedDouble"/> instance.
        /// </summary>
        /// <param name="value">The floating-point value to wrap.</param>
        public static implicit operator StringEncodedDouble(double value) => new(value);

        /// <summary>
        /// Determines whether two <see cref="StringEncodedDouble"/> instances are equal.
        /// </summary>
        public static bool operator ==(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.Equals(sed2);
        /// <summary>
        /// Determines whether two <see cref="StringEncodedDouble"/> instances are not equal.
        /// </summary>
        public static bool operator !=(StringEncodedDouble sed1, StringEncodedDouble sed2) => !sed1.Equals(sed2);
        /// <summary>
        /// Determines whether the first operand is greater than the second operand.
        /// </summary>
        public static bool operator >(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.CompareTo(sed2) > 0;
        /// <summary>
        /// Determines whether the first operand is less than the second operand.
        /// </summary>
        public static bool operator <(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.CompareTo(sed2) < 0;
        /// <summary>
        /// Determines whether the first operand is greater than or equal to the second operand.
        /// </summary>
        public static bool operator >=(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.CompareTo(sed2) >= 0;
        /// <summary>
        /// Determines whether the first operand is less than or equal to the second operand.
        /// </summary>
        public static bool operator <=(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.CompareTo(sed2) <= 0;

        /// <summary>
        /// Determines whether the <see cref="StringEncodedDouble"/> value equals the specified <see cref="double"/> value.
        /// </summary>
        public static bool operator ==(StringEncodedDouble sed1, double d2) => sed1.Equals(d2);
        /// <summary>
        /// Determines whether the <see cref="StringEncodedDouble"/> value differs from the specified <see cref="double"/> value.
        /// </summary>
        public static bool operator !=(StringEncodedDouble sed1, double d2) => !sed1.Equals(d2);
        /// <summary>
        /// Determines whether the <see cref="StringEncodedDouble"/> value is greater than the specified <see cref="double"/> value.
        /// </summary>
        public static bool operator >(StringEncodedDouble sed1, double d2) => sed1.CompareTo(d2) > 0;
        /// <summary>
        /// Determines whether the <see cref="StringEncodedDouble"/> value is less than the specified <see cref="double"/> value.
        /// </summary>
        public static bool operator <(StringEncodedDouble sed1, double d2) => sed1.CompareTo(d2) < 0;
        /// <summary>
        /// Determines whether the <see cref="StringEncodedDouble"/> value is greater than or equal to the specified <see cref="double"/> value.
        /// </summary>
        public static bool operator >=(StringEncodedDouble sed1, double d2) => sed1.CompareTo(d2) >= 0;
        /// <summary>
        /// Determines whether the <see cref="StringEncodedDouble"/> value is less than or equal to the specified <see cref="double"/> value.
        /// </summary>
        public static bool operator <=(StringEncodedDouble sed1, double d2) => sed1.CompareTo(d2) <= 0;

        /// <summary>
        /// Determines whether the specified <see cref="double"/> value equals the <see cref="StringEncodedDouble"/> value.
        /// </summary>
        public static bool operator ==(double d1, StringEncodedDouble sed2) => sed2.Equals(d1);
        /// <summary>
        /// Determines whether the specified <see cref="double"/> value differs from the <see cref="StringEncodedDouble"/> value.
        /// </summary>
        public static bool operator !=(double d1, StringEncodedDouble sed2) => !sed2.Equals(d1);
        /// <summary>
        /// Determines whether the specified <see cref="double"/> value is greater than the <see cref="StringEncodedDouble"/> value.
        /// </summary>
        public static bool operator >(double d1, StringEncodedDouble sed2) => sed2.CompareTo(d1) < 0;
        /// <summary>
        /// Determines whether the specified <see cref="double"/> value is less than the <see cref="StringEncodedDouble"/> value.
        /// </summary>
        public static bool operator <(double d1, StringEncodedDouble sed2) => sed2.CompareTo(d1) > 0;
        /// <summary>
        /// Determines whether the specified <see cref="double"/> value is greater than or equal to the <see cref="StringEncodedDouble"/> value.
        /// </summary>
        public static bool operator >=(double d1, StringEncodedDouble sed2) => sed2.CompareTo(d1) <= 0;
        /// <summary>
        /// Determines whether the specified <see cref="double"/> value is less than or equal to the <see cref="StringEncodedDouble"/> value.
        /// </summary>
        public static bool operator <=(double d1, StringEncodedDouble sed2) => sed2.CompareTo(d1) >= 0;
}

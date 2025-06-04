﻿using System;
using System.Numerics;
using System.Text;

namespace Utils.Net.Structures;


public struct StringEncodedDouble :
	IComparable<StringEncodedDouble>, IEquatable<StringEncodedDouble>,
	IComparable<double>, IEquatable<double>,
	IComparisonOperators<StringEncodedDouble, StringEncodedDouble, bool>,
	IComparisonOperators<StringEncodedDouble, double, bool>,
	IComparable
{
	public StringEncodedDouble(double value)
	{
		Value = value;
	}

	public double Value { get; set; }

	public static StringEncodedDouble FromString(string str) => new StringEncodedDouble(double.Parse(str, System.Globalization.NumberFormatInfo.InvariantInfo));
	public static StringEncodedDouble FromBytes(byte[] bytes) => FromString(Encoding.UTF8.GetString(bytes));

	public override readonly string ToString() => Value.ToString();
	public readonly byte[] ToBytes() => Encoding.UTF8.GetBytes(Value.ToString(System.Globalization.NumberFormatInfo.InvariantInfo));

	public readonly int CompareTo(StringEncodedDouble other) => Value.CompareTo(other.Value);
	public readonly int CompareTo(double other) => Value.CompareTo(other);
	public readonly int CompareTo(object obj) =>
		obj switch
		{
			StringEncodedDouble sed => CompareTo(sed),
			double d => CompareTo(d),
			_ => throw new NotSupportedException()
		};

	public readonly bool Equals(StringEncodedDouble other) => this.Value.Equals(other.Value);
	public readonly bool Equals(double other) => this.Value.Equals(other);
	public override readonly bool Equals(object obj) =>
		obj switch
		{
			StringEncodedDouble sed => Equals(sed),
			double d => Equals(d),
			_ => false
		};


	public override int GetHashCode() => Value.GetHashCode();

	public static implicit operator double(StringEncodedDouble obj) => obj.Value;
	public static implicit operator StringEncodedDouble(double value) => new(value);

	public static bool operator ==(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.Equals(sed2);
	public static bool operator !=(StringEncodedDouble sed1, StringEncodedDouble sed2) => !sed1.Equals(sed2);
	public static bool operator >(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.CompareTo(sed2) > 0;
	public static bool operator <(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.CompareTo(sed2) < 0;
	public static bool operator >=(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.CompareTo(sed2) >= 0;
	public static bool operator <=(StringEncodedDouble sed1, StringEncodedDouble sed2) => sed1.CompareTo(sed2) <= 0;

	public static bool operator ==(StringEncodedDouble sed1, double d2) => sed1.Equals(d2);
	public static bool operator !=(StringEncodedDouble sed1, double d2) => !sed1.Equals(d2);
	public static bool operator >(StringEncodedDouble sed1, double d2) => sed1.CompareTo(d2) > 0;
	public static bool operator <(StringEncodedDouble sed1, double d2) => sed1.CompareTo(d2) < 0;
	public static bool operator >=(StringEncodedDouble sed1, double d2) => sed1.CompareTo(d2) >= 0;
	public static bool operator <=(StringEncodedDouble sed1, double d2) => sed1.CompareTo(d2) <= 0;

	public static bool operator ==(double d1, StringEncodedDouble sed2) => sed2.Equals(d1);
	public static bool operator !=(double d1, StringEncodedDouble sed2) => !sed2.Equals(d1);
	public static bool operator >(double d1, StringEncodedDouble sed2) => sed2.CompareTo(d1) < 0;
	public static bool operator <(double d1, StringEncodedDouble sed2) => sed2.CompareTo(d1) > 0;
	public static bool operator >=(double d1, StringEncodedDouble sed2) => sed2.CompareTo(d1) <= 0;
	public static bool operator <=(double d1, StringEncodedDouble sed2) => sed2.CompareTo(d1) >= 0;
}

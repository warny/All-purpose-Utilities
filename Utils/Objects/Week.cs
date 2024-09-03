using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

namespace Utils.Objects;

public struct Week : 
	IEquatable<Week>, IEqualityOperators<Week, Week, bool>, 
	IComparable<Week>, IComparisonOperators<Week>
{
	public int Year { get; }
	public int WeekNumber { get; }

	public Week(int year, int weekNumber)
	{
		Year = year.ArgMustBeGreaterThan(0);
		WeekNumber = weekNumber.ArgMustBeBetween(1, 53);
	}

	public Week(int weekNumber) : this(DateTime.Now.Year, weekNumber) { }

	public readonly void Deconstruct(out int year, out int weekNumber)
	{
		year = Year;
		weekNumber = WeekNumber;
	}

	public override int GetHashCode() => HashCode.Combine(Year, WeekNumber);

	public override bool Equals([NotNullWhen(true)] object obj)
		=> obj switch
		{
			Week w => Equals(w),
			_ => false
		};

	public bool Equals(Week other)
		=> this.Year == other.Year
		&& this.WeekNumber == other.WeekNumber;

	public int CompareTo(Week other) 
		=> new int[] {
			this.Year.CompareTo(other.Year),
			this.WeekNumber.CompareTo(WeekNumber)
		}.FirstOrDefault(t => t != 0, 0);

	public static bool operator ==(Week left, Week right) => left.Equals(right);
	public static bool operator !=(Week left, Week right) => !left.Equals(right);
	public static bool operator <(Week left, Week right) => left.CompareTo(right) < 0;
	public static bool operator <=(Week left, Week right) => left.CompareTo(right) <= 0;
	public static bool operator >(Week left, Week right) => left.CompareTo(right) > 0;
	public static bool operator >=(Week left, Week right) => left.CompareTo(right) >= 0;
}

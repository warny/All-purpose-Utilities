using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

namespace Utils.Objects;
/// <summary>
/// Represents a specific week in a given year.
/// </summary>
public struct Week :
	IEquatable<Week>,
	IEqualityOperators<Week, Week, bool>,
	IComparable<Week>,
	IComparisonOperators<Week, Week, bool>
{
	/// <summary>
	/// Gets the year associated with the week.
	/// </summary>
	public int Year { get; }

	/// <summary>
	/// Gets the number of the week in the year.
	/// </summary>
	public int WeekNumber { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="Week"/> struct with a specific year and week number.
	/// </summary>
	/// <param name="year">The year.</param>
	/// <param name="weekNumber">The week number (1-53).</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="year"/> is less than 1 or 
	/// <paramref name="weekNumber"/> is not between 1 and 53.
	/// </exception>
	public Week(int year, int weekNumber)
	{
		Year = year.ArgMustBeGreaterThan(0);
		WeekNumber = weekNumber.ArgMustBeBetween(1, 53);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Week"/> struct for the current year with a specific week number.
	/// </summary>
	/// <param name="weekNumber">The week number (1-53).</param>
	public Week(int weekNumber) : this(DateTime.Now.Year, weekNumber) { }

	/// <summary>
	/// Deconstructs the <see cref="Week"/> into its components.
	/// </summary>
	/// <param name="year">The year.</param>
	/// <param name="weekNumber">The week number.</param>
	public readonly void Deconstruct(out int year, out int weekNumber)
	{
		year = Year;
		weekNumber = WeekNumber;
	}

	/// <summary>
	/// Returns the hash code for this <see cref="Week"/>.
	/// </summary>
	/// <returns>A hash code for the current <see cref="Week"/>.</returns>
	public override int GetHashCode() => HashCode.Combine(Year, WeekNumber);

	/// <summary>
	/// Determines whether the specified object is equal to the current <see cref="Week"/>.
	/// </summary>
	/// <param name="obj">The object to compare with the current <see cref="Week"/>.</param>
	/// <returns>
	/// <see langword="true"/> if the specified object is equal to the current <see cref="Week"/>; otherwise, <see langword="false"/>.
	/// </returns>
	public override bool Equals([NotNullWhen(true)] object obj)
		=> obj is Week w && Equals(w);

	/// <summary>
	/// Indicates whether the current <see cref="Week"/> is equal to another <see cref="Week"/>.
	/// </summary>
	/// <param name="other">A <see cref="Week"/> to compare with this <see cref="Week"/>.</param>
	/// <returns>
	/// <see langword="true"/> if the current <see cref="Week"/> is equal to the <paramref name="other"/> parameter; otherwise, <see langword="false"/>.
	/// </returns>
	public bool Equals(Week other)
		=> Year == other.Year && WeekNumber == other.WeekNumber;

	/// <summary>
	/// Compares the current <see cref="Week"/> with another <see cref="Week"/>.
	/// </summary>
	/// <param name="other">A <see cref="Week"/> to compare with this <see cref="Week"/>.</param>
	/// <returns>
	/// A value that indicates the relative order of the objects being compared. 
	/// The return value has these meanings:
	/// - Less than zero: This instance precedes <paramref name="other"/> in the sort order.
	/// - Zero: This instance occurs in the same position in the sort order as <paramref name="other"/>.
	/// - Greater than zero: This instance follows <paramref name="other"/> in the sort order.
	/// </returns>
	public int CompareTo(Week other)
	{
		int yearComparison = Year.CompareTo(other.Year);
		if (yearComparison != 0)
			return yearComparison;

		return WeekNumber.CompareTo(other.WeekNumber);
	}

	/// <summary>
	/// Determines whether two specified <see cref="Week"/> objects are equal.
	/// </summary>
	/// <param name="left">The first <see cref="Week"/> to compare.</param>
	/// <param name="right">The second <see cref="Week"/> to compare.</param>
	/// <returns>
	/// <see langword="true"/> if the two <see cref="Week"/> objects are equal; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool operator ==(Week left, Week right) => left.Equals(right);

	/// <summary>
	/// Determines whether two specified <see cref="Week"/> objects are not equal.
	/// </summary>
	/// <param name="left">The first <see cref="Week"/> to compare.</param>
	/// <param name="right">The second <see cref="Week"/> to compare.</param>
	/// <returns>
	/// <see langword="true"/> if the two <see cref="Week"/> objects are not equal; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool operator !=(Week left, Week right) => !left.Equals(right);

	/// <summary>
	/// Determines whether one specified <see cref="Week"/> is less than another specified <see cref="Week"/>.
	/// </summary>
	/// <param name="left">The first <see cref="Week"/> to compare.</param>
	/// <param name="right">The second <see cref="Week"/> to compare.</param>
	/// <returns>
	/// <see langword="true"/> if the first <see cref="Week"/> is less than the second <see cref="Week"/>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool operator <(Week left, Week right) => left.CompareTo(right) < 0;

	/// <summary>
	/// Determines whether one specified <see cref="Week"/> is less than or equal to another specified <see cref="Week"/>.
	/// </summary>
	/// <param name="left">The first <see cref="Week"/> to compare.</param>
	/// <param name="right">The second <see cref="Week"/> to compare.</param>
	/// <returns>
	/// <see langword="true"/> if the first <see cref="Week"/> is less than or equal to the second <see cref="Week"/>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool operator <=(Week left, Week right) => left.CompareTo(right) <= 0;

	/// <summary>
	/// Determines whether one specified <see cref="Week"/> is greater than another specified <see cref="Week"/>.
	/// </summary>
	/// <param name="left">The first <see cref="Week"/> to compare.</param>
	/// <param name="right">The second <see cref="Week"/> to compare.</param>
	/// <returns>
	/// <see langword="true"/> if the first <see cref="Week"/> is greater than the second <see cref="Week"/>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool operator >(Week left, Week right) => left.CompareTo(right) > 0;

	/// <summary>
	/// Determines whether one specified <see cref="Week"/> is greater than or equal to another specified <see cref="Week"/>.
	/// </summary>
	/// <param name="left">The first <see cref="Week"/> to compare.</param>
	/// <param name="right">The second <see cref="Week"/> to compare.</param>
	/// <returns>
	/// <see langword="true"/> if the first <see cref="Week"/> is greater than or equal to the second <see cref="Week"/>; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool operator >=(Week left, Week right) => left.CompareTo(right) >= 0;
}

using System.Numerics;

namespace Utils.Mathematics;

public static class NullableIntEx
{
	#region Utilities for Endpoints

	/// <summary>
	/// Minimum of two endpoints, taking null as -∞ if <paramref name="isMin"/> is true 
	/// </summary>
	public static T? Min<T>(T? a, T? b, bool isMin)
		where T : struct, IBinaryInteger<T>
	{
		// If isMin => null => -∞ => that is definitely "less" than any finite number 
		// so min(-∞, x) => -∞.
		// If both are null => -∞
		if (!a.HasValue && !b.HasValue) return null; // -∞ or +∞ ??? 
		if (!a.HasValue) return a; // -∞
		if (!b.HasValue) return b; // -∞
		return T.Min(a.Value, b.Value);
	}

	/// <summary>
	/// Maximum of two endpoints, taking null as +∞ if <paramref name="isMax"/> is true
	/// </summary>
	public static T? Max<T>(T? a, T? b, bool isMax)
		where T : struct, IBinaryInteger<T>
	{
		// If isMax => null => +∞ => that is definitely "greater" than any finite number 
		// so max(+∞, x) => +∞.
		// If both are null => +∞
		if (!a.HasValue && !b.HasValue) return null;
		if (!a.HasValue) return a; // +∞
		if (!b.HasValue) return b; // +∞
		return T.Max(a.Value, b.Value);
	}

	/// <summary>
	/// Compares a &lt;= b. Null is interpreted as +∞ here for convenience.
	/// If a is +∞ =&gt; then a &lt;= b is false unless b is also +∞.
	/// </summary>
	public static bool LessOrEqual<T>(this T? a, T? b)
		where T : struct, IBinaryInteger<T>, IComparable<T>
	{
		// +∞ <= +∞ => true
		if (!a.HasValue && !b.HasValue) return true;
		if (!a.HasValue) return false;  // +∞ <= finite => false
		if (!b.HasValue) return true;   // finite <= +∞ => true
		return a.Value.CompareTo(b.Value) <= 0;
	}

	/// <summary>
	/// Compares a &lt; b. Uses LessOrEqual but excludes equality.
	/// </summary>
	public static bool Less<T>(this T? a, T? b)
		where T : struct, IBinaryInteger<T>, IComparable<T>
	{
		// a < b => (a <= b) && (a != b)
		if (a == b) return false;
		return LessOrEqual(a, b);
	}

	/// <summary>
	/// Compares a &gt;= b. Null =&gt; +∞ is always &gt;= any finite.
	/// </summary>
	public static bool GreaterOrEqual<T>(T? a, T? b)
		where T : struct, IBinaryInteger<T>, IComparable<T>
	{
		// a >= b => b <= a
		return LessOrEqual(b, a);
	}

	/// <summary>
	/// Decrements an endpoint if finite. 
	/// If already null =&gt; -∞ or +∞? We have to interpret usage.
	/// 
	/// This method is used for subtracting an interval: we want (r2.Min - 1) etc.
	/// But if r2.Min = null =&gt; that's -∞ =&gt; (r2.Min - 1) =&gt; -∞ =&gt; no difference.
	/// </summary>
	public static T? Decrement<T>(this T? x)
		where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
	{
		if (!x.HasValue)
		{
			// If x is -∞ => can't go "lower" => remains -∞
			// If x is +∞ => doesn't make sense in a min context
			// We'll just keep it null => means "∞" but in context it will be -∞ or +∞.
			return x;
		}
		if (x == T.MinValue)
		{
			// int.MinValue - 1 => -∞ effectively
			return null;
		}
		return x.Value - T.One;
	}

	/// <summary>
	/// Increments an endpoint if finite. 
	/// If x is +∞ =&gt; stays +∞,
	/// if x is -∞ =&gt; stays -∞.
	/// </summary>
	public static T? Increment<T>(this T? x)
		where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
	{
		if (!x.HasValue)
		{
			// +∞ => remains +∞
			return x;
		}
		if (x == T.MaxValue)
		{
			// int.MaxValue + 1 => +∞
			return null;
		}
		return x.Value + T.One;
	}

	/// <summary>
	/// A specialized compare for intersection checks
	/// (start &lt;= end). 
	/// If isMinForComparison =&gt; treat null as -∞ for 'start' 
	/// else treat null as +∞ for 'end'. 
	/// 
	/// This is an attempt to unify a compare approach, but keep in mind 
	/// logic can get subtle with infinite endpoints.
	/// </summary>
	public static int Compare<T>(T? left, T? right, bool isMinForComparison)
		where T : struct, IBinaryInteger<T>, IComparable<T>
	{
		// If both are null => they represent the same ∞ => 0
		if (!left.HasValue && !right.HasValue) return 0;

		// If left is null => either -∞ or +∞
		if (!left.HasValue)
		{
			// If isMinForComparison => left = -∞ => definitely less than or equal to any right
			// => negative
			// If not => left = +∞ => definitely greater than any finite => positive
			return isMinForComparison ? -1 : 1;
		}
		// If right is null => similarly
		if (!right.HasValue)
		{
			return isMinForComparison ? 1 : -1;
		}

		// Both finite
		return left.Value.CompareTo(right.Value);
	}

	#endregion

	public static T? Parse<T>(string token)
		where T : struct, IBinaryInteger<T>, IComparable<T>, IParsable<T>
		=> Parse<T>(token, System.Globalization.CultureInfo.CurrentCulture);

	public static T? Parse<T>(string token, IFormatProvider formatProvider)
		where T : struct, IBinaryInteger<T>, IComparable<T>, IParsable<T>
	{
		// If "∞" => null => means -∞ if isStart, +∞ if not isStart
		// but let's keep it simpler: by convention, 
		// "∞" always => null, 
		// we'll interpret it in the range logic as -∞ or +∞ as needed 
		// based on whether it's Minimum or Maximum. 
		// The 'SimpleRange' logic handles that in comparisons.  
		// 
		// Alternatively, you could parse "∞" => null for maximum, 
		// and "∞" => some special negative? 
		// But let's keep it uniform: "∞" => null, 
		// the usage context is in a SimpleRange constructor 
		// which sets Minimum or Maximum. 
		// If Minimum = null => that indicates -∞, 
		// if Maximum = null => that indicates +∞.

		if (token == "∞" || token == "inf")
			return null; // interpret as infinity

		// else parse as int
		return T.Parse(token, formatProvider);
	}

}

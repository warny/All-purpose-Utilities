using System.Numerics;

namespace Utils.Mathematics;

/// <summary>
/// Provides helper methods for working with nullable numeric endpoints that can represent
/// open ranges by treating <see langword="null"/> as positive or negative infinity as needed.
/// </summary>
public static class NullableIntEx
{
    #region Utilities for Endpoints

    /// <summary>
    /// Returns the minimum of two start endpoints where <see langword="null"/> represents −∞.
    /// If either argument is <see langword="null"/>, the result is <see langword="null"/> (−∞ wins).
    /// </summary>
    public static T? MinStartpoint<T>(T? a, T? b)
        where T : struct, IBinaryInteger<T>
    {
        if (!a.HasValue || !b.HasValue) return null;
        return T.Min(a.Value, b.Value);
    }

    /// <summary>
    /// Returns the minimum of two end endpoints where <see langword="null"/> represents +∞.
    /// If only one argument is <see langword="null"/>, the finite value wins.
    /// </summary>
    public static T? MinEndpoint<T>(T? a, T? b)
        where T : struct, IBinaryInteger<T>
    {
        if (!a.HasValue && !b.HasValue) return null;
        if (!a.HasValue) return b;
        if (!b.HasValue) return a;
        return T.Min(a.Value, b.Value);
    }

    /// <summary>
    /// Returns the maximum of two start endpoints where <see langword="null"/> represents −∞.
    /// If only one argument is <see langword="null"/>, the finite value wins.
    /// </summary>
    public static T? MaxStartpoint<T>(T? a, T? b)
        where T : struct, IBinaryInteger<T>
    {
        if (!a.HasValue && !b.HasValue) return null;
        if (!a.HasValue) return b;
        if (!b.HasValue) return a;
        return T.Max(a.Value, b.Value);
    }

    /// <summary>
    /// Returns the maximum of two end endpoints where <see langword="null"/> represents +∞.
    /// If either argument is <see langword="null"/>, the result is <see langword="null"/> (+∞ wins).
    /// </summary>
    public static T? MaxEndpoint<T>(T? a, T? b)
        where T : struct, IBinaryInteger<T>
    {
        if (!a.HasValue || !b.HasValue) return null;
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
    public static bool GreaterOrEqual<T>(this T? a, T? b)
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
    /// Compares two startpoint values where <see langword="null"/> represents −∞.
    /// Returns a negative value if <paramref name="left"/> &lt; <paramref name="right"/>,
    /// zero if equal, and a positive value if <paramref name="left"/> &gt; <paramref name="right"/>.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="left">The left startpoint; <see langword="null"/> means −∞.</param>
    /// <param name="right">The right startpoint; <see langword="null"/> means −∞.</param>
    public static int CompareStartpoint<T>(T? left, T? right)
        where T : struct, IBinaryInteger<T>, IComparable<T>
    {
        if (!left.HasValue && !right.HasValue) return 0;
        if (!left.HasValue) return -1;  // −∞ < any finite
        if (!right.HasValue) return 1;  // any finite > −∞
        return left.Value.CompareTo(right.Value);
    }

    /// <summary>
    /// Compares two endpoint values where <see langword="null"/> represents +∞.
    /// Returns a negative value if <paramref name="left"/> &lt; <paramref name="right"/>,
    /// zero if equal, and a positive value if <paramref name="left"/> &gt; <paramref name="right"/>.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="left">The left endpoint; <see langword="null"/> means +∞.</param>
    /// <param name="right">The right endpoint; <see langword="null"/> means +∞.</param>
    public static int CompareEndpoint<T>(T? left, T? right)
        where T : struct, IBinaryInteger<T>, IComparable<T>
    {
        if (!left.HasValue && !right.HasValue) return 0;
        if (!left.HasValue) return 1;   // +∞ > any finite
        if (!right.HasValue) return -1; // any finite < +∞
        return left.Value.CompareTo(right.Value);
    }

    #endregion

    /// <summary>
    /// Parses a textual token into a nullable value using the current culture, interpreting
    /// the infinity symbols "∞" and "inf" as <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The numeric type to parse.</typeparam>
    /// <param name="token">The token that represents the number.</param>
    /// <returns>The parsed number or <see langword="null"/> for infinity.</returns>
    public static T? Parse<T>(string token)
        where T : struct, IBinaryInteger<T>, IComparable<T>, IParsable<T>
        => Parse<T>(token, System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Parses a textual token into a nullable value using the specified <see cref="IFormatProvider"/>,
    /// interpreting the infinity symbols "∞" and "inf" as <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The numeric type to parse.</typeparam>
    /// <param name="token">The token that represents the number.</param>
    /// <param name="formatProvider">The culture-specific formatting information to use.</param>
    /// <returns>The parsed number or <see langword="null"/> for infinity.</returns>
    public static T? Parse<T>(string token, IFormatProvider formatProvider)
        where T : struct, IBinaryInteger<T>, IComparable<T>, IParsable<T>
    {
        if (token == "∞" || token == "inf")
            return null;

        return T.Parse(token, formatProvider);
    }
}

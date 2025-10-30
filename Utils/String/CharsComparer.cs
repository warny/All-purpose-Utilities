using System.Runtime.InteropServices;

namespace Utils.String;

/// <summary>
/// Specialized comparer for sequences of <see cref="char"/> (e.g., strings, char arrays, lists).
/// Ensures efficient comparisons between strings, char arrays, spans, and other character sequences,
/// with support for culture-specific or case-insensitive comparisons.
/// </summary>
public sealed class CharsComparer : IComparer<IEnumerable<char>>
{
    private readonly StringComparer stringComparer;

    /// <summary>
    /// A thread-safe, cached instance using <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    public static CharsComparer Ordinal { get; } = new CharsComparer(StringComparer.Ordinal);

    /// <summary>
    /// A thread-safe, cached instance using <see cref="StringComparer.CurrentCulture"/> for culture-aware comparisons.
    /// </summary>
    public static CharsComparer CurrentCulture { get; } = new CharsComparer(StringComparer.CurrentCulture);

    /// <summary>
    /// A thread-safe, cached instance using <see cref="StringComparer.OrdinalIgnoreCase"/> for case-insensitive comparisons.
    /// </summary>
    public static CharsComparer OrdinalIgnoreCase { get; } = new CharsComparer(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="CharsComparer"/> class using a custom string comparer.
    /// </summary>
    /// <param name="stringComparer">The comparer to use for string-based comparisons.</param>
    public CharsComparer(StringComparer stringComparer)
    {
        this.stringComparer = stringComparer ?? throw new ArgumentNullException(nameof(stringComparer));
    }

    /// <inheritdoc/>
    public int Compare(IEnumerable<char> x, IEnumerable<char> y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null && y is null) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        // Special case: string comparison with respect to localization
        if (x is string strX && y is string strY)
            return stringComparer.Compare(strX, strY);

        // Try to get spans for both
        if (GetSpan(x, out var spanX) && GetSpan(y, out var spanY))
            return CompareSpans(spanX, spanY);

        return CompareEnumerables(x, y);
    }

    /// <summary>
    /// Compares two character spans.
    /// </summary>
    private static int CompareSpans(ReadOnlySpan<char> spanX, ReadOnlySpan<char> spanY)
    {
        var minLength = Math.Min(spanX.Length, spanY.Length);

        for (var i = 0; i < minLength; i++)
        {
            var comparison = spanX[i].CompareTo(spanY[i]);
            if (comparison != 0) return comparison;
        }

        return spanX.Length.CompareTo(spanY.Length);
    }

    /// <summary>
    /// Compares two character enumerables by iterating through them.
    /// </summary>
    private static int CompareEnumerables(IEnumerable<char> x, IEnumerable<char> y)
    {
        using var enumX = x.GetEnumerator();
        using var enumY = y.GetEnumerator();

        while (true)
        {
            var hasNextX = enumX.MoveNext();
            var hasNextY = enumY.MoveNext();

            if (!hasNextX && !hasNextY) return 0; // Both sequences finished
            if (!hasNextX) return -1; // `x` is shorter
            if (!hasNextY) return 1; // `y` is shorter

            var comparison = enumX.Current.CompareTo(enumY.Current);
            if (comparison != 0) return comparison;
        }
    }

    /// <summary>
    /// Tries to retrieve a <see cref="ReadOnlySpan{T}"/> from an enumerable of characters.
    /// Returns <see langword="true"/> if successful, along with the extracted span.
    /// Otherwise, returns <see langword="false"/>.
    /// </summary>
    private static bool GetSpan(IEnumerable<char> obj, out ReadOnlySpan<char> span)
    {
        switch (obj)
        {
            case string s:
                span = s.AsSpan();
                return true;
            case char[] array:
                span = array;
                return true;
            case List<char> list:
                span = CollectionsMarshal.AsSpan(list);
                return true;
            default:
                span = default;
                return false;
        }
    }
}

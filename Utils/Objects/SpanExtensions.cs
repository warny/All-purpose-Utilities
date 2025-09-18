
namespace Utils.Objects;

/// <summary>
/// Provides helper methods for trimming and slicing <see cref="ReadOnlySpan{T}"/> instances.
/// </summary>
public static class SpanExtensions
{
    /// <summary>
    /// Removes elements from the beginning and end of <paramref name="s"/> that match the result of the specified function.
    /// </summary>
    /// <param name="s">Reference span</param>
    /// <param name="trimTester">Test function (returns <see langword="true"/> to remove the element)</param>
    /// <returns>Span with removed elements</returns>
    public static ReadOnlySpan<T> Trim<T>(this ReadOnlySpan<T> s, Func<T, bool> trimTester)
    {
        return s.TrimStart(trimTester).TrimEnd(trimTester);
    }

    /// <summary>
    /// Removes elements from the beginning of <paramref name="s"/> that match the result of the specified function.
    /// </summary>
    /// <param name="s">Reference span</param>
    /// <param name="trimTester">Test function (returns <see langword="true"/> to remove the element)</param>
    /// <returns>Span with removed elements</returns>
    public static ReadOnlySpan<T> TrimStart<T>(this ReadOnlySpan<T> s, Func<T, bool> trimTester)
    {
        int start, end = s.Length;
        for (start = 0; start < end; start++)
        {
            if (!trimTester(s[start])) break;
        }
        if (start >= end) return ReadOnlySpan<T>.Empty;
        return s[start..end];
    }

    /// <summary>
    /// Removes elements from the end of <paramref name="s"/> that match the result of the specified function.
    /// </summary>
    /// <param name="s">Reference span</param>
    /// <param name="trimTester">Test function (returns <see langword="true"/> to remove the element)</param>
    /// <returns>Span with removed elements</returns>
    public static ReadOnlySpan<T> TrimEnd<T>(this ReadOnlySpan<T> s, Func<T, bool> trimTester)
    {
        int start = 0, end;
        for (end = s.Length - 1; end > start; end--)
        {
            if (!trimTester(s[end])) break;
        }
        if (start >= end) return ReadOnlySpan<T>.Empty;
        return s.Slice(start, end - start + 1);
    }

    /// <summary>
    /// Retrieves a sub-<see cref="ReadOnlySpan{T}"/> from this instance. The sub-<see cref="ReadOnlySpan{T}"/> starts at a specified character position and has a defined length.
    /// </summary>
    /// <param name="s">The string from which to extract the sub-string</param>
    /// <param name="start">The zero-based character position at which the sub-string starts</param>
    /// <param name="length">The number of characters in the sub-string</param>
    /// <returns>
    /// A <see cref="ReadOnlySpan{T}"/> equivalent to the sub-<see cref="ReadOnlySpan{T}"/> of length <paramref name="length"/> that begins
    /// at <paramref name="start"/> in this instance, or <see cref="ReadOnlySpan{T}"/>.Empty if <paramref name="start"/> is
    /// equal to the length of this instance and <paramref name="length"/> is zero.
    /// </returns>
    /// <example>
    /// <code>
    /// // Extract a sub-span of length 5 starting from index 2.
    /// ReadOnlySpan&lt;char&gt; originalSpan = "Hello, World!".AsSpan();
    /// ReadOnlySpan&lt;char&gt; subSpan = originalSpan.Mid(2, 5);
    /// // subSpan contains "llo, "
    /// 
    /// // Extract a sub-span of length 5 starting from the end.
    /// ReadOnlySpan&lt;char&gt; endSpan = originalSpan.Mid(-5, 5);
    /// // endSpan contains "World"
    /// </code>
    /// </example>
    public static ReadOnlySpan<T> Mid<T>(this ReadOnlySpan<T> s, int start, int length)
    {
        if (length < 0)
        {
            if (start > 0 && -length > start)
            {
                length = start + 1;
                start = 0;
            }
            else
            {
                start += length + 1;
                length = -length;
            }
        }
        if (start < 0) start = s.Length + start;
        if (start <= -length) return [];
        if (start < 0) return s[..(length + start)];
        if (start > s.Length) return [];
        if (start + length > s.Length) return s[start..];
        return s.Slice(start, length);
    }

    /// <summary>
    /// Retrieves a sub-<see cref="ReadOnlySpan{T}"/> from this instance. The sub-<see cref="ReadOnlySpan{T}"/> starts at a specified character position.
    /// </summary>
    /// <param name="s">The string from which to extract the sub-string</param>
    /// <param name="start">The zero-based character position at which the sub-string starts</param>
    /// <returns>
    /// A <see cref="ReadOnlySpan{T}"/> equivalent to the sub-<see cref="ReadOnlySpan{T}"/> starting from the index <paramref name="start"/>
    /// </returns>
    /// <example>
    /// <code>
    /// // Extract a sub-span starting from index 7 to the end.
    /// ReadOnlySpan&lt;char&gt; originalSpan = "Hello, World!".AsSpan();
    /// ReadOnlySpan&lt;char&gt; subSpan = originalSpan.Mid(7);
    /// // subSpan contains "World!"
    /// 
    /// // Extract a sub-span starting from the end.
    /// ReadOnlySpan&lt;char&gt; endSpan = originalSpan.Mid(-6);
    /// // endSpan contains "World!"
    /// </code>
    /// </example>
    public static ReadOnlySpan<T> Mid<T>(this ReadOnlySpan<T> s, int start)
        => s.Mid(start, s.Length);
}

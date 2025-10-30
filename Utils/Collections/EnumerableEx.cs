using System.Runtime.CompilerServices;
using Utils.Objects;

namespace Utils.Collections;

/// <summary>
/// Extension methods for working with <see cref="IEnumerable{T}"/> and related collections.
/// </summary>
public static partial class EnumerableEx
{
    /// <summary>
    /// Enumerates a sliding window of size <paramref name="windowSize"/> over
    /// the source <see cref="IEnumerable{T}"/>. Each yielded array shares
    /// elements with the previous one in a sliding fashion.
    /// </summary>
    /// <typeparam name="T">The element type of the source sequence.</typeparam>
    /// <param name="source">The sequence to read.</param>
    /// <param name="windowSize">The size of each sliding window.</param>
    /// <param name="skipCount">The number of elements to skip from the start before beginning.</param>
    /// <returns>
    /// An <see cref="T:IEnumerable{Array{T}}"/> where each array represents a window
    /// of size <paramref name="windowSize"/>, except possibly the last one if there
    /// are not enough remaining elements.
    /// </returns>
    public static IEnumerable<T[]> SlideEnumerateBy<T>(
        this IEnumerable<T> source,
        int windowSize,
        int skipCount = 0)
    {
        ArgumentNullException.ThrowIfNull(source);

        T[] window = new T[windowSize];
        using var enumerator = source.GetEnumerator();

        // Skip the required elements
        for (int i = 0; i < skipCount; i++)
        {
            if (!enumerator.MoveNext())
                yield break;
        }

        // Fill the first window (or partial if not enough elements)
        for (int i = 0; i < windowSize; i++)
        {
            if (!enumerator.MoveNext())
            {
                // Return a partial window if we don't fill it completely
                var lastPartial = new T[i];
                Array.Copy(window, lastPartial, i);
                yield return lastPartial;
                yield break;
            }
            window[i] = enumerator.Current;
        }

        // We keep two windows to minimize allocations:
        // one for the current yield, one for shifting
        T[] buffer = new T[windowSize];

        while (true)
        {
            // Yield the current full window
            yield return window;

            // Attempt to move next; if no more elements, stop
            if (!enumerator.MoveNext())
                yield break;

            // Slide the window: shift [1..windowSize-1] -> [0..windowSize-2]
            // and place the new element at the end
            Array.Copy(window, 1, buffer, 0, windowSize - 1);
            (window, buffer) = (buffer, window);
            window[windowSize - 1] = enumerator.Current;
        }
    }

    /// <summary>
    /// Indicates whether the <paramref name="enumerable"/> contains more than one element.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="enumerable">The sequence to check.</param>
    /// <returns>
    /// <see langword="true"/> if the sequence has more than one element; otherwise <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasManyElements<T>(this IEnumerable<T> enumerable)
        => HasAtLeastElements(enumerable, 2);

    /// <summary>
    /// Indicates whether the <paramref name="enumerable"/> has at least 
    /// <paramref name="count"/> elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="enumerable">The sequence to check.</param>
    /// <param name="count">The minimum number of elements required.</param>
    /// <returns>
    /// <see langword="true"/> if the sequence has at least <paramref name="count"/> elements; 
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="enumerable"/> is <see langword="null"/>.</exception>
    public static bool HasAtLeastElements<T>(this IEnumerable<T> enumerable, int count)
    {
        ArgumentNullException.ThrowIfNull(enumerable);

        // Fast checks for ICollection<T> or IReadOnlyCollection<T>
        if (enumerable is ICollection<T> collection)
        {
            return collection.Count >= count;
        }
        else if (enumerable is IReadOnlyCollection<T> readOnlyCollection)
        {
            return readOnlyCollection.Count >= count;
        }

        // Fallback: manual iteration
        using var enumerator = enumerable.GetEnumerator();
        for (int i = 0; i < count; i++)
        {
            if (!enumerator.MoveNext())
                return false;
        }
        return true;
    }

    /// <summary>
    /// Enumerates all elements in <paramref name="enumerable"/> followed by 
    /// additional <paramref name="elements"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequences.</typeparam>
    /// <param name="enumerable">The original sequence.</param>
    /// <param name="elements">The elements to append.</param>
    /// <returns>
    /// A sequence that first yields every element of <paramref name="enumerable"/>, 
    /// and then yields every element of <paramref name="elements"/>.
    /// </returns>
    public static IEnumerable<T> FollowedBy<T>(
        this IEnumerable<T> enumerable,
        params T[] elements)
    {
        ArgumentNullException.ThrowIfNull(enumerable);

        foreach (var item in enumerable)
        {
            yield return item;
        }
        foreach (var item in elements)
        {
            yield return item;
        }
    }

    /// <summary>
    /// Enumerates the specified <paramref name="elements"/> first,
    /// then enumerates all elements in <paramref name="enumerable"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequences.</typeparam>
    /// <param name="enumerable">The original sequence.</param>
    /// <param name="elements">The elements to prepend.</param>
    /// <returns>
    /// A sequence that first yields every element of <paramref name="elements"/>,
    /// and then yields every element of <paramref name="enumerable"/>.
    /// </returns>
    public static IEnumerable<T> PrecededBy<T>(
        this IEnumerable<T> enumerable,
        params T[] elements)
    {
        ArgumentNullException.ThrowIfNull(enumerable);

        foreach (var item in elements)
        {
            yield return item;
        }
        foreach (var item in enumerable)
        {
            yield return item;
        }
    }

    /// <summary>
    /// Packs consecutive identical elements from <paramref name="source"/> into 
    /// groups, yielding a <see cref="Pack{T}"/> for each group containing the 
    /// value and its repetition count.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="source">The sequence of elements to pack.</param>
    /// <returns>
    /// A sequence of <see cref="Pack{T}"/> structures, each containing the 
    /// element value and how many times it was repeated consecutively.
    /// </returns>
    /// <remarks>
    /// Uses <see cref="EqualityComparer{T}.Default"/> for equality checks.
    /// </remarks>
    public static IEnumerable<Pack<T>> Pack<T>(this IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var e = source.GetEnumerator();
        if (!e.MoveNext())
            yield break;

        var comparer = EqualityComparer<T>.Default;
        var lastValue = e.Current;
        var repetition = 1;

        while (e.MoveNext())
        {
            if (!comparer.Equals(e.Current, lastValue))
            {
                yield return new Pack<T>(lastValue, repetition);
                repetition = 1;
                lastValue = e.Current;
            }
            else
            {
                repetition++;
            }
        }

        yield return new Pack<T>(lastValue, repetition);
    }

    /// <summary>
    /// Unpacks a sequence of <see cref="Pack{T}"/> into a flat sequence, 
    /// repeating each value according to its repetition count.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the packed sequence.</typeparam>
    /// <param name="source">The packed sequence to unpack.</param>
    /// <returns>
    /// A flat <see cref="IEnumerable{T}"/> restoring the original sequence before packing.
    /// </returns>
    public static IEnumerable<T> Unpack<T>(this IEnumerable<Pack<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var item in source)
        {
            for (int i = 0; i < item.Repetition; i++)
            {
                yield return item.Value;
            }
        }
    }

    /// <summary>
    /// Slices the <paramref name="source"/> into multiple segments at the 
    /// specified <paramref name="cutIndexes"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="source">The <see cref="IEnumerable{T}"/> to slice.</param>
    /// <param name="cutIndexes">The indexes at which the sequence is sliced. They should be sorted in ascending order.</param>
    /// <returns>
    /// A sequence of segments (each itself an <see cref="IEnumerable{T}"/>),
    /// split at each index in <paramref name="cutIndexes"/>.
    /// </returns>
    /// <remarks>
    /// If <paramref name="cutIndexes"/> is empty, then the entire sequence 
    /// is returned as a single segment.
    /// </remarks>
    public static IEnumerable<IEnumerable<T>> Slice<T>(
        this IEnumerable<T> source,
        params int[] cutIndexes)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<T> result = [];
        int index = 0;
        var indexesQueue = new Queue<int>(cutIndexes);

        // If there are no cut indexes, just yield the entire sequence in one shot
        if (indexesQueue.Count == 0)
        {
            yield return source;
            yield break;
        }

        int nextCut = indexesQueue.Dequeue();
        using var enumerator = source.GetEnumerator();

        // Iterate through the source
        bool hasMore = enumerator.MoveNext();
        for (; hasMore; hasMore = enumerator.MoveNext())
        {
            if (index == nextCut)
            {
                yield return result.ToArray();
                result.Clear();

                if (indexesQueue.Count == 0)
                {
                    // No more cuts, the rest belongs to the final segment
                    break;
                }
                nextCut = indexesQueue.Dequeue();
            }

            result.Add(enumerator.Current);
            index++;
        }

        // If we still have the enumerator not exhausted in the loop
        // but ended because we've run out of cuts
        if (hasMore)
        {
            do
            {
                result.Add(enumerator.Current);
            }
            while (enumerator.MoveNext());
        }

        // Yield whatever remains
        yield return result.ToArray();
    }

    /// <summary>
    /// Flattens a sequence of sequences into a single, concatenated sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sub-sequences.</typeparam>
    /// <param name="sources">An <see cref="T:IEnumerable{IEnumerable{T}}"/> containing sub-sequences to flatten.</param>
    /// <returns>
    /// A single <see cref="IEnumerable{T}"/> containing all elements from the sub-sequences in order.
    /// </returns>
    public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        foreach (var sequence in sources)
        {
            foreach (var item in sequence)
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Scans the <paramref name="source"/> for occurrences of the subsequence <paramref name="toReplace"/>,
    /// and replaces them with the subsequence <paramref name="replacement"/>. 
    /// A naive, non-overlapping approach is used.
    /// </summary>
    /// <typeparam name="T">
    /// The element type; must implement <see cref="IEquatable{T}"/> for equality checks.
    /// </typeparam>
    /// <param name="source">The source sequence in which to perform replacements.</param>
    /// <param name="toReplace">The subsequence to find and replace.</param>
    /// <param name="replacement">The subsequence to substitute in place of <paramref name="toReplace"/>.</param>
    /// <returns>A new sequence with the replacements applied where the pattern is matched.</returns>
    /// <remarks>
    /// <para>
    /// Overlapping matches are not recognized. For example, if <c>toReplace</c> is
    /// <c>[1, 2, 1]</c>, and the source is <c>[1, 2, 1, 2, 1]</c>, this method
    /// only replaces the first match, ignoring the second (overlapping) match.
    /// </para>
    /// <para>
    /// If <paramref name="toReplace"/> is empty, no replacement is done and the 
    /// original <paramref name="source"/> is returned as-is.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if any of the parameters (<paramref name="source"/>, <paramref name="toReplace"/>, or <paramref name="replacement"/>)
    /// is <see langword="null"/>.
    /// </exception>
    public static IEnumerable<T> Replace<T>(
        this IEnumerable<T> source,
        T[] toReplace,
        T[] replacement)
        where T : IEquatable<T>
    {
        source.Arg().MustNotBeNull();
        toReplace.Arg().MustNotBeNull();
        replacement.Arg().MustNotBeNull();

        // If the pattern to replace is empty, 
        // the standard assumption is "do nothing" and just return the source.
        if (toReplace.Length == 0)
        {
            foreach (var item in source)
                yield return item;
            yield break;
        }

        using var enumerator = source.GetEnumerator();

        // How many items from 'toReplace' have been matched so far.
        int matchedCount = 0;

        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;

            // If the current item matches the next element in toReplace...
            if (current.Equals(toReplace[matchedCount]))
            {
                matchedCount++;

                // If we've matched the entire 'toReplace' sequence...
                if (matchedCount == toReplace.Length)
                {
                    // Output the replacement
                    foreach (var rep in replacement)
                    {
                        yield return rep;
                    }

                    // Reset matched count for the next potential match
                    matchedCount = 0;
                }
            }
            else
            {
                // We have a mismatch after partially matching some elements.
                // We must yield the partially matched items (since they no longer form a complete match).
                if (matchedCount > 0)
                {
                    for (int i = 0; i < matchedCount; i++)
                    {
                        yield return toReplace[i];
                    }
                    matchedCount = 0;
                }

                // Also yield the current item because it failed to match 
                // the next pattern character.
                yield return current;
            }
        }

        // If we reached the end but still have a partial match leftover,
        // yield those partially matched items as well.
        if (matchedCount > 0)
        {
            for (int i = 0; i < matchedCount; i++)
            {
                yield return toReplace[i];
            }
        }
    }
}


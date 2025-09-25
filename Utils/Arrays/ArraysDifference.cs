using System.Collections;
using System.Collections.Immutable;

namespace Utils.Arrays;

/// <summary>
/// Compares two arrays and returns their differences.
/// </summary>
public class ArraysDifference<T> : IReadOnlyList<ArraysChange<T>>
{
    private readonly IReadOnlyList<ArraysChange<T>> changes;

    /// <summary>
    /// Gets the number of changes detected between the compared sequences.
    /// </summary>
    public int Count => changes.Count;

    /// <summary>
    /// Creates a new <see cref="ArraysDifference{T}"/> instance by comparing two read-only spans using the default equality comparison.
    /// </summary>
    /// <typeparam name="ET">The element type contained in the spans.</typeparam>
    /// <param name="old">The original sequence.</param>
    /// <param name="new">The updated sequence.</param>
    /// <returns>An <see cref="ArraysDifference{T}"/> describing the changes between the two sequences.</returns>
    public static ArraysDifference<ET> GetDifferences<ET>(ReadOnlySpan<ET> old, ReadOnlySpan<ET> @new)
        where ET : IEquatable<ET>
        => new(old, @new, (o, n) => o.Equals(n));

        /// <summary>
        /// Creates a new <see cref="ArraysDifference{T}"/> instance by comparing two read-only spans with a specific equality comparer.
        /// </summary>
        /// <typeparam name="ET">The element type contained in the spans.</typeparam>
        /// <param name="old">The original sequence.</param>
        /// <param name="new">The updated sequence.</param>
        /// <param name="comparer">The equality comparer used to determine whether elements are identical.</param>
        /// <returns>An <see cref="ArraysDifference{T}"/> describing the changes between the two sequences.</returns>
        public static ArraysDifference<ET> GetDifferences<ET>(ReadOnlySpan<ET> old, ReadOnlySpan<ET> @new, IEqualityComparer<ET> comparer)
        => new (old, @new, comparer.Equals);

        /// <summary>
        /// Creates a new <see cref="ArraysDifference{T}"/> instance by comparing two read-only spans with a specific ordering comparer.
        /// </summary>
        /// <typeparam name="ET">The element type contained in the spans.</typeparam>
        /// <param name="old">The original sequence.</param>
        /// <param name="new">The updated sequence.</param>
        /// <param name="comparer">The comparer used to determine whether two elements should be considered equivalent.</param>
        /// <returns>An <see cref="ArraysDifference{T}"/> describing the changes between the two sequences.</returns>
        public static ArraysDifference<ET> GetDifferences<ET>(ReadOnlySpan<ET> old, ReadOnlySpan<ET> @new, IComparer<ET> comparer)
                => new (old, @new, (o, n) => comparer.Compare(o, n) == 0);

	/// <summary>
	/// Compares two strings and returns the modifications needed to transform the first string into the second.
	/// </summary>
	/// <param name="old">String before modifications</param>
	/// <param name="new">String after modifications</param>
    /// <param name="comparer">Function that compares 2 array elements</param>
	private ArraysDifference(ReadOnlySpan<T> old, ReadOnlySpan<T> @new, Func<T, T, bool> comparer)
    {
        changes = Compare(old, @new, 0, 0, comparer);
    }

	private static IReadOnlyList<ArraysChange<T>> Compare(ReadOnlySpan<T> old, ReadOnlySpan<T> @new, int lengthStart, int lengthEnd, Func<T, T, bool> comparer)
	{
		// Utilisation de Span<int> pour éviter l'allocation excessive de mémoire
		Span<int> lengths = stackalloc int[(old.Length + 1) * (@new.Length + 1)];
		int width = @new.Length + 1;

		for (int i = lengthStart; i < old.Length - lengthEnd; i++)
		{
			for (int j = lengthStart; j < @new.Length - lengthEnd; j++)
			{
				if (comparer(old[i], @new[j]))
					lengths[(i + 1) * width + (j + 1)] = lengths[i * width + j] + 1;
				else
					lengths[(i + 1) * width + (j + 1)] = Math.Max(lengths[(i + 1) * width + j], lengths[i * width + (j + 1)]);
			}
		}

		List<ArraysChange<T>> changes = [];
		StringComparisonStatus currentStatus = StringComparisonStatus.Unchanged;
		int previousStatePosition = old.Length - lengthEnd;

		int oldPosition = old.Length - lengthEnd;
		int newPosition = @new.Length - lengthEnd;

		while (oldPosition > lengthStart && newPosition > lengthStart)
		{
			if (lengths[oldPosition * width + newPosition] == lengths[(oldPosition - 1) * width + newPosition])
			{
				if (currentStatus != StringComparisonStatus.Removed)
				{
					AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);
					previousStatePosition = oldPosition;
					currentStatus = StringComparisonStatus.Removed;
				}
				oldPosition--;
			}
			else if (lengths[oldPosition * width + newPosition] == lengths[oldPosition * width + (newPosition - 1)])
			{
				if (currentStatus != StringComparisonStatus.Added)
				{
					AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);
					previousStatePosition = newPosition;
					currentStatus = StringComparisonStatus.Added;
				}
				newPosition--;
			}
			else
			{
				if (!comparer(old[oldPosition - 1], @new[newPosition - 1]))
				{
					throw new InvalidOperationException($"Inconsistency detected in {nameof(Compare)} method.");
				}

				if (currentStatus != StringComparisonStatus.Unchanged)
				{
					AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);
					previousStatePosition = oldPosition;
					currentStatus = StringComparisonStatus.Unchanged;
				}

				oldPosition--;
				newPosition--;
			}
		}

		AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);

		if (newPosition > lengthStart)
		{
			AddChange(changes, old, @new, StringComparisonStatus.Added, newPosition, oldPosition, lengthStart);
		}

		if (oldPosition > lengthStart)
		{
			AddChange(changes, old, @new, StringComparisonStatus.Removed, oldPosition, lengthStart, oldPosition);
		}

		changes.Reverse();
		return [.. changes];
	}

	private static void AddChange(List<ArraysChange<T>> changes, ReadOnlySpan<T> old, ReadOnlySpan<T> @new, StringComparisonStatus currentStatus, int previousStatePosition, int oldPosition, int newPosition)
    {
        var change = currentStatus == StringComparisonStatus.Added ? @new[newPosition .. previousStatePosition] : old[oldPosition .. previousStatePosition];
        if (change.Length > 0)
        {
            changes.Add(new ArraysChange<T>(currentStatus, change));
        }
    }

    /// <summary>
    /// Gets the change at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the change to retrieve.</param>
    public ArraysChange<T> this[int index] => changes[index];

        /// <inheritdoc />
        public IEnumerator<ArraysChange<T>> GetEnumerator() => changes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => changes.GetEnumerator();
}

/// <summary>
/// Indicates the type of change between two arrays.
/// </summary>
public enum StringComparisonStatus
{
    /// <summary>
    /// Indicates that an element was removed from the original sequence.
    /// </summary>
    Removed = -1,

    /// <summary>
    /// Indicates that an element is present in both sequences without modification.
    /// </summary>
    Unchanged = 0,

    /// <summary>
    /// Indicates that an element was added to the updated sequence.
    /// </summary>
    Added = 1
}

/// <summary>
/// Represents a single change between two arrays.
/// </summary>
public class ArraysChange<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArraysChange{T}"/> class.
    /// </summary>
    internal ArraysChange(StringComparisonStatus status, ReadOnlySpan<T> value)
    {
        this.Status = status;
        this.Value = value.ToImmutableArray();
    }

        /// <summary>
        /// Gets the status of this change.
        /// </summary>
        public StringComparisonStatus Status { get; }

    /// <summary>
    /// Gets the values involved in the change.
    /// </summary>
    public IReadOnlyList<T> Value { get; }
}
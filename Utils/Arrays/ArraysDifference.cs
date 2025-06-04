using System.Collections;
using System.Collections.Immutable;

namespace Utils.Arrays;

/// <summary>
/// Compares two arrays and returns their differences.
/// </summary>
public class ArraysDifference<T> : IReadOnlyList<ArraysChange<T>>
{
    private readonly IReadOnlyList<ArraysChange<T>> changes;

    public int Count => changes.Count;

    public static ArraysDifference<ET> GetDifferences<ET>(ReadOnlySpan<ET> old, ReadOnlySpan<ET> @new)
        where ET : IEquatable<ET>
        => new(old, @new, (o, n) => o.Equals(n));

	public static ArraysDifference<ET> GetDifferences<ET>(ReadOnlySpan<ET> old, ReadOnlySpan<ET> @new, IEqualityComparer<ET> comparer)
    	=> new (old, @new, comparer.Equals);

	public static ArraysDifference<ET> GetDifferences<ET>(ReadOnlySpan<ET> old, ReadOnlySpan<ET> @new, IComparer<ET> comparer)
		=> new (old, @new, (o, n) => comparer.Compare(o, n) == 0);

	/// <summary>
	/// Compares two strings and returns the modifications needed to transform the first string into the second.
	/// </summary>
	/// <param name="old">String before modifications</param>
	/// <param name="new">String after modifications</param>
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

    public ArraysChange<T> this[int index] => changes[index];

	public IEnumerator<ArraysChange<T>> GetEnumerator() => changes.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => changes.GetEnumerator();
}

public enum StringComparisonStatus
{
    Removed = -1,
    Unchanged = 0,
    Added = 1
}

public class ArraysChange<T>
{
    internal ArraysChange(StringComparisonStatus status, ReadOnlySpan<T> value)
    {
        this.Status = status;
        this.Value = value.ToImmutableArray();
    }

	public StringComparisonStatus Status { get; }
    public IReadOnlyList<T> Value { get; }
}
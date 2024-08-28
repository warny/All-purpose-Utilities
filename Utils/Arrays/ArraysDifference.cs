using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Arrays;

/// <summary>
/// Compares two strings and returns their differences.
/// </summary>
public class ArraysDifference<T> : IReadOnlyList<ArraysChange<T>>
{
    private readonly IReadOnlyList<ArraysChange<T>> changes;

    public int Count => changes.Count;

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
        List<ArraysChange<T>> changes = new List<ArraysChange<T>>();

        int[,] lengths = new int[old.Length + 1, @new.Length + 1];

        // Row 0 and column 0 are initialized to 0 already

        for (int i = lengthStart; i < old.Length - lengthEnd; i++)
            for (int j = lengthStart; j < @new.Length - lengthEnd; j++)
                if (comparer(old[i], @new[j]))
                    lengths[i + 1, j + 1] = lengths[i, j] + 1;
                else
                    lengths[i + 1, j + 1] = Math.Max(lengths[i + 1, j], lengths[i, j + 1]);

        // Read the substring out from the matrix
        StringComparisonStatus currentStatus = StringComparisonStatus.Unchanged;
        int previousStatePosition = old.Length - lengthEnd;
        int oldPosition, newPosition;
        for (oldPosition = old.Length - lengthEnd, newPosition = @new.Length - lengthEnd;
               oldPosition > lengthStart && newPosition != lengthStart;)
        {
            if (lengths[oldPosition, newPosition] == lengths[oldPosition - 1, newPosition])
            {
                if (currentStatus != StringComparisonStatus.Removed)
                {
                    AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);
                    previousStatePosition = oldPosition;
                    currentStatus = StringComparisonStatus.Removed;
                }
                oldPosition--;
            }
            else if (lengths[oldPosition, newPosition] == lengths[oldPosition, newPosition - 1])
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
                    throw new Exception($"Oops... something went wrong in the {nameof(Compare)} method!");
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

        if (newPosition > 0)
        {
            AddChange(changes, old, @new, StringComparisonStatus.Added, newPosition, oldPosition, 0);
        }
        if (oldPosition > 0)
        {
            AddChange(changes, old, @new, StringComparisonStatus.Removed, oldPosition, 0, oldPosition);
        }

        changes.Reverse();
        return changes.ToImmutableArray();
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
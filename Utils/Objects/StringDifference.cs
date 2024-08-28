using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Objects;

/// <summary>
/// Compares two strings and returns their differences.
/// </summary>
public class StringDifference : IReadOnlyList<StringChange>
{
    private readonly IReadOnlyList<StringChange> changes;

    public int Count => changes.Count;

    /// <summary>
    /// Compares two strings and returns the modifications needed to transform the first string into the second.
    /// </summary>
    /// <param name="old">String before modifications</param>
    /// <param name="new">String after modifications</param>
    public StringDifference(ReadOnlySpan<char> old, ReadOnlySpan<char> @new)
    {
        changes = Compare(old, @new, 0, 0);
    }

	private static IReadOnlyList<StringChange> Compare(ReadOnlySpan<char> old, ReadOnlySpan<char> @new, int lengthStart, int lengthEnd)
	{
		Span<int> lengths = stackalloc int[(old.Length + 1) * (@new.Length + 1)];
		int width = @new.Length + 1;

		for (int i = lengthStart; i < old.Length - lengthEnd; i++)
		{
			for (int j = lengthStart; j < @new.Length - lengthEnd; j++)
			{
				if (old[i] == @new[j])
					lengths[(i + 1) * width + (j + 1)] = lengths[i * width + j] + 1;
				else
					lengths[(i + 1) * width + (j + 1)] = Math.Max(lengths[(i + 1) * width + j], lengths[i * width + (j + 1)]);
			}
		}

		List<StringChange> changes = new List<StringChange>();
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
				if (old[oldPosition - 1] != @new[newPosition - 1])
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
		return changes.ToImmutableArray();
	}

	private static void AddChange(List<StringChange> changes, ReadOnlySpan<char> old, ReadOnlySpan<char> @new, StringComparisonStatus currentStatus, int previousStatePosition, int oldPosition, int newPosition)
    {
        var change = currentStatus == StringComparisonStatus.Added ? @new[newPosition .. previousStatePosition] : old[oldPosition .. previousStatePosition];
        if (change.Length > 0)
        {
            changes.Add(new StringChange(currentStatus, change.ToString()));
        }
    }

    public StringChange this[int index] => changes[index];

    public IEnumerator<StringChange> GetEnumerator()
    {
        return ((IEnumerable<StringChange>)changes).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return changes.GetEnumerator();
    }
}

public enum StringComparisonStatus
{
    Removed = -1,
    Unchanged = 0,
    Added = 1
}

public class StringChange
{
    internal StringChange(StringComparisonStatus status, string @string)
    {
        this.Status = status;
        this.String = @string;
    }

    public StringComparisonStatus Status { get; }
    public string String { get; }
}
using System.Collections;
using System.Collections.Immutable;

namespace Utils.Objects;

/// <summary>
/// Compares two strings and returns their differences as a list of changes.
/// </summary>
public class StringDifference : IReadOnlyList<StringChange>
{
	/// <summary>
	/// Holds the changes detected between the two strings.
	/// </summary>
	private readonly IReadOnlyList<StringChange> changes;

	/// <summary>
	/// Number of changes.
	/// </summary>
	public int Count => changes.Count;

	/// <summary>
	/// Initializes a new instance of the StringDifference class by comparing two strings.
	/// </summary>
	/// <param name="old">The original string before modifications.</param>
	/// <param name="new">The modified string after changes.</param>
	public StringDifference(ReadOnlySpan<char> old, ReadOnlySpan<char> @new)
	{
		// Compares the old and new strings and stores the resulting changes.
		changes = Compare(old, @new, 0, 0);
	}

	/// <summary>
	/// Compares two strings and identifies the changes required to transform the first string into the second.
	/// </summary>
	/// <param name="old">The original string.</param>
	/// <param name="new">The modified string.</param>
	/// <param name="lengthStart">The start index for the comparison.</param>
	/// <param name="lengthEnd">The end index for the comparison.</param>
	/// <returns>A read-only list of StringChange objects representing the differences.</returns>
	private static IReadOnlyList<StringChange> Compare(ReadOnlySpan<char> old, ReadOnlySpan<char> @new, int lengthStart, int lengthEnd)
	{
		// Allocate a span for storing lengths to avoid excessive memory allocations.
		Span<int> lengths = stackalloc int[(old.Length + 1) * (@new.Length + 1)];
		int width = @new.Length + 1;

		// Fill the lengths array using dynamic programming to find the longest common subsequence.
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

		// Trace back through the lengths array to determine the differences.
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

		// Add the final changes after exiting the loop.
		AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);

		if (newPosition > lengthStart)
		{
			AddChange(changes, old, @new, StringComparisonStatus.Added, newPosition, oldPosition, lengthStart);
		}

		if (oldPosition > lengthStart)
		{
			AddChange(changes, old, @new, StringComparisonStatus.Removed, oldPosition, lengthStart, oldPosition);
		}

		// Reverse the changes to present them in the correct order.
		changes.Reverse();
		return [.. changes];
	}

	/// <summary>
	/// Adds a change to the list of changes.
	/// </summary>
	/// <param name="changes">The list of changes to add to.</param>
	/// <param name="old">The original string.</param>
	/// <param name="new">The modified string.</param>
	/// <param name="currentStatus">The type of change (Added, Removed, Unchanged).</param>
	/// <param name="previousStatePosition">The position of the previous state.</param>
	/// <param name="oldPosition">The current position in the old string.</param>
	/// <param name="newPosition">The current position in the new string.</param>
	private static void AddChange(List<StringChange> changes, ReadOnlySpan<char> old, ReadOnlySpan<char> @new, StringComparisonStatus currentStatus, int previousStatePosition, int oldPosition, int newPosition)
	{
		var change = currentStatus == StringComparisonStatus.Added ? @new[newPosition..previousStatePosition] : old[oldPosition..previousStatePosition];
		if (change.Length > 0)
		{
			changes.Add(new StringChange(currentStatus, change.ToString()));
		}
	}

	// Indexer to access a specific change by its index.
	public StringChange this[int index] => changes[index];

	/// <summary>
	/// Enumerator for iterating through the list of changes.
	/// </summary>
	public IEnumerator<StringChange> GetEnumerator() => changes.GetEnumerator();

	/// <summary>
	/// Non-generic enumerator for compatibility with non-generic collections.
	/// </summary>
	IEnumerator IEnumerable.GetEnumerator() => changes.GetEnumerator();
}

/// <summary>
/// Indicates the status of a comparison between two strings (Added, Removed, Unchanged).
/// </summary>
public enum StringComparisonStatus
{
	Removed = -1,
	Unchanged = 0,
	Added = 1
}

/// <summary>
/// Represents a change between two strings.
/// </summary>
public class StringChange
{
	// Constructor to initialize a StringChange instance with a status and the relevant string.
	internal StringChange(StringComparisonStatus status, string @string)
	{
		this.Status = status;
		this.String = @string;
	}

	/// <summary>
	/// The status of the change (Added, Removed, Unchanged).
	/// </summary>
	public StringComparisonStatus Status { get; }

	/// <summary>
	/// The string that was added, removed, or unchanged. 
	/// </summary>
	public string String { get; }
}

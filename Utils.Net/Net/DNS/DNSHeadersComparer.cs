using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Utils.Net.DNS;

/// <summary>
/// Provides an <see cref="IEqualityComparer{T}"/> implementation for <see cref="DNSHeader"/> objects.
/// </summary>
/// <remarks>
/// This comparer evaluates equality based on:
/// <list type="bullet">
/// <item>The fields within the <see cref="DNSHeader"/> itself, using <see cref="DNSElementsComparer"/> for comparison.</item>
/// <item>All associated request and response record lists, including authorities and additional records.</item>
/// </list>
/// If two headers have the same fields, same set of DNS request records, and matching DNS response records
/// (including RData), they are considered equal.
/// </remarks>
public class DNSHeadersComparer : IEqualityComparer<DNSHeader>
{
	/// <summary>
	/// The default elements comparer used to evaluate equality at the <see cref="DNSElement"/> level.
	/// </summary>
	private readonly DNSElementsComparer comparer = DNSElementsComparer.Default;

	/// <summary>
	/// Initializes a new instance of the <see cref="DNSHeadersComparer"/> class.
	/// This constructor is private to enforce use of <see cref="Default"/>.
	/// </summary>
	private DNSHeadersComparer()
	{
	}

	/// <summary>
	/// Gets the default <see cref="DNSHeadersComparer"/> instance.
	/// </summary>
	public static DNSHeadersComparer Default { get; } = new DNSHeadersComparer();

	/// <inheritdoc />
	/// <summary>
	/// Determines whether the specified <see cref="DNSHeader"/> instances are considered equal.
	/// </summary>
	/// <param name="x">The first <see cref="DNSHeader"/> to compare.</param>
	/// <param name="y">The second <see cref="DNSHeader"/> to compare.</param>
	/// <returns>
	/// <see langword="true"/> if both <see cref="DNSHeader"/> instances and all their contained records are equal;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public bool Equals([AllowNull] DNSHeader x, [AllowNull] DNSHeader y)
	{
		// If the header objects themselves are not equal, no need to check further.
		if (!comparer.Equals(x, y))
			return false;

		// Compare each corresponding DNS request record.
		for (int i = 0; i < x.Requests.Count; i++)
		{
			if (!comparer.Equals(x.Requests[i], y.Requests[i]))
				return false;
		}

		// Compare responses, authorities, and additional records.
		if (!AreEquals(x.Responses, y.Responses)) return false;
		if (!AreEquals(x.Authorities, y.Authorities)) return false;
		if (!AreEquals(x.Additionals, y.Additionals)) return false;

		return true;
	}

	/// <summary>
	/// Compares two lists of <see cref="DNSResponseRecord"/> objects for equality,
	/// including their <see cref="DNSResponseRecord.RData"/> content.
	/// </summary>
	/// <param name="record1">The first list of <see cref="DNSResponseRecord"/> to compare.</param>
	/// <param name="record2">The second list of <see cref="DNSResponseRecord"/> to compare.</param>
	/// <returns>
	/// <see langword="true"/> if both lists have the same number of items and each corresponding record
	/// is equal (as determined by <see cref="DNSElementsComparer"/>); otherwise, <see langword="false"/>.
	/// </returns>
	private bool AreEquals(IList<DNSResponseRecord> record1, IList<DNSResponseRecord> record2)
	{
		if (record1.Count != record2.Count)
			return false;

		for (int i = 0; i < record1.Count; i++)
		{
			if (!comparer.Equals(record1[i], record2[i]))
				return false;

			// Also compare the resource data (RData) specifically for additional equality checks.
			if (!comparer.Equals(record1[i].RData, record2[i].RData))
				return false;
		}

		return true;
	}

	/// <inheritdoc />
	/// <summary>
	/// Returns a hash code for the specified <see cref="DNSHeader"/>, combining the hash codes
	/// of the header itself with those of its contained records.
	/// </summary>
	/// <param name="obj">The <see cref="DNSHeader"/> for which to generate a hash code.</param>
	/// <returns>A hash code for the specified <see cref="DNSHeader"/>.</returns>
	public int GetHashCode([DisallowNull] DNSHeader obj)
	{
		int hashCode = 0;

		unchecked
		{
			// Start with the DNSHeader-level hash code.
			hashCode = comparer.GetHashCode(obj);

			// Incorporate the hash codes of the request records.
			for (int i = 0; i < obj.Requests.Count; i++)
			{
				hashCode = hashCode * 27 + comparer.GetHashCode(obj.Requests[i]);
			}

			// Incorporate the hash codes of the response, authority, and additional records.
			hashCode = GetHashCode(hashCode, obj.Responses);
			hashCode = GetHashCode(hashCode, obj.Authorities);
			hashCode = GetHashCode(hashCode, obj.Additionals);
		}

		return hashCode;
	}

	/// <summary>
	/// Combines an existing hash code with those of a set of <see cref="DNSResponseRecord"/> objects
	/// and their associated RData.
	/// </summary>
	/// <param name="hashCode">The current running hash code value.</param>
	/// <param name="record">A list of <see cref="DNSResponseRecord"/> objects to incorporate.</param>
	/// <returns>A new hash code incorporating each record and its <see cref="DNSResponseRecord.RData"/>.</returns>
	private int GetHashCode(int hashCode, IList<DNSResponseRecord> record)
	{
		unchecked
		{
			for (int i = 0; i < record.Count; i++)
			{
				hashCode = hashCode * 27 + comparer.GetHashCode(record[i]);
				hashCode = hashCode * 27 + comparer.GetHashCode(record[i].RData);
			}
		}

		return hashCode;
	}
}

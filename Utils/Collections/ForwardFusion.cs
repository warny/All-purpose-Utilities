using System;
using System.Collections;
using System.Collections.Generic;

namespace Utils.Collections;

/// <summary>
/// Merges two sorted <see cref="IEnumerable{T}"/> collections based on the specified join type. 
/// The left list is considered the primary list in a left join, and vice versa for a right join.
/// </summary>
/// <typeparam name="T1">The type of elements in the left list.</typeparam>
/// <typeparam name="T2">The type of elements in the right list.</typeparam>
public class ForwardFusion<T1, T2> : IEnumerable<(T1 Left, T2 Right)>, IDisposable
{
	private readonly IEnumerable<T1> leftList;
	private readonly IEnumerable<T2> rightList;
	private readonly Func<T1, T2, int> compare;
	private readonly JoinType joinType;

	/// <summary>
	/// Initializes a new instance of the <see cref="ForwardFusion{T1, T2}"/> class.
	/// This constructor automatically determines the comparison logic based on the types of T1 and T2.
	/// </summary>
	/// <param name="leftList">The left list, which is considered the primary list.</param>
	/// <param name="rightList">The right list.</param>
	/// <param name="joinType">The type of join to perform.</param>
	public ForwardFusion(IEnumerable<T1> leftList, IEnumerable<T2> rightList, JoinType joinType = JoinType.InnerJoin)
	{
		this.leftList = leftList ?? throw new ArgumentNullException(nameof(leftList));
		this.rightList = rightList ?? throw new ArgumentNullException(nameof(rightList));
		this.joinType = joinType;

		// Determine the comparison logic based on the types of T1 and T2
		if (typeof(IComparable<T2>).IsAssignableFrom(typeof(T1)))
		{
			compare = (t1, t2) => ((IComparable<T2>)t1).CompareTo(t2);
		}
		else if (typeof(IComparable<T1>).IsAssignableFrom(typeof(T2)))
		{
			compare = (t1, t2) => -((IComparable<T1>)t2).CompareTo(t1);
		}
		else if (typeof(IComparable).IsAssignableFrom(typeof(T1)))
		{
			compare = (t1, t2) => ((IComparable)t1).CompareTo(t2);
		}
		else if (typeof(IComparable).IsAssignableFrom(typeof(T2)))
		{
			compare = (t1, t2) => -((IComparable)t2).CompareTo(t1);
		}
		else
		{
			throw new ArgumentException($"The types {typeof(T1).Name} and {typeof(T2).Name} cannot be compared.");
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ForwardFusion{T1, T2}"/> class
	/// using a custom comparer to compare elements in the lists.
	/// </summary>
	/// <param name="leftList">The left list, which is considered the primary list.</param>
	/// <param name="rightList">The right list.</param>
	/// <param name="comparer">The comparer used to compare elements in the lists.</param>
	/// <param name="joinType">The type of join to perform.</param>
	public ForwardFusion(IEnumerable<T1> leftList, IEnumerable<T2> rightList, IComparer comparer, JoinType joinType = JoinType.InnerJoin)
	{
		this.leftList = leftList ?? throw new ArgumentNullException(nameof(leftList));
		this.rightList = rightList ?? throw new ArgumentNullException(nameof(rightList));
		this.compare = (t1, t2) => comparer.Compare(t1, t2);
		this.joinType = joinType;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ForwardFusion{T1, T2}"/> class
	/// using a custom comparison function.
	/// </summary>
	/// <param name="leftList">The left list, which is considered the primary list.</param>
	/// <param name="rightList">The right list.</param>
	/// <param name="compare">The comparison function used to compare elements in the lists.</param>
	/// <param name="joinType">The type of join to perform.</param>
	public ForwardFusion(IEnumerable<T1> leftList, IEnumerable<T2> rightList, Func<T1, T2, int> compare, JoinType joinType = JoinType.InnerJoin)
	{
		this.leftList = leftList ?? throw new ArgumentNullException(nameof(leftList));
		this.rightList = rightList ?? throw new ArgumentNullException(nameof(rightList));
		this.compare = compare ?? throw new ArgumentNullException(nameof(compare));
		this.joinType = joinType;
	}

	/// <summary>
	/// Merges the two lists by iterating through both and comparing elements according to the specified join type.
	/// </summary>
	/// <returns>An enumerator that merges elements from the left and right lists.</returns>
	private IEnumerable<(T1 Left, T2 Right)> Enumerate()
	{
		using var leftEnum = leftList.GetEnumerator();
		using var rightEnum = rightList.GetEnumerator();

		bool hasLeft = leftEnum.MoveNext();
		bool hasRight = rightEnum.MoveNext();

		while (hasLeft || hasRight)
		{
			if (hasLeft && hasRight)
			{
				// Compare the current elements from both lists
				switch (compare(leftEnum.Current, rightEnum.Current))
				{
					case 0:
						yield return (leftEnum.Current, rightEnum.Current); // Emit matched pair
						hasRight = rightEnum.MoveNext();
						break;
					case -1:
						if (joinType.HasFlag(JoinType.LeftJoin))
						{
							yield return (leftEnum.Current, default); // Emit left element with no match
						}
						hasLeft = leftEnum.MoveNext();
						break;
					case 1:
						if (joinType.HasFlag(JoinType.RightJoin))
						{
							yield return (default, rightEnum.Current); // Emit right element with no match
						}
						hasRight = rightEnum.MoveNext();
						break;
				}
			}
			else if (hasLeft)
			{
				// Only left list has elements left
				if (joinType.HasFlag(JoinType.LeftJoin))
				{
					yield return (leftEnum.Current, default);
				}
				hasLeft = leftEnum.MoveNext();
			}
			else if (hasRight)
			{
				// Only right list has elements left
				if (joinType.HasFlag(JoinType.RightJoin))
				{
					yield return (default, rightEnum.Current);
				}
				hasRight = rightEnum.MoveNext();
			}
		}
	}

	/// <summary>
	/// Returns an enumerator that iterates through the merged lists.
	/// </summary>
	public IEnumerator<(T1 Left, T2 Right)> GetEnumerator() => Enumerate().GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>
	/// Disposes of the resources used by the ForwardFusion class.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes managed resources if disposing is true.
	/// </summary>
	/// <param name="disposing">Indicates whether to dispose managed resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			// Dispose the enumerables if they implement IDisposable
			if (leftList is IDisposable leftDisposable)
			{
				leftDisposable.Dispose();
			}
			if (rightList is IDisposable rightDisposable)
			{
				rightDisposable.Dispose();
			}
		}
	}
}

/// <summary>
/// Specifies the type of join operation.
/// </summary>
[Flags]
public enum JoinType
{
	InnerJoin = 0,
	LeftJoin = 1,
	RightJoin = 2,
	FullOuterJoin = LeftJoin | RightJoin
}


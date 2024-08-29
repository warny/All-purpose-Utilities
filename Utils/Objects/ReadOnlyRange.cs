using System;
using System.Collections;
using System.Collections.Generic;

namespace Utils.Objects
{
	/// <summary>
	/// Represents a read-only range of elements in a list, supporting custom stepping and indexing.
	/// </summary>
	/// <typeparam name="T">The type of elements in the list.</typeparam>
	public class ReadOnlyRange<T> : IReadOnlyList<T>, IReadOnlyCollection<T>
	{
		// The underlying list that the range is based on.
		private IReadOnlyList<T> innerList;

		// The starting index of the range.
		private readonly int startIndex;

		// The ending index of the range.
		private readonly int endIndex;

		// The step value that defines the stride between elements in the range.
		private readonly int step;

		/// <summary>
		/// Gets the element at the specified index within the range.
		/// </summary>
		/// <param name="index">The index within the range.</param>
		/// <returns>The element at the specified index.</returns>
		public T this[int index]
		{
			get {
				// Calculate the actual index in the underlying list.
				var innerIndex = startIndex + index * step;

				// Check if the calculated index is within the valid range.
				if (step > 0 && innerIndex > endIndex) throw new IndexOutOfRangeException();
				else if (step < 0 && innerIndex < endIndex) throw new IndexOutOfRangeException();

				return innerList[innerIndex];
			}
		}

		/// <summary>
		/// Gets the number of elements in the range.
		/// </summary>
		public int Count => Math.Abs(endIndex - startIndex) / Math.Abs(step) + 1;

		/// <summary>
		/// Initializes a new instance of the <see cref="ReadOnlyRange{T}"/> class.
		/// </summary>
		/// <param name="a">The underlying list.</param>
		/// <param name="startIndex">The starting index of the range.</param>
		/// <param name="endIndex">The ending index of the range.</param>
		/// <param name="step">The step value that defines the stride between elements.</param>
		internal ReadOnlyRange(IReadOnlyList<T> a, int startIndex, int endIndex, int step)
		{
			// Set the underlying list or throw an exception if it is null.
			this.innerList = a ?? throw new ArgumentNullException(nameof(a));

			// Ensure the step is non-zero.
			if (step == 0) throw new ArgumentOutOfRangeException(nameof(step));

			// Validate and adjust the start index.
			if (startIndex > a.Count) throw new ArgumentOutOfRangeException(nameof(startIndex));
			if (startIndex < 0) startIndex = a.Count + startIndex;
			if (startIndex < 0) throw new ArgumentOutOfRangeException(nameof(startIndex));

			// Validate and adjust the end index.
			if (endIndex >= a.Count) throw new ArgumentOutOfRangeException(nameof(startIndex));
			if (endIndex < 0) endIndex = a.Count + endIndex;
			if (endIndex < 0) throw new ArgumentOutOfRangeException(nameof(endIndex));

			// Ensure the start and end indexes are ordered correctly based on the step value.
			if (endIndex < startIndex ^ step < 0)
			{
				var temp = startIndex;
				startIndex = endIndex;
				endIndex = temp;
			}

			// Assign the validated and adjusted values.
			this.startIndex = startIndex;
			this.endIndex = endIndex;
			this.step = step;
		}

		/// <summary>
		/// Enumerates the elements in the range based on the step value.
		/// </summary>
		/// <returns>An enumerable sequence of elements in the range.</returns>
		private IEnumerable<T> Enumerate()
		{
			if (step > 0)
			{
				for (int i = startIndex; i <= endIndex; i += step)
				{
					yield return innerList[i];
				}
			}
			else
			{
				for (int i = startIndex; i >= endIndex; i += step)
				{
					yield return innerList[i];
				}
			}
		}

		/// <summary>
		/// Returns an enumerator that iterates through the range.
		/// </summary>
		/// <returns>An enumerator for the range.</returns>
		public IEnumerator<T> GetEnumerator() => Enumerate().GetEnumerator();

		/// <summary>
		/// Returns a non-generic enumerator that iterates through the range.
		/// </summary>
		/// <returns>A non-generic enumerator for the range.</returns>
		IEnumerator IEnumerable.GetEnumerator() => Enumerate().GetEnumerator();
	}

	/// <summary>
	/// Provides extension methods for creating and manipulating ranges on read-only lists.
	/// </summary>
	public static class RangeUtils
	{
		/// <summary>
		/// Creates a <see cref="ReadOnlyRange{T}"/> between the specified start and end indexes.
		/// </summary>
		/// <typeparam name="T">The type of elements in the list.</typeparam>
		/// <param name="a">The list to create the range from.</param>
		/// <param name="startIndex">The starting index of the range.</param>
		/// <param name="endIndex">The ending index of the range.</param>
		/// <returns>A read-only range of elements between the specified indexes.</returns>
		public static ReadOnlyRange<T> Between<T>(this IReadOnlyList<T> a, int startIndex, int endIndex) => new ReadOnlyRange<T>(a, startIndex, endIndex, 1);

		/// <summary>
		/// Creates a <see cref="ReadOnlyRange{T}"/> between the specified start and end indexes with a custom step.
		/// </summary>
		/// <typeparam name="T">The type of elements in the list.</typeparam>
		/// <param name="a">The list to create the range from.</param>
		/// <param name="startIndex">The starting index of the range.</param>
		/// <param name="endIndex">The ending index of the range.</param>
		/// <param name="step">The step value for the range.</param>
		/// <returns>A read-only range of elements between the specified indexes with the specified step.</returns>
		public static ReadOnlyRange<T> Between<T>(this IReadOnlyList<T> a, int startIndex, int endIndex, int step) => new ReadOnlyRange<T>(a, startIndex, endIndex, step);

		/// <summary>
		/// Creates a <see cref="ReadOnlyRange{T}"/> starting from the specified index with an optional step.
		/// </summary>
		/// <typeparam name="T">The type of elements in the list.</typeparam>
		/// <param name="a">The list to create the range from.</param>
		/// <param name="startIndex">The starting index of the range.</param>
		/// <param name="step">The step value for the range. Default is 1.</param>
		/// <returns>A read-only range starting from the specified index.</returns>
		public static ReadOnlyRange<T> From<T>(this IReadOnlyList<T> a, int startIndex, int step = 1)
		{
			return step > 0
				? new ReadOnlyRange<T>(a, startIndex, a.Count - 1, step)
				: new ReadOnlyRange<T>(a, startIndex, 0, step);
		}

		/// <summary>
		/// Creates a <see cref="ReadOnlyRange{T}"/> up to the specified position with an optional step.
		/// </summary>
		/// <typeparam name="T">The type of elements in the list.</typeparam>
		/// <param name="a">The list to create the range from.</param>
		/// <param name="position">The end index of the range.</param>
		/// <param name="step">The step value for the range. Default is 1.</param>
		/// <returns>A read-only range up to the specified position.</returns>
		public static ReadOnlyRange<T> To<T>(this IReadOnlyList<T> a, int position, int step = 1)
		{
			return step > 0
				? new ReadOnlyRange<T>(a, position % step, position, step)
				: new ReadOnlyRange<T>(a, a.Count - 1 - ((a.Count - position + 1) % step), position, step);
		}

		/// <summary>
		/// Creates a reversed <see cref="ReadOnlyRange{T}"/> of the entire list.
		/// </summary>
		/// <typeparam name="T">The type of elements in the list.</typeparam>
		/// <param name="a">The list to reverse.</param>
		/// <returns>A read-only range representing the reversed list.</returns>
		public static ReadOnlyRange<T> Reverse<T>(this IReadOnlyList<T> a) => new ReadOnlyRange<T>(a, 0, a.Count - 1, -1);
	}
}

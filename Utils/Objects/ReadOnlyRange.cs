using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Utils.Objects
{
	public class ReadOnlyRange<T> :
		IReadOnlyList<T>, IReadOnlyCollection<T>
	{
		private IReadOnlyList<T> innerList;
		private int startIndex;
		private int length;

		public T this[int index] => innerList [index + startIndex];
		public int Count => length;

		internal ReadOnlyRange(IReadOnlyList<T> a, int startIndex, int length)
		{
			this.innerList = a ?? throw new ArgumentNullException(nameof(a));
			if (startIndex > a.Count) throw new ArgumentOutOfRangeException(nameof(startIndex));
			if (startIndex < 0) startIndex = a.Count + startIndex;
			if (startIndex < 0) throw new ArgumentOutOfRangeException(nameof(startIndex));
			if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
			this.startIndex = startIndex;
			this.length = Math.Min(length, a.Count - startIndex);
		}

		private IEnumerable<T> Enumerate()
		{
			for (int i = startIndex; i < startIndex + length; i++)
			{
				yield return innerList[i];
			}
		}

		public IEnumerator<T> GetEnumerator() => Enumerate().GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => Enumerate().GetEnumerator();
	}

	public static class Range
	{
		public static ReadOnlyRange<T> Between<T>(this IReadOnlyList<T> a, int startIndex, int endIndex) => new ReadOnlyRange<T>(a, startIndex, endIndex - startIndex + 1);
		public static ReadOnlyRange<T> From<T> (this IReadOnlyList<T> a, int startIndex, int length) => new ReadOnlyRange<T>(a, startIndex, length);
		public static ReadOnlyRange<T> From<T>(this IReadOnlyList<T> a, int startIndex) => new ReadOnlyRange<T>(a, startIndex, a.Count);
		public static ReadOnlyRange<T> To<T>(this IReadOnlyList<T> a, int length) => new ReadOnlyRange<T>(a, 0, length);
	}
}

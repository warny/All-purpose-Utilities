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
		private int endIndex;
		private int step;

		public T this[int index]
		{
			get
			{
				var innerIndex = startIndex + index * step;
				if (step > 0 && innerIndex > endIndex) throw new IndexOutOfRangeException();
				else if (step < 0 && innerIndex < endIndex) throw new IndexOutOfRangeException();
				return innerList[innerIndex];
			}
		}

		public int Count => Math.Abs(endIndex - startIndex) / Math.Abs(step) + 1;

		internal ReadOnlyRange(IReadOnlyList<T> a, int startIndex, int endIndex, int step)
		{
			this.innerList = a ?? throw new ArgumentNullException(nameof(a));
			if (step == 0) throw new ArgumentOutOfRangeException(nameof(step));
			if (startIndex > a.Count) throw new ArgumentOutOfRangeException(nameof(startIndex));
			if (startIndex < 0) startIndex = a.Count + startIndex;
			if (startIndex < 0) throw new ArgumentOutOfRangeException(nameof(startIndex));
			if (endIndex >= a.Count) throw new ArgumentOutOfRangeException(nameof(startIndex));
			if (endIndex < 0) endIndex = a.Count + endIndex;
			if (endIndex < 0) throw new ArgumentOutOfRangeException(nameof(endIndex));
			if (endIndex < startIndex ^ step < 0) {
				var temp = startIndex;
				startIndex = endIndex;
				endIndex = temp;
			} 

			this.startIndex = startIndex;
			this.endIndex = endIndex;
			this.step = step;
		}

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

		public IEnumerator<T> GetEnumerator() => Enumerate().GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => Enumerate().GetEnumerator();
	}

	public static class RangeUtils
	{
		public static ReadOnlyRange<T> Between<T>(this IReadOnlyList<T> a, int startIndex, int endIndex) => new ReadOnlyRange<T>(a, startIndex, endIndex, 1);
		public static ReadOnlyRange<T> Between<T>(this IReadOnlyList<T> a, int startIndex, int endIndex, int step) => new ReadOnlyRange<T>(a, startIndex, endIndex, step);
		public static ReadOnlyRange<T> From<T>(this IReadOnlyList<T> a, int startIndex, int step = 1) 
		{
			return step > 0
				? new ReadOnlyRange<T>(a, startIndex, a.Count - 1, step)
				: new ReadOnlyRange<T>(a, startIndex, 0, step);
		}
		public static ReadOnlyRange<T> To<T>(this IReadOnlyList<T> a, int position, int step = 1)
		{
			return step > 0 
				? new ReadOnlyRange<T>(a, position % step, position, step)
				: new ReadOnlyRange<T>(a, a.Count - 1 - ((a.Count - position + 1) % step), position, step);
		}
		public static ReadOnlyRange<T> Reverse<T>(this IReadOnlyList<T> a) => new ReadOnlyRange<T>(a, 0, a.Count - 1, -1);
	}
}

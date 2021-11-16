using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Mathematics;

namespace Utils.Objects
{
	public class Ranges<T> : ICollection<Range<T>>
		where T : IComparable<T>
	{
		List<Range<T>> ranges = new List<Range<T>>();

		public Range<T> this[int index] => ranges.Skip(index).FirstOrDefault();

		public int Count => ranges.Count;

		public bool IsReadOnly { get; }

		public void Add (T start, T end) => Add(new Range<T>(start, end));
		public void Add(Range<T> item)
		{
			if (item.Start.CompareTo(item.End) == 0) return;

			List<Range<T>> toRemove = new List<Range<T>>();

			T start = item.Start, end = item.End;

			int index = 0;
			foreach (var range in ranges)
			{
				if (range.Overlap(item))
				{
					toRemove.Add(range);
					start = MathEx.Min(start, range.Start);
					end = MathEx.Max(end, range.End);
				}
				else if (item.End.CompareTo(range.Start) < 0)
				{
					break;
				}
				index++;
			}
			if (ranges.Count == 0)
			{
				ranges.Add(item);
			}
			else if (index >= ranges.Count)
			{
				ranges.Add(new Range<T>(start, end));
			}
			else
			{
				ranges.Insert(index, new Range<T>(start, end));
			};
			toRemove.ForEach(r => ranges.Remove(r));
		}

		public void Remove(T start, T end) => Remove(new Range<T>(start, end));
		public bool Remove(Range<T> item)
		{
			if (item.Start.CompareTo(item.End) == 0) return false;

			List<Range<T>> toRemove = new List<Range<T>>();
			List<Range<T>> toAdd = new List<Range<T>>();

			int insertIndex = -1;
			int index = 0;
			foreach (var range in ranges)
			{
				if (range.Overlap(item))
				{
					if (insertIndex == -1)
					{
						insertIndex = index;
					}
					toRemove.Add(range);
					if (item.Start.CompareTo(range.Start) > 0)
					{
						toAdd.Add(new Range<T>(range.Start, item.Start));
					}
					if (item.End.CompareTo(range.End) < 0)
					{
						toAdd.Add(new Range<T>(item.End, range.End));
					}
				}
				index++;
			}
			toRemove.ForEach(r => ranges.Remove(r));
			toAdd.ForEach(r=> ranges.Insert(insertIndex ++, r));

			return toRemove.Count == 0;
		}

		public void Clear() => ranges.Clear();

		public bool Contains(Range<T> item)
		{
			foreach (var range in ranges)
			{
				if (range.Contains(item)) return true;
				if (item.End.CompareTo(range.Start) < 0) return false;
			}
			return false;
		}

		public bool Contains(T item)
		{
			foreach (var range in ranges)
			{
				if (range.Contains(item)) return true;
				if (item.CompareTo(range.Start) < 0) return false;
			}
			return false;
		}

		public void CopyTo(Range<T>[] array, int arrayIndex) => ranges.CopyTo(array, arrayIndex);
		public IEnumerator<Range<T>> GetEnumerator() => ranges.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => ranges.GetEnumerator();

		public override string ToString() => String.Join(" ∪ ", ranges);

	}

	public class Range<T> where T : IComparable<T>
	{
		public T Start { get; }
		public T End { get; }

		public Range(T start, T end)
		{
			Start = MathEx.Min(start, end);
			End = MathEx.Max(start, end);
		}

		public bool Contains(T value) => value.CompareTo(Start) >= 0 && value.CompareTo(End) <= 0;

		public bool Contains(Range<T> range) => Contains(range.Start, range.End);
		public bool Contains(T start, T end) => Start.CompareTo(start) <= 0 && End.CompareTo(end) >= 0;

		public Range<T> Intersect(Range<T> range)
		{
			T start = MathEx.Max(Start, range.Start);
			T end = MathEx.Min(End, range.End);

			if (start.CompareTo(end) > 0) return null;
			return new Range<T>(start, end);
		}

		public bool Overlap(Range<T> range) => Overlap(range.Start, range.End);
		public bool Overlap(T start, T end) => Start.CompareTo(end) <= 0 && End.CompareTo(start) >= 0;

		public bool IsContained(Range<T> range) => IsContained(range.Start, range.End);
		public bool IsContained(T start, T end) => Start.CompareTo(start) >= 0 && End.CompareTo(end) <= 0;

		public void Deconstructor(out T start, out T end)
		{
			start = Start;
			end = End;
		}

		public override string ToString() => $"[ {Start} - {End} ]";
	}
}

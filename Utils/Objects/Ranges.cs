using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Mathematics;

namespace Utils.Objects
{
	public class Ranges<T> : ICollection<Range<T>>, ICloneable
		where T : IComparable<T>
	{
		readonly List<Range<T>> ranges;

		public Range<T> this[int index] => ranges.Skip(index).FirstOrDefault();

		public int Count => ranges.Count;

		public bool IsReadOnly { get; }

		public Ranges() {
			ranges = new List<Range<T>>();
		}

		public Ranges(Ranges<T> ranges)
		{
			this.ranges = ranges.ranges.ToList();
		}

		public Ranges(params Range<T>[] ranges) : this((IEnumerable<Range<T>>)ranges) { }
		public Ranges(IEnumerable<Range<T>> ranges) : this() { Add(ranges); }


		public void Add (T value) => Add(new Range<T>(value));
		public void Add(params Range<T>[] ranges) => Add((IEnumerable<Range<T>>)ranges);
		public void Add(IEnumerable<Range<T>> ranges)
		{
			foreach (var range in ranges)
			{
				this.Add(range);
			}
		}

		public void Add (T start, T end, bool containsStart = true, bool containsEnd = true) => Add(new Range<T>(start, end, containsStart, containsEnd));
		public void Add(Range<T> item)
		{
			List<Range<T>> toRemove = new List<Range<T>>();

			T start = item.Start, end = item.End;
			bool containsStart = item.ContainsStart, containsEnd = item.ContainsEnd;

			int index = 0;
			foreach (var range in ranges)
			{
				if (item.Start.CompareTo(range.End) > 0 || (item.Start.CompareTo(range.End) == 0 && !item.ContainsStart && !range.ContainsEnd))
				{
					index++;
					continue;
				}
				if (item.End.CompareTo(range.Start) < 0 || (item.End.CompareTo(range.Start) == 0 && !range.ContainsStart && !item.ContainsEnd))
				{
					break;
				}

				toRemove.Add(range);
				switch (start.CompareTo(range.Start))
				{
					case 0:
						containsStart |= range.ContainsStart;
						break;
					case 1:
						start = range.Start;
						containsStart = range.ContainsStart;
						break;
				}

				switch (end.CompareTo(range.End))
				{
					case -1:
						end = range.End;
						containsEnd = range.ContainsEnd;
						break;
					case 0:
						containsEnd |= range.ContainsEnd;
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
				ranges.Add(new Range<T>(start, end, containsStart, containsEnd));
			}
			else
			{
				ranges.Insert(index, new Range<T>(start, end, containsStart, containsEnd));
			};
			toRemove.ForEach(r => ranges.Remove(r));
		}

		public void Remove(T value) => Remove(new Range<T>(value));
		public void Remove(params Range<T>[] ranges) => Remove((IEnumerable<Range<T>>)ranges);
		public void Remove(IEnumerable<Range<T>> ranges)
		{
			foreach (var range in ranges)
			{
				this.Remove(range);
			}
		}
		public void Remove(T start, T end, bool containsStart = true, bool containsEnd = true) => Remove(new Range<T>(start, end, containsStart, containsEnd));
		public bool Remove(Range<T> item)
		{
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

					int compareStarts = item.Start.CompareTo(range.Start);
					if (compareStarts > 0 || (compareStarts == 0 && range.ContainsStart && !item.ContainsStart))
					{
						toAdd.Add(new Range<T>(range.Start, item.Start, range.ContainsStart, !item.ContainsStart));
					}

					int compareEnds = item.End.CompareTo(range.End);
					if (compareEnds < 0 || (compareStarts == 0 && !item.ContainsEnd && range.ContainsEnd))
					{
						toAdd.Add(new Range<T>(item.End, range.End, !item.ContainsStart, range.ContainsEnd)); 
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

		public object Clone() => new Ranges<T>(this);

		public static Ranges<T> operator +(Ranges<T> r1, Range<T> r2)
		{
			var result = new Ranges<T>(r1);
			r1.Add(r2);
			return r1;
		}

		public static Ranges<T> operator +(Ranges<T> r1, Ranges<T> r2)
		{
			var result = new Ranges<T>(r1);
			r1.Add(r2);
			return r1;
		}

		public static Ranges<T> operator -(Ranges<T> r1, Range<T> r2)
		{
			var result = new Ranges<T>(r1);
			r1.Remove(r2);
			return r1;
		}

		public static Ranges<T> operator -(Ranges<T> r1, Ranges<T> r2)
		{
			var result = new Ranges<T>(r1);
			r1.Remove(r2);
			return r1;
		}

	}

	public class Range<T> where T : IComparable<T>
	{
		public T Start { get; }
		public bool ContainsStart { get; }
		public T End { get; }
		public bool ContainsEnd { get; }

		public Range(T value) : this(value, value) { }
		public Range(T start, T end, bool containsStart = true, bool containsEnd = true)
		{
			int comparison = start.CompareTo(end);
			if (comparison > 0) throw new ArgumentException($"start ({start}) > end ({end})", nameof(end));
			if (comparison == 0 && !(containsStart && containsEnd)) throw new ArgumentException("A single element range can't exclude itself", nameof(start));

			Start = start;
			End = end;
			ContainsStart = containsStart;
			ContainsEnd = containsEnd;
		}

		public bool Contains(T value)
			=> (ContainsStart ? value.CompareTo(Start) >= 0 : value.CompareTo(Start) > 0)
			&& (ContainsEnd ? value.CompareTo(End) <= 0 : value.CompareTo(End) < 0);

		public bool Contains(Range<T> range) =>
			(
				this.Start.CompareTo(range.Start) < 0
				||
				(
					( this.ContainsStart || !range.ContainsStart )
					&& 
					this.Start.CompareTo(range.Start) == 0
				)
			) && (
				this.End.CompareTo(range.End) > 0
				||
				(
					(this.ContainsEnd || !range.ContainsEnd)
					&&
					this.End.CompareTo(range.End) == 0
				)
			);
		public bool Contains(T start, T end, bool containsStart = true, bool containsEnd = true) => Contains(new Range<T>(start, end, containsStart, containsEnd));

		public Range<T> Intersect(Range<T> range)
		{
			T start = MathEx.Max(Start, range.Start);
			T end = MathEx.Min(End, range.End);

			if (start.CompareTo(end) > 0) return null;
			return new Range<T>(start, end);
		}

		public bool Overlap(Range<T> range) => Overlap(this, range);
		public bool Overlap(T start, T end, bool containsStart = true, bool containsEnd = true) => Overlap(this, new Range<T>(start, end, containsStart, containsEnd));

		public static bool Overlap(Range<T> range1, Range<T> range2) =>
			(
				range1.Start.CompareTo(range2.End) < 0
				|| 
				(range1.ContainsStart && range2.ContainsEnd && range1.Start.CompareTo(range2.End) == 0)
			) && (
				range2.Start.CompareTo(range1.End) < 0
				||
				(range2.ContainsStart && range1.ContainsEnd && range2.Start.CompareTo(range1.End) == 0)
			);

		public bool IsContained(Range<T> range) => IsContained(range.Start, range.End);
		public bool IsContained(T start, T end) => Start.CompareTo(start) >= 0 && End.CompareTo(end) <= 0;


		public void Deconstructor(out T start, out T end)
		{
			start = Start;
			end = End;
		}

		public override string ToString() => $"{(ContainsStart ? "[" : "]") } {Start} - {End} {(ContainsEnd ? "]" : "[")}";

		public static implicit operator Range<T>((T Start, T End) range) => new Range<T>(range.Start, range.End);
	}
}

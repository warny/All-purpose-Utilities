using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Utils.Mathematics;

namespace Utils.Objects
{
	/// <summary>
	/// Represents a collection of ranges for a specific type T that supports various operations such as addition, subtraction, and merging of ranges.
	/// </summary>
	/// <typeparam name="T">The type of elements in the range, which must implement IComparable<T>.</typeparam>
	public class Ranges<T> : ICollection<Range<T>>, ICloneable, IFormattable,
		IAdditionOperators<Ranges<T>, Ranges<T>, Ranges<T>>,
		ISubtractionOperators<Ranges<T>, Ranges<T>, Ranges<T>>
		where T : IComparable<T>
	{
		// The internal list that stores the individual ranges.
		private readonly List<Range<T>> ranges;

		/// <summary>
		/// Gets the range at the specified index.
		/// </summary>
		/// <param name="index">The index of the range to get.</param>
		/// <returns>The range at the specified index.</returns>
		public Range<T> this[int index] => ranges.Skip(index).FirstOrDefault();

		/// <summary>
		/// Gets the number of ranges in the collection.
		/// </summary>
		public int Count => ranges.Count;

		/// <summary>
		/// Gets a value indicating whether the collection is read-only.
		/// </summary>
		public bool IsReadOnly { get; }

		/// <summary>
		/// Parses a string representation of ranges into a collection of Range objects.
		/// </summary>
		/// <typeparam name="T1">The type of the elements in the range.</typeparam>
		/// <param name="range">The string containing the ranges.</param>
		/// <param name="itemSearchPattern">The regex pattern to match the elements in the range.</param>
		/// <param name="valueParser">A function to parse the string into type T1.</param>
		/// <returns>An enumerable collection of parsed Range objects.</returns>
		protected static IEnumerable<Range<T1>> InnerParse<T1>(string range, string itemSearchPattern, IEnumerable<string> separators, Func<string, T1> valueParser) where T1 : IComparable<T1>
		{
			var parse = new Regex(@"(?<includesStart>(\[|\]))\s*(?<start>" + itemSearchPattern + @")\s*(" + string.Join ('|', separators) + @")\s*(?<end>" + itemSearchPattern + @")\s*(?<includesEnd>(\[|\]))");
			var results = parse.Matches(range);

			foreach (Match result in results)
			{
				yield return new Range<T1>(
					valueParser(result.Groups["start"].Value),
					valueParser(result.Groups["end"].Value),
					result.Groups["includesStart"].Value == "[",
					result.Groups["includesEnd"].Value == "]"
				);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ranges{T}"/> class.
		/// </summary>
		public Ranges()
		{
			ranges = new List<Range<T>>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ranges{T}"/> class by copying an existing collection of ranges.
		/// </summary>
		/// <param name="ranges">The ranges to copy.</param>
		public Ranges(Ranges<T> ranges)
		{
			this.ranges = ranges.ranges.ToList();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ranges{T}"/> class with an array of ranges.
		/// </summary>
		/// <param name="ranges">The array of ranges to add to the collection.</param>
		public Ranges(params Range<T>[] ranges) : this((IEnumerable<Range<T>>)ranges) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="Ranges{T}"/> class with an enumerable collection of ranges.
		/// </summary>
		/// <param name="ranges">The collection of ranges to add to the collection.</param>
		public Ranges(IEnumerable<Range<T>> ranges) : this() { Add(ranges); }

		/// <summary>
		/// Adds a value as a single-element range to the collection.
		/// </summary>
		/// <param name="value">The value to add as a range.</param>
		public void Add(T value) => Add(new Range<T>(value));

		/// <summary>
		/// Adds an array of ranges to the collection.
		/// </summary>
		/// <param name="ranges">The ranges to add.</param>
		public void Add(params Range<T>[] ranges) => Add((IEnumerable<Range<T>>)ranges);

		/// <summary>
		/// Adds an enumerable collection of ranges to the collection.
		/// </summary>
		/// <param name="ranges">The ranges to add.</param>
		public void Add(IEnumerable<Range<T>> ranges)
		{
			foreach (var range in ranges)
			{
				this.Add(range);
			}
		}

		/// <summary>
		/// Adds a range defined by start and end values to the collection.
		/// </summary>
		/// <param name="start">The start value of the range.</param>
		/// <param name="end">The end value of the range.</param>
		/// <param name="containsStart">Whether the start value is inclusive.</param>
		/// <param name="containsEnd">Whether the end value is inclusive.</param>
		public void Add(T start, T end, bool containsStart = true, bool containsEnd = true) => Add(new Range<T>(start, end, containsStart, containsEnd));

		/// <summary>
		/// Adds a range to the collection, merging overlapping ranges.
		/// </summary>
		/// <param name="item">The range to add.</param>
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
			}
			toRemove.ForEach(r => ranges.Remove(r));
		}

		/// <summary>
		/// Removes a value by converting it into a single-element range and removing it from the collection.
		/// </summary>
		/// <param name="value">The value to remove.</param>
		public void Remove(T value) => Remove(new Range<T>(value));

		/// <summary>
		/// Removes an array of ranges from the collection.
		/// </summary>
		/// <param name="ranges">The ranges to remove.</param>
		public void Remove(params Range<T>[] ranges) => Remove((IEnumerable<Range<T>>)ranges);

		/// <summary>
		/// Removes an enumerable collection of ranges from the collection.
		/// </summary>
		/// <param name="ranges">The ranges to remove.</param>
		public void Remove(IEnumerable<Range<T>> ranges)
		{
			foreach (var range in ranges)
			{
				this.Remove(range);
			}
		}

		/// <summary>
		/// Removes a range defined by start and end values from the collection.
		/// </summary>
		/// <param name="start">The start value of the range to remove.</param>
		/// <param name="end">The end value of the range to remove.</param>
		/// <param name="containsStart">Whether the start value is inclusive.</param>
		/// <param name="containsEnd">Whether the end value is inclusive.</param>
		public void Remove(T start, T end, bool containsStart = true, bool containsEnd = true) => Remove(new Range<T>(start, end, containsStart, containsEnd));

		/// <summary>
		/// Removes a range from the collection, splitting or adjusting existing ranges as needed.
		/// </summary>
		/// <param name="item">The range to remove.</param>
		/// <returns>True if any range was removed; otherwise, false.</returns>
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
					if (compareEnds < 0 || (compareEnds == 0 && !item.ContainsEnd && range.ContainsEnd))
					{
						toAdd.Add(new Range<T>(item.End, range.End, !item.ContainsStart, range.ContainsEnd));
					}
				}
				index++;
			}

			toRemove.ForEach(r => ranges.Remove(r));
			toAdd.ForEach(r => ranges.Insert(insertIndex++, r));

			return toRemove.Count != 0;
		}

		/// <summary>
		/// Clears all ranges from the collection.
		/// </summary>
		public void Clear() => ranges.Clear();

		/// <summary>
		/// Determines whether the collection contains the specified range.
		/// </summary>
		/// <param name="item">The range to check for containment.</param>
		/// <returns>True if the range is contained in the collection; otherwise, false.</returns>
		public bool Contains(Range<T> item)
		{
			foreach (var range in ranges)
			{
				if (range.Contains(item)) return true;
				if (item.End.CompareTo(range.Start) < 0) return false;
			}
			return false;
		}

		/// <summary>
		/// Determines whether the collection contains a specific value.
		/// </summary>
		/// <param name="item">The value to check for containment.</param>
		/// <returns>True if the value is contained in any range in the collection; otherwise, false.</returns>
		public bool Contains(T item)
		{
			foreach (var range in ranges)
			{
				if (range.Contains(item)) return true;
				if (item.CompareTo(range.Start) < 0) return false;
			}
			return false;
		}

		/// <summary>
		/// Copies the elements of the collection to an array, starting at a particular array index.
		/// </summary>
		/// <param name="array">The destination array.</param>
		/// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
		public void CopyTo(Range<T>[] array, int arrayIndex) => ranges.CopyTo(array, arrayIndex);

		/// <summary>
		/// Returns an enumerator that iterates through the collection of ranges.
		/// </summary>
		/// <returns>An enumerator for the collection.</returns>
		public IEnumerator<Range<T>> GetEnumerator() => ranges.GetEnumerator();

		/// <summary>
		/// Returns a non-generic enumerator that iterates through the collection of ranges.
		/// </summary>
		/// <returns>A non-generic enumerator for the collection.</returns>
		IEnumerator IEnumerable.GetEnumerator() => ranges.GetEnumerator();

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>A string representation of the collection.</returns>
		public override string ToString() => ToString(string.Empty, null);

		/// <summary>
		/// Returns a string that represents the current object, using a specified format.
		/// </summary>
		/// <param name="format">A format string.</param>
		/// <returns>A string representation of the collection.</returns>
		public string ToString(string format) => ToString(format, null);

		/// <summary>
		/// Returns a string that represents the current object, using a specified format provider.
		/// </summary>
		/// <param name="formatProvider">An object that provides culture-specific formatting information.</param>
		/// <returns>A string representation of the collection.</returns>
		public string ToString(IFormatProvider formatProvider) => ToString(string.Empty, formatProvider);

		/// <summary>
		/// Returns a string that represents the current object, using a specified format and format provider.
		/// </summary>
		/// <param name="format">A format string.</param>
		/// <param name="formatProvider">An object that provides culture-specific formatting information.</param>
		/// <returns>A string representation of the collection.</returns>
		public string ToString(string format, IFormatProvider formatProvider)
		{
			formatProvider ??= System.Globalization.CultureInfo.CurrentCulture;
			return string.Join(" ∪ ", ranges.Select(r => r.ToString(format, formatProvider)));
		}

		/// <summary>
		/// Creates a new object that is a copy of the current instance.
		/// </summary>
		/// <returns>A new Ranges{T} object that is a copy of this instance.</returns>
		public object Clone() => new Ranges<T>(this);

		#region Operators

		public static Ranges<T> operator +(Ranges<T> r1, Range<T> r2)
		{
			var result = new Ranges<T>(r1);
			result.Add(r2);
			return result;
		}

		public static Ranges<T> operator +(Ranges<T> r1, Ranges<T> r2)
		{
			var result = new Ranges<T>(r1);
			result.Add(r2);
			return result;
		}

		public static Ranges<T> operator -(Ranges<T> r1, Range<T> r2)
		{
			var result = new Ranges<T>(r1);
			result.Remove(r2);
			return result;
		}

		public static Ranges<T> operator -(Ranges<T> r1, Ranges<T> r2)
		{
			var result = new Ranges<T>(r1);
			result.Remove(r2);
			return result;
		}

		#endregion
	}

	/// <summary>
	/// Represents a range of values with a start and end, and supports various operations like containment and intersection.
	/// </summary>
	/// <typeparam name="T">The type of elements in the range, which must implement IComparable<T>.</typeparam>
	public class Range<T> : IFormattable where T : IComparable<T>
	{
		/// <summary>
		/// Gets the start value of the range.
		/// </summary>
		public T Start { get; }

		/// <summary>
		/// Gets a value indicating whether the start value is inclusive.
		/// </summary>
		public bool ContainsStart { get; }

		/// <summary>
		/// Gets the end value of the range.
		/// </summary>
		public T End { get; }

		/// <summary>
		/// Gets a value indicating whether the end value is inclusive.
		/// </summary>
		public bool ContainsEnd { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Range{T}"/> class with a single value.
		/// </summary>
		/// <param name="value">The single value to represent the range.</param>
		public Range(T value) : this(value, value) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="Range{T}"/> class with specified start and end values.
		/// </summary>
		/// <param name="start">The start value of the range.</param>
		/// <param name="end">The end value of the range.</param>
		/// <param name="containsStart">Whether the start value is inclusive.</param>
		/// <param name="containsEnd">Whether the end value is inclusive.</param>
		/// <exception cref="ArgumentException">Thrown when the start value is greater than the end value, or when the range excludes itself.</exception>
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

		/// <summary>
		/// Determines whether the range contains a specified value.
		/// </summary>
		/// <param name="value">The value to check for containment.</param>
		/// <returns>True if the range contains the value; otherwise, false.</returns>
		public bool Contains(T value)
			=> (ContainsStart ? value.CompareTo(Start) >= 0 : value.CompareTo(Start) > 0)
			&& (ContainsEnd ? value.CompareTo(End) <= 0 : value.CompareTo(End) < 0);

		/// <summary>
		/// Determines whether the range fully contains another range.
		/// </summary>
		/// <param name="range">The range to check for containment.</param>
		/// <returns>True if the range fully contains the other range; otherwise, false.</returns>
		public bool Contains(Range<T> range) =>
			(
				this.Start.CompareTo(range.Start) < 0
				||
				(
					(this.ContainsStart || !range.ContainsStart)
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

		/// <summary>
		/// Determines whether the range contains a specified start and end value.
		/// </summary>
		/// <param name="start">The start value to check.</param>
		/// <param name="end">The end value to check.</param>
		/// <param name="containsStart">Whether the start value is inclusive.</param>
		/// <param name="containsEnd">Whether the end value is inclusive.</param>
		/// <returns>True if the range contains the specified start and end values; otherwise, false.</returns>
		public bool Contains(T start, T end, bool containsStart = true, bool containsEnd = true) => Contains(new Range<T>(start, end, containsStart, containsEnd));

		/// <summary>
		/// Returns the intersection of this range with another range.
		/// </summary>
		/// <param name="range">The range to intersect with.</param>
		/// <returns>A new range representing the intersection, or null if they do not overlap.</returns>
		public Range<T> Intersect(Range<T> range)
		{
			T start = MathEx.Max(Start, range.Start);
			T end = MathEx.Min(End, range.End);

			if (start.CompareTo(end) > 0) return null;
			return new Range<T>(start, end);
		}

		/// <summary>
		/// Determines whether this range overlaps with another range.
		/// </summary>
		/// <param name="range">The range to check for overlap.</param>
		/// <returns>True if the ranges overlap; otherwise, false.</returns>
		public bool Overlap(Range<T> range) => Overlap(this, range);

		/// <summary>
		/// Determines whether this range overlaps with a specified start and end value.
		/// </summary>
		/// <param name="start">The start value to check.</param>
		/// <param name="end">The end value to check.</param>
		/// <param name="containsStart">Whether the start value is inclusive.</param>
		/// <param name="containsEnd">Whether the end value is inclusive.</param>
		/// <returns>True if the ranges overlap; otherwise, false.</returns>
		public bool Overlap(T start, T end, bool containsStart = true, bool containsEnd = true) => Overlap(this, new Range<T>(start, end, containsStart, containsEnd));

		/// <summary>
		/// Determines whether two ranges overlap.
		/// </summary>
		/// <param name="range1">The first range.</param>
		/// <param name="range2">The second range.</param>
		/// <returns>True if the ranges overlap; otherwise, false.</returns>
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

		/// <summary>
		/// Determines whether this range is contained within another range.
		/// </summary>
		/// <param name="range">The range to check for containment.</param>
		/// <returns>True if this range is contained within the specified range; otherwise, false.</returns>
		public bool IsContained(Range<T> range) => IsContained(range.Start, range.End);

		/// <summary>
		/// Determines whether this range is contained within a specified start and end value.
		/// </summary>
		/// <param name="start">The start value to check.</param>
		/// <param name="end">The end value to check.</param>
		/// <returns>True if this range is contained within the specified start and end values; otherwise, false.</returns>
		public bool IsContained(T start, T end) => Start.CompareTo(start) >= 0 && End.CompareTo(end) <= 0;

		/// <summary>
		/// Deconstructs the range into its start and end values.
		/// </summary>
		/// <param name="start">Outputs the start value of the range.</param>
		/// <param name="end">Outputs the end value of the range.</param>
		public void Deconstruct(out T start, out T end)
		{
			start = Start;
			end = End;
		}

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>A string representation of the range.</returns>
		public override string ToString() => ToString(string.Empty, null);

		/// <summary>
		/// Returns a string that represents the current object, using a specified format.
		/// </summary>
		/// <param name="format">A format string.</param>
		/// <returns>A string representation of the range.</returns>
		public string ToString(string format) => ToString(format, null);

		/// <summary>
		/// Returns a string that represents the current object, using a specified format provider.
		/// </summary>
		/// <param name="formatProvider">An object that provides culture-specific formatting information.</param>
		/// <returns>A string representation of the range.</returns>
		public string ToString(IFormatProvider formatProvider) => ToString(string.Empty, formatProvider);

		/// <summary>
		/// Returns a string that represents the current object, using a specified format and format provider.
		/// </summary>
		/// <param name="format">A format string.</param>
		/// <param name="formatProvider">An object that provides culture-specific formatting information.</param>
		/// <returns>A string representation of the range.</returns>
		public string ToString(string format, IFormatProvider formatProvider)
		{
			if (format != null && (Start is IFormattable || End is IFormattable))
			{
				formatProvider ??= System.Globalization.CultureInfo.CurrentCulture;
				return string.Format(formatProvider, "{0} {1:" + format + "} - {2:" + format + "} {3}", ContainsStart ? "[" : "]", Start, End, ContainsEnd ? "]" : "[");
			}
			else
			{
				return string.Format(formatProvider, "{0} {1} - {2} {3}", ContainsStart ? "[" : "]", Start, End, ContainsEnd ? "]" : "[");
			}
		}

		/// <summary>
		/// Converts a tuple to a range implicitly.
		/// </summary>
		/// <param name="range">A tuple containing the start and end values.</param>
		public static implicit operator Range<T>((T Start, T End) range) => new Range<T>(range.Start, range.End);
	}
}

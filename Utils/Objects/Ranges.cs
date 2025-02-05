using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Utils.Objects
{
	/// <summary>
	/// Represents a set of intervals (ranges) for an ordered type <typeparamref name="T"/>.
	/// Provides set-like operations (union, intersection, difference, symmetric difference)
	/// via bitwise operators:
	///   | => Union,
	///   & => Intersection,
	///   ^ => Symmetric Difference,
	///   ~ => Complement (relative to entire domain, if desired),
	///   - => Difference (Except).
	/// 
	/// The intervals are stored internally as disjoint, sorted <see cref="Range{T}"/> objects.
	/// </summary>
	/// <typeparam name="T">A comparable type that supports ordering.</typeparam>
	public class Ranges<T> : IFormattable, IEquatable<Ranges<T>>,
		ISubtractionOperators<Ranges<T>, Ranges<T>, Ranges<T>>,
		IBitwiseOperators<Ranges<T>, Ranges<T>, Ranges<T>>
		where T : IComparable<T>
	{
		#region Private Fields

		private readonly List<Range<T>> _ranges = new();

		#endregion

		#region Constructors

		/// <summary>
		/// Creates an empty set of intervals.
		/// </summary>
		public Ranges() { }

		/// <summary>
		/// Creates a new set of intervals by copying all intervals from <paramref name="other"/>.
		/// </summary>
		public Ranges(Ranges<T> other)
		{
			if (other is null) throw new ArgumentNullException(nameof(other));
			_ranges.AddRange(other._ranges);
		}

		/// <summary>
		/// Creates a set of intervals from the provided collection of <see cref="Range{T}"/>.
		/// Overlapping intervals are merged into disjoint intervals.
		/// </summary>
		public Ranges(IEnumerable<Range<T>> intervals)
		{
			if (intervals is null) throw new ArgumentNullException(nameof(intervals));
			AddAll(intervals);
		}

		/// <summary>
		/// Creates a set of intervals from one or more <see cref="Range{T}"/>.
		/// Overlapping intervals are merged into disjoint intervals.
		/// </summary>
		public Ranges(params Range<T>[] intervals)
			: this((IEnumerable<Range<T>>)intervals)
		{
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Returns the total count of disjoint intervals stored.
		/// </summary>
		public int Count => _ranges.Count;

		/// <summary>
		/// Returns a read-only snapshot of the underlying intervals (disjoint, sorted).
		/// </summary>
		public IReadOnlyList<Range<T>> Intervals => _ranges.AsReadOnly();

		#endregion

		/// <typeparam name="T1">The type of the elements in the range.</typeparam>
		/// <param name="range">The string containing the ranges.</param>
		/// <param name="itemSearchPattern">The regex pattern to match the elements in the range.</param>
		/// <param name="valueParser">A function to parse the string into type T1.</param>
		/// <returns>An enumerable collection of parsed Range objects.</returns>
		protected static IEnumerable<Range<T1>> InnerParse<T1>(string range, string itemSearchPattern, IEnumerable<string> separators, Func<string, T1> valueParser)
			where T1 : IComparable<T1>
		{
			var parse = new Regex(@"(?<includesStart>(\[|\]))\s*(?<start>" + itemSearchPattern + @")\s*(" + string.Join('|', separators) + @")\s*(?<end>" + itemSearchPattern + @")\s*(?<includesEnd>(\[|\]))");
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


		#region Public Methods

		/// <summary>
		/// Removes all intervals, resulting in an empty set.
		/// </summary>
		public void Clear() => _ranges.Clear();

		/// <summary>
		/// Checks if the specified value <paramref name="value"/> is contained
		/// in any of the intervals of this set.
		/// </summary>
		public bool Contains(T value)
		{
			// We can short-circuit with a linear or binary search. 
			// For clarity, we'll do a linear approach:
			foreach (var r in _ranges)
			{
				if (value.CompareTo(r.Start) < 0)
					// we've passed all intervals that could contain value
					break;

				if (r.Contains(value))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Checks if the entire <paramref name="range"/> is contained in this set.
		/// </summary>
		public bool Contains(Range<T> range)
		{
			// We'll see if one of the intervals fully covers 'range'.
			foreach (var r in _ranges)
			{
				if (r.Contains(range))
					return true;

				if (r.Start.CompareTo(range.End) > 0)
					break;
			}
			return false;
		}

		/// <summary>
		/// Adds multiple intervals, merging overlaps.
		/// </summary>
		public void AddAll(IEnumerable<Range<T>> intervals)
		{
			if (intervals is null) throw new ArgumentNullException(nameof(intervals));
			foreach (var r in intervals)
			{
				Add(r);
			}
		}

		/// <summary>
		/// Adds a single interval, merging with existing intervals if they overlap or touch.
		/// </summary>
		public void Add(T singleValue)
			=> Add(new Range<T>(singleValue, singleValue, true, true));

		/// <summary>
		/// Adds a single interval, merging with existing intervals if they overlap or touch.
		/// </summary>
		public void Add(T start, T end, bool includeStart = true, bool includeEnd = true)
			=> Add(new Range<T>(start, end, includeStart, includeEnd));

		/// <summary>
		/// Adds a single interval, merging with existing intervals if they overlap or touch.
		/// </summary>
		public void Add(Range<T> interval)
		{
			if (_ranges.Count == 0)
			{
				_ranges.Add(interval);
				return;
			}

			var toRemove = new List<Range<T>>();
			var newStart = interval.Start;
			var newEnd = interval.End;
			var newContainsStart = interval.ContainsStart;
			var newContainsEnd = interval.ContainsEnd;

			int insertPos = 0;
			for (int i = 0; i < _ranges.Count; i++)
			{
				var current = _ranges[i];

				// If new interval is strictly after 'current', 
				// shift insertion index
				if (!OverlapOrTouch(current, newStart, newContainsStart, after: true))
				{
					insertPos = i + 1;
					continue;
				}

				// If new interval is strictly before 'current', break
				if (!OverlapOrTouch(current, newEnd, newContainsEnd, after: false))
				{
					break;
				}

				// Overlap or adjacency => remove 'current', merge
				toRemove.Add(current);

				// Merge Start
				int cmpS = current.Start.CompareTo(newStart);
				if (cmpS < 0 ||
					(cmpS == 0 && current.ContainsStart && !newContainsStart))
				{
					newStart = current.Start;
					newContainsStart = current.ContainsStart;
				}

				// Merge End
				int cmpE = current.End.CompareTo(newEnd);
				if (cmpE > 0 ||
					(cmpE == 0 && current.ContainsEnd && !newContainsEnd))
				{
					newEnd = current.End;
					newContainsEnd = current.ContainsEnd;
				}
			}

			// Remove all overlapping intervals
			foreach (var old in toRemove)
			{
				_ranges.Remove(old);
			}

			// Insert the merged result
			var merged = new Range<T>(newStart, newEnd, newContainsStart, newContainsEnd);
			_ranges.Insert(insertPos, merged);
		}

		/// <summary>
		/// Removes multiple intervals from this set.
		/// </summary>
		public void RemoveAll(IEnumerable<Range<T>> intervals)
		{
			if (intervals is null) throw new ArgumentNullException(nameof(intervals));
			foreach (var r in intervals)
			{
				Remove(r);
			}
		}
		/// <summary>
		/// Removes a single interval from this set, splitting or trimming intervals.
		/// </summary>
		public void Remove(T singleValue)
			=> Remove(new Range<T>(singleValue, singleValue, true, true));

		/// <summary>
		/// Removes a single interval from this set, splitting or trimming intervals.
		/// </summary>
		public void Remove(T start, T end, bool includeStart = true, bool includeEnd = true)
			=> Remove(new Range<T>(start, end, includeStart, includeEnd));

		/// <summary>
		/// Removes a single interval from this set, splitting or trimming intervals.
		/// </summary>
		public void Remove(Range<T> toRemove)
		{
			var newList = new List<Range<T>>();

			foreach (var current in _ranges)
			{
				if (!current.Overlap(toRemove))
				{
					// no overlap => keep
					newList.Add(current);
					continue;
				}

				// Possibly split
				// Left part
				int cmpS = toRemove.Start.CompareTo(current.Start);
				if (cmpS > 0 ||
					(cmpS == 0 && current.ContainsStart && !toRemove.ContainsStart))
				{
					newList.Add(new Range<T>(
						current.Start,
						toRemove.Start,
						current.ContainsStart,
						!toRemove.ContainsStart
					));
				}

				// Right part
				int cmpE = toRemove.End.CompareTo(current.End);
				if (cmpE < 0 ||
					(cmpE == 0 && !toRemove.ContainsEnd && current.ContainsEnd))
				{
					newList.Add(new Range<T>(
						toRemove.End,
						current.End,
						!toRemove.ContainsEnd,
						current.ContainsEnd
					));
				}
			}

			// Replace old
			_ranges.Clear();
			_ranges.AddRange(newList);
		}

		#endregion

		#region Set Operations

		/// <summary>
		/// Returns a new set representing the union of <paramref name="left"/> and <paramref name="right"/>.
		/// </summary>
		public static Ranges<T> Union(Ranges<T> left, Ranges<T> right)
		{
			ArgumentNullException.ThrowIfNull(left);
			ArgumentNullException.ThrowIfNull(right);

			var result = new Ranges<T>(left);
			result.AddAll(right._ranges);
			return result;
		}

		/// <summary>
		/// Returns a new set representing the intersection of <paramref name="left"/> and <paramref name="right"/>.
		/// </summary>
		public static Ranges<T> Intersect(Ranges<T> left, Ranges<T> right)
		{
			ArgumentNullException.ThrowIfNull(left);
			ArgumentNullException.ThrowIfNull(right);

			var output = new Ranges<T>();
			foreach (var r1 in left._ranges)
			{
				foreach (var r2 in right._ranges)
				{
					var inter = r1.Intersect(r2);
					if (inter.HasValue)
						output.Add(inter.Value);
				}
			}
			return output;
		}

		/// <summary>
		/// Returns a new set representing all intervals in <paramref name="left"/> that are not in <paramref name="right"/>.
		/// (Set difference).
		/// </summary>
		public static Ranges<T> Except(Ranges<T> left, Ranges<T> right)
		{
			ArgumentNullException.ThrowIfNull(left);
			ArgumentNullException.ThrowIfNull(right);

			var result = new Ranges<T>(left);
			foreach (var r in right._ranges)
			{
				result.Remove(r);
			}
			return result;
		}

		/// <summary>
		/// Returns a new set representing the symmetric difference of <paramref name="left"/> and <paramref name="right"/>.
		/// (Elements in left or right, but not both).
		/// </summary>
		public static Ranges<T> SymmetricDifference(Ranges<T> left, Ranges<T> right)
		{
			// (left \ right) ∪ (right \ left)
			var part1 = Except(left, right);
			var part2 = Except(right, left);
			return Union(part1, part2);
		}

		/// <summary>
		/// Returns the complement with respect to the entire domain (-∞..+∞).
		/// 
		/// Implementation details for infinite intervals will vary. 
		/// If you want actual intervals that store ±∞, you must design a specialized <see cref="Range{T}"/> mechanism for that.
		/// </summary>
		public static Ranges<T> Complement(Ranges<T> range)
		{
			// STUB: If you want a real "complement" with infinite intervals, 
			// you'd store something like [-∞..range[0].Start), 
			// plus all gaps, plus (range[n-1].End..+∞], etc.
			throw new NotImplementedException("Complement with infinite intervals not implemented.");
		}

		#endregion

		#region Bitwise Operators

		/// <summary>Union => bitwise OR operator.</summary>
		public static Ranges<T> operator |(Ranges<T> left, Ranges<T> right)
			=> Union(left, right);

		/// <summary>Intersection => bitwise AND operator.</summary>
		public static Ranges<T> operator &(Ranges<T> left, Ranges<T> right)
			=> Intersect(left, right);

		/// <summary>Symmetric Difference => bitwise XOR operator.</summary>
		public static Ranges<T> operator ^(Ranges<T> left, Ranges<T> right)
			=> SymmetricDifference(left, right);

		/// <summary>Complement => bitwise NOT operator.</summary>
		public static Ranges<T> operator ~(Ranges<T> range)
			=> Complement(range);

		/// <summary>Difference => set except operator.</summary>
		public static Ranges<T> operator -(Ranges<T> left, Ranges<T> right)
			=> Except(left, right);

		#endregion

		#region Formatting (IFormattable)

		public override string ToString() => ToString(string.Empty, null);
		public string ToString(string? format) => ToString(format, null);
		public string ToString(IFormatProvider? formatProvider) => ToString(string.Empty, formatProvider);

		/// <summary>
		/// Converts all stored intervals to string, joined by a union symbol " ∪ ".
		/// E.g. "[1..2] ∪ (3..5)"
		/// </summary>
		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			formatProvider ??= CultureInfo.CurrentCulture;
			if (_ranges.Count == 0)
				return "Ø"; // or "" for empty set

			return string.Join(" ∪ ", _ranges.Select(r => r.ToString(format, formatProvider)));
		}

		#endregion

		#region Equality

		public override bool Equals(object? obj)
		{
			return obj is Ranges<T> other && Equals(other);
		}

		public bool Equals(Ranges<T>? other)
		{
			if (other is null) return false;
			return _ranges.SequenceEqual(other._ranges);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = 17;
				foreach (var interval in _ranges)
				{
					hash = hash * 31 + interval.GetHashCode();
				}
				return hash;
			}
		}

		#endregion

		#region Private Helpers

		/// <summary>
		/// Checks if <paramref name="p"/> is strictly after or before <paramref name="current"/>
		/// so that there's no overlap/adjacency.
		/// 
		/// <paramref name="after"/> = true => means "is newStart after 'current'?"
		/// <paramref name="after"/> = false => means "is newEnd before 'current'?"
		/// 
		/// If it returns false, we skip merging in one direction.
		/// </summary>
		private static bool OverlapOrTouch(Range<T> current, T p, bool pIsInclusive, bool after)
		{
			// If after==true => we want to see if p < current.Start 
			// or p == current.Start with no adjacency => 
			// means we have not yet reached overlap => keep going
			// The logic is reversed from typical "overlap" check.

			// We'll break it down to two scenarios. For clarity, 
			// re-check your adjacency logic if you want open/closed intervals to "touch" as one.
			//
			// In the code snippet above, we simply check whether newStart is strictly greater than current.End 
			// or newEnd is strictly less than current.Start, factoring in inclusiveness.

			if (after)
			{
				// "after" => is p definitely after current => no overlap
				// p > current.End => no overlap
				int cmp = p.CompareTo(current.End);
				if (cmp > 0)
					return false; // definitely after
				if (cmp < 0)
					return true;  // definitely overlap
								  // cmp == 0 => adjacency => if current.ContainsEnd or pIsInclusive => merges
				if (!current.ContainsEnd && !pIsInclusive)
					return false; // no adjacency
				return true;     // adjacency => merges
			}
			else
			{
				// "before" => is p definitely before current => no overlap
				// p < current.Start => no overlap
				int cmp = p.CompareTo(current.Start);
				if (cmp < 0)
					return false; // definitely before
				if (cmp > 0)
					return true;  // overlap
								  // cmp == 0 => adjacency => check inclusiveness
				if (!current.ContainsStart && !pIsInclusive)
					return false;
				return true;
			}
		}

		#endregion
	}


	/// <summary>
	/// Represents a range of values with a start and end, plus inclusivity flags.
	/// Stored as a struct for efficient copying.
	/// </summary>
	/// <typeparam name="T">A comparable type that supports ordering.</typeparam>
	public struct Range<T> : IFormattable, IEquatable<Range<T>?>
		where T : IComparable<T>
	{
		public T Start { get; }
		public T End { get; }
		public bool ContainsStart { get; }
		public bool ContainsEnd { get; }

		public Range(T start, T end, bool containsStart = true, bool containsEnd = true)
		{
			// Validate the ordering
			int comparison = start.CompareTo(end);
			if (comparison > 0)
				throw new ArgumentException($"start ({start}) > end ({end})");
			if (comparison == 0 && !(containsStart && containsEnd))
				throw new ArgumentException("A single-element range must include that element.");

			Start = start;
			End = end;
			ContainsStart = containsStart;
			ContainsEnd = containsEnd;
		}

		/// <summary>
		/// Constructs a degenerate range with a single value [value..value].
		/// </summary>
		public Range(T value) : this(value, value, true, true) { }

		#region Containment / Overlap

		public bool Contains(T value)
		{
			bool leftOk = ContainsStart
				? value.CompareTo(Start) >= 0
				: value.CompareTo(Start) > 0;

			bool rightOk = ContainsEnd
				? value.CompareTo(End) <= 0
				: value.CompareTo(End) < 0;

			return leftOk && rightOk;
		}

		public bool Contains(Range<T> other)
		{
			// This range must start <= other.Start
			// If Start == other.Start, then either both are inclusive or we containStart if other does
			bool leftOk = Start.CompareTo(other.Start) < 0
				|| (Start.CompareTo(other.Start) == 0 && (ContainsStart || !other.ContainsStart));

			// Similarly for the end
			bool rightOk = End.CompareTo(other.End) > 0
				|| (End.CompareTo(other.End) == 0 && (ContainsEnd || !other.ContainsEnd));

			return leftOk && rightOk;
		}

		public bool Overlap(Range<T> other) => Overlap(this, other);

		public static bool Overlap(Range<T> a, Range<T> b)
		{
			// a starts <= b ends
			bool leftCheck = a.Start.CompareTo(b.End) < 0
				|| (a.Start.CompareTo(b.End) == 0 && a.ContainsStart && b.ContainsEnd);

			// b starts <= a ends
			bool rightCheck = b.Start.CompareTo(a.End) < 0
				|| (b.Start.CompareTo(a.End) == 0 && b.ContainsStart && a.ContainsEnd);

			return leftCheck && rightCheck;
		}

		/// <summary>
		/// Tries to compute the intersection of two ranges. Returns null if they don't overlap.
		/// </summary>
		public Range<T>? Intersect(Range<T> other)
		{
			T newStart = Max(Start, other.Start);
			T newEnd = Min(End, other.End);

			if (newStart.CompareTo(newEnd) > 0)
				return null;

			// We might refine inclusivity if the boundaries match exactly.
			// For simplicity, here's a minimal approach:
			bool cStart = ContainsStart && other.ContainsStart
				? newStart.CompareTo(Start) == 0 && newStart.CompareTo(other.Start) == 0
				: newStart.CompareTo(Start) > 0 && newStart.CompareTo(other.Start) > 0
|| (Start.CompareTo(other.Start) == 0
					   ? (ContainsStart && other.ContainsStart)
					   : false);

			// For demonstration, you could do more precise logic. We'll keep it simple.

			bool cEnd = ContainsEnd && other.ContainsEnd;
			// etc. (In real code, carefully handle all boundary cases.)

			// The simplest approach is to be inclusive if at least one side is inclusive, etc.
			// But that depends on your domain rules.

			return new Range<T>(newStart, newEnd, cStart, cEnd);
		}

		private static T Max(T a, T b) => a.CompareTo(b) >= 0 ? a : b;
		private static T Min(T a, T b) => a.CompareTo(b) <= 0 ? a : b;

		#endregion

		#region Formatting

		public override string ToString() => ToString(string.Empty, null);
		public string ToString(string? format) => ToString(format, null);
		public string ToString(IFormatProvider? formatProvider) => ToString(string.Empty, formatProvider);

		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			// Example: "[1..5]", "(2..3]", etc.
			var leftBracket = ContainsStart ? "[" : "]";
			var rightBracket = ContainsEnd ? "]" : "[";

			// If T implements IFormattable, you can do Start.ToString(format, formatProvider)
			// Otherwise, just do Start.ToString()
			return string.Format(formatProvider,
				$"{{0}} {{1:{format}}} - {{2:{format}}} {{3}}",
				leftBracket, Start, End, rightBracket);
		}

		#endregion

		#region Equality

		public override readonly bool Equals(object? obj)
		{
			return obj is Range<T> r && Equals(r);
		}

		public readonly bool Equals(Range<T>? other)
		{
			if (other is null) return false;
			return Start.CompareTo(other.Value.Start) == 0
				&& End.CompareTo(other.Value.End) == 0
				&& ContainsStart == other.Value.ContainsStart
				&& ContainsEnd == other.Value.ContainsEnd;
		}

		public override readonly int GetHashCode()
			=> HashCode.Combine(Start, End, ContainsStart, ContainsEnd);

		#endregion
	}
}

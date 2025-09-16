using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Numerics;
using Utils.Mathematics;
using System.Formats.Tar; // If needed for IAdditionOperators, etc.

namespace Utils.Range
{
	/// <summary>
	/// Represents a collection of integer intervals (ranges),
	/// each of which may include negative or positive infinity as an endpoint.
	/// 
	/// Bitwise-like operators for set operations:
	///   | (Union),
	///   &amp; (Intersection),
	///   ^ (Symmetric Difference),
	///   ~ (Complement).
	/// 
	/// Internally stores multiple non-overlapping intervals sorted by their (Min, Max).
	/// A null Minimum =&gt; -∞.
	/// A null Maximum =&gt; +∞.
	/// </summary>
	public sealed class IntRange<T> : 
		IEnumerable<T>,
		IBitwiseOperators<IntRange<T>, IntRange<T>, IntRange<T>>,
		ISubtractionOperators<IntRange<T>, IntRange<T>, IntRange<T>>,
		IFormattable
		where T : struct, IBinaryInteger<T>, IComparable<T>, IMinMaxValue<T>
	{
		#region Inner Class: SimpleRange

		/// <summary>
		/// Represents a single contiguous interval of integers, 
		/// where endpoints can be null to indicate -∞ or +∞.
		/// </summary>
		private struct SimpleRange : IComparable<SimpleRange?>, IComparable, IFormattable
		{
			/// <summary>
			/// Creates a new range [minimum, maximum].
			/// Any endpoint may be null for ±∞.
			/// If <paramref name="minimum"/> is null =&gt; -∞,
			/// if <paramref name="maximum"/> is null =&gt; +∞.
			/// </summary>
			public SimpleRange(T? minimum, T? maximum)
			{
				// Optionally, you could ensure min <= max if both non-null.
				// That's up to your usage scenario.
				Minimum = minimum;
				Maximum = maximum;
			}

			/// <summary>
			/// The inclusive lower bound of this range.
			/// A value of <c>null</c> =&gt; -∞.
			/// </summary>
			public T? Minimum { get; }

			/// <summary>
			/// The inclusive upper bound of this range.
			/// A value of <c>null</c> =&gt; +∞.
			/// </summary>
			public T? Maximum { get; }

			/// <summary>
			/// Returns true if this range is effectively infinite in both directions.
			/// </summary>
			public bool IsFullRange => Minimum is null && Maximum is null;

			#region Comparison / Sorting

			/// <summary>
			/// Compares two SimpleRanges by their lower bound, then upper bound.
			/// Null endpoints are considered -∞ if it's Minimum, +∞ if it's Maximum.
			/// 
			/// Order: 
			///   (null minimum) &lt; any finite minimum
			///   among finite mins =&gt; normal int comparison
			///   if mins are equal =&gt; compare max similarly 
			///   (finite max) &lt; (null max).
			/// </summary>
			public readonly int CompareTo(SimpleRange? other)
			{
				if (other is null) return 1; // Non-null > null by .NET convention

				// Compare Min
				int minCompare = CompareNullableInt(Minimum, other.Value.Minimum, isMin: true);
				if (minCompare != 0)
					return minCompare;

				// If mins are equal or both ∞, compare Max
				int maxCompare = CompareNullableInt(Maximum, other.Value.Maximum, isMin: false);
				return maxCompare;
			}

			public int CompareTo(object obj)
				=> obj switch
				{
					SimpleRange r => CompareTo(r),
					_ => throw new NotImplementedException()
				};


			/// <summary>
			/// Helper that compares two int? endpoints. 
			/// If 'isMin' is true =&gt; a null means -∞. 
			/// If 'isMin' is false =&gt; a null means +∞.
			/// 
			/// Returns negative if left &lt; right,
			/// zero if equal,
			/// positive if left &gt; right.
			/// </summary>
			private static int CompareNullableInt(T? left, T? right, bool isMin)
			{
				// If both are null, they're effectively the same "∞".
				if (!left.HasValue && !right.HasValue)
				{
					// both -∞ or both +∞ => considered equal
					return 0;
				}

				// If only one is null => check if isMin or not
				if (!left.HasValue)
				{
					// left is ∞
					// if isMin => left = -∞ => that is "less" than any finite
					// if not isMin => left = +∞ => that is "greater" than any finite
					return isMin ? -1 : 1;
				}
				if (!right.HasValue)
				{
					// right is ∞
					// if isMin => right = -∞ => left must be > right
					// if not isMin => right = +∞ => left must be < right
					return isMin ? 1 : -1;
				}

				// Both are finite => normal int comparison
				return left.Value.CompareTo(right.Value);
			}

			#endregion

			#region Set Operations: Union, Intersect, Except

			/// <summary>
			/// Checks if two ranges overlap or are adjacent (i.e., can be merged).
			/// If so, <see cref="Union"/> won't return null.
			/// </summary>
			public static bool CanUnion(SimpleRange? r1, SimpleRange? r2)
			{
				if (r1 is null || r2 is null) return false;

				// If r1 or r2 covers full range, they definitely can union
				if (r1.Value.IsFullRange || r2.Value.IsFullRange) return true;

				// We'll interpret adjacency as "r1.Max + 1 >= r2.Min" 
				// but handle null for infinite endpoints
				// 
				// Condition:
				//   (r1.Min <= r2.Max + 1) and (r2.Min <= r1.Max + 1)
				// in an "infinite-friendly" manner

				// We'll define helper to see "r1.Max + 1" effectively
				var r1maxPlusOne = NullableIntEx.Increment(r1.Value.Maximum); // might be null => +∞
				var r2maxPlusOne = NullableIntEx.Increment(r2.Value.Maximum);

				// Check overlap
				bool cond1 = NullableIntEx.LessOrEqual(r1.Value.Minimum, r2maxPlusOne);
				bool cond2 = NullableIntEx.LessOrEqual(r2.Value.Minimum, r1maxPlusOne);

				return (cond1 && cond2);
			}

			/// <summary>
			/// Returns the union of two overlapping or adjacent ranges,
			/// or <c>null</c> if disjoint.
			/// </summary>
			public static SimpleRange? Union(SimpleRange? r1, SimpleRange? r2)
			{
				if (r1 is null) return r2;
				if (r2 is null) return r1;

				if (!CanUnion(r1, r2)) return null;

				// The union is simply min of both minima, max of both maxima
				var newMin = NullableIntEx.Min(r1.Value.Minimum, r2.Value.Minimum, isMin: true);
				var newMax = NullableIntEx.Max(r1.Value.Maximum, r2.Value.Maximum, isMax: true);
				return new SimpleRange(newMin, newMax);
			}

			/// <summary>
			/// Returns the intersection of two ranges, or <c>null</c> if no overlap.
			/// </summary>
			public static SimpleRange? Intersect(SimpleRange? r1, SimpleRange? r2)
			{
				if (r1 is null || r2 is null) return null;

				// Intersection => [max of minima, min of maxima]
				var start = NullableIntEx.Max(r1.Value.Minimum, r2.Value.Minimum, isMax: false);
				var end = NullableIntEx.Min(r1.Value.Maximum, r2.Value.Maximum, isMin: false);

				// If start > end => no intersection
				int compare = NullableIntEx.Compare(start, end, isMinForComparison: true);
				if (compare > 0)
					return null;

				return new SimpleRange(start, end);
			}

			/// <summary>
			/// Subtracts <paramref name="r2"/> from <paramref name="r1"/> =&gt; 0..2 intervals.
			/// </summary>
			public static SimpleRange[] Except(SimpleRange r1, SimpleRange? r2)
			{
				// If r2 is null or disjoint => keep r1 entirely
				if (r2 is null || Intersect(r1, r2) is null)
				{
					return [r1];
				}

				// If r2 fully covers r1 => no remainder
				bool covers = Covers(r2.Value, r1);
				if (covers) return [];

				// Possibly we produce up to 2 intervals
				var results = new List<SimpleRange>();

				// 1) If r2 min is > r1 min => keep [r1.Min, r2.Min - 1] 
				// but handle infinite endpoints
				var leftPartMax = NullableIntEx.Decrement(r2.Value.Minimum);
				if (leftPartMax.Less(r1.Minimum))
				{
					// that sub-interval is empty
				}
				else
				{
					// we produce [r1.Min, leftPartMax]
					var leftRange = new SimpleRange(r1.Minimum, leftPartMax);
					// we also must ensure leftRange is valid => leftPartMax >= r1.Min
					SimpleRange? intersection = Intersect(leftRange, r1);
					if (intersection != null)
					{
						results.Add(intersection.Value);
					}
				}

				// 2) If r2 max is < r1 max => keep [r2.Max + 1, r1.Max]
				var rightPartMin = NullableIntEx.Increment(r2.Value.Maximum);
				if (r1.Maximum.Less(rightPartMin))
				{
					// no sub-interval
				}
				else
				{
					var rightRange = new SimpleRange(rightPartMin, r1.Maximum);
					SimpleRange? intersection = Intersect(rightRange, r1);
					if (intersection != null)
					{
						results.Add(intersection.Value);
					}
				}

				return [.. results];

				// local: checks if r1 covers r2 => i.e. r1's min <= r2.min, r1's max >= r2.max
				static bool Covers(SimpleRange r1, SimpleRange r2)
				{
					// r1.Min <= r2.Min && r1.Max >= r2.Max
					bool leftOk = NullableIntEx.LessOrEqual(r1.Minimum, r2.Minimum);
					bool rightOk = NullableIntEx.GreaterOrEqual(r1.Maximum, r2.Maximum);
					return leftOk && rightOk;
				}
			}

			#endregion

			#region Formatting

			public override readonly string ToString() => ToString(string.Empty, null);
			public readonly string ToString(string? format) => ToString(format, null);
			public readonly string ToString(IFormatProvider? formatProvider) => ToString(string.Empty, formatProvider);

			/// <summary>
			/// If both endpoints are null =&gt; "∞-∞",
			/// if only Minimum is null =&gt; "∞-X",
			/// if only Maximum is null =&gt; "X-∞",
			/// else "X-Y".
			/// If X == Y =&gt; still prints "X-Y" (you could special-case a single value).
			/// </summary>
			public readonly string ToString(string? format, IFormatProvider? formatProvider)
			{
				var fp = formatProvider as CultureInfo ?? CultureInfo.CurrentCulture;
				var minStr = Minimum.HasValue
					? Minimum.Value.ToString(format, fp)
					: "∞"; // -∞
				var maxStr = Maximum.HasValue
					? Maximum.Value.ToString(format, fp)
					: "∞"; // +∞
				if (minStr != "∞" && minStr == maxStr) return minStr;
				return $"{minStr}-{maxStr}";
			}

			#endregion

			public readonly bool EndsBefore(SimpleRange? b)
			{
				// a.Max < b.Min
				if (Maximum.HasValue && b.HasValue && b.Value.Minimum.HasValue)
					return Maximum.Value.CompareTo(b.Value.Minimum.Value) < 0;
				return false;
			}
			public readonly bool StartsAfter(SimpleRange? b)
			{
				// a.Min > b.Max
				if (Minimum.HasValue && b.HasValue && b.Value.Maximum.HasValue)
					return Minimum.Value.CompareTo(b.Value.Maximum.Value) > 0;
				return false;
			}

		}

		#endregion

		#region Fields & Constructors

		private readonly SortedSet<SimpleRange> _ranges = [];

		/// <summary>Creates an empty set of intervals.</summary>
		public IntRange() { }

		/// <summary>
		/// Creates a set of intervals from a string. 
		/// Expects forms like:
		///   "∞-∞" =&gt; all integers
		///   "∞-5" =&gt; [-∞..5]
		///   "5-∞" =&gt; [5..+∞]
		///   "2-5" =&gt; [2..5]
		///   "42"  =&gt; single value [42..42]
		/// And possibly multiple intervals separated by commas or semicolons, e.g. "∞-0, 5-10, 42".
		/// </summary>
		public IntRange(string input, TextInfo textInfo) : this(input, textInfo.ListSeparator) { }

		/// <summary>
		/// Creates a set of intervals from a string. 
		/// Expects forms like:
		///   "∞-∞" =&gt; all integers
		///   "∞-5" =&gt; [-∞..5]
		///   "5-∞" =&gt; [5..+∞]
		///   "2-5" =&gt; [2..5]
		///   "42"  =&gt; single value [42..42]
		/// And possibly multiple intervals separated by commas or semicolons, e.g. "∞-0, 5-10, 42".
		/// </summary>
		public IntRange(string input, CultureInfo cultureInfo) : this(input, cultureInfo.TextInfo.ListSeparator) { }

		/// <summary>
		/// Creates a set of intervals from a string. 
		/// Expects forms like:
		///   "∞-∞" =&gt; all integers
		///   "∞-5" =&gt; [-∞..5]
		///   "5-∞" =&gt; [5..+∞]
		///   "2-5" =&gt; [2..5]
		///   "42"  =&gt; single value [42..42]
		/// And possibly multiple intervals separated by commas or semicolons, e.g. "∞-0, 5-10, 42".
		/// </summary>
		public IntRange(string input, params string[] separators)
		{
			if (string.IsNullOrWhiteSpace(input))
				throw new ArgumentNullException(nameof(input), "Input cannot be null or empty.");

			if (separators == null || separators.Length == 0)
			{
				separators = [",", ";", "|"];
			}

			var sepPattern = string.Join("|", separators.Select(Regex.Escape));
			// Single value =>  (?<singleValue>(∞|\d+))
			// Range =>        (?<start>(∞|\d+))-(?<end>(∞|\d+))
			var pattern = $@"((?<singleValue>(inf|∞|\d+))|(?<start>(inf|∞|\d+))-(?<end>(inf|∞|\d+)))({sepPattern}|$)";

			var matches = Regex.Matches(input, pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture);
			var list = new List<SimpleRange>();

			foreach (Match m in matches)
			{
				if (m.Groups["singleValue"].Success && !m.Groups["start"].Success)
				{
					// single value => "[X..X]"
					T? val = NullableIntEx.Parse<T>(m.Groups["singleValue"].Value);
					list.Add(new SimpleRange(val, val));
				}
				else
				{
					// range
					var sVal = m.Groups["start"].Value;
					var eVal = m.Groups["end"].Value;
					T? start = NullableIntEx.Parse<T>(sVal);
					T? end = NullableIntEx.Parse<T>(eVal);
					list.Add(new SimpleRange(start, end));
				}
			}

			InitRanges(list);
		}


		#endregion

		#region Internal Range Normalization

		/// <summary>
		/// Takes a collection of possibly overlapping/adjacent ranges, merges them, 
		/// and stores as a sorted set of disjoint intervals.
		/// </summary>
		private void InitRanges(IEnumerable<SimpleRange> intervals)
		{
			SimpleRange? current = null;

			// Sort the intervals by CompareTo
			foreach (var range in intervals.OrderBy(r => r))
			{
				var merged = SimpleRange.Union(current, range);
				if (merged == null)
				{
					// not mergeable => store current, move on
					if (current != null)
						_ranges.Add(current.Value);
					current = range;
				}
				else
				{
					// update current to the union
					current = merged;
				}
			}
			if (current != null)
				_ranges.Add(current.Value);
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Returns an IntRange representing all integers from -∞ to +∞.
		/// (A single interval with null-min, null-max).
		/// </summary>
		public static IntRange<T> FullRange
		{
			get {
				var r = new IntRange<T>();
				r._ranges.Add(new SimpleRange(null, null)); // [∞..∞]
				return r;
			}
		}

		/// <summary>
		/// Checks whether an integer value is contained in any of the intervals.
		/// </summary>
		public bool Contains(T value)
		{
			// Because we store sorted intervals,
			// we can break early once we pass possible intervals.
			foreach (var range in _ranges)
			{
				// If rng.Minimum is null => -∞ => definitely <= value
				// If rng.Maximum is null => +∞ => definitely >= value
				// We'll do a small function that checks membership
				if (InRange(range, value))
					return true;

				// If the value is less than rng.Minimum, no further intervals can contain it
				if (NullableIntEx.Less(value, range.Minimum))
					return false;
			}
			return false;

			static bool InRange(SimpleRange r, T val)
			{
				// r.Min <= val <= r.Max
				if (r.Minimum.HasValue && val.CompareTo(r.Minimum.Value) < 0) return false;
				if (r.Maximum.HasValue && val.CompareTo(r.Maximum.Value) > 0) return false;
				return true;
			}

		}

		/// <summary>
		/// Produces the union of two IntRange sets (|).
		/// </summary>
		public static IntRange<T> Union(IntRange<T> left, IntRange<T> right)
		{
			var list = new List<SimpleRange>();
			// merge-lists approach
			using var e1 = left._ranges.GetEnumerator();
			using var e2 = right._ranges.GetEnumerator();

			bool has1 = e1.MoveNext();
			bool has2 = e2.MoveNext();

			while (has1 && has2)
			{
				if (e1.Current.CompareTo(e2.Current) < 0)
				{
					list.Add(e1.Current);
					has1 = e1.MoveNext();
				}
				else
				{
					list.Add(e2.Current);
					has2 = e2.MoveNext();
				}
			}
			while (has1)
			{
				list.Add(e1.Current);
				has1 = e1.MoveNext();
			}
			while (has2)
			{
				list.Add(e2.Current);
				has2 = e2.MoveNext();
			}

			var result = new IntRange<T>();
			result.InitRanges(list);
			return result;
		}

		/// <summary>
		/// Produces the intersection of two IntRange sets (&amp;).
		/// </summary>
		public static IntRange<T> Intersect(IntRange<T> left, IntRange<T> right)
		{
			var output = new List<SimpleRange>();
			using var e2 = right._ranges.GetEnumerator();
			bool has2 = e2.MoveNext();

			foreach (var r1 in left._ranges)
			{
				var current = r1;

				while (has2)
				{
					// skip e2's that end before r1 starts
					if (EndsBefore(e2.Current, current))
					{
						has2 = e2.MoveNext();
						continue;
					}
					// if e2 starts after r1 ends => no overlap
					if (StartsAfter(e2.Current, current))
					{
						break;
					}

					// try intersect
					var intersection = SimpleRange.Intersect(current, e2.Current);
					if (intersection != null)
						output.Add(intersection.Value);

					// if e2 extends beyond current => done with this r1
					// else move e2
					if (ExtentGreater(e2.Current, current)) break;
					has2 = e2.MoveNext();
				}
			}

			var ret = new IntRange<T>();
			ret.InitRanges(output);
			return ret;

			static bool EndsBefore(SimpleRange a, SimpleRange b)
			{
				// a.Max < b.Min
				// if a.Max is finite, b.Min is finite
				if (a.Maximum.HasValue && b.Minimum.HasValue)
					return a.Maximum.Value < b.Minimum.Value;
				// if a.Max is null => +∞ => not "ends before"
				// if b.Min is null => -∞ => "endsBefore"? 
				//   Actually that means b starts at -∞ => a never ends before that 
				return false;
			}

			static bool StartsAfter(SimpleRange a, SimpleRange b)
			{
				// a.Min > b.Max
				if (a.Minimum.HasValue && b.Maximum.HasValue)
					return a.Minimum.Value > b.Maximum.Value;
				// if a.Min is null => -∞ => can't start after 
				return false;
			}

			static bool ExtentGreater(SimpleRange a, SimpleRange b)
			{
				// a.Maximum >= b.Maximum
				// if a.Max is null => +∞ => definitely >= 
				// else compare 
				if (!a.Maximum.HasValue)
					return true;
				if (!b.Maximum.HasValue)
					return false;
				return a.Maximum.Value >= b.Maximum.Value;
			}
		}

		/// <summary>
		/// Produces the difference of two sets: all values in <paramref name="left"/> that are not in <paramref name="right"/>.
		/// </summary>
		public static IntRange<T> Except(IntRange<T> left, IntRange<T> right)
		{
			var diffList = new List<SimpleRange>();
			using var e2 = right._ranges.GetEnumerator();
			bool has2 = e2.MoveNext();

			foreach (var r1 in left._ranges)
			{
				SimpleRange? current = r1;

				while (has2)
				{
					// skip right ranges that end before 'current' starts
					if (e2.Current.EndsBefore(current))
					{
						has2 = e2.MoveNext();
						continue;
					}
					// if right starts after 'current' ends => done with this
					if (e2.Current.StartsAfter(current))
					{
						break;
					}

					var exception = SimpleRange.Except(current.Value, e2.Current);
					if (exception.Length == 0)
					{
						current = null;
						break;
					}
					// the first piece is guaranteed non-overlapping
					diffList.Add(exception[0]);

					if (exception.Length == 1)
					{
						// no second piece
						current = null;
						break;
					}
					else
					{
						current = exception[1];
					}
				}
				if (current is not null) diffList.Add(current.Value);
			}

			var ret = new IntRange<T>();
			ret.InitRanges(diffList);
			return ret;

		}

		/// <summary>
		/// Symmetric difference =&gt; (A \ B) ∪ (B \ A).
		/// </summary>
		public static IntRange<T> SymmetricDifference(IntRange<T> left, IntRange<T> right)
		{
			return Union(Except(left, right), Except(right, left));
		}

		/// <summary>
		/// Computes the complement with respect to the full integer domain (-∞ to +∞).
		/// That is, all values in [-∞..+∞] that are *not* in this <see cref="IntRange"/>.
		/// </summary>
		public IntRange<T> Complement()
		{
			// FullRange = [∞..∞] => entire domain
			return Except(FullRange, this);
		}

		#endregion

		#region Operators (|, &, ^, ~)

		/// <summary>Bitwise OR =&gt; Union</summary>
		public static IntRange<T> operator |(IntRange<T> left, IntRange<T> right)
			=> Union(left, right);

		/// <summary>Bitwise AND =&gt; Intersection</summary>
		public static IntRange<T> operator &(IntRange<T> left, IntRange<T> right)
			=> Intersect(left, right);

		/// <summary>Bitwise XOR =&gt; Symmetric Difference</summary>
		public static IntRange<T> operator ^(IntRange<T> left, IntRange<T> right)
			=> SymmetricDifference(left, right);

		/// <summary>Substraction =&gt; Except</summary>
		public static IntRange<T> operator -(IntRange<T> left, IntRange<T> right)
			=> Except(left, right);

		/// <summary>Bitwise NOT =&gt; Complement (everything in [-∞..+∞] not in this range).</summary>
		public static IntRange<T> operator ~(IntRange<T> range)
			=> range.Complement();

		#endregion

		#region IEnumerable<int>, IFormattable

		/// <summary>
		/// Enumerates *all integers* in the stored intervals.
		/// If you have intervals that go to ±∞, enumerating them is infinite!
		/// For safety, you might want to throw if the range is unbounded.
		/// </summary>
		public IEnumerator<T> GetEnumerator()
		{
			foreach (var rng in _ranges)
			{
				// If unbounded on the left => infinite sequence
				if (!rng.Minimum.HasValue)
					throw new InvalidOperationException("Cannot enumerate from -∞.");

				var start = rng.Minimum.Value;

				// If unbounded on the right => infinite loop
				if (!rng.Maximum.HasValue)
					throw new InvalidOperationException("Cannot enumerate to +∞.");

				var end = rng.Maximum.Value;
				for (T i = start; i <= end; i++)
				{
					yield return i;
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public override string ToString() => ToString(null, null);
		public string ToString(string? format) => ToString(format, null);
		public string ToString(IFormatProvider formatProvider) => ToString(null, formatProvider);

		/// <summary>
		/// Joins each range with the current culture's list separator (or the formatProvider's culture).
		/// Each range is printed as "∞-∞", "∞-5", "5-∞", etc.
		/// </summary>
		public string ToString(string format, IFormatProvider formatProvider)
		{
			var culture = formatProvider as CultureInfo ?? CultureInfo.CurrentCulture;
			var sep = culture.TextInfo.ListSeparator;
			return string.Join(sep, _ranges.Select(r => r.ToString(format, culture)));
		}

		#endregion
	}
}

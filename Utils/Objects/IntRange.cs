using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Utils.Mathematics;
using System.Collections;
using System.Numerics;

namespace Utils.Objects;

public class IntRange : 
	IEnumerable<int>, 
	IFormattable,
	IAdditionOperators<IntRange, IntRange, IntRange>,
	ISubtractionOperators<IntRange, IntRange, IntRange>
{
	private class SimpleRange : IComparable<SimpleRange>, IComparable<int>, IFormattable
	{
		public SimpleRange(int minimum, int maximum)
		{
			Minimum = minimum;
			Maximum = maximum;
		}

		public int Minimum { get; }
		public int Maximum { get; }

		public int CompareTo(SimpleRange other)
		{
			if (other is null) return -1;
			return new[] {
					Minimum.CompareTo(other.Minimum),
					Maximum.CompareTo(other.Maximum)
				}.FirstOrDefault(n => n != 0);
		}

		public int CompareTo(int other)
		{
			if (other < Minimum) return 1;
			if (other > Maximum) return -1;
			return 0;
		}

		public static bool CanMerge(SimpleRange range1, SimpleRange range2)
		{
			if (range1 is null || range2 is null) return false;
			return range1.Minimum <= range2.Maximum + 1 && range2.Minimum <= range1.Maximum + 1;
		}

		public static SimpleRange Merge(SimpleRange range1, SimpleRange range2)
		{
			if (range1 is null) return range2;
			if (range2 is null) return range1;
			if (!CanMerge(range1, range2)) return null;
			return new SimpleRange(MathEx.Min(range1.Minimum, range2.Minimum), MathEx.Max(range1.Maximum, range2.Maximum));
		}

		public static SimpleRange[] Except(SimpleRange range1, SimpleRange range2)
		{
			if (range2 is null || range2.Minimum > range1.Maximum || range2.Maximum < range1.Minimum) return [new SimpleRange(range1.Minimum, range1.Maximum)];
			if (range2.Minimum <= range1.Minimum && range2.Maximum >= range1.Maximum) return [];
			if (range2.Maximum >= range1.Maximum) return [new SimpleRange(range1.Minimum, range2.Minimum - 1)];
			if (range2.Minimum <= range1.Minimum) return [new SimpleRange(range2.Maximum + 1, range1.Maximum)];
			return [
					new SimpleRange(range1.Minimum, range2.Minimum - 1),
					new SimpleRange(range2.Maximum + 1, range1.Maximum)
				];
		}

		public override string ToString() => ToString(string.Empty, null);
		public string ToString(string format) => ToString(format, null);
		public string ToString(IFormatProvider formatProvider) => ToString(string.Empty, formatProvider);

		public string ToString(string format, IFormatProvider formatProvider)
		{
			if (format.IsNullOrWhiteSpace())
			{
				return Minimum == Maximum ? Minimum.ToString() : $"{Minimum}-{Maximum}";
			}
			else
			{
				return Minimum == Maximum ? Minimum.ToString(format, formatProvider) : Minimum.ToString(format, formatProvider) + "-" + Maximum.ToString(format, formatProvider);
			}
		}
	}

	private readonly SortedSet<SimpleRange> ranges = new SortedSet<SimpleRange>();

	public IntRange() { }

	public IntRange(string ranges) : this(ranges,
			System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator,
			System.Globalization.CultureInfo.InvariantCulture.TextInfo.ListSeparator)
	{ }

	public IntRange(string ranges, System.Globalization.CultureInfo cultureInfo) : this(ranges, cultureInfo.TextInfo.ListSeparator) { }
	public IntRange(string ranges, System.Globalization.TextInfo textInfo) : this(ranges, textInfo.ListSeparator) { }

	public IntRange(string ranges, params string[] separators)
	{
		ranges.ArgMustNotBeNull();
		SortedSet<SimpleRange> result = new SortedSet<SimpleRange>();
		var matches = Regex.Matches(ranges, @"((?<singleValue>\d+)|(?<start>\d+)-(?<end>\d+))(" + string.Join("|", separators) + "|$)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
		foreach (Match match in matches)
		{
			if (match.Groups["singleValue"].Success)
			{
				var value = int.Parse(match.Groups["singleValue"].Value);
				result.Add(new SimpleRange(value, value));
			}
			else
			{
				var start = int.Parse(match.Groups["start"].Value);
				var end = int.Parse(match.Groups["end"].Value);
				result.Add(new SimpleRange(start, end));
			}
		}
		InitRanges(result);
	}

	private void InitRanges(IEnumerable<SimpleRange> result)
	{
		result.ArgMustNotBeNull();
		SimpleRange current = null;
		foreach (var simpleRange in result)
		{
			var newRange = SimpleRange.Merge(current, simpleRange);
			if (newRange is null)
			{
				ranges.Add(current);
				current = simpleRange;
			}
			else
			{
				current = newRange;
			}
		}
		if (current is not null) ranges.Add(current);
	}

	public bool Contains(int value)
	{
		foreach (var range in ranges)
		{
			var comparison = range.CompareTo(value);
			if (comparison == 0) return true;
			if (comparison == 1) return false;
		}
		return false;
	}

	public IEnumerator<int> GetEnumerator()
	{
        static IEnumerable<int> getValues(IEnumerable<SimpleRange> ranges)
		{
			foreach (var range in ranges)
			{
				for (int i = range.Minimum; i <= range.Maximum; i++)
				{
					yield return i;
				}
			}
		}

		return getValues(this.ranges).GetEnumerator();

	}

	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	public override string ToString() => ToString(string.Empty, null);
	public string ToString(string format) => ToString(format, null);
	public string ToString(IFormatProvider formatProvider) => ToString(string.Empty, formatProvider);

	public string ToString(string format, IFormatProvider formatProvider)
	{
		var cultureInfo = formatProvider as System.Globalization.CultureInfo ?? System.Globalization.CultureInfo.CurrentCulture;
		return string.Join(cultureInfo.TextInfo.ListSeparator, ranges.Select(r => r.ToString(format, cultureInfo)));
	}

	public static IntRange operator +(IntRange range1, IntRange range2)
	{
		var result = new IntRange();
		var list = new List<SimpleRange>();
		var enum1 = range1.ranges.GetEnumerator();
		var enum2 = range2.ranges.GetEnumerator();
		enum1.MoveNext();
		enum2.MoveNext();
		while (true)
		{
			var c = enum1.Current?.CompareTo(enum2.Current) ?? 1;
			if (c < 0)
			{
				list.Add(enum1.Current);
				if (!enum1.MoveNext())
				{
					list.Add(enum2.Current);
					while (enum2.MoveNext())
					{
						list.Add(enum2.Current);
					}
					break;
				}
			}
			else
			{
				list.Add(enum2.Current);
				if (!enum2.MoveNext())
				{
					list.Add(enum1.Current);
					while (enum1.MoveNext())
					{
						list.Add(enum1.Current);
					}
					break;
				}
			}
		}
		result.InitRanges(list);
		return result;
	}

	public static IntRange operator -(IntRange range1, IntRange range2)
	{
		var ranges = new List<SimpleRange>();

		var enum2 = range2.ranges.GetEnumerator();
		enum2.MoveNext();
		bool find = true;
		foreach (var simpleRange in range1.ranges)
		{
			if (find)
			{
				var current = simpleRange;
				while (current is not null)
				{
					while ((enum2.Current?.Maximum ?? int.MaxValue) < current.Minimum)
					{
						find = enum2.MoveNext();
						if (!find) break;
					}
					var excepted = SimpleRange.Except(current, enum2.Current);
					if (excepted.Length == 0) break;
					ranges.Add(excepted[0]);
					if (excepted.Length == 1) break;
					current = excepted[1];
				}
			}
			else
			{
				ranges.Add(new SimpleRange(simpleRange.Minimum, simpleRange.Maximum));
			}
		}
		var result = new IntRange();
		result.InitRanges(ranges);
		return result;
	}
}

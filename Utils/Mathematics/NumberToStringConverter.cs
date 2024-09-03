using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Objects;

namespace Utils.Mathematics
{
	/// <summary>
	/// Provides functionality to convert numbers to their string representation according to a specific culture or custom configuration.
	/// </summary>
	public partial class NumberToStringConverter : INumberToStringConverter
	{

		/// <summary>
		/// Retrieves a number-to-string converter for the specified culture.
		/// </summary>
		/// <param name="culture">The culture to retrieve the converter for.</param>
		/// <returns>The corresponding NumberToStringConverter instance.</returns>
		public static NumberToStringConverter GetConverter(CultureInfo culture) => GetConverter(culture.Name);

		/// <summary>
		/// Retrieves a number-to-string converter for the specified culture name.
		/// </summary>
		/// <param name="culture">The name of the culture to retrieve the converter for.</param>
		/// <returns>The corresponding NumberToStringConverter instance.</returns>
		public static NumberToStringConverter GetConverter(string culture)
		{
			culture.Length.ArgMustBeIn([2, 5]);  // Ensure culture code length is valid.

			if (configurations.TryGetValue(culture, out var result)) return result;
			if (culture.Length == 5) return GetConverter(culture[..2]);  // Fallback to the language-only code if region-specific code is not found.
			return configurations["EN"];  // Default to English converter.
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NumberToStringConverter"/> class with the specified configuration.
		/// </summary>
		public NumberToStringConverter(
			int group,
			string separator,
			string groupSeparator,
			string zero,
			string minus,
			IReadOnlyDictionary<int, DigitListType> groups,
			IReadOnlyDictionary<long, string> exceptions,
			IReadOnlyDictionary<string, string> replacements,
			NumberScale scale,
			Func<string, string> adjustFunction = null)
		{
			Group = group;
			Separator = separator ?? " ";
			GroupSeparator = groupSeparator ?? "";
			Zero = zero.ArgMustNotBeNull();
			Minus = minus.ArgMustNotBeNull();
			Groups = groups.ArgMustNotBeNull().ToImmutableDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<long, DigitType>)kv.Value.Digits.ToDictionary(d => (long)d.Digit).ToImmutableDictionary());
			Exceptions = exceptions.ArgMustNotBeNull().ToImmutableDictionary();
			Replacements = replacements?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty;
			Scale = scale;
			AdjustFunction = adjustFunction ?? (s => s);
		}

		/// <summary>
		/// Default grouping size (e.g., thousands)
		/// </summary>
		public int Group { get; } = 3;
		/// <summary>
		/// Separator between groups of digits
		/// </summary>
		public string Separator { get; }
		/// <summary>
		/// Separator between different groups (e.g., thousands, millions)
		/// </summary>
		public string GroupSeparator { get; }
		/// <summary>
		/// String representation of zero
		/// </summary>
		public string Zero { get; }
		/// <summary>
		/// String representation of the minus sign
		/// </summary>
		public string Minus { get; }
		/// <summary>
		/// Function to adjust the final output string
		/// </summary>
		public Func<string, string> AdjustFunction { get; }
		/// <summary>
		/// Special cases for specific numbers
		/// </summary>
		public IReadOnlyDictionary<long, string> Exceptions { get; }
		/// <summary>
		/// Replacements for specific text segments
		/// </summary>
		public IReadOnlyDictionary<string, string> Replacements { get; }
		/// <summary>
		/// Group definitions for digits
		/// </summary>
		public IReadOnlyDictionary<int, IReadOnlyDictionary<long, DigitType>> Groups { get; }
		/// <summary>
		/// Scale definition for large numbers
		/// </summary>
		public NumberScale Scale { get; }

		/// <summary>
		/// Converts an integer to its string representation.
		/// </summary>
		public string Convert(int number) => Convert((BigInteger)number);

		/// <summary>
		/// Converts a long integer to its string representation.
		/// </summary>
		public string Convert(long number) => Convert((BigInteger)number);

		/// <summary>
		/// Converts a BigInteger to its string representation.
		/// </summary>
		public string Convert(BigInteger number)
		{
			if (number == 0) return Zero;

			// Check for exceptions
			if (number.Between(long.MinValue, long.MaxValue) && Exceptions.TryGetValue((long)number, out var value))
			{
				return AdjustFunction(value);
			}

			var maxGroup = Groups.Keys.Max();
			var groupValue = BigInteger.Pow(10, maxGroup);

			bool isNegative = number.Sign == -1;
			if (isNegative) number = BigInteger.Abs(number);

			int groupNumber = 0;
			var groupsValues = new Stack<string>();

			// Group the number
			while (number != 0)
			{
				var group = (long)(number % groupValue);
				if (group != 0)
				{
					string resValue = ConvertGroup(maxGroup, group) + Separator + Scale.GetScaleName(groupNumber).ToPlural(group);
					if (Replacements.TryGetValue(resValue, out var replacement)) resValue = replacement;
					groupsValues.Push(resValue.Trim());
				}
				number /= groupValue;
				groupNumber++;
			}

			// Build the final string
			var result = new StringBuilder();
			while (groupsValues.Count > 0)
			{
				result.Append(groupsValues.Pop().Trim()).Append(GroupSeparator).Append(Separator);
			}

			var finalResult = result.ToString().TrimEnd(GroupSeparator.ToCharArray().Union(Separator.ToCharArray()).ToArray());
			finalResult = isNegative ? Minus.Replace("*", finalResult) : AdjustFunction(finalResult);
			return AdjustFunction(finalResult);

		}

		/// <summary>
		/// Converts a group of digits to its string representation based on its group number.
		/// </summary>
		public string ConvertGroup(int groupNumber, long number)
		{
			if (groupNumber == 0) return string.Empty;
			if (groupNumber > 1 && Exceptions.TryGetValue(number, out var value)) return value;

			long group = (long)Math.Pow(10, groupNumber - 1);
			var (groupValue, remainder) = long.DivRem(number, group);

			var leftText = ConvertGroup(groupNumber - 1, remainder);
			var valueText = Groups[groupNumber][groupValue];

			return string.IsNullOrEmpty(leftText) ? valueText.StringValue : valueText.BuildString.Replace("*", leftText);
		}
	}

	/// <summary>
	/// Represents the scale used for large number names (e.g., thousand, million).
	/// </summary>
	public class NumberScale
	{
		public NumberScale(
			IReadOnlyList<string> staticValues,
			IReadOnlyList<string> scaleSuffixes,
			int startIndex = 0,
			string voidGroup = "ni",
			string groupSeparator = "lli",
			IReadOnlyList<string> scale0Prefixes = null,
			IReadOnlyList<string> unitsPrefixes = null,
			IReadOnlyList<string> tensPrefixes = null,
			IReadOnlyList<string> hundredsPrefixes = null,
			bool firstLetterUppercase = false)
		{
			StaticValues = staticValues.ArgMustNotBeNull().ToImmutableArray();
			ScaleSuffixes = scaleSuffixes.ArgMustNotBeNull().ToImmutableArray();
			StartIndex = startIndex;
			FirstLetterUppercase = firstLetterUppercase;

			VoidGroup = voidGroup.NotNullOrEmptyOrDefault("ni");
			GroupSeparator = groupSeparator.NotNullOrEmptyOrDefault("lli");

			Scale0Prefixes = scale0Prefixes?.ToImmutableArray() ?? Scale0Prefixes;
			UnitsPrefixes = unitsPrefixes?.ToImmutableArray() ?? UnitsPrefixes;
			TensPrefixes = tensPrefixes?.ToImmutableArray() ?? TensPrefixes;
			HundredsPrefixes = hundredsPrefixes?.ToImmutableArray() ?? HundredsPrefixes;
		}

		public IReadOnlyList<string> StaticValues { get; }
		public IReadOnlyList<string> ScaleSuffixes { get; }
		public int StartIndex { get; }
		public bool FirstLetterUppercase { get; }

		private readonly string VoidGroup;
		private readonly string GroupSeparator;

		private static readonly Regex PrefixParser = new(@"(\((?<start>\w+)\))?(?<value>\w+)(\((?<end>\w+)\))?", RegexOptions.Compiled);

		public IReadOnlyList<string> Scale0Prefixes { get; } = [
			"",
			"mi",
			"bi",
			"tri",
			"quadri",
			"quinti",
			"sexti",
			"septi",
			"octi",
			"noni"
		];


		public IReadOnlyList<string> UnitsPrefixes { get; } = [
			"",
			"uni",
			"duo",
			"tre(s)",
			"quattuor",
			"quinqua",
			"se(xs)",
			"septe(mn)",
			"octo",
			"nove(mn)"
		];

		public IReadOnlyList<string> TensPrefixes { get; } = [
			"",
			"(n)deci",
			"(ms)vingti",
			"(ns)triginta",
			"(ns)quadraginta",
			"(ns)quinquaginta",
			"(n)sexaginta",
			"(n)septuaginta",
			"(mxs)octoginta",
			"nonaginta"
		];

		public IReadOnlyList<string> HundredsPrefixes { get; } = [
			"",
			"(nx)centi",
			"(ms)ducenti",
			"(ns)trecenti",
			"(ns)quadringenti",
			"(ns)quingenti",
			"(n)sescenti",
			"(n)septingenti",
			"(mxs)octingenti",
			"nongenti"
		];


		/// <summary>
		/// Retrieves the name of the scale for a given power of 10.
		/// </summary>
		public string GetScaleName(int scale)
		{
			if (scale < StaticValues.Count) return StaticValues[scale];

			scale -= StaticValues.Count;
			scale += StartIndex;
			var result = int.DivRem(scale, ScaleSuffixes.Count);

			var suffix = ScaleSuffixes[result.Remainder];
			var prefix = result.Quotient + 1;

			if (prefix.Between(0, 9))
			{
				var value = Scale0Prefixes[prefix] + GroupSeparator + suffix;
				return FirstLetterUppercase ? char.ToUpper(value[0]) + value[1..] : value;
			}

			var prefixes = new List<string>();

			while (prefix > 0)
			{
				(prefix, int u) = int.DivRem(prefix, 10);
				(prefix, int t) = int.DivRem(prefix, 10);
				(prefix, int h) = int.DivRem(prefix, 10);

				if (h == 0 && t == 0 && u == 0)
				{
					prefixes.Add(VoidGroup);
					continue;
				}

				Match[] groupValues = [
					PrefixParser.Match(HundredsPrefixes[h]),
					PrefixParser.Match(TensPrefixes[t]),
					PrefixParser.Match(UnitsPrefixes[u])
				];

				var value = new StringBuilder();
				string start = "", end = "";

				foreach (Match match in groupValues)
				{
					if (match.Success)
					{
						end = match.Groups["end"].Value;

						if (!start.IsNullOrEmpty())
						{
							foreach (var s in end)
							{
								if (start.Contains(s)) value.Insert(0, s);
							}
						}
						value.Insert(0, match.Groups["value"].Value);
						start = match.Groups["start"].Value;
					}
				}

				if (FirstLetterUppercase && value.Length > 0)
				{
					value[0] = char.ToUpper(value[0]);
				}
				prefixes.Add(value.ToString());
			}

			return string.Join(GroupSeparator, prefixes.AsEnumerable().Reverse()) + GroupSeparator + suffix;
		}
	}
}

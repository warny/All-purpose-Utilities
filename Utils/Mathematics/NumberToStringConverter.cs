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
	public partial class NumberToStringConverter : INumberToStringConverter
	{
		public static NumberToStringConverter GetConverter(CultureInfo culture) => GetConverter(culture.Name);

		public static NumberToStringConverter GetConverter(string culture)
		{
			culture.Length.ArgMustBeIn([2, 5]);

			if (configurations.TryGetValue(culture, out var result)) return result;
			if (culture.Length == 5) return GetConverter(culture[0..2]);
			return configurations["EN"];
		}



		public NumberToStringConverter(int group, string separator, string groupSeparator, string zero, string minus, IReadOnlyDictionary<int, DigitListType> groups, IReadOnlyDictionary<long, string> exceptions, IReadOnlyDictionary<string, string> replacements, NumberScale scale, Func<string, string> adjustFunction = null)
		{
			Group = group;
			Separator = separator ?? " ";
			GroupSeparator = groupSeparator ?? "";
			Zero = zero.ArgMustNotBeNull();
			Minus = minus.ArgMustNotBeNull();
			Groups = groups.ArgMustNotBeNull().ToImmutableDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<long, DigitType>)kv.Value.Digits.ToDictionary(d => (long)d.Digit).ToImmutableDictionary());
			Exceptions = exceptions.ArgMustNotBeNull().ToImmutableDictionary();
			Replacements = replacements ?? new Dictionary<string, string>().ToImmutableDictionary();
			Scale = scale;
			AdjustFunction = adjustFunction ?? new(s => s);
		}

		public int Group { get; } = 3;
		public string Separator { get; }
		public string GroupSeparator { get; }
		public string Zero { get; }
		public string Minus { get; }

		public Func<string, string> AdjustFunction { get; }
		public IReadOnlyDictionary<long, string> Exceptions { get; }
		public IReadOnlyDictionary<string, string> Replacements { get; }
		public IReadOnlyDictionary<int, IReadOnlyDictionary<long, DigitType>> Groups { get; }

		public NumberScale Scale { get; }

		public string Convert(int number) => Convert((BigInteger)number);
		public string Convert(long number) => Convert((BigInteger)number);

		public string Convert(BigInteger number)
		{
			if (number == 0) { return Zero; }

			if (number.Between(long.MinValue, long.MaxValue) && Exceptions.TryGetValue((long)number, out var value))
			{
				return AdjustFunction(value);
			}

			var maxGroup = Groups.Keys.Max();
			var groupValue = BigInteger.Pow(10, maxGroup);

			bool isNegative = number.Sign == -1;
			if (isNegative) { number = BigInteger.Abs(number); }

			int groupNumber = 0;
			var groupsValues = new Stack<string>();

			while (number != 0)
			{
				var group = (long)(number % groupValue);
				if (group != 0)
				{
					string resValue = ConvertGroup(maxGroup, group) + Separator + Scale.GetScaleName(groupNumber).ToPlural(group);
					if (Replacements.TryGetValue(resValue, out var replacement)) { resValue = replacement; }
					groupsValues.Push(resValue.Trim());
				}
				number /= groupValue;
				groupNumber++;
			}

			var result = new StringBuilder();
			while (groupsValues.Count > 0)
			{
				result.Append(groupsValues.Pop().Trim());
				result.Append(GroupSeparator);
				result.Append(Separator);
			}

			var v = result.ToString().TrimEnd([.. GroupSeparator, .. Separator]);
			v = isNegative ? Minus.Replace("*", v) : v;

			return AdjustFunction(v);
		}


		public string ConvertGroup(int groupNumber, long number)
		{
			if (groupNumber == 0) { return string.Empty; }
			if (groupNumber > 1 && Exceptions.TryGetValue(number, out var value))
			{
				return value;
			}

			long group = (long)Math.Pow(10, groupNumber - 1);
			var (groupValue, remainder) = long.DivRem(number, group);

			var leftText = ConvertGroup(groupNumber - 1, remainder);
			var valueText = Groups[groupNumber][groupValue];

			return leftText.Length > 0 ? valueText.BuildString.Replace("*", leftText) : valueText.StringValue;
		}
	}

	public class NumberScale {
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
			staticValues.ArgMustNotBeNull();
			scaleSuffixes.ArgMustNotBeNull();
			scaleSuffixes.ArgMustNotBeEmpty();

            StaticValues = staticValues.ToImmutableArray();
			ScaleSuffixes = scaleSuffixes.ToImmutableArray();
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

		private static readonly Regex prefixParser = new(@"(\((?<start>\w+)\))?(?<value>\w+)(\((?<end>\w+)\))?", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture);

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


        public string GetScaleName(int scale)
        {
            if (scale < StaticValues.Count)
            {
                return StaticValues[scale];
            }

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
                (prefix, int u) = Math.DivRem(prefix, 10);
                (prefix, int t) = Math.DivRem(prefix, 10);
                (prefix, int h) = Math.DivRem(prefix, 10);

                if (h == 0 && t == 0 && u == 0)
                {
                    prefixes.Add(VoidGroup);
                    continue;
                }

                Match[] groupValues =
                {
                    prefixParser.Match(HundredsPrefixes[h]),
                    prefixParser.Match(TensPrefixes[t]),
                    prefixParser.Match(UnitsPrefixes[u])
                };

                string value = "";
                string start = "", end = "";

                foreach (Match match in groupValues)
                {
                    if (match.Success)
                    {
                        end = match.Groups["end"].Value;

                        if (start != "")
                        {
                            foreach (var s in end)
                            {
                                if (start.Contains(s)) value = s + value;
                            }
                        }
                        value = match.Groups["value"].Value + value;
                        start = match.Groups["start"].Value;
                    }
                }

                if (FirstLetterUppercase) value = char.ToUpper(value[0]) + value[1..];
                prefixes.Add(value);
            }

            return string.Join(GroupSeparator, prefixes.AsEnumerable().Reverse()) + GroupSeparator + suffix;
        }
    }
}

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
	public partial class NumberToStringConverter
	{
		public NumberToStringConverter(int group, string separator, string groupSeparator, string zero, string minus, Dictionary<int, Dictionary<long, string[]>> groups, IReadOnlyDictionary<long, string> exceptions, IReadOnlyDictionary<string, string> replacements, NumberScale scale, Func<string, string> adjustFunction = null)
		{
			Group = group;
			Separator = separator ?? " ";
			GroupSeparator = groupSeparator ?? "";
			Zero = zero.ArgMustNotBeNull();
			Minus = minus.ArgMustNotBeNull();
			Groups = groups.ArgMustNotBeNull().ToImmutableDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<long, string[]>)kv.Value.ToImmutableDictionary());
			Exceptions = exceptions.ArgMustNotBeNull().ToImmutableDictionary();
			Replacements = replacements ?? new Dictionary<string,string>().ToImmutableDictionary();
			Scale = scale;
			AdjustFunction = adjustFunction ?? new (s=>s); 
		}

		public int Group { get; } = 3;
		public string Separator { get; }
		public string GroupSeparator { get; }
		public string Zero { get; }
		public string Minus { get; }

        public Func<string, string> AdjustFunction { get; }
        public IReadOnlyDictionary<long, string> Exceptions { get; }
		public IReadOnlyDictionary<string, string> Replacements { get; }
		public IReadOnlyDictionary<int, IReadOnlyDictionary<long, string[]>> Groups { get; }

		public NumberScale Scale { get; }

        public string Convert(int number) => Convert((BigInteger)number);
        public string Convert(long number) => Convert((BigInteger)number);

        public string Convert(BigInteger number)
        {
            if (number == 0) { return Zero; }
			if (number.Between(long.MinValue, long.MaxValue))
			{
                if (Exceptions.TryGetValue((long)number, out var value)) return AdjustFunction(value);
			}

            var maxGroup = Groups.Keys.Max();
            var groupValue = BigInteger.Pow(10, maxGroup);

            bool isNegative = number < 0;
            Stack<string> groupsValues = new();
            if (isNegative) { number = -number; }

            int groupNumber = 0;
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

            StringBuilder result = new StringBuilder();
            while (groupsValues.Count > 0)
            {
                result.Append(groupsValues.Pop().Trim());
				result.Append(GroupSeparator);
                result.Append(Separator);
            }
            if (isNegative)
            {
                return AdjustFunction(Minus.Replace("*", result.ToString().Trim([.. Separator, .. GroupSeparator])));
            }
            else
            {
                return AdjustFunction(result.ToString().Trim([..Separator, ..GroupSeparator]));
            }
        }


        public string ConvertGroup(int groupNumber, long number)
		{
			if (groupNumber == 0) { return string.Empty; }
			if (groupNumber > 1 && Exceptions.TryGetValue(number, out var value))
			{
				return value;
			}

			long group = (long) Math.Pow(10, groupNumber - 1);
			(var groupValue, var remainder) = long.DivRem(number, group);

			var leftText = ConvertGroup(groupNumber - 1, remainder);
			var valueText = Groups[groupNumber][groupValue];
			if (leftText.Length > 0)
			{
				return valueText.First(v => v.Contains('*')).Replace("*", leftText);
			}
			else
			{
				return valueText.First(v => !v.Contains('*'));
			}
		}
	}

	public class NumberScale {
		public NumberScale(IReadOnlyList<string> staticValues, IReadOnlyList<string> scaleSuffixes, bool staticFirstLetterUpercase = false, bool firstLetterUppercase = false)
		{
			staticValues.ArgMustNotBeNull();
			scaleSuffixes.ArgMustNotBeNull();
			scaleSuffixes.ArgMustNotBeEmpty();

			StaticValues = staticValues.ToImmutableArray();
			ScaleSuffixes = scaleSuffixes.ToImmutableArray();
			FirstLetterUppercase = firstLetterUppercase;
			StaticFirstLetterUppercase = staticFirstLetterUpercase;
		}

        public IReadOnlyList<string> StaticValues { get; }
		public IReadOnlyList<string> ScaleSuffixes { get; }
		public bool FirstLetterUppercase { get; }
		public bool StaticFirstLetterUppercase { get; }


		private const string VoidGroup = "ni";
		private const string GroupSeparator = "lli";

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


		public IReadOnlyList<string> UnitsPrefixes { get; init; } = [
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

		public IReadOnlyList<string> TensPrefixes { get; init; } = [
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

        public IReadOnlyList<string> hundredPrefixes { get; init; } = [
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
				var value = StaticValues[scale];
                if (StaticFirstLetterUppercase) value = value[..1].ToUpper() + value[1..];
                return value;
			}
			scale -= StaticValues.Count;
            var result = int.DivRem(scale, ScaleSuffixes.Count);

			var suffix = ScaleSuffixes[result.Remainder];
			var prefix = result.Quotient + 1;

			if (prefix.Between(0, 9))
			{
				var value = Scale0Prefixes[prefix] + GroupSeparator + suffix;
                if (FirstLetterUppercase) value = value[..1].ToUpper() + value[1..];
                return value;
			}

			List<string> prefixes = [];

			while (prefix > 0)
			{
				(prefix, var u) = int.DivRem(prefix, 10);
                (prefix, var t) = int.DivRem(prefix, 10);
                (prefix, var h) = int.DivRem(prefix, 10);

				if (h == 0 && t == 0 && u == 0)
				{
					prefixes.Add(VoidGroup);
					continue;
				}

                Match[] groupValues = [
					prefixParser.Match(hundredPrefixes[h]),
					prefixParser.Match(TensPrefixes[t]),
					prefixParser.Match(UnitsPrefixes[u])
				];

				string value = "";
				string start = "", end = "";

				foreach (Match match in groupValues)
				{
					if (match.Value == null) continue;
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
				if (FirstLetterUppercase) value = value = value[..1].ToUpper() + value[1..];
                prefixes.Add(value);
            }
            return string.Join(GroupSeparator, Enumerable.Reverse(prefixes)) + GroupSeparator + suffix;
        }
	}
}

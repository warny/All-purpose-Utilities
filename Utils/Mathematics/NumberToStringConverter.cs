using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Objects;

namespace Utils.Mathematics
{
	public class NumberToStringConverter
	{
		public int Group { get; } = 3;

		public string Separator { get; } = " ";

		public string Zero { get; } = "zéro";

		public string Minus { get; } = "moins *";

		public Dictionary<long, string> Exceptions { get; } = new()
		{
			{ 1, "un" },
			{ 11, "onze" },
			{ 12, "douze" },
			{ 13, "treize" },
			{ 14, "quatorze" },
			{ 15, "quinze" },
			{ 16, "seize" },
			{ 71, "soixante onze" },
			{ 72, "soixante douze" },
			{ 73, "soixante treize" },
			{ 74, "soixante quatorze" },
			{ 75, "soixante quinze" },
			{ 76, "soixante seize" },
			{ 77, "soixante dix sept" },
			{ 78, "soixante dix huit" },
			{ 79, "soixante dix neuf" },
			{ 91, "quatre-vingt onze" },
			{ 92, "quatre-vingt douze" },
			{ 93, "quatre-vingt treize" },
			{ 94, "quatre-vingt quatorze" },
			{ 95, "quatre-vingt quinze" },
			{ 96, "quatre-vingt seize" },
			{ 97, "quatre-vingt dix sept" },
			{ 98, "quatre-vingt dix huit" },
			{ 99, "quatre-vingt dix neuf" },
		};

		public Dictionary<string, string> Replacements { get; } = new()
		{
			{ "un mille", "mille" }
		};

		public Dictionary<int, Dictionary<long, string[]>> Groups { get; } = new()
		{
			{ 1 ,
				new ()
				{
					{ 0, new [] { "" } },
					{ 1, new [] { "et un"} },
					{ 2, new [] { "deux"} },
					{ 3, new [] { "trois"} },
					{ 4, new [] { "quattre"} },
					{ 5, new [] { "cinq"} },
					{ 6, new [] { "six" } },
					{ 7, new [] { "sept" } },
					{ 8, new [] { "huit"} },
					{ 9, new [] { "neuf"} }
				}
			},
			{ 2,
				new () {
					{ 0, new [] { "", "*" } },
					{ 1, new [] { "dix", "dix *" } },
					{ 2, new [] { "vingt", "vingt *" } },
					{ 3, new [] { "trente", "trente *"} },
					{ 4, new [] { "quarante", "quarante *" } },
					{ 5, new [] { "cinquante", "cinquante *" } },
					{ 6, new [] { "soixante", "soixante *" } },
					{ 7, new [] { "soixante dix", "soixante dix *" } },
					{ 8, new [] { "quatre vingt", "quatre vingt *" } },
					{ 9, new [] { "quatre vingt dix", "quatre vingt dix *" } }
				}
			},
			{ 3,
				new ()
				{
					{ 0, new [] { "", "*" } },
					{ 1, new [] { "cent"         , "cent *"         } },
					{ 2, new [] { "deux cents"   , "deux cent *"   } },
					{ 3, new [] { "trois cents"  , "trois cent *"  } },
					{ 4, new [] { "quatre cents" , "quatre cent *" } },
					{ 5, new [] { "cinq cents"   , "cinq cent *"   } },
					{ 6, new [] { "six cents"    , "six cent *"    } },
					{ 7, new [] { "sept cents"   , "sept cent *"   } },
					{ 8, new [] { "huit cents"   , "huit cent *"   } },
					{ 9, new [] { "neuf cents"   , "neuf cent *"   } }
				}
			}
		};

		public string[] GroupTexts { get; } = new[]
		{
			"",
			"mille" ,
			"million(s)" ,
			"milliard(s)" ,
			"billion(s)",
			"billiard(s)",
			"trillion(s)",
			"trilliard(s)"
		};

		public string Convert(long number)
		{
			if (number == 0) { return Zero; }
			if (Exceptions.TryGetValue(number, out var value)) { return value; } 
			var maxGroup = Groups.Keys.Max();
			var groupValue = (long)Math.Pow(10, maxGroup);

			bool isNegative = number < 0;
			Stack<string> groupsValues = new Stack<string>();
			if (isNegative) { number = -number; }

			int groupNumber = 0;
			while (number != 0)
			{
				var group = number % groupValue;
				if (group != 0)
				{
					string resValue = ConvertGroup(maxGroup, group) + Separator + GroupTexts[groupNumber].ToPlural(group);
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
				result.Append(Separator);
			}
			if (isNegative) 
			{
				return Minus.Replace("*", result.ToString().Trim(' '));
			}
			else
			{
				return result.ToString().Trim(' ');
			}
		}

		public string ConvertGroup(int groupNumber, long number)
		{
			if (groupNumber == 0) { return string.Empty; }
			if (groupNumber > 1 && Exceptions.TryGetValue(number, out var value))
			{
				return value;
			}

			int group = (int)Math.Pow(10, groupNumber - 1);
			var groupValue = (int)(number / group);
			var leftOver = number % group;

			var leftText = ConvertGroup(groupNumber - 1, leftOver);
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
}

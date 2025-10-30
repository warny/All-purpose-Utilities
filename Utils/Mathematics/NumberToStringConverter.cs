using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Numerics;
using Utils.Objects;
using Utils.String;

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

            if (CachedConfigurations.TryGetValue(culture, out var result)) return result;
            if (culture.Length == 5) return GetConverter(culture[..2]);  // Fallback to the language-only code if region-specific code is not found.
            return CachedConfigurations["EN"];  // Default to English converter.
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
                        string decimalSeparator,
                        IReadOnlyDictionary<int, DigitListType> groups,
                        IReadOnlyDictionary<long, string> exceptions,
                        IReadOnlyDictionary<string, string> replacements,
                        NumberScale scale,
                        Func<string, string> adjustFunction = null,
                        IReadOnlyDictionary<int, string> fractions = null,
                        BigInteger? maxNumber = null,
                        string fractionSeparator = null)
        {
            Group = group;
            Separator = separator ?? " ";
            GroupSeparator = groupSeparator ?? "";
            Zero = zero.Arg().MustNotBeNull();
            Minus = minus.Arg().MustNotBeNull();
            DecimalSeparator = decimalSeparator ?? ",";
            Groups = groups.Arg().MustNotBeNull().Value.ToImmutableDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<long, DigitType>)kv.Value.Digits.ToDictionary(d => d.Digit).ToImmutableDictionary());
            Exceptions = exceptions.Arg().MustNotBeNull().Value.ToImmutableDictionary();
            Replacements = replacements?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty;
            Scale = scale;
            AdjustFunction = adjustFunction ?? (s => s);
            Fractions = fractions?.ToImmutableDictionary() ?? ImmutableDictionary<int, string>.Empty;
            MaxNumber = maxNumber;
            FractionSeparator = string.IsNullOrWhiteSpace(fractionSeparator) ? "/" : fractionSeparator;
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
        /// Word used as decimal separator
        /// </summary>
        public string DecimalSeparator { get; }
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
        /// Names for decimal fractions by digit count
        /// </summary>
        public IReadOnlyDictionary<int, string> Fractions { get; }
        /// <summary>
        /// Gets the connector used when expressing non-decimal fractions.
        /// </summary>
        public string FractionSeparator { get; }
        /// <summary>
        /// Maximum number that can be converted or null when unlimited.
        /// </summary>
        public BigInteger? MaxNumber { get; }
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
        /// Converts a decimal number to its string representation.
        /// </summary>
        public string Convert(decimal number)
        {
            bool isNegative = number < 0;
            if (isNegative) number = -number;

            decimal integerPart = decimal.Truncate(number);
            decimal fraction = number - integerPart;

            var result = new StringBuilder(Convert((BigInteger)integerPart));

            if (fraction != 0)
            {
                string digits = fraction.ToString(System.Globalization.CultureInfo.InvariantCulture).Split('.')[1];
                result.Append(Separator).Append(DecimalSeparator).Append(Separator);

                if (Fractions.TryGetValue(digits.Length, out var suffix))
                {
                    var valueText = Convert(BigInteger.Parse(digits)).Replace("-", " ");
                    result.Append(valueText).Append(Separator).Append(suffix.ToPlural(long.Parse(digits)));
                }
                else
                {
                    foreach (char c in digits)
                    {
                        int d = c - '0';
                        result.Append(Groups[1][d].StringValue).Append(Separator);
                    }
                }
            }

            var final = result.ToString().Trim();
            return isNegative ? Minus.Replace("*", final) : AdjustFunction(final);
        }

        /// <summary>
        /// Converts a rational <see cref="Number"/> to its string representation.
        /// </summary>
        /// <param name="number">The rational number to convert.</param>
        /// <returns>The string representation of the specified number.</returns>
        public string Convert(Number number)
        {
            if (number.Denominator.IsOne)
            {
                return Convert(number.Numerator);
            }

            bool isNegative = number.Numerator.Sign < 0;
            Number absoluteValue = Number.Abs(number);

            string final = BuildFractionText(
                absoluteValue.Numerator,
                absoluteValue.Denominator,
                allowFractionNames: false);

            final = final.Trim();

            return isNegative ? Minus.Replace("*", final) : AdjustFunction(final);
        }

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
                    resValue = ApplyReplacements(resValue);
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
            finalResult = ApplyReplacements(finalResult);
            finalResult = isNegative ? Minus.Replace("*", finalResult) : AdjustFunction(finalResult);
            return AdjustFunction(finalResult);
        }

        /// <summary>
        /// Applies configured replacements to a generated textual representation.
        /// </summary>
        /// <param name="value">The value to adjust.</param>
        /// <returns>The value after applying any matching replacements.</returns>
        private string ApplyReplacements(string value)
        {
            if (string.IsNullOrEmpty(value) || Replacements.Count == 0)
            {
                return value;
            }

            if (Replacements.TryGetValue(value, out var exactMatch))
            {
                return exactMatch;
            }

            foreach (var replacement in Replacements)
            {
                if (value.Contains(replacement.Key))
                {
                    value = value.Replace(replacement.Key, replacement.Value);
                }
            }

            return value;
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

        /// <summary>
        /// Builds the textual representation of a fraction using existing conversion helpers.
        /// </summary>
        /// <param name="numerator">The numerator of the fraction.</param>
        /// <param name="denominator">The denominator of the fraction.</param>
        /// <returns>The textual representation of the fraction.</returns>
        private string BuildFractionText(BigInteger numerator, BigInteger denominator, bool allowFractionNames = true)
        {
            if (allowFractionNames &&
                TryGetBase10FractionDigits(denominator, out int digits) &&
                Fractions.TryGetValue(digits, out var suffix) &&
                numerator >= 0 &&
                numerator <= long.MaxValue)
            {
                string valueText = Convert(numerator).Replace("-", " ");
                return string.Concat(valueText, Separator, suffix.ToPlural((long)numerator)).Trim();
            }

            string numeratorText = Convert(numerator).Replace("-", " ");
            string denominatorText = Convert(denominator).Replace("-", " ");

            string connector = FractionSeparator;
            return string.Concat(numeratorText, Separator, connector, Separator, denominatorText).Trim();
        }

        /// <summary>
        /// Determines whether the supplied denominator represents a power of ten that can map to a configured fraction suffix.
        /// </summary>
        /// <param name="value">The denominator to inspect.</param>
        /// <param name="digits">When successful, receives the number of zero digits in the power of ten.</param>
        /// <returns><see langword="true"/> when the denominator is a power of ten; otherwise <see langword="false"/>.</returns>
        private static bool TryGetBase10FractionDigits(BigInteger value, out int digits)
        {
            digits = 0;

            if (value <= BigInteger.Zero)
            {
                return false;
            }

            BigInteger current = value;

            while (current % 10 == 0)
            {
                current /= 10;
                digits++;
            }

            return current == BigInteger.One;
        }
    }

    /// <summary>
    /// Represents the scale used for large number names (e.g., thousand, million).
    /// </summary>
    public partial class NumberScale
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NumberScale"/> class with the prefixes and metadata
        /// required to generate scale names.
        /// </summary>
        /// <param name="staticValues">The predefined scale names starting at 10³.</param>
        /// <param name="scaleSuffixes">The suffixes used to build higher-order names.</param>
        /// <param name="startIndex">The starting index applied when calculating suffixes.</param>
        /// <param name="voidGroup">The placeholder for empty groups.</param>
        /// <param name="groupSeparator">The separator inserted between prefix segments.</param>
        /// <param name="scale0Prefixes">Optional prefixes for base scale names.</param>
        /// <param name="unitsPrefixes">Optional prefixes for unit multipliers.</param>
        /// <param name="tensPrefixes">Optional prefixes for ten multipliers.</param>
        /// <param name="hundredsPrefixes">Optional prefixes for hundred multipliers.</param>
        /// <param name="firstLetterUppercase">Whether the generated name should start with a capital letter.</param>
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
            StaticValues = staticValues.Arg().MustNotBeNull().Value.ToImmutableArray();
            ScaleSuffixes = scaleSuffixes.Arg().MustNotBeNull().Value.ToImmutableArray();
            StartIndex = startIndex;
            FirstLetterUppercase = firstLetterUppercase;

            VoidGroup = voidGroup.ToDefaultIfNullOrEmpty("ni");
            GroupSeparator = groupSeparator.ToDefaultIfNullOrEmpty("lli");

            Scale0Prefixes = scale0Prefixes?.ToImmutableArray() ?? Scale0Prefixes;
            UnitsPrefixes = unitsPrefixes?.ToImmutableArray() ?? UnitsPrefixes;
            TensPrefixes = tensPrefixes?.ToImmutableArray() ?? TensPrefixes;
            HundredsPrefixes = hundredsPrefixes?.ToImmutableArray() ?? HundredsPrefixes;
        }

        /// <summary>
        /// Gets the static names associated with the first scale levels.
        /// </summary>
        public IReadOnlyList<string> StaticValues { get; }

        /// <summary>
        /// Gets the list of suffixes appended to generated scale names.
        /// </summary>
        public IReadOnlyList<string> ScaleSuffixes { get; }

        /// <summary>
        /// Gets the index offset applied when calculating dynamic scale names.
        /// </summary>
        public int StartIndex { get; }

        /// <summary>
        /// Gets a value indicating whether generated names should start with a capital letter.
        /// </summary>
        public bool FirstLetterUppercase { get; }

        private readonly string VoidGroup;
        private readonly string GroupSeparator;

        private static readonly Regex PrefixParser = PrefixParserRegex();

        /// <summary>
        /// Gets the default prefixes used for the base scale (10⁰) names.
        /// </summary>
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


        /// <summary>
        /// Gets the prefixes used when building unit multipliers.
        /// </summary>
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

        /// <summary>
        /// Gets the prefixes used when building tens multipliers.
        /// </summary>
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

        /// <summary>
        /// Gets the prefixes used when building hundreds multipliers.
        /// </summary>
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

        [GeneratedRegex(@"(\((?<start>\w+)\))?(?<value>\w+)(\((?<end>\w+)\))?", RegexOptions.Compiled)]
        private static partial Regex PrefixParserRegex();
    }
}

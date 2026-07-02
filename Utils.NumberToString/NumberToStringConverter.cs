using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Mathematics;
using Utils.Numerics;
using Utils.Objects;
using Utils.String;

namespace Utils.NumberToString
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
            ArgumentException.ThrowIfNullOrEmpty(culture);
            if (culture.Length < 2)
                throw new ArgumentException("Culture code must be at least 2 characters.", nameof(culture));

            if (CachedConfigurations.TryGetValue(culture, out var result)) return result;
            // Recursively strip the last BCP-47 subtag (e.g. "zh-Hans-CN" → "zh-Hans" → "zh")
            // until a match is found or only the 2-letter language code remains.
            int lastSep = culture.LastIndexOf('-');
            if (lastSep >= 2) return GetConverter(culture[..lastSep]);
            return CachedConfigurations["EN"];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NumberToStringConverter"/> class from
        /// a <see cref="NumberToStringConverterOptions"/> object.
        /// </summary>
        /// <param name="options">All configuration parameters.</param>
        public NumberToStringConverter(NumberToStringConverterOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Zero.Arg().MustNotBeNull();
            options.Minus.Arg().MustNotBeNull();
            options.Groups.Arg().MustNotBeNull();
            options.Scale.Arg().MustNotBeNull();

            Group = options.Group;
            Separator = options.Separator ?? " ";
            GroupSeparator = options.GroupSeparator ?? "";
            Zero = options.Zero;
            Minus = options.Minus;
            DecimalSeparator = options.DecimalSeparator ?? ",";
            Groups = options.Groups.ToImmutableDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<long, DigitType>)kv.Value.Digits.ToDictionary(d => d.Digit).ToImmutableDictionary());
            Exceptions = (options.Exceptions ?? new Dictionary<long, string>()).ToImmutableDictionary();
            Replacements = (options.Replacements ?? Array.Empty<ReplacementRule>())
                .Select(r => r ?? throw new ArgumentNullException(nameof(options), "Replacement entries must not be null."))
                .ToImmutableArray();
            _replacementLookup = Replacements.ToImmutableDictionary(r => r.OldValue, r => r.NewValue, StringComparer.Ordinal);
            _substringReplacements = Replacements.Where(r =>
                r.Scope is ReplacementScope.Anywhere or ReplacementScope.StartsWith or ReplacementScope.EndsWith)
                .ToImmutableArray();
            Scale = options.Scale;
            LanguageSpecifics = options.LanguageSpecifics ?? new DefaultNumberToStringLanguageSpecifics();
            LanguageIdentifier = options.LanguageIdentifier ?? string.Empty;
            _rawAdjustFunction = options.AdjustFunction;
            AdjustFunction = input => LanguageSpecifics.FinalizeWriting(LanguageIdentifier, (_rawAdjustFunction ?? (s => s))(input));
            Fractions = options.Fractions?.ToImmutableDictionary() ?? ImmutableDictionary<int, string>.Empty;
            MaxNumber = options.MaxNumber;
            FractionSeparator = string.IsNullOrWhiteSpace(options.FractionSeparator) ? "/" : options.FractionSeparator;

            OrdinalSuffix = options.OrdinalSuffix;
            OrdinalRemoveTrailing = options.OrdinalRemoveTrailing;
            OrdinalExceptions = (options.OrdinalExceptions ?? new Dictionary<long, string>()).ToImmutableDictionary();
            OrdinalWordRules = (options.OrdinalWordRules ?? new Dictionary<string, string>()).ToImmutableDictionary();
            OrdinalPrefix = options.OrdinalPrefix;
            OrdinalVariants = (options.OrdinalVariants ?? []).ToImmutableArray();

            VariantDimensions = (options.VariantDimensions ?? []).ToImmutableArray();
            VariantRules = (options.VariantRules ?? []).ToImmutableArray();
            Triggers = (options.Triggers ?? []).ToImmutableArray();
            _yearFormat = options.YearFormat;
            Multiplicatives = options.Multiplicatives?.ToImmutableDictionary() ?? ImmutableDictionary<int, string>.Empty;
            MultiplicativeSuffix = options.MultiplicativeSuffix;
            _groupConnector = options.GroupConnector;
            _groupConnectorThreshold = options.GroupConnectorThreshold;
            // Index both canonical name and localName so both are accepted in API calls and XML constraints.
            _dimensionIndex = VariantDimensions
                .SelectMany(d => string.IsNullOrEmpty(d.LocalName)
                    ? (IEnumerable<(string, VariantDimension)>)[(d.Name, d)]
                    : [(d.Name, d), (d.LocalName, d)])
                .ToImmutableDictionary(t => t.Item1, t => t.Item2, StringComparer.OrdinalIgnoreCase);
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
        /// Gets the language-specific finalizer applied to produced texts.
        /// </summary>
        public INumberToStringLanguageSpecifics LanguageSpecifics { get; }
        /// <summary>
        /// Gets the language identifier used when finalizing written values.
        /// </summary>
        public string LanguageIdentifier { get; }
        /// <summary>
        /// Special cases for specific numbers
        /// </summary>
        public IReadOnlyDictionary<long, string> Exceptions { get; }
        /// <summary>
        /// Replacements for specific text segments
        /// </summary>
        public IReadOnlyList<ReplacementRule> Replacements { get; }
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
        /// Ordinal suffix appended to the last word of the cardinal when no word-level rule matches.
        /// </summary>
        public string? OrdinalSuffix { get; }

        /// <summary>
        /// Trailing string to remove from the last cardinal word before appending <see cref="OrdinalSuffix"/>.
        /// When set, the string is stripped from the end of the last word only if it ends with this value.
        /// </summary>
        public string? OrdinalRemoveTrailing { get; }

        /// <summary>
        /// Integer-level ordinal exceptions (whole number → ordinal text).
        /// </summary>
        public IReadOnlyDictionary<long, string> OrdinalExceptions { get; }

        /// <summary>
        /// Word-level ordinal transformation rules applied to the last word of the cardinal.
        /// </summary>
        public IReadOnlyDictionary<string, string> OrdinalWordRules { get; }

        /// <summary>
        /// Prefix prepended to the whole ordinal result after <see cref="AdjustFunction"/> is applied.
        /// </summary>
        public string? OrdinalPrefix { get; }

        /// <summary>
        /// Variant-specific ordinal rules applied when their dimension constraints match the active variant query.
        /// </summary>
        public IReadOnlyList<OrdinalVariantRule> OrdinalVariants { get; }

        /// <summary>
        /// Declared variant dimensions for this language (e.g. "gender", "case").
        /// The first value of each dimension is the default when no explicit variant is requested.
        /// </summary>
        public IReadOnlyList<VariantDimension> VariantDimensions { get; }

        /// <summary>
        /// Variant rules that map dimension constraints to replacement rules.
        /// Applied in order of ascending specificity between raw adjustment and finalization.
        /// </summary>
        public IReadOnlyList<VariantRule> VariantRules { get; }

        /// <summary>
        /// Trigger rules applied at specific points in the conversion pipeline.
        /// Processed in declaration order.
        /// </summary>
        public IReadOnlyList<TriggerRule> Triggers { get; }

        private readonly YearFormatOptions? _yearFormat;

        /// <summary>Year-format options, or <see langword="null"/> when not configured.</summary>
        internal YearFormatOptions? YearFormat => _yearFormat;

        /// <summary>Named multiplicative forms keyed by multiplier value.</summary>
        public IReadOnlyDictionary<int, string> Multiplicatives { get; }

        /// <summary>Suffix used for unnamed multiplicatives (e.g. " times").</summary>
        public string? MultiplicativeSuffix { get; }

        /// <summary>When <see langword="true"/>, calling <see cref="ConvertMultiplicative"/> will produce a result.</summary>
        public bool SupportsMultiplicative => Multiplicatives.Count > 0 || MultiplicativeSuffix != null;

        /// <summary>Gets the group connector used between scale groups when the lower group is small.</summary>
        public string? GroupConnector => _groupConnector;

        /// <summary>Gets the threshold below which the group connector is used.</summary>
        public int GroupConnectorThreshold => (int)_groupConnectorThreshold;

        /// <inheritdoc/>
        public bool SupportsOrdinals =>
            OrdinalSuffix != null || OrdinalPrefix != null
            || OrdinalExceptions.Count > 0
            || OrdinalWordRules.Count > 0
            || OrdinalVariants.Count > 0;

        private readonly ImmutableDictionary<string, string> _replacementLookup;
        private readonly ImmutableArray<ReplacementRule> _substringReplacements;
        private readonly ImmutableDictionary<string, VariantDimension> _dimensionIndex;
        private readonly Func<string, string>? _rawAdjustFunction;
        private readonly string? _groupConnector;
        private readonly long _groupConnectorThreshold;

        /// <summary>
        /// The raw adjust function before composition with <see cref="LanguageSpecifics"/>.
        /// Used by <see cref="NumberToStringConverterOptions"/> for round-trip cloning.
        /// </summary>
        internal Func<string, string>? RawAdjustFunction => _rawAdjustFunction;

        /// <summary>
        /// Specifies when a <see cref="TriggerRule"/> fires in the conversion pipeline.
        /// </summary>
        public enum TriggerAt
        {
            /// <summary>Fires on the digit-only text of a group, before the scale name is appended.</summary>
            Group,
            /// <summary>Fires on the digit+scale text of a group, after per-group Replacements are applied.</summary>
            GroupWithScale,
            /// <summary>Fires on the fully assembled text, after global Replacements and Variants, before FinalizeWriting.</summary>
            End,
        }

        /// <summary>
        /// A compiled text-replacement rule inside a <see cref="TriggerRule"/>.
        /// When the trigger fires, the most specific matching variant form is selected and
        /// applied exactly once; <see cref="DefaultTo"/> is used when nothing matches.
        /// </summary>
        public sealed class TriggerReplace
        {
            /// <summary>Initializes a new <see cref="TriggerReplace"/>.</summary>
            public TriggerReplace(
                string from,
                bool isRegex,
                IReadOnlyList<(IReadOnlyDictionary<string, string> Constraints, string To)> forms,
                string? defaultTo)
            {
                From = from;
                IsRegex = isRegex;
                CompiledRegex = isRegex ? new Regex(from, RegexOptions.Compiled) : null;
                Forms = forms;
                DefaultTo = defaultTo;
            }
            /// <summary>Gets the text or regex pattern to match.</summary>
            public string From { get; }
            /// <summary>Gets whether <see cref="From"/> is a .NET regular expression.</summary>
            public bool IsRegex { get; }
            /// <summary>Gets the pre-compiled regex when <see cref="IsRegex"/> is <see langword="true"/>; otherwise <see langword="null"/>.</summary>
            public Regex? CompiledRegex { get; }
            /// <summary>
            /// Gets the variant-specific forms ordered by declaration order.
            /// At runtime the most specific match (most constraints) wins.
            /// </summary>
            public IReadOnlyList<(IReadOnlyDictionary<string, string> Constraints, string To)> Forms { get; }
            /// <summary>Gets the unconditional default replacement used when no variant form matches.</summary>
            public string? DefaultTo { get; }
        }

        /// <summary>
        /// A trigger that fires at a specific position in the conversion pipeline and applies
        /// one or more text replacements, each optionally variant-conditioned.
        /// </summary>
        public sealed class TriggerRule
        {
            /// <summary>Initializes a new <see cref="TriggerRule"/>.</summary>
            public TriggerRule(TriggerAt executeAt, int[]? groupIndices, IReadOnlyList<TriggerReplace> replaces)
            {
                ExecuteAt = executeAt;
                GroupIndices = groupIndices;
                Replaces = replaces;
            }
            /// <summary>Gets when this trigger fires.</summary>
            public TriggerAt ExecuteAt { get; }
            /// <summary>Gets the group indices this trigger is restricted to, or <see langword="null"/> for all groups.</summary>
            public int[]? GroupIndices { get; }
            /// <summary>Gets the replacement rules applied when this trigger fires.</summary>
            public IReadOnlyList<TriggerReplace> Replaces { get; }
        }

        /// <summary>
        /// Converts an integer to its string representation.
        /// </summary>
        public string Convert(int number) => Convert((BigInteger)number);

        /// <summary>
        /// Converts a long integer to its string representation.
        /// </summary>
        public string Convert(long number) => Convert((BigInteger)number);

        /// <summary>
        /// Converts an integer to its string representation, applying the specified variant parameters.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        public string Convert(int number, params string[] variants) => Convert((BigInteger)number, variants);

        /// <summary>
        /// Converts a long integer to its string representation, applying the specified variant parameters.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted number with the requested variants applied.</returns>
        public string Convert(long number, params string[] variants) => Convert((BigInteger)number, variants);

        /// <inheritdoc cref="INumberToStringConverter.Convert(int, int, string[])"/>
        public string Convert(int number, int significantDigits, params string[] variants)
            => Convert((BigInteger)number, significantDigits, variants);

        /// <inheritdoc cref="INumberToStringConverter.Convert(long, int, string[])"/>
        public string Convert(long number, int significantDigits, params string[] variants)
            => Convert((BigInteger)number, significantDigits, variants);

        /// <summary>
        /// Converts a decimal number to its string representation.
        /// </summary>
        public string Convert(decimal number) => Convert(number, -1, null, []);

        /// <summary>
        /// Converts a decimal number to its string representation, applying the specified
        /// morphological variant parameters.
        /// </summary>
        public string Convert(decimal number, params string[] variants)
            => Convert(number, -1, null, variants);

        /// <summary>
        /// Converts a decimal number to its string representation with a mandatory number of
        /// decimal digits, applying optional variant parameters.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="mandatoryDecimalDigits">
        /// Negative: show the decimal part as-is. Zero: suppress the decimal part entirely.
        /// Positive: round to N decimal places and always show exactly N digits (zero-padded).
        /// </param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        public string Convert(decimal number, int mandatoryDecimalDigits, params string[] variants)
            => Convert(number, mandatoryDecimalDigits, null, variants);

        /// <summary>
        /// Converts a decimal number to its string representation with a mandatory number of
        /// decimal digits, custom decimal formatting options, and optional variant parameters.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="mandatoryDecimalDigits">
        /// Negative: show the decimal part as-is. Zero: suppress the decimal part entirely.
        /// Positive: round to N decimal places and always show exactly N digits (zero-padded).
        /// </param>
        /// <param name="options">
        /// Optional overrides for the decimal separator word, the denomination suffix, and
        /// zero-decimal suppression. When <see langword="null"/>, the language defaults apply.
        /// </param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        public string Convert(decimal number, int mandatoryDecimalDigits, DecimalFormatOptions? options, params string[] variants)
        {
            bool isNegative = number < 0;
            if (isNegative) number = -number;

            if (mandatoryDecimalDigits >= 0)
                number = decimal.Round(number, mandatoryDecimalDigits, MidpointRounding.AwayFromZero);

            decimal integerPart = decimal.Truncate(number);
            decimal fraction = number - integerPart;

            var result = new StringBuilder(Convert((BigInteger)integerPart, variants));

            string digits = fraction != 0
                ? fraction.ToString(System.Globalization.CultureInfo.InvariantCulture).Split('.')[1]
                : string.Empty;

            if (mandatoryDecimalDigits > 0 && digits.Length < mandatoryDecimalDigits)
                digits = digits.PadRight(mandatoryDecimalDigits, '0');

            bool isZeroDecimal = digits.Length == 0 || BigInteger.Parse(digits) == 0;
            if (isZeroDecimal && (options?.OmitZeroDecimals ?? false))
                digits = string.Empty;

            if (digits.Length > 0)
            {
                string separatorWord = options?.DecimalSeparator ?? DecimalSeparator;
                result.Append(Separator).Append(separatorWord.ToPlural((long)integerPart)).Append(Separator);

                Fractions.TryGetValue(digits.Length, out var configuredSuffix);
                string? activeSuffix = options?.DecimalSuffix ?? configuredSuffix;

                if (activeSuffix != null)
                {
                    var valueText = Convert(BigInteger.Parse(digits), variants).Replace("-", " ");
                    result.Append(valueText).Append(Separator).Append(activeSuffix.ToPlural(long.Parse(digits)));
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
        /// Converts a BigInteger to its string representation using the default variant
        /// (the first declared value of each dimension).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="number"/> exceeds <see cref="MaxNumber"/>.
        /// </exception>
        public string Convert(BigInteger number) => Convert(number, []);

        /// <summary>
        /// Converts <paramref name="number"/> rounded to <paramref name="significantDigits"/> most significant
        /// digits into its string representation, applying optional variant parameters.
        /// Uses standard rounding (≥ 5 rounds up). For example, 123456789 with 3 significant digits
        /// rounds to 123000000 before conversion.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="significantDigits">Number of significant digits to keep. Must be ≥ 1.</param>
        /// <param name="variants">Zero or more <c>"dimension=value"</c> strings.</param>
        /// <returns>The formatted rounded number with variants applied.</returns>
        public string Convert(BigInteger number, int significantDigits, params string[] variants)
            => Convert(MathEx.RoundToSignificantDigits(number, significantDigits), variants);

        /// <summary>
        /// Converts a BigInteger to its string representation, applying the specified morphological
        /// variant parameters. The pipeline is: raw text → user adjust → variant rules → language finalization.
        /// When no parameters are supplied, the first declared value of each dimension is used as default.
        /// </summary>
        /// <param name="number">The value to convert.</param>
        /// <param name="variants">
        /// Zero or more <c>"dimension=value"</c> strings. Unrecognised dimensions fall back silently.
        /// </param>
        /// <returns>The formatted number with variants applied.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="number"/> exceeds <see cref="MaxNumber"/>.
        /// </exception>
        public string Convert(BigInteger number, params string[] variants)
        {
            if (MaxNumber.HasValue && BigInteger.Abs(number) > MaxNumber.Value)
                throw new ArgumentOutOfRangeException(nameof(number), $"The value exceeds the maximum supported number ({MaxNumber.Value}).");

            if (number == 0) return Zero;

            bool isNegative = number.Sign == -1;
            BigInteger abs = isNegative ? BigInteger.Abs(number) : number;

            var query = BuildVariantQuery(variants);
            string raw = ConvertRaw(abs, query);
            if (_rawAdjustFunction != null) raw = _rawAdjustFunction(raw);
            raw = ApplyVariantRules(raw, query);
            raw = ApplyTriggers(raw, TriggerAt.End, null, query);
            string final = LanguageSpecifics.FinalizeWriting(LanguageIdentifier, raw);

            return isNegative ? Minus.Replace("*", final) : final;
        }

        /// <summary>
        /// Produces the raw text for a positive number without any adjustment or finalization.
        /// </summary>
        private string ConvertRaw(BigInteger abs, IReadOnlyDictionary<string, string>? variantQuery = null)
        {
            if (abs.Between(long.MinValue, long.MaxValue) && Exceptions.TryGetValue((long)abs, out var exValue))
                return exValue;

            var maxGroup = Groups.Keys.Max();
            var groupValue = BigInteger.Pow(10, maxGroup);
            int groupNumber = 0;
            var groupsValues = new Stack<(string text, long numericValue)>();

            BigInteger remaining = abs;
            while (remaining != 0)
            {
                var group = (long)(remaining % groupValue);
                if (group != 0)
                {
                    string digits = ConvertGroup(maxGroup, group);
                    digits = ApplyTriggers(digits, TriggerAt.Group, groupNumber, variantQuery);

                    string resValue = digits + Separator + Scale.GetScaleName(groupNumber).ToPlural(group);
                    resValue = ApplyReplacements(resValue);
                    resValue = ApplyTriggers(resValue, TriggerAt.GroupWithScale, groupNumber, variantQuery);

                    groupsValues.Push((resValue.Trim(), group));
                }
                remaining /= groupValue;
                groupNumber++;
            }

            var result = new StringBuilder();
            while (groupsValues.Count > 0)
            {
                var (text, _) = groupsValues.Pop();
                result.Append(text);
                if (groupsValues.Count > 0)
                {
                    long nextValue = groupsValues.Peek().numericValue;
                    if (_groupConnector != null && nextValue < _groupConnectorThreshold)
                        result.Append(Separator).Append(_groupConnector).Append(Separator);
                    else
                        result.Append(GroupSeparator).Append(Separator);
                }
            }

            var finalResult = result.ToString().TrimEnd(GroupSeparator.ToCharArray().Union(Separator.ToCharArray()).ToArray());
            return ApplyReplacements(finalResult);
        }

        /// <summary>
        /// Builds the full variant query by merging explicit parameters with dimension defaults.
        /// For each declared dimension not mentioned in <paramref name="variants"/>, the first
        /// declared value is inserted as the default.
        /// </summary>
        private IReadOnlyDictionary<string, string> BuildVariantQuery(string[] variants)
        {
            var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var param in variants)
            {
                int eq = param.IndexOf('=');
                if (eq > 0)
                {
                    string rawName = param[..eq].Trim();
                    // Normalize: localName (e.g. "genus") → canonical name (e.g. "gender")
                    string canonical = _dimensionIndex.TryGetValue(rawName, out var dim) ? dim.Name : rawName;
                    query[canonical] = param[(eq + 1)..].Trim();
                }
            }

            foreach (var dimension in VariantDimensions)
            {
                if (!query.ContainsKey(dimension.Name) && dimension.DefaultValue != null)
                    query[dimension.Name] = dimension.DefaultValue;
            }

            return query;
        }

        /// <summary>
        /// Applies variant rules to <paramref name="text"/> in order of ascending specificity.
        /// A rule matches when all its dimension constraints are satisfied by <paramref name="query"/>.
        /// </summary>
        private string ApplyVariantRules(string text, IReadOnlyDictionary<string, string> query)
        {
            if (VariantRules.Count == 0 || query.Count == 0) return text;

            foreach (var rule in VariantRules.OrderBy(r => r.Specificity))
            {
                bool matches = rule.Constraints.All(c =>
                    query.TryGetValue(c.Key, out var v) &&
                    string.Equals(v, c.Value, StringComparison.OrdinalIgnoreCase));

                if (!matches) continue;

                foreach (var replacement in rule.Replacements)
                    text = ApplyVariantReplacement(text, replacement);
            }

            return text;
        }

        /// <summary>
        /// Applies a single replacement rule to <paramref name="text"/> according to its scope.
        /// </summary>
        private static string ApplyVariantReplacement(string text, ReplacementRule replacement) =>
            replacement.Scope switch
            {
                ReplacementScope.Standalone  => text == replacement.OldValue ? replacement.NewValue : text,
                ReplacementScope.Anywhere    => text.Replace(replacement.OldValue, replacement.NewValue),
                ReplacementScope.LastWord    => ApplyLastWordReplacement(text, replacement.OldValue, replacement.NewValue),
                ReplacementScope.StartsWith  => text.StartsWith(replacement.OldValue, StringComparison.Ordinal)
                    ? replacement.NewValue + text[replacement.OldValue.Length..]
                    : text,
                ReplacementScope.EndsWith    => text.EndsWith(replacement.OldValue, StringComparison.Ordinal)
                    ? text[..^replacement.OldValue.Length] + replacement.NewValue
                    : text,
                _                            => text,
            };

        /// <summary>
        /// Applies all matching triggers for the given pipeline position to <paramref name="text"/>.
        /// A trigger matches when its <see cref="TriggerRule.ExecuteAt"/> equals <paramref name="at"/>
        /// and, if group indices are specified, <paramref name="groupIndex"/> is among them.
        /// Each Replace within a matching trigger selects the most specific form that satisfies
        /// <paramref name="query"/>, falling back to <see cref="TriggerReplace.DefaultTo"/>.
        /// </summary>
        private string ApplyTriggers(string text, TriggerAt at, int? groupIndex, IReadOnlyDictionary<string, string>? query)
        {
            if (Triggers.Count == 0) return text;

            foreach (var trigger in Triggers)
            {
                if (trigger.ExecuteAt != at) continue;
                if (trigger.GroupIndices != null
                    && (groupIndex == null || !Array.Exists(trigger.GroupIndices, i => i == groupIndex.Value)))
                    continue;

                foreach (var replace in trigger.Replaces)
                    text = ApplyTriggerReplace(text, replace, query);
            }
            return text;
        }

        /// <summary>
        /// Applies a single trigger replace to <paramref name="text"/>, selecting the most specific
        /// variant form that matches <paramref name="query"/>. Falls back to <see cref="TriggerReplace.DefaultTo"/>.
        /// Skips the replacement entirely when neither a matching form nor a default exists.
        /// </summary>
        private static string ApplyTriggerReplace(string text, TriggerReplace replace, IReadOnlyDictionary<string, string>? query)
        {
            string? bestTo = null;
            int bestScore = -1;

            if (query != null)
            {
                foreach (var (constraints, to) in replace.Forms)
                {
                    bool matches = constraints.All(c =>
                        query.TryGetValue(c.Key, out var v) &&
                        string.Equals(v, c.Value, StringComparison.OrdinalIgnoreCase));

                    if (matches && constraints.Count > bestScore)
                    {
                        bestTo = to;
                        bestScore = constraints.Count;
                    }
                }
            }

            string? effectiveTo = bestTo ?? replace.DefaultTo;
            if (effectiveTo == null) return text;

            return replace.CompiledRegex != null
                ? replace.CompiledRegex.Replace(text, effectiveTo)
                : text.Replace(replace.From, effectiveTo, StringComparison.Ordinal);
        }

        /// <summary>
        /// Replaces <paramref name="oldValue"/> at the end of <paramref name="text"/> when it
        /// forms a complete word boundary (preceded by space, hyphen, or start-of-string).
        /// </summary>
        private static string ApplyLastWordReplacement(string text, string oldValue, string newValue)
        {
            if (text.Length < oldValue.Length) return text;
            if (!text.EndsWith(oldValue, StringComparison.Ordinal)) return text;

            int prefixLength = text.Length - oldValue.Length;
            if (prefixLength > 0 && text[prefixLength - 1] is not (' ' or '-'))
                return text;

            return text[..prefixLength] + newValue;
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

            if (_replacementLookup.TryGetValue(value, out var exactMatch))
            {
                return exactMatch;
            }

            foreach (var replacement in _substringReplacements)
            {
                value = replacement.Scope switch
                {
                    ReplacementScope.Anywhere => value.Replace(replacement.OldValue, replacement.NewValue),
                    ReplacementScope.StartsWith when value.StartsWith(replacement.OldValue, StringComparison.Ordinal)
                        => replacement.NewValue + value[replacement.OldValue.Length..],
                    ReplacementScope.EndsWith when value.EndsWith(replacement.OldValue, StringComparison.Ordinal)
                        => value[..^replacement.OldValue.Length] + replacement.NewValue,
                    _ => value,
                };
            }

            return value;
        }

        /// <summary>
        /// Represents a configured replacement rule.
        /// </summary>
        public sealed class ReplacementRule
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ReplacementRule"/> class.
            /// </summary>
            /// <param name="oldValue">The text that should be replaced.</param>
            /// <param name="newValue">The replacement text.</param>
            /// <param name="scope">The scope that controls how the replacement is applied.</param>
            /// <exception cref="ArgumentException">Thrown when <paramref name="oldValue"/> or <paramref name="newValue"/> is null or empty.</exception>
            public ReplacementRule(string oldValue, string newValue, ReplacementScope scope)
            {
                if (string.IsNullOrEmpty(oldValue))
                {
                    throw new ArgumentException("Replacement old value must be provided.", nameof(oldValue));
                }

                if (string.IsNullOrEmpty(newValue))
                {
                    throw new ArgumentException("Replacement new value must be provided.", nameof(newValue));
                }

                OldValue = oldValue;
                NewValue = newValue;
                Scope = scope;
            }

            /// <summary>
            /// Gets the text that should be replaced.
            /// </summary>
            public string OldValue { get; }

            /// <summary>
            /// Gets the replacement text.
            /// </summary>
            public string NewValue { get; }

            /// <summary>
            /// Gets the scope that controls how the replacement is applied.
            /// </summary>
            public ReplacementScope Scope { get; }
        }

        /// <summary>
        /// Declares one dimension of grammatical variation (e.g. "gender") with its ordered
        /// set of values. The first value is the default applied when no explicit variant is requested.
        /// </summary>
        public sealed class VariantDimension
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="VariantDimension"/> class.
            /// </summary>
            /// <param name="name">The canonical English dimension name (e.g. "gender", "case").</param>
            /// <param name="values">The ordered list of valid values; the first is the default.</param>
            /// <param name="localName">Optional language-specific alias (e.g. "genus" for German, "sijamuoto" for Finnish).</param>
            public VariantDimension(string name, IReadOnlyList<string> values, string? localName = null)
            {
                Name = name;
                Values = values;
                LocalName = localName;
            }

            /// <summary>Gets the canonical English dimension name (e.g. "gender", "case").</summary>
            public string Name { get; }

            /// <summary>Gets the optional language-specific alias for this dimension (e.g. "genus", "sijamuoto").</summary>
            public string? LocalName { get; }

            /// <summary>Gets the ordered list of valid values. The first value is the default.</summary>
            public IReadOnlyList<string> Values { get; }

            /// <summary>Gets the default value (first declared), or <see langword="null"/> when the list is empty.</summary>
            public string? DefaultValue => Values.Count > 0 ? Values[0] : null;
        }

        /// <summary>
        /// Associates a set of dimension constraints with replacement rules to apply
        /// when all constraints are satisfied.
        /// </summary>
        public sealed class VariantRule
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="VariantRule"/> class.
            /// </summary>
            /// <param name="constraints">Dimension name → required value pairs.</param>
            /// <param name="replacements">Replacement rules applied when this variant is active.</param>
            public VariantRule(IReadOnlyDictionary<string, string> constraints, IReadOnlyList<ReplacementRule> replacements)
            {
                Constraints = constraints;
                Replacements = replacements;
            }

            /// <summary>Gets the dimension constraints (name → required value).</summary>
            public IReadOnlyDictionary<string, string> Constraints { get; }

            /// <summary>Gets the replacement rules applied when all constraints are satisfied.</summary>
            public IReadOnlyList<ReplacementRule> Replacements { get; }

            /// <summary>
            /// Gets the number of dimension constraints. Used to order rules from least to most specific.
            /// </summary>
            public int Specificity => Constraints.Count;
        }

        /// <summary>
        /// Associates dimension constraints with variant-specific ordinal configuration
        /// (exceptions, word rules, suffix, and removeTrailing override).
        /// All fields fall through to the base ordinal config when absent.
        /// </summary>
        public sealed class OrdinalVariantRule
        {
            /// <summary>
            /// Initializes a new instance of <see cref="OrdinalVariantRule"/>.
            /// </summary>
            public OrdinalVariantRule(
                IReadOnlyDictionary<string, string> constraints,
                IReadOnlyDictionary<long, string> exceptions,
                IReadOnlyDictionary<string, string> wordRules,
                string? suffix,
                string? removeTrailing)
            {
                Constraints = constraints;
                Exceptions = exceptions;
                WordRules = wordRules;
                Suffix = suffix;
                RemoveTrailing = removeTrailing;
            }

            /// <summary>Gets the dimension constraints that must all be satisfied for this variant to apply.</summary>
            public IReadOnlyDictionary<string, string> Constraints { get; }
            /// <summary>Gets variant-specific whole-number ordinal exceptions.</summary>
            public IReadOnlyDictionary<long, string> Exceptions { get; }
            /// <summary>Gets variant-specific last-word ordinal rules.</summary>
            public IReadOnlyDictionary<string, string> WordRules { get; }
            /// <summary>Gets the variant suffix override, or <see langword="null"/> to inherit the base suffix.</summary>
            public string? Suffix { get; }
            /// <summary>Gets the variant removeTrailing override, or <see langword="null"/> to inherit the base value.</summary>
            public string? RemoveTrailing { get; }
            /// <summary>Gets the number of dimension constraints (used for specificity ordering).</summary>
            public int Specificity => Constraints.Count;
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
        /// <param name="allowFractionNames">When <see langword="true"/>, uses named fraction suffixes when available.</param>
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

        /// <inheritdoc cref="INumberToStringConverter.ConvertOrdinal(int)"/>
        public string ConvertOrdinal(int number) => ConvertOrdinal((long)number, []);

        /// <inheritdoc cref="INumberToStringConverter.ConvertOrdinal(int, string[])"/>
        public string ConvertOrdinal(int number, params string[] variants) => ConvertOrdinal((long)number, variants);

        /// <inheritdoc cref="INumberToStringConverter.ConvertOrdinal(long)"/>
        public string ConvertOrdinal(long number) => ConvertOrdinal(number, []);

        /// <inheritdoc cref="INumberToStringConverter.ConvertOrdinal(long, string[])"/>
        public string ConvertOrdinal(long number, params string[] variants)
        {
            bool isNegative = number < 0;
            long absNumber = Math.Abs(number);
            var activeVariants = BuildVariantQuery(variants);

            // Plugin only accepts int; skip for values outside int range
            if (LanguageSpecifics is IOrdinalLanguageSpecifics ordinalPlugin
                && absNumber <= int.MaxValue
                && ordinalPlugin.TryConvertOrdinal((int)absNumber, activeVariants, out var pluginResult))
                return isNegative ? Minus.Replace("*", pluginResult!) : pluginResult!;

            // Find the most specific matching ordinal variant
            OrdinalVariantRule? activeVariant = FindBestOrdinalVariant(activeVariants);

            // Exceptions: variant first, then base
            if (activeVariant?.Exceptions.TryGetValue(absNumber, out var varException) == true)
                return isNegative ? Minus.Replace("*", varException) : varException;
            if (OrdinalExceptions.TryGetValue(absNumber, out var exception))
                return isNegative ? Minus.Replace("*", exception) : exception;

            string raw = absNumber == 0 ? Zero : ConvertRaw((BigInteger)absNumber, activeVariants);
            raw = ApplyVariantRules(raw, activeVariants);
            string ordinal = ApplyOrdinalTransform(raw, activeVariant);
            if (_rawAdjustFunction != null) ordinal = _rawAdjustFunction(ordinal);
            ordinal = ApplyTriggers(ordinal, TriggerAt.End, null, activeVariants);
            string final = LanguageSpecifics.FinalizeWriting(LanguageIdentifier, ordinal);
            if (!string.IsNullOrEmpty(OrdinalPrefix))
                final = OrdinalPrefix + final;
            return isNegative ? Minus.Replace("*", final) : final;
        }

        private OrdinalVariantRule? FindBestOrdinalVariant(IReadOnlyDictionary<string, string> query)
        {
            if (OrdinalVariants.Count == 0) return null;
            OrdinalVariantRule? best = null;
            int bestScore = -1;
            foreach (var variant in OrdinalVariants)
            {
                bool allMatch = variant.Constraints.All(c =>
                    query.TryGetValue(c.Key, out var v) &&
                    string.Equals(v, c.Value, StringComparison.OrdinalIgnoreCase));
                if (!allMatch) continue;
                int score = variant.Specificity;
                if (score > bestScore) { best = variant; bestScore = score; }
            }
            return best;
        }

        /// <inheritdoc cref="INumberToStringConverter.ConvertOrdinal(BigInteger)"/>
        public string ConvertOrdinal(BigInteger number) => ConvertOrdinal(checked((long)number), []);

        /// <inheritdoc cref="INumberToStringConverter.ConvertOrdinal(BigInteger, string[])"/>
        public string ConvertOrdinal(BigInteger number, params string[] variants)
            => ConvertOrdinal(checked((long)number), variants);

        /// <inheritdoc cref="INumberToStringConverter.ConvertCurrency(decimal, CurrencyDefinition)"/>
        public string ConvertCurrency(decimal amount, CurrencyDefinition currency)
            => ConvertCurrency(amount, currency, []);

        /// <inheritdoc cref="INumberToStringConverter.ConvertCurrency(decimal, CurrencyDefinition, string[])"/>
        public string ConvertCurrency(decimal amount, CurrencyDefinition currency, params string[] variants)
        {
            ArgumentNullException.ThrowIfNull(currency);

            bool isNegative = amount < 0;
            decimal absAmount = isNegative ? -amount : amount;

            long units = (long)decimal.Truncate(absAmount);
            decimal fractional = absAmount - units;
            long subunitFactor = (long)Math.Pow(10, currency.SubunitDigits);
            long subunits = (long)Math.Round((double)fractional * subunitFactor);

            // Carry: rounding may push subunits to subunitFactor (e.g. 1.999m → subunits=100).
            units += subunits / subunitFactor;
            subunits %= subunitFactor;

            string unitName = units == 1 ? currency.UnitSingular : currency.UnitPlural;
            string result = Convert(units, variants) + Separator + unitName;

            if (subunits > 0)
            {
                string subunitName = subunits == 1 ? currency.SubunitSingular : currency.SubunitPlural;
                string subunitsText = Convert(subunits, variants) + Separator + subunitName;
                result = result + Separator + currency.Connector + Separator + subunitsText;
            }

            return isNegative ? Minus.Replace("*", result) : result;
        }

        /// <inheritdoc cref="INumberToStringConverter.ConvertYear(int)"/>
        public string ConvertYear(int year) => ConvertYear(year, []);

        /// <inheritdoc cref="INumberToStringConverter.ConvertYear(int, string[])"/>
        public string ConvertYear(int year, params string[] variants)
        {
            bool isNegative = year < 0;
            int abs = Math.Abs(year);

            string body;
            if (_yearFormat?.SplitRanges?.Contains(abs) == true)
            {
                int centuries = abs / 100;
                int remainder = abs % 100;

                if (remainder == 0 && _yearFormat.HundredWord != null)
                    body = Convert(centuries, variants) + Separator + _yearFormat.HundredWord;
                else if (remainder is > 0 and < 10 && _yearFormat.ZeroConnector != null)
                    body = Convert(centuries, variants) + Separator + _yearFormat.ZeroConnector + Separator + Convert(remainder, variants);
                else
                    body = Convert(centuries, variants) + Separator + Convert(remainder, variants);
            }
            else
            {
                body = Convert(abs, variants);
            }

            if (isNegative && _yearFormat?.BeforeChristSuffix != null)
                return body + Separator + _yearFormat.BeforeChristSuffix;
            return isNegative ? Minus.Replace("*", body) : body;
        }

        /// <inheritdoc cref="INumberToStringConverter.ConvertFraction(BigInteger, BigInteger, string[])"/>
        public string ConvertFraction(BigInteger numerator, BigInteger denominator, params string[] variants)
            => BuildFractionText(numerator, denominator);

        /// <summary>
        /// Converts a multiplier to its spoken form (e.g. 2 → "twice", 3 → "trois fois").
        /// Named forms are looked up from the configuration; unnamed forms append <see cref="MultiplicativeSuffix"/>
        /// to the cardinal. Throws <see cref="NotSupportedException"/> when the language has no multiplicative configuration.
        /// </summary>
        public string ConvertMultiplicative(int multiplier, params string[] variants)
        {
            if (!SupportsMultiplicative)
                throw new NotSupportedException($"Language '{LanguageIdentifier}' has no multiplicative configuration.");
            if (Multiplicatives.TryGetValue(multiplier, out var named))
                return named;
            if (MultiplicativeSuffix != null)
                return Convert(multiplier, variants) + MultiplicativeSuffix;
            // No suffix and no named form — fall back to cardinal
            return Convert(multiplier, variants);
        }

        /// <summary>
        /// Transforms a cardinal string into its ordinal form by applying word-level rules
        /// or the ordinal suffix to the last word, optionally using a variant-specific override.
        /// </summary>
        private string ApplyOrdinalTransform(string cardinal, OrdinalVariantRule? activeVariant = null)
        {
            string? effectiveSuffix = activeVariant?.Suffix ?? OrdinalSuffix;
            string? effectiveRemoveTrailing = activeVariant?.RemoveTrailing ?? OrdinalRemoveTrailing;

            bool hasWordRules = (activeVariant != null && activeVariant.WordRules.Count > 0)
                                || OrdinalWordRules.Count > 0;
            if (!hasWordRules && effectiveSuffix == null) return cardinal;

            int lastSpace = cardinal.LastIndexOf(' ');
            string prefix = lastSpace >= 0 ? cardinal[..(lastSpace + 1)] : "";
            string lastComponent = lastSpace >= 0 ? cardinal[(lastSpace + 1)..] : cardinal;

            // Handle hyphenated last component (e.g. "twenty-one" → "twenty-first")
            int lastHyphen = lastComponent.LastIndexOf('-');
            string hyphenPrefix = lastHyphen >= 0 ? lastComponent[..(lastHyphen + 1)] : "";
            string lastWord = lastHyphen >= 0 ? lastComponent[(lastHyphen + 1)..] : lastComponent;

            // Variant word rules take priority over base word rules
            if (activeVariant?.WordRules.TryGetValue(lastWord, out var varReplacement) == true)
                return prefix + hyphenPrefix + varReplacement;
            if (OrdinalWordRules.TryGetValue(lastWord, out var replacement))
                return prefix + hyphenPrefix + replacement;

            if (effectiveSuffix != null)
            {
                string transformed = lastWord;
                if (!string.IsNullOrEmpty(effectiveRemoveTrailing) && transformed.EndsWith(effectiveRemoveTrailing))
                    transformed = transformed[..^effectiveRemoveTrailing.Length];
                return prefix + hyphenPrefix + transformed + effectiveSuffix;
            }

            return cardinal;
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
            // null (not set in XML) → default "lli"; explicit "" → no separator between prefix and suffix
            GroupSeparator = groupSeparator ?? "lli";

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

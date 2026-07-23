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
using Utils.Range;
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

            // Validate Group size: must be in the range [1, _decimalPowersOfTen.Length - 1].
            if (Group <= 0)
                throw new ArgumentOutOfRangeException(nameof(options.Group),
                    $"Group size must be positive; got {Group}.");
            if (Group >= _decimalPowersOfTen.Length)
                throw new ArgumentOutOfRangeException(nameof(options.Group),
                    $"Group size must be between 1 and {_decimalPowersOfTen.Length - 1}; got {Group}.");

            Separator = options.Separator ?? " ";
            GroupSeparator = options.GroupSeparator ?? "";
            Zero = options.Zero;
            Minus = options.Minus;
            DecimalSeparator = options.DecimalSeparator ?? ",";

            // Validate options.Groups before building the immutable snapshot so that failures
            // produce a precise diagnostic rather than a raw NullReferenceException from LINQ.
            ValidateGroupsSource(options.Groups!, nameof(options.Groups));

            Groups = options.Groups!.ToImmutableDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<long, DigitType>)kv.Value.Digits.ToDictionary(d => d.Digit).ToImmutableDictionary());
            Exceptions = (options.Exceptions ?? new Dictionary<long, string>()).ToImmutableDictionary();
            Replacements = (options.Replacements ?? Array.Empty<ReplacementRule>())
                .Select(r => r ?? throw new ArgumentNullException(nameof(options), "Replacement entries must not be null."))
                .ToImmutableArray();
            // Rules with onValue require numeric-value-aware evaluation and are excluded from the
            // fast-path dictionaries.
            var globalUnfiltered = Replacements.Where(r => r.OnScale is null && r.OnValue is null).ToImmutableArray();
            var duplicateKeys = globalUnfiltered
                .GroupBy(r => r.OldValue, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateKeys.Count > 0)
                throw new InvalidOperationException(
                    $"Duplicate exact replacement keys are not allowed. " +
                    $"Conflicting OldValue(s): {string.Join(", ", duplicateKeys.Select(k => $"'{k}'"))}.");
            _replacementLookup = globalUnfiltered.ToImmutableDictionary(r => r.OldValue, r => r.NewValue, StringComparer.Ordinal);
            _substringReplacements = globalUnfiltered.Where(r =>
                r.Scope is ReplacementScope.Anywhere or ReplacementScope.StartsWith or ReplacementScope.EndsWith)
                .ToImmutableArray();
            _valueFilteredGlobalReplacements = Replacements
                .Where(r => r.OnScale is null && r.OnValue is not null)
                .ToImmutableArray();
            _scaleReplacements = Replacements
                .Where(r => r.OnScale is not null)
                .ToImmutableArray();
            Scale = options.Scale;
            LanguageSpecifics = options.LanguageSpecifics ?? new DefaultNumberToStringLanguageSpecifics();
            LanguageIdentifier = options.LanguageIdentifier ?? string.Empty;
            _rawAdjustFunction = options.AdjustFunction;
            AdjustFunction = input => LanguageSpecifics.FinalizeWriting(LanguageIdentifier, (_rawAdjustFunction ?? (s => s))(input));
            Fractions = options.Fractions?.ToImmutableDictionary() ?? ImmutableDictionary<int, string>.Empty;
            MaxNumber = options.MaxNumber;
            if (MaxNumber.HasValue && MaxNumber.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(options.MaxNumber),
                    $"MaxNumber must be non-negative when specified; got {MaxNumber.Value}.");
            FractionSeparator = string.IsNullOrWhiteSpace(options.FractionSeparator) ? "/" : options.FractionSeparator;

            OrdinalSuffix = options.OrdinalSuffix;
            OrdinalRemoveTrailing = options.OrdinalRemoveTrailing;
            OrdinalExceptions = (options.OrdinalExceptions ?? new Dictionary<long, string>()).ToImmutableDictionary();
            OrdinalWordRules = (options.OrdinalWordRules ?? new Dictionary<string, string>()).ToImmutableDictionary();
            OrdinalPrefix = options.OrdinalPrefix;
            OrdinalVariants = (options.OrdinalVariants ?? []).ToImmutableArray();

            VariantDimensions = (options.VariantDimensions ?? []).ToImmutableArray();
            VariantRules = (options.VariantRules ?? []).ToImmutableArray();
            _sortedVariantRules = [.. VariantRules.OrderBy(r => r.Specificity)];
            _hasScaleSpecificVariantRules = VariantRules.Any(r => r.Replacements.Any(rr => rr.OnScale is not null));
            Triggers = (options.Triggers ?? []).ToImmutableArray();
            _yearFormat = options.YearFormat;
            Multiplicatives = options.Multiplicatives?.ToImmutableDictionary() ?? ImmutableDictionary<int, string>.Empty;
            MultiplicativeSuffix = options.MultiplicativeSuffix;
            _groupConnector = options.GroupConnector;
            _groupConnectorThreshold = options.GroupConnectorThreshold;
            _intraGroupConnector = options.IntraGroupConnector;
            _intraGroupConnectorThreshold = options.IntraGroupConnectorThreshold;
            _scaleConnector = options.ScaleConnector;
            _scaleConnectorThreshold = options.ScaleConnectorThreshold;
            _timeUnits = options.TimeUnits?.ToImmutableDictionary()
                         ?? ImmutableDictionary<string, (string Singular, string Plural, string? Count1Form)>.Empty;
            _datePattern = options.DatePattern;
            _dateFirstDay = options.DateFirstDay;
            _dateTimeConnector = options.DateTimeConnector;
            // Index both canonical name and localName so both are accepted in API calls and XML constraints.
            _dimensionIndex = VariantDimensions
                .SelectMany(d => string.IsNullOrEmpty(d.LocalName)
                    ? (IEnumerable<(string, VariantDimension)>)[(d.Name, d)]
                    : [(d.Name, d), (d.LocalName, d)])
                .ToImmutableDictionary(t => t.Item1, t => t.Item2, StringComparer.OrdinalIgnoreCase);
            ValidateVariantReferences(this, LanguageIdentifier.Length > 0 ? LanguageIdentifier : "programmatic");
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

        /// <summary>Gets the intra-group connector inserted between hundreds and lower parts (e.g. "linh" for VN).</summary>
        public string? IntraGroupConnector => _intraGroupConnector;

        /// <summary>Gets the threshold below which the intra-group connector is inserted.</summary>
        public int IntraGroupConnectorThreshold => (int)_intraGroupConnectorThreshold;

        /// <summary>Gets the connector inserted between a group's digit text and the scale name when the group value ≥ threshold (e.g. "de" for Romanian).</summary>
        public string? ScaleConnector => _scaleConnector;

        /// <summary>Gets the threshold at or above which <see cref="ScaleConnector"/> is inserted.</summary>
        public int ScaleConnectorThreshold => (int)_scaleConnectorThreshold;

        /// <inheritdoc/>
        public bool SupportsTimeConversion => _timeUnits.Count > 0;

        /// <inheritdoc/>
        public bool SupportsDateConversion => _datePattern != null;

        /// <summary>Gets the time units for time conversion (hours, minutes, seconds).</summary>
        public IReadOnlyDictionary<string, (string Singular, string Plural, string? Count1Form)> TimeUnits => _timeUnits;

        /// <summary>Gets the date pattern string (tokens: {month}, {ordinal-day}, {cardinal-day}, {year}).</summary>
        public string? DatePattern => _datePattern;

        /// <summary>Gets the special string for day 1 in date ordinal rendering (e.g. "premier" in French).</summary>
        public string? DateFirstDay => _dateFirstDay;

        /// <summary>Gets the connector inserted between date and time in DateTime conversion.</summary>
        public string? DateTimeConnector => _dateTimeConnector;

        /// <inheritdoc/>
        public bool SupportsOrdinals =>
            OrdinalSuffix != null || OrdinalPrefix != null
            || OrdinalExceptions.Count > 0
            || OrdinalWordRules.Count > 0
            || OrdinalVariants.Count > 0;

        private readonly ImmutableDictionary<string, string> _replacementLookup;
        private readonly ImmutableArray<ReplacementRule> _substringReplacements;
        private readonly ImmutableArray<ReplacementRule> _valueFilteredGlobalReplacements;
        private readonly ImmutableArray<ReplacementRule> _scaleReplacements;
        private readonly bool _hasScaleSpecificVariantRules;
        private readonly ImmutableArray<VariantRule> _sortedVariantRules;
        private readonly ImmutableDictionary<string, VariantDimension> _dimensionIndex;
        private readonly Func<string, string>? _rawAdjustFunction;
        private readonly string? _groupConnector;
        private readonly long _groupConnectorThreshold;
        private readonly string? _intraGroupConnector;
        private readonly long _intraGroupConnectorThreshold;
        private readonly string? _scaleConnector;
        private readonly long _scaleConnectorThreshold;
        private readonly ImmutableDictionary<string, (string Singular, string Plural, string? Count1Form)> _timeUnits;
        private readonly string? _datePattern;
        private readonly string? _dateFirstDay;
        private readonly string? _dateTimeConnector;

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
        /// Default timeout applied to compiled trigger regular expressions to prevent
        /// catastrophic-backtracking patterns from consuming unbounded CPU.
        /// </summary>
        public static TimeSpan RegexTimeout { get; set; } = TimeSpan.FromSeconds(1);

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
                if (string.IsNullOrEmpty(from))
                    throw new ArgumentException(
                        "Trigger source pattern must not be null or empty.", nameof(from));
                if (forms is null)
                    throw new ArgumentNullException(nameof(forms),
                        "Trigger forms collection must not be null (use an empty list when no variant forms are needed).");
                foreach (var form in forms)
                {
                    if (form.Constraints is null)
                        throw new ArgumentException(
                            "Each trigger form's Constraints must not be null.", nameof(forms));
                    if (form.To is null)
                        throw new ArgumentException(
                            "Each trigger form's To value must not be null.", nameof(forms));
                }

                From = from;
                IsRegex = isRegex;
                if (isRegex)
                {
                    try
                    {
                        CompiledRegex = new Regex(from, RegexOptions.Compiled, RegexTimeout);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException(
                            $"Trigger pattern '{from}' is not a valid regular expression: {ex.Message}", nameof(from), ex);
                    }
                }
                Forms = forms
                    .Select(static form => (
                        Constraints: (IReadOnlyDictionary<string, string>)
                            form.Constraints.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
                        form.To))
                    .ToImmutableArray();
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
                if (replaces is null)
                    throw new ArgumentNullException(nameof(replaces),
                        "TriggerRule replaces collection must not be null (use an empty list when no replacements are needed).");
                if (groupIndices is not null)
                {
                    foreach (int idx in groupIndices)
                        if (idx < 0)
                            throw new ArgumentException(
                                $"All group indices must be non-negative; found {idx}.", nameof(groupIndices));
                }
                ExecuteAt = executeAt;
                _groupIndices = groupIndices?.ToImmutableArray();
                Replaces = replaces.ToImmutableArray();
            }
            private readonly ImmutableArray<int>? _groupIndices;
            /// <summary>Gets when this trigger fires.</summary>
            public TriggerAt ExecuteAt { get; }
            /// <summary>Gets the group indices this trigger is restricted to, or <see langword="null"/> for all groups.</summary>
            public int[]? GroupIndices => _groupIndices?.ToArray();
            /// <summary>Gets the replacement rules applied when this trigger fires.</summary>
            public IReadOnlyList<TriggerReplace> Replaces { get; }
            /// <summary>Returns <see langword="true"/> when this trigger applies to the given group index.</summary>
            internal bool AppliesToGroup(int? groupIndex) =>
                !_groupIndices.HasValue ||
                (groupIndex.HasValue && _groupIndices.Value.Contains(groupIndex.Value));
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
            if (mandatoryDecimalDigits > 28)
                throw new ArgumentOutOfRangeException(nameof(mandatoryDecimalDigits),
                    $"mandatoryDecimalDigits must be between 0 and 28 (the maximum decimal precision); got {mandatoryDecimalDigits}.");

            bool isNegative = number < 0;
            // Avoid unary negation of decimal.MinValue by using decimal.Negate.
            if (isNegative) number = decimal.Negate(number);

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
                // Use BigInteger to avoid long overflow when the integer part exceeds long.MaxValue.
                BigInteger integerBig = (BigInteger)integerPart;
                long separatorPluralProxy = BigInteger.Abs(integerBig) <= 1 ? (long)integerBig : 2L;
                result.Append(Separator).Append(separatorWord.ToPlural(separatorPluralProxy)).Append(Separator);

                Fractions.TryGetValue(digits.Length, out var configuredSuffix);
                string? activeSuffix = options?.DecimalSuffix ?? configuredSuffix;

                if (activeSuffix != null)
                {
                    BigInteger fracNumerator = BigInteger.Parse(digits);
                    var valueText = Convert(fracNumerator, variants).Replace("-", " ");
                    // Use BigInteger to avoid long overflow when fractional digits exceed 18.
                    long fracPluralProxy = fracNumerator <= 1 ? (long)fracNumerator : 2L;
                    result.Append(valueText).Append(Separator).Append(activeSuffix.ToPlural(fracPluralProxy));
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

            string final = AdjustFunction(result.ToString().Trim());
            return isNegative ? Minus.Replace("*", final) : final;
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

            string adjusted = AdjustFunction(final.Trim());
            return isNegative ? Minus.Replace("*", adjusted) : adjusted;
        }

        /// <inheritdoc cref="INumberToStringConverter.Convert(double)"/>
        public string Convert(double number) => Convert(number, []);

        /// <inheritdoc cref="INumberToStringConverter.Convert(double, string[])"/>
        public string Convert(double number, params string[] variants)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
                throw new ArgumentException($"Cannot convert non-finite value '{number}' to a number in words.", nameof(number));

            if (decimal.TryParse(
                    number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal d))
                return Convert(d, variants);

            // The value is finite but outside the decimal range.
            // Silent truncation of the fractional part would silently change the value semantics,
            // so we throw rather than produce an integer approximation.
            throw new ArgumentOutOfRangeException(nameof(number),
                $"The value '{number}' is outside the supported decimal range and cannot be converted exactly. " +
                "Use a BigInteger overload for integer-only conversion of large values.");
        }

        /// <inheritdoc cref="INumberToStringConverter.Convert(float)"/>
        public string Convert(float number) => Convert((double)number, []);

        /// <inheritdoc cref="INumberToStringConverter.Convert(float, string[])"/>
        public string Convert(float number, params string[] variants)
            => Convert((double)number, variants);

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

            bool isNegative = number.Sign == -1;
            BigInteger abs = isNegative ? BigInteger.Abs(number) : number;

            var query = BuildVariantQuery(variants);
            // Zero goes through the same post-processing pipeline as non-zero values so that
            // variant rules, end triggers, and language finalization can be applied to "zero".
            string raw = abs == BigInteger.Zero ? Zero : ConvertRaw(abs, query);
            if (_rawAdjustFunction != null) raw = _rawAdjustFunction(raw);
            raw = ApplyVariantRules(raw, query, abs <= long.MaxValue ? (long?)abs : null);
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

                    string scaleName = Scale.GetScaleName(groupNumber).ToPlural(group);
                    string scaleJoin = (_scaleConnector != null && groupNumber > 0 && group >= _scaleConnectorThreshold)
                        ? Separator + _scaleConnector + Separator
                        : Separator;
                    // Only append the separator+scale when there is actually a scale name.
                    // If scaleName is empty (group 0 / units), appending scaleJoin would leave a
                    // dangling separator that post-hoc trimming cannot remove safely (e.g. when
                    // Separator="and", the word "thousand" ends with "and" and would be corrupted).
                    string resValue = string.IsNullOrEmpty(scaleName)
                        ? digits
                        : digits + scaleJoin + scaleName;
                    resValue = ApplyReplacements(resValue, groupNumber, group);
                    if (variantQuery != null && _hasScaleSpecificVariantRules)
                        resValue = ApplyVariantRulesForScale(resValue, variantQuery, groupNumber, group);
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

            return ApplyReplacements(result.ToString(), numericValue: abs <= long.MaxValue ? (long?)abs : null);
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
        /// Applies variant rules to <paramref name="text"/> in order of ascending specificity
        /// (pre-sorted once at construction time in <see cref="_sortedVariantRules"/>).
        /// A rule matches when all its dimension constraints are satisfied by <paramref name="query"/>.
        /// Two mutually exclusive modes are supported via <paramref name="scaleGroupNumber"/>:
        /// global replacements (no <c>onScale</c>) when <see langword="null"/>, or scale-specific
        /// replacements for a single group when set (used by <see cref="ApplyVariantRulesForScale"/>).
        /// </summary>
        /// <param name="text">The already-converted text to apply variant replacements to.</param>
        /// <param name="query">The active variant dimension values (e.g. gender, case) to match rules against.</param>
        /// <param name="numericValue">
        /// The full absolute number value, used to evaluate <see cref="ReplacementRule.OnValue"/>
        /// predicates on global (no <c>onScale</c>) variant replacements. Ignored in scale mode.
        /// <see langword="null"/> suppresses <c>onValue</c> filtering (rule fires regardless).
        /// </param>
        /// <param name="scaleGroupNumber">
        /// When set, restricts matching to scale-specific replacements whose <c>onScale</c> range
        /// contains this group number (scale mode). When <see langword="null"/>, only global
        /// (non-scale) replacements are applied.
        /// </param>
        /// <param name="scaleGroupValue">
        /// The numeric value of the current group in scale mode (e.g. 1 for the thousands group
        /// in 1 000), evaluated against <see cref="ReplacementRule.OnValue"/>.
        /// </param>
        private string ApplyVariantRules(string text, IReadOnlyDictionary<string, string> query, long? numericValue = null, int? scaleGroupNumber = null, long scaleGroupValue = 0)
        {
            if (VariantRules.Count == 0 || query.Count == 0) return text;

            foreach (var rule in _sortedVariantRules)
            {
                bool matches = rule.Constraints.All(c =>
                    query.TryGetValue(c.Key, out var v) &&
                    string.Equals(v, c.Value, StringComparison.OrdinalIgnoreCase));

                if (!matches) continue;

                foreach (var replacement in rule.Replacements)
                {
                    if (scaleGroupNumber is int groupNumber)
                    {
                        if (replacement.OnScale is null || !replacement.OnScale.Contains(groupNumber)) continue;
                        if (replacement.OnValue is not null && !replacement.OnValue.Contains(scaleGroupValue)) continue;
                    }
                    else
                    {
                        if (_hasScaleSpecificVariantRules && replacement.OnScale is not null) continue;
                        if (replacement.OnValue is not null && (numericValue is null || !replacement.OnValue.Contains(numericValue.Value)))
                            continue;
                    }
                    text = ApplyVariantReplacement(text, replacement);
                }
            }

            return text;
        }

        /// <summary>
        /// Applies scale-specific variant replacements for <paramref name="groupNumber"/> only.
        /// Called per-group inside <see cref="ConvertRaw"/> when scale-specific variant rules exist.
        /// Thin wrapper over <see cref="ApplyVariantRules"/> in scale mode.
        /// </summary>
        /// <param name="text">The already-converted text to apply variant replacements to.</param>
        /// <param name="query">The active variant dimension values (e.g. gender, case) to match rules against.</param>
        /// <param name="groupNumber">The scale group number the replacements must target via <c>onScale</c>.</param>
        /// <param name="groupNumericValue">
        /// The numeric value of the current group (e.g. 1 for the thousands group in 1 000).
        /// Used to evaluate <see cref="ReplacementRule.OnValue"/> predicates on scale-specific
        /// variant replacements.
        /// </param>
        private string ApplyVariantRulesForScale(string text, IReadOnlyDictionary<string, string> query, int groupNumber, long groupNumericValue = 0) =>
            ApplyVariantRules(text, query, scaleGroupNumber: groupNumber, scaleGroupValue: groupNumericValue);

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
                if (!trigger.AppliesToGroup(groupIndex)) continue;

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

            if (replace.CompiledRegex != null)
            {
                try
                {
                    return replace.CompiledRegex.Replace(text, effectiveTo);
                }
                catch (RegexMatchTimeoutException ex)
                {
                    throw new InvalidOperationException(
                        $"Trigger pattern '{replace.From}' timed out after {RegexTimeout.TotalMilliseconds:0}ms. " +
                        "Consider simplifying the pattern or increasing RegexTimeout.", ex);
                }
            }
            return text.Replace(replace.From, effectiveTo, StringComparison.Ordinal);
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
        /// When <paramref name="groupNumber"/> is supplied, scale-specific rules for that
        /// level are applied first (before global rules).
        /// </summary>
        /// <param name="value">The text to transform.</param>
        /// <param name="groupNumber">
        /// Current scale group index, or <see langword="null"/> for the final combined pass.
        /// 0 = units, 1 = thousands, 2 = millions, etc.
        /// </param>
        /// <param name="numericValue">
        /// For per-group calls: the numeric value of the current group (e.g. 1 for the
        /// thousands group in 1 000). For the final pass: the full absolute number value.
        /// Used to evaluate <see cref="ReplacementRule.OnValue"/> predicates.
        /// <see langword="null"/> suppresses <c>onValue</c> filtering (rule fires regardless).
        /// </param>
        /// <returns>The value after applying any matching replacements.</returns>
        private string ApplyReplacements(string value, int? groupNumber = null, long? numericValue = null)
        {
            if (string.IsNullOrEmpty(value) || Replacements.Count == 0)
                return value;

            if (groupNumber.HasValue && _scaleReplacements.Length > 0)
            {
                foreach (var rule in _scaleReplacements)
                {
                    if (!rule.OnScale!.Contains(groupNumber.Value)) continue;
                    if (rule.OnValue is not null && (numericValue is null || !rule.OnValue.Contains(numericValue.Value)))
                        continue;
                    value = ApplyVariantReplacement(value, rule);
                }
            }

            // Value-filtered global rules (onValue without onScale): fire only in the final
            // assembled-string pass (groupNumber == null). In the per-group pass numericValue
            // is the group digit value, not the full number — applying here would contradict the
            // documented semantics where the value is the full absolute number.
            if (!groupNumber.HasValue && _valueFilteredGlobalReplacements.Length > 0 && numericValue is not null)
            {
                foreach (var rule in _valueFilteredGlobalReplacements)
                {
                    if (rule.OnValue!.Contains(numericValue.Value))
                        value = ApplyVariantReplacement(value, rule);
                }
            }

            if (_replacementLookup.TryGetValue(value, out var exactMatch))
                return exactMatch;

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
            /// <param name="onScale">
            /// Scale level(s) this rule is restricted to, or <see langword="null"/> for all levels.
            /// 0 = units, 1 = thousands, 2 = millions, etc. Supports range syntax ("1..3", "2,4..").
            /// </param>
            /// <exception cref="ArgumentException">Thrown when <paramref name="oldValue"/> or <paramref name="newValue"/> is null or empty.</exception>
            public ReplacementRule(string oldValue, string newValue, ReplacementScope scope, int? onScale = null)
                : this(oldValue, newValue, scope, onScale.HasValue ? new IntRange<long>(onScale.Value.ToString()) : null, null) { }

            /// <summary>
            /// Initializes a new instance of the <see cref="ReplacementRule"/> class with a
            /// numeric-value filter.
            /// </summary>
            /// <param name="oldValue">The text that should be replaced.</param>
            /// <param name="newValue">The replacement text.</param>
            /// <param name="scope">The scope that controls how the replacement is applied.</param>
            /// <param name="onScale">
            /// Scale level(s) this rule is restricted to, or <see langword="null"/> for all levels.
            /// 0 = units, 1 = thousands, 2 = millions, etc. Supports range syntax ("1..3", "2,4..").
            /// </param>
            /// <param name="onValue">
            /// Optional numeric-value filter. When set with <paramref name="onScale"/>, the rule
            /// only fires when the group's numeric value falls within this range. When set without
            /// <paramref name="onScale"/>, applies to the full number in the final assembled-string pass.
            /// </param>
            /// <exception cref="ArgumentException">Thrown when <paramref name="oldValue"/> or <paramref name="newValue"/> is null or empty.</exception>
            public ReplacementRule(string oldValue, string newValue, ReplacementScope scope, IntRange<long>? onScale, IntRange<long>? onValue)
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
                OnScale = onScale;
                OnValue = onValue;
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

            /// <summary>
            /// Gets the scale level(s) this rule is restricted to, or <see langword="null"/> to apply globally.
            /// 0 = units group, 1 = thousands, 2 = millions, etc. Supports ranges ("1..3").
            /// </summary>
            public IntRange<long>? OnScale { get; }

            /// <summary>
            /// Gets the optional numeric-value filter. When non-null and combined with
            /// <see cref="OnScale"/>, the rule only fires when the group's numeric value is
            /// within range. When non-null without <see cref="OnScale"/>, constrains the rule
            /// to matching values in the final assembled-string pass.
            /// </summary>
            public IntRange<long>? OnValue { get; }
        }

        /// <summary>
        /// Parses a comma-separated range expression in the XML attribute syntax and returns
        /// an <see cref="IntRange{T}">IntRange&lt;long&gt;</see> that can be used with
        /// <see cref="IntRange{T}.Contains(T)"/>.
        /// </summary>
        /// <remarks>
        /// Accepted segment syntax (comma-separated list):
        /// <list type="bullet">
        ///   <item><c>5</c> — exact value 5</item>
        ///   <item><c>1..3</c> — inclusive range [1, 3]</item>
        ///   <item><c>..5</c> — all values ≤ 5 (open lower bound)</item>
        ///   <item><c>5..</c> — all values ≥ 5 (open upper bound)</item>
        /// </list>
        /// Example: <c>"1,5..10,20.."</c> matches 1, five through ten, and any value ≥ 20.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is null or whitespace.</exception>
        /// <exception cref="FormatException">Thrown when a segment cannot be parsed.</exception>
        public static IntRange<long> ParseRangeExpression(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Range expression must not be empty.", nameof(s));

            // IntRange<T> constructor expects "min-max" with "∞" for open bounds.
            // Translate our ".." syntax accordingly.
            var parts = s.Split(',');
            var converted = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                int dotIdx = part.IndexOf("..", StringComparison.Ordinal);
                if (dotIdx >= 0)
                {
                    var min = part[..dotIdx].Trim();
                    var max = part[(dotIdx + 2)..].Trim();
                    converted[i] = $"{(min.Length > 0 ? min : "∞")}-{(max.Length > 0 ? max : "∞")}";
                }
                else
                {
                    converted[i] = part;
                }
            }
            return new IntRange<long>(string.Join(",", converted));
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
            if (groupNumber < 0)
                throw new ArgumentOutOfRangeException(nameof(groupNumber),
                    $"groupNumber must be non-negative; got {groupNumber}.");
            if (number < 0)
                throw new ArgumentOutOfRangeException(nameof(number),
                    $"number must be non-negative; got {number}.");
            if (groupNumber == 0) return string.Empty;
            if (!Groups.ContainsKey(groupNumber))
                throw new ArgumentOutOfRangeException(nameof(groupNumber),
                    $"groupNumber {groupNumber} is not a configured group index. " +
                    $"Valid indices: {string.Join(", ", Groups.Keys.OrderBy(k => k))}.");

            if (groupNumber > 1 && Exceptions.TryGetValue(number, out var value)) return value;

            // Use exact integer power from the pre-computed table to avoid floating-point imprecision.
            int powerIndex = groupNumber - 1;
            if (powerIndex >= _decimalPowersOfTen.Length)
                throw new ArgumentOutOfRangeException(nameof(groupNumber),
                    $"groupNumber {groupNumber} exceeds the supported maximum of {_decimalPowersOfTen.Length}.");
            long group = _decimalPowersOfTen[powerIndex];

            // Reject values that exceed the range representable by this group.
            // _decimalPowersOfTen[groupNumber] is the exclusive upper bound (e.g., 10 for group 1, 100 for group 2).
            // For the last supported group (groupNumber == _decimalPowersOfTen.Length) the bound would be 10^19,
            // which exceeds long, so only the non-negative check above applies.
            if (groupNumber < _decimalPowersOfTen.Length)
            {
                long upperExclusive = _decimalPowersOfTen[groupNumber];
                if (number >= upperExclusive)
                    throw new ArgumentOutOfRangeException(nameof(number),
                        $"number must be in the range [0, {upperExclusive - 1}] for groupNumber {groupNumber}; got {number}.");
            }
            var (groupValue, remainder) = long.DivRem(number, group);

            var leftText = ConvertGroup(groupNumber - 1, remainder);
            var valueText = Groups[groupNumber][groupValue];

            if (string.IsNullOrEmpty(leftText)) return valueText.StringValue;

            // Inject intra-group connector at the hundreds level when there are hundreds AND remainder < threshold
            if (groupNumber == 3 && _intraGroupConnector != null && groupValue > 0 && remainder > 0 && remainder < _intraGroupConnectorThreshold)
                return valueText.BuildString.Replace("*", _intraGroupConnector + Separator + leftText);

            return valueText.BuildString.Replace("*", leftText);
        }

        /// <summary>
        /// Builds the textual representation of a fraction using existing conversion helpers.
        /// </summary>
        /// <param name="numerator">The numerator of the fraction.</param>
        /// <param name="denominator">The denominator of the fraction.</param>
        /// <param name="allowFractionNames">When <see langword="true"/>, uses named fraction suffixes when available.</param>
        /// <param name="variants">Optional variant dimension values (e.g. gender) forwarded to the numerator/denominator conversions.</param>
        /// <returns>The textual representation of the fraction.</returns>
        private string BuildFractionText(BigInteger numerator, BigInteger denominator, bool allowFractionNames = true, string[]? variants = null)
        {
            if (allowFractionNames &&
                TryGetBase10FractionDigits(denominator, out int digits) &&
                Fractions.TryGetValue(digits, out var suffix) &&
                BigInteger.Abs(numerator) <= long.MaxValue)
            {
                string valueText = (variants is { Length: > 0 } ? Convert(numerator, variants) : Convert(numerator)).Replace("-", " ");
                return string.Concat(valueText, Separator, suffix.ToPlural((long)BigInteger.Abs(numerator))).Trim();
            }

            string numeratorText = (variants is { Length: > 0 } ? Convert(numerator, variants) : Convert(numerator)).Replace("-", " ");
            string denominatorText = (variants is { Length: > 0 } ? Convert(denominator, variants) : Convert(denominator)).Replace("-", " ");

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
            if (number == long.MinValue)
                throw new ArgumentOutOfRangeException(nameof(number),
                    "long.MinValue cannot be converted to ordinal because its absolute value exceeds long.MaxValue.");
            bool isNegative = number < 0;
            long absNumber = Math.Abs(number);
            var activeVariants = BuildVariantQuery(variants);

            if (LanguageSpecifics is IOrdinalLanguageSpecifics ordinalPlugin
                && ordinalPlugin.TryConvertOrdinal(absNumber, activeVariants, out var pluginResult))
                return isNegative ? Minus.Replace("*", pluginResult!) : pluginResult!;

            // Find the most specific matching ordinal variant
            OrdinalVariantRule? activeVariant = FindBestOrdinalVariant(activeVariants);

            // Exceptions: variant first, then base
            if (activeVariant?.Exceptions.TryGetValue(absNumber, out var varException) == true)
                return isNegative ? Minus.Replace("*", varException) : varException;
            if (OrdinalExceptions.TryGetValue(absNumber, out var exception))
                return isNegative ? Minus.Replace("*", exception) : exception;

            string raw = absNumber == 0 ? Zero : ConvertRaw((BigInteger)absNumber, activeVariants);
            raw = ApplyVariantRules(raw, activeVariants, absNumber);
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
        public string ConvertOrdinal(BigInteger number)
        {
            if (number < long.MinValue || number > long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(number),
                    $"Ordinal conversion is limited to the range [{long.MinValue}, {long.MaxValue}]. " +
                    $"The supplied value {number} is outside that range.");
            return ConvertOrdinal((long)number, []);
        }

        /// <inheritdoc cref="INumberToStringConverter.ConvertOrdinal(BigInteger, string[])"/>
        public string ConvertOrdinal(BigInteger number, params string[] variants)
        {
            if (number < long.MinValue || number > long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(number),
                    $"Ordinal conversion is limited to the range [{long.MinValue}, {long.MaxValue}]. " +
                    $"The supplied value {number} is outside that range.");
            return ConvertOrdinal((long)number, variants);
        }

        /// <inheritdoc cref="INumberToStringConverter.ConvertCurrency(decimal, CurrencyDefinition)"/>
        public string ConvertCurrency(decimal amount, CurrencyDefinition currency)
            => ConvertCurrency(amount, currency, []);

        /// <inheritdoc cref="INumberToStringConverter.ConvertCurrency(decimal, CurrencyDefinition, string[])"/>
        public string ConvertCurrency(decimal amount, CurrencyDefinition currency, params string[] variants)
        {
            ArgumentNullException.ThrowIfNull(currency);
            if (currency.SubunitDigits < 0 || currency.SubunitDigits > 18)
                throw new ArgumentOutOfRangeException(nameof(currency),
                    $"SubunitDigits must be between 0 and 18 (inclusive); got {currency.SubunitDigits}.");

            bool isNegative = amount < 0;
            // Avoid unary negation of extreme values: extract sign and magnitude separately.
            decimal absAmount = isNegative ? decimal.Negate(amount) : amount;

            // Use BigInteger for the unit part so amounts above long.MaxValue are supported.
            BigInteger units = (BigInteger)decimal.Truncate(absAmount);
            decimal fractional = absAmount - (decimal)units;

            // Compute subunit factor with exact decimal arithmetic instead of Math.Pow (double).
            long subunitFactor = _decimalPowersOfTen[currency.SubunitDigits];
            long subunits = (long)decimal.Round(fractional * subunitFactor, MidpointRounding.AwayFromZero);

            // Carry: rounding may push subunits to subunitFactor (e.g. 1.999m → subunits=100).
            units += subunits / subunitFactor;
            subunits %= subunitFactor;

            string unitName = units == BigInteger.One ? currency.UnitSingular : currency.UnitPlural;
            string result = Convert(units, variants) + Separator + unitName;

            if (subunits > 0)
            {
                string subunitName = subunits == 1 ? currency.SubunitSingular : currency.SubunitPlural;
                string subunitsText = Convert(subunits, variants) + Separator + subunitName;
                result = result + Separator + currency.Connector + Separator + subunitsText;
            }

            return isNegative ? Minus.Replace("*", result) : result;
        }

        /// <summary>
        /// Validates the source groups dictionary before it is materialised into an immutable snapshot,
        /// so that null entries, structural gaps, and duplicate digit values produce precise diagnostics
        /// rather than NullReferenceException or ArgumentException from LINQ.
        /// </summary>
        private static void ValidateGroupsSource(IReadOnlyDictionary<int, DigitListType> src, string paramName)
        {
            if (src.Count == 0)
                throw new ArgumentException("Groups must contain at least one group.", paramName);

            var groupKeys = src.Keys.OrderBy(k => k).ToList();
            if (groupKeys[0] < 1)
                throw new ArgumentException(
                    $"Group keys must be positive integers; the smallest key is {groupKeys[0]}.", paramName);
            for (int gi = 1; gi < groupKeys.Count; gi++)
                if (groupKeys[gi] != groupKeys[gi - 1] + 1)
                    throw new ArgumentException(
                        $"Group keys must form a contiguous sequence; gap between {groupKeys[gi - 1]} and {groupKeys[gi]}.",
                        paramName);
            int maxGroupKey = groupKeys[groupKeys.Count - 1];
            if (maxGroupKey > _decimalPowersOfTen.Length)
                throw new ArgumentException(
                    $"The maximum group key ({maxGroupKey}) exceeds the supported limit of {_decimalPowersOfTen.Length}; " +
                    $"provide a smaller number of groups or add a custom power table.", paramName);

            foreach (var (key, digitList) in src)
            {
                if (digitList is null)
                    throw new ArgumentException($"Group {key} has a null DigitListType.", paramName);
                if (digitList.Digits is null)
                    throw new ArgumentException($"Group {key} has a null Digits list.", paramName);
                if (digitList.Digits.Count == 0)
                    throw new ArgumentException($"Group {key} has no digit definitions.", paramName);

                var digitValues = new HashSet<long>();
                for (int i = 0; i < digitList.Digits.Count; i++)
                {
                    var dt = digitList.Digits[i];
                    if (dt is null)
                        throw new ArgumentException(
                            $"Group {key} has a null DigitType at index {i}.", paramName);
                    if (!digitValues.Add(dt.Digit))
                        throw new ArgumentException(
                            $"Group {key} has duplicate digit value {dt.Digit}.", paramName);
                }
            }
        }

        // Powers of ten from 10^0 to 10^18, computed with exact decimal arithmetic.
        private static readonly long[] _decimalPowersOfTen =
        [
            1L, 10L, 100L, 1_000L, 10_000L, 100_000L, 1_000_000L, 10_000_000L,
            100_000_000L, 1_000_000_000L, 10_000_000_000L, 100_000_000_000L,
            1_000_000_000_000L, 10_000_000_000_000L, 100_000_000_000_000L,
            1_000_000_000_000_000L, 10_000_000_000_000_000L, 100_000_000_000_000_000L,
            1_000_000_000_000_000_000L,
        ];

        /// <inheritdoc cref="INumberToStringConverter.ConvertYear(int)"/>
        public string ConvertYear(int year) => ConvertYear(year, []);

        /// <inheritdoc cref="INumberToStringConverter.ConvertYear(int, string[])"/>
        public string ConvertYear(int year, params string[] variants)
        {
            if (year == int.MinValue)
                throw new ArgumentOutOfRangeException(nameof(year),
                    "int.MinValue cannot be converted because its absolute value exceeds int.MaxValue.");
            bool isNegative = year < 0;
            // Use long to safely compute abs of any int value including int.MinValue+1..int.MaxValue.
            int abs = (int)Math.Abs((long)year);

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
        {
            if (denominator == BigInteger.Zero)
                throw new ArgumentOutOfRangeException(nameof(denominator), "Denominator must not be zero.");

            // Normalize: ensure denominator is positive; move sign to numerator.
            if (denominator < BigInteger.Zero)
            {
                numerator = BigInteger.Negate(numerator);
                denominator = BigInteger.Negate(denominator);
            }

            return BuildFractionText(numerator, denominator, allowFractionNames: true, variants);
        }

        /// <inheritdoc cref="INumberToStringConverter.ConvertFraction(int, int, string[])"/>
        public string ConvertFraction(int numerator, int denominator, params string[] variants)
            => ConvertFraction((BigInteger)numerator, (BigInteger)denominator, variants);

        /// <inheritdoc cref="INumberToStringConverter.ConvertFraction(long, long, string[])"/>
        public string ConvertFraction(long numerator, long denominator, params string[] variants)
            => ConvertFraction((BigInteger)numerator, (BigInteger)denominator, variants);

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

        private string FormatTimeUnit(int count, (string Singular, string Plural, string? Count1Form) unit, string[] variants)
        {
            string word = count == 1 ? unit.Singular : unit.Plural;
            string numeral = (count == 1 && unit.Count1Form != null) ? unit.Count1Form : Convert(count, variants);
            return numeral + Separator + word;
        }

        /// <inheritdoc cref="INumberToStringConverter.Convert(TimeSpan, string[])"/>
        public string Convert(TimeSpan duration, params string[] variants)
        {
            if (!SupportsTimeConversion)
                throw new NotSupportedException($"Language '{LanguageIdentifier}' has no <TimeUnits> configuration.");

            if (duration == TimeSpan.MinValue)
                throw new ArgumentOutOfRangeException(nameof(duration),
                    "TimeSpan.MinValue cannot be converted because its negation overflows.");
            var abs = duration < TimeSpan.Zero ? -duration : duration;
            var parts = new List<string>();
            int totalHours = (int)(abs.Days * 24 + abs.Hours);

            if (totalHours > 0)
            {
                if (!_timeUnits.TryGetValue("hour", out var h))
                    throw new InvalidOperationException(
                        $"Language '{LanguageIdentifier}' has a non-zero hours component but no 'hour' unit configured in <TimeUnits>.");
                parts.Add(FormatTimeUnit(totalHours, h, variants));
            }
            if (abs.Minutes > 0)
            {
                if (!_timeUnits.TryGetValue("minute", out var m))
                    throw new InvalidOperationException(
                        $"Language '{LanguageIdentifier}' has a non-zero minutes component but no 'minute' unit configured in <TimeUnits>.");
                parts.Add(FormatTimeUnit(abs.Minutes, m, variants));
            }
            if (abs.Seconds > 0)
            {
                if (!_timeUnits.TryGetValue("second", out var s))
                    throw new InvalidOperationException(
                        $"Language '{LanguageIdentifier}' has a non-zero seconds component but no 'second' unit configured in <TimeUnits>.");
                parts.Add(FormatTimeUnit(abs.Seconds, s, variants));
            }

            string body = parts.Count > 0 ? string.Join(Separator, parts) : Zero;
            return duration < TimeSpan.Zero ? Minus.Replace("*", body) : body;
        }

        /// <inheritdoc cref="INumberToStringConverter.Convert(TimeOnly, string[])"/>
        public string Convert(TimeOnly time, params string[] variants)
        {
            if (!SupportsTimeConversion)
                throw new NotSupportedException($"Language '{LanguageIdentifier}' has no <TimeUnits> configuration.");

            var parts = new List<string>();
            if (_timeUnits.TryGetValue("hour", out var h))
                parts.Add(FormatTimeUnit(time.Hour, h, variants));
            else if (time.Hour > 0)
                throw new InvalidOperationException(
                    $"Language '{LanguageIdentifier}' has a non-zero hours component but no 'hour' unit configured in <TimeUnits>.");

            if (time.Minute > 0)
            {
                if (!_timeUnits.TryGetValue("minute", out var m))
                    throw new InvalidOperationException(
                        $"Language '{LanguageIdentifier}' has a non-zero minutes component but no 'minute' unit configured in <TimeUnits>.");
                parts.Add(FormatTimeUnit(time.Minute, m, variants));
            }
            if (time.Second > 0)
            {
                if (!_timeUnits.TryGetValue("second", out var s))
                    throw new InvalidOperationException(
                        $"Language '{LanguageIdentifier}' has a non-zero seconds component but no 'second' unit configured in <TimeUnits>.");
                parts.Add(FormatTimeUnit(time.Second, s, variants));
            }

            return parts.Count > 0 ? string.Join(Separator, parts) : Zero;
        }

        private string GetMonthName(int month)
        {
            try
            {
                return CultureInfo.GetCultureInfo(LanguageIdentifier).DateTimeFormat.MonthNames[month - 1];
            }
            catch (Exception ex) when (ex is CultureNotFoundException or IndexOutOfRangeException)
            {
                return month.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <inheritdoc cref="INumberToStringConverter.Convert(DateOnly, string[])"/>
        public string Convert(DateOnly date, params string[] variants)
        {
            if (!SupportsDateConversion)
                throw new NotSupportedException($"Language '{LanguageIdentifier}' has no <DateFormat> configuration.");

            string month = GetMonthName(date.Month);
            // firstDay overrides both {ordinal-day} and {cardinal-day} when day == 1
            string cardinalDay = (date.Day == 1 && _dateFirstDay != null)
                ? _dateFirstDay
                : Convert(date.Day, variants);
            string ordinalDay = (date.Day == 1 && _dateFirstDay != null)
                ? _dateFirstDay
                : (SupportsOrdinals ? ConvertOrdinal(date.Day, variants) : cardinalDay);
            string year = ConvertYear(date.Year, variants);

            return _datePattern!
                .Replace("{month}", month)
                .Replace("{ordinal-day}", ordinalDay)
                .Replace("{cardinal-day}", cardinalDay)
                .Replace("{year}", year);
        }

        /// <inheritdoc cref="INumberToStringConverter.Convert(DateTime, string[])"/>
        public string Convert(DateTime dateTime, params string[] variants)
        {
            if (!SupportsDateConversion && !SupportsTimeConversion)
                throw new NotSupportedException($"Language '{LanguageIdentifier}' has no <DateFormat> or <TimeUnits> configuration.");

            string connector = _dateTimeConnector ?? Separator;

            if (SupportsDateConversion && SupportsTimeConversion)
                return Convert(DateOnly.FromDateTime(dateTime), variants) + connector + Convert(TimeOnly.FromDateTime(dateTime), variants);
            if (SupportsDateConversion)
                return Convert(DateOnly.FromDateTime(dateTime), variants);
            return Convert(TimeOnly.FromDateTime(dateTime), variants);
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
            for (int i = 0; i < StaticValues.Count; i++)
                if (StaticValues[i] is null)
                    throw new ArgumentException(
                        $"staticValues contains null at index {i}.", nameof(staticValues));

            ScaleSuffixes = scaleSuffixes.Arg().MustNotBeNull().Value.ToImmutableArray();
            for (int i = 0; i < ScaleSuffixes.Count; i++)
                if (ScaleSuffixes[i] is null)
                    throw new ArgumentException(
                        $"scaleSuffixes contains null at index {i}.", nameof(scaleSuffixes));

            if (startIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startIndex),
                    $"startIndex must be non-negative; got {startIndex}.");

            StartIndex = startIndex;
            FirstLetterUppercase = firstLetterUppercase;

            VoidGroup = voidGroup.ToDefaultIfNullOrEmpty("ni");
            // null (not set in XML) → default "lli"; explicit "" → no separator between prefix and suffix
            GroupSeparator = groupSeparator ?? "lli";

            Scale0Prefixes = scale0Prefixes?.ToImmutableArray() ?? Scale0Prefixes;
            UnitsPrefixes = unitsPrefixes?.ToImmutableArray() ?? UnitsPrefixes;
            TensPrefixes = tensPrefixes?.ToImmutableArray() ?? TensPrefixes;
            HundredsPrefixes = hundredsPrefixes?.ToImmutableArray() ?? HundredsPrefixes;

            // Prefix tables used for dynamic generation must each have exactly 10 entries
            // (one per decimal digit 0–9) so that index-by-digit lookups cannot fail.
            ValidatePrefixTable(Scale0Prefixes, nameof(scale0Prefixes));
            ValidatePrefixTable(UnitsPrefixes, nameof(unitsPrefixes));
            ValidatePrefixTable(TensPrefixes, nameof(tensPrefixes));
            ValidatePrefixTable(HundredsPrefixes, nameof(hundredsPrefixes));
        }

        /// <summary>Validates that a decimal digit-prefix table has exactly 10 entries (one per decimal digit 0–9) and contains no null entries.</summary>
        /// <param name="table">Table to validate.</param>
        /// <param name="paramName">Public constructor parameter represented by the table.</param>
        private static void ValidatePrefixTable(IReadOnlyList<string> table, string paramName)
        {
            if (table.Count != 10)
                throw new ArgumentException(
                    $"Prefix table '{paramName}' must have exactly 10 entries (one per decimal digit 0–9); got {table.Count}.",
                    paramName);
            for (int i = 0; i < table.Count; i++)
                if (table[i] is null)
                    throw new ArgumentException(
                        $"Prefix table '{paramName}' contains null at index {i}.", paramName);
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
            if (scale < 0)
                throw new ArgumentOutOfRangeException(nameof(scale),
                    $"scale must be non-negative; got {scale}.");
            if (scale < StaticValues.Count) return StaticValues[scale];
            if (ScaleSuffixes.Count == 0)
                throw new InvalidOperationException(
                    $"scale {scale} exceeds the {StaticValues.Count} static names and no scaleSuffixes are configured " +
                    "for dynamic generation. Either add more static names or provide at least one suffix.");

            long dynamicScale = (long)scale - StaticValues.Count + StartIndex;
            var (quotient, remainder) = long.DivRem(dynamicScale, ScaleSuffixes.Count);

            var suffix = ScaleSuffixes[(int)remainder];
            long prefix = quotient + 1;

            if (prefix.Between(0L, 9L))
            {
                var value = Scale0Prefixes[(int)prefix] + GroupSeparator + suffix;
                return FirstLetterUppercase && value.Length > 0 ? char.ToUpperInvariant(value[0]) + value[1..] : value;
            }

            var prefixes = new List<string>();

            while (prefix > 0)
            {
                (prefix, long u) = long.DivRem(prefix, 10);
                (prefix, long t) = long.DivRem(prefix, 10);
                (prefix, long h) = long.DivRem(prefix, 10);

                if (h == 0 && t == 0 && u == 0)
                {
                    prefixes.Add(VoidGroup);
                    continue;
                }

                Match[] groupValues = [
                    PrefixParser.Match(HundredsPrefixes[(int)h]),
                    PrefixParser.Match(TensPrefixes[(int)t]),
                    PrefixParser.Match(UnitsPrefixes[(int)u])
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
                    value[0] = char.ToUpperInvariant(value[0]);
                }
                prefixes.Add(value.ToString());
            }

            return string.Join(GroupSeparator, prefixes.AsEnumerable().Reverse()) + GroupSeparator + suffix;
        }

        [GeneratedRegex(@"(\((?<start>\w+)\))?(?<value>\w+)(\((?<end>\w+)\))?", RegexOptions.Compiled)]
        private static partial Regex PrefixParserRegex();
    }
}

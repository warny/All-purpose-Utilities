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
            culture.Length.ArgMustBeIn([2, 5]);  // Ensure culture code length is valid.

            if (CachedConfigurations.TryGetValue(culture, out var result)) return result;
            if (culture.Length == 5) return GetConverter(culture[..2]);  // Fallback to the language-only code if region-specific code is not found.
            return CachedConfigurations["EN"];  // Default to English converter.
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
            _substringReplacements = Replacements.Where(r => r.Scope == ReplacementScope.Anywhere).ToImmutableArray();
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
            _yearFormat = options.YearFormat;
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

        private readonly YearFormatOptions? _yearFormat;

        /// <summary>Year-format options, or <see langword="null"/> when not configured.</summary>
        internal YearFormatOptions? YearFormat => _yearFormat;

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

        /// <summary>
        /// The raw adjust function before composition with <see cref="LanguageSpecifics"/>.
        /// Used by <see cref="NumberToStringConverterOptions"/> for round-trip cloning.
        /// </summary>
        internal Func<string, string>? RawAdjustFunction => _rawAdjustFunction;

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
        /// Converts a BigInteger to its string representation using the default variant
        /// (the first declared value of each dimension).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="number"/> exceeds <see cref="MaxNumber"/>.
        /// </exception>
        public string Convert(BigInteger number) => Convert(number, []);

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

            string raw = ConvertRaw(abs);
            if (_rawAdjustFunction != null) raw = _rawAdjustFunction(raw);
            raw = ApplyVariantRules(raw, BuildVariantQuery(variants));
            string final = LanguageSpecifics.FinalizeWriting(LanguageIdentifier, raw);

            return isNegative ? Minus.Replace("*", final) : final;
        }

        /// <summary>
        /// Produces the raw text for a positive number without any adjustment or finalization.
        /// </summary>
        private string ConvertRaw(BigInteger abs)
        {
            if (abs.Between(long.MinValue, long.MaxValue) && Exceptions.TryGetValue((long)abs, out var exValue))
                return exValue;

            var maxGroup = Groups.Keys.Max();
            var groupValue = BigInteger.Pow(10, maxGroup);
            int groupNumber = 0;
            var groupsValues = new Stack<string>();

            BigInteger remaining = abs;
            while (remaining != 0)
            {
                var group = (long)(remaining % groupValue);
                if (group != 0)
                {
                    string resValue = ConvertGroup(maxGroup, group) + Separator + Scale.GetScaleName(groupNumber).ToPlural(group);
                    resValue = ApplyReplacements(resValue);
                    groupsValues.Push(resValue.Trim());
                }
                remaining /= groupValue;
                groupNumber++;
            }

            var result = new StringBuilder();
            while (groupsValues.Count > 0)
                result.Append(groupsValues.Pop().Trim()).Append(GroupSeparator).Append(Separator);

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
                ReplacementScope.Standalone => text == replacement.OldValue ? replacement.NewValue : text,
                ReplacementScope.Anywhere   => text.Replace(replacement.OldValue, replacement.NewValue),
                ReplacementScope.LastWord   => ApplyLastWordReplacement(text, replacement.OldValue, replacement.NewValue),
                _                           => text,
            };

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
                value = value.Replace(replacement.OldValue, replacement.NewValue);
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
        public string ConvertOrdinal(int number) => ConvertOrdinal(number, []);

        /// <inheritdoc cref="INumberToStringConverter.ConvertOrdinal(int, string[])"/>
        public string ConvertOrdinal(int number, params string[] variants)
        {
            bool isNegative = number < 0;
            int absNumber = Math.Abs(number);
            var activeVariants = BuildVariantQuery(variants);

            // Plugin check: IOrdinalLanguageSpecifics takes highest priority
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

            string raw = absNumber == 0 ? Zero : ConvertRaw((BigInteger)absNumber);
            raw = ApplyVariantRules(raw, activeVariants);
            string ordinal = ApplyOrdinalTransform(raw, activeVariant);
            string final = AdjustFunction(ordinal);
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

        /// <summary>
        /// Converts a decimal currency amount to words using the supplied currency definition.
        /// </summary>
        /// <param name="amount">The amount to convert.</param>
        /// <param name="currency">The currency names and configuration.</param>
        /// <returns>The amount expressed as words (e.g. "twenty euros and fifty centimes").</returns>
        public string ConvertCurrency(decimal amount, CurrencyDefinition currency)
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
            string result = Convert(units) + Separator + unitName;

            if (subunits > 0)
            {
                string subunitName = subunits == 1 ? currency.SubunitSingular : currency.SubunitPlural;
                string subunitsText = Convert(subunits) + Separator + subunitName;
                result = result + Separator + currency.Connector + Separator + subunitsText;
            }

            return isNegative ? Minus.Replace("*", result) : result;
        }

        /// <inheritdoc cref="INumberToStringConverter.ConvertYear(int)"/>
        public string ConvertYear(int year)
        {
            bool isNegative = year < 0;
            int abs = Math.Abs(year);

            string body;
            if (_yearFormat != null && _yearFormat.SplitRanges.Any(r => abs >= r.From && abs <= r.To))
            {
                int centuries = abs / 100;
                int remainder = abs % 100;

                if (remainder == 0 && _yearFormat.HundredWord != null)
                    body = Convert(centuries) + Separator + _yearFormat.HundredWord;
                else if (remainder is > 0 and < 10 && _yearFormat.ZeroConnector != null)
                    body = Convert(centuries) + Separator + _yearFormat.ZeroConnector + Separator + Convert(remainder);
                else
                    body = Convert(centuries) + Separator + Convert(remainder);
            }
            else
            {
                body = Convert(abs);
            }

            return isNegative ? Minus.Replace("*", body) : body;
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

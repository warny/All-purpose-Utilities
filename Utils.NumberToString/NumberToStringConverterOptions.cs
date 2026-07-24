using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Utils.Range;

namespace Utils.NumberToString;

/// <summary>
/// Holds all configuration parameters for a <see cref="NumberToStringConverter"/>.
/// </summary>
/// <remarks>
/// Build from scratch using the default constructor, or clone an existing locale
/// with <see cref="FromCulture(CultureInfo)"/>, <see cref="FromCulture(string)"/>
/// or <see cref="NumberToStringConverterOptions(NumberToStringConverter)"/>.
/// </remarks>
public sealed class NumberToStringConverterOptions
{
    /// <summary>Number of digits per group (e.g. 3 for thousands).</summary>
    public int Group { get; set; } = 3;

    /// <summary>Word separator inserted between sub-parts of a number name.</summary>
    public string Separator { get; set; } = " ";

    /// <summary>Separator inserted between digit groups (thousands, millions …).</summary>
    public string GroupSeparator { get; set; } = "";

    /// <summary>Literal representation of zero.</summary>
    public string? Zero { get; set; }

    /// <summary>
    /// Template for negative numbers. Use <c>*</c> as placeholder for the absolute value
    /// (e.g. <c>"minus *"</c>).
    /// </summary>
    public string? Minus { get; set; }

    /// <summary>Word placed between the integer and fractional parts.</summary>
    public string DecimalSeparator { get; set; } = ",";

    /// <summary>Digit tables keyed by group level.</summary>
    public IReadOnlyDictionary<int, DigitListType>? Groups { get; set; }

    /// <summary>Irregular number names (e.g. <c>11 → "eleven"</c> instead of <c>"ten one"</c>).</summary>
    public IReadOnlyDictionary<long, string> Exceptions { get; set; } = new Dictionary<long, string>();

    /// <summary>Post-processing substitution rules.</summary>
    public IEnumerable<NumberToStringConverter.ReplacementRule> Replacements { get; set; } = [];

    /// <summary>Scale definition used to name large powers of ten.</summary>
    public NumberScale? Scale { get; set; }

    /// <summary>Optional final transformation applied after all other processing.</summary>
    public Func<string, string>? AdjustFunction { get; set; }

    /// <summary>Language-specific post-processing hook.</summary>
    public INumberToStringLanguageSpecifics? LanguageSpecifics { get; set; }

    /// <summary>Culture or language identifier forwarded to <see cref="LanguageSpecifics"/>.</summary>
    public string LanguageIdentifier { get; set; } = "";

    /// <summary>Named suffixes for decimal fractions, keyed by digit count.</summary>
    public IReadOnlyDictionary<int, string>? Fractions { get; set; }

    /// <summary>Upper bound of supported values, or <see langword="null"/> for unlimited.</summary>
    public BigInteger? MaxNumber { get; set; }

    /// <summary>Connector word between numerator and denominator when rendering non-decimal fractions.</summary>
    public string? FractionSeparator { get; set; }

    /// <summary>
    /// Ordinal suffix appended to the last word of a cardinal when no word-level rule matches
    /// (e.g. "th" for English, "ième" for French).
    /// </summary>
    public string? OrdinalSuffix { get; set; }

    /// <summary>
    /// Trailing string to remove from the last cardinal word before appending <see cref="OrdinalSuffix"/>.
    /// When set, the string is stripped from the end of the last word only if it ends with this value.
    /// Example: <c>"e"</c> for French ("quatre" → "quatr" + "ième" = "quatrième").
    /// </summary>
    public string? OrdinalRemoveTrailing { get; set; }

    /// <summary>
    /// Integer-level ordinal exceptions (whole-number → ordinal text, e.g. 1 → "premier" in French).
    /// These take priority over word-level rules.
    /// </summary>
    public IReadOnlyDictionary<long, string> OrdinalExceptions { get; set; } = new Dictionary<long, string>();

    /// <summary>
    /// Word-level ordinal transformation rules applied to the last word of a cardinal
    /// (e.g. "one" → "first" in English).
    /// </summary>
    public IReadOnlyDictionary<string, string> OrdinalWordRules { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Prefix prepended to the whole ordinal result after <see cref="AdjustFunction"/> is applied.
    /// Example: <c>"第"</c> for Chinese/Japanese (第一, 第二…).
    /// </summary>
    public string? OrdinalPrefix { get; set; }

    /// <summary>
    /// Variant-specific ordinal rules applied when dimension constraints match the active variant query.
    /// Each rule overrides exceptions, word rules, suffix, and/or removeTrailing for that variant.
    /// </summary>
    public IReadOnlyList<NumberToStringConverter.OrdinalVariantRule> OrdinalVariants { get; set; } = [];

    /// <summary>
    /// Declared variant dimensions (e.g. "gender", "case") with their ordered values.
    /// The first value of each dimension is the default when no explicit variant is requested.
    /// </summary>
    public IReadOnlyList<NumberToStringConverter.VariantDimension> VariantDimensions { get; set; } = [];

    /// <summary>
    /// Variant rules that associate a set of dimension constraints with replacement rules.
    /// Applied between the raw adjustment function and <see cref="INumberToStringLanguageSpecifics.FinalizeWriting"/>.
    /// </summary>
    public IReadOnlyList<NumberToStringConverter.VariantRule> VariantRules { get; set; } = [];

    /// <summary>Year-format configuration used by <see cref="NumberToStringConverter.ConvertYear(int)"/>.</summary>
    public YearFormatOptions? YearFormat { get; set; }

    /// <summary>
    /// Trigger rules applied at specific points in the conversion pipeline.
    /// Processed in declaration order.
    /// </summary>
    public IReadOnlyList<NumberToStringConverter.TriggerRule> Triggers { get; set; } = [];

    /// <summary>Named multiplicative forms (e.g. 2 → "twice"). Key is the multiplier.</summary>
    public IReadOnlyDictionary<int, string>? Multiplicatives { get; set; }

    /// <summary>Suffix appended to the cardinal for multiplicatives not in <see cref="Multiplicatives"/> (e.g. " times").</summary>
    public string? MultiplicativeSuffix { get; set; }

    /// <summary>Word inserted between scale groups when the lower group value is below <see cref="GroupConnectorThreshold"/>.</summary>
    public string? GroupConnector { get; set; }

    /// <summary>Threshold below which <see cref="GroupConnector"/> is used instead of the regular group separator.</summary>
    public int GroupConnectorThreshold { get; set; } = 100;

    /// <summary>
    /// Word inserted inside a group between the hundreds and the lower part when the lower part is
    /// below <see cref="IntraGroupConnectorThreshold"/> (e.g. "linh" for Vietnamese).
    /// </summary>
    public string? IntraGroupConnector { get; set; }

    /// <summary>Threshold below which <see cref="IntraGroupConnector"/> is inserted within a group.</summary>
    public int IntraGroupConnectorThreshold { get; set; } = 10;

    /// <summary>
    /// Connector word inserted between a group's digit text and the scale name when the group's
    /// numeric value is at or above <see cref="ScaleConnectorThreshold"/>.
    /// Example: <c>"de"</c> for Romanian — "douăzeci de mii" (20 000).
    /// </summary>
    public string? ScaleConnector { get; set; }

    /// <summary>Threshold at or above which <see cref="ScaleConnector"/> is inserted.</summary>
    public int ScaleConnectorThreshold { get; set; } = 20;

    /// <summary>
    /// Time units keyed by canonical name ("hour", "minute", "second"),
    /// each with singular, plural, and an optional count-1 override for grammatical gender.
    /// Required for Convert(TimeSpan/TimeOnly).
    /// </summary>
    public IReadOnlyDictionary<string, (string Singular, string Plural, string? Count1Form)>? TimeUnits { get; set; }

    /// <summary>
    /// Pattern for rendering a date. Supported tokens: {month}, {ordinal-day}, {cardinal-day}, {year}.
    /// Required for Convert(DateOnly/DateTime).
    /// </summary>
    public string? DatePattern { get; set; }

    /// <summary>
    /// Special string for day 1 in {ordinal-day} (e.g. "first" override).
    /// When set, replaces the standard ordinal form of 1 in ordinal-day slots.
    /// </summary>
    public string? DateFirstDay { get; set; }

    /// <summary>
    /// Special string for day 1 in {cardinal-day} (e.g. "premier" in French, "ersten" in German).
    /// When set, replaces the standard cardinal form of 1 in cardinal-day slots.
    /// </summary>
    public string? DateFirstCardinalDay { get; set; }

    /// <summary>Connector inserted between date and time when converting a DateTime (defaults to Separator).</summary>
    public string? DateTimeConnector { get; set; }

    /// <summary>Creates an options object with sensible defaults. Required properties
    /// (<see cref="Zero"/>, <see cref="Minus"/>, <see cref="Groups"/>, <see cref="Scale"/>)
    /// must be set before passing to the constructor.</summary>
    public NumberToStringConverterOptions() { }

    /// <summary>Clones all settings from an existing converter instance.</summary>
    /// <param name="source">The converter to copy.</param>
    public NumberToStringConverterOptions(NumberToStringConverter source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Group = source.Group;
        Separator = source.Separator;
        GroupSeparator = source.GroupSeparator;
        Zero = source.Zero;
        Minus = source.Minus;
        DecimalSeparator = source.DecimalSeparator;
        Groups = source.Groups.ToDictionary(
            kv => kv.Key,
            kv => new DigitListType { Digits = kv.Value.Values.ToList() }
        );
        Exceptions = source.Exceptions;
        Replacements = source.Replacements;
        Scale = source.Scale;
        AdjustFunction = source.RawAdjustFunction;
        LanguageSpecifics = source.LanguageSpecifics;
        LanguageIdentifier = source.LanguageIdentifier;
        Fractions = source.Fractions;
        MaxNumber = source.MaxNumber;
        FractionSeparator = source.FractionSeparator;
        OrdinalSuffix = source.OrdinalSuffix;
        OrdinalRemoveTrailing = source.OrdinalRemoveTrailing;
        OrdinalExceptions = source.OrdinalExceptions;
        OrdinalWordRules = source.OrdinalWordRules;
        OrdinalPrefix = source.OrdinalPrefix;
        OrdinalVariants = source.OrdinalVariants;
        VariantDimensions = source.VariantDimensions;
        VariantRules = source.VariantRules;
        Triggers = source.Triggers;
        YearFormat = source.YearFormat;
        Multiplicatives = source.Multiplicatives;
        MultiplicativeSuffix = source.MultiplicativeSuffix;
        GroupConnector = source.GroupConnector;
        GroupConnectorThreshold = source.GroupConnectorThreshold;
        IntraGroupConnector = source.IntraGroupConnector;
        IntraGroupConnectorThreshold = source.IntraGroupConnectorThreshold;
        ScaleConnector = source.ScaleConnector;
        ScaleConnectorThreshold = source.ScaleConnectorThreshold;
        TimeUnits = source.TimeUnits;
        DatePattern = source.DatePattern;
        DateFirstDay = source.DateFirstDay;
        DateFirstCardinalDay = source.DateFirstCardinalDay;
        DateTimeConnector = source.DateTimeConnector;
    }

    /// <summary>
    /// Clones the settings of the converter registered for <paramref name="culture"/>.
    /// </summary>
    public static NumberToStringConverterOptions FromCulture(CultureInfo culture)
        => new(NumberToStringConverter.GetConverter(culture));

    /// <summary>
    /// Clones the settings of the converter registered for <paramref name="cultureName"/>
    /// (e.g. <c>"FR"</c>, <c>"en-US"</c>).
    /// </summary>
    public static NumberToStringConverterOptions FromCulture(string cultureName)
        => new(NumberToStringConverter.GetConverter(cultureName));
}

/// <summary>
/// Immutable year-format options that drive <see cref="NumberToStringConverter.ConvertYear(int)"/>.
/// </summary>
/// <param name="HundredWord">Word appended for round centuries (e.g. "hundred").</param>
/// <param name="ZeroConnector">Connector before single-digit remainders (e.g. "oh").</param>
/// <param name="SplitRanges">
/// Integer ranges for which the split-at-hundreds algorithm applies.
/// Years outside all declared ranges fall back to <c>Convert(year)</c>.
/// </param>
/// <param name="BeforeChristSuffix">
/// Optional suffix appended after the year body for negative (BC) years instead of the
/// <c>minus</c> prefix (e.g. <c>"av. J.-C."</c> → "quarante-quatre av. J.-C.").
/// When <see langword="null"/>, negative years use the standard <c>minus</c> template.
/// </param>
public record YearFormatOptions(
    string? HundredWord,
    string? ZeroConnector,
    IntRange<int>? SplitRanges,
    string? BeforeChristSuffix = null);

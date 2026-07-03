using System;
using System.Xml;
using System.Xml.Serialization;

namespace Utils.NumberToString;

/// <summary>
/// Represents the root configuration element that aggregates number conversion definitions for multiple languages.
/// </summary>
[XmlRoot]
public class Numbers
{
    /// <summary>
    /// Gets or sets the language-specific conversion definitions.
    /// </summary>
    [XmlElement(ElementName = "Language")]
    public List<LanguageType> Languages { get; set; }
}

/// <summary>
/// Describes a fixed mapping between a numeric value and its literal representation.
/// </summary>
public class NumberType
{
    /// <summary>
    /// Gets or sets the numeric value represented by the entry.
    /// </summary>
    [XmlAttribute("value")]
    public long Value { get; set; }

    /// <summary>
    /// Gets or sets the literal value rendered for <see cref="Value"/>.
    /// </summary>
    [XmlAttribute("string")]
    public string StringValue { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"N : {Value} => {StringValue}";
}

/// <summary>
/// A variant node inside a form-producing element (<c>&lt;Replacement&gt;</c>,
/// <c>&lt;OrdinalException&gt;</c>, <c>&lt;Ordinal&gt;</c>).
/// Plays two roles depending on whether <see cref="Forms"/> is present:
/// <list type="bullet">
///   <item><term>Intermediate node</term><description>
///     <see cref="DimensionType"/> + <see cref="VariantValue"/> add one constraint;
///     child <c>&lt;Variant&gt;</c> elements cascade further constraints.
///   </description></item>
///   <item><term>Leaf node</term><description>
///     <see cref="DimensionType"/> + <see cref="Forms"/> provide one output form per
///     dimension value in the order declared by the matching <c>&lt;Dimension&gt;</c>
///     element. Empty entries are skipped.
///   </description></item>
/// </list>
/// All (constraints, form) pairs are collected at load time and merged by constraint set
/// into <c>OrdinalVariantRule</c> or <c>VariantRule</c> entries — no runtime changes required.
/// </summary>
public class FormVariantType
{
    /// <summary>Canonical dimension name this node targets (e.g. <c>"gender"</c>, <c>"case"</c>).</summary>
    [XmlAttribute("type")]
    public string? DimensionType { get; set; }

    /// <summary>
    /// Single dimension value for an intermediate node.
    /// Used together with child <c>&lt;Variant&gt;</c> elements.
    /// Omit on leaf nodes that use <see cref="Forms"/> instead.
    /// </summary>
    [XmlAttribute("variant")]
    public string? VariantValue { get; set; }

    /// <summary>
    /// Comma-separated positional output forms for a leaf node, one per dimension value
    /// in <c>&lt;Dimension&gt;</c> declaration order. Empty entries produce no rule.
    /// </summary>
    [XmlAttribute("forms")]
    public string? Forms { get; set; }

    /// <summary>
    /// Single output form for the specific dimension value named by <see cref="VariantValue"/>.
    /// Shorthand for single-value overrides without listing all positional forms.
    /// Requires <see cref="VariantValue"/> to be set.
    /// </summary>
    [XmlAttribute("value")]
    public string? Value { get; set; }


    /// <summary>Nested sub-variants that cascade additional constraints.</summary>
    [XmlElement("Variant")]
    public List<FormVariantType>? NestedVariants { get; set; }
}

/// <summary>
/// Describes a single ordinal exception that maps an integer to its ordinal text.
/// </summary>
public class OrdinalExceptionType
{
    /// <summary>Gets or sets the numeric value of the exception.</summary>
    [XmlAttribute("value")]
    public long Value { get; set; }

    /// <summary>
    /// Gets or sets the base ordinal text for <see cref="Value"/> (default variant).
    /// May be <see langword="null"/> when all forms are declared via <see cref="FormVariants"/>.
    /// </summary>
    [XmlAttribute("string")]
    public string? StringValue { get; set; }

    /// <summary>
    /// Per-dimension-value form declarations expanded at load time into <c>OrdinalVariantRule</c>
    /// entries (see <see cref="FormVariantType"/>).
    /// </summary>
    [XmlElement("Variant")]
    public List<FormVariantType>? FormVariants { get; set; }
}

/// <summary>
/// Describes a word-level transformation rule for ordinal conversion: when the last
/// word of a cardinal equals <see cref="From"/>, it is replaced with <see cref="To"/>.
/// </summary>
public class OrdinalRuleType
{
    /// <summary>Gets or sets the cardinal word to match.</summary>
    [XmlAttribute("from")]
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the base ordinal replacement word (default variant).
    /// May be <see langword="null"/> when all forms are declared via <see cref="FormVariants"/>.
    /// </summary>
    [XmlAttribute("to")]
    public string? To { get; set; }

    /// <summary>
    /// Per-dimension-value form declarations expanded at load time into <c>OrdinalVariantRule</c>
    /// word-rule entries (see <see cref="FormVariantType"/>).
    /// </summary>
    [XmlElement("Variant")]
    public List<FormVariantType>? FormVariants { get; set; }
}

/// <summary>
/// Holds the complete ordinal configuration for a language: whole-number exceptions,
/// word-level transformation rules, and a default suffix.
/// </summary>
public class OrdinalsType
{
    /// <summary>
    /// Gets or sets the suffix appended to the last word of the cardinal when no rule matches.
    /// </summary>
    [XmlAttribute("suffix")]
    public string Suffix { get; set; }

    /// <summary>
    /// Gets or sets the trailing string to remove from the last word before appending the suffix.
    /// When set, the suffix is stripped from the end of the last word only if it ends with this value.
    /// </summary>
    [XmlAttribute("removeTrailing")]
    public string? RemoveTrailing { get; set; }

    /// <summary>
    /// Gets or sets the prefix prepended to the whole ordinal result (e.g. "第" for Chinese).
    /// </summary>
    [XmlAttribute("prefix")]
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets integer-level ordinal exceptions (e.g. 1 → "premier" in French).
    /// These take priority over word-level rules.
    /// </summary>
    [XmlElement("OrdinalException")]
    public List<OrdinalExceptionType> Exceptions { get; set; }

    /// <summary>
    /// Gets or sets the word-level ordinal rules applied to the last word of the cardinal.
    /// </summary>
    [XmlElement("Ordinal")]
    public List<OrdinalRuleType> Rules { get; set; }

    /// <summary>
    /// Gets or sets the container for variant-specific ordinal blocks.
    /// </summary>
    [XmlElement("OrdinalVariants")]
    public OrdinalVariants? OrdinalVariantsContainer { get; set; }
}

/// <summary>
/// Container element <c>&lt;OrdinalVariants&gt;</c> that groups variant-specific ordinal override
/// blocks. Each child <c>&lt;Variant&gt;</c> targets one dimension and can contain nested
/// <c>&lt;Variant&gt;</c> elements to express cascaded multi-dimension constraints.
/// </summary>
public class OrdinalVariants
{
    /// <summary>Gets or sets the variant blocks inside this container.</summary>
    [XmlElement("Variant")]
    public List<OrdinalVariantElementType>? Variants { get; set; }
}

/// <summary>
/// A single <c>&lt;Variant&gt;</c> inside <c>&lt;OrdinalVariants&gt;</c>: declares one constraint
/// via <see cref="DimensionType"/> + <see cref="VariantValue"/>, optional suffix/removeTrailing
/// overrides, ordinal exceptions, word rules, and nested sub-variants for cascaded constraints.
/// </summary>
public class OrdinalVariantElementType
{
    /// <summary>The canonical dimension name this constraint targets (e.g. "gender", "case").</summary>
    [XmlAttribute("type")]
    public string? DimensionType { get; set; }

    /// <summary>The value that must be active for this variant to apply (e.g. "femenino").</summary>
    [XmlAttribute("variant")]
    public string? VariantValue { get; set; }

    /// <summary>
    /// Comma-separated list of dimension values; alternative to <see cref="VariantValue"/>.
    /// One rule is emitted per value, all sharing the same body.
    /// Takes priority over <see cref="VariantValue"/> when both are present.
    /// </summary>
    [XmlAttribute("values")]
    public string? VariantValues { get; set; }

    /// <summary>Suffix override for this variant; falls back to the base suffix when absent.</summary>
    [XmlAttribute("suffix")]
    public string? Suffix { get; set; }

    /// <summary>RemoveTrailing override for this variant; falls back to the base value when absent.</summary>
    [XmlAttribute("removeTrailing")]
    public string? RemoveTrailing { get; set; }

    /// <summary>Variant-specific whole-number exceptions (checked before base exceptions).</summary>
    [XmlElement("OrdinalException")]
    public List<OrdinalExceptionType>? Exceptions { get; set; }

    /// <summary>Variant-specific word-level rules (checked before base word rules).</summary>
    [XmlElement("Ordinal")]
    public List<OrdinalRuleType>? Rules { get; set; }

    /// <summary>
    /// Nested sub-variants that inherit this variant's constraint and add further constraints.
    /// Used to express combinations without listing all attribute permutations at a flat level.
    /// </summary>
    [XmlElement("Variant")]
    public List<OrdinalVariantElementType>? NestedVariants { get; set; }
}

/// <summary>
/// Collection wrapper for <see cref="NumberType"/> records to simplify XML serialization.
/// </summary>
[XmlRoot("NumberList")]
public class NumberListType
{
    /// <summary>
    /// Gets or sets the list of numeric exceptions defined for the language.
    /// </summary>
    [XmlElement("Number")]
    public List<NumberType> Numbers { get; set; }
}

/// <summary>
/// Describes how a specific digit should be rendered and composed within a number name.
/// </summary>
public class DigitType
{
    /// <summary>
    /// Gets or sets the numeric digit represented by this entry.
    /// </summary>
    [XmlAttribute("digit")]
    public long Digit { get; set; }

    /// <summary>
    /// Gets or sets the literal representation of the digit.
    /// </summary>
    [XmlAttribute("string")]
    public string StringValue { get; set; }

    /// <summary>
    /// Gets or sets the optional template used to build composite number names.
    /// </summary>
    [XmlAttribute("buildString")]
    public string BuildString { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DigitType"/> class for XML serialization.
    /// </summary>
    public DigitType()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DigitType"/> class with explicit mapping information.
    /// </summary>
    /// <param name="digit">The digit described by the entry.</param>
    /// <param name="stringValue">The literal representation of the digit.</param>
    /// <param name="buildString">An optional template for concatenation.</param>
    public DigitType(int digit, string stringValue, string buildString = null)
    {
        Digit = digit;
        StringValue = stringValue;
        BuildString = buildString;
    }

    /// <summary>
    /// Converts a pair of string values into a <see cref="DigitType"/> instance.
    /// </summary>
    /// <param name="values">The literal text and optional build template.</param>
    /// <returns>A populated digit definition.</returns>
    public static implicit operator DigitType(string[] values)
    {
        if (values.Length == 0) return null;
        var result = new DigitType();
        result.StringValue = values[0];
        if (values.Length > 1) result.BuildString = values[1];
        return result;
    }

    /// <inheritdoc />
    public override string ToString() => $"D : {Digit} => {StringValue}, {BuildString}";
}

/// <summary>
/// Represents a collection of digit definitions for a particular group of numbers.
/// </summary>
public class DigitListType
{
    /// <summary>
    /// Gets or sets the digits that belong to the list.
    /// </summary>
    [XmlElement("Digit")]
    public List<DigitType> Digits { get; set; }
}

/// <summary>
/// Holds a list of string replacements applied after number conversion.
/// </summary>
public class ReplacementsListType
{
    /// <summary>
    /// Gets or sets the replacement rules.
    /// </summary>
    [XmlElement("Replacement")]
    public List<ReplacementType> Replacements { get; set; }
}

/// <summary>
/// Defines a string replacement applied to the generated number name.
/// </summary>
public class ReplacementType
{
    /// <summary>
    /// Gets or sets the text to replace.
    /// </summary>
    [XmlAttribute("oldValue")]
    public string OldValue { get; set; }

    /// <summary>
    /// Gets or sets the base replacement text (default variant).
    /// May be <see langword="null"/> when all forms are declared via <see cref="FormVariants"/>.
    /// </summary>
    [XmlAttribute("newValue")]
    public string? NewValue { get; set; }

    /// <summary>
    /// Gets or sets the textual scope representation supplied by the configuration.
    /// </summary>
    [XmlAttribute("scope")]
    public string ScopeValue { get; set; }

    /// <summary>
    /// Gets the parsed scope that determines how the replacement is applied.
    /// </summary>
    [XmlIgnore]
    public ReplacementScope Scope => ParseScope(ScopeValue);

    /// <summary>
    /// Gets or sets the raw string value of the scale level restriction, or
    /// <see langword="null"/> when no restriction is configured.
    /// </summary>
    [XmlAttribute("onScale")]
    public string? OnScaleValue { get; set; }

    /// <summary>
    /// Gets the scale level(s) this replacement is restricted to, or <see langword="null"/>
    /// to apply at all levels. 0 = units group, 1 = thousands, 2 = millions, etc.
    /// Supports range syntax: <c>"1"</c>, <c>"1..3"</c>, <c>"..2"</c>, <c>"1,3.."</c>.
    /// </summary>
    [XmlIgnore]
    public NumberToStringConverter.IntRange? OnScale =>
        OnScaleValue is { Length: > 0 } s ? NumberToStringConverter.IntRange.Parse(s) : null;

    /// <summary>
    /// Gets or sets the raw value-range expression restricting this rule to specific numeric
    /// group values. Supports comma-separated segments: <c>"1"</c> (exact), <c>"1..3"</c>
    /// (inclusive range), <c>"..5"</c> (≤ 5), <c>"5.."</c> (≥ 5).
    /// <see langword="null"/> when absent (no value restriction).
    /// </summary>
    [XmlAttribute("onValue")]
    public string? OnValueRaw { get; set; }

    /// <summary>
    /// Per-dimension-value form declarations expanded at load time into <c>VariantRule</c>
    /// replacement entries (see <see cref="FormVariantType"/>).
    /// </summary>
    [XmlElement("Variant")]
    public List<FormVariantType>? FormVariants { get; set; }

    private static ReplacementScope ParseScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return ReplacementScope.Standalone;
        }

        return Enum.TryParse(scope.Trim(), ignoreCase: true, out ReplacementScope parsed)
            ? parsed
            : ReplacementScope.Standalone;
    }
}

/// <summary>
/// Defines how a replacement should be applied to the converted value.
/// </summary>
public enum ReplacementScope
{
    /// <summary>
    /// Indicates that the replacement only applies when the entire value matches the configured phrase.
    /// </summary>
    Standalone,

    /// <summary>
    /// Indicates that the replacement may occur within a larger phrase when the words match.
    /// </summary>
    Anywhere,

    /// <summary>
    /// Indicates that the replacement applies only when <c>oldValue</c> matches the last word of
    /// the text (the part after the last space or hyphen), treating the old value as a word-boundary
    /// suffix rather than a substring.
    /// </summary>
    LastWord,

    /// <summary>
    /// Indicates that the replacement applies only when <c>oldValue</c> matches the beginning of
    /// the text, treating the old value as a word-boundary prefix.
    /// </summary>
    StartsWith,

    /// <summary>
    /// Indicates that the replacement applies only when <c>oldValue</c> matches the end of
    /// the text (similar to <see cref="LastWord"/> but without the word-boundary check).
    /// </summary>
    EndsWith,
}

/// <summary>
/// Declares one dimension of grammatical variation for a language (e.g. "gender" or "case")
/// together with its ordered set of values. The first value is the default when no explicit
/// variant is requested.
/// </summary>
public class VariantDimensionType
{
    /// <summary>Gets or sets the canonical English dimension name (e.g. "gender", "case").</summary>
    [XmlAttribute("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets an optional language-specific alias accepted alongside <see cref="Name"/>
    /// in API calls and XML constraints (e.g. "genus" for German, "sijamuoto" for Finnish).
    /// </summary>
    [XmlAttribute("localName")]
    public string? LocalName { get; set; }

    /// <summary>
    /// Gets or sets the comma-separated ordered list of valid values for this dimension
    /// (e.g. "masculin,feminin,neutrum"). The first value is the default.
    /// </summary>
    [XmlAttribute("values")]
    public string ValuesRaw { get; set; }
}

/// <summary>
/// Describes a morphological variant: one dimension constraint expressed as explicit
/// <c>type</c> (dimension name) and <c>variant</c> (dimension value) attributes, plus
/// replacement rules and optional nested sub-variants for cascaded constraints.
/// </summary>
public class VariantType
{
    /// <summary>The canonical dimension name this variant constrains (e.g. "gender", "case").</summary>
    [XmlAttribute("type")]
    public string? DimensionType { get; set; }

    /// <summary>The value that must be active for this variant to apply (e.g. "feminin").</summary>
    [XmlAttribute("variant")]
    public string? VariantValue { get; set; }

    /// <summary>
    /// Comma-separated list of dimension values; alternative to <see cref="VariantValue"/>.
    /// One rule is emitted per value, all sharing the same body.
    /// Takes priority over <see cref="VariantValue"/> when both are present.
    /// </summary>
    [XmlAttribute("values")]
    public string? VariantValues { get; set; }

    /// <summary>Gets or sets the replacement rules applied when this variant is active.</summary>
    [XmlElement("Replacement")]
    public List<ReplacementType>? Replacements { get; set; }

    /// <summary>
    /// Nested sub-variants that inherit this variant's constraint and add further constraints.
    /// Used to express multi-dimension combinations without listing flat attribute permutations.
    /// </summary>
    [XmlElement("Variant")]
    public List<VariantType>? NestedVariants { get; set; }
}

/// <summary>
/// Holds the complete variant configuration for a language: the declared dimensions
/// and the list of variant rules.
/// </summary>
public class VariantsType
{
    /// <summary>Gets or sets the declared variant dimensions.</summary>
    [XmlElement("Dimension")]
    public List<VariantDimensionType>? Dimensions { get; set; }

    /// <summary>Gets or sets the list of variant rules.</summary>
    [XmlElement("Variant")]
    public List<VariantType>? Variants { get; set; }
}

/// <summary>
/// Configures how fractional values should be rendered.
/// </summary>
public class FractionType
{
    /// <summary>
    /// Gets or sets the number of digits considered by the fraction.
    /// </summary>
    [XmlAttribute("digits")]
    public int Digits { get; set; }

    /// <summary>
    /// Gets or sets the literal representation of the fraction.
    /// </summary>
    [XmlAttribute("string")]
    public string StringValue { get; set; }
}

/// <summary>
/// Represents a collection of fractional definitions.
/// </summary>
public class FractionListType
{
    /// <summary>
    /// Gets or sets the list of fraction mappings.
    /// </summary>
    [XmlElement("Fraction")]
    public List<FractionType> Fractions { get; set; }
}

/// <summary>
/// Describes a single time unit (hour, minute, second) with its singular and plural forms.
/// </summary>
public class TimeUnitEntry
{
    /// <summary>Canonical unit name (e.g. "hour", "minute", "second").</summary>
    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    /// <summary>Singular form of the unit (e.g. "heure").</summary>
    [XmlAttribute("singular")]
    public string Singular { get; set; } = "";

    /// <summary>Plural form of the unit (e.g. "heures").</summary>
    [XmlAttribute("plural")]
    public string Plural { get; set; } = "";

    /// <summary>
    /// Optional numeral form to use when count == 1, overriding the standard Convert(1) result.
    /// Useful when the unit has a grammatical gender that requires a different form of "one"
    /// (e.g. <c>count1form="eine"</c> for German feminine nouns like "Stunde").
    /// </summary>
    [XmlAttribute("count1form")]
    public string? Count1Form { get; set; }
}

/// <summary>
/// Holds time-unit configuration for a language (hours, minutes, seconds).
/// </summary>
public class TimeUnitsType
{
    /// <summary>Gets or sets the list of time unit definitions.</summary>
    [XmlElement("Unit")]
    public List<TimeUnitEntry>? Units { get; set; }
}

/// <summary>
/// Holds date-format configuration for a language.
/// </summary>
public class DateFormatType
{
    /// <summary>
    /// Pattern for rendering a date. Supported tokens: {month}, {ordinal-day}, {cardinal-day}, {year}.
    /// Example: "{month} {ordinal-day}, {year}" → "July second, twenty twenty-six".
    /// </summary>
    [XmlAttribute("pattern")]
    public string Pattern { get; set; } = "";

    /// <summary>
    /// Special string used for the first day of the month (e.g. "premier" in French).
    /// When set, overrides the ordinal form of day 1 in {ordinal-day}.
    /// </summary>
    [XmlAttribute("firstDay")]
    public string? FirstDay { get; set; }

    /// <summary>Connector between the date and the time when converting a DateTime.</summary>
    [XmlAttribute("dateTimeConnector")]
    public string? DateTimeConnector { get; set; }
}

/// <summary>
/// Holds multiplicative configuration for a language (e.g. 1 → "once", 2 → "twice").
/// </summary>
public class MultiplicativesType
{
    /// <summary>Suffix appended to the cardinal for unnamed multiplicatives (e.g. " times").</summary>
    [XmlAttribute("suffix")]
    public string? Suffix { get; set; }

    /// <summary>Named multiplicative entries.</summary>
    [XmlElement("Multiplicative")]
    public List<MultiplicativeEntryType>? Entries { get; set; }
}

/// <summary>
/// Maps a specific multiplier value to its named multiplicative form.
/// </summary>
public class MultiplicativeEntryType
{
    /// <summary>The multiplier value.</summary>
    [XmlAttribute("value")]
    public int Value { get; set; }

    /// <summary>The multiplicative string for this value.</summary>
    [XmlAttribute("string")]
    public string String { get; set; } = "";
}

/// <summary>
/// Describes the conversion settings for a single language or culture.
/// </summary>
public class LanguageType
{
    /// <summary>
    /// Gets or sets the list of culture names served by the configuration.
    /// </summary>
    [XmlElement("Culture")]
    public List<string> Cultures { get; set; }

    /// <summary>
    /// Gets or sets the base culture identifier to inherit defaults from.
    /// </summary>
    [XmlAttribute("baseOn")]
    public string BaseOn { get; set; }

    /// <summary>
    /// Gets or sets the number of digits grouped together when formatting.
    /// </summary>
    [XmlAttribute("groupSize")]
    public int GroupSize { get; set; }

    /// <summary>
    /// Gets or sets the separator used between groups.
    /// </summary>
    [XmlAttribute("separator")]
    public string Separator { get; set; }

    /// <summary>
    /// Gets or sets the textual separator used between digit groups.
    /// </summary>
    [XmlAttribute("groupSeparator")]
    public string GroupSeparator { get; set; }

    /// <summary>
    /// Gets or sets the literal used to represent zero.
    /// </summary>
    [XmlAttribute("zero")]
    public string Zero { get; set; }

    /// <summary>
    /// Gets or sets the literal prefix applied to negative numbers.
    /// </summary>
    [XmlAttribute("minus")]
    public string Minus { get; set; }

    /// <summary>
    /// Gets or sets the string inserted before fractional parts.
    /// </summary>
    [XmlAttribute("decimalSeparator")]
    public string DecimalSeparator { get; set; }

    /// <summary>
    /// Gets or sets the connector used when pronouncing fractions (e.g., "sur").
    /// </summary>
    [XmlAttribute("fractionSeparator")]
    public string FractionSeparator { get; set; }

    /// <summary>
    /// Gets or sets the maximum supported number as a string representation.
    /// </summary>
    [XmlAttribute("maxNumber")]
    public string MaxNumber { get; set; }

    /// <summary>
    /// Gets or sets the group definitions used when splitting large numbers.
    /// </summary>
    [XmlElement(ElementName = "Groups")]
    public GroupsListType Groups { get; set; }

    /// <summary>
    /// Gets or sets the special-case number mappings for the language.
    /// </summary>
    [XmlElement(ElementName = "Exceptions")]
    public NumberListType Exceptions { get; set; }

    /// <summary>
    /// Gets or sets the scale definition for large numbers.
    /// </summary>
    [XmlElement(ElementName = "NumberScale")]
    public NumberScaleType NumberScale { get; set; }

    /// <summary>
    /// Gets or sets the post-processing replacement rules.
    /// </summary>
    [XmlElement(ElementName = "Replacements")]
    public ReplacementsListType Replacements { get; set; }

    /// <summary>
    /// Gets or sets the optional language-specific finalizer type name.
    /// The value can be either the full type name or the short type name.
    /// </summary>
    [XmlElement(ElementName = "LanguageSpecifics")]
    public string LanguageSpecificsTypeName { get; set; }

    /// <summary>
    /// Gets or sets the fraction configuration applied to decimal values.
    /// </summary>
    [XmlElement(ElementName = "Fractions")]
    public FractionListType Fractions { get; set; }

    /// <summary>
    /// Gets or sets the ordinal configuration for this language.
    /// </summary>
    [XmlElement(ElementName = "Ordinals")]
    public OrdinalsType Ordinals { get; set; }

    /// <summary>
    /// Gets or sets the morphological variant configuration for this language.
    /// Declares the available dimensions and the replacement rules for each combination.
    /// </summary>
    [XmlElement(ElementName = "Variants")]
    public VariantsType Variants { get; set; }

    /// <summary>
    /// Gets or sets the year-format configuration used by <see cref="NumberToStringConverter.ConvertYear(int)"/>.
    /// When absent, <c>ConvertYear</c> falls back to <c>Convert</c>.
    /// </summary>
    [XmlElement(ElementName = "YearFormat")]
    public YearFormatType? YearFormat { get; set; }

    /// <summary>
    /// Gets or sets the trigger rules applied at specific points in the conversion pipeline.
    /// Multiple Trigger elements are processed in declaration order.
    /// </summary>
    [XmlElement(ElementName = "Trigger")]
    public List<TriggerType>? Triggers { get; set; }

    /// <summary>
    /// Gets or sets the multiplicative configuration (e.g. 1 → "once", 2 → "twice").
    /// </summary>
    [XmlElement(ElementName = "Multiplicatives")]
    public MultiplicativesType? Multiplicatives { get; set; }

    /// <summary>Gets or sets the word inserted between scale groups when the lower group is small.</summary>
    [XmlAttribute("groupConnector")]
    public string? GroupConnector { get; set; }

    /// <summary>Gets or sets the threshold (as string) below which the group connector is used.</summary>
    [XmlAttribute("groupConnectorThreshold")]
    public string? GroupConnectorThresholdString { get; set; }

    /// <summary>Gets the threshold below which <see cref="GroupConnector"/> is used instead of the regular group separator.</summary>
    [XmlIgnore]
    public int GroupConnectorThreshold =>
        int.TryParse(GroupConnectorThresholdString, out var n) ? n : 100;

    /// <summary>
    /// Gets or sets the word inserted inside a group between hundreds and the lower part
    /// when the lower part is below <see cref="IntraGroupConnectorThreshold"/>.
    /// Example: "linh" for Vietnamese (101 → "một trăm linh một").
    /// </summary>
    [XmlAttribute("intraGroupConnector")]
    public string? IntraGroupConnector { get; set; }

    /// <summary>Gets or sets the threshold (as string) below which the intra-group connector is inserted.</summary>
    [XmlAttribute("intraGroupConnectorThreshold")]
    public string? IntraGroupConnectorThresholdString { get; set; }

    /// <summary>Gets the threshold below which <see cref="IntraGroupConnector"/> is inserted.</summary>
    [XmlIgnore]
    public int IntraGroupConnectorThreshold =>
        int.TryParse(IntraGroupConnectorThresholdString, out var n) ? n : 10;

    /// <summary>Gets or sets the time-unit configuration (hours, minutes, seconds).</summary>
    [XmlElement(ElementName = "TimeUnits")]
    public TimeUnitsType? TimeUnits { get; set; }

    /// <summary>Gets or sets the date-format configuration.</summary>
    [XmlElement(ElementName = "DateFormat")]
    public DateFormatType? DateFormat { get; set; }
}

/// <summary>
/// A text-replacement rule inside a <see cref="TriggerType"/>.
/// Selects the most specific matching variant form, or <see cref="To"/> as the unconditional default.
/// Uses the same <see cref="FormVariantType"/> nesting as <c>Replacement</c>, <c>Ordinal</c>, and
/// <c>OrdinalException</c>: leaf nodes provide positional <c>forms</c> or a single <c>value</c>;
/// intermediate nodes add dimension constraints via <c>type</c>+<c>variant</c>.
/// </summary>
public class TriggerReplaceType
{
    /// <summary>Gets or sets the text or regex pattern to match.</summary>
    [XmlAttribute("from")]
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the unconditional default replacement.
    /// May be omitted when all cases are covered by <see cref="FormVariants"/>;
    /// the first expanded form then serves as default.
    /// May contain backreferences ($1, ${name}) when <see cref="IsRegex"/> is true.
    /// </summary>
    [XmlAttribute("to")]
    public string? To { get; set; }

    /// <summary>
    /// Gets or sets whether <see cref="From"/> is treated as a .NET regular expression.
    /// Defaults to <see langword="false"/> (literal string match).
    /// </summary>
    [XmlAttribute("regex")]
    public bool IsRegex { get; set; }

    /// <summary>
    /// Per-dimension-value form declarations expanded at load time.
    /// The most specific matching form is selected at runtime.
    /// </summary>
    [XmlElement("Variant")]
    public List<FormVariantType>? FormVariants { get; set; }
}

/// <summary>
/// A trigger that fires at a specific point in the conversion pipeline and applies
/// text replacements, each optionally selecting a form based on active variant values.
/// </summary>
public class TriggerType
{
    /// <summary>
    /// Gets or sets when the trigger fires.
    /// Syntax: "group", "group(N)", "group(N,M)", "groupWithScale", "groupWithScale(N)", "end".
    /// Group indices: 0=units, 1=thousands, 2=millions, … (negative values reserved for decimals).
    /// </summary>
    [XmlAttribute("executeAt")]
    public string ExecuteAt { get; set; }

    /// <summary>
    /// Gets or sets the replacement rules applied when this trigger fires.
    /// Each Replace is applied independently; for each one the most specific matching
    /// variant form is selected and applied exactly once.
    /// </summary>
    [XmlElement("Replace")]
    public List<TriggerReplaceType>? Replaces { get; set; }
}

/// <summary>
/// Describes a range of years for which the split-at-hundreds algorithm applies in <see cref="YearFormatType"/>.
/// </summary>
public class YearFormatSplitRangeType
{
    /// <summary>Gets or sets the lower bound of the range (inclusive).</summary>
    [XmlAttribute("from")]
    public int From { get; set; }

    /// <summary>Gets or sets the upper bound of the range (inclusive).</summary>
    [XmlAttribute("to")]
    public int To { get; set; }
}

/// <summary>
/// Configures the year-format algorithm used by <see cref="NumberToStringConverter.ConvertYear(int)"/>.
/// When present, years within any declared <see cref="SplitRanges"/> are split at the hundreds boundary.
/// </summary>
public class YearFormatType
{
    /// <summary>
    /// Gets or sets the word appended when the year is a round century (remainder == 0).
    /// Example: "hundred" → 1900 reads as "nineteen hundred".
    /// </summary>
    [XmlAttribute("hundredWord")]
    public string? HundredWord { get; set; }

    /// <summary>
    /// Gets or sets the connector inserted before single-digit remainders (1–9).
    /// Example: "oh" → 2005 reads as "twenty oh five".
    /// </summary>
    [XmlAttribute("zeroConnector")]
    public string? ZeroConnector { get; set; }

    /// <summary>
    /// Gets or sets the suffix appended after the year body for negative (BC) years,
    /// replacing the default <c>minus</c> prefix.
    /// Example: <c>"av. J.-C."</c> → -44 reads as "quarante-quatre av. J.-C.".
    /// When absent, negative years use the language's <c>minus</c> template.
    /// </summary>
    [XmlAttribute("beforeChristSuffix")]
    public string? BeforeChristSuffix { get; set; }

    /// <summary>Gets or sets the year ranges for which the split algorithm applies.</summary>
    [XmlElement("SplitRange")]
    public List<YearFormatSplitRangeType> SplitRanges { get; set; } = new();
}

/// <summary>
/// Represents a numbered group of digits used when decomposing large numbers.
/// </summary>
public class GroupType : DigitListType
{
    /// <summary>
    /// Gets or sets the hierarchical level of the group.
    /// </summary>
    [XmlAttribute("level")]
    public int Level { get; set; }
}

/// <summary>
/// Collection wrapper for <see cref="GroupType"/> entries.
/// </summary>
public class GroupsListType
{
    /// <summary>
    /// Gets or sets the ordered groups that participate in formatting.
    /// </summary>
    [XmlElement("Group")]
    public List<GroupType> Groups { get; set; }
}

/// <summary>
/// Describes the suffixes appended to scale names.
/// </summary>
public class SuffixesType
{
    /// <summary>
    /// Gets or sets the suffix values applied to scale prefixes.
    /// </summary>
    [XmlElement(ElementName = "Suffix")]
    public List<string> Values { get; set; }
}

/// <summary>
/// Holds the static names mapped to particular scales.
/// </summary>
public class StaticNamesType
{
    /// <summary>
    /// Gets or sets the predefined scale names.
    /// </summary>
    [XmlElement(ElementName = "Scale")]
    public List<NumberType> Scales { get; set; }
}

/// <summary>
/// Configures the prefixes and metadata needed to build number scale names.
/// </summary>
public class NumberScaleType
{
    /// <summary>
    /// Gets or sets a value indicating whether the first letter of generated names should be upper-case.
    /// </summary>
    [XmlAttribute("firstLetterUpperCase")]
    public bool FirstLetterUpperCase { get; set; }

    /// <summary>
    /// Gets or sets the literal inserted when a group value is zero.
    /// </summary>
    [XmlAttribute("voidGroup")]
    public string VoidGroup { get; set; }

    /// <summary>
    /// Gets or sets the separator used between prefix segments.
    /// </summary>
    [XmlAttribute("groupSeparator")]
    public string GroupSeparator { get; set; }

    /// <summary>
    /// Gets or sets the index offset applied when computing scale names.
    /// </summary>
    [XmlAttribute("startIndex")]
    public int StartIndex { get; set; }

    /// <summary>
    /// Gets or sets the static names applied to the first scale levels.
    /// </summary>
    [XmlElement(ElementName = "StaticNames")]
    public StaticNamesType StaticNames { get; set; }

    /// <summary>
    /// Gets or sets the prefixes used for the 10^0 scale.
    /// </summary>
    [XmlElement(ElementName = "Scale0Prefixes")]
    public DigitListType Scale0Prefixes { get; set; }

    /// <summary>
    /// Gets or sets the prefixes used for unit multipliers.
    /// </summary>
    [XmlElement(ElementName = "UnitsPrefixes")]
    public DigitListType UnitsPrefixes { get; set; }

    /// <summary>
    /// Gets or sets the prefixes used for tens multipliers.
    /// </summary>
    [XmlElement(ElementName = "TensPrefixes")]
    public DigitListType TensPrefixes { get; set; }

    /// <summary>
    /// Gets or sets the prefixes used for hundreds multipliers.
    /// </summary>
    [XmlElement(ElementName = "HundredsPrefixes")]
    public DigitListType HundredsPrefixes { get; set; }

    /// <summary>
    /// Gets or sets the suffix table associated with scale prefixes.
    /// </summary>
    [XmlElement(ElementName = "Suffixes")]
    public SuffixesType Suffixes { get; set; }
}

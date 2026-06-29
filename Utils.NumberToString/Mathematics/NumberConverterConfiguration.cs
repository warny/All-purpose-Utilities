using System;
using System.Xml;
using System.Xml.Serialization;

namespace Utils.Mathematics;

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
/// Describes a single ordinal exception that maps an integer to its ordinal text.
/// </summary>
public class OrdinalExceptionType
{
    /// <summary>Gets or sets the numeric value of the exception.</summary>
    [XmlAttribute("value")]
    public long Value { get; set; }

    /// <summary>Gets or sets the ordinal text for <see cref="Value"/>.</summary>
    [XmlAttribute("string")]
    public string StringValue { get; set; }
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

    /// <summary>Gets or sets the ordinal replacement word.</summary>
    [XmlAttribute("to")]
    public string To { get; set; }
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
    /// Gets or sets the variant-specific ordinal blocks (checked in specificity order).
    /// </summary>
    [XmlElement("OrdinalVariant")]
    public List<OrdinalVariantType>? Variants { get; set; }
}

/// <summary>
/// Variant-specific ordinal overrides: dimension constraints (arbitrary XML attributes),
/// optional suffix/removeTrailing overrides, plus variant exceptions and word rules.
/// Exceptions and word rules fall through to the base ordinal config when the variant
/// does not supply a match.
/// </summary>
public class OrdinalVariantType
{
    /// <summary>Gets or sets dimension constraints (e.g. gender="femenino").</summary>
    [XmlAnyAttribute]
    public XmlAttribute[]? DimensionAttributes { get; set; }

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

    /// <summary>Returns the dimension constraints as a case-insensitive dictionary.</summary>
    [XmlIgnore]
    public IReadOnlyDictionary<string, string> Dimensions
    {
        get
        {
            if (DimensionAttributes == null || DimensionAttributes.Length == 0)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return DimensionAttributes
                .Where(a => string.IsNullOrEmpty(a.NamespaceURI))
                .ToDictionary(a => a.LocalName, a => a.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
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
    /// Gets or sets the replacement text.
    /// </summary>
    [XmlAttribute("newValue")]
    public string NewValue { get; set; }

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
}

/// <summary>
/// Declares one dimension of grammatical variation for a language (e.g. "gender" or "case")
/// together with its ordered set of values. The first value is the default when no explicit
/// variant is requested.
/// </summary>
public class VariantDimensionType
{
    /// <summary>Gets or sets the dimension name (e.g. "gender").</summary>
    [XmlAttribute("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the comma-separated ordered list of valid values for this dimension
    /// (e.g. "masculin,feminin,neutrum"). The first value is the default.
    /// </summary>
    [XmlAttribute("values")]
    public string ValuesRaw { get; set; }
}

/// <summary>
/// Describes a morphological variant: a set of dimension constraints (encoded as arbitrary
/// XML attributes, e.g. <c>gender="feminin"</c>) and the replacement rules to apply when
/// the constraints are satisfied.
/// </summary>
public class VariantType
{
    /// <summary>
    /// Gets or sets the dimension constraints supplied as XML attributes (e.g. gender="feminin").
    /// Captured via <see cref="XmlAnyAttributeAttribute"/>; standard XML attributes are excluded.
    /// </summary>
    [XmlAnyAttribute]
    public XmlAttribute[]? DimensionAttributes { get; set; }

    /// <summary>Gets or sets the replacement rules applied when this variant is active.</summary>
    [XmlElement("Replacement")]
    public List<ReplacementType>? Replacements { get; set; }

    /// <summary>
    /// Returns the dimension constraints as a case-insensitive dictionary.
    /// </summary>
    [XmlIgnore]
    public IReadOnlyDictionary<string, string> Dimensions
    {
        get
        {
            if (DimensionAttributes == null || DimensionAttributes.Length == 0)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return DimensionAttributes
                .Where(a => string.IsNullOrEmpty(a.NamespaceURI))
                .ToDictionary(a => a.LocalName, a => a.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
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

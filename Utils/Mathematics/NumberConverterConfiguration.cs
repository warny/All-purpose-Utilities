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
    /// Gets or sets the optional lambda used to adjust the converted text.
    /// </summary>
    [XmlElement(ElementName = "AdjustFunction")]
    public string AdjustFunction { get; set; }

    /// <summary>
    /// Gets or sets the fraction configuration applied to decimal values.
    /// </summary>
    [XmlElement(ElementName = "Fractions")]
    public FractionListType Fractions { get; set; }
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

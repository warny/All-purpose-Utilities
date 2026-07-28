using System.Collections.Generic;
using System.Xml.Serialization;

namespace Utils.NumberToString
{
    /// <summary>
    /// Represents a value that may be explicitly specified or absent.
    /// Used internally to preserve the presence/absence distinction that the public
    /// value-type properties (<c>int GroupSize</c>, <c>bool FirstLetterUpperCase</c>,
    /// <c>int StartIndex</c>) cannot express on their own during <c>baseOn</c> merging.
    /// </summary>
    /// <typeparam name="T">The wrapped value type.</typeparam>
    internal readonly record struct Optional<T>
    {
        public bool IsSpecified { get; }

        public T Value { get; }

        private Optional(bool isSpecified, T value)
        {
            IsSpecified = isSpecified;
            Value = value;
        }

        public static Optional<T> Unspecified => new(false, default!);

        public static Optional<T> Of(T value) => new(true, value);

        public T GetValueOrDefault(T fallback) => IsSpecified ? Value : fallback;
    }

    /// <summary>
    /// XML deserialization root. Mirrors <see cref="Numbers"/> but references the internal
    /// language model so the presence-sensitive attributes carry a <c>Specified</c> companion
    /// without exposing those technical members on the public configuration API.
    /// </summary>
    /// <remarks>
    /// This type is public only because <see cref="System.Xml.Serialization.XmlSerializer"/>
    /// requires public types; it is not part of the intended public configuration surface.
    /// </remarks>
    [XmlRoot("Numbers")]
    public sealed class NumbersXmlModel
    {
        /// <summary>Gets or sets the language models declared in the document.</summary>
        [XmlElement(ElementName = "Language")]
        public List<LanguageXmlModel>? Languages { get; set; }
    }

    /// <summary>
    /// Internal XML model for a language definition. Holds the presence-sensitive
    /// <c>groupSize</c> attribute (plus its <c>Specified</c> companion) alongside every other
    /// XML member of a language so XmlSerializer can populate it directly. It is projected onto
    /// the public <see cref="LanguageType"/> after inheritance resolution.
    /// </summary>
    /// <remarks>
    /// This type is public only because <see cref="System.Xml.Serialization.XmlSerializer"/>
    /// requires public types; it is not part of the intended public configuration surface.
    /// </remarks>
    public sealed class LanguageXmlModel
    {
        /// <summary>Gets or sets the culture names served by the configuration.</summary>
        [XmlElement("Culture")]
        public List<string>? Cultures { get; set; }

        /// <summary>Gets or sets the base culture identifier(s) to inherit defaults from.</summary>
        [XmlAttribute("baseOn")]
        public string? BaseOn { get; set; }

        /// <summary>Gets or sets the number of digits grouped together when formatting.</summary>
        [XmlAttribute("groupSize")]
        public int GroupSize { get; set; }

        /// <summary>Set by XmlSerializer when the <c>groupSize</c> attribute is present.</summary>
        [XmlIgnore]
        public bool GroupSizeSpecified { get; set; }

        /// <summary>Gets or sets the separator used between groups.</summary>
        [XmlAttribute("separator")]
        public string? Separator { get; set; }

        /// <summary>Gets or sets the textual separator used between digit groups.</summary>
        [XmlAttribute("groupSeparator")]
        public string? GroupSeparator { get; set; }

        /// <summary>Gets or sets the literal used to represent zero.</summary>
        [XmlAttribute("zero")]
        public string? Zero { get; set; }

        /// <summary>Gets or sets the literal prefix applied to negative numbers.</summary>
        [XmlAttribute("minus")]
        public string? Minus { get; set; }

        /// <summary>Gets or sets the string inserted before fractional parts.</summary>
        [XmlAttribute("decimalSeparator")]
        public string? DecimalSeparator { get; set; }

        /// <summary>Gets or sets the connector used when pronouncing fractions.</summary>
        [XmlAttribute("fractionSeparator")]
        public string? FractionSeparator { get; set; }

        /// <summary>Gets or sets the maximum supported number as a string representation.</summary>
        [XmlAttribute("maxNumber")]
        public string? MaxNumber { get; set; }

        /// <summary>Gets or sets the group definitions used when splitting large numbers.</summary>
        [XmlElement(ElementName = "Groups")]
        public GroupsListType? Groups { get; set; }

        /// <summary>Gets or sets the special-case number mappings for the language.</summary>
        [XmlElement(ElementName = "Exceptions")]
        public NumberListType? Exceptions { get; set; }

        /// <summary>Gets or sets the scale definition for large numbers.</summary>
        [XmlElement(ElementName = "NumberScale")]
        public NumberScaleXmlModel? NumberScale { get; set; }

        /// <summary>Gets or sets the post-processing replacement rules.</summary>
        [XmlElement(ElementName = "Replacements")]
        public ReplacementsListType? Replacements { get; set; }

        /// <summary>Gets or sets the optional language-specific finalizer type name.</summary>
        [XmlElement(ElementName = "LanguageSpecifics")]
        public string? LanguageSpecificsTypeName { get; set; }

        /// <summary>Gets or sets the fraction configuration applied to decimal values.</summary>
        [XmlElement(ElementName = "Fractions")]
        public FractionListType? Fractions { get; set; }

        /// <summary>Gets or sets the ordinal configuration for this language.</summary>
        [XmlElement(ElementName = "Ordinals")]
        public OrdinalsType? Ordinals { get; set; }

        /// <summary>Gets or sets the morphological variant configuration for this language.</summary>
        [XmlElement(ElementName = "Variants")]
        public VariantsType? Variants { get; set; }

        /// <summary>Gets or sets the year-format configuration.</summary>
        [XmlElement(ElementName = "YearFormat")]
        public YearFormatType? YearFormat { get; set; }

        /// <summary>Gets or sets the trigger rules applied during the conversion pipeline.</summary>
        [XmlElement(ElementName = "Trigger")]
        public List<TriggerType>? Triggers { get; set; }

        /// <summary>Gets or sets the multiplicative configuration.</summary>
        [XmlElement(ElementName = "Multiplicatives")]
        public MultiplicativesType? Multiplicatives { get; set; }

        /// <summary>Gets or sets the word inserted between scale groups when the lower group is small.</summary>
        [XmlAttribute("groupConnector")]
        public string? GroupConnector { get; set; }

        /// <summary>Gets or sets the threshold below which the group connector is used.</summary>
        [XmlAttribute("groupConnectorThreshold")]
        public string? GroupConnectorThresholdString { get; set; }

        /// <summary>Gets or sets the word inserted inside a group between hundreds and the lower part.</summary>
        [XmlAttribute("intraGroupConnector")]
        public string? IntraGroupConnector { get; set; }

        /// <summary>Gets or sets the threshold below which the intra-group connector is inserted.</summary>
        [XmlAttribute("intraGroupConnectorThreshold")]
        public string? IntraGroupConnectorThresholdString { get; set; }

        /// <summary>Gets or sets the connector word inserted between a group's digit text and the scale name.</summary>
        [XmlAttribute("scaleConnector")]
        public string? ScaleConnector { get; set; }

        /// <summary>Gets or sets the threshold at or above which the scale connector is inserted.</summary>
        [XmlAttribute("scaleConnectorThreshold")]
        public string? ScaleConnectorThresholdString { get; set; }

        /// <summary>Gets or sets the time-unit configuration.</summary>
        [XmlElement(ElementName = "TimeUnits")]
        public TimeUnitsType? TimeUnits { get; set; }

        /// <summary>Gets or sets the date-format configuration.</summary>
        [XmlElement(ElementName = "DateFormat")]
        public DateFormatType? DateFormat { get; set; }
    }

    /// <summary>
    /// Internal XML model for a number-scale definition. Carries the two presence-sensitive
    /// attributes (<c>firstLetterUpperCase</c> and <c>startIndex</c>) with their <c>Specified</c>
    /// companions. Projected onto the public <see cref="NumberScaleType"/> after resolution.
    /// </summary>
    /// <remarks>
    /// This type is public only because <see cref="System.Xml.Serialization.XmlSerializer"/>
    /// requires public types; it is not part of the intended public configuration surface.
    /// </remarks>
    public sealed class NumberScaleXmlModel
    {
        /// <summary>Gets or sets whether the first letter of generated names should be upper-case.</summary>
        [XmlAttribute("firstLetterUpperCase")]
        public bool FirstLetterUpperCase { get; set; }

        /// <summary>Set by XmlSerializer when the <c>firstLetterUpperCase</c> attribute is present.</summary>
        [XmlIgnore]
        public bool FirstLetterUpperCaseSpecified { get; set; }

        /// <summary>Gets or sets the literal inserted when a group value is zero.</summary>
        [XmlAttribute("voidGroup")]
        public string? VoidGroup { get; set; }

        /// <summary>Gets or sets the separator used between prefix segments.</summary>
        [XmlAttribute("groupSeparator")]
        public string? GroupSeparator { get; set; }

        /// <summary>Gets or sets the index offset applied when computing scale names.</summary>
        [XmlAttribute("startIndex")]
        public int StartIndex { get; set; }

        /// <summary>Set by XmlSerializer when the <c>startIndex</c> attribute is present.</summary>
        [XmlIgnore]
        public bool StartIndexSpecified { get; set; }

        /// <summary>Gets or sets the static names applied to the first scale levels.</summary>
        [XmlElement(ElementName = "StaticNames")]
        public StaticNamesType? StaticNames { get; set; }

        /// <summary>Gets or sets the prefixes used for the 10^0 scale.</summary>
        [XmlElement(ElementName = "Scale0Prefixes")]
        public DigitListType? Scale0Prefixes { get; set; }

        /// <summary>Gets or sets the prefixes used for unit multipliers.</summary>
        [XmlElement(ElementName = "UnitsPrefixes")]
        public DigitListType? UnitsPrefixes { get; set; }

        /// <summary>Gets or sets the prefixes used for tens multipliers.</summary>
        [XmlElement(ElementName = "TensPrefixes")]
        public DigitListType? TensPrefixes { get; set; }

        /// <summary>Gets or sets the prefixes used for hundreds multipliers.</summary>
        [XmlElement(ElementName = "HundredsPrefixes")]
        public DigitListType? HundredsPrefixes { get; set; }

        /// <summary>Gets or sets the suffix table associated with scale prefixes.</summary>
        [XmlElement(ElementName = "Suffixes")]
        public SuffixesType? Suffixes { get; set; }
    }

    /// <summary>
    /// Internal mergeable definition of a language. Uses <see cref="Optional{T}"/> for the
    /// presence-sensitive value-type field (<see cref="GroupSize"/>) and plain reference fields
    /// (where <see langword="null"/> already means "absent") for the rest. This is the type on
    /// which <c>baseOn</c> inheritance is resolved and merged.
    /// </summary>
    internal sealed class LanguageDefinition
    {
        public IReadOnlyList<string> Cultures { get; init; } = [];
        public string? BaseOn { get; init; }
        public Optional<int> GroupSize { get; init; }
        public string? Separator { get; init; }
        public string? GroupSeparator { get; init; }
        public string? Zero { get; init; }
        public string? Minus { get; init; }
        public string? DecimalSeparator { get; init; }
        public string? FractionSeparator { get; init; }
        public string? MaxNumber { get; init; }
        public GroupsListType? Groups { get; init; }
        public NumberListType? Exceptions { get; init; }
        public NumberScaleDefinition? NumberScale { get; init; }
        public ReplacementsListType? Replacements { get; init; }
        public string? LanguageSpecificsTypeName { get; init; }
        public FractionListType? Fractions { get; init; }
        public OrdinalsType? Ordinals { get; init; }
        public VariantsType? Variants { get; init; }
        public YearFormatType? YearFormat { get; init; }
        public List<TriggerType>? Triggers { get; init; }
        public MultiplicativesType? Multiplicatives { get; init; }
        public string? GroupConnector { get; init; }
        public string? GroupConnectorThresholdString { get; init; }
        public string? IntraGroupConnector { get; init; }
        public string? IntraGroupConnectorThresholdString { get; init; }
        public string? ScaleConnector { get; init; }
        public string? ScaleConnectorThresholdString { get; init; }
        public TimeUnitsType? TimeUnits { get; init; }
        public DateFormatType? DateFormat { get; init; }
    }

    /// <summary>
    /// Internal mergeable definition of a number scale. Uses <see cref="Optional{T}"/> for the
    /// two presence-sensitive value-type fields (<see cref="FirstLetterUpperCase"/> and
    /// <see cref="StartIndex"/>) so an explicit <c>false</c>/<c>0</c> overrides an inherited value
    /// while an absent attribute inherits it.
    /// </summary>
    internal sealed class NumberScaleDefinition
    {
        public Optional<bool> FirstLetterUpperCase { get; init; }
        public string? VoidGroup { get; init; }
        public string? GroupSeparator { get; init; }
        public Optional<int> StartIndex { get; init; }
        public StaticNamesType? StaticNames { get; init; }
        public DigitListType? Scale0Prefixes { get; init; }
        public DigitListType? UnitsPrefixes { get; init; }
        public DigitListType? TensPrefixes { get; init; }
        public DigitListType? HundredsPrefixes { get; init; }
        public SuffixesType? Suffixes { get; init; }
    }
}

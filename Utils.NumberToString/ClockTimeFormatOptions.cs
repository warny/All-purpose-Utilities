using System.Collections.Generic;
using System.Xml.Serialization;
using Utils.Range;

namespace Utils.NumberToString;

/// <summary>Specifies how a clock hour placeholder is rendered.</summary>
public enum ClockHourForm
{
    /// <summary>Renders the hour as a cardinal number.</summary>
    [XmlEnum("cardinal")]
    Cardinal,
    /// <summary>Renders the hour as an ordinal number.</summary>
    [XmlEnum("ordinal")]
    Ordinal,
    /// <summary>Renders the hour with the configured <c>hour</c> time unit.</summary>
    [XmlEnum("timeUnit")]
    TimeUnit,
}

/// <summary>Specifies how a minute amount is measured from its reference.</summary>
public enum ClockAmountDirection
{
    /// <summary>Computes <c>reference - minute</c>.</summary>
    [XmlEnum("before")]
    Before,
    /// <summary>Computes <c>minute - reference</c>.</summary>
    [XmlEnum("after")]
    After,
}

/// <summary>Defines one configurable clock-position rule.</summary>
/// <param name="Range">Minute positions to which the rule applies.</param>
/// <param name="HourOffset">Offset applied to the rounded hour, normalized modulo 24.</param>
/// <param name="HourForm">Rendering mode for <c>{hour}</c>.</param>
/// <param name="Pattern">Literal pattern containing the supported <c>{hour}</c> and <c>{amount}</c> placeholders.</param>
/// <param name="AmountReference">Optional minute reference used to calculate <c>{amount}</c>.</param>
/// <param name="AmountDirection">Optional direction used to calculate <c>{amount}</c>.</param>
public sealed record ClockTimeRule(
    IntRange<int> Range,
    int HourOffset,
    ClockHourForm HourForm,
    string Pattern,
    int? AmountReference = null,
    ClockAmountDirection? AmountDirection = null);

/// <summary>Defines idiomatic clock-time rounding and position rules.</summary>
public sealed class ClockTimeFormatOptions
{
    /// <summary>Gets or sets the rounding step in minutes.</summary>
    public int Step { get; set; } = 5;

    /// <summary>
    /// Gets or sets the numeric hour cycle used to project <c>{hour}</c> after special-hour
    /// replacement has been considered. Supported values are 12 and 24.
    /// </summary>
    public int HourCycle { get; set; } = 24;

    /// <summary>Gets or sets the rules. Rules replace the inherited XML section as a whole.</summary>
    public IReadOnlyList<ClockTimeRule> Rules { get; set; } = [];
}

/// <summary>Controls one idiomatic clock-time conversion call.</summary>
public sealed class ClockTimeConversionOptions
{
    /// <summary>Gets or initializes whether configured special-hour words replace numeric hours.</summary>
    public bool ReplaceSpecialHours { get; init; } = true;
}

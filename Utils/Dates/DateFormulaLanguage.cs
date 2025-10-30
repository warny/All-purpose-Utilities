namespace Utils.Dates;

/// <summary>
/// Represents culture specific tokens used to interpret date formulas.
/// </summary>
public sealed class DateFormulaLanguage
{
    /// <summary>Token indicating the start of a period.</summary>
    public required char Start { get; init; }
    /// <summary>Token indicating the end of a period.</summary>
    public required char End { get; init; }
    /// <summary>Token representing a day unit.</summary>
    public required char Day { get; init; }
    /// <summary>Token representing a week unit.</summary>
    public required char Week { get; init; }
    /// <summary>Token representing a month unit.</summary>
    public required char Month { get; init; }
    /// <summary>Token representing a quarter unit.</summary>
    public required char Quarter { get; init; }
    /// <summary>Token representing a year unit.</summary>
    public required char Year { get; init; }
    /// <summary>Token representing a working day unit.</summary>
    public required char WorkingDay { get; init; }
    /// <summary>Mapping between two-letter day names and <see cref="DayOfWeek"/>.</summary>
    public required IReadOnlyDictionary<string, DayOfWeek> Days { get; init; }
}

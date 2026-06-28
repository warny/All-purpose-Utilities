namespace Utils.Mathematics;

/// <summary>
/// Defines the unit and subunit names used to convert a currency amount to words.
/// </summary>
public sealed class CurrencyDefinition
{
    /// <summary>Gets the singular name of the main currency unit (e.g. "euro", "dollar").</summary>
    public required string UnitSingular { get; init; }

    /// <summary>Gets the plural name of the main currency unit (e.g. "euros", "dollars").</summary>
    public required string UnitPlural { get; init; }

    /// <summary>Gets the singular name of the subunit (e.g. "centime", "cent").</summary>
    public required string SubunitSingular { get; init; }

    /// <summary>Gets the plural name of the subunit (e.g. "centimes", "cents").</summary>
    public required string SubunitPlural { get; init; }

    /// <summary>Gets the connector word inserted between the unit and subunit parts (e.g. "and", "et").</summary>
    public string Connector { get; init; } = "and";

    /// <summary>Gets the number of decimal digits for the subunit (default: 2 for cents).</summary>
    public int SubunitDigits { get; init; } = 2;
}

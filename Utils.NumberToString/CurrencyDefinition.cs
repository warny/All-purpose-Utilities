namespace Utils.NumberToString;

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

    /// <summary>
    /// Forces grammatical variant dimensions (e.g. gender) on the numeral fragment of the main
    /// unit only, on top of the language's declared dimension defaults and any caller-supplied
    /// variant. Caller variants for other dimensions remain active. Defaults to
    /// <see cref="ForcedVariantSet.Empty"/> (identical to pre-NTS-04 behavior); existing object
    /// initializers that don't set this property are unaffected.
    /// </summary>
    public ForcedVariantSet UnitForcedVariants { get; init; } = ForcedVariantSet.Empty;

    /// <summary>
    /// Forces grammatical variant dimensions (e.g. gender) on the numeral fragment of the subunit
    /// only, independently of <see cref="UnitForcedVariants"/>. Defaults to
    /// <see cref="ForcedVariantSet.Empty"/>.
    /// </summary>
    public ForcedVariantSet SubunitForcedVariants { get; init; } = ForcedVariantSet.Empty;
}

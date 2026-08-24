using System.Linq;

namespace Utils.NumberToString
{
    public partial class NumberToStringConverter
    {
        /// <summary>
        /// Internal merged runtime representation of a configured time unit. Not public — the
        /// public, backward-compatible split view is <see cref="TimeUnits"/> (singular/plural/
        /// count-one form) and <see cref="TimeUnitForcedVariants"/> (per-unit forced variants).
        /// </summary>
        private readonly record struct TimeUnitDefinition(string Singular, string Plural, string? Count1Form, ForcedVariantSet ForcedVariants);

        /// <summary>
        /// Validates that every dimension and value forced by <paramref name="forced"/> is declared
        /// for this converter's language. A no-op when <paramref name="forced"/> is
        /// <see cref="ForcedVariantSet.Empty"/> (the common case).
        /// </summary>
        /// <param name="forced">The forced variant set to validate.</param>
        /// <param name="constituentDescription">
        /// Identifies the owning constituent in diagnostics, e.g. <c>"TimeUnits[hour]"</c>,
        /// <c>"CurrencyDefinition.UnitForcedVariants"</c>, or <c>"Fractions[2]"</c>.
        /// </param>
        /// <exception cref="NumberToStringConfigurationException">
        /// A referenced dimension or value is not declared for <see cref="LanguageIdentifier"/> —
        /// error code <c>"UNTS006"</c>.
        /// </exception>
        internal void ValidateForcedVariants(ForcedVariantSet forced, string constituentDescription)
        {
            if (forced.IsEmpty) return;

            foreach (var pair in forced.Constraints.Values)
            {
                string dimension = pair.Key;
                string value = pair.Value;

                if (!_dimensionIndex.TryGetValue(dimension, out var dim))
                    throw new NumberToStringConfigurationException("UNTS006", LanguageIdentifier, constituentDescription,
                        $"Language '{LanguageIdentifier}', {constituentDescription}: unknown forced variant dimension '{dimension}'. " +
                        $"Allowed dimensions: {string.Join(", ", VariantDimensions.Select(d => d.Name))}.");

                if (!dim.Values.Contains(value, System.StringComparer.OrdinalIgnoreCase))
                    throw new NumberToStringConfigurationException("UNTS006", LanguageIdentifier, constituentDescription,
                        $"Language '{LanguageIdentifier}', {constituentDescription}: unknown forced variant value '{value}' for dimension '{dim.Name}'. " +
                        $"Allowed values: {string.Join(", ", dim.Values)}.");
            }
        }
    }
}

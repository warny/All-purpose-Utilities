using System.Collections.Generic;
using System.Linq;

namespace Utils.NumberToString
{
    public partial class NumberToStringConverter
    {
        /// <summary>
        /// Internal merged runtime representation of a configured time unit. Not public — the
        /// public, backward-compatible split view is <see cref="TimeUnits"/> (singular/plural/
        /// count-one form), <see cref="TimeUnitForcedVariants"/> (per-unit forced variants),
        /// <see cref="TimeUnitForms"/>, and <see cref="TimeUnitFormSelectors"/>.
        /// </summary>
        /// <param name="Singular">The singular word (e.g. "hour").</param>
        /// <param name="Plural">The plural word (e.g. "hours").</param>
        /// <param name="Count1Form">Optional literal numeral override for count == 1.</param>
        /// <param name="ForcedVariants">Canonicalized numeral-grammar constraints this unit forces.</param>
        /// <param name="Forms">
        /// Always populated with at least "singular"/"plural" (synthesized from
        /// <paramref name="Singular"/>/<paramref name="Plural"/>), merged with any configured
        /// <see cref="NumberToStringConverterOptions.TimeUnitForms"/> override.
        /// </param>
        /// <param name="Selector">
        /// <see cref="DefaultLexicalFormSelector"/> unless overridden via
        /// <see cref="NumberToStringConverterOptions.TimeUnitFormSelectors"/>.
        /// </param>
        private readonly record struct TimeUnitDefinition(
            string Singular, string Plural, string? Count1Form, ForcedVariantSet ForcedVariants,
            LexicalFormSet Forms, ILexicalFormSelector Selector);

        /// <summary>
        /// Validates that every dimension and value forced by <paramref name="forced"/> is declared
        /// for this converter's language, and returns a canonicalized <see cref="ForcedVariantSet"/>
        /// where every dimension key is the declared <see cref="VariantDimension.Name"/> — never a
        /// <see cref="VariantDimension.LocalName"/> alias. Returns <see cref="ForcedVariantSet.Empty"/>
        /// unchanged when <paramref name="forced"/> is already empty (the common case).
        /// </summary>
        /// <remarks>
        /// Canonicalization matters because <see cref="VariantRule"/> matching keys exclusively on
        /// canonical dimension names (see <see cref="BuildVariantQuery"/>/<see cref="ApplyVariantRules"/>).
        /// A forced set that retained an alias key (e.g. French "genre" instead of "gender") would sit
        /// alongside the base query's canonical "gender" entry and never actually override it. Callers
        /// must use the returned, canonicalized set — not <paramref name="forced"/> — when overlaying
        /// a runtime variant query.
        /// </remarks>
        /// <param name="forced">The forced variant set to validate and canonicalize.</param>
        /// <param name="constituentDescription">
        /// Identifies the owning constituent in diagnostics, e.g. <c>"TimeUnits[hour]"</c>,
        /// <c>"CurrencyDefinition.UnitForcedVariants"</c>, or <c>"Fractions[2]"</c>.
        /// </param>
        /// <returns>A canonicalized <see cref="ForcedVariantSet"/> using only canonical dimension names.</returns>
        /// <exception cref="NumberToStringConfigurationException">
        /// A referenced dimension or value is not declared for <see cref="LanguageIdentifier"/> —
        /// error code <c>"UNTS006"</c> — or two entries (a canonical name and one of its aliases, or
        /// two aliases of the same dimension) resolve to the same declared dimension — error code
        /// <c>"UNTS004"</c>.
        /// </exception>
        internal ForcedVariantSet CanonicalizeForcedVariants(ForcedVariantSet forced, string constituentDescription)
        {
            if (forced.IsEmpty) return ForcedVariantSet.Empty;

            var canonicalPairs = new List<(string Dimension, string Value)>(forced.Constraints.Values.Count);
            var seenCanonicalDimensions = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

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

                if (!seenCanonicalDimensions.Add(dim.Name))
                    throw new NumberToStringConfigurationException("UNTS004", LanguageIdentifier, constituentDescription,
                        $"Language '{LanguageIdentifier}', {constituentDescription}: dimension '{dim.Name}' is forced more " +
                        $"than once (declared as '{dimension}'); a canonical dimension name and one of its aliases — or " +
                        "two aliases of the same dimension — cannot force that dimension with different values.");

                canonicalPairs.Add((dim.Name, value));
            }

            return ForcedVariantSet.Create(canonicalPairs);
        }
    }
}

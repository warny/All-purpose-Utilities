using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Represents zero or more grammatical variant dimension values that a configured lexical
/// constituent (a time unit, a currency unit/subunit, a fraction denominator term, …) forces on
/// the numeric fragment it governs.
/// </summary>
/// <remarks>
/// <para>
/// Forced values take precedence, dimension by dimension, over both the language's declared
/// dimension defaults and any caller-supplied variant for the same dimension — but only for the
/// single numeric fragment the owning constituent renders. A caller-supplied dimension not forced
/// here survives untouched; other fragments in the same composite phrase, and any subsequent
/// public conversion call, are never affected. See <see cref="Overlay"/>.
/// </para>
/// <para>
/// Instances are immutable. <see cref="Parse"/> and <see cref="Create"/> perform only syntactic
/// validation (well-formed dimension/value pairs, no duplicate *raw* dimension key); they do not
/// know which dimensions or values a particular language declares, and a raw key may still be a
/// <c>localName</c> alias (e.g. French "genre") rather than the canonical dimension name (e.g.
/// "gender"). Semantic validation against a specific converter's declared variant dimensions is a
/// separate, converter-owned step (performed once at configuration time, not on every conversion
/// call) — see <c>NumberToStringConverter.CanonicalizeForcedVariants</c> — which also normalizes
/// every accepted dimension to its canonical name and rejects a canonical/alias pair that both
/// resolve to the same declared dimension as a semantic duplicate. Only the canonicalized result
/// of that step is used to overlay a runtime variant query, so
/// <see cref="NumberToStringConverter.VariantRule"/> matching (which keys exclusively on canonical
/// dimension names) always sees the forced value.
/// </para>
/// </remarks>
public sealed class ForcedVariantSet
{
    /// <summary>Gets a forced variant set with no forced dimensions. Overlaying it is a no-op.</summary>
    public static ForcedVariantSet Empty { get; } = new(new VariantConstraintSet([]));

    private ForcedVariantSet(VariantConstraintSet constraints) => Constraints = constraints;

    /// <summary>Gets the normalized dimension/value pairs (dimension comparison is case-insensitive).</summary>
    internal VariantConstraintSet Constraints { get; }

    /// <summary>Gets whether no dimension is forced by this set.</summary>
    public bool IsEmpty => Constraints.Values.Count == 0;

    /// <summary>
    /// Parses a comma-separated list of <c>dimension=value</c> pairs, e.g. <c>"gender=feminin"</c>
    /// or <c>"gender=feminin,case=genitive"</c>. A <see langword="null"/>, empty, or
    /// whitespace-only string returns <see cref="Empty"/>.
    /// </summary>
    /// <param name="text">The comma-separated forced-variant declaration to parse.</param>
    /// <param name="languageIdentifier">The owning language identifier, used only for diagnostics.</param>
    /// <param name="configurationPath">A description of the configuration location, used only for diagnostics.</param>
    /// <exception cref="NumberToStringConfigurationException">
    /// The syntax is malformed (missing '=', empty dimension, or empty value) — error code
    /// <c>"UNTS005"</c> — or the same dimension is declared more than once — error code
    /// <c>"UNTS004"</c> (raised by the shared <see cref="VariantConstraintSet"/> constructor).
    /// </exception>
    public static ForcedVariantSet Parse(string? text, string? languageIdentifier = null, string? configurationPath = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return Empty;

        string path = configurationPath ?? "ForcedVariants";
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var token in text.Split(','))
        {
            string trimmedToken = token.Trim();
            int eq = trimmedToken.IndexOf('=');
            if (eq <= 0 || eq == trimmedToken.Length - 1)
                throw new NumberToStringConfigurationException("UNTS005", languageIdentifier, path,
                    $"Malformed forced-variant entry '{trimmedToken}' in \"{text}\"; expected \"dimension=value\".");
            string dimension = trimmedToken[..eq].Trim();
            string value = trimmedToken[(eq + 1)..].Trim();
            if (dimension.Length == 0 || value.Length == 0)
                throw new NumberToStringConfigurationException("UNTS005", languageIdentifier, path,
                    $"Malformed forced-variant entry '{trimmedToken}' in \"{text}\"; dimension and value must not be empty.");
            pairs.Add(new KeyValuePair<string, string>(dimension, value));
        }

        // VariantConstraintSet's constructor throws "UNTS004" for a repeated dimension.
        return new ForcedVariantSet(new VariantConstraintSet(pairs));
    }

    /// <summary>
    /// Creates a forced variant set from explicit dimension/value pairs (programmatic construction).
    /// </summary>
    /// <param name="values">
    /// The dimension/value pairs to force, enumerated once. A <see langword="null"/> or empty
    /// sequence returns <see cref="Empty"/>.
    /// </param>
    /// <exception cref="NumberToStringConfigurationException">
    /// A dimension or value is null/empty — error code <c>"UNTS005"</c> — or the same raw dimension
    /// key is supplied more than once — error code <c>"UNTS004"</c>.
    /// </exception>
    public static ForcedVariantSet Create(params IEnumerable<(string Dimension, string Value)> values)
    {
        if (values is null) return Empty;

        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var (dimension, value) in values)
        {
            if (string.IsNullOrEmpty(dimension))
                throw new NumberToStringConfigurationException("UNTS005", null, "ForcedVariants",
                    "A forced-variant dimension must not be null or empty.");
            if (string.IsNullOrEmpty(value))
                throw new NumberToStringConfigurationException("UNTS005", null, "ForcedVariants",
                    $"Forced-variant dimension '{dimension}' must have a non-empty value.");
            pairs.Add(new KeyValuePair<string, string>(dimension, value));
        }

        if (pairs.Count == 0) return Empty;

        // VariantConstraintSet's constructor throws "UNTS004" for a repeated raw dimension key.
        return new ForcedVariantSet(new VariantConstraintSet(pairs));
    }

    /// <summary>
    /// Returns a new query overlaying these forced values on top of <paramref name="baseQuery"/>.
    /// Dimensions not forced by this set are left untouched, so caller-supplied or default values
    /// for other dimensions survive. <paramref name="baseQuery"/> itself is never mutated. Returns
    /// <paramref name="baseQuery"/> unchanged (no allocation) when this set is empty.
    /// </summary>
    /// <param name="baseQuery">The already-resolved variant query (caller values plus language defaults) to overlay.</param>
    internal IReadOnlyDictionary<string, string> Overlay(IReadOnlyDictionary<string, string> baseQuery)
    {
        if (IsEmpty) return baseQuery;
        var merged = new Dictionary<string, string>(baseQuery, System.StringComparer.OrdinalIgnoreCase);
        foreach (var pair in Constraints.Values)
            merged[pair.Key] = pair.Value;
        return merged;
    }
}

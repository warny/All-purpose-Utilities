using System.Collections.Immutable;

namespace Utils.NumberToString;

/// <summary>Represents configuration failures detected while compiling number-writing rules.</summary>
public sealed class NumberToStringConfigurationException : Exception
{
    /// <summary>Initializes a configuration exception.</summary>
    public NumberToStringConfigurationException(string errorCode, string? languageIdentifier, string configurationPath, string message)
        : base($"{errorCode}: {message}")
    {
        ErrorCode = errorCode;
        LanguageIdentifier = languageIdentifier;
        ConfigurationPath = configurationPath;
    }

    /// <summary>Gets the stable diagnostic code.</summary>
    public string ErrorCode { get; }

    /// <summary>Gets the culture or language identifier, when known.</summary>
    public string? LanguageIdentifier { get; }

    /// <summary>Gets the configuration path associated with the failure.</summary>
    public string ConfigurationPath { get; }
}

/// <summary>Stores canonical, case-insensitive variant constraints in stable diagnostic order.</summary>
internal readonly record struct VariantConstraintSet
{
    /// <summary>Initializes a normalized constraint set.</summary>
    internal VariantConstraintSet(IEnumerable<KeyValuePair<string, string>> values)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            if (!builder.TryAdd(key, value))
                throw new NumberToStringConfigurationException("UNTS004", null, "Constraints",
                    $"Duplicate constraint dimension '{key}'. A rule may constrain a canonical dimension only once.");
        }
        Values = builder.ToImmutable();
    }

    /// <summary>Gets canonical dimension/value pairs.</summary>
    internal ImmutableSortedDictionary<string, string> Values { get; }

    /// <summary>Gets the number of canonical constrained dimensions.</summary>
    internal int Specificity => Values.Count;

    /// <summary>Returns a stable representation for diagnostics.</summary>
    public override string ToString() => $"{{ {string.Join(", ", Values.Select(pair => $"{pair.Key}={pair.Value}"))} }}";
}

/// <summary>Represents the precedence rank of a variant candidate.</summary>
internal readonly record struct VariantRuleRank(int Specificity, int Priority);

/// <summary>Provides shared matching, ranking, and ambiguity operations for variant candidates.</summary>
internal static class VariantRulePrecedence
{
    /// <summary>Compares ranks by specificity first and explicit priority second.</summary>
    internal static int CompareRank(VariantRuleRank left, VariantRuleRank right)
    {
        int specificity = left.Specificity.CompareTo(right.Specificity);
        return specificity != 0 ? specificity : left.Priority.CompareTo(right.Priority);
    }

    /// <summary>Returns whether two sets can be satisfied by the same query.</summary>
    internal static bool CanMatchTogether(VariantConstraintSet left, VariantConstraintSet right) =>
        !left.Values.Any(pair => right.Values.TryGetValue(pair.Key, out string? value)
            && !string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns whether every constraint is satisfied by a query.</summary>
    internal static bool Matches(VariantConstraintSet constraints, IReadOnlyDictionary<string, string> query) =>
        constraints.Values.All(pair => query.TryGetValue(pair.Key, out string? value)
            && string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>Selects the unique highest-ranked matching candidate.</summary>
    internal static T? SelectBestUnique<T>(
        IEnumerable<T> candidates,
        Func<T, VariantConstraintSet> constraints,
        Func<T, int> priority,
        IReadOnlyDictionary<string, string> query,
        string context)
        where T : class
    {
        T? best = null;
        VariantRuleRank bestRank = new(-1, int.MinValue);
        bool ambiguous = false;
        foreach (T candidate in candidates)
        {
            VariantConstraintSet set = constraints(candidate);
            if (!Matches(set, query)) continue;
            var rank = new VariantRuleRank(set.Specificity, priority(candidate));
            int comparison = CompareRank(rank, bestRank);
            if (comparison > 0)
            {
                best = candidate;
                bestRank = rank;
                ambiguous = false;
            }
            else if (comparison == 0)
            {
                ambiguous = true;
            }
        }
        if (ambiguous)
            throw new NumberToStringConfigurationException("UNTS000", null, context,
                $"Multiple matching candidates remain at specificity {bestRank.Specificity} and priority {bestRank.Priority}.");
        return best;
    }
}

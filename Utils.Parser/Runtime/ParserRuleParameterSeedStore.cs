using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Utils.Parser.Runtime;

/// <summary>
/// Stores pending child-rule parameter seeds intended for the next invocation of named parser rules.
/// Seeds are copied into matching child frames when those rules are entered.
/// This type is immutable after construction; mutations produce a new instance.
/// Implements <see cref="ICloneable"/> so the execution-context copier can deep-copy it during
/// parser backtracking snapshots, and <see cref="IParserExecutionStateHashable"/> so it contributes
/// to the managed execution-state key used for rollback-safe memoization.
/// </summary>
public sealed class ParserRuleParameterSeedStore : ICloneable, IParserExecutionStateHashable
{
    /// <summary>
    /// Maps rule names to their pending parameter snapshots.
    /// </summary>
    private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> _seeds;

    /// <summary>
    /// Initializes an empty seed store.
    /// </summary>
    public ParserRuleParameterSeedStore()
    {
        _seeds = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
    }

    private ParserRuleParameterSeedStore(Dictionary<string, IReadOnlyDictionary<string, object?>> seeds)
    {
        _seeds = seeds;
    }

    /// <summary>
    /// Gets whether the store has any pending seeds.
    /// </summary>
    public bool IsEmpty => _seeds.Count == 0;

    /// <summary>
    /// Returns a new store with the specified parameter seed added or updated for the given rule.
    /// </summary>
    internal ParserRuleParameterSeedStore With(string ruleName, string parameterName, object? value)
    {
        var copy = CloneSeeds();
        if (!copy.TryGetValue(ruleName, out var existing))
        {
            existing = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(StringComparer.Ordinal));
            copy[ruleName] = existing;
        }

        var inner = new Dictionary<string, object?>(existing, StringComparer.Ordinal)
        {
            [parameterName] = value
        };
        copy[ruleName] = new ReadOnlyDictionary<string, object?>(inner);
        return new ParserRuleParameterSeedStore(copy);
    }

    /// <summary>
    /// Returns a new store with all pending seeds for <paramref name="ruleName"/> removed.
    /// </summary>
    internal ParserRuleParameterSeedStore Without(string ruleName)
    {
        if (!_seeds.ContainsKey(ruleName))
        {
            return this;
        }

        var copy = CloneSeeds();
        copy.Remove(ruleName);
        return new ParserRuleParameterSeedStore(copy);
    }

    /// <summary>
    /// Attempts to get the pending parameter snapshot for the specified rule name.
    /// </summary>
    internal bool TryGet(string ruleName, out IReadOnlyDictionary<string, object?> parameters)
        => _seeds.TryGetValue(ruleName, out parameters!);

    /// <summary>
    /// Returns a deep copy of this store.
    /// </summary>
    public object Clone()
    {
        return new ParserRuleParameterSeedStore(CloneSeeds());
    }

    /// <summary>
    /// Computes a deterministic hash of the current seed state for managed execution-state key participation.
    /// </summary>
    public ulong GetParserExecutionStateHash()
    {
        ulong hash = 14695981039346656037UL;
        foreach (var (ruleName, parameters) in _seeds)
        {
            foreach (char c in ruleName)
            {
                hash = (hash ^ (ulong)c) * 1099511628211UL;
            }

            hash = (hash ^ (ulong)(uint)parameters.Count) * 1099511628211UL;
            foreach (var kvp in parameters)
            {
                foreach (char c in kvp.Key)
                {
                    hash = (hash ^ (ulong)c) * 1099511628211UL;
                }

                if (kvp.Value is not null)
                {
                    hash = (hash ^ (ulong)(uint)kvp.Value.GetHashCode()) * 1099511628211UL;
                }
            }
        }

        return hash;
    }

    private Dictionary<string, IReadOnlyDictionary<string, object?>> CloneSeeds()
    {
        var copy = new Dictionary<string, IReadOnlyDictionary<string, object?>>(_seeds.Count, StringComparer.Ordinal);
        foreach (var (ruleName, parameters) in _seeds)
        {
            copy[ruleName] = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(parameters, StringComparer.Ordinal));
        }

        return copy;
    }
}

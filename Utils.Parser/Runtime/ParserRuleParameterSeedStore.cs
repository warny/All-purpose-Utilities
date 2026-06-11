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
        foreach ((string ruleName, IReadOnlyDictionary<string, object?> parameters) in
            _seeds.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            AddText(ref hash, ruleName);
            AddUInt64(ref hash, (ulong)parameters.Count);
            foreach (KeyValuePair<string, object?> parameter in
                parameters.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                AddText(ref hash, parameter.Key);
                AddSupportedValue(ref hash, parameter.Value);
            }
        }

        return hash;
    }

    /// <summary>
    /// Adds one supported deterministic seed value to the hash stream.
    /// </summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Seed value to hash.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a seed value is outside the deterministic scalar set supported by managed memoization.
    /// </exception>
    private static void AddSupportedValue(ref ulong hash, object? value)
    {
        if (value is null)
        {
            AddByte(ref hash, 0);
            return;
        }

        AddByte(ref hash, 1);
        switch (value)
        {
            case bool boolean:
                AddByte(ref hash, 1);
                AddByte(ref hash, boolean ? (byte)1 : (byte)0);
                return;
            case int integer:
                AddByte(ref hash, 2);
                AddUInt64(ref hash, unchecked((ulong)(long)integer));
                return;
            case long longInteger:
                AddByte(ref hash, 3);
                AddUInt64(ref hash, unchecked((ulong)longInteger));
                return;
            case double floatingPoint:
                AddByte(ref hash, 4);
                AddUInt64(ref hash, BitConverter.DoubleToUInt64Bits(floatingPoint));
                return;
            case string text:
                AddByte(ref hash, 5);
                AddText(ref hash, text);
                return;
            case char character:
                AddByte(ref hash, 6);
                AddUInt64(ref hash, character);
                return;
            default:
                throw new InvalidOperationException(
                    $"Pending parser rule parameter seed type '{value.GetType().FullName}' is not supported by deterministic memoization hashing.");
        }
    }

    /// <summary>
    /// Adds text to the deterministic hash stream.
    /// </summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="text">Text to add.</param>
    private static void AddText(ref ulong hash, string text)
    {
        AddUInt64(ref hash, (ulong)text.Length);
        foreach (char character in text)
        {
            AddUInt64(ref hash, character);
        }
    }

    /// <summary>
    /// Adds an unsigned integer to the deterministic hash stream in little-endian order.
    /// </summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Value to add.</param>
    private static void AddUInt64(ref ulong hash, ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            AddByte(ref hash, (byte)(value >> shift));
        }
    }

    /// <summary>
    /// Adds one byte to the deterministic FNV-1a hash stream.
    /// </summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Byte to add.</param>
    private static void AddByte(ref ulong hash, byte value)
    {
        hash = (hash ^ value) * 1099511628211UL;
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

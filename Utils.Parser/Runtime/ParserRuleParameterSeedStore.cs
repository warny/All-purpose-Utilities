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
    /// Monotonically increasing nonce used to prevent memoization reuse for non-hashable explicit seed values.
    /// </summary>
    private static long _volatileHashNonce;

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
    /// Computes a managed execution-state hash for the current seed state.
    /// Deterministically hashable values produce stable keys. Arbitrary explicit values remain accepted but
    /// contribute a fresh volatile nonce so completed-result memoization is conservatively bypassed.
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
                AddValue(ref hash, parameter.Value);
            }
        }

        return hash;
    }

    /// <summary>
    /// Adds one seed value to the hash stream or a volatile nonce when deterministic hashing is unavailable.
    /// </summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Seed value to hash.</param>
    private static void AddValue(ref ulong hash, object? value)
    {
        if (value is null)
        {
            AddByte(ref hash, 0);
            return;
        }

        AddByte(ref hash, 1);
        Type valueType = value.GetType();
        AddText(ref hash, valueType.AssemblyQualifiedName ?? valueType.FullName ?? valueType.Name);
        if (TryAddDeterministicValue(ref hash, value, valueType))
        {
            return;
        }

        AddByte(ref hash, byte.MaxValue);
        AddUInt64(ref hash, unchecked((ulong)Interlocked.Increment(ref _volatileHashNonce)));
    }

    /// <summary>
    /// Attempts to add a deterministic scalar or explicitly hashable value to the hash stream.
    /// </summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Non-null seed value.</param>
    /// <param name="valueType">Runtime seed value type.</param>
    /// <returns><c>true</c> when the value has a deterministic representation; otherwise, <c>false</c>.</returns>
    private static bool TryAddDeterministicValue(ref ulong hash, object value, Type valueType)
    {
        if (value is IParserExecutionStateHashable hashable)
        {
            AddUInt64(ref hash, hashable.GetParserExecutionStateHash());
            return true;
        }

        if (valueType.IsEnum)
        {
            Type underlyingType = Enum.GetUnderlyingType(valueType);
            object underlyingValue = Convert.ChangeType(value, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
            return TryAddDeterministicValue(ref hash, underlyingValue, underlyingType);
        }

        if (value is bool boolean)
        {
            AddByte(ref hash, boolean ? (byte)1 : (byte)0);
            return true;
        }

        if (value is char character)
        {
            AddUInt64(ref hash, character);
            return true;
        }

        if (value is sbyte signedByte)
        {
            AddUInt64(ref hash, unchecked((ulong)(long)signedByte));
            return true;
        }

        if (value is byte unsignedByte)
        {
            AddUInt64(ref hash, unsignedByte);
            return true;
        }

        if (value is short signedShort)
        {
            AddUInt64(ref hash, unchecked((ulong)(long)signedShort));
            return true;
        }

        if (value is ushort unsignedShort)
        {
            AddUInt64(ref hash, unsignedShort);
            return true;
        }

        if (value is int signedInteger)
        {
            AddUInt64(ref hash, unchecked((ulong)(long)signedInteger));
            return true;
        }

        if (value is uint unsignedInteger)
        {
            AddUInt64(ref hash, unsignedInteger);
            return true;
        }

        if (value is long signedLong)
        {
            AddUInt64(ref hash, unchecked((ulong)signedLong));
            return true;
        }

        if (value is ulong unsignedLong)
        {
            AddUInt64(ref hash, unsignedLong);
            return true;
        }

        if (value is float singlePrecision)
        {
            AddUInt64(ref hash, BitConverter.SingleToUInt32Bits(singlePrecision));
            return true;
        }

        if (value is double doublePrecision)
        {
            AddUInt64(ref hash, BitConverter.DoubleToUInt64Bits(doublePrecision));
            return true;
        }

        if (value is decimal decimalValue)
        {
            foreach (int part in decimal.GetBits(decimalValue))
            {
                AddUInt64(ref hash, unchecked((ulong)(long)part));
            }
            return true;
        }

        if (value is string text)
        {
            AddText(ref hash, text);
            return true;
        }

        if (value is DateTime dateTime)
        {
            AddUInt64(ref hash, unchecked((ulong)dateTime.ToBinary()));
            return true;
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            AddUInt64(ref hash, unchecked((ulong)dateTimeOffset.Ticks));
            AddUInt64(ref hash, unchecked((ulong)dateTimeOffset.Offset.Ticks));
            return true;
        }

        if (value is TimeSpan timeSpan)
        {
            AddUInt64(ref hash, unchecked((ulong)timeSpan.Ticks));
            return true;
        }

        if (value is Guid guid)
        {
            foreach (byte part in guid.ToByteArray())
            {
                AddByte(ref hash, part);
            }
            return true;
        }

        return false;
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

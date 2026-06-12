using System.Collections.ObjectModel;

namespace Utils.Parser.Runtime;

/// <summary>
/// Captures an immutable passive snapshot of a successfully completed child parser rule invocation.
/// Return names remain ordinal metadata; no type conversion, implicit variable, or ANTLR attribute syntax is provided.
/// </summary>
public sealed class ParserRuleCallResult : IParserExecutionStateHashable
{
    private static long _volatileHashNonce;
    private static readonly IReadOnlyDictionary<string, object?> EmptyReturnsValue =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(StringComparer.Ordinal));
    private IReadOnlyDictionary<string, object?> _returns = EmptyReturnsValue;

    /// <summary>Gets the name of the parser rule that produced this call result.</summary>
    public required string RuleName { get; init; }

    /// <summary>Gets the token-stream position at the time of rule entry.</summary>
    public required int InputPosition { get; init; }

    /// <summary>Gets the zero-based call-stack depth of the completed rule invocation.</summary>
    public required int Depth { get; init; }

    /// <summary>
    /// Gets an immutable snapshot of return values present after successful child execution and its <c>@after</c> hook.
    /// An absent key and a key whose value is <c>null</c> remain distinguishable.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Returns
    {
        get => _returns;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _returns = value.Count == 0
                ? EmptyReturnsValue
                : new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(value, StringComparer.Ordinal));
        }
    }

    /// <summary>Gets the passive rule metadata descriptor attached to the completed invocation, when available.</summary>
    public ParserRuleInvocationDescriptor? Descriptor { get; init; }

    /// <summary>Gets raw call-site argument text without its outer brackets, or <c>null</c>.</summary>
    public string? RawArguments { get; init; }

    /// <summary>Gets the current call-site label name, or <c>null</c> for an unlabeled reference.</summary>
    public string? LabelName { get; init; }

    /// <summary>Gets the current call-site label kind.</summary>
    public ParserRuleReferenceLabelKind LabelKind { get; init; }

    /// <summary>
    /// Attempts to retrieve a return value by ordinal metadata name without conversion.
    /// </summary>
    /// <param name="returnName">Return metadata name.</param>
    /// <param name="value">Receives the value, including <c>null</c> when the key is present-null.</param>
    /// <returns><c>true</c> when the return key is present; otherwise, <c>false</c>.</returns>
    public bool TryGetReturn(string returnName, out object? value)
    {
        ArgumentNullException.ThrowIfNull(returnName);
        return Returns.TryGetValue(returnName, out value);
    }

    /// <summary>
    /// Creates a copy annotated with current call-site raw arguments while preserving the immutable return snapshot.
    /// </summary>
    /// <param name="rawArguments">Current call-site raw argument text.</param>
    /// <returns>An annotated immutable result.</returns>
    internal ParserRuleCallResult WithRawArguments(string? rawArguments) => new()
    {
        RuleName = RuleName,
        InputPosition = InputPosition,
        Depth = Depth,
        Returns = Returns,
        Descriptor = Descriptor,
        RawArguments = rawArguments,
        LabelName = LabelName,
        LabelKind = LabelKind,
    };

    /// <summary>
    /// Creates a copy annotated with current call-site label metadata while preserving the immutable return snapshot.
    /// </summary>
    /// <param name="labelName">Current call-site label name.</param>
    /// <param name="labelKind">Current call-site label kind.</param>
    /// <returns>An annotated immutable result.</returns>
    internal ParserRuleCallResult WithLabel(string? labelName, ParserRuleReferenceLabelKind labelKind) => new()
    {
        RuleName = RuleName,
        InputPosition = InputPosition,
        Depth = Depth,
        Returns = Returns,
        Descriptor = Descriptor,
        RawArguments = RawArguments,
        LabelName = labelName,
        LabelKind = labelKind,
    };

    /// <summary>
    /// Computes a deterministic managed-state hash. Unsupported arbitrary return objects contribute a fresh nonce,
    /// conservatively preventing unsafe completed-result memoization instead of using unstable object identity hashes.
    /// </summary>
    /// <returns>The managed execution-state hash.</returns>
    public ulong GetParserExecutionStateHash()
    {
        ulong hash = 14695981039346656037UL;
        AddText(ref hash, RuleName);
        AddUInt64(ref hash, unchecked((ulong)(uint)InputPosition));
        AddUInt64(ref hash, unchecked((ulong)(uint)Depth));
        AddUInt64(ref hash, (ulong)Returns.Count);
        foreach (KeyValuePair<string, object?> item in Returns.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            AddText(ref hash, item.Key);
            AddValue(ref hash, item.Value);
        }

        AddValue(ref hash, RawArguments);
        AddUInt64(ref hash, (ulong)(int)LabelKind);
        AddValue(ref hash, LabelName);
        return hash;
    }

    /// <summary>Creates a call result by snapshotting return values from a completed invocation frame.</summary>
    /// <param name="frame">Completed child frame.</param>
    /// <returns>An immutable call-result snapshot.</returns>
    internal static ParserRuleCallResult FromFrame(ParserRuleInvocationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        IReadOnlyDictionary<string, object?> returns = frame.Returns.Count == 0
            ? EmptyReturnsValue
            : new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(frame.Returns, StringComparer.Ordinal));
        return new ParserRuleCallResult
        {
            RuleName = frame.RuleName,
            InputPosition = frame.InputPosition,
            Depth = frame.Depth,
            Returns = returns,
            Descriptor = frame.Descriptor,
        };
    }

    /// <summary>Adds a nullable supported value or conservative volatile marker to the hash stream.</summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Value to add.</param>
    private static void AddValue(ref ulong hash, object? value)
    {
        if (value is null)
        {
            AddByte(ref hash, 0);
            return;
        }

        AddByte(ref hash, 1);
        Type type = value.GetType();
        AddText(ref hash, type.AssemblyQualifiedName ?? type.FullName ?? type.Name);
        if (TryAddDeterministicValue(ref hash, value, type))
        {
            return;
        }

        AddByte(ref hash, byte.MaxValue);
        AddUInt64(ref hash, unchecked((ulong)Interlocked.Increment(ref _volatileHashNonce)));
    }

    /// <summary>Attempts to add a deterministically supported scalar or explicitly hashable value.</summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Non-null value.</param>
    /// <param name="type">Runtime value type.</param>
    /// <returns><c>true</c> when deterministic hashing succeeded.</returns>
    private static bool TryAddDeterministicValue(ref ulong hash, object value, Type type)
    {
        if (value is IParserExecutionStateHashable hashable) { AddUInt64(ref hash, hashable.GetParserExecutionStateHash()); return true; }
        if (type.IsEnum)
        {
            Type underlying = Enum.GetUnderlyingType(type);
            return TryAddDeterministicValue(ref hash, Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture), underlying);
        }
        if (value is bool boolean) { AddByte(ref hash, boolean ? (byte)1 : (byte)0); return true; }
        if (value is char character) { AddUInt64(ref hash, character); return true; }
        if (value is sbyte i8) { AddUInt64(ref hash, unchecked((ulong)(long)i8)); return true; }
        if (value is byte u8) { AddUInt64(ref hash, u8); return true; }
        if (value is short i16) { AddUInt64(ref hash, unchecked((ulong)(long)i16)); return true; }
        if (value is ushort u16) { AddUInt64(ref hash, u16); return true; }
        if (value is int i32) { AddUInt64(ref hash, unchecked((ulong)(long)i32)); return true; }
        if (value is uint u32) { AddUInt64(ref hash, u32); return true; }
        if (value is long i64) { AddUInt64(ref hash, unchecked((ulong)i64)); return true; }
        if (value is ulong u64) { AddUInt64(ref hash, u64); return true; }
        if (value is float f32) { AddUInt64(ref hash, BitConverter.SingleToUInt32Bits(f32)); return true; }
        if (value is double f64) { AddUInt64(ref hash, BitConverter.DoubleToUInt64Bits(f64)); return true; }
        if (value is decimal decimalValue) { foreach (int part in decimal.GetBits(decimalValue)) AddUInt64(ref hash, unchecked((ulong)(long)part)); return true; }
        if (value is string text) { AddText(ref hash, text); return true; }
        if (value is DateTime dateTime) { AddUInt64(ref hash, unchecked((ulong)dateTime.ToBinary())); return true; }
        if (value is DateTimeOffset offset) { AddUInt64(ref hash, unchecked((ulong)offset.Ticks)); AddUInt64(ref hash, unchecked((ulong)offset.Offset.Ticks)); return true; }
        if (value is TimeSpan span) { AddUInt64(ref hash, unchecked((ulong)span.Ticks)); return true; }
        if (value is Guid guid) { foreach (byte part in guid.ToByteArray()) AddByte(ref hash, part); return true; }
        return false;
    }

    /// <summary>Adds text to the hash stream.</summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="text">Text to add.</param>
    private static void AddText(ref ulong hash, string text)
    {
        AddUInt64(ref hash, (ulong)text.Length);
        foreach (char character in text) AddUInt64(ref hash, character);
    }

    /// <summary>Adds an unsigned integer to the hash stream.</summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Value to add.</param>
    private static void AddUInt64(ref ulong hash, ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8) AddByte(ref hash, (byte)(value >> shift));
    }

    /// <summary>Adds one byte to the hash stream.</summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Byte to add.</param>
    private static void AddByte(ref ulong hash, byte value) => hash = (hash ^ value) * 1099511628211UL;
}

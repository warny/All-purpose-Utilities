namespace Utils.Parser.Runtime;

/// <summary>
/// Represents passive per-invocation parser rule metadata state.
/// Parameters, locals, returns, and labeled completed child calls remain untyped managed metadata;
/// the frame does not bind ANTLR type syntax or expose implicit grammar variables.
/// </summary>
public sealed class ParserRuleInvocationFrame
{
    /// <summary>
    /// Parameter values keyed by metadata name.
    /// </summary>
    private readonly Dictionary<string, object?> _parameters;

    /// <summary>
    /// Local values keyed by metadata name.
    /// </summary>
    private readonly Dictionary<string, object?> _locals = new(StringComparer.Ordinal);

    /// <summary>
    /// Return values keyed by metadata name.
    /// </summary>
    private readonly Dictionary<string, object?> _returns = new(StringComparer.Ordinal);

    /// <summary>
    /// Passive rule-level metadata descriptor associated with this invocation, when available.
    /// </summary>
    private readonly ParserRuleInvocationDescriptor? _descriptor;

    /// <summary>
    /// Initializes an empty parser rule invocation frame.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule being invoked.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    public ParserRuleInvocationFrame(string ruleName, int inputPosition)
        : this(ruleName, inputPosition, new Dictionary<string, object?>(), null)
    {
    }

    /// <summary>
    /// Initializes a parser rule invocation frame with passive parameter values.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule being invoked.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    /// <param name="parameters">Passive parameter values to expose through the frame.</param>
    public ParserRuleInvocationFrame(string ruleName, int inputPosition, IReadOnlyDictionary<string, object?> parameters)
        : this(ruleName, inputPosition, parameters, null)
    {
    }

    /// <summary>
    /// Initializes a parser rule invocation frame with passive parameter values and rule metadata.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule being invoked.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    /// <param name="parameters">Passive parameter values to expose through the frame.</param>
    /// <param name="descriptor">Passive rule metadata descriptor to expose through the frame.</param>
    public ParserRuleInvocationFrame(
        string ruleName,
        int inputPosition,
        IReadOnlyDictionary<string, object?> parameters,
        ParserRuleInvocationDescriptor? descriptor)
        : this(ruleName, inputPosition, parameters, descriptor, parent: null)
    {
    }

    /// <summary>
    /// Initializes a parser rule invocation frame with passive parameter values, rule metadata, and a parent frame.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule being invoked.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    /// <param name="parameters">Passive parameter values to expose through the frame.</param>
    /// <param name="descriptor">Passive rule metadata descriptor to expose through the frame.</param>
    /// <param name="parent">Parent invocation frame in the current call stack, or <c>null</c> for root-level rules.</param>
    public ParserRuleInvocationFrame(
        string ruleName,
        int inputPosition,
        IReadOnlyDictionary<string, object?> parameters,
        ParserRuleInvocationDescriptor? descriptor,
        ParserRuleInvocationFrame? parent)
    {
        RuleName = ruleName ?? throw new ArgumentNullException(nameof(ruleName));
        InputPosition = inputPosition;
        _parameters = parameters is null
            ? throw new ArgumentNullException(nameof(parameters))
            : new Dictionary<string, object?>(parameters, StringComparer.Ordinal);
        _descriptor = descriptor;
        Parent = parent;
        Depth = parent is null ? 0 : parent.Depth + 1;
    }

    /// <summary>
    /// Gets the name of the parser rule associated with this invocation frame.
    /// </summary>
    public string RuleName { get; }

    /// <summary>
    /// Gets the token-stream position at the time of rule entry.
    /// </summary>
    public int InputPosition { get; }

    /// <summary>
    /// Gets passive parameter values associated with this invocation frame.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters => _parameters;

    /// <summary>
    /// Gets passive local values associated with this invocation frame.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Locals => _locals;

    /// <summary>
    /// Gets passive return values associated with this invocation frame.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Returns => _returns;

    /// <summary>
    /// Gets passive rule-level metadata associated with this invocation frame, when supplied by the runtime.
    /// </summary>
    public ParserRuleInvocationDescriptor? Descriptor => _descriptor;

    /// <summary>
    /// Gets the parent invocation frame in the current parser rule call stack, or <c>null</c> when this is a root-level rule.
    /// </summary>
    public ParserRuleInvocationFrame? Parent { get; }

    /// <summary>
    /// Gets the zero-based call-stack depth of this invocation frame.
    /// Root-level rule frames have depth 0; each nested rule invocation increments the depth by 1.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Gets or sets the store of pending child-rule parameter seeds on this frame.
    /// Seeds are consumed by <see cref="StackParserRuleInvocationFrameManager"/> when the matching child rule is entered.
    /// This value is synced to the managed execution-state snapshot before each capture so that backtracking
    /// restores the seed state correctly and seeds do not leak across failed parser alternatives.
    /// </summary>
    internal ParserRuleParameterSeedStore? PendingChildSeeds { get; set; }

    /// <summary>
    /// Gets the call result from the most recently completed successful direct child rule invocation,
    /// or <c>null</c> when no child rule has yet completed successfully in the current rule context.
    /// This value is set by the invocation-frame manager on successful child exit and is synchronized
    /// with the managed execution-state snapshot during parser backtracking restoration.
    /// It must not be stored in static or global fields; its rollback safety relies on the
    /// managed execution-state snapshot mechanism being active.
    /// </summary>
    public ParserRuleCallResult? LastCompletedChildCall { get; internal set; }

    /// <summary>
    /// Gets the immutable assignment and list label results produced by successful direct child calls in this frame.
    /// Store replacement is synchronized with managed execution-state snapshots so failed parser attempts roll back changes.
    /// </summary>
    public ParserLabeledRuleCallResultStore LabeledCallResults { get; internal set; } = ParserLabeledRuleCallResultStore.Empty;

    /// <summary>
    /// Gets a parameter value by name.
    /// </summary>
    /// <param name="name">Parameter metadata name.</param>
    /// <returns>The parameter value associated with <paramref name="name"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the parameter name is not present.</exception>
    public object? GetParameter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _parameters[name];
    }

    /// <summary>
    /// Attempts to get a parameter value by name.
    /// </summary>
    /// <param name="name">Parameter metadata name.</param>
    /// <param name="value">Receives the parameter value when present.</param>
    /// <returns><c>true</c> when the parameter name is present; otherwise, <c>false</c>.</returns>
    public bool TryGetParameter(string name, out object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _parameters.TryGetValue(name, out value);
    }

    /// <summary>
    /// Sets a passive local value by metadata name.
    /// </summary>
    /// <param name="name">Local metadata name.</param>
    /// <param name="value">Local value to store.</param>
    public void SetLocal(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        _locals[name] = value;
    }

    /// <summary>
    /// Attempts to get a passive local value by metadata name.
    /// </summary>
    /// <param name="name">Local metadata name.</param>
    /// <param name="value">Receives the local value when present.</param>
    /// <returns><c>true</c> when the local name is present; otherwise, <c>false</c>.</returns>
    public bool TryGetLocal(string name, out object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _locals.TryGetValue(name, out value);
    }

    /// <summary>
    /// Sets a passive return value by metadata name.
    /// </summary>
    /// <param name="name">Return metadata name.</param>
    /// <param name="value">Return value to store.</param>
    public void SetReturnValue(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        _returns[name] = value;
    }

    /// <summary>
    /// Replaces passive return values from a managed execution-state snapshot.
    /// </summary>
    /// <param name="returns">Return values to restore on this invocation frame.</param>
    internal void ReplaceReturnValues(IReadOnlyDictionary<string, object?>? returns)
    {
        _returns.Clear();
        if (returns is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object?> item in returns)
        {
            _returns[item.Key] = item.Value;
        }
    }

    /// <summary>
    /// Attempts to get a passive return value by metadata name.
    /// </summary>
    /// <param name="name">Return metadata name.</param>
    /// <param name="value">Receives the return value when present.</param>
    /// <returns><c>true</c> when the return name is present; otherwise, <c>false</c>.</returns>
    public bool TryGetReturnValue(string name, out object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _returns.TryGetValue(name, out value);
    }

    /// <summary>
    /// Seeds an untyped parameter value for the next invocation of <paramref name="ruleName"/> as a direct child of this frame.
    /// Seeds are consumed and copied into the matching child frame when that rule is entered through the stack frame manager.
    /// Parameters are not auto-bound; rule call arguments are not evaluated.
    /// </summary>
    /// <param name="ruleName">Name of the child rule that will receive the seed.</param>
    /// <param name="parameterName">Parameter metadata name.</param>
    /// <param name="value">Untyped parameter value to seed.</param>
    public void SetPendingChildParameter(string ruleName, string parameterName, object? value)
    {
        SetPendingChildParameters(ruleName, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [parameterName] = value
        });
    }

    /// <summary>
    /// Atomically merges untyped parameter values for the next invocation of a named child rule.
    /// </summary>
    /// <param name="ruleName">Name of the child rule that will receive the seeds.</param>
    /// <param name="values">Parameter metadata names and untyped values to seed.</param>
    public void SetPendingChildParameters(string ruleName, IReadOnlyDictionary<string, object?> values)
    {
        PendingChildSeeds = (PendingChildSeeds ?? new ParserRuleParameterSeedStore()).With(ruleName, values);
    }

    /// <summary>
    /// Attempts to consume and remove the pending parameter seed for <paramref name="ruleName"/>.
    /// On success, the seeds are removed from this frame and returned as a read-only snapshot.
    /// </summary>
    /// <param name="ruleName">Name of the child rule whose seeds to consume.</param>
    /// <param name="parameters">Receives the pending parameter snapshot when present.</param>
    /// <returns><c>true</c> when seeds for <paramref name="ruleName"/> were present; otherwise, <c>false</c>.</returns>
    public bool TryConsumePendingChildParameters(string ruleName, out IReadOnlyDictionary<string, object?> parameters)
    {
        if (PendingChildSeeds is null || !PendingChildSeeds.TryGet(ruleName, out parameters!))
        {
            parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
            return false;
        }

        PendingChildSeeds = PendingChildSeeds.Without(ruleName);
        if (PendingChildSeeds.IsEmpty)
        {
            PendingChildSeeds = null;
        }

        return true;
    }

    /// <summary>
    /// Removes all pending child-parameter seeds for <paramref name="ruleName"/>, or all seeds when <paramref name="ruleName"/> is <c>null</c>.
    /// </summary>
    public void ClearPendingChildParameters(string? ruleName = null)
    {
        if (PendingChildSeeds is null)
        {
            return;
        }

        if (ruleName is null)
        {
            PendingChildSeeds = null;
        }
        else
        {
            PendingChildSeeds = PendingChildSeeds.Without(ruleName);
            if (PendingChildSeeds.IsEmpty)
            {
                PendingChildSeeds = null;
            }
        }
    }
}

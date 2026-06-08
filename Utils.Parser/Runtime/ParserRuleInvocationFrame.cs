namespace Utils.Parser.Runtime;

/// <summary>
/// Represents passive per-invocation parser rule metadata state.
/// This frame is preparatory infrastructure for future rule parameters, locals, and return values;
/// it does not bind ANTLR type syntax or execute rule metadata by itself.
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
}

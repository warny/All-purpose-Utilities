using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Carries passive metadata for an explicit parser rule-call execution policy invocation.
/// Raw arguments are split syntactically only. The parser does not evaluate or bind them automatically;
/// an explicitly installed policy may request an atomic managed seed batch through <see cref="TrySetParameterSeeds"/>.
/// </summary>
public sealed class ParserRuleCallExecutionContext
{
    private Func<IReadOnlyDictionary<string, object?>, bool>? _setParameterSeeds;

    /// <summary>
    /// Gets the caller invocation frame that was current when the rule call began, when frame tracking is enabled.
    /// Policies must not treat the mutable frame as rollback-managed external state or retain it beyond the callback.
    /// </summary>
    public required ParserRuleInvocationFrame? CallerFrame { get; init; }

    /// <summary>
    /// Gets the target parser rule name.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Gets the uninterpreted raw argument text from the current call site, without outer brackets.
    /// </summary>
    public string? RawArguments { get; init; }

    /// <summary>
    /// Gets the optional label name from the current call site.
    /// </summary>
    public string? LabelName { get; init; }

    /// <summary>
    /// Gets the label kind from the current call site.
    /// </summary>
    public ParserRuleReferenceLabelKind LabelKind { get; init; }

    /// <summary>
    /// Gets syntactically split positional raw arguments when an argument clause is present.
    /// Values remain uninterpreted source text.
    /// </summary>
    public IReadOnlyList<string>? PositionalRawArguments { get; init; }

    /// <summary>
    /// Gets syntactically split named raw arguments when every top-level argument has a supported name separator;
    /// otherwise, gets <c>null</c>. Values remain uninterpreted source text.
    /// </summary>
    public IReadOnlyDictionary<string, string>? NamedRawArguments { get; init; }

    /// <summary>
    /// Gets passive metadata for the target parser rule.
    /// </summary>
    public ParserRuleInvocationDescriptor? TargetRuleDescriptor { get; init; }

    /// <summary>
    /// Installs the internal rollback-managed batch seed writer used by <see cref="TrySetParameterSeeds"/>.
    /// This delegate is intentionally not exposed to policy implementations.
    /// </summary>
    internal Func<IReadOnlyDictionary<string, object?>, bool>? ParameterSeedWriter
    {
        init => _setParameterSeeds = value;
    }

    /// <summary>
    /// Attempts to seed one parameter for the current target rule through managed pending-child state.
    /// The caller cannot select a different target rule through this API.
    /// </summary>
    /// <param name="parameterName">Declared target parameter name.</param>
    /// <param name="value">Untyped value to seed, including <c>null</c>.</param>
    /// <returns><c>true</c> when managed seeding is available; otherwise, <c>false</c>.</returns>
    public bool TrySetParameterSeed(string parameterName, object? value)
    {
        ArgumentNullException.ThrowIfNull(parameterName);
        return TrySetParameterSeeds(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [parameterName] = value
        });
    }

    /// <summary>
    /// Attempts to atomically seed multiple parameters for the current target rule through managed pending-child state.
    /// The caller cannot select a different target rule through this API, and implementations must retain all values or none.
    /// </summary>
    /// <param name="values">Declared target parameter names and their untyped values, including <c>null</c>.</param>
    /// <returns><c>true</c> when every seed was retained atomically; otherwise, <c>false</c>.</returns>
    public bool TrySetParameterSeeds(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (_setParameterSeeds is null)
        {
            return false;
        }

        return _setParameterSeeds(values);
    }

    /// <summary>
    /// Gets the annotated completed child-call result after a successful call when invocation-frame tracking is enabled.
    /// </summary>
    public ParserRuleCallResult? CompletedCallResult { get; internal set; }

    /// <summary>
    /// Gets whether the target parser rule produced a parse node.
    /// </summary>
    public bool Succeeded { get; internal set; }
}

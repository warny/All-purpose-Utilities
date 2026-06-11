using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Carries passive metadata for an explicit parser rule-call execution policy invocation.
/// Raw arguments are split syntactically only; they are not evaluated, bound, or seeded automatically.
/// </summary>
public sealed class ParserRuleCallExecutionContext
{
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
    /// Gets the annotated completed child-call result after a successful call when invocation-frame tracking is enabled.
    /// </summary>
    public ParserRuleCallResult? CompletedCallResult { get; internal set; }

    /// <summary>
    /// Gets whether the target parser rule produced a parse node.
    /// </summary>
    public bool Succeeded { get; internal set; }
}

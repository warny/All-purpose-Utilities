namespace Utils.Parser.Runtime;

/// <summary>
/// Explicitly binds exact-arity positional simple literals to declared parser rule parameter names.
/// Declared parameter types are metadata only and are not validated by this policy.
/// </summary>
public sealed class PositionalLiteralRuleCallExecutionPolicy : IParserRuleCallExecutionPolicy
{
    private readonly ParserRuleCallBindingFailureBehavior _failureBehavior;

    /// <summary>
    /// Initializes a positional literal rule-call execution policy.
    /// </summary>
    /// <param name="failureBehavior">Behavior used when call metadata cannot be bound safely.</param>
    public PositionalLiteralRuleCallExecutionPolicy(
        ParserRuleCallBindingFailureBehavior failureBehavior = ParserRuleCallBindingFailureBehavior.IgnoreCall)
    {
        if (!Enum.IsDefined(failureBehavior))
        {
            throw new ArgumentOutOfRangeException(nameof(failureBehavior));
        }

        _failureBehavior = failureBehavior;
    }

    /// <summary>
    /// Validates the complete positional call and then writes rollback-managed child parameter seeds atomically.
    /// Existing seeds for the same target parameter are overwritten; unrelated seeds remain unchanged.
    /// </summary>
    /// <param name="context">Metadata and managed seed access for the current parser rule call.</param>
    public void BeforeRuleCall(ParserRuleCallExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.PositionalRawArguments is null)
        {
            return;
        }

        IReadOnlyList<ParserRuleParameterDescriptor>? parameters = context.TargetRuleDescriptor?.Parameters;
        if (parameters is null)
        {
            Fail(context, "The target rule descriptor is unavailable.");
            return;
        }

        if (context.PositionalRawArguments.Count != parameters.Count)
        {
            Fail(context, $"Expected {parameters.Count} argument(s), but received {context.PositionalRawArguments.Count}.");
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var bindings = new (string Name, object? Value)[parameters.Count];
        for (int index = 0; index < parameters.Count; index++)
        {
            string name = parameters[index].Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                Fail(context, "A target parameter does not expose a usable name.", index);
                return;
            }

            if (!names.Add(name))
            {
                Fail(context, $"Target parameter name '{name}' is duplicated.", index);
                return;
            }

            string rawArgument = context.PositionalRawArguments[index];
            if (!ParserSimpleLiteralParser.TryParse(rawArgument, out object? value))
            {
                Fail(context, "The argument is not a supported simple literal.", index);
                return;
            }

            bindings[index] = (name, value);
        }

        foreach ((string name, object? value) in bindings)
        {
            if (!context.TrySetParameterSeed(name, value))
            {
                Fail(context, "Managed parameter seeding is unavailable for the current parser invocation.");
                return;
            }
        }
    }

    /// <summary>
    /// Performs no post-call binding or result transformation.
    /// </summary>
    /// <param name="context">Metadata for the completed parser rule call.</param>
    public void AfterRuleCall(ParserRuleCallExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// Applies the configured conservative failure behavior.
    /// </summary>
    /// <param name="context">Current parser rule-call context.</param>
    /// <param name="reason">Deterministic validation failure reason.</param>
    /// <param name="argumentIndex">Optional offending argument index.</param>
    private void Fail(ParserRuleCallExecutionContext context, string reason, int? argumentIndex = null)
    {
        if (_failureBehavior == ParserRuleCallBindingFailureBehavior.Throw)
        {
            throw new ParserRuleCallBindingException(context.RuleName, context.RawArguments, reason, argumentIndex);
        }
    }
}

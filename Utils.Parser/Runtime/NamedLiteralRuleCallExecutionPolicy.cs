namespace Utils.Parser.Runtime;

/// <summary>
/// Explicitly binds named simple literals to declared parser rule parameter names by exact ordinal name.
/// Declared parameter types are metadata only and are not validated by this policy.
/// </summary>
public sealed class NamedLiteralRuleCallExecutionPolicy : IParserRuleCallExecutionPolicy
{
    private readonly ParserRuleCallBindingFailureBehavior _failureBehavior;

    /// <summary>
    /// Initializes a named literal rule-call execution policy.
    /// </summary>
    /// <param name="failureBehavior">Behavior used when call metadata cannot be bound safely.</param>
    public NamedLiteralRuleCallExecutionPolicy(
        ParserRuleCallBindingFailureBehavior failureBehavior = ParserRuleCallBindingFailureBehavior.IgnoreCall)
    {
        if (!Enum.IsDefined(failureBehavior))
        {
            throw new ArgumentOutOfRangeException(nameof(failureBehavior));
        }

        _failureBehavior = failureBehavior;
    }

    /// <summary>
    /// Validates exact named-parameter coverage and every literal before writing one rollback-managed seed batch.
    /// Existing seeds for matching target parameters are overwritten; unrelated seeds remain unchanged.
    /// </summary>
    /// <param name="context">Metadata and managed seed access for the current parser rule call.</param>
    public void BeforeRuleCall(ParserRuleCallExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyDictionary<string, string>? arguments = context.NamedRawArguments;
        if (arguments is null)
        {
            if (context.RawArguments is not null)
            {
                Fail(context, "The raw arguments are not a complete named argument list.");
            }

            return;
        }

        IReadOnlyList<ParserRuleParameterDescriptor>? parameters = context.TargetRuleDescriptor?.Parameters;
        if (parameters is null)
        {
            Fail(context, "The target rule descriptor is unavailable.");
            return;
        }

        var parameterNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (ParserRuleParameterDescriptor parameter in parameters)
        {
            string name = parameter.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                Fail(context, "A target parameter does not expose a usable name.");
                return;
            }

            if (!parameterNames.Add(name))
            {
                Fail(context, $"Target parameter name '{name}' is duplicated.");
                return;
            }
        }

        if (arguments.Count != parameterNames.Count)
        {
            Fail(context, $"Expected exactly {parameterNames.Count} named argument(s), but received {arguments.Count}.");
            return;
        }

        foreach (string argumentName in arguments.Keys)
        {
            if (!parameterNames.Contains(argumentName))
            {
                Fail(context, $"Named argument '{argumentName}' does not match a declared target parameter.");
                return;
            }
        }

        var seeds = new Dictionary<string, object?>(parameterNames.Count, StringComparer.Ordinal);
        foreach (ParserRuleParameterDescriptor parameter in parameters)
        {
            string rawArgument = arguments[parameter.Name];
            if (!ParserSimpleLiteralParser.TryParse(rawArgument, out object? value))
            {
                Fail(context, $"Named argument '{parameter.Name}' is not a supported simple literal.");
                return;
            }

            seeds.Add(parameter.Name, value);
        }

        if (!context.TrySetParameterSeeds(seeds))
        {
            Fail(context, "Managed parameter seeding is unavailable for the current parser invocation.");
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
    private void Fail(ParserRuleCallExecutionContext context, string reason)
    {
        if (_failureBehavior == ParserRuleCallBindingFailureBehavior.Throw)
        {
            throw new ParserRuleCallBindingException(context.RuleName, context.RawArguments, reason);
        }
    }
}

namespace Utils.Parser.Runtime;

/// <summary>
/// Explicitly binds named simple literals and omitted literal defaults after validating and safely converting every value against the target
/// rule's allowlisted declared parameter types.
/// </summary>
/// <remarks>
/// This policy intentionally changes binding behavior only when a caller installs it explicitly: omitted named
/// parameters may use declared simple-literal defaults. The conservative default policy and the untyped literal policies
/// do not consume defaults and retain their existing behavior.
/// </remarks>
public sealed class TypedNamedLiteralRuleCallExecutionPolicy : IParserRuleCallExecutionPolicy
{
    private readonly ParserRuleCallBindingFailureBehavior _failureBehavior;

    /// <summary>
    /// Initializes an explicitly opt-in typed named literal rule-call execution policy.
    /// </summary>
    /// <param name="failureBehavior">Behavior used when syntax, metadata, or conversion cannot be bound safely.</param>
    public TypedNamedLiteralRuleCallExecutionPolicy(
        ParserRuleCallBindingFailureBehavior failureBehavior = ParserRuleCallBindingFailureBehavior.IgnoreCall)
    {
        if (!Enum.IsDefined(failureBehavior))
        {
            throw new ArgumentOutOfRangeException(nameof(failureBehavior));
        }

        _failureBehavior = failureBehavior;
    }

    /// <summary>
    /// Validates named syntax, names, declared types, explicit literals, required defaults, and conversions before one atomic seed batch.
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

        var descriptors = new Dictionary<string, ParserRuleParameterDescriptor>(parameters.Count, StringComparer.Ordinal);
        foreach (ParserRuleParameterDescriptor parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                Fail(context, "A target parameter does not expose a usable name.");
                return;
            }

            if (!descriptors.TryAdd(parameter.Name, parameter))
            {
                Fail(context, $"Target parameter name '{parameter.Name}' is duplicated.", parameter);
                return;
            }

            if (parameter.RawType is null)
            {
                Fail(context, "The declared parameter type is unavailable or does not use the supported 'type name' shape.", parameter);
                return;
            }
        }

        foreach (string argumentName in arguments.Keys)
        {
            if (!descriptors.ContainsKey(argumentName))
            {
                Fail(context, $"Named argument '{argumentName}' does not match a declared target parameter.");
                return;
            }
        }

        var seeds = new Dictionary<string, object?>(descriptors.Count, StringComparer.Ordinal);
        foreach (ParserRuleParameterDescriptor parameter in parameters)
        {
            bool hasExplicitArgument = arguments.TryGetValue(parameter.Name, out string? rawValue);
            rawValue ??= parameter.RawDefaultValue;
            if (rawValue is null)
            {
                Fail(context, $"Required parameter '{parameter.Name}' has no explicit argument or default value.", parameter);
                return;
            }

            if (!ParserSimpleLiteralParser.TryParse(rawValue, out object? literalValue))
            {
                string reason = hasExplicitArgument
                    ? $"Named argument '{parameter.Name}' is not a supported simple literal."
                    : $"Default value for parameter '{parameter.Name}' is not a supported simple literal.";
                Fail(context, reason, parameter);
                return;
            }

            ParserLiteralConversionResult conversion = ParserLiteralTypeConverter.Convert(literalValue, parameter.RawType!);
            if (!conversion.Success)
            {
                string reason = hasExplicitArgument
                    ? conversion.Error!
                    : $"Default value for parameter '{parameter.Name}' cannot bind to declared type '{parameter.RawType}'.";
                Fail(context, reason, parameter);
                return;
            }

            seeds.Add(parameter.Name, conversion.Value);
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
    /// <param name="parameter">Optional offending parameter descriptor.</param>
    private void Fail(
        ParserRuleCallExecutionContext context,
        string reason,
        ParserRuleParameterDescriptor? parameter = null)
    {
        if (_failureBehavior == ParserRuleCallBindingFailureBehavior.Throw)
        {
            throw new ParserRuleCallBindingException(
                context.RuleName,
                context.RawArguments,
                reason,
                parameterName: parameter?.Name,
                declaredType: parameter?.RawType);
        }
    }
}

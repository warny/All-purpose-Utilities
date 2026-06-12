namespace Utils.Parser.Runtime;

/// <summary>
/// Explicitly binds positional simple literals and omitted trailing literal defaults after validating and safely converting every value
/// against the target rule's allowlisted declared parameter types.
/// </summary>
/// <remarks>
/// This policy intentionally changes binding behavior only when a caller installs it explicitly: omitted trailing positional
/// parameters may use declared simple-literal defaults. The conservative default policy and the untyped literal policies
/// do not consume defaults and retain their existing behavior.
/// </remarks>
public sealed class TypedPositionalLiteralRuleCallExecutionPolicy : IParserRuleCallExecutionPolicy
{
    private readonly ParserRuleCallBindingFailureBehavior _failureBehavior;

    /// <summary>
    /// Initializes an explicitly opt-in typed positional literal rule-call execution policy.
    /// </summary>
    /// <param name="failureBehavior">Behavior used when syntax, metadata, or conversion cannot be bound safely.</param>
    public TypedPositionalLiteralRuleCallExecutionPolicy(
        ParserRuleCallBindingFailureBehavior failureBehavior = ParserRuleCallBindingFailureBehavior.IgnoreCall)
    {
        if (!Enum.IsDefined(failureBehavior))
        {
            throw new ArgumentOutOfRangeException(nameof(failureBehavior));
        }

        _failureBehavior = failureBehavior;
    }

    /// <summary>
    /// Validates syntax, trailing omission, names, declared types, literals, defaults, and conversions before submitting one atomic seed batch.
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

        if (context.PositionalRawArguments.Count > parameters.Count)
        {
            Fail(context, $"Expected at most {parameters.Count} argument(s), but received {context.PositionalRawArguments.Count}.");
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var seeds = new Dictionary<string, object?>(parameters.Count, StringComparer.Ordinal);
        for (int index = 0; index < parameters.Count; index++)
        {
            ParserRuleParameterDescriptor parameter = parameters[index];
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                Fail(context, "A target parameter does not expose a usable name.", index);
                return;
            }

            if (!names.Add(parameter.Name))
            {
                Fail(context, $"Target parameter name '{parameter.Name}' is duplicated.", index, parameter);
                return;
            }

            if (parameter.RawType is null)
            {
                Fail(context, "The declared parameter type is unavailable or does not use the supported 'type name' shape.", index, parameter);
                return;
            }

            bool hasExplicitArgument = index < context.PositionalRawArguments.Count;
            string? rawValue = hasExplicitArgument
                ? context.PositionalRawArguments[index]
                : parameter.RawDefaultValue;
            if (rawValue is null)
            {
                Fail(context, $"Required parameter '{parameter.Name}' has no explicit argument or default value.", parameter: parameter);
                return;
            }

            if (!ParserSimpleLiteralParser.TryParse(rawValue, out object? literalValue))
            {
                string reason = hasExplicitArgument
                    ? "The argument is not a supported simple literal."
                    : $"Default value for parameter '{parameter.Name}' is not a supported simple literal.";
                Fail(context, reason, hasExplicitArgument ? index : null, parameter);
                return;
            }

            ParserLiteralConversionResult conversion = ParserLiteralTypeConverter.Convert(literalValue, parameter.RawType);
            if (!conversion.Success)
            {
                string reason = hasExplicitArgument
                    ? conversion.Error!
                    : $"Default value for parameter '{parameter.Name}' cannot bind to declared type '{parameter.RawType}'.";
                Fail(context, reason, hasExplicitArgument ? index : null, parameter);
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
    /// <param name="argumentIndex">Optional offending argument index.</param>
    /// <param name="parameter">Optional offending parameter descriptor.</param>
    private void Fail(
        ParserRuleCallExecutionContext context,
        string reason,
        int? argumentIndex = null,
        ParserRuleParameterDescriptor? parameter = null)
    {
        if (_failureBehavior == ParserRuleCallBindingFailureBehavior.Throw)
        {
            throw new ParserRuleCallBindingException(
                context.RuleName,
                context.RawArguments,
                reason,
                argumentIndex,
                parameter?.Name,
                parameter?.RawType);
        }
    }
}

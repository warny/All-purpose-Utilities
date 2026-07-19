namespace Utils.Parser.Generators.Internal;

/// <summary>Describes one deterministic generated rule-call argument binding validation issue.</summary>
internal sealed class GeneratedRuleArgumentBindingIssue
{
    /// <summary>Gets or initializes the target rule name.</summary>
    public string TargetRuleName { get; init; } = string.Empty;
    /// <summary>Gets or initializes the stable diagnostic reason.</summary>
    public string Reason { get; init; } = string.Empty;
    /// <summary>Gets or initializes the source call site.</summary>
    public G4RuleRef CallSite { get; init; } = null!;
    /// <summary>Gets or initializes the optional zero-based argument index.</summary>
    public int? ArgumentIndex { get; init; }
    /// <summary>Gets or initializes the optional target parameter name.</summary>
    public string? ParameterName { get; init; }
    /// <summary>Gets or initializes the optional declared target type.</summary>
    public string? DeclaredType { get; init; }
}

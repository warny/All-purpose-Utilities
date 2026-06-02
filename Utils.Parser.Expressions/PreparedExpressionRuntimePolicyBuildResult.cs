using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Contains the complete result of building an opt-in prepared expression parser runtime policy.
/// </summary>
public sealed record PreparedExpressionRuntimePolicyBuildResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreparedExpressionRuntimePolicyBuildResult"/> record.
    /// </summary>
    /// <param name="policy">Runtime feature policy configured with prepared expression adapters.</param>
    /// <param name="registry">Registry used by the configured prepared expression adapters.</param>
    /// <param name="registryBuildResult">Registry build result containing all audit metadata and failures.</param>
    public PreparedExpressionRuntimePolicyBuildResult(
        ParserRuntimeFeaturePolicy policy,
        PreparedExpressionEmbeddedCodeRegistry registry,
        PreparedExpressionEmbeddedCodeRegistryBuildResult registryBuildResult)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        RegistryBuildResult = registryBuildResult ?? throw new ArgumentNullException(nameof(registryBuildResult));
    }

    /// <summary>
    /// Gets the runtime feature policy configured with prepared expression semantic predicate and parser action adapters.
    /// </summary>
    public ParserRuntimeFeaturePolicy Policy { get; }

    /// <summary>
    /// Gets the registry used by the configured prepared expression adapters.
    /// </summary>
    public PreparedExpressionEmbeddedCodeRegistry Registry { get; }

    /// <summary>
    /// Gets the registry build result containing successes, failures, duplicates, skips, and all audit entries.
    /// </summary>
    public PreparedExpressionEmbeddedCodeRegistryBuildResult RegistryBuildResult { get; }

    /// <summary>
    /// Gets a value indicating whether registry preparation recorded failures or duplicate keys.
    /// </summary>
    public bool HasFailures => RegistryBuildResult.HasFailures;
}

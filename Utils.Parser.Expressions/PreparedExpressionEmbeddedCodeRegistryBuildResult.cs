namespace Utils.Parser.Expressions;

/// <summary>
/// Contains the registry and audit entries produced by an explicit prepared expression registry build.
/// </summary>
public sealed record PreparedExpressionEmbeddedCodeRegistryBuildResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreparedExpressionEmbeddedCodeRegistryBuildResult"/> record.
    /// </summary>
    /// <param name="registry">Registry populated with successfully prepared, non-duplicate artifacts.</param>
    /// <param name="successfulSemanticPredicates">Successful semantic predicate entries added to the registry.</param>
    /// <param name="successfulParserActions">Successful parser action entries added to the registry.</param>
    /// <param name="nonSuccessEntries">Entries returned by the preparer with non-success statuses.</param>
    /// <param name="duplicateEntries">Successful preparation entries that were not registered because their keys were duplicates.</param>
    /// <param name="skippedEntries">Embedded-code model entries intentionally skipped by the builder.</param>
    /// <param name="allEntries">All build entries in model traversal order.</param>
    public PreparedExpressionEmbeddedCodeRegistryBuildResult(
        PreparedExpressionEmbeddedCodeRegistry registry,
        IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulSemanticPredicates,
        IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulParserActions,
        IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        SuccessfulSemanticPredicates = successfulSemanticPredicates?.ToArray() ?? throw new ArgumentNullException(nameof(successfulSemanticPredicates));
        SuccessfulParserActions = successfulParserActions?.ToArray() ?? throw new ArgumentNullException(nameof(successfulParserActions));
        NonSuccessEntries = nonSuccessEntries?.ToArray() ?? throw new ArgumentNullException(nameof(nonSuccessEntries));
        DuplicateEntries = duplicateEntries?.ToArray() ?? throw new ArgumentNullException(nameof(duplicateEntries));
        SkippedEntries = skippedEntries?.ToArray() ?? throw new ArgumentNullException(nameof(skippedEntries));
        AllEntries = allEntries?.ToArray() ?? throw new ArgumentNullException(nameof(allEntries));
    }

    /// <summary>
    /// Gets the registry populated with successfully prepared, non-duplicate artifacts.
    /// </summary>
    public PreparedExpressionEmbeddedCodeRegistry Registry { get; }

    /// <summary>
    /// Gets successful semantic predicate entries added to the registry.
    /// </summary>
    public IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> SuccessfulSemanticPredicates { get; }

    /// <summary>
    /// Gets successful parser action entries added to the registry.
    /// </summary>
    public IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> SuccessfulParserActions { get; }

    /// <summary>
    /// Gets entries returned by the preparer with non-success statuses.
    /// </summary>
    public IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> NonSuccessEntries { get; }

    /// <summary>
    /// Gets successful preparation entries that were not registered because their keys were duplicates.
    /// </summary>
    public IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> DuplicateEntries { get; }

    /// <summary>
    /// Gets embedded-code model entries intentionally skipped by the builder.
    /// </summary>
    public IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> SkippedEntries { get; }

    /// <summary>
    /// Gets all build entries in model traversal order.
    /// </summary>
    public IReadOnlyList<PreparedExpressionEmbeddedCodeRegistryBuildEntry> AllEntries { get; }

    /// <summary>
    /// Gets a value indicating whether any preparation failure or duplicate key was recorded.
    /// </summary>
    public bool HasFailures => NonSuccessEntries.Count > 0 || DuplicateEntries.Count > 0;
}

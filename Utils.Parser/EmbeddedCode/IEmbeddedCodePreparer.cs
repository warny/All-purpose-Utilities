namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Defines the minimal preparation boundary for ANTLR parser embedded code.
/// </summary>
/// <typeparam name="TPredicateArtifact">Prepared artifact type produced for semantic predicates.</typeparam>
/// <typeparam name="TActionArtifact">Prepared artifact type produced for inline parser actions.</typeparam>
internal interface IEmbeddedCodePreparer<TPredicateArtifact, TActionArtifact>
{
    /// <summary>
    /// Prepares semantic predicate source without evaluating it.
    /// </summary>
    /// <param name="source">Raw embedded-code source and metadata for a semantic predicate.</param>
    /// <param name="context">Explicit preparation context selected by the caller.</param>
    /// <returns>Preparation result containing an artifact or metadata that explains why no artifact was produced.</returns>
    EmbeddedCodePreparationResult<TPredicateArtifact> PrepareSemanticPredicate(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext context);

    /// <summary>
    /// Prepares inline parser action source without executing it.
    /// </summary>
    /// <param name="source">Raw embedded-code source and metadata for an inline parser action.</param>
    /// <param name="context">Explicit preparation context selected by the caller.</param>
    /// <returns>Preparation result containing an artifact or metadata that explains why no artifact was produced.</returns>
    EmbeddedCodePreparationResult<TActionArtifact> PrepareParserAction(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext context);
}

namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Neutral embedded-code preparer that preserves source metadata and never compiles or executes code.
/// </summary>
/// <typeparam name="TPredicateArtifact">Prepared artifact type that would be used for semantic predicates.</typeparam>
/// <typeparam name="TActionArtifact">Prepared artifact type that would be used for inline parser actions.</typeparam>
internal sealed class PreservingEmbeddedCodePreparer<TPredicateArtifact, TActionArtifact> : IEmbeddedCodePreparer<TPredicateArtifact, TActionArtifact>
{
    /// <inheritdoc />
    public EmbeddedCodePreparationResult<TPredicateArtifact> PrepareSemanticPredicate(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        return source.Kind == EmbeddedCodeKind.SemanticPredicate
            ? EmbeddedCodePreparationResult<TPredicateArtifact>.PreservedNotCompiled(CreateDiagnosticArguments(source, context))
            : EmbeddedCodePreparationResult<TPredicateArtifact>.Unsupported(CreateDiagnosticArguments(source, context));
    }

    /// <inheritdoc />
    public EmbeddedCodePreparationResult<TActionArtifact> PrepareParserAction(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        return source.Kind == EmbeddedCodeKind.ParserInlineAction
            ? EmbeddedCodePreparationResult<TActionArtifact>.PreservedNotCompiled(CreateDiagnosticArguments(source, context))
            : EmbeddedCodePreparationResult<TActionArtifact>.Unsupported(CreateDiagnosticArguments(source, context));
    }

    /// <summary>
    /// Creates diagnostic arguments that identify the preserved or unsupported embedded-code construct.
    /// </summary>
    /// <param name="source">Embedded source metadata used to describe the construct.</param>
    /// <param name="context">Preparation context used to describe the target path.</param>
    /// <returns>Diagnostic arguments suitable for the shared embedded-code diagnostic descriptors.</returns>
    private static IReadOnlyList<object?> CreateDiagnosticArguments(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext context)
    {
        var constructName = source.RuleName is null
            ? source.Kind.ToString()
            : $"{source.RuleName}:{source.Kind}";

        return new object?[] { constructName, context.Target.ToString() };
    }
}

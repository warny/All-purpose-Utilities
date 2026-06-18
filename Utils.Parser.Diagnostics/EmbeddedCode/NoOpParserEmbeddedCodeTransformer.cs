namespace Utils.Parser.Diagnostics.EmbeddedCode;

/// <summary>
/// Embedded-code transformer that preserves source code unchanged.
/// </summary>
public sealed class NoOpParserEmbeddedCodeTransformer : IParserEmbeddedCodeTransformer
{
    /// <summary>
    /// Gets the shared no-op transformer instance.
    /// </summary>
    public static NoOpParserEmbeddedCodeTransformer Instance { get; } = new();

    /// <inheritdoc />
    public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
    {
        if (context is null) throw new System.ArgumentNullException(nameof(context));
        return new ParserEmbeddedCodeTransformationResult { Code = context.Code };
    }
}

using System;
using System.Linq;

namespace Utils.Parser.Diagnostics.EmbeddedCode;

/// <summary>
/// Represents raw embedded target-language code exactly as it was read from the grammar.
/// </summary>
public sealed class RawEmbeddedCode
{
    /// <summary>
    /// Initializes a new raw embedded-code value.
    /// </summary>
    /// <param name="text">Raw embedded-code text without ANTLR delimiters.</param>
    public RawEmbeddedCode(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Gets the raw embedded-code text without ANTLR delimiters.</summary>
    public string Text { get; }

    /// <summary>Returns the raw embedded-code text for diagnostics and transformer input only.</summary>
    public override string ToString() => Text;
}

/// <summary>
/// Represents embedded target-language code produced by <see cref="ParserEmbeddedCodeTransformationService"/>.
/// </summary>
public sealed class TransformedEmbeddedCode
{
    /// <summary>
    /// Initializes a transformed embedded-code value after a transformer has run and diagnostics have been validated.
    /// </summary>
    /// <param name="text">Transformed embedded-code text ready for generated emission or expression compilation.</param>
    internal TransformedEmbeddedCode(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Gets the transformed embedded-code text ready for generated emission or expression compilation.</summary>
    public string Text { get; }

    /// <summary>Returns the transformed embedded-code text for final emission or compilation.</summary>
    public override string ToString() => Text;
}

/// <summary>
/// Executes embedded-code transformations and materializes validated transformed-code values.
/// </summary>
public static class ParserEmbeddedCodeTransformationService
{
    /// <summary>
    /// Runs a transformer for one raw embedded-code fragment and returns a typed transformed-code value.
    /// </summary>
    /// <param name="transformer">Transformer selected by the caller.</param>
    /// <param name="rawCode">Raw embedded-code value read from the grammar.</param>
    /// <param name="context">Transformation context containing passive grammar metadata.</param>
    /// <param name="createException">Optional exception factory used when the transformer reports an error diagnostic.</param>
    /// <returns>A transformed embedded-code value produced from the transformer result.</returns>
    public static TransformedEmbeddedCode TransformOrThrow(
        IParserEmbeddedCodeTransformer transformer,
        RawEmbeddedCode rawCode,
        ParserEmbeddedCodeTransformationContext context,
        Func<ParserEmbeddedCodeDiagnostic, Exception>? createException = null)
    {
        if (transformer is null) throw new ArgumentNullException(nameof(transformer));
        if (rawCode is null) throw new ArgumentNullException(nameof(rawCode));
        if (context is null) throw new ArgumentNullException(nameof(context));

        context.Code = rawCode.Text;
        ParserEmbeddedCodeTransformationResult result = transformer.Transform(context);
        if (result is null) throw new InvalidOperationException("Embedded-code transformer returned null.");

        ParserEmbeddedCodeDiagnostic? error = result.Diagnostics.FirstOrDefault(static diagnostic => diagnostic.Severity == ParserEmbeddedCodeDiagnosticSeverity.Error);
        if (error is not null)
        {
            throw createException?.Invoke(error) ?? new InvalidOperationException(error.Message);
        }

        return new TransformedEmbeddedCode(result.Code);
    }
}

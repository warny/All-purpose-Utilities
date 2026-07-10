using System;

namespace Utils.Parser.Diagnostics.EmbeddedCode;

/// <summary>
/// Represents raw embedded target-language code exactly as it was read from the grammar.
/// </summary>
public readonly record struct RawEmbeddedCode
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
/// Represents embedded target-language code that has passed through <see cref="IParserEmbeddedCodeTransformer"/>.
/// </summary>
public readonly record struct TransformedEmbeddedCode
{
    /// <summary>
    /// Initializes a transformed embedded-code value from a transformer result.
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
/// Converts embedded-code transformer results into typed transformed-code values.
/// </summary>
public static class ParserEmbeddedCodeTransformationResultExtensions
{
    /// <summary>
    /// Creates a transformed embedded-code value from an already returned transformer result.
    /// </summary>
    /// <param name="result">Transformer result to materialize.</param>
    /// <returns>A transformed embedded-code value carrying <see cref="ParserEmbeddedCodeTransformationResult.Code"/>.</returns>
    public static TransformedEmbeddedCode ToTransformedEmbeddedCode(this ParserEmbeddedCodeTransformationResult result)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        return new TransformedEmbeddedCode(result.Code);
    }
}

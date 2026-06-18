namespace Utils.Parser.Expressions;

/// <summary>
/// Exception thrown when an embedded-code transformer reports an error before dynamic expression compilation.
/// </summary>
public sealed class ParserEmbeddedCodeTransformationException : Exception
{
    /// <summary>
    /// Initializes a new transformation exception.
    /// </summary>
    /// <param name="message">Transformation diagnostic message.</param>
    public ParserEmbeddedCodeTransformationException(string message)
        : base(message)
    {
    }
}

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a blocking lexer validation failure.
/// </summary>
public sealed class LexerValidationException(string message) : Exception(message)
{
}

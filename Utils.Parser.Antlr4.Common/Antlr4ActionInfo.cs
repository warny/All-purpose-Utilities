namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Represents a grammar-level ANTLR4 action declared in the prequel section.
/// </summary>
/// <param name="Name">Action name such as <c>header</c> or <c>members</c>.</param>
/// <param name="Code">Raw action code content without surrounding braces.</param>
/// <param name="Target">Optional target scope such as <c>parser</c> or <c>lexer</c>.</param>
public sealed record Antlr4ActionInfo(string Name, string Code, string? Target);

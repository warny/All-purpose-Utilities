namespace Utils.Parser.ProjectCompilation;

/// <summary>
/// Represents a named ANTLR4 grammar source consumed by multi-grammar compilation.
/// </summary>
/// <param name="Name">Logical grammar name (without extension).</param>
/// <param name="Path">Optional source path used for diagnostics and relative-resolution context.</param>
/// <param name="Text">Raw <c>.g4</c> content.</param>
public sealed record GrammarSource(string Name, string? Path, string Text);

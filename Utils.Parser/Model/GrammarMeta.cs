namespace Utils.Parser.Model;

public enum GrammarType { Combined, Lexer, Parser }

/// <summary>
/// options { tokenVocab=MyLexer; superClass=MyBase; ... }
/// </summary>
public record GrammarOptions(IReadOnlyDictionary<string, string> Values);

/// <summary>
/// @header { import ... } ou @members { int count = 0; }
/// </summary>
public record GrammarAction(
    string Name,           // "header", "members", ou nom custom
    string RawCode,        // contenu brut du bloc { }
    string? Target = null  // @header::lexer { } → Target="lexer"
);

/// <summary>
/// Import d'une autre grammaire : import CommonLexer;
/// </summary>
public record GrammarImport(string GrammarName, string? Alias = null);

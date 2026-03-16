namespace Utils.Parser.Model;

/// <summary>
/// A named lexer mode containing an ordered list of lexer rules.
/// The default mode is always named <c>DEFAULT_MODE</c>.
/// Additional modes are declared via <c>mode NAME;</c> in the grammar.
/// Rules are ordered by <see cref="Rule.DeclarationOrder"/> to ensure correct
/// maximal-munch priority during tokenization.
/// </summary>
public record LexerMode(
    /// <summary>Name of this mode (e.g. <c>"DEFAULT_MODE"</c>, <c>"Argument"</c>).</summary>
    string Name,
    /// <summary>Lexer rules belonging to this mode, ordered by declaration order.</summary>
    IReadOnlyList<Rule> Rules
);

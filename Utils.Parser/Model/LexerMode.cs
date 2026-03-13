namespace Utils.Parser.Model;

public record LexerMode(
    string Name,
    IReadOnlyList<Rule> Rules    // ordonnées par DeclarationOrder
);

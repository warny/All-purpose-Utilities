namespace Utils.Parser.Model;

public record ParserDefinition(
    string Name,
    GrammarType Type,
    GrammarOptions? Options,
    IReadOnlyList<GrammarAction> Actions,
    IReadOnlyList<GrammarImport> Imports,
    IReadOnlyList<LexerMode> Modes,          // au moins le mode DEFAULT_MODE
    IReadOnlyList<Rule> ParserRules,
    Rule? RootRule                           // règle d'entrée (première règle parser)
)
{
    // Construit en passe 2
    public IReadOnlyDictionary<string, Rule> AllRules { get; init; }
        = new Dictionary<string, Rule>();
}

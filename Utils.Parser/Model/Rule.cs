namespace Utils.Parser.Model;

public enum RuleKind { Lexer, Parser, Unresolved }

/// <summary>
/// Options portées par une règle individuelle.
/// ex: options { greedy=false; }
/// </summary>
public record RuleOptions(IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Paramètre de règle parser : rule[int x, String y]
/// </summary>
public record RuleParameter(string Type, string Name);

/// <summary>
/// Valeur de retour de règle parser : rule returns [int value]
/// </summary>
public record RuleReturn(string Type, string Name);

public record Rule(
    string Name,
    int DeclarationOrder,       // ordre dans le fichier source → priorité inter-règles
    bool IsFragment,            // mot-clé 'fragment' explicitement présent
    Alternation Content,
    RuleOptions? Options = null,
    IReadOnlyList<RuleParameter>? Parameters = null,
    IReadOnlyList<RuleReturn>? Returns = null,
    EmbeddedAction? InitAction = null,   // @init { }
    EmbeddedAction? AfterAction = null   // @after { }
)
{
    // Inféré en passe 2 de résolution — jamais déclaré
    public RuleKind Kind { get; internal set; } = RuleKind.Unresolved;
}

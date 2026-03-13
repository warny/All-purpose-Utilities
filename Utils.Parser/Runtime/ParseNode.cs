using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

public abstract record ParseNode(SourceSpan Span, string ModeName, Rule Rule);

public record LexerNode(
    SourceSpan Span,
    string ModeName,
    Rule Rule,
    Token Token
) : ParseNode(Span, ModeName, Rule);

public record ParserNode(
    SourceSpan Span,
    string ModeName,
    Rule Rule,
    IReadOnlyList<ParseNode> Children
) : ParseNode(Span, ModeName, Rule);

/// <summary>
/// Nœud d'erreur — ne jamais lever d'exception, insérer dans l'arbre et continuer
/// </summary>
public record ErrorNode(
    SourceSpan Span,
    string ModeName,
    string Message,
    Rule? AttemptedRule = null
) : ParseNode(Span, ModeName, AttemptedRule!);

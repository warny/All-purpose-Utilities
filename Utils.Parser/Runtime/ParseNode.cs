using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Abstract base for all nodes in a parse tree produced by <see cref="ParserEngine"/>.
/// Every node records the source span it covers, the active lexer mode, and the grammar
/// rule that produced it.
/// </summary>
public abstract record ParseNode(
    /// <summary>Source range covered by this node.</summary>
    SourceSpan Span,
    /// <summary>Name of the active lexer mode when this node was produced.</summary>
    string ModeName,
    /// <summary>Grammar rule that produced this node.</summary>
    Rule Rule
);

/// <summary>
/// A leaf node corresponding to a single token produced by the lexer.
/// </summary>
public record LexerNode(
    /// <inheritdoc/>
    SourceSpan Span,
    /// <inheritdoc/>
    string ModeName,
    /// <inheritdoc/>
    Rule Rule,
    /// <summary>The token matched by the lexer.</summary>
    Token Token
) : ParseNode(Span, ModeName, Rule);

/// <summary>
/// An interior node produced by a parser rule, containing zero or more child nodes.
/// </summary>
public record ParserNode(
    /// <inheritdoc/>
    SourceSpan Span,
    /// <inheritdoc/>
    string ModeName,
    /// <inheritdoc/>
    Rule Rule,
    /// <summary>Ordered list of child nodes matched by this rule's alternative.</summary>
    IReadOnlyList<ParseNode> Children
) : ParseNode(Span, ModeName, Rule);

/// <summary>
/// A synthetic node inserted when parsing fails at a given position.
/// An error node is never thrown as an exception; it is always embedded in the tree
/// so that partial results remain accessible.
/// <see cref="ParseNode.Rule"/> holds the rule that was being attempted when the failure occurred.
/// </summary>
public record ErrorNode(
    /// <inheritdoc/>
    SourceSpan Span,
    /// <inheritdoc/>
    string ModeName,
    /// <summary>Human-readable description of the parse failure.</summary>
    string Message,
    /// <summary>The rule that was being attempted when parsing failed, or <c>null</c>.</summary>
    Rule? AttemptedRule = null
) : ParseNode(Span, ModeName, AttemptedRule!);

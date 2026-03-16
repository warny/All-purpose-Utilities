namespace Utils.Parser.Model;

/// <summary>
/// Identifies whether a rule belongs to the lexer or parser.
/// </summary>
public enum RuleKind
{
    /// <summary>Lexer rule — matched against the character stream to produce tokens.</summary>
    Lexer,
    /// <summary>Parser rule — matched against the token stream to build parse-tree nodes.</summary>
    Parser,
    /// <summary>
    /// Kind has not yet been determined during resolution.
    /// Rules in this state are resolved by <c>RuleResolver</c> in a subsequent pass.
    /// </summary>
    Unresolved
}

/// <summary>
/// Key/value options attached to an individual rule:
/// <c>options { greedy=false; }</c>.
/// </summary>
public record RuleOptions(IReadOnlyDictionary<string, string> Values);

/// <summary>
/// A typed parameter declared on a parser rule:
/// <c>rule[int x, String y]</c>.
/// </summary>
public record RuleParameter(
    /// <summary>Declared parameter type (e.g. <c>"int"</c>).</summary>
    string Type,
    /// <summary>Parameter name (e.g. <c>"x"</c>).</summary>
    string Name
);

/// <summary>
/// A typed return value declared on a parser rule:
/// <c>rule returns [int value]</c>.
/// </summary>
public record RuleReturn(
    /// <summary>Declared return type (e.g. <c>"int"</c>).</summary>
    string Type,
    /// <summary>Return value name (e.g. <c>"value"</c>).</summary>
    string Name
);

/// <summary>
/// A single grammar rule — either a lexer rule (upper-case name by convention)
/// or a parser rule (lower-case name by convention).
/// </summary>
public record Rule(
    /// <summary>Rule name as declared in the grammar (e.g. <c>"ID"</c>, <c>"expr"</c>).</summary>
    string Name,
    /// <summary>
    /// Zero-based position in the source file.
    /// Used by <see cref="Utils.Parser.Runtime.LexerEngine"/> to break ties when two rules
    /// match the same length: the rule with the lower declaration order wins.
    /// </summary>
    int DeclarationOrder,
    /// <summary>
    /// <c>true</c> when the <c>fragment</c> keyword is present.
    /// Fragment rules are only usable from other lexer rules and are never emitted as tokens.
    /// </summary>
    bool IsFragment,
    /// <summary>The rule body as an <see cref="Alternation"/> of one or more alternatives.</summary>
    Alternation Content,
    /// <summary>Per-rule options block, or <c>null</c> when absent.</summary>
    RuleOptions? Options = null,
    /// <summary>Typed parameters for parameterised parser rules, or <c>null</c>.</summary>
    IReadOnlyList<RuleParameter>? Parameters = null,
    /// <summary>Typed return specifications for parser rules with <c>returns</c>, or <c>null</c>.</summary>
    IReadOnlyList<RuleReturn>? Returns = null,
    /// <summary>Code executed before the rule body (<c>@init { }</c>), or <c>null</c>.</summary>
    EmbeddedAction? InitAction = null,
    /// <summary>Code executed after the rule body (<c>@after { }</c>), or <c>null</c>.</summary>
    EmbeddedAction? AfterAction = null
)
{
    /// <summary>
    /// Whether this is a lexer or parser rule.
    /// Set by <c>RuleResolver.Resolve</c> during the resolution pass;
    /// initially <see cref="RuleKind.Unresolved"/>.
    /// </summary>
    public RuleKind Kind { get; internal set; } = RuleKind.Unresolved;
}

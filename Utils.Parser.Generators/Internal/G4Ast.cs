using System.Collections.Generic;

namespace Utils.Parser.Generators.Internal;

// ── Grammar-level ────────────────────────────────────────────────────────────

/// <summary>
/// Identifies the ANTLR4 grammar declaration kind parsed by the source generator.
/// </summary>
internal enum G4GrammarKind
{
    /// <summary>A combined grammar that can contain both parser and lexer rules.</summary>
    Combined,

    /// <summary>A lexer grammar that can contain only lexer rules.</summary>
    Lexer,

    /// <summary>A parser grammar that can contain only parser rules.</summary>
    Parser
}

/// <summary>
/// Represents the generator's parsed ANTLR4 grammar model.
/// </summary>
internal sealed class G4Grammar
{
    /// <summary>Gets or sets the grammar name declared in the grammar header.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the grammar declaration kind.</summary>
    public G4GrammarKind Kind { get; set; }

    /// <summary>Gets grammar-level options declared in <c>options { ... }</c>.</summary>
    public Dictionary<string, string> Options { get; } = new Dictionary<string, string>();

    /// <summary>Gets lexer rules in <c>DEFAULT_MODE</c>.</summary>
    public List<G4Rule> LexerRules { get; } = new List<G4Rule>();

    /// <summary>Gets parser rules declared by the grammar.</summary>
    public List<G4Rule> ParserRules { get; } = new List<G4Rule>();

    /// <summary>Gets extra lexer modes declared via <c>mode Name;</c>.</summary>
    public List<G4LexerMode> ExtraModes { get; } = new List<G4LexerMode>();

    /// <summary>Gets grammar import directives declared with <c>import ...;</c>.</summary>
    public List<G4GrammarImport> Imports { get; } = new List<G4GrammarImport>();

    /// <summary>Gets token names declared in <c>tokens { ... }</c> blocks.</summary>
    public List<string> DeclaredTokens { get; } = new List<string>();

    /// <summary>Gets or sets a value indicating whether at least one <c>tokens { ... }</c> block was parsed.</summary>
    public bool HasTokensBlock { get; set; }

    /// <summary>Gets channel names declared in <c>channels { ... }</c> blocks.</summary>
    public List<string> DeclaredChannels { get; } = new List<string>();

    /// <summary>Gets or sets a value indicating whether at least one <c>channels { ... }</c> block was parsed.</summary>
    public bool HasChannelsBlock { get; set; }

    /// <summary>Gets grammar-level actions declared with <c>@...</c> constructs.</summary>
    public List<G4GrammarAction> Actions { get; } = new List<G4GrammarAction>();
}

/// <summary>
/// Represents one grammar imported by an ANTLR4 <c>import</c> directive.
/// </summary>
internal sealed class G4GrammarImport
{
    /// <summary>Gets or sets the imported grammar name.</summary>
    public string GrammarName { get; set; } = "";

    /// <summary>Gets or sets the optional import alias.</summary>
    public string? Alias { get; set; }
}

/// <summary>
/// Represents a grammar-level action such as <c>@header</c>, <c>@members</c>, or a scoped action.
/// </summary>
internal sealed class G4GrammarAction
{
    /// <summary>Gets or sets the action name after the <c>@</c> marker.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the raw action body captured from the grammar source.</summary>
    public string RawCode { get; set; } = "";

    /// <summary>Gets or sets the optional scoped action target, such as <c>parser</c> or <c>lexer</c>.</summary>
    public string? Target { get; set; }

    /// <summary>Gets or sets the one-based line where the grammar-level action starts in the <c>.g4</c> source.</summary>
    public int Line { get; set; }
}

/// <summary>
/// Represents a named ANTLR4 lexer mode and the rules declared within it.
/// </summary>
internal sealed class G4LexerMode
{
    /// <summary>Gets or sets the lexer mode name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets the lexer rules declared in this mode.</summary>
    public List<G4Rule> Rules { get; } = new List<G4Rule>();
}

/// <summary>
/// Represents a parser or lexer rule in the generator grammar model.
/// </summary>
internal sealed class G4Rule
{
    /// <summary>Gets or sets the rule name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets a value indicating whether the rule is declared as a lexer fragment.</summary>
    public bool IsFragment { get; set; }

    /// <summary>Gets or sets the optional rule <c>@init</c> lifecycle action.</summary>
    public G4EmbeddedAction? InitAction { get; set; }

    /// <summary>Gets or sets the optional rule <c>@after</c> lifecycle action.</summary>
    public G4EmbeddedAction? AfterAction { get; set; }

    /// <summary>Gets or sets raw rule-parameter metadata text preserved from the parameter clause (<c>[...]</c> before <c>returns</c>), or <c>null</c> when absent.</summary>
    public string? Parameters { get; set; }

    /// <summary>Gets or sets raw rule-return metadata text preserved from a <c>returns [...]</c> clause, or <c>null</c> when absent.</summary>
    public string? Returns { get; set; }

    /// <summary>Gets raw rule-local declarations preserved from a <c>locals [...]</c> clause.</summary>
    public List<string> Locals { get; } = new List<string>();

    /// <summary>Gets or sets the rule content as an alternation.</summary>
    public G4Alternation Content { get; set; } = new G4Alternation();
}

// ── Rule content ─────────────────────────────────────────────────────────────

/// <summary>Abstract base for all grammar content elements.</summary>
internal abstract class G4Content { }

/// <summary>
/// Represents an ANTLR4 alternation containing one or more alternatives.
/// </summary>
internal sealed class G4Alternation : G4Content
{
    /// <summary>Gets the alternatives in declaration order.</summary>
    public List<G4Alternative> Alternatives { get; } = new List<G4Alternative>();
}

/// <summary>
/// Represents one alternative inside an ANTLR4 alternation.
/// </summary>
internal sealed class G4Alternative : G4Content
{
    /// <summary>Gets or sets the zero-based priority assigned from declaration order.</summary>
    public int Priority { get; set; }

    /// <summary>Gets the ordered content items in this alternative.</summary>
    public List<G4Content> Items { get; } = new List<G4Content>();

    /// <summary>Gets or sets the optional alternative label declared with <c>#Label</c>.</summary>
    public string? Label { get; set; }
}

/// <summary>
/// Represents a sequence of grammar content items.
/// </summary>
internal sealed class G4Sequence : G4Content
{
    /// <summary>Gets the ordered content items in the sequence.</summary>
    public List<G4Content> Items { get; } = new List<G4Content>();
}

/// <summary>
/// Represents a quantified grammar content item.
/// </summary>
internal sealed class G4Quantifier : G4Content
{
    /// <summary>Gets or sets the quantified inner content.</summary>
    public G4Content Inner { get; set; } = null!;

    /// <summary>Gets or sets the minimum number of repetitions.</summary>
    public int Min { get; set; }

    /// <summary>Gets or sets the maximum number of repetitions, or <c>null</c> for unbounded repetitions.</summary>
    public int? Max { get; set; }

    /// <summary>Gets or sets a value indicating whether the quantifier is greedy.</summary>
    public bool Greedy { get; set; } = true;
}

/// <summary>
/// Represents a literal token or character match.
/// </summary>
internal sealed class G4LiteralMatch : G4Content
{
    /// <summary>Gets or sets the literal value to match.</summary>
    public string Value { get; set; } = "";
}

/// <summary>
/// Represents a character range match such as <c>'a'..'z'</c>.
/// </summary>
internal sealed class G4RangeMatch : G4Content
{
    /// <summary>Gets or sets the inclusive lower bound of the range.</summary>
    public char From { get; set; }

    /// <summary>Gets or sets the inclusive upper bound of the range.</summary>
    public char To { get; set; }
}

/// <summary>
/// Represents a character class <c>[...]</c> with ranges and single-character entries.
/// </summary>
internal sealed class G4CharClassMatch : G4Content
{
    /// <summary>Gets entries where each item is either <c>(c, null)</c> for a single char or <c>(lo, hi)</c> for a range.</summary>
    public List<(char Lo, char? Hi)> Entries { get; } = new List<(char, char?)>();

    /// <summary>Gets or sets a value indicating whether the character class is negated.</summary>
    public bool Negated { get; set; }
}

/// <summary>
/// Represents a wildcard match (<c>.</c>) in the generator grammar model.
/// </summary>
internal sealed class G4AnyCharMatch : G4Content { }

/// <summary>
/// Represents a reference to another parser or lexer rule.
/// </summary>
internal sealed class G4RuleRef : G4Content
{
    /// <summary>Gets or sets the referenced rule name.</summary>
    public string RuleName { get; set; } = "";

    /// <summary>
    /// Gets or sets raw argument text preserved from a <c>callee[...]</c> call-site argument clause, or <c>null</c> when absent.
    /// The outer brackets are excluded. This text is not evaluated and is not passed to child rule frames.
    /// </summary>
    public string? RawArguments { get; set; }
}

/// <summary>
/// Represents a negated grammar content item.
/// </summary>
internal sealed class G4Negation : G4Content
{
    /// <summary>Gets or sets the negated inner content.</summary>
    public G4Content Inner { get; set; } = null!;
}

/// <summary>
/// Represents an ANTLR4 lexer command declared after <c>-></c>.
/// </summary>
internal sealed class G4LexerCommand : G4Content
{
    /// <summary>Gets or sets the lexer command name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the optional lexer command argument.</summary>
    public string? Arg { get; set; }
}

/// <summary>
/// Represents an embedded action block or semantic predicate block.
/// </summary>
internal sealed class G4EmbeddedAction : G4Content
{
    /// <summary>Gets or sets the raw embedded code captured from the grammar source.</summary>
    public string Code { get; set; } = "";

    /// <summary>Gets or sets a value indicating whether the block ends with <c>?</c> and is a validating predicate.</summary>
    public bool IsPredicate { get; set; }

    /// <summary>Gets or sets the one-based line where the embedded action block starts in the <c>.g4</c> source.</summary>
    public int Line { get; set; }
}

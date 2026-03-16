namespace Utils.Parser.Model;

/// <summary>
/// Abstract root for all grammar elements that can appear inside a rule body.
/// Concrete subtypes fall into three categories:
/// tokenizer content (character-level matching), composite structures
/// (sequences, alternations, quantifiers), and meta-instructions
/// (predicates, embedded actions, lexer commands).
/// </summary>
public abstract record RuleContent;

// ─── Tokenizer leaves (operate directly on the character stream) ──────────────

/// <summary>
/// Base for all character-level match operations used inside lexer rules.
/// </summary>
public abstract record TokenizerContent : RuleContent;

/// <summary>
/// Matches an exact literal string, e.g. <c>'class'</c> or <c>'=='</c>.
/// </summary>
public record LiteralMatch(
    /// <summary>The exact string to match.</summary>
    string Value
) : TokenizerContent;

/// <summary>
/// Matches any single character within an inclusive range, e.g. <c>'a'..'z'</c>.
/// </summary>
public record RangeMatch(
    /// <summary>Inclusive lower bound of the character range.</summary>
    char From,
    /// <summary>Inclusive upper bound of the character range.</summary>
    char To
) : TokenizerContent;

/// <summary>
/// Matches a single character that is (or is not) a member of an explicit set,
/// e.g. <c>[a-zA-Z_]</c> or <c>[^0-9]</c>.
/// </summary>
public record CharSetMatch(
    /// <summary>Set of characters that constitute the match class.</summary>
    IReadOnlySet<char> Chars,
    /// <summary><c>true</c> when the <c>^</c> negation prefix is present.</summary>
    bool Negated
) : TokenizerContent;

/// <summary>
/// Matches any single character (the <c>.</c> wildcard in lexer rules).
/// </summary>
public record AnyChar : TokenizerContent;

// ─── Reference leaves ─────────────────────────────────────────────────────────

/// <summary>
/// A reference to another rule, either lexer or parser depending on context.
/// Optionally carries a label: <c>e=expr</c> or <c>ids+=ID</c>.
/// </summary>
public record RuleRef(
    /// <summary>Name of the referenced rule.</summary>
    string RuleName,
    /// <summary>Optional label assignment, or <c>null</c> when unlabeled.</summary>
    RuleLabel? Label = null
) : RuleContent;

/// <summary>
/// A label attached to a rule reference inside a parser alternative.
/// </summary>
public record RuleLabel(
    /// <summary>Label identifier (the left-hand side of <c>=</c> or <c>+=</c>).</summary>
    string Label,
    /// <summary>Name of the referenced rule.</summary>
    string RuleName,
    /// <summary>
    /// <c>true</c> for additive labels (<c>+=</c>, accumulates into a list);
    /// <c>false</c> for scalar labels (<c>=</c>, captures a single value).
    /// </summary>
    bool IsAdditive
);

/// <summary>
/// A lexer mode switch: <c>pushMode</c>, <c>popMode</c>, or <c>mode</c> directive.
/// </summary>
public record ModeSwitch(
    /// <summary>Target mode name.</summary>
    string ModeName,
    /// <summary><c>true</c> for <c>pushMode</c>; <c>false</c> for <c>mode</c> or <c>popMode</c>.</summary>
    bool Push
) : RuleContent;

// ─── Semantic predicates ──────────────────────────────────────────────────────

/// <summary>
/// A validating semantic predicate <c>{ condition }?</c> placed before an element.
/// If the condition evaluates to <c>false</c> the enclosing alternative is rejected.
/// </summary>
public record ValidatingPredicate(
    /// <summary>Raw predicate source code (without surrounding braces and the trailing <c>?</c>).</summary>
    string Code
) : RuleContent;

/// <summary>
/// A precedence predicate of the form <c>{ precpred(_ctx, n) }?</c>,
/// used by ANTLR4 to handle left-recursive rules.
/// The numeric level is parsed from the raw code rather than stored verbatim.
/// </summary>
public record PrecedencePredicate(
    /// <summary>Precedence level extracted from the <c>precpred</c> call.</summary>
    int Level
) : RuleContent;

/// <summary>
/// A gating semantic predicate in ANTLR3 syntax: <c>{ condition }? =></c>.
/// Retained for compatibility with grammars that still use this older form.
/// </summary>
public record GatingPredicate(
    /// <summary>Raw predicate source code.</summary>
    string Code
) : RuleContent;

// ─── Embedded actions ─────────────────────────────────────────────────────────

/// <summary>
/// Describes where in a grammar an embedded action or predicate was declared.
/// </summary>
public enum ActionContext
{
    /// <summary>Top-level grammar action: <c>@header { }</c>, <c>@members { }</c>.</summary>
    Grammar,
    /// <summary>Rule-level lifecycle action: <c>@init { }</c>, <c>@after { }</c>.</summary>
    Rule,
    /// <summary>Inline action embedded inside an alternative: <c>{ code }</c>.</summary>
    Alternative,
    /// <summary>Structured lexer command: <c>-> skip</c>, <c>-> channel(HIDDEN)</c>, <c>-> type(TOKEN)</c>.</summary>
    LexerCommand
}

/// <summary>
/// Describes where relative to the surrounding element an action appears.
/// </summary>
public enum ActionPosition
{
    /// <summary>Action precedes the element (e.g. <c>@init</c>).</summary>
    Before,
    /// <summary>Action follows the element (e.g. <c>@after</c>).</summary>
    After,
    /// <summary>Action is inlined directly inside an alternative.</summary>
    Inline
}

/// <summary>
/// A reference to a labelled value inside action code: <c>$e.text</c>, <c>$value</c>, <c>$ctx.start</c>.
/// </summary>
public record LabelRef(
    /// <summary>Rule label prefix (the part before <c>.</c>), or <c>null</c> for direct references such as <c>$value</c>.</summary>
    string? RuleLabel,
    /// <summary>Property name (the part after <c>.</c>), or <c>null</c> when no property is accessed.</summary>
    string? Property
);

/// <summary>
/// An opaque action block embedded in the grammar, captured with its context.
/// Label references (<c>$xxx</c>) are extracted from the raw code to allow
/// later resolution without re-parsing the action text.
/// </summary>
public record EmbeddedAction(
    /// <summary>Raw action source code (without surrounding braces).</summary>
    string RawCode,
    /// <summary>Context in which the action was declared.</summary>
    ActionContext Context,
    /// <summary>Position of the action relative to its surrounding element.</summary>
    ActionPosition Position,
    /// <summary>Label references extracted from <see cref="RawCode"/>.</summary>
    IReadOnlyList<LabelRef> Labels
) : RuleContent;

/// <summary>
/// A structured lexer command such as <c>-> skip</c>, <c>-> channel(HIDDEN)</c>,
/// or <c>-> pushMode(MyMode)</c>.
/// Unlike <see cref="EmbeddedAction"/>, these commands have direct runtime semantics
/// and are not opaque code.
/// </summary>
public record LexerCommand(
    /// <summary>Type of the lexer command.</summary>
    LexerCommandType Type,
    /// <summary>Optional argument (e.g. channel name, mode name, token type name).</summary>
    string? Argument
) : RuleContent;

/// <summary>
/// Identifies the kind of a structured lexer command.
/// </summary>
public enum LexerCommandType
{
    /// <summary>Discard the matched text; the token is not emitted.</summary>
    Skip,
    /// <summary>Append matched text to the next token rather than emitting a separate token.</summary>
    More,
    /// <summary>Assign the token to a named channel (e.g. <c>HIDDEN</c>).</summary>
    Channel,
    /// <summary>Override the token type.</summary>
    Type,
    /// <summary>Save the current mode and switch to a new one.</summary>
    PushMode,
    /// <summary>Return to the previously saved mode.</summary>
    PopMode,
    /// <summary>Switch unconditionally to a named mode.</summary>
    Mode
}

// ─── Composite structures ─────────────────────────────────────────────────────

/// <summary>
/// An ordered sequence of grammar elements that must all match in order: <c>A B C</c>.
/// </summary>
public record Sequence(IReadOnlyList<RuleContent> Items) : RuleContent;

/// <summary>
/// A quantified repetition: <c>*</c>, <c>+</c>, <c>?</c>, or <c>{n,m}</c>.
/// <see cref="Greedy"/> is <c>true</c> by default; setting it to <c>false</c>
/// enables non-greedy matching (the <c>??</c>, <c>*?</c>, <c>+?</c> suffixes).
/// </summary>
public record Quantifier(
    /// <summary>The element to be repeated.</summary>
    RuleContent Inner,
    /// <summary>Minimum number of repetitions (0 for <c>*</c> and <c>?</c>, 1 for <c>+</c>).</summary>
    int Min,
    /// <summary>Maximum number of repetitions, or <c>null</c> for unbounded (<c>*</c>, <c>+</c>).</summary>
    int? Max,
    /// <summary><c>true</c> for greedy matching (default); <c>false</c> for non-greedy.</summary>
    bool Greedy = true
) : RuleContent;

/// <summary>
/// Negation of an element (<c>~expr</c>): matches any single token or character
/// that does <em>not</em> match <see cref="Inner"/>.
/// </summary>
public record Negation(RuleContent Inner) : RuleContent;

/// <summary>
/// A single alternative inside an <see cref="Alternation"/>, with its priority,
/// associativity, content, and optional label.
/// </summary>
public record Alternative(
    /// <summary>Zero-based ordinal within the enclosing alternation; lower value = higher priority.</summary>
    int Priority,
    /// <summary>Associativity used for left-recursive disambiguation.</summary>
    Associativity Assoc,
    /// <summary>Body of this alternative (typically a <see cref="Sequence"/>).</summary>
    RuleContent Content,
    /// <summary>Alternative label declared with <c>#</c>, e.g. <c>expr # AddExpr</c>.</summary>
    string? Label = null
) : RuleContent;

/// <summary>
/// A set of mutually exclusive alternatives: <c>A | B | C</c>.
/// This is the top-level content of every <see cref="Rule"/>.
/// </summary>
public record Alternation(IReadOnlyList<Alternative> Alternatives) : RuleContent;

/// <summary>
/// Left/right/no associativity for an alternative, used when resolving
/// left-recursive ambiguity.
/// </summary>
public enum Associativity
{
    /// <summary>Left-associative (default).</summary>
    Left,
    /// <summary>Right-associative (<c>assoc=right</c> option).</summary>
    Right,
    /// <summary>Non-associative.</summary>
    None
}

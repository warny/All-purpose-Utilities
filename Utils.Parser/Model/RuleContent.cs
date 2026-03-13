namespace Utils.Parser.Model;

// Racine abstraite
public abstract record RuleContent;

// ─── Feuilles Tokenizer (opèrent sur le flux de caractères) ───────────────────

public abstract record TokenizerContent : RuleContent;

/// <summary>
/// Correspond à une chaîne littérale exacte, ex: 'class', '=='
/// </summary>
public record LiteralMatch(string Value) : TokenizerContent;

/// <summary>
/// Correspond à un range de caractères, ex: 'a'..'z'
/// </summary>
public record RangeMatch(char From, char To) : TokenizerContent;

/// <summary>
/// Correspond à un ensemble de caractères, ex: [a-zA-Z_], [^0-9]
/// </summary>
public record CharSetMatch(IReadOnlySet<char> Chars, bool Negated) : TokenizerContent;

/// <summary>
/// Correspond à n'importe quel caractère (wildcard '.')
/// </summary>
public record AnyChar : TokenizerContent;

// ─── Feuilles de référence ────────────────────────────────────────────────────

/// <summary>
/// Référence à une autre règle (lexer ou parser selon le contexte).
/// Peut porter un label : e=expr ou ids+=ID
/// </summary>
public record RuleRef(
    string RuleName,
    RuleLabel? Label = null
) : RuleContent;

/// <summary>
/// Label sur une référence de règle dans une alternative parser.
/// IsAdditive = true pour += (liste), false pour = (scalaire)
/// </summary>
public record RuleLabel(string Label, string RuleName, bool IsAdditive);

/// <summary>
/// Changement de mode lexer (pushMode, popMode, mode)
/// </summary>
public record ModeSwitch(string ModeName, bool Push) : RuleContent;

// ─── Prédicats sémantiques ────────────────────────────────────────────────────

/// <summary>
/// { condition }? avant un élément — rejette l'alternative si false
/// </summary>
public record ValidatingPredicate(string Code) : RuleContent;

/// <summary>
/// { precpred(_ctx, n) }? — gère la précédence pour la récursion gauche.
/// Le niveau est parsé depuis le code, pas stocké en brut.
/// </summary>
public record PrecedencePredicate(int Level) : RuleContent;

/// <summary>
/// { condition }? => (forme historique ANTLR3, encore présente dans certaines grammaires)
/// </summary>
public record GatingPredicate(string Code) : RuleContent;

// ─── Actions embarquées ───────────────────────────────────────────────────────

public enum ActionContext
{
    Grammar,      // @header { }, @members { } au niveau grammaire
    Rule,         // @init { }, @after { } au niveau règle
    Alternative,  // { code } inline dans une alternative
    LexerCommand  // -> skip, -> channel(HIDDEN), -> type(TOKEN)
}

public enum ActionPosition { Before, After, Inline }

/// <summary>
/// Référence à un label dans le code d'une action : $e.text, $value, $ctx.start
/// </summary>
public record LabelRef(
    string? RuleLabel,  // null si référence directe ($value)
    string? Property    // null si pas de propriété ($e seul)
);

/// <summary>
/// Action embarquée — code cible opaque capturé avec son contexte.
/// Les labels $xxx sont extraits du code brut pour permettre leur résolution.
/// </summary>
public record EmbeddedAction(
    string RawCode,
    ActionContext Context,
    ActionPosition Position,
    IReadOnlyList<LabelRef> Labels   // labels $xxx extraits du RawCode
) : RuleContent;

/// <summary>
/// Commandes lexer structurées -> skip, -> channel(HIDDEN), etc.
/// Ce sont des actions avec effet direct sur le runtime, pas du code opaque.
/// </summary>
public record LexerCommand(LexerCommandType Type, string? Argument) : RuleContent;

public enum LexerCommandType { Skip, More, Channel, Type, PushMode, PopMode, Mode }

// ─── Composites ───────────────────────────────────────────────────────────────

/// <summary>
/// Séquence ordonnée d'éléments (A B C)
/// </summary>
public record Sequence(IReadOnlyList<RuleContent> Items) : RuleContent;

/// <summary>
/// Quantificateur : *, +, ?, {n,m}.
/// Greedy = true par défaut, false si suffixe ? (non-greedy)
/// </summary>
public record Quantifier(RuleContent Inner, int Min, int? Max, bool Greedy = true) : RuleContent;

/// <summary>
/// Négation d'un élément (~expr)
/// </summary>
public record Negation(RuleContent Inner) : RuleContent;

/// <summary>
/// Une alternative avec sa priorité et son associativité
/// </summary>
public record Alternative(
    int Priority,           // index ordinal dans la règle parente (0 = plus prioritaire)
    Associativity Assoc,
    RuleContent Content,    // généralement une Sequence
    string? Label = null    // label d'alternative : expr # AddExpr
) : RuleContent;

/// <summary>
/// Ensemble d'alternatives (A | B | C)
/// </summary>
public record Alternation(IReadOnlyList<Alternative> Alternatives) : RuleContent;

public enum Associativity { Left, Right, None }

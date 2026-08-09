using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Utils.Parser.Model;

/// <summary>
/// Complete, immutable description of a grammar produced either by loading a <c>.g4</c>
/// file through <c>Antlr4GrammarConverter</c> or by constructing the meta-grammar
/// programmatically (as done in <c>Antlr4Grammar.Build()</c>).
/// <para>
/// A <see cref="ParserDefinition"/> is consumed by:
/// <list type="bullet">
///   <item><see cref="Utils.Parser.Runtime.LexerEngine"/> — to tokenize an input stream,</item>
///   <item><see cref="Utils.Parser.Runtime.ParserEngine"/> — to build a parse tree.</item>
/// </list>
/// </para>
/// After construction, call <c>RuleResolver.Resolve(definition)</c> to populate
/// <see cref="AllRules"/> and validate rule references.
/// </summary>
public record ParserDefinition(
    /// <summary>Grammar name as declared in the source (e.g. <c>"Exp"</c>).</summary>
    string Name,
    /// <summary>Grammar kind: combined, lexer-only, or parser-only.</summary>
    GrammarType Type,
    /// <summary>Options block, or <c>null</c> when absent.</summary>
    GrammarOptions? Options,
    /// <summary>Top-level action blocks (<c>@header</c>, <c>@members</c>, etc.).</summary>
    IReadOnlyList<GrammarAction> Actions,
    /// <summary>Grammar import directives.</summary>
    IReadOnlyList<GrammarImport> Imports,
    /// <summary>
    /// All lexer modes, with <c>DEFAULT_MODE</c> always at index 0.
    /// Each mode holds an ordered list of lexer rules.
    /// </summary>
    IReadOnlyList<LexerMode> Modes,
    /// <summary>Token names declared in optional <c>tokens { ... }</c> blocks.</summary>
    IReadOnlySet<string>? DeclaredTokens,
    /// <summary>Channel names declared in optional <c>channels { ... }</c> blocks.</summary>
    IReadOnlySet<string>? DeclaredChannels,
    /// <summary>Grammar-superClass extension bindings discovered during compilation.</summary>
    IReadOnlyList<GrammarExtensionBinding>? ExtensionBindings,
    /// <summary>Parser rules in declaration order.</summary>
    IReadOnlyList<Rule> ParserRules,
    /// <summary>Entry-point rule (first parser rule), or <c>null</c> for lexer-only grammars.</summary>
    Rule? RootRule
)
{
    /// <summary>Top-level action blocks captured as an immutable snapshot.</summary>
    private IReadOnlyList<GrammarAction> _actions = Actions.ToImmutableArray();

    /// <summary>Gets the immutable snapshot of top-level action blocks.</summary>
    public IReadOnlyList<GrammarAction> Actions
    {
        get => _actions;
        init => _actions = value.ToImmutableArray();
    }

    /// <summary>Grammar imports captured as an immutable snapshot.</summary>
    private IReadOnlyList<GrammarImport> _imports = Imports.ToImmutableArray();

    /// <summary>Gets the immutable snapshot of grammar imports.</summary>
    public IReadOnlyList<GrammarImport> Imports
    {
        get => _imports;
        init => _imports = value.ToImmutableArray();
    }

    /// <summary>Lexer modes captured as an immutable snapshot.</summary>
    private IReadOnlyList<LexerMode> _modes = Modes.ToImmutableArray();

    /// <summary>Gets the immutable snapshot of lexer modes.</summary>
    public IReadOnlyList<LexerMode> Modes
    {
        get => _modes;
        init => _modes = value.ToImmutableArray();
    }

    /// <summary>Parser rules captured as an immutable snapshot.</summary>
    private IReadOnlyList<Rule> _parserRules = ParserRules.ToImmutableArray();

    /// <summary>Gets the immutable snapshot of parser rules.</summary>
    public IReadOnlyList<Rule> ParserRules
    {
        get => _parserRules;
        init => _parserRules = value.ToImmutableArray();
    }

    /// <summary>
    /// Backward-compatible constructor used by generated code that does not provide extension metadata.
    /// </summary>
    public ParserDefinition(
        string Name,
        GrammarType Type,
        GrammarOptions? Options,
        IReadOnlyList<GrammarAction> Actions,
        IReadOnlyList<GrammarImport> Imports,
        IReadOnlyList<LexerMode> Modes,
        IReadOnlyList<Rule> ParserRules,
        Rule? RootRule)
        : this(
            Name,
            Type,
            Options,
            Actions,
            Imports,
            Modes,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            [],
            ParserRules: ParserRules,
            RootRule: RootRule)
    {
    }

    /// <summary>Token names declared in optional <c>tokens { ... }</c> blocks.</summary>
    private IReadOnlySet<string> _declaredTokens =
        (DeclaredTokens ?? Enumerable.Empty<string>()).ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>Gets the immutable ordinal set of declared tokens.</summary>
    public IReadOnlySet<string> DeclaredTokens
    {
        get => _declaredTokens;
        init => _declaredTokens = (value ?? Enumerable.Empty<string>()).ToImmutableHashSet(StringComparer.Ordinal);
    }

    /// <summary>Channel names declared in optional <c>channels { ... }</c> blocks.</summary>
    private IReadOnlySet<string> _declaredChannels =
        (DeclaredChannels ?? Enumerable.Empty<string>()).ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>Gets the immutable ordinal set of declared channels.</summary>
    public IReadOnlySet<string> DeclaredChannels
    {
        get => _declaredChannels;
        init => _declaredChannels = (value ?? Enumerable.Empty<string>()).ToImmutableHashSet(StringComparer.Ordinal);
    }

    /// <summary>Grammar-superClass extension bindings discovered during compilation.</summary>
    private IReadOnlyList<GrammarExtensionBinding> _extensionBindings =
        (ExtensionBindings ?? []).ToImmutableArray();

    /// <summary>Gets the immutable snapshot of extension bindings.</summary>
    public IReadOnlyList<GrammarExtensionBinding> ExtensionBindings
    {
        get => _extensionBindings;
        init => _extensionBindings = (value ?? []).ToImmutableArray();
    }

    /// <summary>
    /// Normalized effective options derived from <see cref="Options"/> and <see cref="Type"/>.
    /// Populated by <c>RuleResolver.Resolve</c>.
    /// </summary>
    public EffectiveGrammarOptions EffectiveOptions { get; init; } = new();

    /// <summary>
    /// Allows parser grammars to include external lexer rules provided by project-level compilation.
    /// </summary>
    public bool AllowExternalLexerRules { get; init; }

    /// <summary>
    /// Flat lookup of all rules (both lexer and parser) by name.
    /// Populated during the resolution pass by <c>RuleResolver.Resolve</c>.
    /// </summary>
    private IReadOnlyDictionary<string, Rule> _allRules =
        ImmutableDictionary<string, Rule>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>Gets the immutable ordinal lookup of all rules.</summary>
    public IReadOnlyDictionary<string, Rule> AllRules
    {
        get => _allRules;
        init => _allRules = value.ToImmutableDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Lookup table of direct left-recursive parser rules computed during
    /// resolution.
    /// </summary>
    private IReadOnlyDictionary<string, LeftRecursiveRuleInfo> _leftRecursiveRules =
        ImmutableDictionary<string, LeftRecursiveRuleInfo>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>Gets the immutable ordinal lookup of left-recursive rules.</summary>
    public IReadOnlyDictionary<string, LeftRecursiveRuleInfo> LeftRecursiveRules
    {
        get => _leftRecursiveRules;
        init => _leftRecursiveRules = value.ToImmutableDictionary(StringComparer.Ordinal);
    }
}

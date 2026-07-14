using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{
    private static readonly NamedActionInjectionDescriptor ParserHeaderDescriptor = new(
        ParserEmbeddedCodeLocation.ParserHeader,
        CSharpEmbeddedCodeRegion.ParserHeader,
        EmbeddedMembersSupport.IsInjectableParserHeaderAction,
        "parser header");

    private static readonly NamedActionInjectionDescriptor ParserMembersDescriptor = new(
        ParserEmbeddedCodeLocation.ParserMembers,
        CSharpEmbeddedCodeRegion.ParserMembers,
        EmbeddedMembersSupport.IsInjectableParserMembersAction,
        "parser members");

    private static readonly NamedActionInjectionDescriptor ParserFooterDescriptor = new(
        ParserEmbeddedCodeLocation.ParserFooter,
        CSharpEmbeddedCodeRegion.ParserFooter,
        EmbeddedMembersSupport.IsInjectableParserFooterAction,
        "parser footer");

    private static readonly NamedActionInjectionDescriptor LexerHeaderDescriptor = new(
        ParserEmbeddedCodeLocation.LexerHeader,
        CSharpEmbeddedCodeRegion.LexerHeader,
        EmbeddedMembersSupport.IsInjectableLexerHeaderAction,
        "lexer header");

    private static readonly NamedActionInjectionDescriptor LexerMembersDescriptor = new(
        ParserEmbeddedCodeLocation.LexerMembers,
        CSharpEmbeddedCodeRegion.LexerMembers,
        EmbeddedMembersSupport.IsInjectableLexerMembersAction,
        "lexer members");

    private static readonly NamedActionInjectionDescriptor LexerFooterDescriptor = new(
        ParserEmbeddedCodeLocation.LexerFooter,
        CSharpEmbeddedCodeRegion.LexerFooter,
        EmbeddedMembersSupport.IsInjectableLexerFooterAction,
        "lexer footer");

    /// <summary>
    /// Describes the grammar-level named-action injection point that varies between parser and lexer headers, members, and footers.
    /// </summary>
    private readonly struct NamedActionInjectionDescriptor
    {
        /// <summary>Initializes a named-action injection descriptor.</summary>
        /// <param name="location">Transformation location to report to the embedded-code transformer.</param>
        /// <param name="region">Generated C# region that controls markers, indentation, and spacing.</param>
        /// <param name="selector">Classification predicate used to choose grammar actions.</param>
        /// <param name="diagnosticTargetName">Human-readable target name reserved for diagnostics and debugging.</param>
        public NamedActionInjectionDescriptor(ParserEmbeddedCodeLocation location, CSharpEmbeddedCodeRegion region, Func<G4Grammar, G4GrammarAction, bool> selector, string diagnosticTargetName)
        {
            Location = location;
            Region = region;
            Selector = selector ?? throw new ArgumentNullException(nameof(selector));
            DiagnosticTargetName = diagnosticTargetName ?? throw new ArgumentNullException(nameof(diagnosticTargetName));
        }

        /// <summary>Gets the embedded-code transformer location.</summary>
        public ParserEmbeddedCodeLocation Location { get; }

        /// <summary>Gets the generated C# region emitted by the injector.</summary>
        public CSharpEmbeddedCodeRegion Region { get; }

        /// <summary>Gets the predicate that selects matching grammar actions.</summary>
        public Func<G4Grammar, G4GrammarAction, bool> Selector { get; }

        /// <summary>Gets the human-readable target name reserved for diagnostics and debugging.</summary>
        public string DiagnosticTargetName { get; }
    }

    /// <summary>
    /// Collects parser header blocks that can be injected near the top of generated C# output.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <returns>Parser header actions in grammar order.</returns>
    private static IReadOnlyList<G4GrammarAction> CollectParserHeaders(G4Grammar grammar) => CollectNamedActions(grammar, ParserHeaderDescriptor);

    /// <summary>
    /// Collects parser member blocks that can be injected into the parser execution context.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <returns>Parser member actions in grammar order.</returns>
    private static IReadOnlyList<G4GrammarAction> CollectParserMembers(G4Grammar grammar) => CollectNamedActions(grammar, ParserMembersDescriptor);

    /// <summary>
    /// Collects parser footer blocks that can be injected near the end of generated C# output.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <returns>Parser footer actions in grammar order.</returns>
    private static IReadOnlyList<G4GrammarAction> CollectParserFooters(G4Grammar grammar) => CollectNamedActions(grammar, ParserFooterDescriptor);

    /// <summary>
    /// Collects lexer header blocks that can be injected near the top of generated C# output.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <returns>Lexer header actions in grammar order.</returns>
    private static IReadOnlyList<G4GrammarAction> CollectLexerHeaders(G4Grammar grammar) => CollectNamedActions(grammar, LexerHeaderDescriptor);

    /// <summary>
    /// Collects lexer member blocks that can be injected into the generated execution context.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <returns>Lexer member actions in grammar order.</returns>
    private static IReadOnlyList<G4GrammarAction> CollectLexerMembers(G4Grammar grammar) => CollectNamedActions(grammar, LexerMembersDescriptor);

    /// <summary>
    /// Collects lexer footer blocks that can be injected near the end of generated C# output.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <returns>Lexer footer actions in grammar order.</returns>
    private static IReadOnlyList<G4GrammarAction> CollectLexerFooters(G4Grammar grammar) => CollectNamedActions(grammar, LexerFooterDescriptor);

    /// <summary>
    /// Emits parser header blocks before generated type declarations.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="parserHeaders">Parser header blocks to inject.</param>
    /// <param name="grammar">Parsed grammar AST used for transformer context.</param>
    /// <param name="transformer">Parser embedded-code transformer used for parser headers.</param>
    private static void EmitParserHeaders(StringBuilder sb, IReadOnlyList<G4GrammarAction> parserHeaders, G4Grammar grammar, IParserEmbeddedCodeTransformer transformer) =>
        EmitNamedActionRegion(sb, parserHeaders, grammar, transformer, ParserHeaderDescriptor);

    /// <summary>
    /// Emits parser member blocks into the generated execution context.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="parserMembers">Parser member blocks to inject.</param>
    /// <param name="grammar">Parsed grammar AST used for transformer context.</param>
    /// <param name="transformer">Parser embedded-code transformer used for parser members.</param>
    private static void EmitParserMembers(StringBuilder sb, IReadOnlyList<G4GrammarAction> parserMembers, G4Grammar grammar, IParserEmbeddedCodeTransformer transformer) =>
        EmitNamedActionRegion(sb, parserMembers, grammar, transformer, ParserMembersDescriptor);

    /// <summary>
    /// Emits parser footer blocks after generated type declarations.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="parserFooters">Parser footer blocks to inject.</param>
    /// <param name="grammar">Parsed grammar AST used for transformer context.</param>
    /// <param name="transformer">Parser embedded-code transformer used for parser footers.</param>
    private static void EmitParserFooters(StringBuilder sb, IReadOnlyList<G4GrammarAction> parserFooters, G4Grammar grammar, IParserEmbeddedCodeTransformer transformer) =>
        EmitNamedActionRegion(sb, parserFooters, grammar, transformer, ParserFooterDescriptor);

    /// <summary>
    /// Emits lexer header blocks before generated type declarations.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="lexerHeaders">Lexer header blocks to inject.</param>
    /// <param name="grammar">Parsed grammar AST used for transformer context.</param>
    /// <param name="transformer">Parser embedded-code transformer used for lexer headers.</param>
    private static void EmitLexerHeaders(StringBuilder sb, IReadOnlyList<G4GrammarAction> lexerHeaders, G4Grammar grammar, IParserEmbeddedCodeTransformer transformer) =>
        EmitNamedActionRegion(sb, lexerHeaders, grammar, transformer, LexerHeaderDescriptor);

    /// <summary>
    /// Emits lexer member blocks into the generated execution context.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="lexerMembers">Lexer member blocks to inject.</param>
    /// <param name="grammar">Parsed grammar AST used for transformer context.</param>
    /// <param name="transformer">Parser embedded-code transformer used for lexer members.</param>
    private static void EmitLexerMembers(StringBuilder sb, IReadOnlyList<G4GrammarAction> lexerMembers, G4Grammar grammar, IParserEmbeddedCodeTransformer transformer) =>
        EmitNamedActionRegion(sb, lexerMembers, grammar, transformer, LexerMembersDescriptor);

    /// <summary>
    /// Emits lexer footer blocks after generated type declarations.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="lexerFooters">Lexer footer blocks to inject.</param>
    /// <param name="grammar">Parsed grammar AST used for transformer context.</param>
    /// <param name="transformer">Parser embedded-code transformer used for lexer footers.</param>
    private static void EmitLexerFooters(StringBuilder sb, IReadOnlyList<G4GrammarAction> lexerFooters, G4Grammar grammar, IParserEmbeddedCodeTransformer transformer) =>
        EmitNamedActionRegion(sb, lexerFooters, grammar, transformer, LexerFooterDescriptor);

    /// <summary>
    /// Selects named actions matching a descriptor while preserving grammar order.
    /// </summary>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <param name="descriptor">Descriptor that identifies the named-action family.</param>
    /// <returns>Matching named actions in grammar order.</returns>
    private static IReadOnlyList<G4GrammarAction> CollectNamedActions(G4Grammar grammar, NamedActionInjectionDescriptor descriptor)
    {
        return grammar.Actions
            .Where(action => descriptor.Selector(grammar, action))
            .ToArray();
    }

    /// <summary>
    /// Transforms and injects a named-action family through the centralized embedded-code boundaries.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="actions">Named-action fragments to emit in their existing order.</param>
    /// <param name="grammar">Parsed grammar AST used for transformer context.</param>
    /// <param name="transformer">Parser embedded-code transformer selected for generation.</param>
    /// <param name="descriptor">Descriptor for the named-action injection point.</param>
    private static void EmitNamedActionRegion(StringBuilder sb, IReadOnlyList<G4GrammarAction> actions, G4Grammar grammar, IParserEmbeddedCodeTransformer transformer, NamedActionInjectionDescriptor descriptor)
    {
        _ = descriptor.DiagnosticTargetName;
        var injector = new CSharpEmbeddedCodeInjector(sb);
        foreach (G4GrammarAction action in actions)
        {
            TransformedEmbeddedCode code = TransformEmbeddedCode(transformer, new RawEmbeddedCode(action.RawCode), descriptor.Location, grammar, null);
            injector.InjectRegion(code, descriptor.Region);
        }
    }
}

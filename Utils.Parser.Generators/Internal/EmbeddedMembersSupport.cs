using System;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Identifies the source-generator support category for one ANTLR grammar-level action.
/// </summary>
internal enum GrammarActionSupportKind
{
    /// <summary>Parser <c>@header</c> or <c>@parser::header</c> action injected near the top of generated C# source.</summary>
    ParserHeader,

    /// <summary>Parser <c>@members</c> or <c>@parser::members</c> action injected into the generated execution context.</summary>
    ParserMembers,

    /// <summary>Parser <c>@footer</c> or <c>@parser::footer</c> action injected as trailing generated C# source.</summary>
    ParserFooter,

    /// <summary>Lexer <c>@lexer::header</c> action injected near the top of generated C# source.</summary>
    LexerHeader,

    /// <summary>Lexer <c>@lexer::members</c> action injected into the generated execution context as a limited compatibility bridge.</summary>
    LexerMembers,

    /// <summary>Lexer <c>@lexer::footer</c> action injected as trailing generated C# source.</summary>
    LexerFooter,

    /// <summary>Parser-scoped compatibility action used in a lexer grammar.</summary>
    UnsupportedParserNamedActionInLexerGrammar,

    /// <summary>Unscoped parser compatibility action used in a lexer grammar.</summary>
    UnsupportedUnscopedParserCompatibilityActionInLexerGrammar,

    /// <summary>Named action that uses an unsupported non-parser scope.</summary>
    UnsupportedUnknownScope,

    /// <summary>Parser-scoped named action whose name is not a supported parser compatibility block.</summary>
    UnsupportedUnknownParserNamedAction,

    /// <summary>Grammar-level action that is preserved as metadata only.</summary>
    UnsupportedMetadataOnly
}

/// <summary>
/// Provides the shared source of truth for ANTLR grammar-level action classification,
/// source-generator injection eligibility, and unsupported diagnostic reasons.
/// </summary>
internal static class EmbeddedMembersSupport
{
    /// <summary>
    /// Classifies a grammar-level action into the exact source-generator support category.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to classify.</param>
    /// <returns>The support category that all emitter and diagnostic paths must use.</returns>
    public static GrammarActionSupportKind Classify(G4Grammar grammar, G4GrammarAction action)
    {
        if (IsInjectableLexerAction(grammar, action, "header"))
        {
            return GrammarActionSupportKind.LexerHeader;
        }

        if (IsInjectableLexerAction(grammar, action, "members"))
        {
            return GrammarActionSupportKind.LexerMembers;
        }

        if (IsInjectableLexerAction(grammar, action, "footer"))
        {
            return GrammarActionSupportKind.LexerFooter;
        }

        if (string.Equals(action.Target, "parser", StringComparison.Ordinal)
            && grammar.Kind is G4GrammarKind.Lexer
            && IsParserCompatibilityActionName(action.Name))
        {
            return GrammarActionSupportKind.UnsupportedParserNamedActionInLexerGrammar;
        }

        if (action.Target is null
            && grammar.Kind is G4GrammarKind.Lexer
            && IsParserCompatibilityActionName(action.Name))
        {
            return GrammarActionSupportKind.UnsupportedUnscopedParserCompatibilityActionInLexerGrammar;
        }

        if (action.Target is not null && !string.Equals(action.Target, "parser", StringComparison.Ordinal))
        {
            return GrammarActionSupportKind.UnsupportedUnknownScope;
        }

        if (IsInjectableParserAction(grammar, action, "header"))
        {
            return GrammarActionSupportKind.ParserHeader;
        }

        if (IsInjectableParserAction(grammar, action, "members"))
        {
            return GrammarActionSupportKind.ParserMembers;
        }

        if (IsInjectableParserAction(grammar, action, "footer"))
        {
            return GrammarActionSupportKind.ParserFooter;
        }

        if (string.Equals(action.Target, "parser", StringComparison.Ordinal))
        {
            return GrammarActionSupportKind.UnsupportedUnknownParserNamedAction;
        }

        return GrammarActionSupportKind.UnsupportedMetadataOnly;
    }

    /// <summary>
    /// Determines whether a grammar-level action is a parser members block supported
    /// by the generated per-parse execution-context compatibility bridge.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to classify.</param>
    /// <returns>
    /// <c>true</c> for unscoped <c>@members</c> in parser or combined grammars, and
    /// for <c>@parser::members</c> in parser or combined grammars; otherwise <c>false</c>.
    /// </returns>
    public static bool IsInjectableParserMembersAction(G4Grammar grammar, G4GrammarAction action)
    {
        return Classify(grammar, action) is GrammarActionSupportKind.ParserMembers;
    }

    /// <summary>
    /// Determines whether a grammar-level action is a parser header block supported
    /// by the generated C# source-file compatibility bridge.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to classify.</param>
    /// <returns>
    /// <c>true</c> for unscoped <c>@header</c> in parser or combined grammars, and
    /// for <c>@parser::header</c> in parser or combined grammars; otherwise <c>false</c>.
    /// </returns>
    public static bool IsInjectableParserHeaderAction(G4Grammar grammar, G4GrammarAction action)
    {
        return Classify(grammar, action) is GrammarActionSupportKind.ParserHeader;
    }

    /// <summary>
    /// Determines whether a grammar-level action is a parser footer block supported
    /// by the generated C# trailing source-file compatibility bridge.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to classify.</param>
    /// <returns>
    /// <c>true</c> for unscoped <c>@footer</c> in parser or combined grammars, and
    /// for <c>@parser::footer</c> in parser or combined grammars; otherwise <c>false</c>.
    /// </returns>
    public static bool IsInjectableParserFooterAction(G4Grammar grammar, G4GrammarAction action)
    {
        return Classify(grammar, action) is GrammarActionSupportKind.ParserFooter;
    }

    /// <summary>
    /// Determines whether a grammar-level action is a lexer header block supported by the generated C# source-file compatibility bridge.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to classify.</param>
    /// <returns><c>true</c> for <c>@lexer::header</c> in combined or lexer grammars; otherwise <c>false</c>.</returns>
    public static bool IsInjectableLexerHeaderAction(G4Grammar grammar, G4GrammarAction action)
    {
        return Classify(grammar, action) is GrammarActionSupportKind.LexerHeader;
    }

    /// <summary>
    /// Determines whether a grammar-level action is a lexer members block supported by the generated execution-context compatibility bridge.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to classify.</param>
    /// <returns><c>true</c> for <c>@lexer::members</c> in combined or lexer grammars; otherwise <c>false</c>.</returns>
    public static bool IsInjectableLexerMembersAction(G4Grammar grammar, G4GrammarAction action)
    {
        return Classify(grammar, action) is GrammarActionSupportKind.LexerMembers;
    }

    /// <summary>
    /// Determines whether a grammar-level action is a lexer footer block supported by the generated C# trailing source-file compatibility bridge.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to classify.</param>
    /// <returns><c>true</c> for <c>@lexer::footer</c> in combined or lexer grammars; otherwise <c>false</c>.</returns>
    public static bool IsInjectableLexerFooterAction(G4Grammar grammar, G4GrammarAction action)
    {
        return Classify(grammar, action) is GrammarActionSupportKind.LexerFooter;
    }

    /// <summary>
    /// Formats the deterministic unsupported diagnostic reason for a grammar-level action.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to classify.</param>
    /// <returns>Construct-specific diagnostic reason matching the classified support category.</returns>
    public static string FormatUnsupportedReason(G4Grammar grammar, G4GrammarAction action)
    {
        return Classify(grammar, action) switch
        {
            GrammarActionSupportKind.UnsupportedParserNamedActionInLexerGrammar => $"Parser named action '@parser::{action.Name}' is not valid in a lexer grammar.",
            GrammarActionSupportKind.UnsupportedUnscopedParserCompatibilityActionInLexerGrammar => $"Unscoped grammar action '@{action.Name}' is not supported in lexer grammars by this generator.",
            GrammarActionSupportKind.UnsupportedUnknownScope => $"Named action scope '@{action.Target}::{action.Name}' is not supported by this generator.",
            GrammarActionSupportKind.UnsupportedUnknownParserNamedAction => $"Parser named action '@parser::{action.Name}' is not supported by this generator.",
            _ => FormatMetadataOnlyReason(action)
        };
    }

    /// <summary>
    /// Determines whether a grammar-level action with the supplied name targets generated parser C# output.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to classify.</param>
    /// <param name="name">ANTLR grammar-level action name to match.</param>
    /// <returns><c>true</c> when the action is unscoped parser/combined grammar content or scoped <c>@parser::</c> content outside lexer grammars.</returns>
    private static bool IsInjectableParserAction(G4Grammar grammar, G4GrammarAction action, string name)
    {
        if (!string.Equals(action.Name, name, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(action.Target, "parser", StringComparison.Ordinal))
        {
            return grammar.Kind is not G4GrammarKind.Lexer;
        }

        if (action.Target is null)
        {
            return grammar.Kind is G4GrammarKind.Parser or G4GrammarKind.Combined;
        }

        return false;
    }

    /// <summary>
    /// Determines whether a grammar-level action is one of the explicitly supported lexer named actions.
    /// </summary>
    /// <param name="grammar">Grammar that owns the action.</param>
    /// <param name="action">Grammar-level action metadata to inspect.</param>
    /// <param name="name">ANTLR grammar-level lexer action name to match.</param>
    /// <returns><c>true</c> for the matching <c>@lexer::</c> compatibility action in combined or lexer grammars.</returns>
    private static bool IsInjectableLexerAction(G4Grammar grammar, G4GrammarAction action, string name)
    {
        return grammar.Kind is G4GrammarKind.Combined or G4GrammarKind.Lexer
            && string.Equals(action.Target, "lexer", StringComparison.Ordinal)
            && string.Equals(action.Name, name, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether a grammar-level action name is one of the parser compatibility block names.
    /// </summary>
    /// <param name="name">ANTLR grammar-level action name.</param>
    /// <returns><see langword="true"/> for header, members, or footer action names.</returns>
    private static bool IsParserCompatibilityActionName(string name)
    {
        return string.Equals(name, "header", StringComparison.Ordinal)
            || string.Equals(name, "members", StringComparison.Ordinal)
            || string.Equals(name, "footer", StringComparison.Ordinal);
    }

    /// <summary>
    /// Formats the existing metadata-only diagnostic reason for an unsupported unscoped grammar action.
    /// </summary>
    /// <param name="action">Grammar-level action metadata to describe.</param>
    /// <returns>Metadata-only unsupported diagnostic reason.</returns>
    private static string FormatMetadataOnlyReason(G4GrammarAction action)
    {
        if (string.Equals(action.Name, "header", StringComparison.Ordinal))
        {
            return "This header action is not a parser @header block supported by generated C# source-file injection and remains metadata-only.";
        }

        if (string.Equals(action.Name, "members", StringComparison.Ordinal))
        {
            return "This members action is not a parser @members block supported by the generated execution context and remains metadata-only.";
        }

        if (string.Equals(action.Name, "footer", StringComparison.Ordinal))
        {
            return "This footer action is not a parser @footer block supported by generated trailing C# source injection and remains metadata-only.";
        }

        return "Grammar-level actions are preserved as metadata only and are not injected into generated parser or lexer types.";
    }
}

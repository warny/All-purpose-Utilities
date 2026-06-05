using System;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Provides shared source-generator rules for deciding whether ANTLR grammar-level actions
/// are parser compatibility blocks that can be injected into generated C# output.
/// </summary>
internal static class EmbeddedMembersSupport
{
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
        return IsInjectableParserAction(grammar, action, "members");
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
        return IsInjectableParserAction(grammar, action, "header");
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
}

using System;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Provides shared source-generator rules for deciding whether ANTLR members blocks
/// are parser members that can be injected into the generated execution context.
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
        if (!string.Equals(action.Name, "members", StringComparison.Ordinal))
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

using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Parser.Antlr4.Common;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Maps generator-side ANTLR4 prequel metadata to the shared prequel model.
/// </summary>
internal static class Antlr4GeneratorPrequelMapper
{
    /// <summary>
    /// Converts generator grammar prequel metadata into the shared model.
    /// </summary>
    /// <param name="grammar">Generator grammar AST root.</param>
    /// <returns>Shared ANTLR4 prequel metadata model.</returns>
    public static Antlr4PrequelModel Map(G4Grammar grammar)
    {
        var imports = grammar.Imports
            .Select(static importInfo => new Antlr4ImportInfo(importInfo.GrammarName, importInfo.Alias))
            .ToList();

        var actions = grammar.Actions
            .Select(static actionInfo => new Antlr4ActionInfo(actionInfo.Name, actionInfo.RawCode, actionInfo.Target))
            .ToList();

        var channels = new HashSet<string>(grammar.DeclaredChannels, StringComparer.Ordinal)
        {
            "DEFAULT_CHANNEL",
            "HIDDEN",
        };

        return new Antlr4PrequelModel(
            grammar.Options.Count == 0 ? null : new Antlr4OptionSet(grammar.Options),
            imports,
            actions,
            Antlr4NameSet.Create(grammar.DeclaredTokens),
            Antlr4NameSet.Create(channels));
    }
}

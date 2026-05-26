using Utils.Parser.Model;

namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Maps runtime-side ANTLR4 conversion metadata to the shared prequel model.
/// </summary>
internal static class Antlr4RuntimePrequelMapper
{
    /// <summary>
    /// Converts runtime parser definition prequel metadata into the shared model.
    /// </summary>
    /// <param name="definition">Runtime parser definition.</param>
    /// <returns>Shared ANTLR4 prequel metadata model.</returns>
    public static Antlr4PrequelModel Map(ParserDefinition definition)
    {
        Antlr4OptionSet? options = definition.Options is null
            ? null
            : new Antlr4OptionSet(definition.Options.Values);

        var imports = definition.Imports
            .Select(static import => new Antlr4ImportInfo(import.GrammarName, import.Alias))
            .ToList();

        var actions = definition.Actions
            .Select(static action => new Antlr4ActionInfo(action.Name, action.RawCode, action.Target))
            .ToList();

        return new Antlr4PrequelModel(
            options,
            imports,
            actions,
            Antlr4NameSet.Create(definition.DeclaredTokens),
            Antlr4NameSet.Create(definition.DeclaredChannels));
    }
}

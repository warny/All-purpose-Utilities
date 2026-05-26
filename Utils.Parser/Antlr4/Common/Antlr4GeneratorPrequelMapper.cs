namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Maps generator-side ANTLR4 prequel metadata payloads to the shared prequel model.
/// </summary>
internal static class Antlr4GeneratorPrequelMapper
{
    /// <summary>
    /// Converts generator-collected prequel metadata into the shared model.
    /// </summary>
    /// <param name="options">Generator options map.</param>
    /// <param name="imports">Generator imports in declaration order.</param>
    /// <param name="actions">Generator grammar actions in declaration order.</param>
    /// <param name="declaredTokens">Generator declared token names.</param>
    /// <param name="declaredChannels">Generator declared channel names.</param>
    /// <param name="includeDefaultChannels">Whether to include <c>DEFAULT_CHANNEL</c> and <c>HIDDEN</c>.</param>
    /// <returns>Shared ANTLR4 prequel metadata model.</returns>
    public static Antlr4PrequelModel Map(
        IReadOnlyDictionary<string, string> options,
        IReadOnlyList<Antlr4ImportInfo> imports,
        IReadOnlyList<Antlr4ActionInfo> actions,
        IEnumerable<string> declaredTokens,
        IEnumerable<string> declaredChannels,
        bool includeDefaultChannels)
    {
        var normalizedChannels = includeDefaultChannels
            ? declaredChannels.Concat(["DEFAULT_CHANNEL", "HIDDEN"])
            : declaredChannels;

        return new Antlr4PrequelModel(
            options.Count == 0 ? null : new Antlr4OptionSet(options),
            imports,
            actions,
            Antlr4NameSet.Create(declaredTokens),
            Antlr4NameSet.Create(normalizedChannels));
    }
}

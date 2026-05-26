namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Maps generator-side ANTLR4 prequel metadata payloads to the shared prequel model.
/// </summary>
internal static class Antlr4GeneratorPrequelMapper
{
    /// <summary>
    /// Converts generator-collected prequel metadata into the shared model.
    /// </summary>
    /// <typeparam name="TImport">Generator import metadata type.</typeparam>
    /// <typeparam name="TAction">Generator action metadata type.</typeparam>
    /// <param name="options">Generator options map.</param>
    /// <param name="imports">Generator imports in declaration order.</param>
    /// <param name="actions">Generator grammar actions in declaration order.</param>
    /// <param name="declaredTokens">Generator declared token names.</param>
    /// <param name="declaredChannels">Generator declared channel names.</param>
    /// <param name="includeDefaultChannels">Whether to include <c>DEFAULT_CHANNEL</c> and <c>HIDDEN</c>.</param>
    /// <param name="grammarNameSelector">Selects grammar name from import metadata.</param>
    /// <param name="aliasSelector">Selects alias from import metadata.</param>
    /// <param name="actionNameSelector">Selects action name.</param>
    /// <param name="actionCodeSelector">Selects action raw code.</param>
    /// <param name="actionTargetSelector">Selects action target scope.</param>
    /// <returns>Shared ANTLR4 prequel metadata model.</returns>
    public static Antlr4PrequelModel Map<TImport, TAction>(
        IReadOnlyDictionary<string, string> options,
        IReadOnlyList<TImport> imports,
        IReadOnlyList<TAction> actions,
        IEnumerable<string> declaredTokens,
        IEnumerable<string> declaredChannels,
        bool includeDefaultChannels,
        Func<TImport, string> grammarNameSelector,
        Func<TImport, string?> aliasSelector,
        Func<TAction, string> actionNameSelector,
        Func<TAction, string> actionCodeSelector,
        Func<TAction, string?> actionTargetSelector)
    {
        var normalizedChannels = includeDefaultChannels
            ? declaredChannels.Concat(["DEFAULT_CHANNEL", "HIDDEN"])
            : declaredChannels;

        return new Antlr4PrequelModel(
            options.Count == 0 ? null : new Antlr4OptionSet(options),
            imports.Select(import => new Antlr4ImportInfo(grammarNameSelector(import), aliasSelector(import))).ToList(),
            actions.Select(action => new Antlr4ActionInfo(actionNameSelector(action), actionCodeSelector(action), actionTargetSelector(action))).ToList(),
            Antlr4NameSet.Create(declaredTokens),
            Antlr4NameSet.Create(normalizedChannels));
    }
}

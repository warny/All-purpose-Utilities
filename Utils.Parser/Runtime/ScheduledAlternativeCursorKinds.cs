namespace Utils.Parser.Runtime;

/// <summary>
/// Defines scheduled alternative cursor identity values used by parser look-ahead observations.
/// </summary>
internal static class ScheduledAlternativeCursorKinds
{
    public const string RuleRoot = "rule-root";

    public const string LeftRecursiveSeed = "left-recursive-seed";

    public const string Alternation = "alternation";

    public const string AlternativeRoot = "alternative-root";

    /// <summary>
    /// Returns <c>true</c> when the cursor kind supports conservative negative look-ahead shortcut reuse.
    /// </summary>
    /// <param name="cursorKind">Scheduled alternative cursor identity.</param>
    public static bool AllowsNegativeLookaheadShortcut(string cursorKind)
    {
        return cursorKind == RuleRoot
            || cursorKind == LeftRecursiveSeed;
    }
}

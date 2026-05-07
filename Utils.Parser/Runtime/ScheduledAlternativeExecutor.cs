using Utils.Parser.Diagnostics;
using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Executes a single scheduled alternative attempt while keeping parser semantics
/// in runtime components owned by <see cref="ParserEngine"/>.
/// </summary>
internal sealed class ScheduledAlternativeExecutor
{
    private readonly ParserStateRegistry _stateRegistry;
    private readonly ParserLookaheadCache _lookaheadCache;

    /// <summary>
    /// Creates an executor bound to parser runtime registries.
    /// </summary>
    public ScheduledAlternativeExecutor(ParserStateRegistry stateRegistry, ParserLookaheadCache lookaheadCache)
    {
        _stateRegistry = stateRegistry;
        _lookaheadCache = lookaheadCache;
    }

    /// <summary>
    /// Executes one alternative from the scheduler and returns a completed parse state when successful.
    /// </summary>
    public ActiveParseState? Execute(
        ParseContext context,
        Rule rule,
        Alternative alternative,
        int alternativeIndex,
        int startPosition,
        int precedence,
        string cursorKind,
        int cursorIndex,
        DiagnosticBag? diagnostics,
        Func<Alternative, bool> checkPrecedence,
        Func<RuleContent, bool> containsPredicateOrAction,
        Func<ParseContext, (int? Start, int? Length)> resolveDiagnosticSpan,
        Func<Alternative, ParseNode?> parseAlternative)
    {
        var stateKey = new ParserStateKey(rule.Name, startPosition, alternativeIndex, alternativeIndex, precedence);
        _stateRegistry.TryEnterState(stateKey);

        if (!checkPrecedence(alternative))
        {
            return null;
        }

        var lookaheadKey = new ParserLookaheadKey(rule.Name, startPosition, alternativeIndex, precedence, cursorKind, cursorIndex);
        var allowNegativeShortcut =
            diagnostics is null
            && ScheduledAlternativeCursorKinds.AllowsNegativeLookaheadShortcut(cursorKind);
        var token = context.Peek();
        if (allowNegativeShortcut
            && _lookaheadCache.TryGet(lookaheadKey, out var cachedLookahead)
            && cachedLookahead.Kind == ParserLookaheadProbeKind.ImmediateReject)
        {
            return null;
        }

        var savedPosition = context.SavePosition();
        var result = parseAlternative(alternative);
        if (result is null)
        {
            var consumed = context.Position > savedPosition;
            if (allowNegativeShortcut
                && !consumed
                && !containsPredicateOrAction(alternative.Content))
            {
                _lookaheadCache.TryAdd(
                    lookaheadKey,
                    new ParserLookaheadProbeResult(
                        ParserLookaheadProbeKind.ImmediateReject,
                        token?.RuleName,
                        token?.Text));
            }

            var diagnosticSpan = resolveDiagnosticSpan(context);
            diagnostics?.AddWithContext(ParserDiagnostics.BacktrackingUsed, diagnosticSpan.Start, diagnosticSpan.Length, rule.Name, null, rule.Name);
            context.RestorePosition(savedPosition);
            return null;
        }

        _lookaheadCache.TryAdd(
            lookaheadKey,
            new ParserLookaheadProbeResult(
                ParserLookaheadProbeKind.RequiresParse,
                token?.RuleName,
                token?.Text));

        var state = new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = startPosition,
            CurrentInputPosition = context.Position,
            AlternativeIndex = alternativeIndex,
            Cursor = new RuleContentCursor { Index = 0, Kind = "alternative-root" },
            PartialNode = result,
            EndPosition = context.Position,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };

        context.RestorePosition(savedPosition);
        return state;
    }
}

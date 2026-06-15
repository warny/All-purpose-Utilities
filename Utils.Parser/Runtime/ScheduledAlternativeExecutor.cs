using Utils.Parser.Diagnostics;
using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Executes a single scheduled alternative attempt while keeping parser semantics
/// in runtime components owned by <see cref="ParserEngine"/>.
/// This executor is local and non-authoritative: it does not provide global parse authority,
/// replay semantics beyond parser attempt-boundary state transport.
/// Look-ahead data (live probe or cached result) is advisory orchestration metadata:
/// it can only support conservative shortcut rejection paths and never directly accept a branch.
/// It may propagate branch-local diagnostics through callback outputs, but final diagnostic authority
/// remains in <see cref="ParserEngine"/>.
/// Its role is bounded coordination between <see cref="AlternativeScheduler"/>,
/// <see cref="ParserStateRegistry"/>, and engine-owned parsing callbacks.
/// Shared-prefix metadata observed during execution is non-authoritative and never enables replay,
/// continuation resume, shared-frame execution, or branch merging.
/// </summary>
internal sealed class ScheduledAlternativeExecutor
{
    /// <summary>Registry for tracking visited states and invocation-local completion results.</summary>
    private readonly ParserStateRegistry _stateRegistry;
    /// <summary>Cache of prior look-ahead probe outcomes reused to skip redundant probing.</summary>
    private readonly ParserLookaheadCache _lookaheadCache;
    /// <summary>Probe used to evaluate shallow first-token look-ahead for an alternative.</summary>
    private readonly ParserLookaheadProbe _lookaheadProbe;

    /// <summary>
    /// Creates an executor bound to parser runtime registries.
    /// </summary>
    public ScheduledAlternativeExecutor(ParserStateRegistry stateRegistry, ParserLookaheadCache lookaheadCache, ParserLookaheadProbe lookaheadProbe)
    {
        _stateRegistry = stateRegistry;
        _lookaheadCache = lookaheadCache;
        _lookaheadProbe = lookaheadProbe;
    }

    /// <summary>
    /// Executes one alternative from the scheduler and returns a completed parse state when successful.
    /// Returned completion/failure is local branch outcome transport, not global parse authority.
    /// Embedded parser actions may run during this attempt even when the branch is later rejected.
    /// Failed parser backtracking attempt boundaries restore parser execution state through the configured state manager.
    /// Successful attempts carry their resulting state for later winner commit, without action replay or buffering.
    /// </summary>
    public ScheduledAlternativeExecutionResult Execute(
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
        Func<string, Rule?> resolveRule,
        bool caseInsensitive,
        Func<RuleContent, bool> containsPredicateOrAction,
        Func<ParseContext, (int? Start, int? Length)> resolveDiagnosticSpan,
        Func<ParseContext, ParserAttemptSnapshot> captureAttempt,
        Action<ParseContext, ParserAttemptSnapshot> restoreAttempt,
        Func<Alternative, ParseNode?> parseAlternative)
    {
        var stateKey = new ParserStateKey(rule.Name, startPosition, alternativeIndex, alternativeIndex, precedence);
        _stateRegistry.TryEnterState(stateKey);

        if (!checkPrecedence(alternative))
        {
            return new ScheduledAlternativeExecutionResult(null, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null));
        }

        var lookaheadKey = new ParserLookaheadKey(rule.Name, startPosition, alternativeIndex, precedence, cursorKind, cursorIndex);
        var allowNegativeShortcut =
            diagnostics is null
            && ScheduledAlternativeCursorKinds.AllowsNegativeLookaheadShortcut(cursorKind);
        var token = context.Peek();

        var hasCached = _lookaheadCache.TryGet(lookaheadKey, out var cachedLookahead);

        ParserLookaheadProbeResult effectiveLookahead;
        if (hasCached)
        {
            effectiveLookahead = cachedLookahead;
        }
        else
        {
            effectiveLookahead = _lookaheadProbe.Probe(alternative, token, resolveRule, caseInsensitive);
            if (effectiveLookahead.Kind != ParserLookaheadProbeKind.Unknown)
            {
                _lookaheadCache.TryAdd(lookaheadKey, effectiveLookahead);
            }
        }

        // Authoritative shortcut boundary:
        // look-ahead can reject immediately when deterministic first-token mismatch is observed,
        // but it cannot accept a branch. Any non-reject outcome requires real parsing.
        if (allowNegativeShortcut
            && effectiveLookahead.Kind == ParserLookaheadProbeKind.ImmediateReject)
        {
            return new ScheduledAlternativeExecutionResult(null, effectiveLookahead);
        }

        var attemptSnapshot = captureAttempt(context);
        var result = parseAlternative(alternative);
        if (result is null)
        {
            var consumed = context.Position > attemptSnapshot.InputPosition;
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
            restoreAttempt(context, attemptSnapshot);
            return new ScheduledAlternativeExecutionResult(null, effectiveLookahead);
        }

        var completedAttemptSnapshot = captureAttempt(context);

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
            Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
            PartialNode = result,
            EndPosition = context.Position,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null,
            ExecutionStateSnapshot = completedAttemptSnapshot.ExecutionStateSnapshot
        };

        restoreAttempt(context, attemptSnapshot);
        return new ScheduledAlternativeExecutionResult(state, effectiveLookahead);
    }
}

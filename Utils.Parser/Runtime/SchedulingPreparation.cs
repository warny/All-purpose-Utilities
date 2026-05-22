using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Composes deterministic preparation outputs consumed by the scheduler.
/// This component assembles metadata only and does not execute parsing or scheduling.
/// </summary>
internal sealed class SchedulingPreparation
{
    private readonly AlternativeStructuralPrefixExtractor _structuralPrefixExtractor = new();
    private readonly ParserLookaheadSharedPrefixDetector _sharedPrefixDetector = new();
    private readonly ContinuationMetadataPreparation _continuationMetadataPreparation = new();

    /// <summary>
    /// Prepares scheduler inputs from grammar alternatives and local parse context.
    /// </summary>
    /// <param name="rule">Owning rule for the alternatives being prepared.</param>
    /// <param name="orderedAlternatives">Alternatives ordered by priority for deterministic preparation.</param>
    /// <param name="context">Minimal context carrying the parse cursor and scheduling metadata keys.</param>
    /// <param name="checkPrecedence">Predicate used to filter alternatives by precedence.</param>
    /// <param name="lookaheadCache">Look-ahead cache used for conservative probe reuse.</param>
    /// <param name="lookaheadProbe">Look-ahead probe implementation used for shallow probing.</param>
    /// <param name="resolveRule">Rule resolver used by look-ahead probing.</param>
    /// <param name="caseInsensitive">Whether token matching is case-insensitive.</param>
    /// <returns>Immutable aggregate of precomputed scheduler inputs.</returns>
    public PreparedSchedulingInputs Prepare(
        Rule rule,
        IReadOnlyList<Alternative> orderedAlternatives,
        SchedulingPreparationContext context,
        Func<Alternative, bool> checkPrecedence,
        ParserLookaheadCache lookaheadCache,
        ParserLookaheadProbe lookaheadProbe,
        Func<string, Rule?> resolveRule,
        bool caseInsensitive)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(orderedAlternatives);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(checkPrecedence);
        ArgumentNullException.ThrowIfNull(lookaheadCache);
        ArgumentNullException.ThrowIfNull(lookaheadProbe);
        ArgumentNullException.ThrowIfNull(resolveRule);

        var structuralDescriptors = _structuralPrefixExtractor.ExtractAll(orderedAlternatives);
        var precomputedLookaheadProbes = PrepareSchedulingLookaheadProbes(
            rule,
            orderedAlternatives,
            context,
            checkPrecedence,
            lookaheadCache,
            lookaheadProbe,
            resolveRule,
            caseInsensitive);
        var sharedPrefixCandidates = _sharedPrefixDetector.Detect(precomputedLookaheadProbes);
        var continuationDescriptors = _continuationMetadataPreparation.Prepare(rule, orderedAlternatives, precomputedLookaheadProbes, sharedPrefixCandidates);

        return new PreparedSchedulingInputs(structuralDescriptors, precomputedLookaheadProbes, sharedPrefixCandidates, continuationDescriptors);
    }

    /// <summary>
    /// Prepares look-ahead probes for scheduling metadata using the same precedence and cache policy as scheduled execution.
    /// </summary>
    private static IReadOnlyList<ParserLookaheadProbeResult> PrepareSchedulingLookaheadProbes(
        Rule rule,
        IReadOnlyList<Alternative> orderedAlternatives,
        SchedulingPreparationContext context,
        Func<Alternative, bool> checkPrecedence,
        ParserLookaheadCache lookaheadCache,
        ParserLookaheadProbe lookaheadProbe,
        Func<string, Rule?> resolveRule,
        bool caseInsensitive)
    {
        var probes = new ParserLookaheadProbeResult[orderedAlternatives.Count];
        var token = context.ParseContext.Peek();

        for (var index = 0; index < orderedAlternatives.Count; index++)
        {
            var alternative = orderedAlternatives[index];
            if (!checkPrecedence(alternative))
            {
                probes[index] = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null);
                continue;
            }

            var lookaheadKey = new ParserLookaheadKey(rule.Name, context.StartPosition, index, context.Precedence, context.CursorKind, context.CursorIndex);
            if (lookaheadCache.TryGet(lookaheadKey, out var cachedProbe))
            {
                probes[index] = cachedProbe;
                continue;
            }

            var probe = lookaheadProbe.Probe(alternative, token, resolveRule, caseInsensitive);
            probes[index] = probe;
            if (probe.Kind != ParserLookaheadProbeKind.Unknown)
            {
                lookaheadCache.TryAdd(lookaheadKey, probe);
            }
        }

        return probes;
    }
}

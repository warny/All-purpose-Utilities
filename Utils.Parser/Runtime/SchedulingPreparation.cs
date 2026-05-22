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
    private readonly ParserLookaheadProbe _lookaheadProbe;
    private readonly ParserLookaheadCache _lookaheadCache;
    private readonly Func<string, Rule?> _resolveRule;

    /// <summary>
    /// Initializes scheduling preparation with runtime look-ahead dependencies.
    /// </summary>
    /// <param name="lookaheadProbe">Look-ahead probe implementation used for shallow probing.</param>
    /// <param name="lookaheadCache">Look-ahead cache used for conservative probe reuse.</param>
    /// <param name="resolveRule">Rule resolver used by look-ahead probing.</param>
    public SchedulingPreparation(
        ParserLookaheadProbe lookaheadProbe,
        ParserLookaheadCache lookaheadCache,
        Func<string, Rule?> resolveRule)
    {
        _lookaheadProbe = lookaheadProbe ?? throw new ArgumentNullException(nameof(lookaheadProbe));
        _lookaheadCache = lookaheadCache ?? throw new ArgumentNullException(nameof(lookaheadCache));
        _resolveRule = resolveRule ?? throw new ArgumentNullException(nameof(resolveRule));
    }

    /// <summary>
    /// Prepares scheduler inputs from grammar alternatives and local parse context.
    /// </summary>
    /// <param name="rule">Owning rule for the alternatives being prepared.</param>
    /// <param name="orderedAlternatives">Alternatives ordered by priority for deterministic preparation.</param>
    /// <param name="context">Minimal context carrying parse cursor and scheduling metadata keys.</param>
    /// <returns>Immutable aggregate of precomputed scheduler inputs.</returns>
    public PreparedSchedulingInputs Prepare(
        Rule rule,
        IReadOnlyList<Alternative> orderedAlternatives,
        SchedulingPreparationContext context)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(orderedAlternatives);
        ArgumentNullException.ThrowIfNull(context);

        var structuralDescriptors = _structuralPrefixExtractor.ExtractAll(orderedAlternatives);
        var precomputedLookaheadProbes = PrepareSchedulingLookaheadProbes(rule, orderedAlternatives, context);
        var sharedPrefixCandidates = _sharedPrefixDetector.Detect(precomputedLookaheadProbes);
        var continuationDescriptors = _continuationMetadataPreparation.Prepare(rule, orderedAlternatives, precomputedLookaheadProbes, sharedPrefixCandidates);

        return new PreparedSchedulingInputs(structuralDescriptors, precomputedLookaheadProbes, sharedPrefixCandidates, continuationDescriptors);
    }

    /// <summary>
    /// Prepares look-ahead probes for scheduling metadata using the same precedence and cache policy as scheduled execution.
    /// </summary>
    private IReadOnlyList<ParserLookaheadProbeResult> PrepareSchedulingLookaheadProbes(
        Rule rule,
        IReadOnlyList<Alternative> orderedAlternatives,
        SchedulingPreparationContext context)
    {
        var probes = new ParserLookaheadProbeResult[orderedAlternatives.Count];
        var token = context.ParseContext.Peek();

        for (var index = 0; index < orderedAlternatives.Count; index++)
        {
            var alternative = orderedAlternatives[index];
            if (!CheckPrecedence(alternative, context.Precedence))
            {
                probes[index] = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null);
                continue;
            }

            var lookaheadKey = new ParserLookaheadKey(rule.Name, context.StartPosition, index, context.Precedence, context.CursorKind, context.CursorIndex);
            if (_lookaheadCache.TryGet(lookaheadKey, out var cachedProbe))
            {
                probes[index] = cachedProbe;
                continue;
            }

            var probe = _lookaheadProbe.Probe(alternative, token, _resolveRule, context.CaseInsensitive);
            probes[index] = probe;
            if (probe.Kind != ParserLookaheadProbeKind.Unknown)
            {
                _lookaheadCache.TryAdd(lookaheadKey, probe);
            }
        }

        return probes;
    }

    private static bool CheckPrecedence(Alternative alt, int currentPrecedence)
    {
        var predLevel = FindPrecedenceLevel(alt.Content);
        if (predLevel is null)
        {
            return true;
        }

        return predLevel.Value >= currentPrecedence;
    }

    private static int? FindPrecedenceLevel(RuleContent content)
    {
        return content switch
        {
            PrecedencePredicate p => p.Level,
            Sequence s => s.Items.OfType<PrecedencePredicate>().Select(static p => (int?)p.Level).FirstOrDefault(),
            _ => null
        };
    }
}

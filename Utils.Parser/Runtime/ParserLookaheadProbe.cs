using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Performs conservative first-token look-ahead probing for a scheduled alternative.
/// The probe is intentionally shallow: it must not evaluate semantic predicates, execute parser actions,
/// or consult external semantic runtime state.
/// It is syntax-oriented only and does not execute parser rules speculatively,
/// traverse parser graphs semantically, or run continuation metadata.
/// Probe results are advisory metadata consumed by orchestration components.
/// This probe can only provide deterministic immediate-reject evidence; all non-reject outcomes
/// remain parse-required and must be confirmed by authoritative parser execution.
/// </summary>
internal sealed class ParserLookaheadProbe
{
    /// <summary>
    /// Probes whether an alternative can be rejected immediately or still requires parsing.
    /// </summary>
    public ParserLookaheadProbeResult Probe(
        Alternative alternative,
        Token? token,
        Func<string, Rule?> resolveRule,
        bool caseInsensitive)
    {
        return ProbeContent(alternative.Content, token, resolveRule, caseInsensitive);
    }

    /// <summary>
    /// Probes a rule-content node using only deterministic, first-token-safe checks.
    /// </summary>
    private static ParserLookaheadProbeResult ProbeContent(
        RuleContent content,
        Token? token,
        Func<string, Rule?> resolveRule,
        bool caseInsensitive)
    {
        var expectedTokenNames = TryGetExpectedTokenNames(content, resolveRule);
        return content switch
        {
            LiteralMatch literal => ProbeLiteralMatch(literal, token, caseInsensitive, expectedTokenNames),
            RuleRef ruleRef => ProbeRuleReference(ruleRef, token, resolveRule, expectedTokenNames),
            Sequence sequence => ProbeSequence(sequence, token, resolveRule, caseInsensitive, expectedTokenNames),
            Quantifier quantifier => ProbeQuantifier(quantifier, token, expectedTokenNames),
            _ => Unknown(token, expectedTokenNames)
        };
    }

    /// <summary>
    /// Probes a literal first item against the current token text.
    /// </summary>
    private static ParserLookaheadProbeResult ProbeLiteralMatch(
        LiteralMatch literal,
        Token? token,
        bool caseInsensitive,
        IReadOnlyList<string>? expectedTokenNames)
    {
        if (token is null)
        {
            return ImmediateReject(token, expectedTokenNames);
        }

        var comparison = caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(token.Text, literal.Value, comparison)
            ? RequiresParse(token, expectedTokenNames)
            : ImmediateReject(token, expectedTokenNames);
    }

    /// <summary>
    /// Probes a rule reference when the target is a lexer rule.
    /// Parser-rule references always return Unknown to preserve conservative fallback-to-parse behavior:
    /// parser rules may accept empty input and may depend on deeper structure that shallow probing cannot prove.
    /// </summary>
    private static ParserLookaheadProbeResult ProbeRuleReference(
        RuleRef ruleRef,
        Token? token,
        Func<string, Rule?> resolveRule,
        IReadOnlyList<string>? expectedTokenNames)
    {
        var resolved = resolveRule(ruleRef.RuleName);
        if (resolved is null || resolved.Kind != RuleKind.Lexer)
        {
            return Unknown(token, expectedTokenNames);
        }

        if (token is null)
        {
            return ImmediateReject(token, expectedTokenNames);
        }

        return string.Equals(token.RuleName, ruleRef.RuleName, StringComparison.Ordinal)
            ? RequiresParse(token, expectedTokenNames)
            : ImmediateReject(token, expectedTokenNames);
    }

    /// <summary>
    /// Probes sequence items while skipping non-semantic runtime directives.
    /// </summary>
    private static ParserLookaheadProbeResult ProbeSequence(
        Sequence sequence,
        Token? token,
        Func<string, Rule?> resolveRule,
        bool caseInsensitive,
        IReadOnlyList<string>? expectedTokenNames)
    {
        var meaningfulItems = sequence.Items
            .Where(static item => item is not EmbeddedAction and not LexerCommand)
            .ToArray();

        if (meaningfulItems.Length == 0)
        {
            return EpsilonPossible(token, expectedTokenNames);
        }

        var encounteredEpsilonPossible = false;

        for (var i = 0; i < meaningfulItems.Length; i++)
        {
            var itemProbe = ProbeContent(meaningfulItems[i], token, resolveRule, caseInsensitive);
            switch (itemProbe.Kind)
            {
                case ParserLookaheadProbeKind.ImmediateReject:
                case ParserLookaheadProbeKind.RequiresParse:
                    return encounteredEpsilonPossible ? Unknown(token, expectedTokenNames) : itemProbe;
                case ParserLookaheadProbeKind.EpsilonPossible:
                    encounteredEpsilonPossible = true;
                    continue;
                case ParserLookaheadProbeKind.Unknown:
                default:
                    return itemProbe.ExpectedTokenNames is null ? Unknown(token, expectedTokenNames) : itemProbe;
            }
        }

        return EpsilonPossible(token, expectedTokenNames);
    }

    /// <summary>
    /// Probes quantifiers conservatively for local epsilon capability only.
    /// </summary>
    private static ParserLookaheadProbeResult ProbeQuantifier(
        Quantifier quantifier,
        Token? token,
        IReadOnlyList<string>? expectedTokenNames)
    {
        return quantifier.Min == 0
            ? EpsilonPossible(token, expectedTokenNames)
            : Unknown(token, expectedTokenNames);
    }

    /// <summary>
    /// Extracts shallow expected-token names for supported rule-content shapes.
    /// Returns <c>null</c> when the shape is unsupported or ambiguous.
    /// </summary>
    private static IReadOnlyList<string>? TryGetExpectedTokenNames(RuleContent content, Func<string, Rule?> resolveRule)
    {
        return content switch
        {
            LiteralMatch literal => [literal.Value],
            RuleRef ruleRef => TryGetExpectedTokenNamesForRuleReference(ruleRef, resolveRule),
            Alternation alternation => TryGetExpectedTokenNamesForAlternation(alternation, resolveRule),
            Sequence sequence => TryGetExpectedTokenNamesForSequence(sequence, resolveRule),
            Quantifier => null,
            _ => null
        };
    }

    /// <summary>
    /// Returns the lexer rule name when the reference targets a lexer rule.
    /// Parser-rule references return <c>null</c>.
    /// </summary>
    private static IReadOnlyList<string>? TryGetExpectedTokenNamesForRuleReference(RuleRef ruleRef, Func<string, Rule?> resolveRule)
    {
        var target = resolveRule(ruleRef.RuleName);
        return target is { Kind: RuleKind.Lexer } ? [ruleRef.RuleName] : null;
    }

    /// <summary>
    /// Aggregates expected-token names for flat alternations when all branches
    /// are shallow and supported; otherwise returns <c>null</c>.
    /// </summary>
    private static IReadOnlyList<string>? TryGetExpectedTokenNamesForAlternation(
        Alternation alternation,
        Func<string, Rule?> resolveRule)
    {
        var expected = new List<string>();
        foreach (var alternative in alternation.Alternatives)
        {
            var alternativeExpected = TryGetExpectedTokenNames(alternative.Content, resolveRule);
            if (alternativeExpected is null)
            {
                return null;
            }

            expected.AddRange(alternativeExpected);
        }

        return expected;
    }

    /// <summary>
    /// Extracts expected-token names from the first meaningful sequence item.
    /// Returns <c>null</c> when epsilon-first ambiguity is detected.
    /// </summary>
    private static IReadOnlyList<string>? TryGetExpectedTokenNamesForSequence(Sequence sequence, Func<string, Rule?> resolveRule)
    {
        var meaningfulItems = sequence.Items.Where(static item => item is not EmbeddedAction and not LexerCommand).ToArray();
        if (meaningfulItems.Length == 0)
        {
            return null;
        }

        var firstProbe = TryGetExpectedTokenNames(meaningfulItems[0], resolveRule);
        if (firstProbe is null)
        {
            return null;
        }

        if (meaningfulItems[0] is Quantifier quantifier && quantifier.Min == 0 && meaningfulItems.Length > 1)
        {
            return null;
        }

        return firstProbe;
    }

    /// <summary>
    /// Creates an unknown probe result while preserving token snapshot metadata.
    /// </summary>
    private static ParserLookaheadProbeResult Unknown(Token? token, IReadOnlyList<string>? expectedTokenNames) =>
        new(ParserLookaheadProbeKind.Unknown, token?.RuleName, token?.Text, expectedTokenNames);

    /// <summary>
    /// Creates an immediate-reject probe result while preserving token snapshot metadata.
    /// </summary>
    private static ParserLookaheadProbeResult ImmediateReject(Token? token, IReadOnlyList<string>? expectedTokenNames) =>
        new(ParserLookaheadProbeKind.ImmediateReject, token?.RuleName, token?.Text, expectedTokenNames);

    /// <summary>
    /// Creates a requires-parse probe result while preserving token snapshot metadata.
    /// </summary>
    private static ParserLookaheadProbeResult RequiresParse(Token? token, IReadOnlyList<string>? expectedTokenNames) =>
        new(ParserLookaheadProbeKind.RequiresParse, token?.RuleName, token?.Text, expectedTokenNames);

    /// <summary>
    /// Creates an epsilon-possible probe result while preserving token snapshot metadata.
    /// </summary>
    private static ParserLookaheadProbeResult EpsilonPossible(Token? token, IReadOnlyList<string>? expectedTokenNames) =>
        new(ParserLookaheadProbeKind.EpsilonPossible, token?.RuleName, token?.Text, expectedTokenNames);
}

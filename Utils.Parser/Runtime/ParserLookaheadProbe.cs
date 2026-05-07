using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Performs conservative first-token look-ahead probing for a scheduled alternative.
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
        return content switch
        {
            LiteralMatch literal => ProbeLiteralMatch(literal, token, caseInsensitive),
            RuleRef ruleRef => ProbeRuleReference(ruleRef, token, resolveRule),
            Sequence sequence => ProbeSequence(sequence, token, resolveRule, caseInsensitive),
            Quantifier quantifier => ProbeQuantifier(quantifier, token),
            _ => Unknown(token)
        };
    }

    /// <summary>
    /// Probes a literal first item against the current token text.
    /// </summary>
    private static ParserLookaheadProbeResult ProbeLiteralMatch(LiteralMatch literal, Token? token, bool caseInsensitive)
    {
        if (token is null)
        {
            return ImmediateReject(token);
        }

        var comparison = caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(token.Text, literal.Value, comparison)
            ? RequiresParse(token)
            : ImmediateReject(token);
    }

    /// <summary>
    /// Probes a rule reference when the target is a lexer rule.
    /// Parser rule references always return Unknown because they may accept empty input.
    /// </summary>
    private static ParserLookaheadProbeResult ProbeRuleReference(
        RuleRef ruleRef,
        Token? token,
        Func<string, Rule?> resolveRule)
    {
        var resolved = resolveRule(ruleRef.RuleName);
        if (resolved is null || resolved.Kind != RuleKind.Lexer)
        {
            return Unknown(token);
        }

        if (token is null)
        {
            return ImmediateReject(token);
        }

        return string.Equals(token.RuleName, ruleRef.RuleName, StringComparison.Ordinal)
            ? RequiresParse(token)
            : ImmediateReject(token);
    }

    /// <summary>
    /// Probes sequence items while skipping non-semantic runtime directives.
    /// </summary>
    private static ParserLookaheadProbeResult ProbeSequence(
        Sequence sequence,
        Token? token,
        Func<string, Rule?> resolveRule,
        bool caseInsensitive)
    {
        var meaningfulItems = sequence.Items
            .Where(static item => item is not EmbeddedAction and not LexerCommand)
            .ToArray();

        if (meaningfulItems.Length == 0)
        {
            return EpsilonPossible(token);
        }

        var encounteredEpsilonPossible = false;

        for (var i = 0; i < meaningfulItems.Length; i++)
        {
            var itemProbe = ProbeContent(meaningfulItems[i], token, resolveRule, caseInsensitive);
            switch (itemProbe.Kind)
            {
                case ParserLookaheadProbeKind.ImmediateReject:
                case ParserLookaheadProbeKind.RequiresParse:
                    return encounteredEpsilonPossible ? Unknown(token) : itemProbe;
                case ParserLookaheadProbeKind.EpsilonPossible:
                    encounteredEpsilonPossible = true;
                    continue;
                case ParserLookaheadProbeKind.Unknown:
                default:
                    return itemProbe;
            }
        }

        return EpsilonPossible(token);
    }

    /// <summary>
    /// Probes quantifiers conservatively for local epsilon capability only.
    /// </summary>
    private static ParserLookaheadProbeResult ProbeQuantifier(Quantifier quantifier, Token? token)
    {
        return quantifier.Min == 0
            ? EpsilonPossible(token)
            : Unknown(token);
    }

    private static ParserLookaheadProbeResult Unknown(Token? token) =>
        new(ParserLookaheadProbeKind.Unknown, token?.RuleName, token?.Text);

    private static ParserLookaheadProbeResult ImmediateReject(Token? token) =>
        new(ParserLookaheadProbeKind.ImmediateReject, token?.RuleName, token?.Text);

    private static ParserLookaheadProbeResult RequiresParse(Token? token) =>
        new(ParserLookaheadProbeKind.RequiresParse, token?.RuleName, token?.Text);

    private static ParserLookaheadProbeResult EpsilonPossible(Token? token) =>
        new(ParserLookaheadProbeKind.EpsilonPossible, token?.RuleName, token?.Text);
}



using Utils.Parser.Model;
using Utils.Parser.Diagnostics;

namespace Utils.Parser.Runtime;

/// <summary>
/// Builds a parse tree from a flat token list using the rules in a
/// <see cref="ParserDefinition"/>.
/// <para>
/// The engine is a recursive-descent parser with full backtracking.
/// It tries each alternative in priority order and rolls back the token position on
/// failure. Left-recursive rules are handled by a cycle-detection stack that returns
/// <c>null</c> when the same rule is re-entered at the same token position.
/// </para>
/// <para>
/// Additional runtime guards stop repeated parser-state exploration and non-progressive
/// iterations in quantifiers and left-recursive extensions. Backtracking remains active
/// in this implementation and may be revisited in a future PR.
/// </para>
/// <para>
/// Semantic predicates (<see cref="ValidatingPredicate"/>, <see cref="GatingPredicate"/>)
/// and embedded actions (<see cref="EmbeddedAction"/>) are silently accepted without
/// execution; they have no effect on the parse tree shape.
/// </para>
/// </summary>
public sealed class ParserEngine
{
    private readonly ParserDefinition _definition;
    private readonly HashSet<ParserFrameKey> _activeRuleFrames = new();
    private readonly bool _caseInsensitive;
    private readonly ParserStateRegistry _stateRegistry = new();
    private readonly AlternativeScheduler _alternativeScheduler;
    private readonly ParserLookaheadCache _lookaheadCache = new();

    public ParserEngine(ParserDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _caseInsensitive = IsCaseInsensitive(_definition);
        _alternativeScheduler = new AlternativeScheduler();
    }

    /// <summary>
    /// Parses <paramref name="tokens"/> starting from <paramref name="startRule"/>
    /// (or the definition's root rule when <c>null</c>) and returns the root
    /// <see cref="ParseNode"/>.
    /// If parsing fails completely an <see cref="ErrorNode"/> is returned rather than
    /// throwing an exception.
    /// </summary>
    /// <param name="tokens">Flat list of tokens produced by <see cref="LexerEngine"/>.</param>
    /// <param name="startRule">Override for the start rule, or <c>null</c> to use the definition's root.</param>
    /// <returns>Root parse-tree node.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no start rule is available (neither <paramref name="startRule"/>
    /// nor <see cref="ParserDefinition.RootRule"/> is set).
    /// </exception>
    public ParseNode Parse(IEnumerable<Token> tokens, Rule? startRule = null, DiagnosticBag? diagnostics = null)
    {
        var tokenList = tokens.Where(static token => token.Channel == "DEFAULT_CHANNEL").ToList();
        var root = startRule ?? _definition.RootRule
            ?? throw new InvalidOperationException("No root rule defined");

        _activeRuleFrames.Clear();
        _stateRegistry.Clear();
        _lookaheadCache.Clear();
        var context = new ParseContext(tokenList);
        var result = ParseRule(context, root, precedence: 0, diagnostics);

        if (result is null)
        {
            diagnostics?.Add(ParserDiagnostics.ParseFailure, "Failed to parse from root rule");
            return new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE",
                "Failed to parse from root rule", root);
        }

        // Reject parses that leave trailing tokens unconsumed.
        if (!context.IsEnd)
        {
            var trailing = context.Peek()!;
            diagnostics?.AddWithContext(
                ParserDiagnostics.TrailingTokensAfterParse,
                trailing.Span.Position,
                trailing.Span.Length,
                null,
                null,
                trailing.Text);
            return new ErrorNode(result.Span, result.ModeName,
                $"Unexpected token '{trailing.Text}' at position {trailing.Span.Position}", root);
        }

        diagnostics?.Add(ParserDiagnostics.DefaultBehaviorApplied, "Parser completed without recovery.");
        return result;
    }

    /// <summary>
    /// Attempts to match <paramref name="rule"/> at the current context position.
    /// Tries each alternative in ascending priority order and returns the first match,
    /// or <c>null</c> when no alternative matches.
    /// Returns <c>null</c> immediately when a left-recursive cycle is detected
    /// (same rule re-entered at the same token position).
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="rule">Parser rule to attempt.</param>
    /// <param name="precedence">Minimum precedence level accepted for this parse attempt.</param>
    private ParseNode? ParseRule(ParseContext context, Rule rule, int precedence, DiagnosticBag? diagnostics = null)
    {
        diagnostics?.AddWithContext(ParserDiagnostics.EnteringRule, null, null, rule.Name, null, rule.Name);
        var initialPosition = context.Position;
        // Registry-backed completion cache:
        // this supersedes the previous local parse-memo dictionary and keeps reuse decisions centralized.
        // Safety currently depends on RuleInvocationKey identity:
        //   (rule name, input position, minimum precedence).
        // This is sufficient for current parser semantics because semantic predicates/actions are not executed.
        // TODO: revisit key shape when semantic state, mode-sensitive parser state, or predicate execution is enabled.
        var invocationKey = new RuleInvocationKey(rule.Name, initialPosition, precedence);
        if (_stateRegistry.TryGetReusableResult(invocationKey, out var reusableResult))
        {
            context.RestorePosition(reusableResult.EndPosition);
            diagnostics?.AddWithContext(ParserDiagnostics.ParseMemoHit, null, null, rule.Name, null, rule.Name);
            return reusableResult.IsFailure ? null : reusableResult.Node;
        }
        diagnostics?.AddWithContext(ParserDiagnostics.ParseMemoMiss, null, null, rule.Name, null, rule.Name);
        var frameKey = new ParserFrameKey(rule.Name, context.Position);
        if (!_activeRuleFrames.Add(frameKey))
        {
            return null;
        }

        try
        {
            ParseNode? parsed;
            if (_definition.LeftRecursiveRules.TryGetValue(rule.Name, out var leftRecursiveInfo))
            {
                diagnostics?.AddWithContext(
                    ParserDiagnostics.LeftRecursivePrecedencePartiallySupported,
                    null,
                    null,
                    rule.Name,
                    null,
                    rule.Name);
                parsed = ParseLeftRecursiveRule(context, leftRecursiveInfo, precedence, diagnostics);
            }
            else
            {
                parsed = TryParseScheduledAlternatives(context, rule.Content.Alternatives, rule, precedence, diagnostics, "rule-root", -1);
            }

            _stateRegistry.AddCompletedResult(invocationKey, new ParserRuleResult(parsed, context.Position, parsed is null));

            return parsed;
        }
        finally
        {
            _activeRuleFrames.Remove(frameKey);
            if (context.Position < initialPosition)
            {
                context.RestorePosition(initialPosition);
            }
            diagnostics?.AddWithContext(ParserDiagnostics.LeavingRule, null, null, rule.Name, null, rule.Name);
        }
    }

    private ParseNode? ParseLeftRecursiveRule(
        ParseContext context,
        LeftRecursiveRuleInfo info,
        int minimumPrecedence,
        DiagnosticBag? diagnostics)
    {
        var seed = TryParseScheduledAlternatives(
            context,
            info.BaseAlternatives,
            info.Rule,
            minimumPrecedence,
            diagnostics,
            "left-recursive-seed",
            -1);
        if (seed is null)
        {
            return null;
        }

        var current = seed;
        var currentEndPosition = context.Position;
        var visitedStates = new HashSet<ParserStateKey>();
        while (true)
        {
            var extension = TryExtendLeft(context, info, current, minimumPrecedence, visitedStates, diagnostics);
            if (extension is null)
            {
                break;
            }

            // Guard against infinite loops: if the extension did not consume any tokens,
            // further iterations cannot make progress either.
            if (!context.HasStrictProgress(currentEndPosition))
            {
                var span = ResolveDiagnosticSpan(context);
                diagnostics?.AddWithContext(
                    ParserDiagnostics.ParserStateCycleDetected,
                    span.Start,
                    span.Length,
                    info.Rule.Name,
                    null);
                diagnostics?.AddWithContext(
                    ParserDiagnostics.NonProgressiveLeftRecursionStopped,
                    span.Start,
                    span.Length,
                    info.Rule.Name,
                    null);
                diagnostics?.AddWithContext(
                    ParserDiagnostics.ParserStateRejected,
                    span.Start,
                    span.Length,
                    info.Rule.Name,
                    null,
                    info.Rule.Name,
                    $"left recursion non-progressive at position {context.Position}");
                break;
            }

            currentEndPosition = context.Position;
            current = extension;
        }

        return current;
    }

    private ParseNode? TryExtendLeft(
        ParseContext context,
        LeftRecursiveRuleInfo info,
        ParseNode current,
        int minimumPrecedence,
        HashSet<ParserStateKey> visitedStates,
        DiagnosticBag? diagnostics)
    {
        var startPosition = context.Position;
        ParseBranch? bestBranch = null;
        var recursiveAlternatives = info.RecursiveAlternatives.OrderBy(a => a.Priority).ToList();

        for (int index = 0; index < recursiveAlternatives.Count; index++)
        {
            var alternative = recursiveAlternatives[index];
            var stateKey = new ParserStateKey(info.Rule.Name, startPosition, index, index, minimumPrecedence);
            // Registry state is currently preparatory for future active-state parsing.
            // We keep recording here, but branch rejection remains driven by local state
            // checks to avoid false positives across legitimate shared invocations.
            _stateRegistry.TryEnterState(new ParserStateKey(
                info.Rule.Name,
                startPosition,
                index,
                index,
                minimumPrecedence));
            if (!visitedStates.Add(stateKey))
            {
                var span = ResolveDiagnosticSpan(context);
                diagnostics?.AddWithContext(
                    ParserDiagnostics.ParserStateCycleDetected,
                    span.Start,
                    span.Length,
                    info.Rule.Name,
                    null);
                diagnostics?.AddWithContext(
                    ParserDiagnostics.ParserStateRejected,
                    span.Start,
                    span.Length,
                    info.Rule.Name,
                    null,
                    info.Rule.Name,
                    $"repeated state {stateKey}");
                continue;
            }

            var precedenceLevel = recursiveAlternatives.Count - index;
            if (precedenceLevel < minimumPrecedence || !CheckPrecedence(alternative, minimumPrecedence))
            {
                continue;
            }

            var saved = context.SavePosition();
            var candidate = TryParseRecursiveAlternative(
                context,
                info.Rule,
                alternative,
                index,
                current,
                precedenceLevel,
                diagnostics);
            if (candidate is null)
            {
                context.RestorePosition(saved);
                continue;
            }

            var branch = new ParseBranch
            {
                Rule = info.Rule,
                Alternative = alternative,
                InputPosition = startPosition,
                Cursor = new RuleContentCursor { Index = 0, Kind = "recursive-extension" },
                PartialNode = candidate,
                EndPosition = context.Position,
                IsComplete = true
            };

            if (bestBranch is null || IsBetterBranch(branch, bestBranch))
            {
                if (bestBranch is not null)
                {
                    diagnostics?.AddWithContext(ParserDiagnostics.ParseBranchPruned, null, null, info.Rule.Name, null, info.Rule.Name);
                }

                bestBranch = branch;
            }
            else
            {
                diagnostics?.AddWithContext(ParserDiagnostics.ParseBranchPruned, null, null, info.Rule.Name, null, info.Rule.Name);
            }

            context.RestorePosition(saved);
        }

        if (bestBranch is null)
        {
            context.RestorePosition(startPosition);
            return null;
        }

        context.RestorePosition(bestBranch.EndPosition);
        return bestBranch.PartialNode;
    }

    private ParseNode? TryParseRecursiveAlternative(
        ParseContext context,
        Rule ownerRule,
        Alternative alternative,
        int alternativeIndex,
        ParseNode leftSeed,
        int precedenceLevel,
        DiagnosticBag? diagnostics)
    {
        var children = new List<ParseNode> { leftSeed };

        var tailContent = RemoveLeadingSelfReference(ownerRule.Name, alternative.Content);
        if (tailContent is null)
        {
            return null;
        }

        var tailNodes = TryParseLeftRecursiveTail(
            context,
            tailContent,
            ownerRule,
            alternativeIndex,
            alternative.Assoc,
            precedenceLevel,
            diagnostics);
        if (tailNodes is null)
        {
            return null;
        }

        children.AddRange(tailNodes);
        var endToken = context.Peek(-1);
        var end = endToken is null ? leftSeed.Span.Position + leftSeed.Span.Length : endToken.Span.Position + endToken.Span.Length;
        var span = new SourceSpan(leftSeed.Span.Position, Math.Max(0, end - leftSeed.Span.Position));
        return new ParserNode(span, leftSeed.ModeName, ownerRule, children);
    }

    /// <summary>
    /// Returns <c>true</c> when the alternative either has no precedence predicate
    /// or its predicate level satisfies <paramref name="currentPrecedence"/>.
    /// </summary>
    /// <param name="alt">Alternative to check.</param>
    /// <param name="currentPrecedence">Minimum acceptable precedence level.</param>
    private bool CheckPrecedence(Alternative alt, int currentPrecedence)
    {
        var predLevel = FindPrecedenceLevel(alt.Content);
        if (predLevel is null)
            return true; // No predicate — always accepted.

        return predLevel.Value >= currentPrecedence;
    }

    /// <summary>
    /// Searches <paramref name="content"/> for the first <see cref="PrecedencePredicate"/>
    /// and returns its level, or <c>null</c> when none is found.
    /// Only the first level of sequences is examined (matching ANTLR4 semantics).
    /// </summary>
    /// <param name="content">Grammar element to inspect.</param>
    private int? FindPrecedenceLevel(RuleContent content)
    {
        switch (content)
        {
            case PrecedencePredicate pp:
                return pp.Level;
            case Sequence seq:
                foreach (var item in seq.Items)
                {
                    var level = FindPrecedenceLevel(item);
                    if (level is not null) return level;
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Delegates to <see cref="TryParseContent"/> for the content of a single alternative.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="alt">Alternative to attempt.</param>
    /// <param name="rule">Parent rule (carried to child nodes).</param>
    private ParseNode? TryParseAlternative(
        ParseContext context,
        Alternative alt,
        Rule rule,
        int precedence = 0,
        int alternativeIndex = -1,
        int elementIndex = -1,
        DiagnosticBag? diagnostics = null)
    {
        return TryParseContent(context, alt.Content, rule, precedence, alternativeIndex, elementIndex, diagnostics);
    }

    /// <summary>
    /// Dispatches to the appropriate handler based on the concrete type of
    /// <paramref name="content"/>.
    /// Predicates and embedded actions are silently treated as empty successful matches.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="content">Grammar element to match.</param>
    /// <param name="rule">Rule in whose context the element is being matched.</param>
    private ParseNode? TryParseContent(
        ParseContext context,
        RuleContent content,
        Rule rule,
        int precedence = 0,
        int alternativeIndex = -1,
        int elementIndex = -1,
        DiagnosticBag? diagnostics = null)
    {
        switch (content)
        {
            case RuleRef ruleRef:
                return TryParseRuleRef(context, ruleRef, rule, precedence, alternativeIndex, elementIndex, diagnostics);

            case Sequence seq:
                return TryParseSequence(context, seq, rule, precedence, alternativeIndex, diagnostics);

            case Alternation alternation:
                return TryParseAlternation(context, alternation, rule, precedence, alternativeIndex, elementIndex, diagnostics);

            case Alternative alt:
                return TryParseAlternative(context, alt, rule, precedence, alternativeIndex, elementIndex, diagnostics);

            case Quantifier quant:
                return TryParseQuantifier(context, quant, rule, precedence, alternativeIndex, diagnostics);

            case LiteralMatch lit:
                return TryParseLiteral(context, lit, rule);

            case Negation neg:
                return TryParseNegation(context, neg, rule, precedence, alternativeIndex, diagnostics);

            case ValidatingPredicate:
            case GatingPredicate:
                // Semantic predicates: silently accepted without evaluation.
                diagnostics?.AddWithContext(ParserDiagnostics.SemanticPredicateNotEnforced, null, null, rule.Name, null);
                return CreateEmptyNode(context, rule);

            case PrecedencePredicate:
                // Already handled in CheckPrecedence.
                return CreateEmptyNode(context, rule);

            case EmbeddedAction:
                // Embedded actions: never executed, always succeed.
                diagnostics?.AddWithContext(ParserDiagnostics.InlineActionStoredNotExecuted, null, null, rule.Name, null);
                return CreateEmptyNode(context, rule);

            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves a rule reference, either consuming a matching token (for lexer rules)
    /// or recursing into the referenced parser rule.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="ruleRef">Reference to resolve.</param>
    /// <param name="parentRule">The rule in which this reference appears.</param>
    private ParseNode? TryParseRuleRef(
        ParseContext context,
        RuleRef ruleRef,
        Rule parentRule,
        int minimumPrecedence,
        int alternativeIndex,
        int elementIndex,
        DiagnosticBag? diagnostics = null)
    {
        if (!_definition.AllRules.TryGetValue(ruleRef.RuleName, out var referencedRule))
            return null;

        // The tuple (RuleName, Position) alone is not enough to reject a shared call:
        // different callers can legitimately invoke the same rule at the same input position.
        // We therefore keep lightweight continuation metadata for future state-based parsing.
        _stateRegistry.AddContinuation(
            new RuleInvocationKey(ruleRef.RuleName, context.Position, minimumPrecedence),
            new ContinuationKey(parentRule.Name, alternativeIndex, elementIndex, context.Position, minimumPrecedence));

        if (referencedRule.Kind == RuleKind.Lexer)
        {
            // Direct token match: the next token must have been produced by this lexer rule.
            var token = context.Peek();
            if (token is null) return null;

            if (token.RuleName == ruleRef.RuleName)
            {
                context.Consume();
                diagnostics?.AddWithContext(
                    ParserDiagnostics.TokenMatched,
                    token.Span.Position,
                    token.Span.Length,
                    ruleRef.RuleName,
                    null,
                    ruleRef.RuleName,
                    token.Text);
                return new LexerNode(token.Span, token.ModeName, referencedRule, token);
            }
            return null;
        }

        // Parser rule: recurse.
        return ParseRule(context, referencedRule, precedence: 0, diagnostics);
    }

    /// <summary>
    /// Attempts to match every item in a sequence in order.
    /// Returns a <see cref="ParserNode"/> containing all non-empty child nodes on success,
    /// or <c>null</c> if any item fails to match (the cursor is not restored here;
    /// callers must save/restore the position).
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="seq">Sequence to match.</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private ParseNode? TryParseSequence(
        ParseContext context,
        Sequence seq,
        Rule rule,
        int minimumPrecedence,
        int alternativeIndex,
        DiagnosticBag? diagnostics = null)
    {
        var children = new List<ParseNode>();
        var startPos = context.Position;
        var startToken = context.Peek();
        var visitedStates = new HashSet<ParserStateKey>();

        for (int itemIndex = 0; itemIndex < seq.Items.Count; itemIndex++)
        {
            var item = seq.Items[itemIndex];
            if (item is EmbeddedAction or Model.LexerCommand)
                continue;

            var itemStartPosition = context.Position;
            var stateKey = new ParserStateKey(
                rule.Name,
                itemStartPosition,
                alternativeIndex,
                itemIndex,
                minimumPrecedence);
            if (!visitedStates.Add(stateKey))
            {
                var diagnosticSpan = ResolveDiagnosticSpan(context);
                diagnostics?.AddWithContext(
                    ParserDiagnostics.ParserStateCycleDetected,
                    diagnosticSpan.Start,
                    diagnosticSpan.Length,
                    rule.Name,
                    null);
                diagnostics?.AddWithContext(
                    ParserDiagnostics.ParserStateRejected,
                    diagnosticSpan.Start,
                    diagnosticSpan.Length,
                    rule.Name,
                    null,
                    rule.Name,
                    $"repeated sequence item state {stateKey}");
                return null;
            }
            var node = TryParseContent(context, item, rule, minimumPrecedence, alternativeIndex, itemIndex, diagnostics);
            if (node is null)
                return null;

            // Omit empty nodes (predicates, actions) from the child list.
            if (node.Span.Length > 0 || node is ParserNode { Children.Count: > 0 })
                children.Add(node);
        }

        var span = ComputeSpan(startToken, context, startPos);
        return new ParserNode(span, startToken?.ModeName ?? "DEFAULT_MODE", rule, children);
    }

    /// <summary>
    /// Tries each alternative of an <see cref="Alternation"/> in priority order,
    /// saving and restoring the cursor between attempts.
    /// Returns the first successful match, or <c>null</c> when all alternatives fail.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="alternation">Alternation to evaluate.</param>
    /// <param name="rule">Owning rule (carried to child matches).</param>
    private ParseNode? TryParseAlternation(
        ParseContext context,
        Alternation alternation,
        Rule rule,
        int precedence = 0,
        int alternativeIndex = -1,
        int elementIndex = -1,
        DiagnosticBag? diagnostics = null)
    {
        return TryParseScheduledAlternatives(context, alternation.Alternatives, rule, precedence, diagnostics, "alternation", elementIndex >= 0 ? elementIndex : alternativeIndex);
    }

    private ParseNode? TryParseScheduledAlternatives(
        ParseContext context,
        IEnumerable<Alternative> alternatives,
        Rule rule,
        int precedence,
        DiagnosticBag? diagnostics,
        string cursorKind,
        int cursorIndex)
    {
        var startPosition = context.Position;
        var startToken = context.Peek();
        var scheduling = _alternativeScheduler.Run(
            rule,
            alternatives,
            startPosition,
            precedence,
            diagnostics,
            parseAlternative: (alternative, alternativeIndex) =>
            {
                var stateKey = new ParserStateKey(rule.Name, startPosition, alternativeIndex, alternativeIndex, precedence);
                _stateRegistry.TryEnterState(stateKey);

                if (!CheckPrecedence(alternative, precedence))
                {
                    return null;
                }

                var lookaheadKey = new ParserLookaheadKey(rule.Name, startPosition, alternativeIndex, precedence, cursorKind, cursorIndex);
                var token = context.Peek();
                if (_lookaheadCache.TryGet(lookaheadKey, out var cachedLookahead) && !cachedLookahead.CanStart)
                {
                    var diagnosticSpan = ResolveDiagnosticSpan(context);
                    diagnostics?.AddWithContext(ParserDiagnostics.BacktrackingUsed, diagnosticSpan.Start, diagnosticSpan.Length, rule.Name, null, rule.Name);
                    return null;
                }

                var savedPosition = context.SavePosition();
                var result = TryParseContent(context, alternative.Content, rule, precedence, alternativeIndex, alternativeIndex, diagnostics);
                if (result is null)
                {
                    var consumed = context.Position > savedPosition;
                    if (!consumed && !ContainsPredicateOrAction(alternative.Content))
                    {
                        _lookaheadCache.TryAdd(lookaheadKey, new ParserLookaheadResult(false, token?.RuleName, token?.Text));
                    }

                    var diagnosticSpan = ResolveDiagnosticSpan(context);
                    diagnostics?.AddWithContext(ParserDiagnostics.BacktrackingUsed, diagnosticSpan.Start, diagnosticSpan.Length, rule.Name, null, rule.Name);
                    context.RestorePosition(savedPosition);
                    return null;
                }

                _lookaheadCache.TryAdd(lookaheadKey, new ParserLookaheadResult(true, token?.RuleName, token?.Text));

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
            });

        if (scheduling.SelectedState is null)
        {
            return null;
        }

        var winner = scheduling.SelectedState;
        context.RestorePosition(winner.EndPosition ?? winner.CurrentInputPosition);
        if (winner.PartialNode is ParserNode parserNode && ReferenceEquals(parserNode.Rule, rule))
        {
            return parserNode;
        }

        var span = ComputeSpan(startToken, context, startPosition);
        return new ParserNode(span, winner.PartialNode.ModeName, rule, [winner.PartialNode]);
    }

    private static bool IsBetterBranch(ParseBranch candidate, ParseBranch current)
    {
        if (candidate.EndPosition != current.EndPosition)
        {
            return candidate.EndPosition > current.EndPosition;
        }

        return candidate.Alternative.Priority < current.Alternative.Priority;
    }

    internal static bool HasDistinctSemantics(Alternative left, Alternative right)
    {
        return !string.Equals(left.Label, right.Label, StringComparison.Ordinal)
            || left.Assoc != right.Assoc
            || ContainsPredicateOrAction(left.Content)
            || ContainsPredicateOrAction(right.Content);
    }

    private static bool ContainsPredicateOrAction(RuleContent content)
    {
        return content switch
        {
            ValidatingPredicate or GatingPredicate or EmbeddedAction => true,
            Sequence seq => seq.Items.Any(ContainsPredicateOrAction),
            Alternation alt => alt.Alternatives.Any(a => ContainsPredicateOrAction(a.Content)),
            Quantifier q => ContainsPredicateOrAction(q.Inner),
            Negation n => ContainsPredicateOrAction(n.Inner),
            Alternative a => ContainsPredicateOrAction(a.Content),
            _ => false
        };
    }

    /// <summary>
    /// Matches a quantified element as many times as allowed by its bounds.
    /// Returns a <see cref="ParserNode"/> when the minimum repeat count is satisfied,
    /// or <c>null</c> otherwise.
    /// Zero-length matches are detected and break the loop to prevent infinite recursion.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="quant">Quantifier to evaluate.</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private ParseNode? TryParseQuantifier(
        ParseContext context,
        Quantifier quant,
        Rule rule,
        int minimumPrecedence,
        int alternativeIndex,
        DiagnosticBag? diagnostics = null)
    {
        var children = new List<ParseNode>();
        var startPos = context.Position;
        var startToken = context.Peek();

        int count = 0;
        int previousPosition = context.Position;
        while (quant.Max is null || count < quant.Max.Value)
        {
            var savedPos = context.SavePosition();
            var node = TryParseContent(context, quant.Inner, rule, minimumPrecedence, alternativeIndex, alternativeIndex, diagnostics);
            if (node is null)
            {
                context.RestorePosition(savedPos);
                break;
            }

            // Guard against zero-length matches.
            if (context.Position <= previousPosition)
            {
                context.RestorePosition(savedPos);
                var diagnosticSpan = ResolveDiagnosticSpan(context);
                diagnostics?.AddWithContext(
                    ParserDiagnostics.NonProgressiveQuantifierStopped,
                    diagnosticSpan.Start,
                    diagnosticSpan.Length,
                    rule.Name,
                    null);
                diagnostics?.AddWithContext(
                    ParserDiagnostics.ParserStateRejected,
                    diagnosticSpan.Start,
                    diagnosticSpan.Length,
                    rule.Name,
                    null,
                    rule.Name,
                    $"quantifier inner matched without progress at position {savedPos}");
                break;
            }

            children.Add(node);
            count++;
            previousPosition = context.Position;

            if (!quant.Greedy && count >= quant.Min)
                break;
        }

        if (count < quant.Min)
            return null;

        var span = ComputeSpan(startToken, context, startPos);
        return new ParserNode(span, startToken?.ModeName ?? "DEFAULT_MODE", rule, children);
    }

    /// <summary>
    /// Matches the next token against a literal string.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="lit">Literal to match.</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private ParseNode? TryParseLiteral(ParseContext context, LiteralMatch lit, Rule rule)
    {
        var token = context.Peek();
        if (token is null) return null;

        if (string.Equals(
            token.Text,
            lit.Value,
            _caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            context.Consume();
            return new LexerNode(token.Span, token.ModeName, rule, token);
        }

        return null;
    }

    /// <summary>Returns <c>true</c> when the grammar declares <c>caseInsensitive = true</c>.</summary>
    private static bool IsCaseInsensitive(ParserDefinition definition) =>
        definition.EffectiveOptions.CaseInsensitive;

    /// <summary>
    /// Implements the <c>~</c> negation operator at the token level: consumes the
    /// next token when the inner element does <em>not</em> match.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="neg">Negation element to evaluate.</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private ParseNode? TryParseNegation(
        ParseContext context,
        Negation neg,
        Rule rule,
        int minimumPrecedence,
        int alternativeIndex,
        DiagnosticBag? diagnostics = null)
    {
        var token = context.Peek();
        if (token is null) return null;

        var savedPos = context.SavePosition();
        var matched = TryParseContent(context, neg.Inner, rule, minimumPrecedence, alternativeIndex, alternativeIndex, diagnostics);
        context.RestorePosition(savedPos);

        if (matched is null)
        {
            // Negation succeeds: consume one token.
            var consumed = context.Consume();
            return new LexerNode(consumed.Span, consumed.ModeName, rule, consumed);
        }

        return null;
    }

    private static RuleContent? RemoveLeadingSelfReference(string ruleName, RuleContent content)
    {
        switch (content)
        {
            case RuleRef ruleRef when string.Equals(ruleRef.RuleName, ruleName, StringComparison.Ordinal):
                return new Sequence([]);
            case Sequence sequence when sequence.Items.Count > 0:
            {
                if (sequence.Items[0] is RuleRef leading &&
                    string.Equals(leading.RuleName, ruleName, StringComparison.Ordinal))
                {
                    return new Sequence(sequence.Items.Skip(1).ToList());
                }

                return null;
            }
            default:
                return null;
        }
    }

    private List<ParseNode>? TryParseLeftRecursiveTail(
        ParseContext context,
        RuleContent tailContent,
        Rule ownerRule,
        int alternativeIndex,
        Associativity associativity,
        int precedenceLevel,
        DiagnosticBag? diagnostics)
    {
        if (tailContent is Sequence sequence)
        {
            var result = new List<ParseNode>();
            for (int itemIndex = 0; itemIndex < sequence.Items.Count; itemIndex++)
            {
                var item = sequence.Items[itemIndex];
                ParseNode? node;
                if (item is RuleRef rr &&
                    string.Equals(rr.RuleName, ownerRule.Name, StringComparison.Ordinal) &&
                    _definition.AllRules.TryGetValue(rr.RuleName, out var recursiveRule))
                {
                    var minimumRightPrecedence = GetRightPrecedenceThreshold(associativity, precedenceLevel);
                    node = ParseRule(context, recursiveRule, minimumRightPrecedence, diagnostics);
                }
                else
                {
                    node = TryParseContent(context, item, ownerRule, precedenceLevel, alternativeIndex, itemIndex, diagnostics);
                }

                if (node is null)
                {
                    return null;
                }

                if (node.Span.Length > 0 || node is ParserNode { Children.Count: > 0 })
                {
                    result.Add(node);
                }
            }

            return result;
        }

        var tailNode = TryParseContent(context, tailContent, ownerRule, precedenceLevel, alternativeIndex, alternativeIndex, diagnostics);
        return tailNode is null ? null : [tailNode];
    }

    private static int GetRightPrecedenceThreshold(Associativity associativity, int precedenceLevel)
    {
        if (associativity == Associativity.Right)
        {
            return precedenceLevel;
        }

        return precedenceLevel + 1;
    }

    private static (int? Start, int? Length) ResolveDiagnosticSpan(ParseContext context)
    {
        var token = context.Peek() ?? context.Peek(-1);
        return (token?.Span.Position, token?.Span.Length);
    }

    /// <summary>
    /// Creates a zero-length, empty <see cref="ParserNode"/> at the current position.
    /// Used for predicates and embedded actions that succeed without consuming input.
    /// </summary>
    /// <param name="context">Token-stream cursor (position is not advanced).</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private static ParseNode CreateEmptyNode(ParseContext context, Rule rule)
    {
        var token = context.Peek();
        var pos = token?.Span.Position ?? 0;
        var modeName = token?.ModeName ?? "DEFAULT_MODE";
        return new ParserNode(new SourceSpan(pos, 0), modeName, rule, []);
    }

    /// <summary>
    /// Computes the <see cref="SourceSpan"/> that covers all tokens consumed since
    /// <paramref name="startPosition"/>. Returns a zero-length span when no tokens
    /// were consumed or <paramref name="startToken"/> is <c>null</c>.
    /// </summary>
    /// <param name="startToken">The first token in the range, or <c>null</c>.</param>
    /// <param name="context">Current cursor (used to locate the last consumed token).</param>
    /// <param name="startPosition">Token-list index at the start of the range.</param>
    private static SourceSpan ComputeSpan(Token? startToken, ParseContext context, int startPosition)
    {
        if (startToken is null)
            return new SourceSpan(0, 0);

        var endToken = context.Peek(-1);
        if (endToken is not null && context.Position > startPosition)
        {
            var end = endToken.Span.Position + endToken.Span.Length;
            return new SourceSpan(startToken.Span.Position, end - startToken.Span.Position);
        }

        return new SourceSpan(startToken.Span.Position, 0);
    }
}

using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Runtime;
using Utils.Parser.Source;

namespace UtilsTest.Security.Parser;

/// <summary>
/// S-06 retained-state accounting of the compact <see cref="ParserStateRegistry"/> storage. The registry must charge,
/// refuse and deduplicate exactly as the historical storage did (one set of visited states, one set of continuations per
/// invocation, one result list per invocation): the historical implementation is kept below as a reference model and
/// compared operation by operation, and parse-level charges are pinned to the values measured on the historical runtime.
/// </summary>
[TestClass]
public class ParserStateRegistryBudgetTests
{
    /// <summary>The input offset reported by the test budgets.</summary>
    private const int Offset = 42;

    /// <summary>
    /// Grammar with actions and semantic predicates driven by a counter execution state (the registry probe's
    /// <c>g-stateful</c> scenario): memoization restores post-rule snapshots and predicates depend on restored states.
    /// </summary>
    private const string StatefulGrammar = """
        grammar S;
        s : item* ;
        item : assign | stmt | expr ;
        assign : NAME '=' {bump} expr ;
        stmt : expr ';' {bump} ;
        expr : {even}? NAME {bump} | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>
    /// Random operation sequences produce the same results, the same charges and the same refusals on the compact registry
    /// and on the historical reference model, including after refusals (nothing is inserted by a refused operation).
    /// </summary>
    [TestMethod]
    public void RandomOperations_MatchTheHistoricalStorage()
    {
        int refusals = 0;
        for (int seed = 1; seed <= 200; seed++)
        {
            var random = new Random(seed);
            long limit = random.Next(5, 400);
            var compact = new ParserStateRegistry();
            var reference = new ReferenceRegistry();
            var compactBudget = CreateBudget(limit);
            var referenceBudget = CreateBudget(limit);
            ParseNode?[] nodes = [null, CreateNode(), CreateNode(), CreateNode()];
            var invocations = new HashSet<RuleInvocationKey>();

            for (int step = 0; step < 600; step++)
            {
                string context = $"seed {seed}, step {step}";
                var invocation = RandomInvocation(random);
                invocations.Add(invocation);
                switch (random.Next(6))
                {
                    case 0:
                    {
                        var key = RandomState(random);
                        refusals += Compare(context, () => compact.TryEnterState(key, compactBudget), () => reference.TryEnterState(key, referenceBudget));
                        break;
                    }
                    case 1:
                    {
                        var continuation = RandomContinuation(random);
                        refusals += Compare(context, () => compact.AddContinuation(invocation, continuation, compactBudget), () => reference.AddContinuation(invocation, continuation, referenceBudget));
                        break;
                    }
                    case 2:
                    {
                        var node = nodes[random.Next(nodes.Length)];
                        var result = new ParserRuleResult(node, random.Next(3), node is null ? random.Next(4) != 0 : random.Next(4) == 0, random.Next(3) == 0 ? null : (object)step);
                        refusals += Compare(context, () => compact.AddCompletedResult(invocation, result, compactBudget), () => reference.AddCompletedResult(invocation, result, referenceBudget));
                        break;
                    }
                    case 3:
                    {
                        bool compactFound = compact.TryGetReusableResult(invocation, out var compactResult);
                        bool referenceFound = reference.TryGetReusableResult(invocation, out var referenceResult);
                        Assert.AreEqual(referenceFound, compactFound, context);
                        Assert.AreEqual(referenceResult, compactResult, context);
                        Assert.AreSame(referenceResult.Node, compactResult.Node, context);
                        Assert.AreSame(referenceResult.ExecutionStateSnapshot, compactResult.ExecutionStateSnapshot, context);
                        break;
                    }
                    case 4:
                    {
                        // Unbudgeted calls (direct registry callers) follow the same deduplication.
                        var key = RandomState(random);
                        Assert.AreEqual(reference.TryEnterState(key, null), compact.TryEnterState(key), context);
                        break;
                    }
                    default:
                    {
                        var continuation = RandomContinuation(random);
                        Assert.AreEqual(reference.AddContinuation(invocation, continuation, null), compact.AddContinuation(invocation, continuation), context);
                        break;
                    }
                }

                Assert.AreEqual(referenceBudget.RetainedStates, compactBudget.RetainedStates, context);
            }

            foreach (var invocation in invocations)
            {
                CollectionAssert.AreEqual(reference.GetContinuations(invocation).ToArray(), compact.GetContinuations(invocation).ToArray(), $"seed {seed}: continuations of {invocation}");
                CollectionAssert.AreEqual(reference.GetCompletedResults(invocation).ToArray(), compact.GetCompletedResults(invocation).ToArray(), $"seed {seed}: results of {invocation}");
            }

            Assert.AreEqual(reference.VisitedStateCount, compact.VisitedStateCount, $"seed {seed}");
            Assert.AreEqual(reference.ContinuationCount, compact.ContinuationCount, $"seed {seed}");
            Assert.AreEqual(reference.ContinuationInvocationCount, compact.ContinuationInvocationCount, $"seed {seed}");
            Assert.AreEqual(reference.CompletedResultCount, compact.CompletedResultCount, $"seed {seed}");
        }

        Assert.IsTrue(refusals > 0, "some sequences reach their retained-state limit");
    }

    /// <summary>A duplicate is not charged, even when the budget is exhausted, for every kind of registry entry.</summary>
    [TestMethod]
    public void Duplicates_AreNotChargedOnAnExhaustedBudget()
    {
        var registry = new ParserStateRegistry();
        var budget = CreateBudget(5);
        var grouped = new ParserStateKey("expr", 1, 2, 2, 0);
        var overflow = new ParserStateKey("expr", 1, 2, 3, 0);
        var invocation = new RuleInvocationKey("expr", 1, 0);
        var continuation = new ContinuationKey("start", 0, 0, 1, 0);
        var first = new ParserRuleResult(CreateNode(), 2, false, "first");
        var second = new ParserRuleResult(null, 1, true);

        Assert.IsTrue(registry.TryEnterState(grouped, budget));
        Assert.IsTrue(registry.TryEnterState(overflow, budget));
        Assert.IsTrue(registry.AddContinuation(invocation, continuation, budget));
        Assert.IsTrue(registry.AddCompletedResult(invocation, first, budget));
        Assert.IsTrue(registry.AddCompletedResult(invocation, second, budget));
        Assert.AreEqual(5L, budget.RetainedStates);

        Assert.IsFalse(registry.TryEnterState(grouped, budget));
        Assert.IsFalse(registry.TryEnterState(overflow, budget));
        Assert.IsFalse(registry.AddContinuation(invocation, continuation, budget));
        Assert.IsFalse(registry.AddCompletedResult(invocation, first, budget));
        Assert.IsFalse(registry.AddCompletedResult(invocation, second, budget));
        Assert.AreEqual(5L, budget.RetainedStates);
    }

    /// <summary>
    /// Each kind of new entry is refused on an exhausted budget with the exact classification, and the refused entry is
    /// not stored: a new group, a new bit of an existing group, an overflow key, a first and a later continuation of an
    /// invocation, a first and a later completed result of an invocation.
    /// </summary>
    [TestMethod]
    public void NewEntries_AreRefusedWithExactClassificationAndNotStored()
    {
        var registry = new ParserStateRegistry();
        var budget = CreateBudget(3);
        var invocation = new RuleInvocationKey("expr", 1, 0);
        Assert.IsTrue(registry.TryEnterState(new ParserStateKey("expr", 1, 0, 0, 0), budget));
        Assert.IsTrue(registry.AddContinuation(invocation, new ContinuationKey("start", 0, 0, 1, 0), budget));
        Assert.IsTrue(registry.AddCompletedResult(invocation, new ParserRuleResult(null, 1, true), budget));

        var newGroup = new ParserStateKey("expr", 2, 0, 0, 0);
        var newBit = new ParserStateKey("expr", 1, 1, 1, 0);
        var newOverflow = new ParserStateKey("expr", 1, 0, 1, 0);
        var otherInvocation = new RuleInvocationKey("expr", 2, 0);
        var newContinuation = new ContinuationKey("start", 0, 1, 1, 0);
        var newResult = new ParserRuleResult(CreateNode(), 3, false);
        Action[] refusedOperations =
        [
            () => registry.TryEnterState(newGroup, budget),
            () => registry.TryEnterState(newBit, budget),
            () => registry.TryEnterState(newOverflow, budget),
            () => registry.AddContinuation(otherInvocation, newContinuation, budget),
            () => registry.AddContinuation(invocation, newContinuation, budget),
            () => registry.AddCompletedResult(otherInvocation, newResult, budget),
            () => registry.AddCompletedResult(invocation, newResult, budget),
        ];

        foreach (var operation in refusedOperations)
        {
            var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(operation);
            Assert.AreEqual(ParserResourceLimitKind.RetainedStates, refusal.Kind);
            Assert.AreEqual(ParserResourcePhase.Parser, refusal.Phase);
            Assert.AreEqual(3L, refusal.Limit);
            Assert.AreEqual(4L, refusal.Observed);
            Assert.AreEqual(Offset, refusal.Offset);
            Assert.AreEqual(3L, budget.RetainedStates);
        }

        Assert.AreEqual(1L, registry.VisitedStateCount);
        Assert.AreEqual(1, registry.VisitedStateGroupCount);
        Assert.AreEqual(1, registry.ContinuationCount);
        Assert.AreEqual(1L, registry.CompletedResultCount);
        Assert.IsFalse(registry.TryGetReusableResult(otherInvocation, out _));
        Assert.IsTrue(registry.TryEnterState(newGroup), "the refused group was not stored");
        Assert.IsTrue(registry.TryEnterState(newBit), "the refused bit was not stored");
        Assert.IsTrue(registry.TryEnterState(newOverflow), "the refused overflow key was not stored");
        Assert.IsTrue(registry.AddContinuation(otherInvocation, newContinuation), "the refused first continuation was not stored");
        Assert.IsTrue(registry.AddContinuation(invocation, newContinuation), "the refused later continuation was not stored");
        Assert.IsTrue(registry.AddCompletedResult(invocation, newResult), "the refused later result was not stored");
    }

    /// <summary>
    /// Parse-level charges equal the distinct entries of the registry and the look-ahead cache at the end of the parse, and
    /// are pinned to the values measured on the historical storage (commit 6e7892a0) for the stateful grammar.
    /// </summary>
    /// <param name="withDiagnostics">Whether the parse collects diagnostics (which disables the negative look-ahead shortcut).</param>
    /// <param name="expectedRetained">The retained states measured on the historical runtime.</param>
    [TestMethod]
    [DataRow(false, 26689L)]
    [DataRow(true, 25354L)]
    public void StatefulParse_ChargesTheDistinctEntriesMeasuredOnTheHistoricalStorage(bool withDiagnostics, long expectedRetained)
    {
        var (definition, tokens) = StatefulInput();
        var state = new CounterState();
        var frames = new RootExitFrameManager();
        var engine = new ParserEngine(definition, StatefulPolicy(state, frames), ParserResourceLimits.Default);
        long distinctAtRootExit = -1;
        frames.OnRootExit = () => distinctAtRootExit = DistinctEntries(engine);

        engine.Parse(tokens, diagnostics: withDiagnostics ? new DiagnosticBag() : null);

        Assert.AreEqual(expectedRetained, engine.LastResourceUsage!.RetainedStates);
        Assert.AreEqual(expectedRetained, distinctAtRootExit, "every charge corresponds to one distinct stored entry");
        Assert.AreEqual(1333L, state.Actions, "the action count measured on the historical runtime");
    }

    /// <summary>
    /// With the limit set to the exact retained total the stateful parse succeeds; one below, it is refused with the
    /// classification measured on the historical runtime, and the same engine then parses again with the same tree and
    /// the same action trace as a fresh engine.
    /// </summary>
    [TestMethod]
    public void StatefulParse_RefusalAtTheExactThreshold_LeavesTheEngineReusable()
    {
        var (definition, tokens) = StatefulInput();

        var exactState = new CounterState();
        var exact = new ParserEngine(definition, StatefulPolicy(exactState, new RootExitFrameManager()), ParserResourceLimits.Default with { MaxRetainedStates = 26689 });
        ParseNode accepted = exact.Parse(tokens);
        Assert.IsNotInstanceOfType<ErrorNode>(accepted);

        var refusedState = new CounterState();
        var refused = new ParserEngine(definition, StatefulPolicy(refusedState, new RootExitFrameManager()), ParserResourceLimits.Default with { MaxRetainedStates = 26688 });
        var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => refused.Parse(tokens));
        Assert.AreEqual(ParserResourceLimitKind.RetainedStates, refusal.Kind);
        Assert.AreEqual(ParserResourcePhase.Parser, refusal.Phase);
        Assert.AreEqual(26688L, refusal.Limit);
        Assert.AreEqual(26689L, refusal.Observed);
        Assert.AreEqual(26688L, refused.LastResourceUsage!.RetainedStates);

        // Reuse after the refusal on a smaller input: same tree and trace as a fresh engine.
        IReadOnlyList<Token> small = new CompiledGrammar(definition).Tokenize("a = a a ; a a = a");
        refusedState.Reset();
        ParseNode reused = refused.Parse(small);
        var freshState = new CounterState();
        ParseNode fresh = new ParserEngine(definition, StatefulPolicy(freshState, new RootExitFrameManager()), ParserResourceLimits.Default).Parse(small);
        Assert.AreEqual(fresh, reused);
        Assert.AreEqual(freshState.Trace, refusedState.Trace);
        Assert.AreEqual(freshState.Actions, refusedState.Actions);
    }

    /// <summary>Compares one budgeted operation on both registries: same result, or the same refusal.</summary>
    /// <param name="context">The failure context.</param>
    /// <param name="compact">The operation on the compact registry.</param>
    /// <param name="reference">The operation on the reference model.</param>
    /// <returns>1 when both refused, otherwise 0.</returns>
    private static int Compare(string context, Func<bool> compact, Func<bool> reference)
    {
        var referenceOutcome = Outcome(reference);
        var compactOutcome = Outcome(compact);
        Assert.AreEqual(referenceOutcome, compactOutcome, context);
        return referenceOutcome.StartsWith("refused", StringComparison.Ordinal) ? 1 : 0;
    }

    /// <summary>Runs an operation and describes its outcome.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The returned value or the refusal classification.</returns>
    private static string Outcome(Func<bool> operation)
    {
        try
        {
            return operation().ToString();
        }
        catch (ParserResourceLimitException exception)
        {
            return $"refused {exception.Kind} {exception.Phase} {exception.Limit} {exception.Observed} {exception.Offset}";
        }
    }

    /// <summary>Creates a parser budget with a retained-state limit.</summary>
    /// <param name="maxRetainedStates">The limit.</param>
    /// <returns>The budget.</returns>
    private static ParserResourceBudget CreateBudget(long maxRetainedStates)
        => new(ParserResourceLimits.Default with { MaxRetainedStates = maxRetainedStates }, ParserResourcePhase.Parser, static () => Offset);

    /// <summary>Draws a rule name, sometimes as a separate string instance with the same value.</summary>
    /// <param name="random">The generator.</param>
    /// <returns>The name.</returns>
    private static string RandomRule(Random random) => random.Next(3) switch
    {
        0 => "a",
        1 => "b",
        _ => new string(['a']),
    };

    /// <summary>Draws a visited-state key from a small domain so duplicates are frequent; some keys use the overflow set.</summary>
    /// <param name="random">The generator.</param>
    /// <returns>The key.</returns>
    private static ParserStateKey RandomState(Random random)
    {
        int alternative = random.Next(10) switch
        {
            0 => 63,
            1 => 64,
            2 => -1,
            _ => random.Next(3),
        };
        int element = random.Next(5) == 0 ? random.Next(3) : alternative;
        return new ParserStateKey(RandomRule(random), random.Next(3), alternative, element, random.Next(2));
    }

    /// <summary>Draws an invocation key from a small domain.</summary>
    /// <param name="random">The generator.</param>
    /// <returns>The key.</returns>
    private static RuleInvocationKey RandomInvocation(Random random)
        => new(RandomRule(random), random.Next(3), random.Next(2), new ParserExecutionStateKey((ulong)random.Next(2)));

    /// <summary>Draws a continuation key from a small domain.</summary>
    /// <param name="random">The generator.</param>
    /// <returns>The key.</returns>
    private static ContinuationKey RandomContinuation(Random random)
        => new(RandomRule(random), random.Next(2), random.Next(2), random.Next(3), random.Next(2));

    /// <summary>Creates a distinct parse node instance.</summary>
    /// <returns>The node.</returns>
    private static ParseNode CreateNode() => new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "result");

    /// <summary>Builds the stateful grammar and the probe's 1,000-item input.</summary>
    /// <returns>The definition and the tokens.</returns>
    private static (ParserDefinition Definition, IReadOnlyList<Token> Tokens) StatefulInput()
    {
        ParserDefinition definition = Antlr4GrammarConverter.Parse(StatefulGrammar);
        string text = string.Join(" ", Enumerable.Range(0, 1000).Select(i => (i % 3) switch { 0 => "a", 1 => "a = a", _ => "a ;" }));
        return (definition, new CompiledGrammar(definition).Tokenize(text));
    }

    /// <summary>Creates the runtime policy of the stateful scenario.</summary>
    /// <param name="state">The counter state.</param>
    /// <param name="frames">The frame manager.</param>
    /// <returns>The policy.</returns>
    private static ParserRuntimeFeaturePolicy StatefulPolicy(CounterState state, RootExitFrameManager frames)
        => ParserRuntimeFeaturePolicy.Default with
        {
            ExecutionStateManager = state,
            ParserActionExecutor = state,
            SemanticPredicateEvaluator = state,
            RuleInvocationFrameManager = frames,
        };

    /// <summary>Counts the distinct entries of the registry and of the look-ahead cache of an engine during a parse.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The entry count.</returns>
    private static long DistinctEntries(ParserEngine engine)
    {
        var registry = (ParserStateRegistry)typeof(ParserEngine).GetField("_stateRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        object cache = typeof(ParserEngine).GetField("_lookaheadCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        var results = (System.Collections.ICollection)cache.GetType().GetField("_results", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache)!;
        return registry.VisitedStateCount + registry.ContinuationCount + registry.CompletedResultCount + results.Count;
    }

    /// <summary>Calls <see cref="OnRootExit"/> when the outermost rule invocation exits, while the registry is still populated.</summary>
    private sealed class RootExitFrameManager : IParserRuleInvocationFrameManager
    {
        /// <summary>The current rule depth.</summary>
        private int _depth;

        /// <summary>Gets or sets the callback.</summary>
        public Action? OnRootExit { get; set; }

        /// <inheritdoc />
        public ParserRuleInvocationFrame? Current => null;

        /// <inheritdoc />
        public ParserRuleInvocationFrame Enter(string ruleName, int inputPosition, ParserRuleInvocationDescriptor? descriptor = null)
        {
            _depth++;
            return NullParserRuleInvocationFrameManager.Instance.Enter(ruleName, inputPosition, descriptor);
        }

        /// <inheritdoc />
        public void Exit(ParserRuleInvocationFrame frame, bool succeeded)
        {
            _depth--;
            if (_depth == 0) OnRootExit?.Invoke();
        }
    }

    /// <summary>
    /// Counter execution state: <c>bump</c> increments it, <c>even</c> accepts even values, the state key is the counter,
    /// and every action, predicate and restoration is traced.
    /// </summary>
    private sealed class CounterState : IParserExecutionStateManager, IParserActionExecutor, ISemanticPredicateEvaluator
    {
        /// <summary>The trace builder.</summary>
        private readonly StringBuilder _trace = new();

        /// <summary>The counter.</summary>
        private int _value;

        /// <summary>Gets the executed action count.</summary>
        public long Actions { get; private set; }

        /// <summary>Gets the trace.</summary>
        public string Trace => _trace.ToString();

        /// <summary>Resets the counter, the action count and the trace.</summary>
        public void Reset()
        {
            _value = 0;
            Actions = 0;
            _trace.Clear();
        }

        /// <inheritdoc />
        public object Capture() => _value;

        /// <inheritdoc />
        public void Restore(object snapshot)
        {
            _value = (int)snapshot;
            _trace.Append('R').Append(_value).Append(';');
        }

        /// <inheritdoc />
        public ParserExecutionStateKey GetCurrentStateKey() => new((ulong)_value);

        /// <inheritdoc />
        public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
        {
            _value++;
            Actions++;
            _trace.Append('A').Append(context.Rule.Name).Append('@').Append(context.InputPosition).Append('=').Append(_value).Append(';');
            return ParserActionExecutionOutcome.Executed;
        }

        /// <inheritdoc />
        public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
        {
            bool satisfied = _value % 2 == 0;
            _trace.Append('P').Append(context.Rule.Name).Append('@').Append(context.InputPosition).Append('=').Append(satisfied ? '1' : '0').Append(';');
            return satisfied ? SemanticPredicateEvaluationOutcome.Satisfied : SemanticPredicateEvaluationOutcome.Rejected;
        }
    }

    /// <summary>
    /// The historical registry storage (commit 6e7892a0), kept verbatim as the reference model: one set of visited states,
    /// one continuation set per invocation, one result list per invocation.
    /// </summary>
    private sealed class ReferenceRegistry
    {
        /// <summary>Visited states.</summary>
        private readonly HashSet<ParserStateKey> _visitedStates = [];

        /// <summary>Continuations per invocation.</summary>
        private readonly Dictionary<RuleInvocationKey, HashSet<ContinuationKey>> _continuations = [];

        /// <summary>Completed results per invocation.</summary>
        private readonly Dictionary<RuleInvocationKey, List<ParserRuleResult>> _completedResults = [];

        /// <summary>Gets the visited-state count.</summary>
        public long VisitedStateCount => _visitedStates.Count;

        /// <summary>Gets the continuation count.</summary>
        public int ContinuationCount => _continuations.Values.Sum(static set => set.Count);

        /// <summary>Gets the number of invocations with continuations.</summary>
        public int ContinuationInvocationCount => _continuations.Count;

        /// <summary>Gets the completed-result count.</summary>
        public long CompletedResultCount => _completedResults.Values.Sum(static list => list.Count);

        /// <summary>Historical <c>TryEnterState</c> (budgeted when <paramref name="budget"/> is not null).</summary>
        /// <param name="key">The key.</param>
        /// <param name="budget">The budget.</param>
        /// <returns>Whether the state is new.</returns>
        public bool TryEnterState(ParserStateKey key, ParserResourceBudget? budget)
        {
            if (budget is null)
            {
                return _visitedStates.Add(key);
            }

            if (_visitedStates.Contains(key))
            {
                return false;
            }

            budget.ChargeRetainedState();
            return _visitedStates.Add(key);
        }

        /// <summary>Historical <c>AddContinuation</c>.</summary>
        /// <param name="invocation">The invocation.</param>
        /// <param name="continuation">The continuation.</param>
        /// <param name="budget">The budget.</param>
        /// <returns>Whether the continuation is new.</returns>
        public bool AddContinuation(RuleInvocationKey invocation, ContinuationKey continuation, ParserResourceBudget? budget)
        {
            if (!_continuations.TryGetValue(invocation, out var set))
            {
                budget?.ChargeRetainedState();
                set = [];
                _continuations[invocation] = set;
                return set.Add(continuation);
            }

            if (set.Contains(continuation))
            {
                return false;
            }

            budget?.ChargeRetainedState();
            return set.Add(continuation);
        }

        /// <summary>Historical <c>AddCompletedResult</c>.</summary>
        /// <param name="invocation">The invocation.</param>
        /// <param name="result">The result.</param>
        /// <param name="budget">The budget.</param>
        /// <returns>Whether the result was stored.</returns>
        public bool AddCompletedResult(RuleInvocationKey invocation, ParserRuleResult result, ParserResourceBudget? budget)
        {
            if (_completedResults.TryGetValue(invocation, out var list))
            {
                foreach (var existing in list)
                {
                    if (existing.EndPosition == result.EndPosition
                        && existing.IsFailure == result.IsFailure
                        && ReferenceEquals(existing.Node, result.Node))
                    {
                        return false;
                    }
                }
            }

            budget?.ChargeRetainedState();
            if (list is null)
            {
                list = [];
                _completedResults[invocation] = list;
            }

            list.Add(result);
            return true;
        }

        /// <summary>Historical <c>GetContinuations</c>.</summary>
        /// <param name="invocation">The invocation.</param>
        /// <returns>The continuations.</returns>
        public IReadOnlyList<ContinuationKey> GetContinuations(RuleInvocationKey invocation)
            => _continuations.TryGetValue(invocation, out var set) ? [.. set] : [];

        /// <summary>Historical <c>GetCompletedResults</c>.</summary>
        /// <param name="invocation">The invocation.</param>
        /// <returns>The results.</returns>
        public IReadOnlyList<ParserRuleResult> GetCompletedResults(RuleInvocationKey invocation)
            => _completedResults.TryGetValue(invocation, out var list) ? list : [];

        /// <summary>Historical <c>TryGetReusableResult</c>.</summary>
        /// <param name="invocation">The invocation.</param>
        /// <param name="result">The reusable result.</param>
        /// <returns>Whether a reusable result exists.</returns>
        public bool TryGetReusableResult(RuleInvocationKey invocation, out ParserRuleResult result)
        {
            result = default;
            if (!_completedResults.TryGetValue(invocation, out var list) || list.Count == 0)
            {
                return false;
            }

            var success = list.FirstOrDefault(static item => !item.IsFailure && item.Node is not null);
            if (success.Node is not null)
            {
                result = success;
                return true;
            }

            var failure = list.FirstOrDefault(static item => item.IsFailure);
            if (!failure.IsFailure)
            {
                return false;
            }

            result = failure;
            return true;
        }
    }
}

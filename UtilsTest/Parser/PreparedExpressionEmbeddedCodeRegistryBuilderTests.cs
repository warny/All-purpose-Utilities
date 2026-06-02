using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Expressions;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates explicit prepared expression registry building from parser model metadata.
/// </summary>
[TestClass]
public class PreparedExpressionEmbeddedCodeRegistryBuilderTests
{
    /// <summary>
    /// Verifies that a null parser definition is rejected before any preparation work starts.
    /// </summary>
    [TestMethod]
    public void Build_WhenDefinitionIsNull_ThrowsArgumentNullException()
    {
        var preparer = new FakePreparer();

        Assert.ThrowsException<ArgumentNullException>(() => PreparedExpressionEmbeddedCodeRegistryBuilder.Build(null!, preparer));
    }

    /// <summary>
    /// Verifies that a null preparer is rejected before model traversal starts.
    /// </summary>
    [TestMethod]
    public void Build_WhenPreparerIsNull_ThrowsArgumentNullException()
    {
        var definition = CreateDefinition(CreateRule("start", new Sequence([])));

        Assert.ThrowsException<ArgumentNullException>(() => PreparedExpressionEmbeddedCodeRegistryBuilder.Build(definition, null!));
    }

    /// <summary>
    /// Verifies that a validating predicate is prepared and added to the registry with matching runtime indexes.
    /// </summary>
    [TestMethod]
    public void Build_WhenSemanticPredicateSucceeds_AddsArtifactToRegistry()
    {
        var predicate = new ValidatingPredicate("inputPosition == 0");
        var rule = CreateRule("start", new Sequence([predicate]));
        var preparer = new FakePreparer();

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), preparer);

        Assert.AreEqual(1, result.SuccessfulSemanticPredicates.Count);
        Assert.AreEqual(0, result.NonSuccessEntries.Count);
        Assert.IsFalse(result.HasFailures);
        Assert.IsTrue(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, predicate, 0, 0), out var artifact));
        Assert.AreSame(preparer.PreparedPredicates[0], artifact);
    }

    /// <summary>
    /// Verifies that an inline parser action is prepared and added to the registry with matching runtime indexes.
    /// </summary>
    [TestMethod]
    public void Build_WhenParserInlineActionSucceeds_AddsArtifactToRegistry()
    {
        var action = new EmbeddedAction("record", ActionContext.Alternative, ActionPosition.Inline, []);
        var rule = CreateRule("start", new Sequence([new LiteralMatch("x"), action]));
        var preparer = new FakePreparer();

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), preparer);

        Assert.AreEqual(1, result.SuccessfulParserActions.Count);
        Assert.AreEqual(0, result.NonSuccessEntries.Count);
        Assert.IsFalse(result.HasFailures);
        Assert.IsTrue(result.Registry.TryGetParserAction(CreateActionContext(rule, action, 0, 1), out var artifact));
        Assert.AreSame(preparer.PreparedActions[0], artifact);
    }

    /// <summary>
    /// Verifies that successful entries retain source, key, and registry state metadata.
    /// </summary>
    [TestMethod]
    public void Build_WhenPreparationSucceeds_RecordsSuccessEntries()
    {
        var predicate = new ValidatingPredicate("true");
        var action = new EmbeddedAction("touch", ActionContext.Alternative, ActionPosition.Inline, []);
        var rule = CreateRule("start", new Sequence([predicate, action]));

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), new FakePreparer());

        var predicateEntry = result.SuccessfulSemanticPredicates.Single();
        var actionEntry = result.SuccessfulParserActions.Single();
        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, predicateEntry.Status);
        Assert.AreEqual(EmbeddedCodeKind.SemanticPredicate, predicateEntry.Source.Kind);
        Assert.AreEqual("start", predicateEntry.RuleName);
        Assert.AreEqual(0, predicateEntry.Key!.AlternativeIndex);
        Assert.AreEqual(0, predicateEntry.Key.ElementIndex);
        Assert.IsTrue(predicateEntry.WasAddedToRegistry);
        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, actionEntry.Status);
        Assert.AreEqual(EmbeddedCodeKind.ParserInlineAction, actionEntry.Source.Kind);
        Assert.AreEqual(1, actionEntry.Key!.ElementIndex);
        Assert.IsTrue(actionEntry.WasAddedToRegistry);
    }

    /// <summary>
    /// Verifies that non-success preparation results are retained without registry insertion.
    /// </summary>
    [TestMethod]
    public void Build_WhenPreparerReturnsNonSuccess_RecordsFailureEntry()
    {
        var exception = new InvalidOperationException("compile failed");
        var predicate = new ValidatingPredicate("bad");
        var rule = CreateRule("start", new Sequence([predicate]));
        var preparer = new FakePreparer
        {
            PredicateResultFactory = (source, _) => EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate>.CompilationFailed(
                exception,
                new object?[] { source.SourceText, exception.Message })
        };

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), preparer);

        var entry = result.NonSuccessEntries.Single();
        Assert.IsTrue(result.HasFailures);
        Assert.AreEqual(EmbeddedCodePreparationStatus.CompilationFailed, entry.Status);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeCompilationFailed, entry.DiagnosticDescriptor);
        Assert.AreSame(exception, entry.Exception);
        Assert.AreEqual("bad", entry.DiagnosticArguments[0]);
        Assert.IsFalse(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, predicate, 0, 0), out _));
    }

    /// <summary>
    /// Verifies that duplicate keys are detected and the first registered artifact is preserved.
    /// </summary>
    [TestMethod]
    public void Build_WhenDuplicateKeyIsPrepared_RecordsDuplicateWithoutOverwrite()
    {
        var first = new ValidatingPredicate("same");
        var second = new ValidatingPredicate("same");
        var rule = CreateRule("start", new Sequence([first, second]));
        var preparer = new FakePreparer
        {
            PredicateResultFactory = (source, context) => EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate>.Success(
                new PreparedExpressionSemanticPredicate(
                    new EmbeddedCodeSource(source.SourceText, source.Kind, source.RuleName, alternativeIndex: 0, elementIndex: 0),
                    context,
                    _ => true))
        };

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), preparer);

        Assert.AreEqual(1, result.SuccessfulSemanticPredicates.Count);
        Assert.AreEqual(1, result.DuplicateEntries.Count);
        Assert.IsTrue(result.HasFailures);
        Assert.IsTrue(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, first, 0, 0), out var artifact));
        Assert.AreSame(preparer.PreparedPredicates[0], artifact);
    }

    /// <summary>
    /// Verifies that the builder scans without mutating the source parser model.
    /// </summary>
    [TestMethod]
    public void Build_DoesNotModifySourceModel()
    {
        var sequence = new Sequence([new ValidatingPredicate("true")]);
        var rule = CreateRule("start", sequence);
        var definition = CreateDefinition(rule);
        var originalContent = rule.Content;
        var originalRules = definition.ParserRules;

        _ = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(definition, new FakePreparer());

        Assert.AreSame(originalContent, rule.Content);
        Assert.AreSame(originalRules, definition.ParserRules);
        Assert.AreEqual(1, ((Sequence)rule.Content.Alternatives[0].Content).Items.Count);
    }

    /// <summary>
    /// Verifies that builder keys are compatible with prepared runtime adapters.
    /// </summary>
    [TestMethod]
    public void Build_WhenArtifactsAreRegistered_RuntimeAdaptersFindThemByContext()
    {
        var predicate = new ValidatingPredicate("true");
        var action = new EmbeddedAction("count", ActionContext.Alternative, ActionPosition.Inline, []);
        var rule = CreateRule("start", new Sequence([predicate, action]));
        var actionCalls = 0;
        var preparer = new FakePreparer
        {
            ActionResultFactory = (source, context) => EmbeddedCodePreparationResult<PreparedExpressionParserAction>.Success(
                new PreparedExpressionParserAction(source, context, _ => actionCalls++))
        };

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), preparer);
        var evaluator = new PreparedExpressionSemanticPredicateEvaluator(result.Registry);
        var executor = new PreparedExpressionParserActionExecutor(result.Registry);

        var predicateOutcome = evaluator.Evaluate(CreatePredicateContext(rule, predicate, 0, 0));
        var actionOutcome = executor.Execute(CreateActionContext(rule, action, 0, 1));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.Satisfied, predicateOutcome.Status);
        Assert.AreEqual(ParserActionExecutionStatus.Executed, actionOutcome.Status);
        Assert.AreEqual(1, actionCalls);
    }

    /// <summary>
    /// Verifies that rule lifecycle and grammar-level actions are skipped rather than prepared.
    /// </summary>
    [TestMethod]
    public void Build_WhenNonInlineActionsExist_SkipsWithoutPreparing()
    {
        var init = new EmbeddedAction("init", ActionContext.Rule, ActionPosition.Before, []);
        var after = new EmbeddedAction("after", ActionContext.Rule, ActionPosition.After, []);
        var grammarAction = new GrammarAction("members", "int value;");
        var inlineNonParser = new EmbeddedAction("before", ActionContext.Alternative, ActionPosition.Before, []);
        var rule = CreateRule("start", new Sequence([inlineNonParser]), init, after);
        var preparer = new FakePreparer();

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule, [grammarAction]), preparer);

        Assert.AreEqual(4, result.SkippedEntries.Count);
        Assert.AreEqual(0, preparer.SemanticPredicateCalls.Count);
        Assert.AreEqual(0, preparer.ParserActionCalls.Count);
        Assert.IsTrue(result.SkippedEntries.Any(static entry => entry.Source.Kind == EmbeddedCodeKind.GrammarAction));
        Assert.IsTrue(result.SkippedEntries.Any(static entry => entry.Source.Kind == EmbeddedCodeKind.RuleInitAction));
        Assert.IsTrue(result.SkippedEntries.Any(static entry => entry.Source.Kind == EmbeddedCodeKind.RuleAfterAction));
        Assert.IsTrue(result.SkippedEntries.Any(static entry => entry.Source.Kind == EmbeddedCodeKind.ParserInlineAction));
    }

    /// <summary>
    /// Verifies that builder options flow into the preparation context without requiring an expression compiler.
    /// </summary>
    [TestMethod]
    public void Build_UsesEmbeddedCodePreparerContractWithoutExpressionCompiler()
    {
        var predicate = new ValidatingPredicate("ruleName == \"start\"");
        var rule = CreateRule("start", new Sequence([predicate]));
        var symbols = new HashSet<EmbeddedCodeContextSymbol> { EmbeddedCodeContextSymbol.RuleName };
        var options = new PreparedExpressionEmbeddedCodeRegistryBuilderOptions
        {
            GrammarName = "ConfiguredGrammar",
            LanguageOrCompilerIdentity = "fake-language",
            SupportedSymbols = symbols
        };
        var preparer = new FakePreparer();

        _ = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), preparer, options);

        var call = preparer.SemanticPredicateCalls.Single();
        Assert.AreEqual("ConfiguredGrammar", call.Context.GrammarName);
        Assert.AreEqual("fake-language", call.Context.LanguageOrCompilerIdentity);
        Assert.AreEqual(EmbeddedCodeTarget.RuntimeInlineExpression, call.Context.Target);
        CollectionAssert.AreEqual(symbols.ToArray(), call.Context.SupportedSymbols.ToArray());
    }

    /// <summary>
    /// Verifies that alternation priority order is the key strategy used by the parser scheduler.
    /// </summary>
    [TestMethod]
    public void Build_UsesPriorityOrderedAlternativeIndexes()
    {
        var secondPriorityPredicate = new ValidatingPredicate("second");
        var firstPriorityPredicate = new ValidatingPredicate("first");
        var rule = CreateRule(
            "start",
            new Alternation([
                new Alternative(10, Associativity.Left, new Sequence([secondPriorityPredicate])),
                new Alternative(0, Associativity.Left, new Sequence([firstPriorityPredicate]))
            ]));

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), new FakePreparer());

        Assert.IsTrue(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, firstPriorityPredicate, 0, 0), out _));
        Assert.IsTrue(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, secondPriorityPredicate, 1, 0), out _));
    }


    /// <summary>
    /// Verifies that predicates directly inside quantifiers use the runtime quantifier element index.
    /// </summary>
    [TestMethod]
    public void Build_WhenPredicateIsInsideQuantifier_UsesRuntimeQuantifierIndexes()
    {
        var predicate = new ValidatingPredicate("inside-quantifier");
        var quantifier = new Quantifier(predicate, 0, 1);
        var rule = CreateRule("start", new Sequence([new LiteralMatch("a"), new LiteralMatch("b"), quantifier]));

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), new FakePreparer());

        Assert.IsTrue(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, predicate, 0, 0), out _));
        Assert.IsFalse(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, predicate, 0, 2), out _));
    }

    /// <summary>
    /// Verifies that inline actions directly inside quantifiers use the runtime quantifier element index.
    /// </summary>
    [TestMethod]
    public void Build_WhenActionIsInsideQuantifier_UsesRuntimeQuantifierIndexes()
    {
        var action = new EmbeddedAction("inside-quantifier-action", ActionContext.Alternative, ActionPosition.Inline, []);
        var quantifier = new Quantifier(action, 0, 1);
        var rule = CreateRule("start", new Sequence([new LiteralMatch("a"), new LiteralMatch("b"), quantifier]));

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), new FakePreparer());

        Assert.IsTrue(result.Registry.TryGetParserAction(CreateActionContext(rule, action, 0, 0), out _));
        Assert.IsFalse(result.Registry.TryGetParserAction(CreateActionContext(rule, action, 0, 2), out _));
    }

    /// <summary>
    /// Verifies that predicates inside negation use the runtime negation probe element index.
    /// </summary>
    [TestMethod]
    public void Build_WhenPredicateIsInsideNegation_UsesRuntimeNegationIndexes()
    {
        var predicate = new ValidatingPredicate("inside-negation");
        var negation = new Negation(predicate);
        var rule = CreateRule("start", new Sequence([new LiteralMatch("a"), new LiteralMatch("b"), negation]));

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), new FakePreparer());

        Assert.IsTrue(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, predicate, 0, 0), out _));
        Assert.IsFalse(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, predicate, 0, 2), out _));
    }

    /// <summary>
    /// Verifies that inline actions inside negation use the runtime negation probe element index.
    /// </summary>
    [TestMethod]
    public void Build_WhenActionIsInsideNegation_UsesRuntimeNegationIndexes()
    {
        var action = new EmbeddedAction("inside-negation-action", ActionContext.Alternative, ActionPosition.Inline, []);
        var negation = new Negation(action);
        var rule = CreateRule("start", new Sequence([new LiteralMatch("a"), new LiteralMatch("b"), negation]));

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule), new FakePreparer());

        Assert.IsTrue(result.Registry.TryGetParserAction(CreateActionContext(rule, action, 0, 0), out _));
        Assert.IsFalse(result.Registry.TryGetParserAction(CreateActionContext(rule, action, 0, 2), out _));
    }

    /// <summary>
    /// Verifies that left-recursive tails are scanned after removing the leading self-reference.
    /// </summary>
    [TestMethod]
    public void Build_WhenPredicateAndActionAreInLeftRecursiveTail_UsesRuntimeTailIndexes()
    {
        var action = new EmbeddedAction("tail-action", ActionContext.Alternative, ActionPosition.Inline, []);
        var predicate = new ValidatingPredicate("tail-predicate");
        var baseAlternative = new Alternative(0, Associativity.Left, new Sequence([new LiteralMatch("id")]));
        var recursiveAlternative = new Alternative(1, Associativity.Left, new Sequence([
            new RuleRef("expr"),
            action,
            new LiteralMatch("+"),
            predicate,
            new RuleRef("expr")
        ]));
        var rule = CreateRule("expr", new Alternation([baseAlternative, recursiveAlternative]));
        var leftRecursiveInfo = new LeftRecursiveRuleInfo
        {
            Rule = rule,
            BaseAlternatives = [baseAlternative],
            RecursiveAlternatives = [recursiveAlternative]
        };

        var result = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(CreateDefinition(rule, leftRecursiveRules: new Dictionary<string, LeftRecursiveRuleInfo>
        {
            [rule.Name] = leftRecursiveInfo
        }), new FakePreparer());

        Assert.IsTrue(result.Registry.TryGetParserAction(CreateActionContext(rule, action, 0, 0), out _));
        Assert.IsTrue(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, predicate, 0, 2), out _));
        Assert.IsFalse(result.Registry.TryGetParserAction(CreateActionContext(rule, action, 0, 1), out _));
        Assert.IsFalse(result.Registry.TryGetSemanticPredicate(CreatePredicateContext(rule, predicate, 0, 3), out _));
    }

    /// <summary>
    /// Creates a parser definition for tests.
    /// </summary>
    /// <param name="rule">Parser rule to include.</param>
    /// <param name="actions">Optional grammar-level actions.</param>
    /// <param name="leftRecursiveRules">Optional left-recursive rule metadata.</param>
    /// <returns>A parser definition containing the supplied rule.</returns>
    private static ParserDefinition CreateDefinition(
        Rule rule,
        IReadOnlyList<GrammarAction>? actions = null,
        IReadOnlyDictionary<string, LeftRecursiveRuleInfo>? leftRecursiveRules = null) =>
        new ParserDefinition(
            "G",
            GrammarType.Combined,
            null,
            actions ?? [],
            [],
            [],
            [rule],
            rule)
        {
            LeftRecursiveRules = leftRecursiveRules ?? new Dictionary<string, LeftRecursiveRuleInfo>()
        };

    /// <summary>
    /// Creates a parser rule around supplied content.
    /// </summary>
    /// <param name="name">Rule name.</param>
    /// <param name="content">Rule content or full alternation.</param>
    /// <param name="initAction">Optional rule initialization action.</param>
    /// <param name="afterAction">Optional rule finalization action.</param>
    /// <returns>A parser rule.</returns>
    private static Rule CreateRule(string name, RuleContent content, EmbeddedAction? initAction = null, EmbeddedAction? afterAction = null)
    {
        var alternation = content as Alternation ?? new Alternation([new Alternative(0, Associativity.Left, content)]);
        return new Rule(name, 0, false, alternation, InitAction: initAction, AfterAction: afterAction, Kind: RuleKind.Parser);
    }

    /// <summary>
    /// Creates a runtime semantic predicate context for registry lookup.
    /// </summary>
    /// <param name="rule">Runtime rule.</param>
    /// <param name="predicate">Predicate model node.</param>
    /// <param name="alternativeIndex">Expected alternative index.</param>
    /// <param name="elementIndex">Expected element index.</param>
    /// <returns>A semantic predicate evaluation context.</returns>
    private static SemanticPredicateEvaluationContext CreatePredicateContext(Rule rule, ValidatingPredicate predicate, int alternativeIndex, int elementIndex) =>
        new(rule, predicate, predicate.Code, 0, alternativeIndex, elementIndex);

    /// <summary>
    /// Creates a runtime parser action context for registry lookup.
    /// </summary>
    /// <param name="rule">Runtime rule.</param>
    /// <param name="action">Action model node.</param>
    /// <param name="alternativeIndex">Expected alternative index.</param>
    /// <param name="elementIndex">Expected element index.</param>
    /// <returns>A parser action execution context.</returns>
    private static ParserActionExecutionContext CreateActionContext(Rule rule, EmbeddedAction action, int alternativeIndex, int elementIndex) =>
        new(rule, action, action.RawCode, 0, alternativeIndex, elementIndex);

    /// <summary>
    /// Fake embedded-code preparer that records calls and returns already-prepared delegates.
    /// </summary>
    private sealed class FakePreparer : IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction>
    {
        /// <summary>
        /// Gets semantic predicate preparation calls.
        /// </summary>
        public List<(EmbeddedCodeSource Source, EmbeddedCodePreparationContext Context)> SemanticPredicateCalls { get; } = [];

        /// <summary>
        /// Gets parser action preparation calls.
        /// </summary>
        public List<(EmbeddedCodeSource Source, EmbeddedCodePreparationContext Context)> ParserActionCalls { get; } = [];

        /// <summary>
        /// Gets prepared predicate artifacts returned by this fake.
        /// </summary>
        public List<PreparedExpressionSemanticPredicate> PreparedPredicates { get; } = [];

        /// <summary>
        /// Gets prepared parser action artifacts returned by this fake.
        /// </summary>
        public List<PreparedExpressionParserAction> PreparedActions { get; } = [];

        /// <summary>
        /// Gets or sets the semantic predicate result factory.
        /// </summary>
        public Func<EmbeddedCodeSource, EmbeddedCodePreparationContext, EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate>> PredicateResultFactory { get; set; }

        /// <summary>
        /// Gets or sets the parser action result factory.
        /// </summary>
        public Func<EmbeddedCodeSource, EmbeddedCodePreparationContext, EmbeddedCodePreparationResult<PreparedExpressionParserAction>> ActionResultFactory { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FakePreparer"/> class.
        /// </summary>
        public FakePreparer()
        {
            PredicateResultFactory = CreateDefaultPredicateResult;
            ActionResultFactory = CreateDefaultActionResult;
        }

        /// <summary>
        /// Prepares a semantic predicate by returning an already-available delegate.
        /// </summary>
        /// <param name="source">Predicate source metadata.</param>
        /// <param name="context">Preparation context.</param>
        /// <returns>A preparation result.</returns>
        public EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate> PrepareSemanticPredicate(EmbeddedCodeSource source, EmbeddedCodePreparationContext context)
        {
            SemanticPredicateCalls.Add((source, context));
            var result = PredicateResultFactory(source, context);
            if (result.Artifact is not null)
            {
                PreparedPredicates.Add(result.Artifact);
            }

            return result;
        }

        /// <summary>
        /// Prepares a parser action by returning an already-available delegate.
        /// </summary>
        /// <param name="source">Action source metadata.</param>
        /// <param name="context">Preparation context.</param>
        /// <returns>A preparation result.</returns>
        public EmbeddedCodePreparationResult<PreparedExpressionParserAction> PrepareParserAction(EmbeddedCodeSource source, EmbeddedCodePreparationContext context)
        {
            ParserActionCalls.Add((source, context));
            var result = ActionResultFactory(source, context);
            if (result.Artifact is not null)
            {
                PreparedActions.Add(result.Artifact);
            }

            return result;
        }

        /// <summary>
        /// Creates the default successful predicate result.
        /// </summary>
        /// <param name="source">Predicate source metadata.</param>
        /// <param name="context">Preparation context.</param>
        /// <returns>A successful predicate result.</returns>
        private static EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate> CreateDefaultPredicateResult(EmbeddedCodeSource source, EmbeddedCodePreparationContext context) =>
            EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate>.Success(new PreparedExpressionSemanticPredicate(source, context, _ => true));

        /// <summary>
        /// Creates the default successful parser action result.
        /// </summary>
        /// <param name="source">Action source metadata.</param>
        /// <param name="context">Preparation context.</param>
        /// <returns>A successful parser action result.</returns>
        private static EmbeddedCodePreparationResult<PreparedExpressionParserAction> CreateDefaultActionResult(EmbeddedCodeSource source, EmbeddedCodePreparationContext context) =>
            EmbeddedCodePreparationResult<PreparedExpressionParserAction>.Success(new PreparedExpressionParserAction(source, context, _ => { }));
    }
}

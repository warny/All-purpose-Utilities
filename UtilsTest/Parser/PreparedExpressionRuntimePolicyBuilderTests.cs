using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Expressions;
using Utils.Parser.Bootstrap;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Expressions;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates the opt-in prepared expression runtime policy integration end to end.
/// </summary>
[TestClass]
public class PreparedExpressionRuntimePolicyBuilderTests
{
    /// <summary>
    /// Verifies that a null parser definition is rejected before preparation starts.
    /// </summary>
    [TestMethod]
    public void Build_WhenDefinitionIsNull_ThrowsArgumentNullException()
    {
        var compiler = new TrackingExpressionCompiler();

        Assert.ThrowsException<ArgumentNullException>(() => PreparedExpressionRuntimePolicyBuilder.Build(null!, compiler));
    }

    /// <summary>
    /// Verifies that a null expression compiler is rejected before preparation starts.
    /// </summary>
    [TestMethod]
    public void Build_WhenCompilerIsNull_ThrowsArgumentNullException()
    {
        var definition = CreatePredicateDefinition("predicateTrue");

        Assert.ThrowsException<ArgumentNullException>(() => PreparedExpressionRuntimePolicyBuilder.Build(definition, null!));
    }

    /// <summary>
    /// Verifies that the builder returns a policy configured with prepared registry-backed adapters.
    /// </summary>
    [TestMethod]
    public void Build_ReturnsPolicyWithPreparedEvaluatorAndExecutor()
    {
        var definition = CreateActionDefinition("record");
        var compiler = new TrackingExpressionCompiler();

        var result = PreparedExpressionRuntimePolicyBuilder.Build(definition, compiler);

        Assert.IsInstanceOfType<PreparedExpressionSemanticPredicateEvaluator>(result.Policy.SemanticPredicateEvaluator);
        Assert.IsInstanceOfType<PreparedExpressionParserActionExecutor>(result.Policy.ParserActionExecutor);
        Assert.AreSame(result.Registry, result.RegistryBuildResult.Registry);
    }

    /// <summary>
    /// Verifies that registry audit metadata is exposed directly through the integration result.
    /// </summary>
    [TestMethod]
    public void Build_ReturnsRegistryBuildResult()
    {
        var definition = CreatePredicateDefinition("predicateTrue");
        var compiler = new TrackingExpressionCompiler();

        var result = PreparedExpressionRuntimePolicyBuilder.Build(definition, compiler);

        Assert.AreEqual(1, result.RegistryBuildResult.SuccessfulSemanticPredicates.Count);
        Assert.AreEqual(0, result.RegistryBuildResult.NonSuccessEntries.Count);
        Assert.IsFalse(result.HasFailures);
    }

    /// <summary>
    /// Verifies that integration options flow into preparation contexts and compiler symbol exposure.
    /// </summary>
    [TestMethod]
    public void Build_UsesOptionsForGrammarNameLanguageAndSupportedSymbols()
    {
        var definition = CreatePredicateDefinition("predicateTrue");
        var compiler = new TrackingExpressionCompiler();

        var result = PreparedExpressionRuntimePolicyBuilder.Build(
            definition,
            compiler,
            new PreparedExpressionRuntimePolicyBuilderOptions
            {
                GrammarName = "CustomGrammar",
                LanguageOrCompilerIdentity = "tracking-compiler",
                SupportedSymbols = new HashSet<EmbeddedCodeContextSymbol> { EmbeddedCodeContextSymbol.RuleName }
            });

        var rule = result.RegistryBuildResult.SuccessfulSemanticPredicates.Single().Key!.RuleName;
        var sourceText = result.RegistryBuildResult.SuccessfulSemanticPredicates.Single().Key!.SourceText;
        var context = new SemanticPredicateEvaluationContext(definition.RootRule, new ValidatingPredicate(sourceText), sourceText, 0, 0, 0);
        Assert.IsTrue(result.Registry.TryGetSemanticPredicate(context, out var artifact));
        Assert.AreEqual("start", rule);
        Assert.AreEqual("CustomGrammar", artifact!.PreparationContext.GrammarName);
        Assert.AreEqual("tracking-compiler", artifact.PreparationContext.LanguageOrCompilerIdentity);
        Assert.IsTrue(compiler.LastSymbols!.ContainsKey("ruleName"));
        Assert.IsFalse(compiler.LastSymbols.ContainsKey("inputPosition"));
    }

    /// <summary>
    /// Verifies that the builder preserves unrelated base policy components while replacing prepared adapters.
    /// </summary>
    [TestMethod]
    public void Build_WhenBasePolicyIsProvided_PreservesRuntimeObserver()
    {
        var observer = new NoOpRuntimeObserver();
        var basePolicy = ParserRuntimeFeaturePolicy.Default with { RuntimeObserver = observer };
        var definition = CreatePredicateDefinition("predicateTrue");
        var compiler = new TrackingExpressionCompiler();

        var result = PreparedExpressionRuntimePolicyBuilder.Build(
            definition,
            compiler,
            new PreparedExpressionRuntimePolicyBuilderOptions { BasePolicy = basePolicy });

        Assert.AreSame(observer, result.Policy.RuntimeObserver);
        Assert.IsInstanceOfType<PreparedExpressionSemanticPredicateEvaluator>(result.Policy.SemanticPredicateEvaluator);
        Assert.IsInstanceOfType<PreparedExpressionParserActionExecutor>(result.Policy.ParserActionExecutor);
    }


    /// <summary>
    /// Verifies that building the integration prepares artifacts but does not execute parser runtime delegates.
    /// </summary>
    [TestMethod]
    public void Build_DoesNotInvokeParserEngine()
    {
        var definition = CreatePredicateAndActionDefinition("predicateTrue", "record");
        var compiler = new TrackingExpressionCompiler();

        _ = PreparedExpressionRuntimePolicyBuilder.Build(definition, compiler);

        Assert.AreEqual(2, compiler.CompilationCount);
        Assert.AreEqual(0, compiler.PredicateTrueExecutionCount);
        Assert.AreEqual(0, compiler.ActionExecutionCount);
    }

    /// <summary>
    /// Verifies that prepared predicates are compiled during policy construction and not during parsing.
    /// </summary>
    [TestMethod]
    public void Build_DoesNotCompileDuringEvaluateOrExecute()
    {
        var definition = CreatePredicateAndActionDefinition("predicateTrue", "record");
        var compiler = new TrackingExpressionCompiler();
        var integration = PreparedExpressionRuntimePolicyBuilder.Build(definition, compiler);
        var compilationCountAfterBuild = compiler.CompilationCount;
        var parser = new ParserEngine(definition, integration.Policy);
        var lexer = new LexerEngine(definition);

        var result = parser.Parse(lexer.Tokenize(new StringReader("a")));

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(compilationCountAfterBuild, compiler.CompilationCount);
        Assert.AreEqual(1, compiler.PredicateTrueExecutionCount);
        Assert.AreEqual(1, compiler.ActionExecutionCount);
    }

    /// <summary>
    /// Verifies that a prepared semantic predicate returning true accepts the parse through ParserEngine.
    /// </summary>
    [TestMethod]
    public void Parse_WhenPreparedSemanticPredicateReturnsTrue_ParsesSuccessfully()
    {
        var definition = CreatePredicateDefinition("predicateTrue");
        var compiler = new TrackingExpressionCompiler();

        var integration = PreparedExpressionRuntimePolicyBuilder.Build(definition, compiler);
        var result = ParseText(definition, integration.Policy, "a");

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1, integration.RegistryBuildResult.SuccessfulSemanticPredicates.Count);
        Assert.AreEqual(1, compiler.CompilationCount);
        Assert.AreEqual(1, compiler.PredicateTrueExecutionCount);
    }

    /// <summary>
    /// Verifies that a prepared semantic predicate returning false rejects the parse through ParserEngine.
    /// </summary>
    [TestMethod]
    public void Parse_WhenPreparedSemanticPredicateReturnsFalse_RejectsParse()
    {
        var definition = CreatePredicateDefinition("predicateFalse");
        var compiler = new TrackingExpressionCompiler();
        var integration = PreparedExpressionRuntimePolicyBuilder.Build(definition, compiler);

        var result = ParseText(definition, integration.Policy, "a");
        var sourceText = integration.RegistryBuildResult.SuccessfulSemanticPredicates.Single().Key!.SourceText;
        var directOutcome = integration.Policy.SemanticPredicateEvaluator.Evaluate(
            new SemanticPredicateEvaluationContext(definition.RootRule, new ValidatingPredicate(sourceText), sourceText, 0, 0, 0));

        Assert.IsInstanceOfType<ErrorNode>(result);
        Assert.AreEqual(SemanticPredicateEvaluationStatus.Rejected, directOutcome.Status);
        Assert.AreEqual(2, compiler.PredicateFalseExecutionCount);
    }

    /// <summary>
    /// Verifies that a prepared inline parser action runs during parsing and not during preparation.
    /// </summary>
    [TestMethod]
    public void Parse_WhenPreparedInlineActionExists_ExecutesActionDuringParsingOnly()
    {
        var definition = CreateActionDefinition("record");
        var compiler = new TrackingExpressionCompiler();
        var integration = PreparedExpressionRuntimePolicyBuilder.Build(definition, compiler);
        var executionCountAfterBuild = compiler.ActionExecutionCount;

        var result = ParseText(definition, integration.Policy, "a");

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(0, executionCountAfterBuild);
        Assert.AreEqual(1, integration.RegistryBuildResult.SuccessfulParserActions.Count);
        Assert.AreEqual(1, compiler.ActionExecutionCount);
    }

    /// <summary>
    /// Verifies that preparation failures remain visible and are not hidden by the integration builder.
    /// </summary>
    [TestMethod]
    public void Build_WhenPreparationFails_ExposesFailuresWithoutThrowing()
    {
        var definition = CreatePredicateDefinition("fail");
        var compiler = new TrackingExpressionCompiler();

        var result = PreparedExpressionRuntimePolicyBuilder.Build(definition, compiler);

        Assert.IsTrue(result.HasFailures);
        Assert.IsTrue(result.RegistryBuildResult.HasFailures);
        Assert.AreEqual(1, result.RegistryBuildResult.NonSuccessEntries.Count);
        Assert.AreEqual(0, result.RegistryBuildResult.SuccessfulSemanticPredicates.Count);
    }

    /// <summary>
    /// Parses text using the supplied definition and policy.
    /// </summary>
    /// <param name="definition">Parser definition used by the lexer and parser.</param>
    /// <param name="policy">Runtime feature policy supplied to the parser.</param>
    /// <param name="text">Input text to parse.</param>
    /// <returns>The parse result.</returns>
    private static ParseNode ParseText(ParserDefinition definition, ParserRuntimeFeaturePolicy policy, string text)
    {
        var lexer = new LexerEngine(definition);
        var parser = new ParserEngine(definition, policy);
        return parser.Parse(lexer.Tokenize(new StringReader(text)));
    }

    /// <summary>
    /// Creates a parser definition with one prepared semantic predicate before token <c>A</c>.
    /// </summary>
    /// <param name="predicateCode">Predicate source code.</param>
    /// <returns>A parser definition parsed from ANTLR text.</returns>
    private static ParserDefinition CreatePredicateDefinition(string predicateCode)
    {
        return Antlr4GrammarConverter.Parse(
            $$"""
            grammar P;
            start : { {{predicateCode}} }? A ;
            A : 'a' ;
            """,
            diagnostics: null);
    }

    /// <summary>
    /// Creates a parser definition with one inline parser action before token <c>A</c>.
    /// </summary>
    /// <param name="actionCode">Action source code.</param>
    /// <returns>A parser definition parsed from ANTLR text.</returns>
    private static ParserDefinition CreateActionDefinition(string actionCode)
    {
        return Antlr4GrammarConverter.Parse(
            $$"""
            grammar P;
            start : { {{actionCode}} } A ;
            A : 'a' ;
            """,
            diagnostics: null);
    }

    /// <summary>
    /// Creates a parser definition with one predicate and one inline parser action before token <c>A</c>.
    /// </summary>
    /// <param name="predicateCode">Predicate source code.</param>
    /// <param name="actionCode">Action source code.</param>
    /// <returns>A parser definition parsed from ANTLR text.</returns>
    private static ParserDefinition CreatePredicateAndActionDefinition(string predicateCode, string actionCode)
    {
        return Antlr4GrammarConverter.Parse(
            $$"""
            grammar P;
            start : { {{predicateCode}} }? { {{actionCode}} } A ;
            A : 'a' ;
            """,
            diagnostics: null);
    }

    /// <summary>
    /// Test expression compiler that tracks preparation-time compilation and runtime delegate execution separately.
    /// </summary>
    private sealed class TrackingExpressionCompiler : IExpressionCompiler
    {
        /// <summary>
        /// Gets the number of compiler invocations.
        /// </summary>
        public int CompilationCount { get; private set; }

        /// <summary>
        /// Gets the number of true predicate delegate executions.
        /// </summary>
        public int PredicateTrueExecutionCount { get; private set; }

        /// <summary>
        /// Gets the number of false predicate delegate executions.
        /// </summary>
        public int PredicateFalseExecutionCount { get; private set; }

        /// <summary>
        /// Gets the number of parser action delegate executions.
        /// </summary>
        public int ActionExecutionCount { get; private set; }

        /// <summary>
        /// Gets the latest symbols supplied to the compiler.
        /// </summary>
        public IReadOnlyDictionary<string, Expression>? LastSymbols { get; private set; }

        /// <inheritdoc />
        public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null)
        {
            CompilationCount++;
            LastSymbols = symbols;

            return content.Trim() switch
            {
                "predicateTrue" => Expression.Call(Expression.Constant(this), GetMethod(nameof(EvaluateTruePredicate))),
                "predicateFalse" => Expression.Call(Expression.Constant(this), GetMethod(nameof(EvaluateFalsePredicate))),
                "record" => Expression.Call(Expression.Constant(this), GetMethod(nameof(ExecuteAction))),
                "fail" => throw new InvalidOperationException("Compilation failed for test expression."),
                _ => throw new InvalidOperationException($"Unexpected test expression '{content}'.")
            };
        }

        /// <summary>
        /// Runtime predicate delegate target that returns <c>true</c>.
        /// </summary>
        /// <returns><c>true</c>.</returns>
        public bool EvaluateTruePredicate()
        {
            PredicateTrueExecutionCount++;
            return true;
        }

        /// <summary>
        /// Runtime predicate delegate target that returns <c>false</c>.
        /// </summary>
        /// <returns><c>false</c>.</returns>
        public bool EvaluateFalsePredicate()
        {
            PredicateFalseExecutionCount++;
            return false;
        }

        /// <summary>
        /// Runtime parser action delegate target that records action execution.
        /// </summary>
        public void ExecuteAction()
        {
            ActionExecutionCount++;
        }

        /// <summary>
        /// Resolves a public instance method for expression tree calls.
        /// </summary>
        /// <param name="name">Method name to resolve.</param>
        /// <returns>The resolved method.</returns>
        private static MethodInfo GetMethod(string name)
        {
            return typeof(TrackingExpressionCompiler).GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMethodException(typeof(TrackingExpressionCompiler).FullName, name);
        }
    }

    /// <summary>
    /// Passive runtime observer used to verify base policy preservation.
    /// </summary>
    private sealed class NoOpRuntimeObserver : IParserRuntimeObserver
    {
        /// <inheritdoc />
        public void OnAlternativeStarted(AlternativeRuntimeObservation observation)
        {
        }

        /// <inheritdoc />
        public void OnAlternativeCompleted(AlternativeRuntimeObservation observation)
        {
        }

        /// <inheritdoc />
        public void OnAlternativeFailed(AlternativeRuntimeObservation observation)
        {
        }

        /// <inheritdoc />
        public void OnAlternativePruned(AlternativeRuntimeObservation observation)
        {
        }

        /// <inheritdoc />
        public void OnAlternativeSelected(AlternativeRuntimeObservation observation)
        {
        }
    }
}

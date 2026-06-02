using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.Loader;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Generators.Internal;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies that generated ANTLR4 embedded parser code is emitted as C# hooks,
/// compiled by Roslyn, and executed by <see cref="ParserEngine"/> through a generated runtime policy.
/// </summary>
[TestClass]
public class Antlr4GeneratedEmbeddedCodeTests
{
    /// <summary>
    /// Ensures a generated <c>true</c> predicate hook is compiled and allows parsing to succeed.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateTrue_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : { true }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "private static bool __Predicate_start_0_0_0");
        StringAssert.Contains(source, "GeneratedSemanticPredicateEvaluator");

        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures a generated <c>false</c> predicate hook is executed and rejects the branch through the parser engine.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateFalse_RejectsParse()
    {
        const string grammar = """
            grammar P;
            start : { false }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures generated predicate hooks expose the documented contextual symbols.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateContextualSymbols_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : { inputPosition == 0 && ruleName == "start" && alternativeIndex == 0 && elementIndex == 0 }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures inline parser actions can call user code supplied in another partial class.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InlineAction_ExecutesUserPartialMethod()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } A ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount++;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "private static void __Action_start_0_0_0");
        StringAssert.Contains(source, "GeneratedParserActionExecutor");

        var assembly = CompileGeneratedSource(source, userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.AreEqual(1, ReadActionCount(assembly));
    }

    /// <summary>
    /// Ensures the existing default parse helper remains conservative and does not execute generated action hooks.
    /// </summary>
    [TestMethod]
    public void Parse_DefaultParse_DoesNotExecuteGeneratedInlineAction()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } A ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount++;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);

        InvokeParse(assembly, "Parse", "a");
        Assert.AreEqual(0, ReadActionCount(assembly));

        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");
        Assert.AreEqual(1, ReadActionCount(assembly));
    }

    /// <summary>
    /// Ensures duplicate embedded source text in different sequence positions dispatches through distinct hooks.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_DuplicateActionSourceText_UsesPositionSpecificHooks()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } A { OnAction(context); } B ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.ElementIndex == 0 || context.ElementIndex == 2 ? 1 : 100;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Action_start_0_0_0");
        StringAssert.Contains(source, "__Action_start_0_2_1");

        var assembly = CompileGeneratedSource(source, userPartial);
        InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsTrue(ReadActionCount(assembly) is > 0 and < 100);
    }

    /// <summary>
    /// Ensures a semantic predicate that is the only item in an alternative uses the runtime single-item element index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SinglePredicateAlternative_RejectsParse()
    {
        const string grammar = """
            grammar P;
            start : { false }? ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Predicate_start_0_m1_0");

        var assembly = CompileGeneratedSource(source);

        Assert.IsNotInstanceOfType(InvokeParse(assembly, "Parse", string.Empty), typeof(ErrorNode));
        Assert.IsInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", string.Empty), typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures an inline action that is the only item in an alternative dispatches and executes.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_SingleActionAlternative_ExecutesAction()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.ElementIndex == -1 ? 1 : 100;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Action_start_0_m1_0");

        var assembly = CompileGeneratedSource(source, userPartial);
        Assert.IsNotInstanceOfType(InvokeParse(assembly, "Parse", string.Empty), typeof(ErrorNode));
        Assert.AreEqual(0, ReadActionCount(assembly));

        Assert.IsNotInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", string.Empty), typeof(ErrorNode));
        Assert.AreEqual(1, ReadActionCount(assembly));
    }

    /// <summary>
    /// Ensures an inline action inside a quantifier uses the runtime inner element index rather than the parent sequence index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_QuantifierInlineAction_ExecutesAction()
    {
        const string grammar = """
            grammar P;
            start : A ({ OnAction(context); } B)* ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.ElementIndex == 0 ? 1 : 100;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "abb");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadActionCount(assembly) is > 0 and < 100);
    }

    /// <summary>
    /// Ensures a predicate inside a quantifier is evaluated with the runtime inner element index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_QuantifierPredicate_EvaluatesPredicate()
    {
        const string grammar = """
            grammar P;
            start : A ({ OnPredicate(context) }? B)* ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int PredicateCount;

                private static bool OnPredicate(SemanticPredicateEvaluationContext context)
                {
                    PredicateCount++;
                    return context.InputPosition == 1 && context.ElementIndex == 0;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "ab");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadPredicateCount(assembly) > 0);
    }

    /// <summary>
    /// Ensures equal action source text in separate alternatives dispatches by alternative index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_AlternativesWithSameActionSource_DispatchesByAlternativeIndex()
    {
        const string grammar = """
            grammar P;
            start
                : { OnAction(context); } A
                | { OnAction(context); } B
                ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.AlternativeIndex == 0 ? 1 : 10;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Action_start_0_0_0");
        StringAssert.Contains(source, "__Action_start_1_0_1");

        var assembly = CompileGeneratedSource(source, userPartial);
        Assert.IsNotInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", "a"), typeof(ErrorNode));
        Assert.AreEqual(1, ReadActionCount(assembly));

        Assert.IsNotInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", "b"), typeof(ErrorNode));
        Assert.AreEqual(11, ReadActionCount(assembly));
    }

    /// <summary>
    /// Ensures a predicate inside negation dispatches with the runtime probe index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NegationPredicate_DispatchesWithRuntimeIndex()
    {
        const string grammar = """
            grammar P;
            start : ~({ OnPredicate(context) }? A) ;
            A : 'a' ;
            B : 'b' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int PredicateCount;

                private static bool OnPredicate(SemanticPredicateEvaluationContext context)
                {
                    PredicateCount++;
                    return context.ElementIndex == 0;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "b");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadPredicateCount(assembly));
    }

    /// <summary>
    /// Ensures generated hooks in a direct-left-recursive tail use the runtime tail element index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LeftRecursiveTailAction_DispatchesWithRuntimeTailIndex()
    {
        const string grammar = """
            grammar P;
            expr
                : INT
                | expr { OnAction(context); } PLUS INT
                ;
            INT : [0-9]+ ;
            PLUS : '+' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int ActionCount;

                private static void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount += context.ElementIndex == 0 ? 1 : 100;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Action_expr_0_0_0");

        var assembly = CompileGeneratedSource(source, userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "1+2");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadActionCount(assembly) is > 0 and < 100);
    }

    /// <summary>
    /// Ensures generated predicates in a direct-left-recursive tail use the runtime tail element index.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LeftRecursiveTailPredicate_DispatchesWithRuntimeTailIndex()
    {
        const string grammar = """
            grammar P;
            expr
                : INT
                | expr { OnPredicate(context) }? PLUS INT
                ;
            INT : [0-9]+ ;
            PLUS : '+' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal static partial class P
            {
                public static int PredicateCount;

                private static bool OnPredicate(SemanticPredicateEvaluationContext context)
                {
                    PredicateCount++;
                    return context.ElementIndex == 0;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "__Predicate_expr_0_0_0");

        var assembly = CompileGeneratedSource(source, userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "1+2");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(ReadPredicateCount(assembly) > 0);
    }

    /// <summary>
    /// Ensures a generated helper resolves direct-left-recursive metadata before dispatching a base alternative after a recursive alternative.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LeftRecursiveBaseAfterRecursiveAlternative_UsesResolvedAlternativeIndex()
    {
        const string grammar = """
            grammar P;
            expr
                : expr PLUS INT
                | { false }? INT
                ;
            INT : [0-9]+ ;
            PLUS : '+' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "new CompiledGrammar(Build(), CreateRuntimePolicy())");
        StringAssert.Contains(source, "__Predicate_expr_0_0_0");

        var assembly = CompileGeneratedSource(source);

        Assert.IsNotInstanceOfType(InvokeParse(assembly, "Parse", "1"), typeof(ErrorNode));
        Assert.IsInstanceOfType(InvokeParse(assembly, "ParseWithEmbeddedCode", "1"), typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures invalid embedded C# remains a Roslyn compilation error in the source-generator path.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_InvalidPredicateCode_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;
            start : { not valid }? A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.ToString().Contains("not", StringComparison.Ordinal)));
    }


    /// <summary>
    /// Ensures invalid inline action C# remains a Roslyn compilation error in the source-generator path.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_InvalidActionCode_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;
            start : { not valid ; } A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.ToString().Contains("not", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Ensures generated hook names remain aligned with shared runtime discovery metadata for representative parser shapes.
    /// </summary>
    [TestMethod]
    public void Emit_GeneratedHooks_MatchSharedRuntimeDiscoveryIndexes_ForParserShapes()
    {
        var singlePredicate = new ValidatingPredicate("true");
        var singleAction = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var sequenceAction = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var quantifierPredicate = new ValidatingPredicate("OnPredicate(context)");
        var negationPredicate = new ValidatingPredicate("OnPredicate(context)");
        var duplicateFirst = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var duplicateSecond = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var leftRecursiveBasePredicate = new ValidatingPredicate("false");
        var leftRecursiveTailAction = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);

        var cases = new[]
        {
            (
                Grammar: """
                    grammar P;
                    start : { true }? ;
                    A : 'a' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, singlePredicate)]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start : { OnAction(context); } ;
                    A : 'a' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, singleAction)]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start : A { OnAction(context); } B ;
                    A : 'a' ;
                    B : 'b' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, new Sequence([new RuleRef("A"), sequenceAction, new RuleRef("B")]))]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start : A ({ OnPredicate(context) }? B)* ;
                    A : 'a' ;
                    B : 'b' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, new Sequence([new RuleRef("A"), new Quantifier(new Sequence([quantifierPredicate, new RuleRef("B")]), 0, null)]))]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start : ~({ OnPredicate(context) }? A) ;
                    A : 'a' ;
                    B : 'b' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, new Negation(new Sequence([negationPredicate, new RuleRef("A")])))]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    start
                        : { OnAction(context); } A
                        | { OnAction(context); } B
                        ;
                    A : 'a' ;
                    B : 'b' ;
                    """,
                Definition: CreateGeneratedParityDefinition(new Rule("start", 0, false, new Alternation([
                    new Alternative(0, Associativity.Left, new Sequence([duplicateFirst, new RuleRef("A")])),
                    new Alternative(1, Associativity.Left, new Sequence([duplicateSecond, new RuleRef("B")]))
                ]), Kind: RuleKind.Parser))),
            (
                Grammar: """
                    grammar P;
                    expr
                        : expr PLUS INT
                        | { false }? INT
                        ;
                    INT : [0-9]+ ;
                    PLUS : '+' ;
                    """,
                Definition: CreateGeneratedParityLeftRecursiveBaseDefinition(leftRecursiveBasePredicate)),
            (
                Grammar: """
                    grammar P;
                    expr
                        : INT
                        | expr { OnAction(context); } PLUS INT
                        ;
                    INT : [0-9]+ ;
                    PLUS : '+' ;
                    """,
                Definition: CreateGeneratedParityLeftRecursiveTailDefinition(leftRecursiveTailAction))
        };

        foreach (var testCase in cases)
        {
            AssertGeneratedHooksMatchDiscovery(testCase.Grammar, testCase.Definition);
        }
    }

    /// <summary>
    /// Ensures generated hook dispatch metadata remains aligned with shared ParserDefinition runtime discovery metadata.
    /// </summary>
    [TestMethod]
    public void Emit_InlineActionHook_UsesSharedRuntimeDiscoveryIndexes()
    {
        const string grammar = """
            grammar P;
            start : A ({ OnAction(context); } B)* ;
            A : 'a' ;
            B : 'b' ;
            """;
        var action = new EmbeddedAction("OnAction(context);", ActionContext.Alternative, ActionPosition.Inline, []);
        var parserRule = new Rule(
            "start",
            0,
            false,
            new Alternation([new Alternative(0, Associativity.Left, new Sequence([
                new RuleRef("A"),
                new Quantifier(new Sequence([action, new RuleRef("B")]), 0, null)
            ]))]),
            Kind: RuleKind.Parser);
        var definition = new ParserDefinition("P", GrammarType.Combined, null, [], [], [], [parserRule], parserRule);

        var entry = EmbeddedCodeRuntimeDiscovery.Discover(definition).ExecutableEntries.Single();
        string generatedSource = Emit(grammar);
        string expectedHookName = $"__Action_{entry.RuleName}_{entry.AlternativeIndex}_{entry.ElementIndex}_0";

        Assert.AreEqual(EmbeddedCodeKind.ParserInlineAction, entry.Kind);
        Assert.AreEqual(0, entry.AlternativeIndex);
        Assert.AreEqual(0, entry.ElementIndex);
        StringAssert.Contains(generatedSource, expectedHookName);
    }


    /// <summary>
    /// Asserts generated hook names for all runtime-executable entries discovered from a hand-built parser definition.
    /// </summary>
    /// <param name="grammarText">ANTLR grammar text emitted by the production generator.</param>
    /// <param name="definition">Equivalent parser definition inspected by shared runtime discovery.</param>
    private static void AssertGeneratedHooksMatchDiscovery(string grammarText, ParserDefinition definition)
    {
        string generatedSource = Emit(grammarText);
        var entries = EmbeddedCodeRuntimeDiscovery.Discover(definition).ExecutableEntries;
        var ordinalsByKind = new Dictionary<EmbeddedCodeKind, int>();

        foreach (var entry in entries)
        {
            int ordinal = ordinalsByKind.TryGetValue(entry.Kind, out int current) ? current : 0;
            ordinalsByKind[entry.Kind] = ordinal + 1;
            string prefix = entry.Kind == EmbeddedCodeKind.SemanticPredicate ? "__Predicate" : "__Action";
            string elementIndex = entry.ElementIndex?.ToString() ?? "m1";
            string expectedHookName = $"{prefix}_{entry.RuleName}_{entry.AlternativeIndex}_{elementIndex}_{ordinal}";

            StringAssert.Contains(generatedSource, expectedHookName);
        }
    }

    /// <summary>
    /// Creates a parser definition used by generated-hook parity tests.
    /// </summary>
    /// <param name="rootRule">Root parser rule.</param>
    /// <returns>A parser definition containing the supplied root rule.</returns>
    private static ParserDefinition CreateGeneratedParityDefinition(Rule rootRule) =>
        new("P", GrammarType.Combined, null, [], [], [], [rootRule], rootRule);

    /// <summary>
    /// Creates a direct-left-recursive parser definition whose base alternative follows a recursive alternative.
    /// </summary>
    /// <param name="predicate">Predicate contained in the base alternative.</param>
    /// <returns>A parser definition with left-recursive metadata.</returns>
    private static ParserDefinition CreateGeneratedParityLeftRecursiveBaseDefinition(ValidatingPredicate predicate)
    {
        var recursiveAlternative = new Alternative(0, Associativity.Left, new Sequence([new RuleRef("expr"), new RuleRef("PLUS"), new RuleRef("INT")]));
        var baseAlternative = new Alternative(1, Associativity.Left, new Sequence([predicate, new RuleRef("INT")]));
        var rule = new Rule("expr", 0, false, new Alternation([recursiveAlternative, baseAlternative]), Kind: RuleKind.Parser);
        return CreateGeneratedParityLeftRecursiveDefinition(rule, [baseAlternative], [recursiveAlternative]);
    }

    /// <summary>
    /// Creates a direct-left-recursive parser definition with an executable tail action.
    /// </summary>
    /// <param name="action">Action contained in the recursive tail.</param>
    /// <returns>A parser definition with left-recursive metadata.</returns>
    private static ParserDefinition CreateGeneratedParityLeftRecursiveTailDefinition(EmbeddedAction action)
    {
        var baseAlternative = new Alternative(0, Associativity.Left, new Sequence([new RuleRef("INT")]));
        var recursiveAlternative = new Alternative(1, Associativity.Left, new Sequence([new RuleRef("expr"), action, new RuleRef("PLUS"), new RuleRef("INT")]));
        var rule = new Rule("expr", 0, false, new Alternation([baseAlternative, recursiveAlternative]), Kind: RuleKind.Parser);
        return CreateGeneratedParityLeftRecursiveDefinition(rule, [baseAlternative], [recursiveAlternative]);
    }

    /// <summary>
    /// Creates a parser definition with direct-left-recursive metadata populated.
    /// </summary>
    /// <param name="rule">Left-recursive parser rule.</param>
    /// <param name="baseAlternatives">Resolved base alternatives.</param>
    /// <param name="recursiveAlternatives">Resolved recursive alternatives.</param>
    /// <returns>A parser definition with left-recursive metadata.</returns>
    private static ParserDefinition CreateGeneratedParityLeftRecursiveDefinition(
        Rule rule,
        IReadOnlyList<Alternative> baseAlternatives,
        IReadOnlyList<Alternative> recursiveAlternatives) =>
        new ParserDefinition("P", GrammarType.Combined, null, [], [], [], [rule], rule)
        {
            LeftRecursiveRules = new Dictionary<string, LeftRecursiveRuleInfo>
            {
                [rule.Name] = new()
                {
                    Rule = rule,
                    BaseAlternatives = baseAlternatives,
                    RecursiveAlternatives = recursiveAlternatives
                }
            }
        };

    /// <summary>
    /// Emits generated C# for the supplied grammar using the production grammar emitter.
    /// </summary>
    /// <param name="grammarText">ANTLR4 grammar source.</param>
    /// <returns>Generated C# source.</returns>
    private static string Emit(string grammarText)
    {
        var grammar = new G4Parser(new G4Tokenizer(grammarText).Tokenize()).Parse();
        return GrammarEmitter.Emit(grammar, "Generated.Tests", "P", "P.g4");
    }

    /// <summary>
    /// Compiles generated C# and optional user partial source, then loads the resulting in-memory assembly.
    /// </summary>
    /// <param name="generatedSource">Generated grammar source.</param>
    /// <param name="additionalSource">Optional user source compiled with the generated source.</param>
    /// <returns>Loaded test assembly.</returns>
    private static Assembly CompileGeneratedSource(string generatedSource, string? additionalSource = null)
    {
        var result = CompileGeneratedSourceExpectingFailure(generatedSource, additionalSource);
        if (!result.Success)
        {
            Assert.Fail(string.Join(Environment.NewLine, result.Diagnostics));
        }

        result.AssemblyStream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(result.AssemblyStream);
    }

    /// <summary>
    /// Compiles generated C# and returns raw Roslyn diagnostics without asserting success.
    /// </summary>
    /// <param name="generatedSource">Generated grammar source.</param>
    /// <param name="additionalSource">Optional user source compiled with the generated source.</param>
    /// <returns>Compilation output and diagnostics.</returns>
    private static CompilationResult CompileGeneratedSourceExpectingFailure(string generatedSource, string? additionalSource = null)
    {
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(generatedSource, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), path: "P.g.cs")
        };

        if (additionalSource is not null)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(additionalSource, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), path: "P.User.cs"));
        }

        var references = GetMetadataReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratedEmbeddedCodeTests_" + Guid.NewGuid().ToString("N"),
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        return new CompilationResult(emitResult.Success, stream, emitResult.Diagnostics);
    }

    /// <summary>
    /// Builds metadata references from trusted platform assemblies plus parser assemblies used by generated code.
    /// </summary>
    /// <returns>Roslyn metadata references.</returns>
    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedPlatformAssemblies is not null)
        {
            foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                paths.Add(path);
            }
        }

        AddAssemblyPath(paths, typeof(ParserEngine).Assembly);
        AddAssemblyPath(paths, typeof(CompiledGrammar).Assembly);
        AddAssemblyPath(paths, typeof(object).Assembly);
        AddAssemblyPath(paths, typeof(Enumerable).Assembly);

        return paths.Select(static path => MetadataReference.CreateFromFile(path)).ToArray();
    }

    /// <summary>
    /// Adds an assembly location to the reference path set when available.
    /// </summary>
    /// <param name="paths">Reference path set to update.</param>
    /// <param name="assembly">Assembly to add.</param>
    private static void AddAssemblyPath(HashSet<string> paths, Assembly assembly)
    {
        if (!string.IsNullOrEmpty(assembly.Location))
        {
            paths.Add(assembly.Location);
        }
    }

    /// <summary>
    /// Invokes a generated parse helper by reflection on the internal generated class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="methodName">Parse helper method name.</param>
    /// <param name="input">Input text to parse.</param>
    /// <returns>Parse-tree root returned by the generated helper.</returns>
    private static ParseNode InvokeParse(Assembly assembly, string methodName, string input)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;
        return (ParseNode)method.Invoke(null, [input])!;
    }

    /// <summary>
    /// Reads the test action counter from the user partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <returns>Current action count.</returns>
    private static int ReadActionCount(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var field = type.GetField("ActionCount", BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Reads the test predicate counter from the user partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <returns>Current predicate count.</returns>
    private static int ReadPredicateCount(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var field = type.GetField("PredicateCount", BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Captures Roslyn compilation output for generated embedded-code tests.
    /// </summary>
    /// <param name="Success">Whether compilation succeeded.</param>
    /// <param name="AssemblyStream">Emitted assembly stream.</param>
    /// <param name="Diagnostics">Roslyn diagnostics reported during compilation.</param>
    private sealed record CompilationResult(bool Success, MemoryStream AssemblyStream, IReadOnlyList<Diagnostic> Diagnostics);
}

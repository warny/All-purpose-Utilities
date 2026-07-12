using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Expressions;
using Utils.Parser.Diagnostics.EmbeddedCode;
using Utils.Parser.Generators.Internal;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies that every generated-C# embedded-code location crosses the raw-to-transformed boundary exactly once.
/// </summary>
[TestClass]
public sealed class EmbeddedCodeTransformationInvariantTests
{
    /// <summary>
    /// Covers all currently supported generated-C# embedded-code locations in one grammar-level matrix.
    /// </summary>
    [TestMethod]
    public void Emit_WhenAllGeneratedEmbeddedCodeLocationsArePresent_TransformsEachLocationExactlyOnce()
    {
        const string grammar = """
            grammar P;
            @header { RAW_PARSER_HEADER }
            @members { RAW_PARSER_MEMBERS }
            @footer { RAW_PARSER_FOOTER }
            @lexer::header { RAW_LEXER_HEADER }
            @lexer::members { RAW_LEXER_MEMBERS }
            @lexer::footer { RAW_LEXER_FOOTER }
            start[int p] returns [int r] locals [int l]
            @init { RAW_RULE_INIT; }
            @after { RAW_RULE_AFTER; }
                : first=child { RAW_PARSER_PREDICATE }? A { RAW_PARSER_ACTION; }
                | many+=child A
                ;
            child : A ;
            A : { RAW_LEXER_PREDICATE }? 'a' { RAW_LEXER_ACTION; } ;
            """;
        var transformer = new RecordingEmbeddedCodeTransformer();

        string source = EmitWithTransformer(grammar, transformer);

        Assert.AreEqual(12, transformer.Calls.Count, DescribeCalls(transformer));
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.LexerSemanticPredicate), " RAW_LEXER_PREDICATE ", ParserEmbeddedCodeLocation.LexerSemanticPredicate, "P", "A");
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.LexerInlineAction), " RAW_LEXER_ACTION; ", ParserEmbeddedCodeLocation.LexerInlineAction, "P", "A");
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.RuleInit), " RAW_RULE_INIT; ", ParserEmbeddedCodeLocation.RuleInit, "P", "start");
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.RuleAfter), " RAW_RULE_AFTER; ", ParserEmbeddedCodeLocation.RuleAfter, "P", "start");
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.SemanticPredicate), " RAW_PARSER_PREDICATE ", ParserEmbeddedCodeLocation.SemanticPredicate, "P", "start");
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.InlineAction), " RAW_PARSER_ACTION; ", ParserEmbeddedCodeLocation.InlineAction, "P", "start");
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.ParserHeader), " RAW_PARSER_HEADER ", ParserEmbeddedCodeLocation.ParserHeader, "P", null);
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.LexerHeader), " RAW_LEXER_HEADER ", ParserEmbeddedCodeLocation.LexerHeader, "P", null);
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.ParserMembers), " RAW_PARSER_MEMBERS ", ParserEmbeddedCodeLocation.ParserMembers, "P", null);
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.LexerMembers), " RAW_LEXER_MEMBERS ", ParserEmbeddedCodeLocation.LexerMembers, "P", null);
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.ParserFooter), " RAW_PARSER_FOOTER ", ParserEmbeddedCodeLocation.ParserFooter, "P", null);
        AssertCall(FindCall(transformer, ParserEmbeddedCodeLocation.LexerFooter), " RAW_LEXER_FOOTER ", ParserEmbeddedCodeLocation.LexerFooter, "P", null);
        Assert.IsTrue(FindCall(transformer, ParserEmbeddedCodeLocation.RuleInit).Index < FindCall(transformer, ParserEmbeddedCodeLocation.RuleAfter).Index);

        RecordedTransformationCall parserPredicate = FindCall(transformer, ParserEmbeddedCodeLocation.SemanticPredicate);
        CollectionAssert.AreEqual(new[] { "p" }, parserPredicate.Parameters.Select(static item => item.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "l" }, parserPredicate.Locals.Select(static item => item.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "r" }, parserPredicate.Returns.Select(static item => item.Name).ToArray());
        CollectionAssert.AreEquivalent(new[] { "first", "many" }, parserPredicate.Labels.Keys.ToArray());
        Assert.IsFalse(parserPredicate.Labels["first"].IsList);
        Assert.IsTrue(parserPredicate.Labels["many"].IsList);

        foreach (RecordedTransformationCall call in transformer.Calls)
        {
            if (call.Location is ParserEmbeddedCodeLocation.ParserHeader or ParserEmbeddedCodeLocation.ParserMembers or ParserEmbeddedCodeLocation.ParserFooter
                or ParserEmbeddedCodeLocation.LexerHeader or ParserEmbeddedCodeLocation.LexerMembers or ParserEmbeddedCodeLocation.LexerFooter
                or ParserEmbeddedCodeLocation.RuleInit or ParserEmbeddedCodeLocation.RuleAfter)
            {
                AssertRawCodeAbsent(source, call.RawCode);
            }
            AssertTransformedCodeOccursExactlyOnce(source, call.TransformedCode);
        }

        Assert.IsFalse(source.Contains("return  RAW_PARSER_PREDICATE ", StringComparison.Ordinal), source);
        Assert.IsFalse(source.Contains("        RAW_PARSER_ACTION;", StringComparison.Ordinal), source);
        Assert.IsFalse(source.Contains("return  RAW_LEXER_PREDICATE ", StringComparison.Ordinal), source);
        Assert.IsFalse(source.Contains("        RAW_LEXER_ACTION;", StringComparison.Ordinal), source);

        StringAssert.Contains(source, "// <auto-generated-parser-header>");
        StringAssert.Contains(source, "    // <auto-generated-parser-members>");
        StringAssert.Contains(source, "// <auto-generated-parser-footer>");
        StringAssert.Contains(source, "// <auto-generated-lexer-header>");
        StringAssert.Contains(source, "    // <auto-generated-lexer-members>");
        StringAssert.Contains(source, "// <auto-generated-lexer-footer>");
        StringAssert.Contains(source, "private bool __Predicate_start_0_1_0");
        StringAssert.Contains(source, "private void __Action_start_0_3_1");
        StringAssert.Contains(source, "internal void __Init_start");
        StringAssert.Contains(source, "internal void __After_start");
        StringAssert.Contains(source, "private bool __LexerPredicate_A_0_0_0");
        StringAssert.Contains(source, "private void __LexerAction_A_0_2_1");
    }

    /// <summary>
    /// Ensures predicate expressions and explicit-return predicate blocks are both injected from transformed code only.
    /// </summary>
    [DataTestMethod]
    [DataRow("true /* TRANSFORMED_SEMANTIC_PREDICATE_01 */", "return true /* TRANSFORMED_SEMANTIC_PREDICATE_01 */;")]
    [DataRow("return true; /* TRANSFORMED_SEMANTIC_PREDICATE_01 */", "return true; /* TRANSFORMED_SEMANTIC_PREDICATE_01 */")]
    public void Emit_WhenParserPredicateIsTransformed_EmitsExpectedPredicateBody(string transformedPredicate, string expectedSource)
    {
        const string grammar = """
            grammar P;
            start : { RAW_PARSER_PREDICATE }? A ;
            A : 'a' ;
            """;
        var transformer = new RecordingEmbeddedCodeTransformer(context => context.Location == ParserEmbeddedCodeLocation.SemanticPredicate ? transformedPredicate : null);

        string source = EmitWithTransformer(grammar, transformer);

        RecordedTransformationCall call = AssertSingleCall(transformer, ParserEmbeddedCodeLocation.SemanticPredicate, " RAW_PARSER_PREDICATE ");
        Assert.IsFalse(source.Contains("return  RAW_PARSER_PREDICATE ", StringComparison.Ordinal), source);
        AssertTransformedCodeOccursExactlyOnce(source, call.TransformedCode);
        StringAssert.Contains(source, expectedSource);
    }

    /// <summary>
    /// Ensures a blocking transformer diagnostic prevents generated C# injection instead of falling back to raw text.
    /// </summary>
    [TestMethod]
    public void Emit_WhenTransformerReportsError_DoesNotInjectRawOrTransformedCode()
    {
        const string grammar = """
            grammar P;
            @members { RAW_PARSER_MEMBERS }
            start : A ;
            A : 'a' ;
            """;
        var transformer = new RecordingEmbeddedCodeTransformer { DiagnosticSeverity = ParserEmbeddedCodeDiagnosticSeverity.Error };

        var exception = Assert.ThrowsException<Utils.Parser.Diagnostics.EmbeddedCode.ParserEmbeddedCodeTransformationException>(() => EmitWithTransformer(grammar, transformer));

        Assert.AreEqual(ParserEmbeddedCodeLocation.ParserMembers, exception.Location);
        Assert.AreEqual(1, transformer.Calls.Count);
        Assert.AreEqual(" RAW_PARSER_MEMBERS ", transformer.Calls[0].RawCode);
    }

    /// <summary>
    /// Ensures transformer exceptions prevent generated C# injection instead of falling back to raw text.
    /// </summary>
    [TestMethod]
    public void Emit_WhenTransformerThrows_DoesNotInjectRawOrTransformedCode()
    {
        const string grammar = """
            grammar P;
            @footer { RAW_PARSER_FOOTER }
            start : A ;
            A : 'a' ;
            """;
        var inner = new InvalidOperationException("boom");
        var transformer = new RecordingEmbeddedCodeTransformer { Exception = inner };

        var exception = Assert.ThrowsException<Utils.Parser.Diagnostics.EmbeddedCode.ParserEmbeddedCodeTransformationException>(() => EmitWithTransformer(grammar, transformer));

        Assert.AreEqual(ParserEmbeddedCodeLocation.ParserFooter, exception.Location);
        Assert.AreSame(inner, exception.InnerException);
        Assert.AreEqual(1, transformer.Calls.Count);
    }


    /// <summary>
    /// Verifies the runtime semantic-predicate preparation path transforms once before compiling once.
    /// </summary>
    [TestMethod]
    public void PrepareSemanticPredicate_WhenRuntimePathIsUsed_TransformsOnceBeforeCompiler()
    {
        var compiler = new RecordingExpressionCompiler(Expression.Constant(true));
        var transformer = new RecordingEmbeddedCodeTransformer(_ => "true");
        var preparer = new ExpressionEmbeddedCodePreparer(compiler, transformer);

        var result = preparer.PrepareSemanticPredicate(
            new EmbeddedCodeSource("RAW_RUNTIME_PREDICATE", EmbeddedCodeKind.SemanticPredicate, ruleName: "start", alternativeIndex: 1, elementIndex: 2),
            new EmbeddedCodePreparationContext("P", EmbeddedCodeTarget.RuntimeInlineExpression, ruleName: "start", languageOrCompilerIdentity: "test"));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        RecordedTransformationCall call = AssertSingleRuntimeCall(transformer, ParserEmbeddedCodeLocation.SemanticPredicate, "RAW_RUNTIME_PREDICATE");
        Assert.AreEqual("start", call.RuleName);
        Assert.AreEqual(1, compiler.CompileCount);
        Assert.AreEqual("true", compiler.Contents.Single());
        CollectionAssert.DoesNotContain(compiler.Contents, "RAW_RUNTIME_PREDICATE");
    }

    /// <summary>
    /// Verifies the runtime parser-action preparation path transforms once before compiling once.
    /// </summary>
    [TestMethod]
    public void PrepareParserAction_WhenRuntimePathIsUsed_TransformsOnceBeforeCompiler()
    {
        var compiler = new RecordingExpressionCompiler(Expression.Empty());
        var transformer = new RecordingEmbeddedCodeTransformer(_ => "TRANSFORMED_RUNTIME_ACTION");
        var preparer = new ExpressionEmbeddedCodePreparer(compiler, transformer);

        var result = preparer.PrepareParserAction(
            new EmbeddedCodeSource("RAW_RUNTIME_ACTION", EmbeddedCodeKind.ParserInlineAction, ruleName: "start", alternativeIndex: 1, elementIndex: 2),
            new EmbeddedCodePreparationContext("P", EmbeddedCodeTarget.RuntimeInlineExpression, ruleName: "start", languageOrCompilerIdentity: "test"));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        RecordedTransformationCall call = AssertSingleRuntimeCall(transformer, ParserEmbeddedCodeLocation.InlineAction, "RAW_RUNTIME_ACTION");
        Assert.AreEqual("start", call.RuleName);
        Assert.AreEqual(1, compiler.CompileCount);
        Assert.AreEqual("TRANSFORMED_RUNTIME_ACTION", compiler.Contents.Single());
        CollectionAssert.DoesNotContain(compiler.Contents, "RAW_RUNTIME_ACTION");
    }

    /// <summary>
    /// Verifies runtime transformer errors block compilation.
    /// </summary>
    [DataTestMethod]
    [DataRow(ParserEmbeddedCodeLocation.SemanticPredicate)]
    [DataRow(ParserEmbeddedCodeLocation.InlineAction)]
    public void PrepareRuntimeEmbeddedCode_WhenTransformerReportsError_DoesNotCompile(ParserEmbeddedCodeLocation location)
    {
        var compiler = new RecordingExpressionCompiler(Expression.Empty());
        var transformer = new RecordingEmbeddedCodeTransformer { DiagnosticSeverity = ParserEmbeddedCodeDiagnosticSeverity.Error };
        var preparer = new ExpressionEmbeddedCodePreparer(compiler, transformer);

        EmbeddedCodePreparationStatus status;
        Exception? exception;
        if (location == ParserEmbeddedCodeLocation.SemanticPredicate)
        {
            var result = preparer.PrepareSemanticPredicate(new EmbeddedCodeSource("RAW_RUNTIME_PREDICATE", EmbeddedCodeKind.SemanticPredicate, ruleName: "start"), new EmbeddedCodePreparationContext("P", EmbeddedCodeTarget.RuntimeInlineExpression, ruleName: "start", languageOrCompilerIdentity: "test"));
            status = result.Status;
            exception = result.Exception;
        }
        else
        {
            var result = preparer.PrepareParserAction(new EmbeddedCodeSource("RAW_RUNTIME_ACTION", EmbeddedCodeKind.ParserInlineAction, ruleName: "start"), new EmbeddedCodePreparationContext("P", EmbeddedCodeTarget.RuntimeInlineExpression, ruleName: "start", languageOrCompilerIdentity: "test"));
            status = result.Status;
            exception = result.Exception;
        }

        Assert.AreEqual(EmbeddedCodePreparationStatus.CompilationFailed, status);
        Assert.AreEqual(1, transformer.Calls.Count);
        Assert.AreEqual(0, compiler.CompileCount);
        Assert.IsInstanceOfType(exception, typeof(Utils.Parser.Expressions.ParserEmbeddedCodeTransformationException));
    }

    /// <summary>
    /// Verifies runtime transformer warnings are preserved by transformation and still compile once.
    /// </summary>
    [TestMethod]
    public void PrepareSemanticPredicate_WhenTransformerReportsWarning_CompilesOnce()
    {
        var compiler = new RecordingExpressionCompiler(Expression.Constant(true));
        var transformer = new RecordingEmbeddedCodeTransformer(_ => "true") { DiagnosticSeverity = ParserEmbeddedCodeDiagnosticSeverity.Warning };
        var preparer = new ExpressionEmbeddedCodePreparer(compiler, transformer);

        var result = preparer.PrepareSemanticPredicate(new EmbeddedCodeSource("RAW_RUNTIME_WARNING", EmbeddedCodeKind.SemanticPredicate, ruleName: "start"), new EmbeddedCodePreparationContext("P", EmbeddedCodeTarget.RuntimeInlineExpression, ruleName: "start", languageOrCompilerIdentity: "test"));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.AreEqual(1, transformer.Calls.Count);
        Assert.AreEqual(1, compiler.CompileCount);
        Assert.AreEqual("true", compiler.Contents.Single());
    }

    /// <summary>
    /// Emits generated C# with a test transformer.
    /// </summary>
    private static string EmitWithTransformer(string grammarText, IParserEmbeddedCodeTransformer transformer)
    {
        var grammar = new G4Parser(new G4Tokenizer(grammarText).Tokenize()).Parse();
        return GrammarEmitter.Emit(grammar, "Generated.Tests", "P", "P.g4", transformer);
    }


    /// <summary>
    /// Asserts a single runtime transformation call.
    /// </summary>
    private static RecordedTransformationCall AssertSingleRuntimeCall(RecordingEmbeddedCodeTransformer transformer, ParserEmbeddedCodeLocation location, string rawCode)
    {
        Assert.AreEqual(1, transformer.Calls.Count, DescribeCalls(transformer));
        RecordedTransformationCall call = transformer.Calls[0];
        Assert.AreEqual(location, call.Location);
        Assert.AreEqual(rawCode, call.RawCode);
        Assert.IsNull(call.GrammarName);
        Assert.AreEqual(0, call.Parameters.Count);
        Assert.AreEqual(0, call.Locals.Count);
        Assert.AreEqual(0, call.Returns.Count);
        Assert.AreEqual(0, call.Labels.Count);
        return call;
    }

    /// <summary>
    /// Finds the unique recorded call for a location.
    /// </summary>
    private static RecordedTransformationCall FindCall(RecordingEmbeddedCodeTransformer transformer, ParserEmbeddedCodeLocation location)
    {
        RecordedTransformationCall[] calls = transformer.Calls.Where(call => call.Location == location).ToArray();
        Assert.AreEqual(1, calls.Length, $"Expected one call for {location}. Actual calls: {DescribeCalls(transformer)}");
        return calls[0];
    }

    /// <summary>
    /// Asserts one recorded transformation call.
    /// </summary>
    private static void AssertCall(RecordedTransformationCall call, string rawCode, ParserEmbeddedCodeLocation location, string grammarName, string? ruleName)
    {
        Assert.AreEqual(rawCode, call.RawCode, $"Raw code mismatch for {location}. Actual calls: {call}");
        Assert.AreEqual(location, call.Location, $"Location mismatch for {rawCode}.");
        Assert.AreEqual(grammarName, call.GrammarName, $"Grammar mismatch for {rawCode}.");
        Assert.AreEqual(ruleName, call.RuleName, $"Rule mismatch for {rawCode}.");
    }

    /// <summary>
    /// Asserts a single call for one location and raw fragment.
    /// </summary>
    private static RecordedTransformationCall AssertSingleCall(RecordingEmbeddedCodeTransformer transformer, ParserEmbeddedCodeLocation location, string rawCode)
    {
        Assert.AreEqual(1, transformer.Calls.Count, DescribeCalls(transformer));
        RecordedTransformationCall call = transformer.Calls[0];
        AssertCall(call, rawCode, location, "P", "start");
        return call;
    }

    /// <summary>
    /// Asserts raw code is absent from generated source.
    /// </summary>
    private static void AssertRawCodeAbsent(string source, string rawCode)
    {
        Assert.IsFalse(source.Contains(rawCode, StringComparison.Ordinal), $"Raw code '{rawCode}' unexpectedly appeared in generated source.\n{source}");
    }

    /// <summary>
    /// Asserts transformed code occurs exactly once in generated source.
    /// </summary>
    private static void AssertTransformedCodeOccursExactlyOnce(string source, string transformedCode)
    {
        Assert.AreEqual(1, CountOccurrences(source, transformedCode), $"Transformed code '{transformedCode}' should occur exactly once.\n{source}");
    }

    /// <summary>
    /// Counts non-overlapping ordinal occurrences.
    /// </summary>
    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    /// <summary>
    /// Formats recorded calls for assertion messages.
    /// </summary>
    private static string DescribeCalls(RecordingEmbeddedCodeTransformer transformer) => string.Join(Environment.NewLine, transformer.Calls);

    /// <summary>
    /// Deterministic transformer spy used by generated-C# invariant tests.
    /// </summary>
    private sealed class RecordingEmbeddedCodeTransformer : IParserEmbeddedCodeTransformer
    {
        private readonly Func<ParserEmbeddedCodeTransformationContext, string?>? _overrideCode;
        private readonly List<RecordedTransformationCall> _calls = [];

        public RecordingEmbeddedCodeTransformer(Func<ParserEmbeddedCodeTransformationContext, string?>? overrideCode = null)
        {
            _overrideCode = overrideCode;
        }

        public IReadOnlyList<RecordedTransformationCall> Calls => _calls;

        public ParserEmbeddedCodeDiagnosticSeverity? DiagnosticSeverity { get; set; }

        public Exception? Exception { get; set; }

        public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
        {
            int index = _calls.Count + 1;
            string transformedCode = _overrideCode?.Invoke(context) ?? CreateCode(context.Location, index);
            var call = new RecordedTransformationCall(
                index,
                context.Code,
                transformedCode,
                context.Location,
                context.GrammarName,
                context.RuleName,
                context.Parameters.ToArray(),
                context.Locals.ToArray(),
                context.Returns.ToArray(),
                context.Labels.ToDictionary(static item => item.Key, static item => item.Value));
            _calls.Add(call);
            if (Exception is not null)
            {
                throw Exception;
            }

            return new ParserEmbeddedCodeTransformationResult
            {
                Code = transformedCode,
                Diagnostics = DiagnosticSeverity is null ? [] : [new ParserEmbeddedCodeDiagnostic { Severity = DiagnosticSeverity.Value, Message = "configured diagnostic", Code = "TEST001" }]
            };
        }

        private static string CreateCode(ParserEmbeddedCodeLocation location, int index)
        {
            string suffix = index.ToString("00");
            return location switch
            {
                ParserEmbeddedCodeLocation.ParserHeader => $"// TRANSFORMED_PARSER_HEADER_{suffix}",
                ParserEmbeddedCodeLocation.ParserMembers => $"private int transformedParserMembers{suffix};",
                ParserEmbeddedCodeLocation.ParserFooter => $"// TRANSFORMED_PARSER_FOOTER_{suffix}",
                ParserEmbeddedCodeLocation.LexerHeader => $"// TRANSFORMED_LEXER_HEADER_{suffix}",
                ParserEmbeddedCodeLocation.LexerMembers => $"private int transformedLexerMembers{suffix};",
                ParserEmbeddedCodeLocation.LexerFooter => $"// TRANSFORMED_LEXER_FOOTER_{suffix}",
                ParserEmbeddedCodeLocation.SemanticPredicate => $"true /* TRANSFORMED_SEMANTIC_PREDICATE_{suffix} */",
                ParserEmbeddedCodeLocation.InlineAction => $"int transformedParserAction{suffix} = 0;",
                ParserEmbeddedCodeLocation.RuleInit => $"int transformedRuleInit{suffix} = 0;",
                ParserEmbeddedCodeLocation.RuleAfter => $"int transformedRuleAfter{suffix} = 0;",
                ParserEmbeddedCodeLocation.LexerSemanticPredicate => $"true /* TRANSFORMED_LEXER_PREDICATE_{suffix} */",
                ParserEmbeddedCodeLocation.LexerInlineAction => $"int transformedLexerAction{suffix} = 0;",
                _ => throw new ArgumentOutOfRangeException(nameof(location), location, "Unknown location.")
            };
        }
    }


    /// <summary>
    /// Expression compiler spy that records every compile request.
    /// </summary>
    private sealed class RecordingExpressionCompiler : IExpressionCompiler
    {
        private readonly Expression _expression;

        public RecordingExpressionCompiler(Expression expression)
        {
            _expression = expression;
        }

        public int CompileCount { get; private set; }

        public List<string> Contents { get; } = [];

        public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null)
        {
            CompileCount++;
            Contents.Add(content);
            return _expression;
        }
    }

    /// <summary>
    /// Captures one transformer invocation and its passive metadata snapshot.
    /// </summary>
    private sealed record RecordedTransformationCall(
        int Index,
        string RawCode,
        string TransformedCode,
        ParserEmbeddedCodeLocation Location,
        string? GrammarName,
        string? RuleName,
        IReadOnlyList<ParserEmbeddedRuleDeclarationDescriptor> Parameters,
        IReadOnlyList<ParserEmbeddedRuleDeclarationDescriptor> Locals,
        IReadOnlyList<ParserEmbeddedRuleDeclarationDescriptor> Returns,
        IReadOnlyDictionary<string, ParserEmbeddedRuleLabelDescriptor> Labels);
}

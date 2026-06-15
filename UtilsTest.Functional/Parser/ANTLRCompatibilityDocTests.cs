using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies every claim and usage example in Utils.Parser/ANTLRCompatibility.md.
/// Each test is named after the section it covers.
/// </summary>
[TestClass]
public class ANTLRCompatibilityDocTests
{
    // ─── superClass / ILexerExtension ─────────────────────────────────────────

    [TestMethod]
    public void SuperClass_IsStoredInEffectiveOptions()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass = IndentTracker; }
            start : 'x' ;
            """);

        Assert.AreEqual("IndentTracker", def.EffectiveOptions.ParserSuperClass);
    }

    [TestMethod]
    public void SuperClass_IsStoredInExtensionBindings()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass = IndentTracker; }
            start : 'x' ;
            """);

        Assert.AreEqual(1, def.ExtensionBindings.Count);
        var binding = def.ExtensionBindings[0];
        Assert.AreEqual("IndentTracker", binding.SuperClassName);
    }

    [TestMethod]
    public void SuperClass_ExtensionBinding_ExposesLexerRuleNames()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass = MyExt; }
            start : ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var binding = def.ExtensionBindings[0];
        Assert.IsTrue(binding.LexerRuleNames.Contains("ID"));
        Assert.IsTrue(binding.LexerRuleNames.Contains("WS"));
    }

    [TestMethod]
    public void SuperClass_ExtensionBinding_ExposesDeclaredTokensAndChannels()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass = MyExt; }
            tokens { INDENT, DEDENT }
            channels { COMMENTS }
            start : 'x' ;
            """);

        // Tokens and channels from tokens{}/channels{} blocks land in both
        // ParserDefinition.DeclaredTokens / DeclaredChannels and in ExtensionBindings.
        Assert.IsTrue(def.DeclaredTokens.Contains("INDENT"));
        Assert.IsTrue(def.DeclaredTokens.Contains("DEDENT"));
        Assert.IsTrue(def.DeclaredChannels.Contains("COMMENTS"));

        // ExtensionBindings mirrors the same sets.
        var binding = def.ExtensionBindings[0];
        Assert.IsTrue(binding.DeclaredTokens.Contains("INDENT"));
        Assert.IsTrue(binding.DeclaredTokens.Contains("DEDENT"));
        Assert.IsTrue(binding.DeclaredChannels.Contains("COMMENTS"));
    }

    [TestMethod]
    public void SuperClass_ExtensionRegistered_ContextExposesBindings()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass = IndentTracker; }
            start : 'x' ;
            X : 'x' ;
            """);

        LexerExtensionContext? capturedContext = null;
        var ext = new CapturingLexerExtension(ctx => capturedContext = ctx);
        var lexer = new LexerEngine(def);
        _ = lexer.Tokenize(new StringReader("x"), new LexerEngineOptions { Extensions = [ext] }).ToList();

        Assert.IsNotNull(capturedContext);
        Assert.AreEqual(1, capturedContext!.Definition.ExtensionBindings.Count);
        Assert.AreEqual("IndentTracker",
            capturedContext.Definition.ExtensionBindings[0].SuperClassName);
    }

    [TestMethod]
    public void SuperClass_AllExtensionHooksAreCalled()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass = MyExt; }
            start : 'x' ;
            X : 'x' ;
            """);

        bool tryReadCalled = false, afterTokenCalled = false, endOfInputCalled = false;
        var ext = new CallTrackingLexerExtension(
            onTryRead: () => tryReadCalled = true,
            onAfterToken: () => afterTokenCalled = true,
            onEndOfInput: () => endOfInputCalled = true);

        var lexer = new LexerEngine(def);
        _ = lexer.Tokenize(new StringReader("x"), new LexerEngineOptions { Extensions = [ext] }).ToList();

        Assert.IsTrue(tryReadCalled,    "TryReadTokens was not called");
        Assert.IsTrue(afterTokenCalled, "OnAfterToken was not called");
        Assert.IsTrue(endOfInputCalled, "OnEndOfInput was not called");
    }

    [TestMethod]
    public void SuperClass_ExtensionCanGuardBySuperClassName()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass = IndentTracker; }
            start : 'x' ;
            X : 'x' ;
            """);

        int guardedCallCount = 0;
        var ext = new GuardingLexerExtension("IndentTracker", onMatch: () => guardedCallCount++);
        var lexer = new LexerEngine(def);
        _ = lexer.Tokenize(new StringReader("x"), new LexerEngineOptions { Extensions = [ext] }).ToList();

        Assert.IsTrue(guardedCallCount > 0,
            "Extension guard matched superClass name but was not invoked");
    }

    // ─── Semantic predicates ──────────────────────────────────────────────────

    [TestMethod]
    public void SemanticPredicate_EvaluatorReceivesPredicateCode()
    {
        string? capturedCode = null;
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : {IsKeyword()}? ID | ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var evaluator = new CapturingPredicateEvaluator(ctx =>
        {
            capturedCode = ctx.PredicateCode;
            return SemanticPredicateEvaluationOutcome.NotEvaluated();
        });
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            SemanticPredicateEvaluator = evaluator
        };
        var tokens = new LexerEngine(def).Tokenize(new StringReader("foo")).ToList();
        new ParserEngine(def, policy).Parse(tokens);

        Assert.IsNotNull(capturedCode);
        Assert.IsTrue(capturedCode!.Contains("IsKeyword()"));
    }

    [TestMethod]
    public void SemanticPredicate_EvaluatorReceivesRuleAndPosition()
    {
        Rule? capturedRule = null;
        int capturedPosition = -1;
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : {check()}? ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var evaluator = new CapturingPredicateEvaluator(ctx =>
        {
            capturedRule = ctx.Rule;
            capturedPosition = ctx.InputPosition;
            return SemanticPredicateEvaluationOutcome.Satisfied;
        });
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            SemanticPredicateEvaluator = evaluator
        };
        var tokens = new LexerEngine(def).Tokenize(new StringReader("foo")).ToList();
        new ParserEngine(def, policy).Parse(tokens);

        Assert.IsNotNull(capturedRule);
        Assert.AreEqual("start", capturedRule!.Name);
        Assert.AreEqual(0, capturedPosition);
    }

    // ─── Inline actions ───────────────────────────────────────────────────────

    [TestMethod]
    public void InlineAction_ExecutorReceivesActionCodeAndContext()
    {
        string? capturedCode = null;
        Rule? capturedRule = null;
        int capturedPosition = -1;
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : { doSomething(); } 'x' ;
            X : 'x' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var executor = new CapturingActionExecutor(ctx =>
        {
            capturedCode = ctx.ActionCode;
            capturedRule = ctx.Rule;
            capturedPosition = ctx.InputPosition;
            return ParserActionExecutionOutcome.Executed;
        });
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            ParserActionExecutor = executor
        };
        var tokens = new LexerEngine(def).Tokenize(new StringReader("x")).ToList();
        new ParserEngine(def, policy).Parse(tokens);

        Assert.IsNotNull(capturedCode);
        Assert.IsTrue(capturedCode!.Contains("doSomething()"));
        Assert.AreEqual("start", capturedRule!.Name);
        Assert.AreEqual(0, capturedPosition);
    }

    [TestMethod]
    public void InlineAction_DefaultPolicy_DoesNotExecuteAction()
    {
        bool executorCalled = false;
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : { sideEffect(); } 'x' ;
            X : 'x' ;
            """);

        var executor = new CapturingActionExecutor(_ =>
        {
            executorCalled = true;
            return ParserActionExecutionOutcome.Executed;
        });

        // default policy — executor not registered
        var tokens = new LexerEngine(def).Tokenize(new StringReader("x")).ToList();
        new ParserEngine(def).Parse(tokens);

        Assert.IsFalse(executorCalled);
    }

    // ─── Rule actions @init / @after ──────────────────────────────────────────

    [TestMethod]
    public void RuleAction_Init_StoredInRule()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start
                @init { int x = 0; }
                : 'x' ;
            X : 'x' ;
            """);

        var rule = def.AllRules["start"];
        Assert.IsNotNull(rule.InitAction);
        Assert.IsTrue(rule.InitAction!.RawCode.Contains("int x = 0"));
    }

    [TestMethod]
    public void RuleAction_After_StoredInRule()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start
                @after { cleanup(); }
                : 'x' ;
            X : 'x' ;
            """);

        var rule = def.AllRules["start"];
        Assert.IsNotNull(rule.AfterAction);
        Assert.IsTrue(rule.AfterAction!.RawCode.Contains("cleanup()"));
    }

    [TestMethod]
    public void RuleAction_UnknownName_EmitsActionIgnoredDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        Antlr4GrammarConverter.Parse("""
            grammar G;
            start
                @before { setup(); }
                : 'x' ;
            X : 'x' ;
            """, diagnostics);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.ActionIgnored.Code));
    }

    // ─── Grammar-level actions ────────────────────────────────────────────────

    [TestMethod]
    public void GrammarAction_Header_StoredInDefinition()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            @header { using System; }
            start : 'x' ;
            X : 'x' ;
            """);

        Assert.IsTrue(def.Actions.Any(a => a.Name == "header"));
        var header = def.Actions.First(a => a.Name == "header");
        Assert.IsTrue(header.RawCode.Contains("using System"));
    }

    [TestMethod]
    public void GrammarAction_Members_StoredInDefinition()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            @members { int counter = 0; }
            start : 'x' ;
            X : 'x' ;
            """);

        Assert.IsTrue(def.Actions.Any(a => a.Name == "members"));
    }

    // ─── Parsed and stored — no runtime semantics ────────────────────────────

    [TestMethod]
    public void RuleParameters_StoredAsRawText()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start[int level] : 'x' ;
            X : 'x' ;
            """);

        var rule = def.AllRules["start"];
        Assert.IsNotNull(rule.Parameters);
        Assert.IsTrue(rule.Parameters!.Count > 0);
        Assert.IsTrue(rule.Parameters![0].Type.Contains("int level")
                   || rule.Parameters[0].Name.Contains("int level"));
    }

    [TestMethod]
    public void RuleReturns_StoredAsRawText()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start returns [int value] : 'x' ;
            X : 'x' ;
            """);

        var rule = def.AllRules["start"];
        Assert.IsNotNull(rule.Returns);
        Assert.IsTrue(rule.Returns!.Count > 0);
        Assert.IsTrue(rule.Returns![0].Type.Contains("int value")
                   || rule.Returns[0].Name.Contains("int value"));
    }

    [TestMethod]
    public void Locals_Discarded_NoError()
    {
        var diagnostics = new DiagnosticBag();
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start
                locals [int x]
                : 'x' ;
            X : 'x' ;
            """, diagnostics);

        Assert.IsTrue(def.AllRules.ContainsKey("start"));
        Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
        Assert.AreEqual(1, diagnostics.Count(d => d.Code == ParserDiagnostics.RuleLocalsIgnored.Code),
            "Converter must emit exactly one RuleLocalsIgnored diagnostic for the locals clause.");
    }

    [TestMethod]
    public void Throws_Discarded_NoError()
    {
        var diagnostics = new DiagnosticBag();
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start
                throws RecognitionException
                : 'x' ;
            X : 'x' ;
            """, diagnostics);

        Assert.IsTrue(def.AllRules.ContainsKey("start"));
        Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
        Assert.AreEqual(1, diagnostics.Count(d => d.Code == ParserDiagnostics.RuleExceptionMetadataIgnored.Code),
            "Converter must emit exactly one RuleExceptionMetadataIgnored diagnostic for the throws clause.");
    }

    [TestMethod]
    public void CatchFinally_Discarded_NoError()
    {
        var diagnostics = new DiagnosticBag();
        // catch/finally must appear immediately after the rule's semicolon (still part of that rule spec).
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : X ;
            catch [Exception e] { handle(e); }
            finally { cleanup(); }
            X : 'x' ;
            """, diagnostics);

        Assert.IsTrue(def.AllRules.ContainsKey("start"));
        Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
        Assert.AreEqual(1, diagnostics.Count(d => d.Code == ParserDiagnostics.RuleExceptionMetadataIgnored.Code),
            "Converter must emit exactly one RuleExceptionMetadataIgnored diagnostic for catch/finally blocks.");
    }

    // ─── Partially supported ──────────────────────────────────────────────────

    [TestMethod]
    public void Labels_OnRuleRef_AreStored()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : e=term other=term ;
            term  : ID | NUM ;
            ID    : ('a'..'z' | 'A'..'Z' | '_')+ ;
            NUM   : ('0'..'9')+ ;
            WS    : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var startRule = def.AllRules["start"];
        var alt = ((Alternation)startRule.Content).Alternatives[0];
        var items = ((Sequence)alt.Content).Items;
        var labeledRefs = items.OfType<RuleRef>().Where(r => r.Label != null).ToList();
        Assert.IsTrue(labeledRefs.Count >= 1);
        Assert.IsTrue(labeledRefs.Any(r => r.Label!.Label == "e"));
    }

    [TestMethod]
    public void Import_SingleFile_EmitsImportParsedButNotResolvedDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        Antlr4GrammarConverter.Parse("""
            grammar G;
            import CommonLexer;
            start : 'x' ;
            X : 'x' ;
            """, diagnostics);

        Assert.IsTrue(diagnostics.Any(d =>
            d.Code == ParserDiagnostics.ImportParsedButNotResolved.Code));
    }

    // ─── Not supported — intentional exclusions ───────────────────────────────

    [TestMethod]
    public void IndirectLeftRecursion_ThrowsAndEmitsDiagnosticError()
    {
        var diagnostics = new DiagnosticBag();
        Assert.ThrowsException<GrammarValidationException>(() =>
            Antlr4GrammarConverter.Parse("""
                grammar G;
                start : a ;
                a : b ;
                b : a '+' INT | INT ;
                INT : ('0'..'9')+ ;
                WS  : (' ' | '\t' | '\r' | '\n')+ -> skip ;
                """, diagnostics));

        Assert.IsTrue(diagnostics.Any(d =>
            d.Code == ParserDiagnostics.IndirectLeftRecursionNotSupported.Code));
    }

    [TestMethod]
    public void ParseFailure_ReturnsErrorNode()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : 'x' ;
            X : 'x' ;
            """);

        var tokens = new LexerEngine(def).Tokenize(new StringReader("y")).ToList();
        var result = new ParserEngine(def).Parse(tokens);

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
    }

    // ─── Runtime observation ──────────────────────────────────────────────────

    [TestMethod]
    public void RuntimeObservation_RecorderAndWriters_FullWorkflow()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : 'x' ;
            X : 'x' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var recorder = new RuntimeObservationRecorder();
        var policy = ParserRuntimeFeaturePolicy.Default with { RuntimeObserver = recorder };
        var tokens = new LexerEngine(def).Tokenize(new StringReader("x")).ToList();
        var result = new ParserEngine(def, policy).Parse(tokens);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue(recorder.Observations.Count > 0);

        string text = RuntimeObservationTextWriter.Write(recorder.Observations);
        string json = RuntimeObservationJsonWriter.Write(recorder.Observations);

        Assert.IsFalse(string.IsNullOrWhiteSpace(text));
        Assert.IsTrue(json.StartsWith("["));
    }

    [TestMethod]
    public void RuntimeObservation_DoesNotAffectParseOutcome()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : 'x' ;
            X : 'x' ;
            """);

        var tokens = new LexerEngine(def).Tokenize(new StringReader("x")).ToList();

        var withoutObserver = new ParserEngine(def).Parse(tokens);

        var recorder = new RuntimeObservationRecorder();
        var policy = ParserRuntimeFeaturePolicy.Default with { RuntimeObserver = recorder };
        var withObserver = new ParserEngine(def, policy).Parse(tokens);

        Assert.IsNotInstanceOfType(withoutObserver, typeof(ErrorNode));
        Assert.IsNotInstanceOfType(withObserver, typeof(ErrorNode));
        Assert.AreEqual(withoutObserver.Rule.Name, withObserver.Rule.Name);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private sealed class CapturingLexerExtension(Action<LexerExtensionContext> capture) : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context)
        {
            capture(context);
            return [];
        }
        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];
        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
    }

    private sealed class CallTrackingLexerExtension(
        Action onTryRead,
        Action onAfterToken,
        Action onEndOfInput) : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context)
        {
            onTryRead();
            return [];
        }
        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context)
        {
            onAfterToken();
            return [];
        }
        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context)
        {
            onEndOfInput();
            return [];
        }
    }

    private sealed class GuardingLexerExtension(string superClassName, Action onMatch) : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context)
        {
            if (context.Definition.ExtensionBindings.Any(b => b.SuperClassName == superClassName))
                onMatch();
            return [];
        }
        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];
        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
    }

    private sealed class CapturingPredicateEvaluator(
        Func<SemanticPredicateEvaluationContext, SemanticPredicateEvaluationOutcome> evaluate)
        : ISemanticPredicateEvaluator
    {
        public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
            => evaluate(context);
    }

    private sealed class CapturingActionExecutor(
        Func<ParserActionExecutionContext, ParserActionExecutionOutcome> execute)
        : IParserActionExecutor
    {
        public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
            => execute(context);
    }
}

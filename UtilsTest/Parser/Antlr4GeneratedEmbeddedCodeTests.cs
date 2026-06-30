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
    /// Ensures the optional C# transformer rewrites lexer <c>$text</c> actions to generated helpers.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerTextAttribute_RewritesAndReadsAcceptedText()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : A ;
            A : 'a' { Seen = $text; } ;
            """;

        string source = EmitWithAntlrStyleTransformer(grammar);
        StringAssert.Contains(source, "Seen = GetRequiredLexerText(context);");
        int hookStart = source.IndexOf("private void __LexerAction_A_0_1_0", StringComparison.Ordinal);
        Assert.IsTrue(hookStart >= 0, source);
        Assert.IsFalse(source.Substring(hookStart).Contains("Seen = $text;", StringComparison.Ordinal), source);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);
        var conservativeContext = CreateExecutionContext(assembly);
        _ = InvokeParse(assembly, "Parse", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("a", ReadInstanceStringField(context, "Seen"));
        Assert.AreEqual("", ReadInstanceStringField(conservativeContext, "Seen"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> exposes the full accepted token chunk for quantified matches.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerTextAttribute_ReadsLongTokenText()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : A ;
            A : 'a'+ { Seen = $text; } ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "aaa", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("aaa", ReadInstanceStringField(context, "Seen"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> before and after a fragment exposes the accepted token context text.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerTextAttribute_AroundFragmentReadsAcceptedTokenText()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Before = "";
                public string After = "";
            }

            start : A ;
            A : 'a' { Before = $text; } F { After = $text; } ;
            fragment F : 'b' ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "ab", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("ab", ReadInstanceStringField(context, "Before"));
        Assert.AreEqual("ab", ReadInstanceStringField(context, "After"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> inside fragments exposes the owning accepted token context text.
    /// </summary>
    [DataTestMethod]
    [DataRow("fragment F : 'a' { Seen = $text; } ;", "a")]
    [DataRow("fragment F : 'a' 'b' { Seen = $text; } ;", "ab")]
    public void ParseWithEmbeddedCode_LexerTextAttribute_InFragmentReadsAcceptedTokenText(string fragmentRule, string input)
    {
        string grammar = $$"""
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : A ;
            A : F ;
            {{fragmentRule}}
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, input, context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(input, ReadInstanceStringField(context, "Seen"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> inside a referenced lexer rule exposes the accepted outer token context text.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerTextAttribute_InLexerRuleReferenceReadsAcceptedTokenText()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : A ;
            A : B ;
            B : 'b' { Seen = $text; } ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "b", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("b", ReadInstanceStringField(context, "Seen"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> reads the text accepted by the selected alternative.
    /// </summary>
    [DataTestMethod]
    [DataRow("a", "a")]
    [DataRow("bc", "bc")]
    public void ParseWithEmbeddedCode_LexerTextAttribute_InAlternativeReadsSelectedText(string input, string expectedText)
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : A ;
            A
                : 'a' { Seen = $text; }
                | 'b' 'c' { Seen = $text; }
                ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, input, context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(expectedText, ReadInstanceStringField(context, "Seen"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> is read only on accepted predicate paths.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerTextAttribute_WithPredicateReadsOnlyAcceptedPath()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = false;
                public string Seen = "";
            }

            start : A ;
            A : { Enabled }? 'a' { Seen = $text; } ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object disabledContext = CreateExecutionContext(assembly);
        object enabledContext = CreateExecutionContext(assembly);
        WriteInstanceBoolField(enabledContext, "Enabled", true);

        var disabledResult = InvokeParseWithContext(assembly, "a", disabledContext);
        var enabledResult = InvokeParseWithContext(assembly, "a", enabledContext);

        Assert.IsInstanceOfType(disabledResult, typeof(ErrorNode));
        Assert.AreEqual("", ReadInstanceStringField(disabledContext, "Seen"));
        Assert.IsNotInstanceOfType(enabledResult, typeof(ErrorNode));
        Assert.AreEqual("a", ReadInstanceStringField(enabledContext, "Seen"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> reads the accepted token text before skip commands suppress token emission.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerTextAttribute_WithSkipCommandReadsSkippedText()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : ;
            A : 'a' { Seen = $text; } -> skip ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("a", ReadInstanceStringField(context, "Seen"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> reads accepted text before a type command retags the token.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerTextAttribute_WithTypeCommandReadsOriginalText()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : B ;
            A : 'a' { Seen = $text; } -> type(B) ;
            B : 'b' ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("a", ReadInstanceStringField(context, "Seen"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> reads accepted text before a channel command hides the token.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerTextAttribute_WithChannelCommandReadsHiddenText()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : ;
            A : 'a' { Seen = $text; } -> channel(HIDDEN) ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("a", ReadInstanceStringField(context, "Seen"));
    }

    /// <summary>
    /// Ensures lexer <c>$text</c> with more reads each accepted chunk before final accumulation.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerTextAttribute_WithMoreCommandReadsChunkText()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string First = "";
                public string Second = "";
            }

            start : A ;
            M : 'm' { First = $text; } -> more ;
            A : 'a' { Second = $text; } ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "ma", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("m", ReadInstanceStringField(context, "First"));
        Assert.AreEqual("a", ReadInstanceStringField(context, "Second"));
    }

    /// <summary>
    /// Ensures readable lexer attributes expose passive runtime metadata before lexer commands are applied.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerMetadataAttributes_ReadCurrentMatchMetadata()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string SeenType = "";
                public string SeenChannel = "";
                public string SeenMode = "";
            }

            start : A ;
            A : 'a' { SeenType = $type; SeenChannel = $channel; SeenMode = $mode; } ;
            """;

        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("A", ReadInstanceStringField(context, "SeenType"));
        Assert.AreEqual("DEFAULT_CHANNEL", ReadInstanceStringField(context, "SeenChannel"));
        Assert.AreEqual("DEFAULT_MODE", ReadInstanceStringField(context, "SeenMode"));
    }

    /// <summary>
    /// Ensures the no-op transformer preserves lexer attribute source unchanged.
    /// </summary>
    [TestMethod]
    public void Emit_DefaultTransformer_PreservesLexerAttributes()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : A ;
            A : 'a' { Seen = $text; } ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "Seen = $text;");
    }

    /// <summary>
    /// Ensures unsupported lexer attributes in predicates are rejected by the transformer before raw C# compilation.
    /// </summary>
    [TestMethod]
    public void EmitWithAntlrStyleTransformer_LexerPredicateAttribute_ReportsDiagnostic()
    {
        const string grammar = """
            grammar P;

            start : A ;
            A : { $text == "a" }? 'a' ;
            """;

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EmitWithAntlrStyleTransformer(grammar));

        StringAssert.Contains(exception.Message, "APU0105");
        StringAssert.Contains(exception.Message, "not supported in lexer predicates");
    }

    /// <summary>
    /// Ensures unsupported lexer attribute writes report deterministic transformer diagnostics.
    /// </summary>
    [DataTestMethod]
    [DataRow("$type = B;")]
    [DataRow("$text ??= \"fallback\";")]
    public void EmitWithAntlrStyleTransformer_LexerAttributeWrite_ReportsDiagnostic(string actionCode)
    {
        string grammar = $$"""
            grammar P;

            start : A ;
            A : 'a' { {{actionCode}} } ;
            B : 'b' ;
            """;

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EmitWithAntlrStyleTransformer(grammar));

        StringAssert.Contains(exception.Message, "APU0105");
        StringAssert.Contains(exception.Message, "Lexer attribute writes are not supported");
    }

    /// <summary>
    /// Ensures unsupported lexer attributes report deterministic transformer diagnostics.
    /// </summary>
    [TestMethod]
    public void EmitWithAntlrStyleTransformer_UnknownLexerAttribute_ReportsDiagnostic()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : A ;
            A : 'a' { Seen = $foo; } ;
            """;

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EmitWithAntlrStyleTransformer(grammar));

        StringAssert.Contains(exception.Message, "APU0105");
        StringAssert.Contains(exception.Message, "Lexer attribute '$foo' is not supported");
    }

    /// <summary>
    /// Ensures lexer attribute-looking text inside strings and comments is ignored by the transformer.
    /// </summary>
    [TestMethod]
    public void EmitWithAntlrStyleTransformer_LexerAttributesInStringsAndComments_AreIgnored()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public string Seen = "";
            }

            start : A ;
            A : 'a' { Seen = "$text"; /* $text */ } ;
            """;

        string source = EmitWithAntlrStyleTransformer(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("$text", ReadInstanceStringField(context, "Seen"));
    }

    /// <summary>
    /// Ensures conservative generated Parse does not execute lexer inline actions.
    /// </summary>
    [TestMethod]
    public void Parse_LexerInlineAction_DoesNotExecuteAction()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public static int Count;
            }

            start : A ;
            A : 'a' { Count++; } ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "private void __LexerAction_A_0_1_0");
        var assembly = CompileGeneratedSource(source);

        var result = InvokeParse(assembly, "Parse", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "Count"));
    }

    /// <summary>
    /// Ensures opt-in generated parsing executes a simple lexer inline action.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerInlineAction_ExecutesAction()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : A ;
            A : 'a' { Count++; } ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures lexer inline actions can call members injected through @lexer::members.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerInlineAction_CanCallLexerMember()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
                public void Mark() { Count++; }
            }

            start : A ;
            A : 'a' { Mark(); } ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(context, "Count"));
    }


    /// <summary>
    /// Ensures lexer action dispatch uses runtime alternative and element indexes.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerInlineAction_ReceivesRuntimeIndexes()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : A ;
            A
                : 'a' { Count += context.AlternativeIndex == 0 && context.ElementIndex == 1 ? 1 : 100; }
                | 'b' { Count += context.AlternativeIndex == 1 && context.ElementIndex == 1 ? 10 : 1000; }
                ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "b", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(10, ReadInstanceIntField(context, "Count"));
    }


    /// <summary>
    /// Ensures conservative generated Parse does not execute lexer predicates and keeps the predicate path non-matching.
    /// </summary>
    [TestMethod]
    public void Parse_LexerPredicate_RemainsConservative()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = false;
            }

            start : A ;
            A : { Enabled }? 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParse(assembly, "Parse", "a");

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
        Assert.IsFalse(ReadInstanceBoolField(context, "Enabled"));
    }

    /// <summary>
    /// Ensures opt-in generated parsing evaluates a true lexer predicate during matching.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateTrue_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = true;
            }

            start : A ;
            A : { Enabled }? 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures opt-in generated parsing rejects a token path when a lexer predicate is false.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateFalse_RejectsParse()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = false;
            }

            start : A ;
            A : { Enabled }? 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures lexer predicates can call members injected through <c>@lexer::members</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicate_CanCallLexerMember()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool IsEnabled() => true;
            }

            start : A ;
            A : { IsEnabled() }? 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures lexer predicate dispatch uses alternative and element indexes while matching alternatives.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateAlternative_UsesRuntimeIndexes()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool UseB = true;
            }

            start : A ;
            A
                : { false }? 'a'
                | { UseB && context.AlternativeIndex == 1 && context.ElementIndex == 0 }? 'b'
                ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "b", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures generated source contains lexer predicate hooks and keeps conservative Parse disconnected from them.
    /// </summary>
    [TestMethod]
    public void Emit_LexerPredicate_GeneratesStableHookOutsideConservativeParse()
    {
        const string grammar = """
            grammar P;
            start : A ;
            A : { true }? 'a' { } ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "private bool __LexerPredicate_A_0_0_0");
        StringAssert.Contains(source, "GeneratedLexerPredicateEvaluator");
        StringAssert.Contains(source, "&& string.Equals(context.PredicateCode, \" true \"");
        StringAssert.Contains(source, "&& context.AlternativeIndex == 0");
        StringAssert.Contains(source, "&& context.ElementIndex == 0");
        StringAssert.Contains(source, "private void __LexerAction_A_0_2_1");
        int parseIndex = source.IndexOf("public static ParseNode Parse(", StringComparison.Ordinal);
        int optInIndex = source.IndexOf("public static ParseNode ParseWithEmbeddedCode(", StringComparison.Ordinal);
        string conservativeSection = source.Substring(parseIndex, optInIndex - parseIndex);
        Assert.IsFalse(conservativeSection.Contains("__LexerPredicate_A_0_0_0", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures lexer predicates run before accepted lexer actions and prevent later action execution when false.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateAndAction_ExecutesActionOnlyWhenPredicateTrue()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = true;
                public int Count;
            }

            start : A ;
            A : { Enabled }? 'a' { Count++; } ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object enabledContext = CreateExecutionContext(assembly);
        var enabledResult = InvokeParseWithContext(assembly, "a", enabledContext);

        object disabledContext = CreateExecutionContext(assembly);
        WriteInstanceBoolField(disabledContext, "Enabled", false);
        var disabledResult = InvokeParseWithContext(assembly, "a", disabledContext);

        Assert.IsNotInstanceOfType(enabledResult, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(enabledContext, "Count"));
        Assert.IsInstanceOfType(disabledResult, typeof(ErrorNode));
        Assert.AreEqual(0, ReadInstanceIntField(disabledContext, "Count"));
    }

    /// <summary>
    /// Ensures generated source has lexer action hooks without invoking them from conservative Parse.
    /// </summary>
    [TestMethod]
    public void Emit_LexerInlineAction_GeneratesStableHookOutsideConservativeParse()
    {
        const string grammar = """
            lexer grammar L;
            A : 'a' { } ;
            B : { IsEnabled() }? 'b' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "private void __LexerAction_A_0_1_0");
        StringAssert.Contains(source, "&& context.AlternativeIndex == 0");
        StringAssert.Contains(source, "&& context.ElementIndex == 1");
        StringAssert.Contains(source, "private bool __LexerPredicate_B_0_0_1");
        StringAssert.Contains(source, "public static ParseNode Parse(");
        int parseIndex = source.IndexOf("public static ParseNode Parse(", StringComparison.Ordinal);
        int optInIndex = source.IndexOf("public static ParseNode ParseWithEmbeddedCode(", StringComparison.Ordinal);
        string conservativeSection = source.Substring(parseIndex, optInIndex - parseIndex);
        Assert.IsFalse(conservativeSection.Contains("__LexerAction_A_0_1_0", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures a lexer predicate declared inside a fragment participates in generated opt-in matching while conservative parsing remains disconnected.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateInFragment_EvaluatesForOwningFragmentRule()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = true;
            }

            start : A ;
            A : F ;
            fragment F : { Enabled }? 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object enabledContext = CreateExecutionContext(assembly);
        object disabledContext = CreateExecutionContext(assembly);
        WriteInstanceBoolField(disabledContext, "Enabled", false);

        var enabledResult = InvokeParseWithContext(assembly, "a", enabledContext);
        var disabledResult = InvokeParseWithContext(assembly, "a", disabledContext);
        var conservativeResult = InvokeParse(assembly, "Parse", "a");

        Assert.IsNotInstanceOfType(enabledResult, typeof(ErrorNode));
        Assert.IsInstanceOfType(disabledResult, typeof(ErrorNode));
        Assert.IsInstanceOfType(conservativeResult, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures lexer actions collected through a fragment execute only after the owning token is accepted.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerActionInFragment_ExecutesAfterTokenAcceptance()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public static int Count;
            }

            start : A ;
            A : F ;
            fragment F : 'a' { Count++; } ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object embeddedContext = CreateExecutionContext(assembly);
        var conservativeResult = InvokeParse(assembly, "Parse", "a");

        Assert.IsNotInstanceOfType(conservativeResult, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "Count"));

        var embeddedResult = InvokeParseWithContext(assembly, "a", embeddedContext);

        Assert.IsNotInstanceOfType(embeddedResult, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "Count"));
    }

    /// <summary>
    /// Ensures a false lexer predicate in a fragment rejects the token path and prevents later accepted-token actions from running.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateFalseInFragment_PreventsFollowingAction()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = false;
                public int Count;
            }

            start : A ;
            A : F { Count++; } ;
            fragment F : { Enabled }? 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures duplicate lexer action source at multiple positions emits and dispatches with distinct source-position keys.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerActionsAtMultiplePositions_DispatchByPosition()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : A ;
            A
                : 'a' { Count++; }
                | 'b' { Count++; }
                ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "private void __LexerAction_A_0_1_0");
        StringAssert.Contains(source, "private void __LexerAction_A_1_1_1");
        StringAssert.Contains(source, "&& string.Equals(context.ActionCode, \" Count++; \", global::System.StringComparison.Ordinal)");
        StringAssert.Contains(source, "&& context.AlternativeIndex == 0");
        StringAssert.Contains(source, "&& context.AlternativeIndex == 1");
        var assembly = CompileGeneratedSource(source);
        object aContext = CreateExecutionContext(assembly);
        object bContext = CreateExecutionContext(assembly);

        var aResult = InvokeParseWithContext(assembly, "a", aContext);
        var bResult = InvokeParseWithContext(assembly, "b", bContext);

        Assert.IsNotInstanceOfType(aResult, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(aContext, "Count"));
        Assert.IsNotInstanceOfType(bResult, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(bContext, "Count"));
    }

    /// <summary>
    /// Ensures duplicate lexer predicate source at multiple positions emits and dispatches with distinct source-position keys.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicatesAtMultiplePositions_DispatchByPosition()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool UseA = false;
                public bool UseB = true;
            }

            start : A ;
            A
                : { (context.AlternativeIndex == 0 && UseA) || (context.AlternativeIndex == 1 && UseB) }? 'a'
                | { (context.AlternativeIndex == 0 && UseA) || (context.AlternativeIndex == 1 && UseB) }? 'b'
                ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "private bool __LexerPredicate_A_0_0_0");
        StringAssert.Contains(source, "private bool __LexerPredicate_A_1_0_1");
        StringAssert.Contains(source, "&& string.Equals(context.PredicateCode, \" (context.AlternativeIndex == 0 && UseA) || (context.AlternativeIndex == 1 && UseB) \", global::System.StringComparison.Ordinal)");
        StringAssert.Contains(source, "&& context.AlternativeIndex == 0");
        StringAssert.Contains(source, "&& context.AlternativeIndex == 1");
        var assembly = CompileGeneratedSource(source);
        object aContext = CreateExecutionContext(assembly);
        object bContext = CreateExecutionContext(assembly);

        var aResult = InvokeParseWithContext(assembly, "a", aContext);
        var bResult = InvokeParseWithContext(assembly, "b", bContext);

        Assert.IsInstanceOfType(aResult, typeof(ErrorNode));
        Assert.IsNotInstanceOfType(bResult, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures lexer predicates and actions in simple quantifiers are evaluated per matched iteration without leaking failed attempts.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateAndActionInQuantifier_DoNotLeakFailedAttempts()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = true;
                public int Count;
            }

            start : A ;
            A : ({ Enabled }? 'a' { Count++; })+ ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object enabledContext = CreateExecutionContext(assembly);
        object disabledContext = CreateExecutionContext(assembly);
        WriteInstanceBoolField(disabledContext, "Enabled", false);

        var enabledResult = InvokeParseWithContext(assembly, "aaa", enabledContext);
        var disabledResult = InvokeParseWithContext(assembly, "aaa", disabledContext);

        Assert.IsNotInstanceOfType(enabledResult, typeof(ErrorNode));
        Assert.AreEqual(3, ReadInstanceIntField(enabledContext, "Count"));
        Assert.IsInstanceOfType(disabledResult, typeof(ErrorNode));
        Assert.AreEqual(0, ReadInstanceIntField(disabledContext, "Count"));
    }

    /// <summary>
    /// Ensures a false lexer predicate rejects only its current alternative and still allows another alternative to match.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateFalseInAlternative_AllowsOtherAlternative()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool UseB = true;
            }

            start : A ;
            A
                : { false }? 'a'
                | { UseB }? 'b'
                ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var bResult = InvokeParseWithContext(assembly, "b", context);
        var aResult = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(bResult, typeof(ErrorNode));
        Assert.IsInstanceOfType(aResult, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures conservative parsing does not execute generated lexer predicates or lexer actions.
    /// </summary>
    [TestMethod]
    public void Parse_LexerPredicateAndAction_RemainsConservative()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public static int Count;
                public bool Enabled = true;
            }

            start : A ;
            A : { Enabled }? 'a' { Count++; } ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);

        var result = InvokeParse(assembly, "Parse", "a");

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "Count"));
    }

    /// <summary>
    /// Ensures generated lexer actions still run before supported lexer commands are applied by the language-neutral lexer engine.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerActionWithSkipCommand_ExecutesActionAndSkipsToken()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : ;
            A : 'a' { Count++; } -> skip ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures a false lexer predicate prevents both later accepted-token actions and the skip command from taking effect.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateFalseWithSkipCommand_DoesNotExecuteActionOrSkip()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = false;
                public int Count;
            }

            start : ;
            A : { Enabled }? 'a' { Count++; } -> skip ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures a true lexer predicate allows the accepted-token action and skip command to run.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateTrueWithSkipCommand_ExecutesActionAndSkipsToken()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public bool Enabled = true;
                public int Count;
            }

            start : ;
            A : { Enabled }? 'a' { Count++; } -> skip ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures accepted lexer actions run before a supported channel command moves the token off the default parser channel.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerActionWithChannelCommand_ExecutesActionAndHidesToken()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : ;
            A : 'a' { Count++; } -> channel(HIDDEN) ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures a false lexer predicate prevents a channel command and later action from the rejected path.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateFalseWithChannelCommand_DoesNotExecuteActionOrCommand()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : ;
            A : { false }? 'a' { Count++; } -> channel(HIDDEN) ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures accepted lexer actions run before a supported type command retags the emitted token.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerActionWithTypeCommand_ExecutesActionAndRetagsToken()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : B ;
            A : 'a' { Count++; } -> type(B) ;
            B : 'b' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures a false lexer predicate prevents a type command and later action from the rejected path.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateFalseWithTypeCommand_DoesNotExecuteActionOrCommand()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : B ;
            A : { false }? 'a' { Count++; } -> type(B) ;
            B : 'b' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(0, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures actions attached to a token using more execute at the current accepted-chunk point.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerActionWithMoreCommand_ExecutesActionAndContinuesToken()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : A ;
            M : 'm' { Count++; } -> more ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "ma", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures accepted lexer actions work with pushMode and popMode commands.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerActionsWithModeCommands_ExecuteActionsAndSwitchModes()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : A B ;

            A : 'a' { Count++; } -> pushMode(SECOND) ;

            mode SECOND;
            B : 'b' { Count++; } -> popMode ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "ab", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures accepted lexer actions work with direct mode commands.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerActionsWithDirectModeCommand_ExecuteActionsAndSwitchModes()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : A B C ;

            A : 'a' { Count++; } -> mode(SECOND) ;
            C : 'c' { Count++; } ;

            mode SECOND;
            B : 'b' { Count++; } -> mode(DEFAULT_MODE) ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "abc", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(3, ReadInstanceIntField(context, "Count"));
    }

    /// <summary>
    /// Ensures a false predicate in a non-default mode rejects the path without executing its later action.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_LexerPredicateFalseInMode_DoesNotExecuteFollowingAction()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                public int Count;
            }

            start : A B ;

            A : 'a' { Count++; } -> pushMode(SECOND) ;

            mode SECOND;
            B : { false }? 'b' { Count++; } -> popMode ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        object context = CreateExecutionContext(assembly);

        var result = InvokeParseWithContext(assembly, "ab", context);

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadInstanceIntField(context, "Count"));
    }

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
        StringAssert.Contains(source, "private bool __Predicate_start_0_0_0");
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
    /// Ensures expression-bodied predicate hooks can use multiple contextual symbols and parse successfully.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateExpressionBody_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : { inputPosition == 0 && ruleName == "start" }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures block-bodied predicate hooks keep local variables and return statements as C# statements.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateBlockWithReturn_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : {
                var isStart = inputPosition == 0;
                return isStart && ruleName == "start";
            }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures one-line predicate statement blocks with conditional returns compile and parse through generated hooks.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateOneLineConditionalReturn_ParsesSuccessfully()
    {
        const string grammar = """
            grammar P;
            start : { if (inputPosition == 0) return true; return false; }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures predicate expressions containing return as part of an identifier stay expression-bodied.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateReturnIdentifier_DoesNotUseBlockBody()
    {
        const string grammar = """
            grammar P;
            start : { returnValue == true }? A ;
            A : 'a' ;
            """;
        const string userPartial = """
            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static bool returnValue = true;
            }
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source, userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures block-bodied predicate hooks can reject parsing through a generated runtime policy.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateBlockWithFalseReturn_RejectsParse()
    {
        const string grammar = """
            grammar P;
            start : {
                var blocked = true;
                return !blocked;
            }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        var assembly = CompileGeneratedSource(source);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsInstanceOfType(result, typeof(ErrorNode));
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

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
                {
                    ActionCount++;
                }
            }
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "private void __Action_start_0_0_0");
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

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
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
    /// Ensures <c>@members</c> is injected into the per-parse execution context and can be called by an inline action.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_MembersAction_InjectsMembersIntoExecutionContext()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;

                private void OnAction(ParserActionExecutionContext context)
                {
                    Count++;
                }

                internal int CountValue => Count;
            }

            start : { OnAction(context); } A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "internal sealed partial class PExecutionContext");
        StringAssert.Contains(source, "private int Count;");

        var assembly = CompileGeneratedSource(source);
        var context = CreateExecutionContext(assembly);
        var result = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadContextIntProperty(context, "CountValue"));
    }

    /// <summary>
    /// Ensures unscoped <c>@header</c> can inject C# using directives consumed by generated parser members and actions.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_HeaderUsing_AllowsMembersAndActionsToReferenceImportedType()
    {
        const string grammar = """
            grammar P;

            @header {
                using System.Text;
            }

            @members {
                private string TextValue = string.Empty;
                internal string Text => TextValue;
            }

            start : { var builder = new StringBuilder(); builder.Append("a"); TextValue = builder.ToString(); } A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "// <auto-generated-parser-header>");
        StringAssert.Contains(source, "using System.Text;");
        StringAssert.Contains(source, "var builder = new StringBuilder();");

        var assembly = CompileGeneratedSource(source);
        var context = CreateExecutionContext(assembly);

        var defaultResult = InvokeParse(assembly, "Parse", "a");
        Assert.IsNotInstanceOfType(defaultResult, typeof(ErrorNode));
        Assert.AreEqual(string.Empty, ReadContextStringProperty(context, "Text"));

        var embeddedResult = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(embeddedResult, typeof(ErrorNode));
        Assert.AreEqual("a", ReadContextStringProperty(context, "Text"));
    }

    /// <summary>
    /// Ensures scoped <c>@parser::header</c> can inject C# using directives consumed by generated parser members and actions.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ParserHeaderUsing_AllowsMembersAndActionsToReferenceImportedType()
    {
        const string grammar = """
            grammar P;

            @parser::header {
                using System.Text;
            }

            @parser::members {
                private string TextValue = string.Empty;
                internal string Text => TextValue;
            }

            start : { var builder = new StringBuilder(); builder.Append("a"); TextValue = builder.ToString(); } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);

        InvokeParse(assembly, "Parse", "a");
        Assert.AreEqual(string.Empty, ReadContextStringProperty(context, "Text"));

        InvokeParseWithContext(assembly, "a", context);
        Assert.AreEqual("a", ReadContextStringProperty(context, "Text"));
    }


    /// <summary>
    /// Ensures <c>@footer</c> can inject a trailing helper type that generated members and inline actions reference explicitly.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_FooterHelperType_AllowsMembersAndActionsToReferenceTrailingType()
    {
        const string grammar = """
            grammar P;

            @members {
                private string TextValue = string.Empty;
                internal string Text => TextValue;
            }

            @footer {
                internal static class ParserFooterHelper
                {
                    internal static string Read() => "footer";
                }
            }

            start : { TextValue = ParserFooterHelper.Read(); } A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "// <auto-generated-parser-footer>");
        StringAssert.Contains(source, "internal static class ParserFooterHelper");
        Assert.IsTrue(
            source.IndexOf("internal static class ParserFooterHelper", StringComparison.Ordinal)
                > source.IndexOf("internal sealed partial class PExecutionContext", StringComparison.Ordinal),
            source);

        var assembly = CompileGeneratedSource(source);
        var context = CreateExecutionContext(assembly);

        var defaultResult = InvokeParse(assembly, "Parse", "a");
        Assert.IsNotInstanceOfType(defaultResult, typeof(ErrorNode));
        Assert.AreEqual(string.Empty, ReadContextStringProperty(context, "Text"));

        var embeddedResult = InvokeParseWithContext(assembly, "a", context);

        Assert.IsNotInstanceOfType(embeddedResult, typeof(ErrorNode));
        Assert.AreEqual("footer", ReadContextStringProperty(context, "Text"));
    }

    /// <summary>
    /// Ensures scoped <c>@parser::footer</c> can inject the same trailing helper type shape as unscoped parser footer blocks.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ParserFooterHelperType_AllowsMembersAndActionsToReferenceTrailingType()
    {
        const string grammar = """
            grammar P;

            @parser::members {
                private string TextValue = string.Empty;
                internal string Text => TextValue;
            }

            @parser::footer {
                internal static class ParserFooterHelper
                {
                    internal static string Read() => "parser-footer";
                }
            }

            start : { TextValue = ParserFooterHelper.Read(); } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);

        InvokeParse(assembly, "Parse", "a");
        Assert.AreEqual(string.Empty, ReadContextStringProperty(context, "Text"));

        InvokeParseWithContext(assembly, "a", context);
        Assert.AreEqual("parser-footer", ReadContextStringProperty(context, "Text"));
    }

    /// <summary>
    /// Ensures explicit generated execution contexts keep instance state isolated across parses.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ExplicitExecutionContexts_IsolateMembersState()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;

                private void OnAction(ParserActionExecutionContext context)
                {
                    Count++;
                }

                internal int CountValue => Count;
            }

            start : { OnAction(context); } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var firstContext = CreateExecutionContext(assembly);
        var secondContext = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", firstContext);
        InvokeParseWithContext(assembly, "a", secondContext);
        InvokeParseWithContext(assembly, "a", firstContext);

        Assert.AreEqual(2, ReadContextIntProperty(firstContext, "CountValue"));
        Assert.AreEqual(1, ReadContextIntProperty(secondContext, "CountValue"));
    }

    /// <summary>
    /// Ensures generated execution contexts expose an internal <c>Fork</c> helper that returns the context type.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_Fork_IsGeneratedWithContextReturnType()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                internal int CountValue => Count;
            }

            start : A { Count++; } ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "internal PExecutionContext Fork()");
        StringAssert.Contains(source, "global::Utils.Parser.Runtime.ParserExecutionContextCopier<PExecutionContext>.Copy(");

        var assembly = CompileGeneratedSource(source);
        var contextType = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var fork = contextType.GetMethod("Fork", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(fork);
        Assert.AreEqual(contextType, fork.ReturnType);
    }

    /// <summary>
    /// Ensures generated <c>Fork</c> copies scalar and mutable collection state from the source context.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_Fork_CopiesCurrentState()
    {
        var assembly = CompileGeneratedSource(EmitCopyGrammar());
        var context = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", context);
        var fork = InvokeFork(context);

        Assert.AreNotSame(context, fork);
        Assert.AreEqual(ReadContextIntProperty(context, "CountValue"), ReadContextIntProperty(fork, "CountValue"));
        CollectionAssert.AreEqual(ReadContextStringItems(context, "ItemValues"), ReadContextStringItems(fork, "ItemValues"));
    }

    /// <summary>
    /// Ensures generated <c>Fork</c> structurally copies mutable collections instead of sharing collection instances.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_Fork_IsolatesMutableCollections()
    {
        var assembly = CompileGeneratedSource(EmitCopyGrammar());
        var context = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", context);
        var fork = InvokeFork(context);

        Assert.AreNotSame(ReadContextObjectProperty(context, "MutableItems"), ReadContextObjectProperty(fork, "MutableItems"));

        InvokeParseWithContext(assembly, "a", fork);

        Assert.AreEqual(1, ReadContextIntProperty(context, "CountValue"));
        Assert.AreEqual(2, ReadContextIntProperty(fork, "CountValue"));
        CollectionAssert.AreEqual(new[] { "a" }, ReadContextStringItems(context, "ItemValues"));
        CollectionAssert.AreEqual(new[] { "a", "a" }, ReadContextStringItems(fork, "ItemValues"));
    }

    /// <summary>
    /// Ensures generated <c>CopyFrom</c> replaces target state with a structural copy of source state.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CopyFrom_ReplacesStateAndIsolatesCollections()
    {
        var assembly = CompileGeneratedSource(EmitCopyGrammar());
        var source = CreateExecutionContext(assembly);
        var target = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", source);
        InvokeParseWithContext(assembly, "a", source);
        InvokeParseWithContext(assembly, "a", target);

        InvokeCopyFrom(target, source);

        Assert.AreEqual(ReadContextIntProperty(source, "CountValue"), ReadContextIntProperty(target, "CountValue"));
        CollectionAssert.AreEqual(ReadContextStringItems(source, "ItemValues"), ReadContextStringItems(target, "ItemValues"));
        Assert.AreNotSame(ReadContextObjectProperty(source, "MutableItems"), ReadContextObjectProperty(target, "MutableItems"));

        InvokeParseWithContext(assembly, "a", target);

        Assert.AreEqual(2, ReadContextIntProperty(source, "CountValue"));
        Assert.AreEqual(3, ReadContextIntProperty(target, "CountValue"));
        CollectionAssert.AreEqual(new[] { "a", "a" }, ReadContextStringItems(source, "ItemValues"));
        CollectionAssert.AreEqual(new[] { "a", "a", "a" }, ReadContextStringItems(target, "ItemValues"));
    }

    /// <summary>
    /// Ensures generated runtime policies install an execution-state manager that can manually capture and restore context state.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CreateRuntimePolicy_InstallsExecutionStateManager()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                public int CountValue => Count;
            }

            start @init { Count++; } : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "internal global::Utils.Parser.Runtime.ParserExecutionStateKey GetExecutionStateKey()");
        StringAssert.Contains(source, "return _executionContext.GetExecutionStateKey();");

        var assembly = CompileGeneratedSource(source);
        var context = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", context);
        var policy = InvokeCreateRuntimePolicy(assembly, context);
        Assert.IsNotNull(policy.ExecutionStateManager);
        var firstKey = policy.ExecutionStateManager.GetCurrentStateKey();

        var snapshot = policy.ExecutionStateManager.Capture();
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(context.GetType(), snapshot.GetType());

        InvokeParseWithContext(assembly, "a", context);
        Assert.AreEqual(2, ReadContextIntProperty(context, "CountValue"));
        Assert.AreNotEqual(firstKey, policy.ExecutionStateManager.GetCurrentStateKey());

        policy.ExecutionStateManager.Restore(snapshot);
        Assert.AreEqual(1, ReadContextIntProperty(context, "CountValue"));
        Assert.AreEqual(firstKey, policy.ExecutionStateManager.GetCurrentStateKey());
    }

    /// <summary>
    /// Ensures a grammar with only inline actions and no lifecycle hooks wires a generated execution-state manager into the runtime policy.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CreateRuntimePolicy_InlineActionWithoutLifecycleHook_InstallsExecutionStateManager()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                public int CountValue => Count;
            }

            start : { Count++; } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);

        Assert.IsNotInstanceOfType<NullParserExecutionStateManager>(policy.ExecutionStateManager);
    }

    /// <summary>
    /// Ensures a grammar with only semantic predicates (no inline actions, no lifecycle hooks) also wires a generated execution-state manager into the runtime policy.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CreateRuntimePolicy_PredicateOnlyGrammar_InstallsExecutionStateManager()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Guard;
                public void Allow() => Guard = 1;
            }

            start : { Guard != 0 }? A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);

        Assert.IsNotInstanceOfType<NullParserExecutionStateManager>(policy.ExecutionStateManager);
    }

    /// <summary>
    /// Ensures generated execution-state managers reject snapshots from another type.
    /// </summary>
    [TestMethod]
    public void GeneratedExecutionStateManager_RestoreRejectsWrongSnapshotType()
    {
        const string grammar = """
            grammar P;
            start @init { } : A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var context = CreateExecutionContext(assembly);
        var policy = InvokeCreateRuntimePolicy(assembly, context);

        Assert.ThrowsException<ArgumentException>(() => policy.ExecutionStateManager.Restore(new object()));
    }

    /// <summary>
    /// Ensures generated <c>CopyFrom</c> validates the supplied source context.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_CopyFrom_RejectsNullSource()
    {
        var assembly = CompileGeneratedSource(EmitCopyGrammar());
        var context = CreateExecutionContext(assembly);
        var copyFrom = context.GetType().GetMethod("CopyFrom", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var exception = Assert.ThrowsException<TargetInvocationException>(() => copyFrom.Invoke(context, [null]));

        Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
    }

    /// <summary>
    /// Ensures generated <c>Fork</c> delegates to <c>ParserExecutionContextCopier&lt;TContext&gt;.Copy</c>, preserving <see cref="ICloneable"/> precedence after parser rollback snapshots have also used the same path.
    /// </summary>
    [TestMethod]
    public void ExecutionContext_Fork_UsesCloneableContextWhenAvailable()
    {
        const string userPartial = """
            using System;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext : ICloneable
            {
                public int CloneCallCount { get; private set; }

                public object Clone()
                {
                    CloneCallCount++;
                    var clone = new PExecutionContext();
                    clone.CopyFrom(this);
                    return clone;
                }
            }
            """;
        var assembly = CompileGeneratedSource(EmitCopyGrammar(), userPartial);
        var context = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", context);
        var cloneCallCountBeforeManualFork = ReadContextIntProperty(context, "CloneCallCount");
        var fork = InvokeFork(context);

        Assert.AreEqual(cloneCallCountBeforeManualFork + 1, ReadContextIntProperty(context, "CloneCallCount"));
        Assert.AreEqual(ReadContextIntProperty(context, "CountValue"), ReadContextIntProperty(fork, "CountValue"));
        CollectionAssert.AreEqual(ReadContextStringItems(context, "ItemValues"), ReadContextStringItems(fork, "ItemValues"));
    }

    /// <summary>
    /// Ensures the default embedded-code parse helper creates a fresh generated execution context for each call.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_DefaultOverload_CreatesFreshExecutionContextEachCall()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                internal static readonly System.Collections.Generic.List<int> ObservedCounts = new();

                private void OnAction(ParserActionExecutionContext context)
                {
                    Count++;
                    ObservedCounts.Add(Count);
                }
            }

            start : { OnAction(context); } A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));

        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");
        InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        CollectionAssert.AreEqual(new[] { 1, 1 }, ReadContextObservedCounts(assembly));
    }

    /// <summary>
    /// Ensures the generated facade does not expose a policy helper that creates and captures a hidden execution context.
    /// </summary>
    [TestMethod]
    public void CreateRuntimePolicy_WithoutExecutionContext_IsNotGenerated()
    {
        const string grammar = """
            grammar P;
            start : { true }? A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        StringAssert.Contains(source, "CreateRuntimePolicy(PExecutionContext executionContext, ParserRuntimeFeaturePolicy? basePolicy = null)");
        Assert.IsFalse(source.Contains("public static ParserRuntimeFeaturePolicy CreateRuntimePolicy(ParserRuntimeFeaturePolicy? basePolicy = null)", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("new PExecutionContext().CreateRuntimePolicy(basePolicy)", StringComparison.Ordinal));

        var assembly = CompileGeneratedSource(source);
        var facadeType = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var contextType = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var policyMethods = facadeType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static method => method.Name == "CreateRuntimePolicy")
            .ToArray();

        Assert.AreEqual(1, policyMethods.Length);
        var parameters = policyMethods[0].GetParameters();
        Assert.AreEqual(2, parameters.Length);
        Assert.AreEqual(contextType, parameters[0].ParameterType);
        Assert.AreEqual(typeof(ParserRuntimeFeaturePolicy), policyMethods[0].ReturnType);
        Assert.AreEqual(typeof(ParserRuntimeFeaturePolicy), parameters[1].ParameterType);
        Assert.IsTrue(parameters[1].HasDefaultValue);
    }

    /// <summary>
    /// Ensures the generated opt-in parse overload preserves a custom rule-call execution policy
    /// and exposes current raw argument and label metadata without changing conservative Parse behavior.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_BasePolicy_ObservesRuleCallMetadata()
    {
        const string grammar = """
            grammar P;
            start : item=child[42] ;
            child : A ;
            A : 'a' ;
            """;
        string source = Emit(grammar);
        StringAssert.Contains(source, "ParseWithEmbeddedCode([global::System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxName, typeof(P))] string input, PExecutionContext executionContext, ParserRuntimeFeaturePolicy basePolicy)");
        var assembly = CompileGeneratedSource(source);
        var executionContext = CreateExecutionContext(assembly);
        var callPolicy = new GeneratedRecordingRuleCallPolicy();
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = callPolicy,
        };

        var result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        var before = callPolicy.Events.Single(item => item.Phase == "before" && item.Context.RuleName == "child").Context;
        var after = callPolicy.Events.Single(item => item.Phase == "after" && item.Context.RuleName == "child").Context;
        Assert.AreEqual("42", before.RawArguments);
        Assert.AreEqual("item", before.LabelName);
        Assert.AreEqual(ParserRuleReferenceLabelKind.Assignment, before.LabelKind);
        Assert.IsTrue(after.Succeeded);
        Assert.IsNotNull(after.CompletedCallResult);
        Assert.AreEqual("42", after.CompletedCallResult.RawArguments);
        Assert.AreEqual("item", after.CompletedCallResult.LabelName);

        callPolicy.Events.Clear();
        Assert.IsNotInstanceOfType(InvokeParse(assembly, "Parse", "a"), typeof(ErrorNode));
        Assert.AreEqual(0, callPolicy.Events.Count, "Conservative Parse() must not use the opt-in custom policy.");
    }

    /// <summary>
    /// Ensures generated embedded-code parsing accepts an explicitly installed typed positional policy through <c>basePolicy</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPositionalPolicy_BindsConvertedValue()
    {
        const string grammar = """
            grammar P;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[42] ;
            child[byte value]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual((byte)42, ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures typed bare parameter reads compile and execute from <c>@init</c>, inline actions, and <c>@after</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedParameterAttributes_ReadInInitInlineAndAfter()
    {
        const string grammar = """
            grammar P;
            @members {
                public int InitSeen { get; private set; }
                public int InlineSeen { get; private set; }
                public int AfterSeen { get; private set; }
            }
            start : child[42] ;
            child[int value]
            @init {
                InitSeen = $value;
            }
            @after {
                AfterSeen = $value;
            }
                : { InlineSeen = $value; } A ;
            A : 'a' ;
            """;
        string source = EmitWithAntlrStyleTransformer(grammar);
        StringAssert.Contains(source, "GetRequiredRuleParameter<int>(context, \"value\")");
        var assembly = CompileGeneratedSource(source);
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "InitSeen"));
        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "InlineSeen"));
        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "AfterSeen"));
    }

    /// <summary>
    /// Ensures typed bare local reads observe values explicitly written through the frame helper.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedLocalAttribute_ReadsValueAfterExplicitSet()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start locals [int total]
            @init {
                SetRuleLocal(context, "total", 42);
            }
            @after {
                Seen = $total;
            }
                : A ;
            A : 'a' ;
            """;
        string source = EmitWithAntlrStyleTransformer(grammar);
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\")");
        var assembly = CompileGeneratedSource(source);
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures default no-op embedded-code generation preserves ANTLR-style local writes unchanged.
    /// </summary>
    [TestMethod]
    public void Emit_WithDefaultTransformer_PreservesLocalWriteSyntax()
    {
        const string grammar = """
            grammar P;
            start locals [int total]
            @after {
                $total = 1;
            }
                : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "$total = 1;");
    }

    /// <summary>
    /// Ensures the optional ANTLR-style transformer rewrites supported typed local write operators.
    /// </summary>
    [TestMethod]
    public void EmitWithAntlrStyleTransformer_TypedLocalWrites_RewritesSupportedOperators()
    {
        const string grammar = """
            grammar P;
            start locals [int total]
            @init {
                int mask = 1;
                $total = 1;
                $total += 1;
                $total -= 1;
                $total *= 2;
                $total /= 2;
                $total %= 2;
                $total &= mask;
                $total |= mask;
                $total ^= mask;
                $total <<= 1;
                $total >>= 1;
                $total++;
                ++$total;
                $total--;
                --$total;
            }
                : A ;
            A : 'a' ;
            """;

        string source = EmitWithAntlrStyleTransformer(grammar);

        StringAssert.Contains(source, "SetRequiredRuleLocal<int>(context, \"total\", 1)");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") + 1");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") - 1");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") * 2");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") / 2");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") % 2");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") & mask");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") | mask");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") ^ mask");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") << 1");
        StringAssert.Contains(source, "GetRequiredRuleLocal<int>(context, \"total\") >> 1");
    }



    /// <summary>
    /// Ensures default no-op embedded-code generation preserves ANTLR-style return writes unchanged.
    /// </summary>
    [TestMethod]
    public void Emit_WithDefaultTransformer_PreservesReturnWriteSyntax()
    {
        const string grammar = """
            grammar P;
            start returns [int value]
            @after {
                $value = 42;
            }
                : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "$value = 42;");
    }

    /// <summary>
    /// Ensures the optional ANTLR-style transformer rewrites current-rule return write operators in @after.
    /// </summary>
    [TestMethod]
    public void EmitWithAntlrStyleTransformer_CurrentRuleReturnWrites_RewritesSupportedOperators()
    {
        const string grammar = """
            grammar P;
            start returns [int value]
            @after {
                int mask = 1;
                $value = 1;
                $value += 1;
                $value -= 1;
                $value *= 2;
                $value /= 2;
                $value %= 2;
                $value &= mask;
                $value |= mask;
                $value ^= mask;
                $value <<= 1;
                $value >>= 1;
                $value++;
                ++$value;
                $value--;
                --$value;
            }
                : A ;
            A : 'a' ;
            """;

        string source = EmitWithAntlrStyleTransformer(grammar);

        StringAssert.Contains(source, "SetRequiredRuleReturn<int>(context, \"value\", 1)");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") + 1");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") - 1");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") * 2");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") / 2");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") % 2");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") & mask");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") | mask");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") ^ mask");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") << 1");
        StringAssert.Contains(source, "GetRequiredRuleReturn<int>(context, \"value\") >> 1");
    }


    /// <summary>Ensures inline current-rule return writes are visible through parent assignment labels.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InlineReturnWrite_IsVisibleToParentAssignmentLabel()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start
                : x=child { Seen = $x.value; }
                ;
            child returns [int value]
                : A { $value = 42; }
                ;
            A : 'a' ;
            """;
        string source = EmitWithAntlrStyleTransformer(grammar);
        StringAssert.Contains(source, "SetRequiredRuleReturn<int>(context, \"value\", 42)");
        var assembly = CompileGeneratedSource(source);
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>Ensures inline and @after return writes share the same parser-managed frame return state.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InlineAndAfterReturnWrites_ShareFrameState()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start
                : x=child { Seen = $x.value; }
                ;
            child returns [int value]
            @after {
                $value += 1;
            }
                : A { $value = 41; }
                ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>Ensures failed alternatives roll back current-rule return writes before a later alternative succeeds.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ReturnWrites_RollBackFailedAlternative()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            root
                : x=start { Seen = $x.value; }
                ;
            start returns [int value]
                : { $value = 1; } B
                | { $value = 2; } A
                ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadContextIntProperty(executionContext, "Seen"));
    }


    /// <summary>Ensures memoized child return snapshots are not restored into the caller return frame.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_MemoizedChildReturnSnapshot_DoesNotOverwriteCallerReturn()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            root
                : x=start { Seen = $x.value; }
                ;
            start returns [int value]
                : { $value = 1; } child B
                | { $value = 2; } child A
                ;
            child returns [int value]
            @after {
                $value = 99;
            }
                : ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>Ensures arbitrary object current-rule returns do not make state-key hashing throw.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_ArbitraryObjectReturnBeforeChild_DoesNotBreakStateHashing()
    {
        const string grammar = """
            grammar P;
            root returns [object value]
                : { $value = new object(); } child
                ;
            child
                : A
                ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>Ensures present-null return values remain distinguishable from missing returns.</summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NullReturnWrite_IsPresentForParentRead()
    {
        const string grammar = """
            grammar P;
            @members {
                public bool IsNull { get; private set; }
            }
            start
                : x=child { IsNull = $x.value == null; }
                ;
            child returns [string? value]
                : A { $value = null; }
                ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.IsTrue((bool)ReadContextObjectProperty(executionContext, "IsNull")!);
    }

    /// <summary>
    /// Ensures return write right-hand sides still use existing ANTLR-style read rewrites.
    /// </summary>
    [TestMethod]
    public void EmitWithAntlrStyleTransformer_CurrentRuleReturnWriteRhs_RewritesParameterReads()
    {
        const string grammar = """
            grammar P;
            start[int count] returns [int value]
            @after {
                $value = $count + 1;
            }
                : A ;
            A : 'a' ;
            """;

        string source = EmitWithAntlrStyleTransformer(grammar);

        StringAssert.Contains(source, "SetRequiredRuleReturn<int>(context, \"value\", GetRequiredRuleParameter<int>(context, \"count\") + 1)");
    }

    /// <summary>
    /// Ensures comparison operators in return write right-hand sides are accepted by the transformer.
    /// </summary>
    [DataTestMethod]
    [DataRow("$count == 1 ? 10 : 20")]
    [DataRow("$count != 0 ? 1 : 0")]
    [DataRow("$count <= 10 ? 1 : 0")]
    [DataRow("$count >= 10 ? 1 : 0")]
    public void EmitWithAntlrStyleTransformer_CurrentRuleReturnWriteRhs_AllowsComparisonOperators(string expression)
    {
        string grammar = $$"""
            grammar P;
            start[int count] returns [int value]
            @after {
                $value = {{expression}};
            }
                : A ;
            A : 'a' ;
            """;

        string source = EmitWithAntlrStyleTransformer(grammar);

        StringAssert.Contains(source, "SetRequiredRuleReturn<int>(context, \"value\", GetRequiredRuleParameter<int>(context, \"count\")");
    }

    /// <summary>
    /// Ensures typed local writes execute through managed parser frame local state.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedLocalWrites_UpdateManagedLocalState()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start locals [int total]
            @init {
                $total = 10;
                $total *= 2;
                $total -= 3;
                $total++;
                --$total;
            }
            @after {
                Seen = $total;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(17, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures inline-only local writes allocate declared local slots even when no lifecycle executor is installed.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedLocalWrite_InlineOnlyAllocatesDeclaredLocal()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start locals [int total]
                : { $total = 1; Seen = $total; } A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures typed local write right-hand sides still use supported ANTLR-style read rewrites.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedLocalWriteRhs_RewritesParameterReads()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start : child[41] ;
            child[int count] locals [int total]
            @init {
                $total = $count + 1;
            }
            @after {
                Seen = $total;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures comparison operators in typed local write right-hand sides are not mistaken for nested assignments.
    /// </summary>
    [DataTestMethod]
    [DataRow("$count == 1 ? 10 : 20", 1, 10)]
    [DataRow("$count != 0 ? 1 : 0", 0, 0)]
    [DataRow("$count <= 10 ? 1 : 0", 10, 1)]
    [DataRow("$count >= 10 ? 1 : 0", 9, 0)]
    public void ParseWithEmbeddedCode_TypedLocalWriteRhs_AllowsComparisonOperators(string expression, int count, int expected)
    {
        string grammar = $$"""
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start : child[{{count}}] ;
            child[int count] locals [int total]
            @init {
                $total = {{expression}};
            }
            @after {
                Seen = $total;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(expected, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures typed string local compound writes use the generated getter/operator/setter form.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedStringLocalCompoundWrite_Concatenates()
    {
        const string grammar = """
            grammar P;
            @members {
                public string? Seen { get; private set; }
            }
            start locals [string text]
            @init {
                $text = "a";
            }
            @after {
                Seen = $text;
            }
                : { $text += "b"; } A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("ab", ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures unsupported ANTLR-style local write contexts fail with transformer diagnostics.
    /// </summary>
    [TestMethod]
    public void EmitWithAntlrStyleTransformer_UnsupportedLocalWriteContexts_ReportDiagnostics()
    {
        AssertTransformerDiagnostic("start[int count] locals [int total] @init { $count = 1; } : A ; A : 'a' ;", "Parser parameter '$count' is read-only.");
        AssertTransformerDiagnostic("start locals [int total] returns [int value] @after { $start.value = 1; } : A ; A : 'a' ;", "Current-rule dotted return writes are not supported");
        AssertTransformerDiagnostic("start locals [int total] : x=child { $x.value = 1; } ; child returns [int value] : A ; A : 'a' ;", "Labeled rule-call return attributes are read-only.");
        AssertTransformerDiagnostic("start locals [int total] : xs+=child { $xs.value = values; } ; child returns [int value] : A ; A : 'a' ;", "List-labeled rule-call return projections are read-only.");
        AssertTransformerDiagnostic("start locals [int total] @init { value = $total++; } : A ; A : 'a' ;", "Increment/decrement parser attributes are supported only as standalone statements.");
        AssertTransformerDiagnostic("start locals [int total] @init { Use(ref $total); } : A ; A : 'a' ;", "ref/out parser attributes are not supported");
        AssertTransformerDiagnostic("start locals [int total] : { $total = 1; }? A ; A : 'a' ;", "Parser local writes are not supported in semantic predicates.");
    }

    /// <summary>
    /// Ensures typed nullable/reference local reads can observe an allocated present-null value.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NullableLocalAttribute_PresentNullSucceeds()
    {
        const string grammar = """
            grammar P;
            @members {
                public bool IsNull { get; private set; }
            }
            start locals [string? label]
            @after {
                IsNull = $label == null;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(true, ReadContextObjectProperty(executionContext, "IsNull"));
    }

    /// <summary>
    /// Ensures reading a present-null local as a non-nullable value type fails deterministically.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_NonNullableLocalAttribute_PresentNullThrows()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start locals [int total]
            @after {
                Seen = $total;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        TargetInvocationException exception = Assert.ThrowsException<TargetInvocationException>(() =>
            InvokeParseWithContext(assembly, "a", executionContext));

        Assert.IsInstanceOfType<ParserAttributeAccessException>(exception.InnerException);
    }

    /// <summary>
    /// Ensures typed bare parameter reads do not convert incompatible runtime seed values.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedParameterAttribute_WrongRuntimeTypeThrows()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start : child["42"] ;
            child[int value]
            @after {
                Seen = $value;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new PositionalLiteralRuleCallExecutionPolicy(),
        };

        TargetInvocationException exception = Assert.ThrowsException<TargetInvocationException>(() =>
            InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy));

        Assert.IsInstanceOfType<ParserAttributeAccessException>(exception.InnerException);
    }

    /// <summary>
    /// Ensures managed rollback restores local writes before a later typed local read.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedLocalAttribute_FailedAlternativeDoesNotLeak()
    {
        const string grammar = """
            grammar P;
            @members {
                public int Seen { get; private set; }
            }
            start locals [int total]
            @after {
                Seen = $total;
            }
                : { SetRuleLocal(context, "total", 1); } B
                | { SetRuleLocal(context, "total", 2); } A
                ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(EmitWithAntlrStyleTransformer(grammar));
        var executionContext = CreateExecutionContext(assembly);

        ParseNode result = InvokeParseWithContext(assembly, "a", executionContext);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(2, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures generated embedded-code parsing accepts an explicitly installed typed named policy through <c>basePolicy</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedNamedPolicy_BindsConvertedValue()
    {
        const string grammar = """
            grammar P;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[value: 42] ;
            child[long value]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedNamedLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(42L, ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures typed IgnoreCall leaves an incompatible value absent while typed Throw reports the conversion failure.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPolicy_IncompatibleValueHonorsFailureBehavior()
    {
        const string grammar = """
            grammar P;
            @members {
                public bool Found { get; private set; }
            }
            start : child["hello"] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? value);
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var ignoredContext = CreateExecutionContext(assembly);
        var ignoredPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode ignoredResult = InvokeParseWithContextAndPolicy(assembly, "a", ignoredContext, ignoredPolicy);

        Assert.IsNotInstanceOfType(ignoredResult, typeof(ErrorNode));
        Assert.AreEqual(false, ReadContextObjectProperty(ignoredContext, "Found"));

        var throwingContext = CreateExecutionContext(assembly);
        var throwingPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw),
        };
        TargetInvocationException invocationException = Assert.ThrowsException<TargetInvocationException>(() =>
            InvokeParseWithContextAndPolicy(assembly, "a", throwingContext, throwingPolicy));
        Assert.IsInstanceOfType<ParserRuleCallBindingException>(invocationException.InnerException);
    }

    /// <summary>
    /// Ensures nullable null is retained as a present seed in generated execution state.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPolicy_NullableNullIsPresent()
    {
        const string grammar = """
            grammar P;
            @members {
                public bool Found { get; private set; }
                public object? Seen { get; private set; } = 1;
            }
            start : child[null] ;
            child[int? value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(true, ReadContextObjectProperty(executionContext, "Found"));
        Assert.IsNull(ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures generated rollback and memoization use converted positional seed values from the current call site.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPositionalPolicy_RollbackUsesSuccessfulConvertedValue()
    {
        const string grammar = """
            grammar P;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[1] B | child[2] ;
            child[byte value]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual((byte)2, ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures memoization keys use the converted effective value rather than the original literal source form.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPolicy_EquivalentConvertedValuesShareMemoizedResult()
    {
        const string grammar = """
            grammar P;
            @members {
                public static int InitCount;
            }
            start : child[1] B | child[1.0] ;
            child[double value]
            @init {
                InitCount++;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "InitCount"));
    }

    /// <summary>
    /// Ensures generated rollback and memoization use converted named seed values from the current call site.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedNamedPolicy_RollbackUsesSuccessfulConvertedValue()
    {
        const string grammar = """
            grammar P;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[value: 1] B | child[value: 2] ;
            child[byte value]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var basePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedNamedLiteralRuleCallExecutionPolicy(),
        };

        ParseNode result = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, basePolicy);

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual((byte)2, ReadContextObjectProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures generated typed positional and named policies bind omitted defaults while explicit arguments override them.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedPolicies_BindOmittedDefaultsAndExplicitOverrides()
    {
        const string positionalGrammar = """
            grammar P;
            @members {
                public object? First { get; private set; }
                public object? Second { get; private set; }
            }
            start : child[7] ;
            child[int first = invalidExpression, byte second = 42]
            @init {
                TryGetRuleParameter(context, "first", out object? first);
                TryGetRuleParameter(context, "second", out object? second);
                First = first;
                Second = second;
            }
                : A ;
            A : 'a' ;
            """;
        var positionalAssembly = CompileGeneratedSource(Emit(positionalGrammar));
        var positionalContext = CreateExecutionContext(positionalAssembly);
        var positionalPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode positionalResult = InvokeParseWithContextAndPolicy(positionalAssembly, "a", positionalContext, positionalPolicy);

        Assert.IsNotInstanceOfType(positionalResult, typeof(ErrorNode));
        Assert.AreEqual(7, ReadContextObjectProperty(positionalContext, "First"));
        Assert.AreEqual((byte)42, ReadContextObjectProperty(positionalContext, "Second"));

        const string namedGrammar = """
            grammar N;
            @members {
                public object? First { get; private set; }
                public object? Second { get; private set; }
            }
            start : child[second: 7] ;
            child[int first = 42, byte second = invalidExpression]
            @init {
                TryGetRuleParameter(context, "first", out object? first);
                TryGetRuleParameter(context, "second", out object? second);
                First = first;
                Second = second;
            }
                : A ;
            A : 'a' ;
            """;
        var namedAssembly = CompileGeneratedSource(Emit(namedGrammar));
        var namedContext = CreateExecutionContext(namedAssembly);
        var namedPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedNamedLiteralRuleCallExecutionPolicy(),
        };

        ParseNode namedResult = InvokeParseWithContextAndPolicy(namedAssembly, "a", namedContext, namedPolicy);

        Assert.IsNotInstanceOfType(namedResult, typeof(ErrorNode));
        Assert.AreEqual(42, ReadContextObjectProperty(namedContext, "First"));
        Assert.AreEqual((byte)7, ReadContextObjectProperty(namedContext, "Second"));
    }

    /// <summary>
    /// Ensures generated default-derived nullable seeds remain present and missing required defaults honor failure behavior.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedDefaultFailuresAndNullableNull_HonorPolicyBehavior()
    {
        const string nullableGrammar = """
            grammar P;
            @members {
                public bool Found { get; private set; }
                public object? Seen { get; private set; } = 1;
            }
            start : child[] ;
            child[int? value = null]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            """;
        var nullableAssembly = CompileGeneratedSource(Emit(nullableGrammar));
        var nullableContext = CreateExecutionContext(nullableAssembly);
        var nullablePolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode nullableResult = InvokeParseWithContextAndPolicy(nullableAssembly, "a", nullableContext, nullablePolicy);

        Assert.IsNotInstanceOfType(nullableResult, typeof(ErrorNode));
        Assert.AreEqual(true, ReadContextObjectProperty(nullableContext, "Found"));
        Assert.IsNull(ReadContextObjectProperty(nullableContext, "Seen"));

        const string requiredGrammar = """
            grammar R;
            @members {
                public bool Found { get; private set; }
            }
            start : child[] ;
            child[int value]
            @init {
                Found = TryGetRuleParameter(context, "value", out object? value);
            }
                : A ;
            A : 'a' ;
            """;
        var requiredAssembly = CompileGeneratedSource(Emit(requiredGrammar));
        var ignoredContext = CreateExecutionContext(requiredAssembly);
        var ignoredPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode ignoredResult = InvokeParseWithContextAndPolicy(requiredAssembly, "a", ignoredContext, ignoredPolicy);

        Assert.IsNotInstanceOfType(ignoredResult, typeof(ErrorNode));
        Assert.AreEqual(false, ReadContextObjectProperty(ignoredContext, "Found"));

        var throwingContext = CreateExecutionContext(requiredAssembly);
        var throwingPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(ParserRuleCallBindingFailureBehavior.Throw),
        };
        TargetInvocationException exception = Assert.ThrowsException<TargetInvocationException>(() =>
            InvokeParseWithContextAndPolicy(requiredAssembly, "a", throwingContext, throwingPolicy));
        Assert.IsInstanceOfType<ParserRuleCallBindingException>(exception.InnerException);
    }

    /// <summary>
    /// Ensures rollback replaces failed explicit seeds with successful positional and named default-derived seeds.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedDefaultSeeds_RollBackWithFailedAlternatives()
    {
        const string positionalGrammar = """
            grammar P;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[1] B | child[] ;
            child[byte value = 2]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var positionalAssembly = CompileGeneratedSource(Emit(positionalGrammar));
        var positionalContext = CreateExecutionContext(positionalAssembly);
        var positionalPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode positionalResult = InvokeParseWithContextAndPolicy(positionalAssembly, "a", positionalContext, positionalPolicy);

        Assert.IsNotInstanceOfType(positionalResult, typeof(ErrorNode));
        Assert.AreEqual((byte)2, ReadContextObjectProperty(positionalContext, "Seen"));

        const string namedGrammar = """
            grammar N;
            @members {
                public object? Seen { get; private set; }
            }
            start : child[value: 1] B | child[] ;
            child[byte value = 2]
            @init {
                TryGetRuleParameter(context, "value", out object? value);
                Seen = value;
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var namedAssembly = CompileGeneratedSource(Emit(namedGrammar));
        var namedContext = CreateExecutionContext(namedAssembly);
        var namedPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedNamedLiteralRuleCallExecutionPolicy(),
        };

        ParseNode namedResult = InvokeParseWithContextAndPolicy(namedAssembly, "a", namedContext, namedPolicy);

        Assert.IsNotInstanceOfType(namedResult, typeof(ErrorNode));
        Assert.AreEqual((byte)2, ReadContextObjectProperty(namedContext, "Seen"));
    }

    /// <summary>
    /// Ensures memoization keys use final converted default state and conservative parsing leaves defaults metadata-only.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_TypedDefaults_MemoizeByEffectiveStateOnly()
    {
        const string grammar = """
            grammar P;
            @members {
                public static int InitCount;
                public bool Found { get; private set; }
            }
            start : child[] B | child[42] ;
            child[int value = 42]
            @init {
                InitCount++;
                Found = TryGetRuleParameter(context, "value", out object? value);
            }
                : A ;
            A : 'a' ;
            B : 'b' ;
            """;
        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);
        var typedPolicy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleCallExecutionPolicy = new TypedPositionalLiteralRuleCallExecutionPolicy(),
        };

        ParseNode typedResult = InvokeParseWithContextAndPolicy(assembly, "a", executionContext, typedPolicy);

        Assert.IsNotInstanceOfType(typedResult, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "InitCount"));
        Assert.AreEqual(true, ReadContextObjectProperty(executionContext, "Found"));

        var conservativeContext = CreateExecutionContext(assembly);
        ParseNode conservativeResult = InvokeParseWithContextAndPolicy(
            assembly,
            "a",
            conservativeContext,
            ParserRuntimeFeaturePolicy.Default);

        Assert.IsNotInstanceOfType(conservativeResult, typeof(ErrorNode));
        Assert.AreEqual(false, ReadContextObjectProperty(conservativeContext, "Found"));
        Assert.IsNotInstanceOfType(InvokeParse(assembly, "Parse", "a"), typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures predicates can call instance members injected through <c>@members</c>.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_Predicate_CallsInjectedInstanceMember()
    {
        const string grammar = """
            grammar P;

            @members {
                private bool Allow() => true;
            }

            start : { Allow() }? A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
    }

    /// <summary>
    /// Ensures predicate instance state belongs to the supplied generated execution context.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_PredicateState_UsesSuppliedExecutionContext()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                private bool Allow()
                {
                    Count++;
                    return true;
                }

                internal int CountValue => Count;
            }

            start : { Allow() }? A ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var firstContext = CreateExecutionContext(assembly);
        var secondContext = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", firstContext);
        InvokeParseWithContext(assembly, "a", secondContext);

        Assert.IsTrue(ReadContextIntProperty(firstContext, "CountValue") > 0);
        Assert.IsTrue(ReadContextIntProperty(secondContext, "CountValue") > 0);
    }


    /// <summary>
    /// Ensures <c>@parser::members</c> is emitted inside the generated parser execution context.
    /// </summary>
    [TestMethod]
    public void ParserNamedActionMembers_SourceShape_EmitsInsideExecutionContext()
    {
        const string grammar = """
            grammar P;

            @parser::members {
                public int Seen { get; private set; }

                private void Mark(int value)
                {
                    Seen = value;
                }
            }

            start : A { Mark(42); } ;
            A : 'a' ;
            """;

        string source = Emit(grammar);
        int contextStart = source.IndexOf("internal sealed partial class PExecutionContext", StringComparison.Ordinal);
        int membersStart = source.IndexOf("public int Seen { get; private set; }", StringComparison.Ordinal);
        int actionHookStart = source.IndexOf("private void __Action_start_0_1_0", StringComparison.Ordinal);

        Assert.IsTrue(contextStart >= 0, source);
        Assert.IsTrue(membersStart > contextStart, source);
        Assert.IsTrue(actionHookStart > membersStart, source);
        StringAssert.Contains(source, "private void Mark(int value)");
    }

    /// <summary>
    /// Ensures parser members are callable from generated inline parser actions.
    /// </summary>
    [TestMethod]
    public void ParserNamedActionMembers_InlineAction_CanCallMember()
    {
        const string grammar = """
            grammar P;

            @parser::members {
                public int Seen { get; private set; }

                private void Mark(int value)
                {
                    Seen = value;
                }
            }

            start : A { Mark(42); } ;
            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", executionContext);

        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures parser members are callable from generated <c>@after</c> lifecycle hooks.
    /// </summary>
    [TestMethod]
    public void ParserNamedActionMembers_AfterAction_CanCallMember()
    {
        const string grammar = """
            grammar P;

            @parser::members {
                public int Seen { get; private set; }
                private void Mark(int value) => Seen = value;
            }

            start
            @after { Mark(42); }
                : A ;

            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", executionContext);

        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures parser members are callable from generated <c>@init</c> lifecycle hooks.
    /// </summary>
    [TestMethod]
    public void ParserNamedActionMembers_InitAction_CanCallMember()
    {
        const string grammar = """
            grammar P;

            @parser::members {
                public int Seen { get; private set; }
                private void Mark(int value) => Seen = value;
            }

            start
            @init { Mark(42); }
                : A ;

            A : 'a' ;
            """;

        var assembly = CompileGeneratedSource(Emit(grammar));
        var executionContext = CreateExecutionContext(assembly);

        InvokeParseWithContext(assembly, "a", executionContext);

        Assert.AreEqual(42, ReadContextIntProperty(executionContext, "Seen"));
    }

    /// <summary>
    /// Ensures multiple parser named-action blocks are emitted in deterministic source order and compile.
    /// </summary>
    [TestMethod]
    public void ParserNamedActions_MultipleBlocks_EmitInSourceOrderAndCompile()
    {
        const string grammar = """
            grammar P;

            @header { using System; }
            @parser::header { using System.Linq; }

            @parser::members { public int AValue { get; private set; } }
            @parser::members { public int BValue { get; private set; } }

            @parser::footer { // parser footer marker }

            start : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        Assert.IsTrue(source.IndexOf("using System;", StringComparison.Ordinal) < source.IndexOf("using System.Linq;", StringComparison.Ordinal), source);
        Assert.IsTrue(source.IndexOf("public int AValue", StringComparison.Ordinal) < source.IndexOf("public int BValue", StringComparison.Ordinal), source);
        StringAssert.Contains(source, "// parser footer marker");
        _ = CompileGeneratedSource(source);
    }

    /// <summary>
    /// Ensures no-op parser named-action emission preserves raw <c>$...</c> text in member content.
    /// </summary>
    [TestMethod]
    public void ParserNamedActionMembers_NoOpPreservesRawDollarText()
    {
        const string grammar = """
            grammar P;

            @parser::members {
                public string Raw => "$value should stay raw here";
            }

            start : A ;
            A : 'a' ;
            """;

        string source = Emit(grammar);

        StringAssert.Contains(source, "public string Raw => \"$value should stay raw here\";");
        _ = CompileGeneratedSource(source);
    }

    /// <summary>
    /// Ensures invalid C# injected through <c>@members</c> remains a Roslyn compilation error.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_InvalidMembersCode_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;

            @members {
                not valid csharp
            }

            start : A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// Ensures member-name collisions in injected <c>@members</c> remain Roslyn compilation errors.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_MembersHookNameCollision_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;

            @members {
                private void __Action_start_0_0_0(ParserActionExecutionContext context) { }
            }

            start : { } A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// Ensures multi-statement inline parser action hooks execute each generated C# statement.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InlineActionMultiStatement_ExecutesAllStatements()
    {
        const string grammar = """
            grammar P;
            start : {
                OnBefore(context);
                OnAfter(context);
            } A ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static int BeforeCount;
                public static int AfterCount;

                private void OnBefore(ParserActionExecutionContext context)
                {
                    BeforeCount++;
                }

                private void OnAfter(ParserActionExecutionContext context)
                {
                    AfterCount++;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var defaultResult = InvokeParse(assembly, "Parse", "a");

        Assert.IsNotInstanceOfType(defaultResult, typeof(ErrorNode));
        Assert.AreEqual(0, ReadIntField(assembly, "BeforeCount"));
        Assert.AreEqual(0, ReadIntField(assembly, "AfterCount"));

        var embeddedResult = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(embeddedResult, typeof(ErrorNode));
        Assert.AreEqual(1, ReadIntField(assembly, "BeforeCount"));
        Assert.AreEqual(1, ReadIntField(assembly, "AfterCount"));
    }

    /// <summary>
    /// Ensures multi-line inline parser action hooks can declare local variables and pass them to user code.
    /// </summary>
    [TestMethod]
    public void ParseWithEmbeddedCode_InlineActionWithLocalVariable_ExecutesWithGeneratedLocals()
    {
        const string grammar = """
            grammar P;
            start : {
                var name = ruleName;
                OnAction(context, name);
            } A ;
            A : 'a' ;
            """;
        const string userPartial = """
            using Utils.Parser.Runtime;

            namespace Generated.Tests;

            internal sealed partial class PExecutionContext
            {
                public static string? ActionName;

                private void OnAction(ParserActionExecutionContext context, string name)
                {
                    ActionName = name;
                }
            }
            """;

        var assembly = CompileGeneratedSource(Emit(grammar), userPartial);
        var result = InvokeParse(assembly, "ParseWithEmbeddedCode", "a");

        Assert.IsNotInstanceOfType(result, typeof(ErrorNode));
        Assert.AreEqual("start", ReadStringField(assembly, "ActionName"));
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

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
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

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
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

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
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

            internal sealed partial class PExecutionContext
            {
                public static int PredicateCount;

                private bool OnPredicate(SemanticPredicateEvaluationContext context)
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

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
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

            internal sealed partial class PExecutionContext
            {
                public static int PredicateCount;

                private bool OnPredicate(SemanticPredicateEvaluationContext context)
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

            internal sealed partial class PExecutionContext
            {
                public static int ActionCount;

                private void OnAction(ParserActionExecutionContext context)
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

            internal sealed partial class PExecutionContext
            {
                public static int PredicateCount;

                private bool OnPredicate(SemanticPredicateEvaluationContext context)
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
        StringAssert.Contains(source, "new CompiledGrammar(Build(), executionContext.CreateRuntimePolicy())");
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
    /// Ensures predicate statement blocks without a return remain Roslyn compilation errors.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_PredicateBlockWithoutReturn_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;
            start : {
                var isStart = inputPosition == 0;
            }? A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// Ensures predicate blocks that return a non-boolean value remain Roslyn compilation errors.
    /// </summary>
    [TestMethod]
    public void CompileGeneratedSource_PredicateReturnWrongType_ReportsRoslynError()
    {
        const string grammar = """
            grammar P;
            start : { return "not bool"; }? A ;
            A : 'a' ;
            """;

        var result = CompileGeneratedSourceExpectingFailure(Emit(grammar));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.IsTrue(result.Diagnostics.Any(static diagnostic => diagnostic.ToString().Contains("string", StringComparison.OrdinalIgnoreCase)));
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
    /// Asserts that the optional ANTLR-style transformer rejects a parser rule fragment.
    /// </summary>
    private static void AssertTransformerDiagnostic(string ruleFragment, string expectedMessage)
    {
        string grammar = "grammar P; " + ruleFragment;

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EmitWithAntlrStyleTransformer(grammar));

        StringAssert.Contains(exception.Message, expectedMessage);
    }

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
    /// Emits generated C# with the optional C# ANTLR-style transformer enabled for compatibility tests.
    /// </summary>
    /// <param name="grammarText">ANTLR4 grammar source.</param>
    /// <returns>Generated C# source with optional ANTLR-style convenience rewrites applied.</returns>
    private static string EmitWithAntlrStyleTransformer(string grammarText)
    {
        var grammar = new G4Parser(new G4Tokenizer(grammarText).Tokenize()).Parse();
        return GrammarEmitter.Emit(grammar, "Generated.Tests", "P", "P.g4", new CSharpAntlrStyleParserEmbeddedCodeTransformer(grammar));
    }

    /// <summary>
    /// Emits the shared grammar used to verify generated execution-context copy helpers.
    /// </summary>
    /// <returns>Generated C# source for a grammar with scalar and mutable collection context state.</returns>
    private static string EmitCopyGrammar()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Count;
                public int CountValue => Count;

                private List<string> Items = new();
                public IReadOnlyList<string> ItemValues => Items;
                public List<string> MutableItems => Items;
            }

            start : A { Count++; Items.Add("a"); } ;
            A : 'a' ;
            """;

        return Emit(grammar);
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
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == methodName
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(string));
        return (ParseNode)method.Invoke(null, [input])!;
    }

    /// <summary>
    /// Reads the test action counter from the user partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <returns>Current action count.</returns>
    private static int ReadActionCount(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
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
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField("PredicateCount", BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Reads a named integer field from the generated test partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="fieldName">Public static integer field name.</param>
    /// <returns>Current integer field value.</returns>
    private static int ReadIntField(Assembly assembly, string fieldName)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Reads a named string field from the generated test partial class.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="fieldName">Public static string field name.</param>
    /// <returns>Current string field value.</returns>
    private static string? ReadStringField(Assembly assembly, string fieldName)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)!;
        return (string?)field.GetValue(null);
    }


    /// <summary>
    /// Reads a named integer field from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Generated execution context instance.</param>
    /// <param name="fieldName">Public integer field name.</param>
    /// <returns>Current integer field value.</returns>
    private static int ReadInstanceIntField(object executionContext, string fieldName)
    {
        var field = executionContext.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance)!;
        return (int)field.GetValue(executionContext)!;
    }


    /// <summary>
    /// Reads a named string field from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Generated execution context instance.</param>
    /// <param name="fieldName">Public string field name.</param>
    /// <returns>Current string field value.</returns>
    private static string? ReadInstanceStringField(object executionContext, string fieldName)
    {
        var field = executionContext.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance)!;
        return (string?)field.GetValue(executionContext);
    }

    /// <summary>
    /// Creates a generated execution context instance by reflection.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated execution context.</param>
    /// <returns>A new generated execution context instance.</returns>
    private static object CreateExecutionContext(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }


    /// <summary>
    /// Invokes the generated facade helper that creates a runtime policy for an explicit execution context.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="executionContext">Execution context instance to bind to the policy.</param>
    /// <returns>The generated runtime policy bound to the supplied execution context.</returns>
    private static ParserRuntimeFeaturePolicy InvokeCreateRuntimePolicy(Assembly assembly, object executionContext)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var contextType = executionContext.GetType();
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "CreateRuntimePolicy"
                && method.GetParameters() is [{ ParameterType: var executionContextType }, { ParameterType: var basePolicyType }]
                && executionContextType == contextType
                && basePolicyType == typeof(ParserRuntimeFeaturePolicy));
        return (ParserRuntimeFeaturePolicy)method.Invoke(null, [executionContext, null])!;
    }

    /// <summary>
    /// Invokes the generated embedded-code parse overload that accepts an explicit execution context.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="input">Input text to parse.</param>
    /// <param name="executionContext">Execution context instance to pass to the generated overload.</param>
    /// <returns>Parse-tree root returned by the generated helper.</returns>
    private static ParseNode InvokeParseWithContext(Assembly assembly, string input, object executionContext)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var contextType = executionContext.GetType();
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ParseWithEmbeddedCode"
                && method.GetParameters() is [{ ParameterType: var inputType }, { ParameterType: var executionContextType }]
                && inputType == typeof(string)
                && executionContextType == contextType);
        return (ParseNode)method.Invoke(null, [input, executionContext])!;
    }

    /// <summary>
    /// Invokes the generated embedded-code parse overload that accepts an execution context and base policy.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated grammar class.</param>
    /// <param name="input">Input text to parse.</param>
    /// <param name="executionContext">Execution context instance to pass to the generated overload.</param>
    /// <param name="basePolicy">Base runtime policy whose custom rule-call policy should be preserved.</param>
    /// <returns>Parse-tree root returned by the generated helper.</returns>
    private static ParseNode InvokeParseWithContextAndPolicy(
        Assembly assembly,
        string input,
        object executionContext,
        ParserRuntimeFeaturePolicy basePolicy)
    {
        var type = assembly.GetType("Generated.Tests.P", throwOnError: true)!;
        var contextType = executionContext.GetType();
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ParseWithEmbeddedCode"
                && method.GetParameters() is
                [
                    { ParameterType: var inputType },
                    { ParameterType: var executionContextType },
                    { ParameterType: var policyType },
                ]
                && inputType == typeof(string)
                && executionContextType == contextType
                && policyType == typeof(ParserRuntimeFeaturePolicy));
        return (ParseNode)method.Invoke(null, [input, executionContext, basePolicy])!;
    }


    /// <summary>
    /// Reads a named boolean field from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to inspect.</param>
    /// <param name="fieldName">Field name.</param>
    /// <returns>The boolean field value.</returns>
    private static bool ReadInstanceBoolField(object executionContext, string fieldName)
    {
        var field = executionContext.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        return (bool)field.GetValue(executionContext)!;
    }

    /// <summary>
    /// Writes a named boolean field on a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to update.</param>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">Value to assign.</param>
    private static void WriteInstanceBoolField(object executionContext, string fieldName, bool value)
    {
        var field = executionContext.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        field.SetValue(executionContext, value);
    }

    /// <summary>
    /// Invokes the generated internal <c>Fork</c> helper on an execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to fork.</param>
    /// <returns>The copied execution context returned by <c>Fork</c>.</returns>
    private static object InvokeFork(object executionContext)
    {
        var method = executionContext.GetType().GetMethod("Fork", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return method.Invoke(executionContext, [])!;
    }

    /// <summary>
    /// Invokes the generated internal <c>CopyFrom</c> helper on an execution context instance.
    /// </summary>
    /// <param name="target">Execution context instance that receives copied state.</param>
    /// <param name="source">Execution context instance that provides copied state.</param>
    private static void InvokeCopyFrom(object target, object source)
    {
        var method = target.GetType().GetMethod("CopyFrom", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(target, [source]);
    }

    /// <summary>
    /// Reads a named integer property from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to inspect.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>The integer property value.</returns>
    private static int ReadContextIntProperty(object executionContext, string propertyName)
    {
        var property = executionContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        return (int)property.GetValue(executionContext)!;
    }

    /// <summary>
    /// Reads a named object property from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to inspect.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>The property value.</returns>
    private static object ReadContextObjectProperty(object executionContext, string propertyName)
    {
        var property = executionContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        return property.GetValue(executionContext)!;
    }

    /// <summary>
    /// Reads a named string property from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to inspect.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>The string property value.</returns>
    private static string? ReadContextStringProperty(object executionContext, string propertyName)
    {
        var property = executionContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        return (string?)property.GetValue(executionContext);
    }

    /// <summary>
    /// Reads a named string collection property from a generated execution context instance.
    /// </summary>
    /// <param name="executionContext">Execution context instance to inspect.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>The string collection values as an array.</returns>
    private static string[] ReadContextStringItems(object executionContext, string propertyName)
    {
        var property = executionContext.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        var values = (System.Collections.Generic.IEnumerable<string>)property.GetValue(executionContext)!;
        return values.ToArray();
    }

    /// <summary>
    /// Reads static observed action counts from the generated execution context type.
    /// </summary>
    /// <param name="assembly">Assembly containing the generated execution context.</param>
    /// <returns>Observed action counts.</returns>
    private static int[] ReadContextObservedCounts(Assembly assembly)
    {
        var type = assembly.GetType("Generated.Tests.PExecutionContext", throwOnError: true)!;
        var field = type.GetField("ObservedCounts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        var counts = (System.Collections.Generic.IEnumerable<int>)field.GetValue(null)!;
        return counts.ToArray();
    }

    /// <summary>
    /// Records rule-call callbacks observed through a generated opt-in runtime policy.
    /// </summary>
    private sealed class GeneratedRecordingRuleCallPolicy : IParserRuleCallExecutionPolicy
    {
        /// <summary>
        /// Gets callback events in invocation order.
        /// </summary>
        public List<(string Phase, ParserRuleCallExecutionContext Context)> Events { get; } = [];

        /// <summary>
        /// Records a before-call callback.
        /// </summary>
        /// <param name="context">Current rule-call context.</param>
        public void BeforeRuleCall(ParserRuleCallExecutionContext context)
        {
            Events.Add(("before", context));
        }

        /// <summary>
        /// Records an after-call callback.
        /// </summary>
        /// <param name="context">Completed rule-call context.</param>
        public void AfterRuleCall(ParserRuleCallExecutionContext context)
        {
            Events.Add(("after", context));
        }
    }

    /// <summary>
    /// Captures Roslyn compilation output for generated embedded-code tests.
    /// </summary>
    /// <param name="Success">Whether compilation succeeded.</param>
    /// <param name="AssemblyStream">Emitted assembly stream.</param>
    /// <param name="Diagnostics">Roslyn diagnostics reported during compilation.</param>
    private sealed record CompilationResult(bool Success, MemoryStream AssemblyStream, IReadOnlyList<Diagnostic> Diagnostics);
}

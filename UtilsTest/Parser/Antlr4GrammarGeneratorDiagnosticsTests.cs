using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Immutable;
using System.Text;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators;
using Utils.Parser.Generators.Internal;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies source-generator diagnostics for visible embedded-code constructs that are intentionally not executed.
/// </summary>
[TestClass]
public class Antlr4GrammarGeneratorDiagnosticsTests
{
    /// <summary>
    /// Ensures grammar-level named-action classification remains centralized and deterministic.
    /// </summary>
    [DataTestMethod]
    [DataRow("grammar P; @header { } start : A ; A : 'a' ;", "ParserHeader")]
    [DataRow("parser grammar P; @parser::header { } start : A ;", "ParserHeader")]
    [DataRow("grammar P; @members { } start : A ; A : 'a' ;", "ParserMembers")]
    [DataRow("parser grammar P; @parser::members { } start : A ;", "ParserMembers")]
    [DataRow("grammar P; @footer { } start : A ; A : 'a' ;", "ParserFooter")]
    [DataRow("parser grammar P; @parser::footer { } start : A ;", "ParserFooter")]
    [DataRow("grammar P; @lexer::header { } start : A ; A : 'a' ;", "LexerHeader")]
    [DataRow("lexer grammar L; @lexer::header { } A : 'a' ;", "LexerHeader")]
    [DataRow("grammar P; @lexer::members { } start : A ; A : 'a' ;", "LexerMembers")]
    [DataRow("lexer grammar L; @lexer::members { } A : 'a' ;", "LexerMembers")]
    [DataRow("grammar P; @lexer::footer { } start : A ; A : 'a' ;", "LexerFooter")]
    [DataRow("lexer grammar L; @lexer::footer { } A : 'a' ;", "LexerFooter")]
    [DataRow("grammar P; @lexer::custom { } start : A ; A : 'a' ;", "UnsupportedUnknownScope")]
    [DataRow("lexer grammar L; @parser::header { } A : 'a' ;", "UnsupportedParserNamedActionInLexerGrammar")]
    [DataRow("lexer grammar L; @members { } A : 'a' ;", "UnsupportedUnscopedParserCompatibilityActionInLexerGrammar")]
    [DataRow("grammar P; @tree::members { } start : A ; A : 'a' ;", "UnsupportedUnknownScope")]
    [DataRow("grammar P; @parser::custom { } start : A ; A : 'a' ;", "UnsupportedUnknownParserNamedAction")]
    [DataRow("grammar P; @custom { } start : A ; A : 'a' ;", "UnsupportedMetadataOnly")]
    public void GrammarActionSupport_Classify_ReturnsExpectedKind(string grammarText, string expectedKindName)
    {
        var grammar = new G4Parser(new G4Tokenizer(grammarText).Tokenize()).Parse();
        var action = grammar.Actions.Single();

        Assert.AreEqual(expectedKindName, EmbeddedMembersSupport.Classify(grammar, action).ToString());
    }

    /// <summary>
    /// Ensures lexer actions are reported as unsupported generator execution constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerAction_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : A ;
            A : 'a' { OnLex(context); } ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>
    /// Ensures supported lexer predicates no longer report unsupported generator execution diagnostics.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerPredicate_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : A ;
            A : { IsA(context) }? 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>
    /// Ensures unsupported grammar-level actions are reported as metadata-only generator constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_GrammarAction_ReportsUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;

            @unsupported {
                // metadata only
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostic = AssertSingleUnsupportedDiagnostic(RunGenerator(grammar), "Grammar @unsupported action");

        StringAssert.Contains(diagnostic.GetMessage(), "preserved as metadata only");
    }

    /// <summary>
    /// Ensures unscoped <c>@footer</c> reports the compatibility warning for trailing generated source injection.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_FooterAction_ReportsTrailingSourceInjection()
    {
        const string grammar = """
            grammar P;

            @footer {
                internal sealed class FooterHelper { }
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleFooterInjectedDiagnostic(diagnostics, "Grammar @footer action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), "trailing generated C# source");
        StringAssert.Contains(diagnostic.GetMessage(), "near the end");
        AssertNoUnsupportedDiagnostics(diagnostics);
        StringAssert.Contains(generatedSource, "// <auto-generated-parser-footer>");
        StringAssert.Contains(generatedSource, "internal sealed class FooterHelper { }");
        StringAssert.Contains(generatedSource, "// </auto-generated-parser-footer>");
        Assert.IsTrue(
            generatedSource.IndexOf("internal sealed class FooterHelper", StringComparison.Ordinal)
                > generatedSource.IndexOf("internal sealed partial class PExecutionContext", StringComparison.Ordinal),
            generatedSource);
    }

    /// <summary>
    /// Ensures scoped <c>@parser::footer</c> reports the compatibility warning for trailing generated source injection.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_ParserFooterAction_ReportsTrailingSourceInjection()
    {
        const string grammar = """
            grammar P;

            @parser::footer {
                internal sealed class FooterHelper { }
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleFooterInjectedDiagnostic(diagnostics, "Grammar @parser::footer action");

        StringAssert.Contains(diagnostic.GetMessage(), "compatibility bridge only");
        AssertNoUnsupportedDiagnostics(diagnostics);
    }

    /// <summary>
    /// Ensures <c>@lexer::footer</c> is injected as trailing generated source with lexer markers.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerFooterAction_ReportsTrailingSourceInjection()
    {
        const string grammar = """
            grammar P;

            @lexer::footer {
                internal sealed class LexerFooterHelper { }
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleFooterInjectedDiagnostic(diagnostics, "Grammar @lexer::footer action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), "trailing generated C# source");
        AssertNoUnsupportedDiagnostics(diagnostics);
        StringAssert.Contains(generatedSource, "// <auto-generated-lexer-footer>");
        StringAssert.Contains(generatedSource, "internal sealed class LexerFooterHelper { }");
        StringAssert.Contains(generatedSource, "// </auto-generated-lexer-footer>");
    }

    /// <summary>
    /// Ensures unscoped <c>@footer</c> in a lexer-only grammar remains unsupported and is not injected as a parser footer.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerGrammarFooterAction_ReportsUnsupportedEmbeddedCode()
    {
        const string grammar = """
            lexer grammar L;

            @footer {
                internal sealed class LexerFooterHelper { }
            }

            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar, "L.g4");
        var diagnostic = AssertSingleUnsupportedDiagnostic(diagnostics, "Grammar @footer action");
        string generatedSource = GetGeneratedSource(grammar, "L.g4");

        StringAssert.Contains(diagnostic.GetMessage(), "Unscoped grammar action '@footer' is not supported in lexer grammars by this generator.");
        AssertNoFooterInjectedDiagnostics(diagnostics);
        Assert.IsFalse(generatedSource.Contains("LexerFooterHelper", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures unscoped <c>@header</c> reports the compatibility warning for generated source injection.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_HeaderAction_ReportsSourceInjection()
    {
        const string grammar = """
            grammar P;

            @header {
                using System.Text;
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleHeaderInjectedDiagnostic(diagnostics, "Grammar @header action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), "generated C# source");
        AssertNoUnsupportedDiagnostics(diagnostics);
        StringAssert.Contains(generatedSource, "// <auto-generated-parser-header>");
        StringAssert.Contains(generatedSource, "using System.Text;");
        StringAssert.Contains(generatedSource, "// </auto-generated-parser-header>");
    }

    /// <summary>
    /// Ensures <c>@parser::header</c> reports the compatibility warning for generated source injection.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_ParserHeaderAction_ReportsSourceInjection()
    {
        const string grammar = """
            grammar P;

            @parser::header {
                using System.Text;
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleHeaderInjectedDiagnostic(diagnostics, "Grammar @parser::header action");

        StringAssert.Contains(diagnostic.GetMessage(), "compatibility bridge only");
        AssertNoUnsupportedDiagnostics(diagnostics);
    }

    /// <summary>
    /// Ensures unscoped <c>@members</c> reports the compatibility warning for execution-context injection.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_MembersAction_ReportsExecutionContextInjection()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Value;
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostic = AssertSingleMembersInjectedDiagnostic(RunGenerator(grammar), "Grammar @members action");

        StringAssert.Contains(diagnostic.GetMessage(), "per-parse execution context");
        Assert.IsFalse(diagnostic.GetMessage().Contains("not injected", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ensures <c>@parser::members</c> reports the compatibility warning for execution-context injection.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_ParserMembersAction_ReportsExecutionContextInjection()
    {
        const string grammar = """
            grammar P;

            @parser::members {
                private int Value;
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostic = AssertSingleMembersInjectedDiagnostic(RunGenerator(grammar), "Grammar @parser::members action");

        StringAssert.Contains(diagnostic.GetMessage(), "compatibility bridge");
    }

    /// <summary>
    /// Ensures unscoped <c>@members</c> in a parser-only grammar is injected into the parser execution context.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_ParserGrammarMembersAction_ReportsExecutionContextInjection()
    {
        const string grammar = """
            parser grammar P;

            @members {
                private int ParserState;
            }

            start : A ;
            """;

        var diagnostic = AssertSingleMembersInjectedDiagnostic(RunGenerator(grammar), "Grammar @members action");
        string generatedSource = GetGeneratedSource(grammar, "P.g4");

        StringAssert.Contains(diagnostic.GetMessage(), "per-parse execution context");
        StringAssert.Contains(generatedSource, "internal sealed partial class PExecutionContext");
        StringAssert.Contains(generatedSource, "private int ParserState;");
    }

    /// <summary>
    /// Ensures unscoped <c>@members</c> in a lexer-only grammar remains an unsupported lexer construct.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerGrammarMembersAction_ReportsUnsupportedEmbeddedCode()
    {
        const string grammar = """
            lexer grammar L;

            @members {
                private int LexerState;
            }

            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar, "L.g4");
        var diagnostic = AssertSingleUnsupportedDiagnostic(diagnostics, "Grammar @members action");
        string generatedSource = GetGeneratedSource(grammar, "L.g4");

        StringAssert.Contains(diagnostic.GetMessage(), "Unscoped grammar action '@members' is not supported in lexer grammars by this generator.");
        AssertNoMembersInjectedDiagnostics(diagnostics);
        StringAssert.Contains(generatedSource, "internal sealed partial class LExecutionContext");
        Assert.IsFalse(generatedSource.Contains("private int LexerState;", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures <c>@lexer::members</c> is injected into the generated execution context with lexer markers.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerMembersAction_ReportsExecutionContextInjection()
    {
        const string grammar = """
            grammar P;

            @lexer::members {
                private int LexerState;
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleMembersInjectedDiagnostic(diagnostics, "Grammar @lexer::members action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), "per-parse execution context");
        AssertNoUnsupportedDiagnostics(diagnostics);
        StringAssert.Contains(generatedSource, "internal sealed partial class PExecutionContext");
        StringAssert.Contains(generatedSource, "// <auto-generated-lexer-members>");
        StringAssert.Contains(generatedSource, "private int LexerState;");
        StringAssert.Contains(generatedSource, "// </auto-generated-lexer-members>");
    }

    /// <summary>
    /// Ensures <c>@lexer::header</c> is injected near the top of generated source with lexer markers.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerHeaderAction_ReportsSourceInjection()
    {
        const string grammar = """
            grammar P;

            @lexer::header {
                using System.Text;
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleHeaderInjectedDiagnostic(diagnostics, "Grammar @lexer::header action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), "generated C# source");
        AssertNoUnsupportedDiagnostics(diagnostics);
        StringAssert.Contains(generatedSource, "// <auto-generated-lexer-header>");
        StringAssert.Contains(generatedSource, "using System.Text;");
        StringAssert.Contains(generatedSource, "// </auto-generated-lexer-header>");
    }

    /// <summary>
    /// Ensures unscoped <c>@header</c> in a lexer-only grammar remains unsupported and is not injected as a parser header.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerGrammarHeaderAction_ReportsUnsupportedEmbeddedCode()
    {
        const string grammar = """
            lexer grammar L;

            @header {
                using System.Text;
            }

            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar, "L.g4");
        var diagnostic = AssertSingleUnsupportedDiagnostic(diagnostics, "Grammar @header action");
        string generatedSource = GetGeneratedSource(grammar, "L.g4");

        StringAssert.Contains(diagnostic.GetMessage(), "Unscoped grammar action '@header' is not supported in lexer grammars by this generator.");
        AssertNoHeaderInjectedDiagnostics(diagnostics);
        Assert.IsFalse(generatedSource.Contains("using System.Text;", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures scoped lexer grammar-level actions in lexer-only grammars inject source fragments and diagnostics deterministically.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerGrammarLexerNamedActions_ReportInjectionDiagnosticsAndEmitInOrder()
    {
        const string grammar = """
            lexer grammar L;

            @lexer::header {
                // lexer grammar header
            }

            @lexer::members {
                private int LexerOnlyState;
            }

            @lexer::footer {
                // lexer grammar footer
            }

            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar, "L.g4");
        string generatedSource = GetGeneratedSource(grammar, "L.g4");

        AssertNoUnsupportedDiagnostics(diagnostics);
        Assert.AreEqual(1, diagnostics.Count(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedHeaderInjectedByGenerator.Code));
        Assert.AreEqual(1, diagnostics.Count(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedMembersInjectedByGenerator.Code));
        Assert.AreEqual(1, diagnostics.Count(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedFooterInjectedByGenerator.Code));
        AssertInOrder(generatedSource, "// <auto-generated-lexer-header>", "// lexer grammar header", "// </auto-generated-lexer-header>", "internal static partial class L");
        AssertInOrder(generatedSource, "internal sealed partial class LExecutionContext", "// <auto-generated-lexer-members>", "private int LexerOnlyState;", "// </auto-generated-lexer-members>", "internal LExecutionContext Fork()");
        AssertInOrder(generatedSource, "// <auto-generated-lexer-footer>", "// lexer grammar footer", "// </auto-generated-lexer-footer>");
    }

    /// <summary>
    /// Ensures scoped lexer grammar-level actions in lexer-only grammars report the expected injection diagnostic constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerGrammarLexerNamedActions_ReportExpectedConstructDiagnostics()
    {
        const string grammar = """
            lexer grammar L;

            @lexer::header {
                // lexer grammar header
            }

            @lexer::members {
                private int LexerOnlyState;
            }

            @lexer::footer {
                // lexer grammar footer
            }

            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar, "L.g4");

        AssertSingleHeaderInjectedDiagnostic(diagnostics, "Grammar @lexer::header action");
        AssertSingleMembersInjectedDiagnostic(diagnostics, "Grammar @lexer::members action");
        AssertSingleFooterInjectedDiagnostic(diagnostics, "Grammar @lexer::footer action");
        AssertNoUnsupportedDiagnostics(diagnostics);
    }

    /// <summary>
    /// Ensures lexer inline actions in lexer-only grammars remain unsupported generator execution constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerGrammarLexerAction_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            lexer grammar L;
            A : 'a' { OnLex(context); } ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar, "L.g4"));
    }

    /// <summary>
    /// Ensures lexer predicates in lexer-only grammars no longer report unsupported generator execution diagnostics.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerGrammarLexerPredicate_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            lexer grammar L;
            A : { IsA(context) }? 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar, "L.g4"));
    }

    /// <summary>
    /// Ensures rule <c>@init</c> lifecycle actions are not reported as unsupported embedded code now that they are generated.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_RuleInitAction_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;

            start
            @init { OnInit(context); }
                : A ;

            A : 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>
    /// Ensures rule <c>@after</c> lifecycle actions are not reported as unsupported embedded code now that they are generated.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_RuleAfterAction_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;

            start
            @after { OnAfter(context); }
                : A ;

            A : 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>
    /// Ensures supported parser semantic predicates are not reported as unsupported generator constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_SupportedParserPredicate_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : { inputPosition == 0 }? A ;
            A : 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>
    /// Ensures supported inline parser actions are not reported as unsupported generator constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_SupportedInlineParserAction_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } A ;
            A : 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>
    /// Ensures invalid C# inside a supported generated predicate remains owned by Roslyn instead of the unsupported-construct diagnostic.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_InvalidSupportedPredicateCode_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : { return "not bool"; }? A ;
            A : 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>Verifies invalid parser attribute roots are reported structurally by the generator.</summary>
    [TestMethod]
    public void GeneratorDiagnostics_UnknownParserAttributeRoot_ReportsDedicatedError()
    {
        const string grammar = """
            grammar P;
            start @after { Seen = $unknown.value; } : A ;
            A : 'a' ;
            """;

        Diagnostic diagnostic = RunGenerator(grammar).Single(candidate => candidate.Id == ParserDiagnostics.InvalidEmbeddedParserAttribute.Code);

        Assert.AreEqual(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "not the current rule name");
    }

    /// <summary>Verifies invalid list-label returns, lifecycle locations, writes, and ambiguity are diagnosed.</summary>
    [DataTestMethod]
    [DataRow("@after { Seen = $xs.missing; }", "xs+=child", "List-label parser attribute '$xs.missing' is not supported")]
    [DataRow("@after { $xs.value = 1; }", "xs+=child", "writes are not supported")]
    [DataRow("@init { Seen = $xs.value; }", "xs+=child", "List-label parser attribute '$xs.value' is not supported")]
    [DataRow(": { return $xs.value.Count > 0; }? xs+=child", null, "not supported in semantic predicates")]
    [DataRow("@after { Seen = $x.value; }", "x=child | x+=child", "List-label parser attribute '$x.value' is not supported")]
    public void GeneratorDiagnostics_InvalidListParserAttribute_ReportsDedicatedError(string ruleFragment, string? content, string expectedMessage)
    {
        string grammar = $$"""
            grammar P;
            start {{ruleFragment}} {{(content is null ? ";" : ": " + content + " ;")}}
            child returns [int value] : A ;
            A : 'a' ;
            """;

        Diagnostic diagnostic = RunGenerator(grammar).Single(candidate => candidate.Id == ParserDiagnostics.InvalidEmbeddedParserAttribute.Code);

        StringAssert.Contains(diagnostic.GetMessage(), expectedMessage);
    }

    /// <summary>Verifies assignment-label access in init and attribute writes are rejected before source emission.</summary>
    [DataTestMethod]
    [DataRow("@init { Seen = $x.value; }", "Assignment label 'x' is not available in @init")]
    [DataRow("@after { $x.value = 1; }", "writes are not supported")]
    public void GeneratorDiagnostics_InvalidAttributeLifecycleOrWrite_ReportsDedicatedError(string lifecycle, string expectedMessage)
    {
        string grammar = $$"""
            grammar P;
            start {{lifecycle}} : x=child ;
            child returns [int value] : A ;
            A : 'a' ;
            """;

        Diagnostic diagnostic = RunGenerator(grammar).Single(candidate => candidate.Id == ParserDiagnostics.InvalidEmbeddedParserAttribute.Code);

        StringAssert.Contains(diagnostic.GetMessage(), expectedMessage);
    }

    /// <summary>Verifies invalid bare parser attributes are surfaced through generator diagnostics.</summary>
    [DataTestMethod]
    [DataRow("start @after { Seen = $unknown; } : A ;", "does not resolve")]
    [DataRow("start[int count] @after { $count = 1; } : A ;", "Parser parameter '$count' is read-only")]
    [DataRow("start[int count] : { return $count > 0; }? A ;", "not supported in semantic predicates")]
    [DataRow("start @after { Seen = $x; } : x=child ;", "label access")]
    public void GeneratorDiagnostics_InvalidBareParserAttribute_ReportsDedicatedError(string ruleText, string expectedMessage)
    {
        string grammar = $$"""
            grammar P;
            {{ruleText}}
            child returns [int value] : A ;
            A : 'a' ;
            """;

        Diagnostic diagnostic = RunGenerator(grammar).Single(candidate => candidate.Id == ParserDiagnostics.InvalidEmbeddedParserAttribute.Code);

        StringAssert.Contains(diagnostic.GetMessage(), expectedMessage);
    }

    /// <summary>
    /// Asserts that exactly one unsupported embedded-code diagnostic exists and that it describes the expected construct.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    /// <param name="expectedConstruct">Expected construct kind text.</param>
    /// <returns>The matching diagnostic.</returns>
    private static Diagnostic AssertSingleUnsupportedDiagnostic(ImmutableArray<Diagnostic> diagnostics, string expectedConstruct)
    {
        var matches = diagnostics
            .Where(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedCodeConstructNotExecutedByGenerator.Code)
            .ToArray();

        Assert.AreEqual(1, matches.Length, string.Join(Environment.NewLine, diagnostics));
        Assert.AreEqual(ParserDiagnostics.EmbeddedCodeConstructNotExecutedByGenerator.Code, matches[0].Id);
        StringAssert.Contains(matches[0].GetMessage(), expectedConstruct);
        StringAssert.Contains(matches[0].GetMessage(), "Supported generated C# embedded constructs");
        return matches[0];
    }

    /// <summary>
    /// Asserts that exactly one parser-members injection compatibility diagnostic exists and describes the expected construct.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    /// <param name="expectedConstruct">Expected construct kind text.</param>
    /// <returns>The matching diagnostic.</returns>
    private static Diagnostic AssertSingleMembersInjectedDiagnostic(ImmutableArray<Diagnostic> diagnostics, string expectedConstruct)
    {
        var matches = diagnostics
            .Where(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedMembersInjectedByGenerator.Code)
            .ToArray();

        Assert.AreEqual(1, matches.Length, string.Join(Environment.NewLine, diagnostics));
        Assert.AreEqual(ParserDiagnostics.EmbeddedMembersInjectedByGenerator.Code, matches[0].Id);
        Assert.AreEqual(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, matches[0].Severity);
        StringAssert.Contains(matches[0].GetMessage(), expectedConstruct);
        return matches[0];
    }

    /// <summary>
    /// Asserts that exactly one parser-header injection compatibility diagnostic exists and describes the expected construct.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    /// <param name="expectedConstruct">Expected construct kind text.</param>
    /// <returns>The matching diagnostic.</returns>
    private static Diagnostic AssertSingleHeaderInjectedDiagnostic(ImmutableArray<Diagnostic> diagnostics, string expectedConstruct)
    {
        var matches = diagnostics
            .Where(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedHeaderInjectedByGenerator.Code)
            .ToArray();

        Assert.AreEqual(1, matches.Length, string.Join(Environment.NewLine, diagnostics));
        Assert.AreEqual(ParserDiagnostics.EmbeddedHeaderInjectedByGenerator.Code, matches[0].Id);
        Assert.AreEqual(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, matches[0].Severity);
        StringAssert.Contains(matches[0].GetMessage(), expectedConstruct);
        return matches[0];
    }

    /// <summary>
    /// Asserts that exactly one parser-footer injection compatibility diagnostic exists and describes the expected construct.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    /// <param name="expectedConstruct">Expected construct kind text.</param>
    /// <returns>The matching diagnostic.</returns>
    private static Diagnostic AssertSingleFooterInjectedDiagnostic(ImmutableArray<Diagnostic> diagnostics, string expectedConstruct)
    {
        var matches = diagnostics
            .Where(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedFooterInjectedByGenerator.Code)
            .ToArray();

        Assert.AreEqual(1, matches.Length, string.Join(Environment.NewLine, diagnostics));
        Assert.AreEqual(ParserDiagnostics.EmbeddedFooterInjectedByGenerator.Code, matches[0].Id);
        Assert.AreEqual(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, matches[0].Severity);
        StringAssert.Contains(matches[0].GetMessage(), expectedConstruct);
        return matches[0];
    }

    /// <summary>
    /// Asserts that no unsupported embedded-code source-generator diagnostics were emitted.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    private static void AssertNoUnsupportedDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        Assert.IsFalse(
            diagnostics.Any(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedCodeConstructNotExecutedByGenerator.Code),
            string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>
    /// Asserts that no parser-members injection compatibility diagnostics were emitted.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    private static void AssertNoMembersInjectedDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        Assert.IsFalse(
            diagnostics.Any(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedMembersInjectedByGenerator.Code),
            string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>
    /// Asserts that no parser-footer injection compatibility diagnostics were emitted.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    private static void AssertNoFooterInjectedDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        Assert.IsFalse(
            diagnostics.Any(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedFooterInjectedByGenerator.Code),
            string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>
    /// Asserts that no parser-header injection compatibility diagnostics were emitted.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    private static void AssertNoHeaderInjectedDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        Assert.IsFalse(
            diagnostics.Any(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedHeaderInjectedByGenerator.Code),
            string.Join(Environment.NewLine, diagnostics));
    }


    /// <summary>
    /// Ensures unsupported custom lexer named actions report deterministic messages and are not injected into parser source.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_CustomLexerNamedAction_ReportsDeterministicDiagnostic()
    {
        const string grammar = """
            grammar P;

            @lexer::custom {
                // lexer custom
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleUnsupportedDiagnostic(diagnostics, "Grammar @lexer::custom action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), "Named action scope '@lexer::custom' is not supported by this generator.");
        Assert.IsFalse(generatedSource.Contains("// lexer custom", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures unknown named-action scopes report deterministic diagnostics.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_UnknownNamedActionScope_ReportsDeterministicDiagnostic()
    {
        const string grammar = """
            grammar P;

            @tree::members {
                // tree
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostic = AssertSingleUnsupportedDiagnostic(RunGenerator(grammar), "Grammar @tree::members action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), "Named action scope '@tree::members' is not supported by this generator.");
        Assert.IsFalse(generatedSource.Contains("// tree", StringComparison.Ordinal));
    }


    /// <summary>
    /// Ensures <c>@parser::members</c> in lexer grammars stays unsupported with a deterministic reason.
    /// </summary>
    [TestMethod]
    public void ParserScopedMembers_InLexerGrammar_RemainsUnsupportedWithDeterministicDiagnostic()
    {
        const string grammar = """
            lexer grammar L;

            @parser::members {
                public int Seen = 1;
            }

            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar, "L.g4");
        var diagnostic = AssertSingleUnsupportedDiagnostic(diagnostics, "Grammar @parser::members action");
        string generatedSource = GetGeneratedSource(grammar, "L.g4");

        StringAssert.Contains(diagnostic.GetMessage(), "Parser named action '@parser::members' is not valid in a lexer grammar.");
        AssertNoMembersInjectedDiagnostics(diagnostics);
        Assert.IsFalse(generatedSource.Contains("public int Seen = 1;", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures unknown parser named actions remain unsupported metadata instead of source injection.
    /// </summary>
    [TestMethod]
    public void UnknownParserNamedAction_RemainsMetadataOnlyOrUnsupported()
    {
        const string grammar = """
            grammar P;

            @parser::init {
                // unsupported init marker
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostic = AssertSingleUnsupportedDiagnostic(RunGenerator(grammar), "Grammar @parser::init action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), "Parser named action '@parser::init' is not supported by this generator.");
        AssertNoMembersInjectedDiagnostics(RunGenerator(grammar));
        Assert.IsFalse(generatedSource.Contains("unsupported init marker", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures unknown parser named-action names report deterministic diagnostics.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_UnknownParserNamedAction_ReportsDeterministicDiagnostic()
    {
        const string grammar = """
            grammar P;

            @parser::custom {
                // custom
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostic = AssertSingleUnsupportedDiagnostic(RunGenerator(grammar), "Grammar @parser::custom action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), "Parser named action '@parser::custom' is not supported by this generator.");
        Assert.IsFalse(generatedSource.Contains("// custom", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures scoped parser compatibility actions in lexer-only grammars report deterministic invalid-grammar diagnostics.
    /// </summary>
    [DataTestMethod]
    [DataRow("header", "// parser header")]
    [DataRow("members", "// parser members")]
    [DataRow("footer", "// parser footer")]
    public void GeneratorDiagnostics_ParserNamedActionsInLexerGrammar_ReportDeterministicDiagnostic(string actionName, string marker)
    {
        string grammar = $$"""
            lexer grammar L;

            @parser::{{actionName}} {
                {{marker}}
            }

            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar, "L.g4");
        var diagnostic = AssertSingleUnsupportedDiagnostic(diagnostics, $"Grammar @parser::{actionName} action");
        string generatedSource = GetGeneratedSource(grammar, "L.g4");

        StringAssert.Contains(diagnostic.GetMessage(), $"Parser named action '@parser::{actionName}' is not valid in a lexer grammar.");
        Assert.IsFalse(generatedSource.Contains(marker, StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures unscoped parser compatibility actions in lexer-only grammars report deterministic unsupported diagnostics.
    /// </summary>
    [DataTestMethod]
    [DataRow("header", "// header")]
    [DataRow("members", "// members")]
    [DataRow("footer", "// footer")]
    public void GeneratorDiagnostics_UnscopedActionsInLexerGrammar_ReportDeterministicDiagnostic(string actionName, string marker)
    {
        string grammar = $$"""
            lexer grammar L;

            @{{actionName}} {
                {{marker}}
            }

            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar, "L.g4");
        var diagnostic = AssertSingleUnsupportedDiagnostic(diagnostics, $"Grammar @{actionName} action");
        string generatedSource = GetGeneratedSource(grammar, "L.g4");

        StringAssert.Contains(diagnostic.GetMessage(), $"Unscoped grammar action '@{actionName}' is not supported in lexer grammars by this generator.");
        Assert.IsFalse(generatedSource.Contains(marker, StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures supported parser compatibility actions emit only injection compatibility warnings.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_SupportedParserNamedActions_DoNotReportUnsupportedDiagnostics()
    {
        const string grammar = """
            grammar P;

            @header {
                using System;
            }

            @parser::header {
                using System.Linq;
            }

            @members {
                public int Seen { get; private set; }
            }

            @parser::members {
                public int Other { get; private set; }
            }

            @footer {
                // footer
            }

            @parser::footer {
                // parser footer
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);

        AssertNoUnsupportedDiagnostics(diagnostics);
        Assert.AreEqual(2, diagnostics.Count(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedHeaderInjectedByGenerator.Code));
        Assert.AreEqual(2, diagnostics.Count(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedMembersInjectedByGenerator.Code));
        Assert.AreEqual(2, diagnostics.Count(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedFooterInjectedByGenerator.Code));
    }


    /// <summary>
    /// Ensures scoped lexer compatibility actions in parser-only grammars remain unsupported because no lexer is generated.
    /// </summary>
    [DataTestMethod]
    [DataRow("header", "// lexer header")]
    [DataRow("members", "// lexer members")]
    [DataRow("footer", "// lexer footer")]
    public void GeneratorDiagnostics_LexerNamedActionsInParserGrammar_ReportUnsupportedEmbeddedCode(string actionName, string marker)
    {
        string grammar = $$"""
            parser grammar P;

            @lexer::{{actionName}} {
                {{marker}}
            }

            start : A ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleUnsupportedDiagnostic(diagnostics, $"Grammar @lexer::{actionName} action");
        string generatedSource = GetGeneratedSource(grammar);

        StringAssert.Contains(diagnostic.GetMessage(), $"Named action scope '@lexer::{actionName}' is not supported by this generator.");
        Assert.IsFalse(generatedSource.Contains(marker, StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures supported lexer named actions do not report unsupported diagnostics and preserve deterministic source order.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_SupportedLexerNamedActions_DoNotReportUnsupportedDiagnosticsAndEmitInOrder()
    {
        const string grammar = """
            grammar P;

            @parser::header {
                // parser header
            }

            @lexer::header {
                // lexer header
            }

            @parser::members {
                private int ParserState;
            }

            @lexer::members {
                private int LexerState;
            }

            @parser::footer {
                // parser footer
            }

            @lexer::footer {
                // lexer footer
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostics = RunGenerator(grammar);
        string generatedSource = GetGeneratedSource(grammar);

        AssertNoUnsupportedDiagnostics(diagnostics);
        Assert.AreEqual(2, diagnostics.Count(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedHeaderInjectedByGenerator.Code));
        Assert.AreEqual(2, diagnostics.Count(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedMembersInjectedByGenerator.Code));
        Assert.AreEqual(2, diagnostics.Count(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedFooterInjectedByGenerator.Code));
        AssertInOrder(generatedSource, "// <auto-generated-parser-header>", "// <auto-generated-lexer-header>", "internal static partial class P");
        AssertInOrder(generatedSource, "// <auto-generated-parser-members>", "// <auto-generated-lexer-members>", "internal PExecutionContext Fork()");
        AssertInOrder(generatedSource, "// <auto-generated-parser-footer>", "// <auto-generated-lexer-footer>");
    }

    /// <summary>
    /// Asserts that source snippets appear in the supplied order.
    /// </summary>
    /// <param name="source">Source text to inspect.</param>
    /// <param name="snippets">Ordered snippets that must exist.</param>
    private static void AssertInOrder(string source, params string[] snippets)
    {
        var previous = -1;
        foreach (string snippet in snippets)
        {
            int current = source.IndexOf(snippet, StringComparison.Ordinal);
            Assert.IsTrue(current >= 0, $"Missing snippet: {snippet}");
            Assert.IsTrue(current > previous, source);
            previous = current;
        }
    }

    /// <summary>
    /// Runs the ANTLR4 grammar source generator against an in-memory grammar file.
    /// </summary>
    /// <param name="grammar">ANTLR4 grammar source.</param>
    /// <param name="path">Virtual grammar file path used by the source generator.</param>
    /// <returns>Generator diagnostics.</returns>
    private static ImmutableArray<Diagnostic> RunGenerator(string grammar, string path = "P.g4")
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorDiagnosticsTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText("namespace Generated.Tests;", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new Antlr4GrammarGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(path, grammar)],
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Diagnostics;
    }

    /// <summary>
    /// Runs the generator and returns the generated C# source for one in-memory grammar file.
    /// </summary>
    /// <param name="grammar">ANTLR4 grammar source.</param>
    /// <param name="path">Virtual grammar file path used by the source generator.</param>
    /// <returns>Generated source text.</returns>
    private static string GetGeneratedSource(string grammar, string path = "P.g4")
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new Antlr4GrammarGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(path, grammar)],
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));

        var compilation = CSharpCompilation.Create("GeneratorDiagnosticsSourceTests");
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().GeneratedTrees.Single().ToString();
    }

    /// <summary>
    /// Ensures generated source does not contain lexer hook helpers for lexer actions.
    /// </summary>
    /// <param name="grammar">ANTLR4 grammar source.</param>
    /// <param name="path">Virtual grammar file path used by the source generator.</param>
    private static void AssertGeneratedSourceDoesNotContainLexerHook(string grammar, string path = "P.g4")
    {
        string generatedSource = GetGeneratedSource(grammar, path);

        Assert.IsFalse(generatedSource.Contains("__Lexer", StringComparison.Ordinal));
        Assert.IsFalse(generatedSource.Contains("__Action_A", StringComparison.Ordinal));
    }

    /// <summary>
    /// In-memory additional file used to provide grammar text to the source generator.
    /// </summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        /// <summary>
        /// Initializes an in-memory additional text file.
        /// </summary>
        /// <param name="path">Virtual file path.</param>
        /// <param name="text">File content.</param>
        public InMemoryAdditionalText(string path, string text)
        {
            Path = path;
            _text = SourceText.From(text, Encoding.UTF8);
        }

        /// <inheritdoc />
        public override string Path { get; }

        /// <inheritdoc />
        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return _text;
        }
    }
}

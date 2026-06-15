using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.Parser.Generators.Internal;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies the narrow lexical and metadata-aware parser attribute rewrite.
/// </summary>
[TestClass]
public class EmbeddedParserAttributeRewriterTests
{
    private const string GrammarText = """
        grammar P;
        start returns [int own] : x=child | xs+=child | t=A ;
        child returns [int value, object nullable] : nested=leaf ;
        leaf returns [int value] : A ;
        A : 'a' ;
        """;

    /// <summary>Verifies assignment-label and current-rule reads are rewritten independently.</summary>
    [TestMethod]
    public void Rewrite_SupportedReads_RewritesMultipleReferences()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = (int)$x.value + (int)$start.own;");

        Assert.AreEqual("Seen = (int)GetRequiredLabeledRuleCallReturn(context, \"x\", \"value\") + (int)GetRequiredRuleReturn(context, \"own\");", result.Code);
        Assert.AreEqual(0, result.Errors.Count);
    }

    /// <summary>Verifies strings, characters, and comments are copied without attribute rewriting.</summary>
    [TestMethod]
    public void Rewrite_LiteralsAndComments_IgnoresAttributeText()
    {
        const string code = "var a = \"$x.value\\\"\"; var b = @\"$x.value\"; var c = '$'; // $x.value\n/* $x.value */ Seen = $x.value;";

        EmbeddedParserAttributeRewriteResult result = Rewrite(code);

        StringAssert.Contains(result.Code, "\"$x.value\\\"\"");
        StringAssert.Contains(result.Code, "@\"$x.value\"");
        StringAssert.Contains(result.Code, "// $x.value");
        StringAssert.Contains(result.Code, "/* $x.value */");
        StringAssert.Contains(result.Code, "Seen = GetRequiredLabeledRuleCallReturn(context, \"x\", \"value\")");
        Assert.AreEqual(0, result.Errors.Count);
    }

    /// <summary>Verifies interpolated and raw string contents are not rewritten.</summary>
    [TestMethod]
    public void Rewrite_InterpolatedAndRawStrings_IgnoresAttributeText()
    {
        const string code = "var a = $\"$x.value\"; var b = \"\"\"$x.value\"\"\";";

        EmbeddedParserAttributeRewriteResult result = Rewrite(code);

        Assert.AreEqual(code, result.Code);
        Assert.AreEqual(0, result.Errors.Count);
    }

    /// <summary>Verifies unsupported roots, list labels, token labels, missing returns, bare reads, and chains are diagnosed.</summary>
    [DataTestMethod]
    [DataRow("$unknown.value", "not the current rule name")]
    [DataRow("$xs.value", "List label 'xs'")]
    [DataRow("$t.value", "Token label 't'")]
    [DataRow("$x.missing", "not declared by parser rule 'child'")]
    [DataRow("$start.missing", "not declared by current parser rule 'start'")]
    [DataRow("$x", "Bare parser attribute '$x'")]
    [DataRow("$x.value.other", "Chained parser attribute '$x.value'")]
    public void Rewrite_UnsupportedReference_ReportsDeterministicError(string code, string expectedMessage)
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite(code);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], expectedMessage);
    }

    /// <summary>Verifies every supported write-target shape is rejected before C# compilation.</summary>
    [DataTestMethod]
    [DataRow("$x.value = 1;")]
    [DataRow("$start.own = 1;")]
    [DataRow("$x.value += 1;")]
    [DataRow("$x.value++;")]
    [DataRow("++$x.value;")]
    [DataRow("Use(ref $x.value);")]
    [DataRow("Use(out $x.value);")]
    public void Rewrite_WriteTarget_ReportsDeterministicError(string code)
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite(code);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "writes are not supported");
    }

    /// <summary>Verifies assignment-labeled child returns are statically unavailable during initialization.</summary>
    [TestMethod]
    public void Rewrite_AssignmentLabelInInit_ReportsLifecycleError()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $x.value;", EmbeddedParserAttributeLocationKind.Init);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "not available in @init");
    }

    /// <summary>Verifies current-rule name resolution takes precedence over a same-named assignment label.</summary>
    [TestMethod]
    public void Rewrite_CurrentRuleName_TakesPrecedenceOverSameNamedLabel()
    {
        const string grammarText = """
            grammar P;
            start returns [int own] : start=child ;
            child returns [int value] : A ;
            A : 'a' ;
            """;
        G4Grammar grammar = Parse(grammarText);
        G4Rule rule = grammar.ParserRules.Single(candidate => candidate.Name == "start");

        EmbeddedParserAttributeRewriteResult result = EmbeddedParserAttributeRewriter.Rewrite("Seen = $start.own;", grammar, rule, EmbeddedParserAttributeLocationKind.After);

        Assert.AreEqual("Seen = GetRequiredRuleReturn(context, \"own\");", result.Code);
        Assert.AreEqual(0, result.Errors.Count);
    }

    /// <summary>Verifies labels declared only in child rules are not visible in the parent rule.</summary>
    [TestMethod]
    public void Rewrite_NestedChildLabel_IsNotVisibleInParent()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $nested.value;");

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "not the current rule name");
    }

    /// <summary>Rewrites code against the start rule in the shared grammar.</summary>
    private static EmbeddedParserAttributeRewriteResult Rewrite(string code, EmbeddedParserAttributeLocationKind kind = EmbeddedParserAttributeLocationKind.After)
    {
        G4Grammar grammar = Parse(GrammarText);
        G4Rule rule = grammar.ParserRules.Single(candidate => candidate.Name == "start");
        return EmbeddedParserAttributeRewriter.Rewrite(code, grammar, rule, kind);
    }

    /// <summary>Parses an in-memory grammar for deterministic generator-stage tests.</summary>
    private static G4Grammar Parse(string grammarText)
    {
        return new G4Parser(new G4Tokenizer(grammarText).Tokenize()).Parse();
    }
}

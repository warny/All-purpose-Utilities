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
        start[int count, string? name] returns [int own] locals [int total, object value] : x=child | xs+=child | t=A ;
        child returns [int value, object nullable] : nested=leaf ;
        leaf returns [int value] : A ;
        A : 'a' ;
        """;

    /// <summary>Verifies assignment-label, list-label, and current-rule reads are rewritten independently.</summary>
    [TestMethod]
    public void Rewrite_SupportedReads_RewritesMultipleReferences()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = (int)$x.value + $xs.value.Count + $xs.value.Select(v => v).Count() + (int)$start.own;");

        Assert.AreEqual("Seen = (int)GetRequiredLabeledRuleCallReturn(context, \"x\", \"value\") + GetLabeledRuleCallReturns(context, \"xs\", \"value\").Count + GetLabeledRuleCallReturns(context, \"xs\", \"value\").Select(v => v).Count() + (int)GetRequiredRuleReturn(context, \"own\");", result.Code);
        Assert.AreEqual(0, result.Errors.Count);
    }

    /// <summary>Verifies current-rule parameters and locals are rewritten through typed helper calls.</summary>
    [TestMethod]
    public void Rewrite_ParameterAndLocalReads_RewritesToTypedHelpers()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $count + $total; Name = $name; Value = $value;");

        Assert.AreEqual("Seen = GetRequiredRuleParameter<int>(context, \"count\") + GetRequiredRuleLocal<int>(context, \"total\"); Name = GetRequiredRuleParameter<string?>(context, \"name\"); Value = GetRequiredRuleLocal<object>(context, \"value\");", result.Code);
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

    /// <summary>Verifies unsupported roots, token labels, missing returns, bare reads, and chains are diagnosed.</summary>
    [DataTestMethod]
    [DataRow("$unknown.value", "not the current rule name")]
    [DataRow("$t.value", "Token label 't'")]
    [DataRow("$x.missing", "not declared by parser rule 'child'")]
    [DataRow("$xs.missing", "not declared by any parser rule referenced by list label 'xs'")]
    [DataRow("$start.missing", "not declared by current parser rule 'start'")]
    [DataRow("$x", "label access")]
    [DataRow("$xs", "label access")]
    [DataRow("$x.value.other", "Chained parser attribute '$x.value'")]
    [DataRow("$xs.value.other", "Chained parser attribute '$xs.value'")]
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
    [DataRow("$xs.value = 1;")]
    [DataRow("$xs.value += 1;")]
    [DataRow("$xs.value++;")]
    [DataRow("++$x.value;")]
    [DataRow("++$xs.value;")]
    [DataRow("Use(ref $x.value);")]
    [DataRow("Use(ref $xs.value);")]
    [DataRow("Use(out $x.value);")]
    [DataRow("Use(out $xs.value);")]
    public void Rewrite_WriteTarget_ReportsDeterministicError(string code)
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite(code);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "writes are not supported");
    }

    /// <summary>Verifies bare parameter and local write-target shapes are rejected.</summary>
    [DataTestMethod]
    [DataRow("$count = 1;")]
    [DataRow("$count += 1;")]
    [DataRow("$count++;")]
    [DataRow("++$count;")]
    [DataRow("Use(ref $count);")]
    [DataRow("Use(out $count);")]
    public void Rewrite_BareParameterWriteTarget_ReportsDeterministicError(string code)
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite(code);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "writes are not supported");
    }

    /// <summary>Verifies bare parameter and local reads are rejected in predicates.</summary>
    [TestMethod]
    public void Rewrite_BareParameterInPredicate_ReportsLifecycleError()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("$count > 0", EmbeddedParserAttributeLocationKind.Predicate);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "not supported in semantic predicates");
    }

    /// <summary>Verifies assignment-labeled child returns are statically unavailable during initialization.</summary>
    [TestMethod]
    public void Rewrite_AssignmentLabelInInit_ReportsLifecycleError()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $x.value;", EmbeddedParserAttributeLocationKind.Init);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "not available in @init");
    }

    /// <summary>Verifies list-labeled child returns are statically unavailable during initialization.</summary>
    [TestMethod]
    public void Rewrite_ListLabelInInit_ReportsLifecycleError()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $xs.value;", EmbeddedParserAttributeLocationKind.Init);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "List label 'xs' is not available in @init");
    }

    /// <summary>Verifies list-label attributes remain unavailable in semantic predicates.</summary>
    [TestMethod]
    public void Rewrite_ListLabelInPredicate_ReportsLifecycleError()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("$xs.value.Count > 0", EmbeddedParserAttributeLocationKind.Predicate);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "not supported in semantic predicates");
    }

    /// <summary>Verifies a lexical name shared by assignment and list labels requires explicit helpers.</summary>
    [TestMethod]
    public void Rewrite_AmbiguousAssignmentAndListLabel_ReportsDeterministicError()
    {
        const string grammarText = """
            grammar P;
            start : x=child | x+=child ;
            child returns [int value] : A ;
            A : 'a' ;
            """;
        G4Grammar grammar = Parse(grammarText);
        G4Rule rule = grammar.ParserRules.Single(candidate => candidate.Name == "start");

        EmbeddedParserAttributeRewriteResult result = EmbeddedParserAttributeRewriter.Rewrite("Seen = $x.value;", grammar, rule, EmbeddedParserAttributeLocationKind.After);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "used as both assignment and list label");
    }

    /// <summary>Verifies repeated list labels validate all targets without depending on alternative order.</summary>
    [DataTestMethod]
    [DataRow("xs+=withValue | xs+=withoutValue")]
    [DataRow("xs+=withoutValue | xs+=withValue")]
    public void Rewrite_RepeatedListLabelTargets_AcceptsReturnDeclaredByAnyTarget(string alternatives)
    {
        string grammarText = $$"""
            grammar P;
            start : {{alternatives}} ;
            withValue returns [int value] : A ;
            withoutValue : A ;
            A : 'a' ;
            """;
        G4Grammar grammar = Parse(grammarText);
        G4Rule rule = grammar.ParserRules.Single(candidate => candidate.Name == "start");

        EmbeddedParserAttributeRewriteResult result = EmbeddedParserAttributeRewriter.Rewrite("Values = $xs.value;", grammar, rule, EmbeddedParserAttributeLocationKind.After);

        Assert.AreEqual("Values = GetLabeledRuleCallReturns(context, \"xs\", \"value\");", result.Code);
        Assert.AreEqual(0, result.Errors.Count);
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

    /// <summary>Verifies a parameter takes bare-name precedence while label-return syntax remains label-based.</summary>
    [TestMethod]
    public void Rewrite_ParameterAndLabelSameName_SeparatesBareAndReturnForms()
    {
        const string grammarText = """
            grammar P;
            start[int x] : x=child ;
            child returns [int value] : A ;
            A : 'a' ;
            """;
        G4Grammar grammar = Parse(grammarText);
        G4Rule rule = grammar.ParserRules.Single(candidate => candidate.Name == "start");

        EmbeddedParserAttributeRewriteResult result = EmbeddedParserAttributeRewriter.Rewrite("A = $x; B = $x.value;", grammar, rule, EmbeddedParserAttributeLocationKind.After);

        Assert.AreEqual("A = GetRequiredRuleParameter<int>(context, \"x\"); B = GetRequiredLabeledRuleCallReturn(context, \"x\", \"value\");", result.Code);
        Assert.AreEqual(0, result.Errors.Count);
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

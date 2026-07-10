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

    /// <summary>Verifies current-rule returns plus assignment-label and list-label child returns are rewritten.</summary>
    [TestMethod]
    public void Rewrite_SupportedCurrentRuleRead_RewritesBareReturnAndAssignmentLabelReturn()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = (int)$x.value + $xs.value.Count + $xs.value.Select(v => v).Count() + (int)$own;");

        Assert.AreEqual("Seen = (int)GetRequiredLabeledRuleCallReturn(context, \"x\", \"value\") + GetLabeledRuleCallReturns(context, \"xs\", \"value\").Count + GetLabeledRuleCallReturns(context, \"xs\", \"value\").Select(v => v).Count() + (int)GetRequiredRuleReturn<int>(context, \"own\");", result.Code);
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
    [DataRow("$t.value", "Token label 't' cannot be used as a parser rule-return attribute")]
    [DataRow("$x.missing", "Return 'missing' is not declared by every parser rule referenced by assignment label 'x'. Missing on parser rule 'child'")]
    [DataRow("$xs.missing", "Return 'missing' is not declared by every parser rule referenced by list label 'xs'")]
    [DataRow("$start.missing", "Dotted current-rule return attribute '$start.missing' is not supported")]
    [DataRow("$x", "label access")]
    [DataRow("$xs", "label access")]
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
        StringAssert.Contains(result.Errors[0], code.Contains("$start.own") ? "Current-rule dotted return writes are not supported" : "writes are not supported");
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
        StringAssert.Contains(result.Errors[0], code.Contains("ref") || code.Contains("out") ? "ref/out parser attributes are not supported" : "read-only");
    }

    /// <summary>Verifies current-rule return writes are rewritten to typed helper calls in @after.</summary>
    [TestMethod]
    public void Rewrite_CurrentRuleReturnWrites_RewritesToTypedHelpers()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("$own = $count + 1; $own += 2; $own++; ++$own; $own--; --$own;");

        StringAssert.Contains(result.Code, "SetRequiredRuleReturn<int>(context, \"own\", GetRequiredRuleParameter<int>(context, \"count\") + 1)");
        StringAssert.Contains(result.Code, "GetRequiredRuleReturn<int>(context, \"own\") + 2");
        StringAssert.Contains(result.Code, "GetRequiredRuleReturn<int>(context, \"own\") + 1");
        StringAssert.Contains(result.Code, "GetRequiredRuleReturn<int>(context, \"own\") - 1");
        Assert.AreEqual(0, result.Errors.Count);
    }

    /// <summary>Verifies comparison operators in return write right-hand sides are not treated as nested assignments.</summary>
    [DataTestMethod]
    [DataRow("$own = $count == 1 ? 10 : 20;")]
    [DataRow("$own = $count != 0 ? 1 : 0;")]
    [DataRow("$own = $count <= 10 ? 1 : 0;")]
    [DataRow("$own = $count >= 10 ? 1 : 0;")]
    public void Rewrite_CurrentRuleReturnWriteRhs_AllowsComparisonOperators(string code)
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite(code);

        Assert.AreEqual(0, result.Errors.Count);
        StringAssert.Contains(result.Code, "SetRequiredRuleReturn<int>(context, \"own\", GetRequiredRuleParameter<int>(context, \"count\")");
    }

    /// <summary>Verifies unsupported current-rule return write contexts produce deterministic diagnostics.</summary>
    [DataTestMethod]
    [DataRow("value = $own++;", "Increment/decrement parser attributes are supported only as standalone statements")]
    [DataRow("Foo(++$own);", "Increment/decrement parser attributes are supported only as standalone statements")]
    [DataRow("Use(ref $own);", "ref/out parser attributes are not supported")]
    [DataRow("Use(out $own);", "ref/out parser attributes are not supported")]
    public void Rewrite_UnsupportedReturnWriteContext_ReportsDeterministicError(string code, string expectedMessage)
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite(code);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], expectedMessage);
    }

    /// <summary>Verifies current-rule return writes are rejected outside supported parser action code.</summary>
    [DataTestMethod]
    [DataRow(0, "Parser return writes are not supported in @init")]
    [DataRow(2, "Parser return writes are not supported in semantic predicates")]
    public void Rewrite_CurrentRuleReturnWriteInUnsupportedLocation_ReportsLifecycleError(int kindValue, string expectedMessage)
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("$own = 1;", (EmbeddedParserAttributeLocationKind)kindValue);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], expectedMessage);
    }

    /// <summary>Verifies current-rule return writes are supported in inline parser actions.</summary>
    [TestMethod]
    public void Rewrite_CurrentRuleReturnWriteInInlineAction_RewritesToTypedHelper()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("$own = 1;", EmbeddedParserAttributeLocationKind.InlineAction);

        Assert.AreEqual(0, result.Errors.Count);
        Assert.AreEqual("SetRequiredRuleReturn<int>(context, \"own\", 1);", result.Code);
    }

    /// <summary>Verifies bare parameter and local reads are rejected in predicates.</summary>
    [TestMethod]
    public void Rewrite_BareParameterInPredicate_ReportsLifecycleError()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("$count > 0", EmbeddedParserAttributeLocationKind.Predicate);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "not supported in semantic predicates");
    }

    /// <summary>Verifies assignment-labeled child returns are unavailable during initialization.</summary>
    [TestMethod]
    public void Rewrite_AssignmentLabelInInit_ReportsLifecycleError()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $x.value;", EmbeddedParserAttributeLocationKind.Init);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "Assignment label 'x' is not available in @init");
    }

    /// <summary>Verifies list-labeled child returns remain unavailable during initialization.</summary>
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

        Assert.AreEqual("Seen = $x.value;", result.Code);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "used as both assignment and list label");
    }


    /// <summary>Verifies reused assignment labels validate every referenced parser-rule target before rewriting.</summary>
    [DataTestMethod]
    [DataRow("x=withoutValue | x=withValue")]
    [DataRow("x=withValue | x=withoutValue")]
    public void Rewrite_ReusedAssignmentLabelTargets_RejectsWhenAnyTargetMissingReturn(string alternatives)
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

        EmbeddedParserAttributeRewriteResult result = EmbeddedParserAttributeRewriter.Rewrite("Seen = $x.value;", grammar, rule, EmbeddedParserAttributeLocationKind.After);

        Assert.AreEqual("Seen = $x.value;", result.Code);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "Return 'value' is not declared by every parser rule referenced by assignment label 'x'");
        StringAssert.Contains(result.Errors[0], "withoutValue");
    }

    /// <summary>Verifies reused assignment labels rewrite when every referenced parser-rule target declares the requested return.</summary>
    [TestMethod]
    public void Rewrite_ReusedAssignmentLabelTargets_RewritesWhenAllTargetsDeclareReturn()
    {
        const string grammarText = """
            grammar P;
            start : x=left | x=right ;
            left returns [int value] : A ;
            right returns [int value] : A ;
            A : 'a' ;
            """;
        G4Grammar grammar = Parse(grammarText);
        G4Rule rule = grammar.ParserRules.Single(candidate => candidate.Name == "start");

        EmbeddedParserAttributeRewriteResult result = EmbeddedParserAttributeRewriter.Rewrite("Seen = $x.value;", grammar, rule, EmbeddedParserAttributeLocationKind.After);

        Assert.AreEqual("Seen = GetRequiredLabeledRuleCallReturn(context, \"x\", \"value\");", result.Code);
        Assert.AreEqual(0, result.Errors.Count);
    }

    /// <summary>Verifies repeated list-label targets validate every referenced parser-rule target before rewriting.</summary>
    [DataTestMethod]
    [DataRow("xs+=withValue | xs+=withoutValue")]
    [DataRow("xs+=withoutValue | xs+=withValue")]
    public void Rewrite_RepeatedListLabelTargets_RejectsWhenAnyTargetMissingReturn(string alternatives)
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

        Assert.AreEqual("Values = $xs.value;", result.Code);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "Return 'value' is not declared by every parser rule referenced by list label 'xs'");
    }

    /// <summary>Verifies dotted current-rule returns remain unsupported even when a label shares the rule name.</summary>
    [TestMethod]
    public void Rewrite_CurrentRuleDottedReturn_RemainsUnsupported()
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

        Assert.AreEqual("Seen = $start.own;", result.Code);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "Dotted current-rule return attribute '$start.own' is not supported");
        StringAssert.Contains(result.Errors[0], "Use bare '$own' instead");
    }

    /// <summary>Verifies labels declared only in child rules are not visible in the parent rule.</summary>
    [TestMethod]
    public void Rewrite_NestedChildLabel_IsNotVisibleInParent()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $nested.value;");

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "not the current rule name");
    }

    /// <summary>Verifies a parameter takes bare-name precedence while same-name label-return syntax still rewrites through the label helper.</summary>
    [TestMethod]
    public void Rewrite_ParameterAndLabelSameName_RewritesBareAndLabelReturnForm()
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

    /// <summary>Verifies unsupported dotted forms stay rejected after list-label sugar support.</summary>
    [DataTestMethod]
        [DataRow("Seen = $child.value;", "not the current rule name")]
    [DataRow("Seen = $start.value;", "Dotted current-rule return attribute '$start.value' is not supported")]
    public void ParseWithEmbeddedCode_TransformerStillRejectsUnsupportedLabelReturnSyntax(string code, string expectedMessage)
    {
        const string grammarText = """
            grammar P;
            start returns [int value] : c=child | x=child | xs+=child ;
            child returns [int value] : A ;
            A : 'a' ;
            """;
        G4Grammar grammar = Parse(grammarText);
        G4Rule rule = grammar.ParserRules.Single(candidate => candidate.Name == "start");

        EmbeddedParserAttributeRewriteResult result = EmbeddedParserAttributeRewriter.Rewrite(code, grammar, rule, EmbeddedParserAttributeLocationKind.After);

        Assert.AreEqual(code, result.Code);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], expectedMessage);
    }

    /// <summary>Verifies assignment-label child return syntax rewrites to the required helper.</summary>
    [TestMethod]
    public void Rewrite_AssignmentLabelReturn_RewritesToRequiredHelper()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $x.value;");

        Assert.AreEqual("Seen = GetRequiredLabeledRuleCallReturn(context, \"x\", \"value\");", result.Code);
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

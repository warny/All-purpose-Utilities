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

    /// <summary>Verifies current-rule returns are rewritten while child and labeled returns remain unsupported.</summary>
    [TestMethod]
    public void Rewrite_SupportedCurrentRuleRead_RewritesAndRejectsChildReferences()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = (int)$x.value + $xs.value.Count + $xs.value.Select(v => v).Count() + (int)$start.own;");

        Assert.AreEqual("Seen = (int)$x.value + $xs.value.Count + $xs.value.Select(v => v).Count() + (int)GetRequiredRuleReturn(context, \"own\");", result.Code);
        Assert.AreEqual(3, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "Labeled rule-call return attribute '$x.value' is not supported");
        StringAssert.Contains(result.Errors[1], "Labeled rule-call return attribute '$xs.value' is not supported");
        StringAssert.Contains(result.Errors[2], "Labeled rule-call return attribute '$xs.value' is not supported");
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
        StringAssert.Contains(result.Code, "Seen = $x.value");
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "Labeled rule-call return attribute '$x.value' is not supported");
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
    [DataRow("$t.value", "Labeled rule-call return attribute '$t.value' is not supported")]
    [DataRow("$x.missing", "Labeled rule-call return attribute '$x.missing' is not supported")]
    [DataRow("$xs.missing", "Labeled rule-call return attribute '$xs.missing' is not supported")]
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

    /// <summary>Verifies assignment-labeled child returns remain unsupported during initialization.</summary>
    [TestMethod]
    public void Rewrite_AssignmentLabelInInit_ReportsUnsupportedLabelError()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $x.value;", EmbeddedParserAttributeLocationKind.Init);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "Labeled rule-call return attribute '$x.value' is not supported");
    }

    /// <summary>Verifies list-labeled child returns remain unsupported during initialization.</summary>
    [TestMethod]
    public void Rewrite_ListLabelInInit_ReportsUnsupportedLabelError()
    {
        EmbeddedParserAttributeRewriteResult result = Rewrite("Seen = $xs.value;", EmbeddedParserAttributeLocationKind.Init);

        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "Labeled rule-call return attribute '$xs.value' is not supported");
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
        StringAssert.Contains(result.Errors[0], "Labeled rule-call return attribute '$x.value' is not supported");
    }

    /// <summary>Verifies repeated list-label return convenience remains unsupported regardless of target order.</summary>
    [DataTestMethod]
    [DataRow("xs+=withValue | xs+=withoutValue")]
    [DataRow("xs+=withoutValue | xs+=withValue")]
    public void Rewrite_RepeatedListLabelTargets_ReportsUnsupportedLabelReturn(string alternatives)
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
        StringAssert.Contains(result.Errors[0], "Labeled rule-call return attribute '$xs.value' is not supported");
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

    /// <summary>Verifies a parameter takes bare-name precedence while same-name label-return syntax remains unsupported.</summary>
    [TestMethod]
    public void Rewrite_ParameterAndLabelSameName_RewritesBareAndRejectsReturnForm()
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

        Assert.AreEqual("A = GetRequiredRuleParameter<int>(context, \"x\"); B = $x.value;", result.Code);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "Labeled rule-call return attribute '$x.value' is not supported");
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

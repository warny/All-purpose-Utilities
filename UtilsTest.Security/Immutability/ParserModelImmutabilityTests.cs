using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Antlr4.Common;
using Utils.Parser.Antlr4.Common.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Runtime;
using Utils.Parser.Source;

namespace UtilsTest.Security.Immutability;

/// <summary>Verifies defensive immutable snapshots in public parser models.</summary>
[TestClass]
public sealed class ParserModelImmutabilityTests
{
    /// <summary>Verifies ordered grammar structures reject array and list aliasing.</summary>
    [TestMethod]
    public void GrammarStructures_CaptureImmutableSnapshots()
    {
        RuleContent first = new LiteralMatch("first");
        RuleContent second = new LiteralMatch("second");
        RuleContent[] items = [first];
        var sequence = new Sequence(items);
        items[0] = second;
        Assert.AreSame(first, sequence.Items[0]);
        Assert.IsFalse(sequence.Items is RuleContent[]);
        Assert.IsFalse(sequence.Items is List<RuleContent>);

        var alternative1 = new Alternative(0, Associativity.Left, first);
        var alternative2 = new Alternative(1, Associativity.Left, second);
        var alternatives = new List<Alternative> { alternative1 };
        var alternation = new Alternation(alternatives);
        alternatives[0] = alternative2;
        alternatives.Add(alternative2);
        Assert.AreSame(alternative1, alternation.Alternatives[0]);
        Assert.AreEqual(1, alternation.Alternatives.Count);
    }

    /// <summary>Verifies parser definitions and rule metadata normalize every external collection.</summary>
    [TestMethod]
    public void ParserDefinitionAndRule_CaptureImmutableSnapshots()
    {
        Rule first = CreateRule("first");
        Rule second = CreateRule("second");
        var actions = new List<GrammarAction> { new("header", "code") };
        var imports = new List<GrammarImport> { new("Base") };
        var modes = new List<LexerMode> { new("DEFAULT_MODE", [first]) };
        var parserRules = new List<Rule> { first };
        var tokens = new HashSet<string>(StringComparer.Ordinal) { "TOKEN" };
        var definition = new ParserDefinition("Grammar", GrammarType.Combined, null, actions, imports, modes, tokens, tokens, [], parserRules, first);
        actions.Clear(); imports.Clear(); modes.Clear(); parserRules[0] = second; tokens.Add("OTHER");
        Assert.AreEqual(1, definition.Actions.Count);
        Assert.AreEqual(1, definition.Imports.Count);
        Assert.AreEqual(1, definition.Modes.Count);
        Assert.AreSame(first, definition.ParserRules[0]);
        Assert.IsFalse(definition.DeclaredTokens.Contains("OTHER"));

        parserRules[0] = first;
        var allRules = new Dictionary<string, Rule>(StringComparer.Ordinal) { ["first"] = first };
        ParserDefinition resolved = definition with { AllRules = allRules, ParserRules = parserRules };
        allRules["first"] = second; parserRules[0] = second;
        Assert.AreSame(first, resolved.AllRules["first"]);
        Assert.AreSame(first, resolved.ParserRules[0]);
        Assert.IsFalse(resolved.AllRules is Dictionary<string, Rule>);
        Assert.IsFalse(resolved.ParserRules is List<Rule>);

        var parameters = new List<RuleParameter> { new("int", "value") };
        var throws = new List<string> { "Exception" };
        var catches = new List<RuleCatchClause> { new("Exception ex", "handle") };
        var metadata = new RuleExceptionMetadata(throws, catches, null);
        var rule = first with { Parameters = parameters, ExceptionMetadata = metadata };
        parameters.Clear(); throws.Clear(); catches.Clear();
        Assert.AreEqual(1, rule.Parameters!.Count);
        Assert.AreEqual(1, rule.ExceptionMetadata!.Throws.Count);
        Assert.AreEqual(1, rule.ExceptionMetadata.CatchClauses.Count);
    }

    /// <summary>Verifies lexer modes capture their ordered rule input.</summary>
    [TestMethod]
    public void LexerMode_CapturesImmutableRuleSnapshot()
    {
        Rule first = CreateRule("FIRST");
        Rule second = CreateRule("SECOND");
        Rule[] rules = [first];
        var mode = new LexerMode("DEFAULT_MODE", rules);
        rules[0] = second;
        Assert.AreSame(first, mode.Rules[0]);
        Assert.IsFalse(mode.Rules is Rule[]);
    }

    /// <summary>Verifies model dictionaries and sets preserve ordinal immutable snapshots.</summary>
    [TestMethod]
    public void MetadataCollections_CaptureImmutableSnapshotsAndOrdinalComparers()
    {
        var optionsSource = new Dictionary<string, string>(StringComparer.Ordinal) { ["A"] = "one" };
        var options = new RuleOptions(optionsSource);
        optionsSource["A"] = "two";
        optionsSource["B"] = "three";
        Assert.AreEqual("one", options.Values["A"]);
        Assert.IsFalse(options.Values.ContainsKey("B"));
        Assert.IsFalse(options.Values.ContainsKey("a"));
        Assert.IsFalse(options.Values is Dictionary<string, string>);

        var names = new HashSet<string>(StringComparer.Ordinal) { "A" };
        var binding = new GrammarExtensionBinding { LexerRuleNames = names, DeclaredTokens = names, DeclaredChannels = names };
        names.Add("B");
        Assert.IsFalse(binding.LexerRuleNames.Contains("B"));
        Assert.IsFalse(binding.LexerRuleNames.Contains("a"));
        Assert.IsFalse(binding.LexerRuleNames is HashSet<string>);
    }

    /// <summary>Verifies init setters normalize mutable collections used through with expressions.</summary>
    [TestMethod]
    public void RecordWithExpressions_NormalizeAssignedCollections()
    {
        RuleContent first = new LiteralMatch("first");
        RuleContent second = new LiteralMatch("second");
        var original = new Sequence([first]);
        RuleContent[] replacement = [first];
        var copy = original with { Items = replacement };
        replacement[0] = second;
        Assert.AreSame(first, copy.Items[0]);
        Assert.IsFalse(copy.Items is RuleContent[]);

        var alternatives = new[] { new Alternative(0, Associativity.Left, first) };
        var info = new LeftRecursiveRuleInfo { Rule = CreateRule("rule"), BaseAlternatives = alternatives, RecursiveAlternatives = alternatives };
        alternatives[0] = new Alternative(1, Associativity.Right, second);
        Assert.AreEqual(0, info.BaseAlternatives[0].Priority);
    }

    /// <summary>Verifies parse-tree nodes capture child arrays once without exposing them.</summary>
    [TestMethod]
    public void ParseNodes_CaptureImmutableChildrenSnapshot()
    {
        Rule rule = CreateRule("rule");
        ParseNode first = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "first", rule);
        ParseNode second = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "second", rule);
        ParseNode[] children = [first];
        var node = new ParserNode(new SourceSpan(0, 0), "DEFAULT_MODE", rule, children);
        IReadOnlyList<ParseNode> snapshot = node.Children;
        children[0] = second;
        Assert.AreSame(first, node.Children[0]);
        Assert.AreSame(snapshot, node.Children);
        Assert.IsFalse(node.Children is ParseNode[]);
        Assert.IsFalse(node.Children is List<ParseNode>);
    }

    /// <summary>Verifies quantifier-node with expressions use the inherited immutable child storage.</summary>
    [TestMethod]
    public void QuantifierNode_WithChildren_PreservesImmutabilityAndRecordEquality()
    {
        Rule rule = CreateRule("rule");
        SourceSpan span = new(0, 0);
        ParseNode first = new ErrorNode(span, "DEFAULT_MODE", "first", rule);
        ParseNode second = new ErrorNode(span, "DEFAULT_MODE", "second", rule);
        ParseNode third = new ErrorNode(span, "DEFAULT_MODE", "third", rule);
        var original = new QuantifierNode(span, "DEFAULT_MODE", rule, [first]);
        ParseNode[] replacement = [second];

        QuantifierNode copy = original with { Children = replacement };
        replacement[0] = third;
        var expected = new QuantifierNode(span, "DEFAULT_MODE", rule, [second]);

        Assert.AreSame(second, copy.Children[0]);
        Assert.IsFalse(copy.Children is ParseNode[]);
        Assert.AreEqual(expected, copy);
        Assert.AreEqual(expected.GetHashCode(), copy.GetHashCode());
    }

    /// <summary>Verifies ANTLR prequel models capture lists, sets, dictionaries, and diagnostics.</summary>
    [TestMethod]
    public void AntlrPrequelModels_CaptureImmutableSnapshots()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["language"] = "CSharp" };
        var optionSet = new Antlr4OptionSet(values);
        values["language"] = "Java";
        Assert.AreEqual("CSharp", optionSet.Values["language"]);
        Assert.IsFalse(optionSet.Values is Dictionary<string, string>);

        var imports = new List<Antlr4ImportInfo> { new("Base", null) };
        var actions = new List<Antlr4ActionInfo> { new("header", "code", null) };
        var tokens = new HashSet<string>(StringComparer.Ordinal) { "TOKEN" };
        var model = new Antlr4PrequelModel(optionSet, imports, actions, tokens, tokens);
        imports.Clear(); actions.Clear(); tokens.Add("OTHER");
        Assert.AreEqual(1, model.Imports.Count);
        Assert.AreEqual(1, model.Actions.Count);
        Assert.IsFalse(model.DeclaredTokens.Contains("OTHER"));

        var diagnostics = new List<Antlr4PrequelDiagnostic>();
        var result = new Antlr4PrequelValidationResult(diagnostics);
        diagnostics.Add(null!);
        Assert.AreEqual(0, result.Diagnostics.Count);
        Assert.IsFalse(result.Diagnostics is List<Antlr4PrequelDiagnostic>);
    }

    /// <summary>Creates a minimal rule for immutable model tests.</summary>
    private static Rule CreateRule(string name)
    {
        return new Rule(name, 0, false, new Alternation([new Alternative(0, Associativity.Left, new Sequence([]))]));
    }
}

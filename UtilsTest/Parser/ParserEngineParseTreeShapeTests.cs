using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Runtime;
using static UtilsTest.Parser.TestInfrastructure.ParserEngineTestHelpers;

namespace UtilsTest.Parser;

/// <summary>
/// Structural parse-tree tests that intentionally lock observable tree shape and navigator behavior.
/// </summary>
[TestClass]
public class ParserEngineParseTreeShapeTests
{
    // ═══════════════════════════════════════════════════════════════
    // Tree structure — node navigation
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// For "2+5" the grammar produces:
    ///   eval
    ///   └─[0] additionExp
    ///         ├─[0] multiplyExp  (wraps "2")
    ///         └─[1] additionExp  (quantifier outer)
    ///               └─[0] additionExp  (sequence '+' multiplyExp)
    ///                     ├─[0] LexerNode("+"  rule=additionExp)
    ///                     └─[1] multiplyExp  (wraps "5")
    /// </summary>
    [TestMethod]
    public void Parser_TreeStructure_EvalHasOneChild_AdditionExp()
    {
        var nav = new ParseTreeNavigator(Parse("2+5"));
        Assert.AreEqual("eval", nav.RuleName);
        Assert.AreEqual(1, nav.RawChildren!.Count);
        Assert.AreEqual("additionExp", nav[0].RuleName);
    }

    [TestMethod]
    public void Parser_TreeStructure_AdditionExpHasTwoChildren()
    {
        var nav = new ParseTreeNavigator(Parse("2+5"));
        // Child[0] = multiplyExp for the left operand
        // Child[1] = additionExp quantifier wrapper for ('+' multiplyExp)*
        var additionExp = nav[0];
        Assert.AreEqual(2, additionExp.RawChildren!.Count);
    }

    [TestMethod]
    public void Parser_TreeStructure_LeftOperand_LeadsToNumber2()
    {
        var nav = new ParseTreeNavigator(Parse("2+5"));
        // eval[0] → additionExp[0] → multiplyExp[0] → atomExp[0] → LexerNode("2")
        Assert.AreEqual("2", nav[0][0][0][0].Token!.Text);
    }

    [TestMethod]
    public void Parser_TreeStructure_PlusOperator_AtExpectedPosition()
    {
        var nav = new ParseTreeNavigator(Parse("2+5"));
        // eval[0] → additionExp[1] → quantifier[0] → seq[0] → LexerNode("+")
        Assert.AreEqual("+", nav[0][1][0][0].Token!.Text);
    }

    [TestMethod]
    public void Parser_TreeStructure_RightOperand_LeadsToNumber5()
    {
        var nav = new ParseTreeNavigator(Parse("2+5"));
        // eval[0] → additionExp[1] → seq[0] → multiplyExp[1][0] → atomExp[0] → LexerNode("5")
        Assert.AreEqual("5", nav[0][1][0][1][0][0].Token!.Text);
    }

    [TestMethod]
    public void Parser_TreeStructure_PrecedenceMul_DeepInTree()
    {
        // In "1+2*3", multiplication must be inside multiplyExp, deeper than addition.
        // eval[0]     = additionExp
        // [0][1][0]   = sequence for '+' multiplyExp(2*3)
        // [0][1][0][1]= multiplyExp for "2*3"
        var nav = new ParseTreeNavigator(Parse("1+2*3"));
        var multiplyExp = nav[0][1][0][1];
        Assert.AreEqual("multiplyExp", multiplyExp.RuleName);

        // multiplyExp[0] → atomExp[0] → "2"
        Assert.AreEqual("2", multiplyExp[0][0].Token!.Text);

        // multiplyExp[1][0] → seq('*' atomExp)
        //   [0] → "*", [1][0] → "3"
        Assert.AreEqual("*", multiplyExp[1][0][0].Token!.Text);
        Assert.AreEqual("3", multiplyExp[1][0][1][0].Token!.Text);
    }

    [TestMethod]
    public void Parser_TreeStructure_ChainedAdditions_ThreeOperands()
    {
        // "1+2+3": additionExp[0]=multiplyExp("1"), additionExp[1]=quantifier(2 iterations)
        var nav = new ParseTreeNavigator(Parse("1+2+3"));
        var additionExp = nav[0];
        Assert.AreEqual(2, additionExp.RawChildren!.Count);

        // Left operand "1"
        Assert.AreEqual("1", additionExp[0][0][0].Token!.Text);

        // Quantifier has 2 children: seq('+' 2) and seq('+' 3)
        var quantOuter = additionExp[1];
        Assert.AreEqual(2, quantOuter.RawChildren!.Count);

        Assert.AreEqual("+", quantOuter[0][0].Token!.Text);   // first '+'
        Assert.AreEqual("2", quantOuter[0][1][0][0].Token!.Text); // "2"

        Assert.AreEqual("+", quantOuter[1][0].Token!.Text);   // second '+'
        Assert.AreEqual("3", quantOuter[1][1][0][0].Token!.Text); // "3"
    }

    [TestMethod]
    public void Navigator_TryChild_ReturnsNullOnMissingIndex()
    {
        var nav = new ParseTreeNavigator(Parse("42"));
        Assert.IsNull(nav[0].TryChild(99));
    }

    [TestMethod]
    public void Navigator_TryDescendant_ReturnsNullWhenNotFound()
    {
        var nav = new ParseTreeNavigator(Parse("42"));
        Assert.IsNull(nav.TryDescendant("nonExistentRule"));
    }

    [TestMethod]
    public void Navigator_Descendant_FindsAdditionExpByName()
    {
        var nav = new ParseTreeNavigator(Parse("1+2"));
        var additionNode = nav.Descendant("additionExp");
        Assert.AreEqual("additionExp", additionNode.RuleName);
    }

    [TestMethod]
    public void Navigator_Descendants_FindsAllNumbers()
    {
        var nav = new ParseTreeNavigator(Parse("1+2*3"));
        var numbers = nav.Descendants()
            .Where(n => n.IsLexer && n.Token!.RuleName == "Number")
            .Select(n => n.Token!.Text)
            .ToList();
        CollectionAssert.AreEqual(new[] { "1", "2", "3" }, numbers);
    }

    [TestMethod]
    public void Navigator_Children_EnumeratesDirectChildren()
    {
        var nav = new ParseTreeNavigator(Parse("2+5"));
        var additionExp = nav[0];
        var children = additionExp.Children().ToList();
        Assert.AreEqual(2, children.Count);
        Assert.AreEqual("multiplyExp", children[0].RuleName);
    }

    [TestMethod]
    public void Navigator_ToString_DescribesNode()
    {
        var nav = new ParseTreeNavigator(Parse("42"));
        // Root is a ParserNode
        StringAssert.Contains(nav.ToString(), "ParserNode");
        // A leaf LexerNode
        var leaf = nav.Descendants().First(n => n.IsLexer);
        StringAssert.Contains(leaf.ToString(), "LexerNode");
    }

    // ═══════════════════════════════════════════════════════════════
    // QuantifierNode — TryChild / Children navigation through wrappers
    // ═══════════════════════════════════════════════════════════════

    // Grammar used by these tests:
    //   statement : keyword suffix? ;
    //   keyword   : WORD ;
    //   suffix    : COMMA WORD ;
    // When suffix is present the parser produces:
    //   statement
    //   ├─ keyword
    //   └─ QuantifierNode(statement)  ← quantifier wrapper for suffix?
    //        └─ suffix
    private static readonly CompiledGrammar OptionalGrammar = Antlr4GrammarConverter.Compile("""
        grammar OptionalTest;
        statement : keyword suffix? ;
        keyword   : WORD ;
        suffix    : COMMA WORD ;
        COMMA     : ',' ;
        WORD      : ('a'..'z')+ ;
        WS        : (' ' | '\t')+ -> skip ;
        """);

    [TestMethod]
    public void QuantifierNode_TryChild_FindsOptionalChild_WhenPresent()
    {
        // nav IS statement (the root rule of OptionalGrammar)
        var nav = new ParseTreeNavigator(OptionalGrammar.Parse("hello, world"));
        var suffix = nav.TryChild("suffix");
        Assert.IsNotNull(suffix, "TryChild must find 'suffix' through the QuantifierNode wrapper.");
        Assert.AreEqual("suffix", suffix!.RuleName);
    }

    [TestMethod]
    public void QuantifierNode_TryChild_ReturnsNull_WhenOptionalChildAbsent()
    {
        var nav = new ParseTreeNavigator(OptionalGrammar.Parse("hello"));
        Assert.IsNull(nav.TryChild("suffix"), "TryChild must return null when the optional child is absent.");
    }

    [TestMethod]
    public void QuantifierNode_TryChild_FindsDirectChild_Unchanged()
    {
        var nav = new ParseTreeNavigator(OptionalGrammar.Parse("hello, world"));
        var keyword = nav.TryChild("keyword");
        Assert.IsNotNull(keyword, "TryChild must still find direct children.");
        Assert.AreEqual("keyword", keyword!.RuleName);
    }

    [TestMethod]
    public void QuantifierNode_Children_YieldsOptionalChild_WhenPresent()
    {
        var nav = new ParseTreeNavigator(OptionalGrammar.Parse("hello, world"));
        var suffixes = nav.Children("suffix").ToList();
        Assert.AreEqual(1, suffixes.Count, "Children must yield the optional child through the QuantifierNode wrapper.");
        Assert.AreEqual("suffix", suffixes[0].RuleName);
    }

    [TestMethod]
    public void QuantifierNode_Children_YieldsEmpty_WhenOptionalChildAbsent()
    {
        var nav = new ParseTreeNavigator(OptionalGrammar.Parse("hello"));
        var suffixes = nav.Children("suffix").ToList();
        Assert.AreEqual(0, suffixes.Count, "Children must yield nothing when the optional child is absent.");
    }

    [TestMethod]
    public void QuantifierNode_ExistingIndexNavigation_StillWorks()
    {
        // nav IS statement — children: [keyword_node, QuantifierNode(statement, [suffix_node])]
        var nav = new ParseTreeNavigator(OptionalGrammar.Parse("hello, world"));
        Assert.AreEqual(2, nav.RawChildren!.Count,
            "The QuantifierNode wrapper must still appear as a direct child (integer index navigation unchanged).");
        Assert.IsInstanceOfType<QuantifierNode>(nav.RawChildren[1],
            "The second child must be a QuantifierNode.");
    }
}

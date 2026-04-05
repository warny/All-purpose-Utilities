using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserEngineTests
{
    private static readonly CompiledGrammar Grammar = new CompiledGrammar(ExpGrammar.Build());

    private static ParseNode Parse(string input) => Grammar.Parse(input);

    private static List<Token> Lex(string input) => Grammar.Tokenize(input).ToList();

    // ═══════════════════════════════════════════════════════════════
    // Basic parsing
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parser_SingleNumber()
    {
        var tree = Parse("42");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsInstanceOfType<ParserNode>(tree);

        // eval → additionExp → multiplyExp → atomExp → Number
        var root = (ParserNode)tree;
        Assert.AreEqual("eval", root.Rule.Name);
    }

    [TestMethod]
    public void Parser_SingleNumber_ContainsNumberToken()
    {
        var tree = Parse("42");
        var numberNode = FindFirstLexerNode(tree, "Number");
        Assert.IsNotNull(numberNode, "Should find a Number token in the tree");
        Assert.AreEqual("42", numberNode!.Token.Text);
    }

    [TestMethod]
    public void Parser_SimpleAddition()
    {
        var tree = Parse("1+2");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsInstanceOfType<ParserNode>(tree);

        // Should contain two Number tokens
        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(2, numbers.Count);
        Assert.AreEqual("1", numbers[0].Token.Text);
        Assert.AreEqual("2", numbers[1].Token.Text);
    }

    [TestMethod]
    public void Parser_SimpleSubtraction()
    {
        var tree = Parse("5-3");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(2, numbers.Count);
        Assert.AreEqual("5", numbers[0].Token.Text);
        Assert.AreEqual("3", numbers[1].Token.Text);
    }

    [TestMethod]
    public void Parser_SimpleMultiplication()
    {
        var tree = Parse("4*5");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(2, numbers.Count);
    }

    [TestMethod]
    public void Parser_SimpleDivision()
    {
        var tree = Parse("10/2");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(2, numbers.Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // Operator precedence (structural)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parser_MixedAddMul_ThreeNumbers()
    {
        // 1+2*3 → additionExp has multiplyExp(1) and multiplyExp(2*3)
        var tree = Parse("1+2*3");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(3, numbers.Count);
        Assert.AreEqual("1", numbers[0].Token.Text);
        Assert.AreEqual("2", numbers[1].Token.Text);
        Assert.AreEqual("3", numbers[2].Token.Text);
    }

    [TestMethod]
    public void Parser_MixedAddMul_OperatorsPresent()
    {
        var tree = Parse("1+2*3");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        // The tree must contain both a + and * operator as literal tokens
        var allLexerNodes = FindAllLexerNodesAny(tree);
        var operatorTexts = allLexerNodes
            .Select(n => n.Token.Text)
            .Where(t => t == "+" || t == "*")
            .ToList();

        CollectionAssert.Contains(operatorTexts, "+");
        CollectionAssert.Contains(operatorTexts, "*");
    }

    [TestMethod]
    public void Parser_SubAndDiv()
    {
        var tree = Parse("10-4/2");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(3, numbers.Count);
    }

    [TestMethod]
    public void Parser_ChainedAdditions()
    {
        var tree = Parse("1+2+3+4");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(4, numbers.Count);
    }

    [TestMethod]
    public void Parser_ChainedMultiplications()
    {
        var tree = Parse("2*3*4*5");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(4, numbers.Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // Parentheses
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parser_ParenthesizedExpression()
    {
        var tree = Parse("(1+2)*3");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(3, numbers.Count);
    }

    [TestMethod]
    public void Parser_NestedParentheses()
    {
        var tree = Parse("((1+2))");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(2, numbers.Count);
    }

    [TestMethod]
    public void Parser_DeeplyNestedParentheses()
    {
        var tree = Parse("(((42)))");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numberNode = FindFirstLexerNode(tree, "Number");
        Assert.IsNotNull(numberNode);
        Assert.AreEqual("42", numberNode!.Token.Text);
    }

    [TestMethod]
    public void Parser_ComplexWithParentheses()
    {
        var tree = Parse("(1 + 2) * (3 - 4) / 5");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(5, numbers.Count);
    }

    [TestMethod]
    public void Parser_ComplexArithmeticExpression_ParsesWithoutCycle()
    {
        var tree = Parse("(5+10)*3/(10+2)-4+(3/2*(8-1))");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(10, numbers.Count);
    }

    [TestMethod]
    public void Parser_CycleGuard_AllowsSameRuleAtDifferentPositions()
    {
        var tree = Parse("1+2+3+4+5+6+7+8+9");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(9, numbers.Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // Whitespace handling
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parser_WithSpaces()
    {
        var tree = Parse("  1  +  2  ");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(2, numbers.Count);
    }

    [TestMethod]
    public void Parser_WithTabsAndNewlines()
    {
        var tree = Parse("1\n+\t2");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(2, numbers.Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // Decimal numbers
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parser_DecimalNumber()
    {
        var tree = Parse("3.14");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numberNode = FindFirstLexerNode(tree, "Number");
        Assert.IsNotNull(numberNode);
        Assert.AreEqual("3.14", numberNode!.Token.Text);
    }

    [TestMethod]
    public void Parser_DecimalExpression()
    {
        var tree = Parse("3.14 + 2.72");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(2, numbers.Count);
        Assert.AreEqual("3.14", numbers[0].Token.Text);
        Assert.AreEqual("2.72", numbers[1].Token.Text);
    }

    // ═══════════════════════════════════════════════════════════════
    // Tree structure
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parser_TreeSpansCorrectly()
    {
        var tree = Parse("1+2");
        Assert.IsTrue(tree.Span.Length > 0, "Root span should cover the parsed content");
    }

    [TestMethod]
    public void Parser_RootRuleIsEval()
    {
        var tree = Parse("1");
        Assert.AreEqual("eval", tree.Rule.Name);
    }

    [TestMethod]
    public void Parser_FindAdditionExpInTree()
    {
        var tree = Parse("1+2");
        var additionNode = FindFirstParserNode(tree, "additionExp");
        Assert.IsNotNull(additionNode, "Tree should contain an additionExp node");
    }

    [TestMethod]
    public void Parser_FindMultiplyExpInTree()
    {
        var tree = Parse("1*2");
        var multiplyNode = FindFirstParserNode(tree, "multiplyExp");
        Assert.IsNotNull(multiplyNode, "Tree should contain a multiplyExp node");
    }

    [TestMethod]
    public void Parser_FindAtomExpInTree()
    {
        var tree = Parse("42");
        var atomNode = FindFirstParserNode(tree, "atomExp");
        Assert.IsNotNull(atomNode, "Tree should contain an atomExp node");
    }

    // ═══════════════════════════════════════════════════════════════
    // Error handling
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parser_EmptyInput_ReturnsErrorNode()
    {
        var tree = Parse("");
        // An empty input with no tokens should fail to parse
        Assert.IsInstanceOfType<ErrorNode>(tree);
    }

    // ═══════════════════════════════════════════════════════════════
    // Complex expressions
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parser_ComplexExpressionAllOperators()
    {
        var tree = Parse("1 + 2 - 3 * 4 / 5");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(5, numbers.Count);
    }

    [TestMethod]
    public void Parser_MixedParensAndOperators()
    {
        var tree = Parse("(1 + 2) * 3 + 4 / (5 - 6)");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(6, numbers.Count);
    }

    [TestMethod]
    public void Parser_LongChainedExpression()
    {
        var tree = Parse("1+2+3+4+5+6+7+8+9+10");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var numbers = FindAllLexerNodes(tree, "Number");
        Assert.AreEqual(10, numbers.Count);
    }

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
    // Trailing-token rejection (P1 review fix)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Parse_TrailingTokens_ReturnsErrorNode()
    {
        // "2+3" is valid, but the extra "???" tokens should not be accepted.
        // The lexer will emit ERROR tokens for '?', so the parse must fail.
        var result = Parse("2+3 ???");
        Assert.IsInstanceOfType<ErrorNode>(result,
            "A parse with trailing unrecognized tokens must return ErrorNode, not a valid tree.");
    }

    [TestMethod]
    public void Parse_ValidFullInput_ReturnsParserNode()
    {
        // Regression: ensure the trailing-token check does not break valid inputs.
        var result = Parse("2+3");
        Assert.IsInstanceOfType<ParserNode>(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private static LexerNode? FindFirstLexerNode(ParseNode node, string ruleName)
        => new ParseTreeNavigator(node)
            .Descendants()
            .Prepend(new ParseTreeNavigator(node))
            .FirstOrDefault(n => n.IsLexer && n.RuleName == ruleName)
            ?.Node as LexerNode;

    private static List<LexerNode> FindAllLexerNodes(ParseNode node, string ruleName)
        => new ParseTreeNavigator(node)
            .Descendants()
            .Prepend(new ParseTreeNavigator(node))
            .Where(n => n.IsLexer && n.RuleName == ruleName)
            .Select(n => (LexerNode)n.Node)
            .ToList();

    private static List<LexerNode> FindAllLexerNodesAny(ParseNode node)
        => new ParseTreeNavigator(node)
            .Descendants()
            .Prepend(new ParseTreeNavigator(node))
            .Where(n => n.IsLexer)
            .Select(n => (LexerNode)n.Node)
            .ToList();

    private static ParserNode? FindFirstParserNode(ParseNode node, string ruleName)
        => new ParseTreeNavigator(node)
            .Descendants()
            .Prepend(new ParseTreeNavigator(node))
            .FirstOrDefault(n => n.IsParser && n.RuleName == ruleName)
            ?.Node as ParserNode;
}

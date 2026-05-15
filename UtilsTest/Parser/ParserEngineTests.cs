using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

using static UtilsTest.Parser.TestInfrastructure.ParserEngineTestHelpers;

[TestClass]
public class ParserEngineTests
{
    
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

}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserEngineTests
{
    private static ParseNode Parse(string input)
    {
        var definition = ExpGrammar.Build();
        var lexer = new LexerEngine(definition);
        var stream = new StringCharStream(input);
        var tokens = lexer.Tokenize(stream).ToList();
        var parser = new ParserEngine(definition);
        return parser.Parse(tokens);
    }

    private static List<Token> Lex(string input)
    {
        var definition = ExpGrammar.Build();
        var lexer = new LexerEngine(definition);
        return lexer.Tokenize(new StringCharStream(input)).ToList();
    }

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
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private static LexerNode? FindFirstLexerNode(ParseNode node, string ruleName)
    {
        if (node is LexerNode ln && ln.Rule.Name == ruleName)
            return ln;

        if (node is ParserNode pn)
        {
            foreach (var child in pn.Children)
            {
                var found = FindFirstLexerNode(child, ruleName);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    private static List<LexerNode> FindAllLexerNodes(ParseNode node, string ruleName)
    {
        var result = new List<LexerNode>();
        CollectLexerNodes(node, ruleName, result);
        return result;
    }

    private static void CollectLexerNodes(ParseNode node, string ruleName, List<LexerNode> result)
    {
        if (node is LexerNode ln && ln.Rule.Name == ruleName)
            result.Add(ln);

        if (node is ParserNode pn)
            foreach (var child in pn.Children)
                CollectLexerNodes(child, ruleName, result);
    }

    private static List<LexerNode> FindAllLexerNodesAny(ParseNode node)
    {
        var result = new List<LexerNode>();
        CollectAllLexerNodes(node, result);
        return result;
    }

    private static void CollectAllLexerNodes(ParseNode node, List<LexerNode> result)
    {
        if (node is LexerNode ln)
            result.Add(ln);

        if (node is ParserNode pn)
            foreach (var child in pn.Children)
                CollectAllLexerNodes(child, result);
    }

    private static ParserNode? FindFirstParserNode(ParseNode node, string ruleName)
    {
        if (node is ParserNode pn)
        {
            if (pn.Rule.Name == ruleName)
                return pn;
            foreach (var child in pn.Children)
            {
                var found = FindFirstParserNode(child, ruleName);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;
using Utils.Parser.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Covers grammar capabilities of the C-style token parser.
/// </summary>
[TestClass]
public class CStyleTokenParserTests
{
    /// <summary>
    /// Ensures that language keywords are emitted as dedicated keyword tokens.
    /// </summary>
    [TestMethod]
    public void TokenizeRecognizesKeywordTokens()
    {
        var parser = new CStyleTokenParser();
        var tokens = parser.Tokenize("if (true) return value;");

        Assert.AreEqual("IF", tokens[0].RuleName);
        Assert.AreEqual("TRUE", tokens[2].RuleName);
        Assert.AreEqual("RETURN", tokens[4].RuleName);
    }

    /// <summary>
    /// Ensures that interpolated strings are tokenized with dedicated interpolation pieces.
    /// </summary>
    [TestMethod]
    public void TokenizeInterpolatedStringUsesDedicatedTokens()
    {
        var parser = new CStyleTokenParser();
        var tokens = parser.Tokenize("$\"hello {name}\"");

        CollectionAssert.Contains(tokens.Select(t => t.RuleName).ToList(), "INTERPOLATED_STRING_START");
        CollectionAssert.Contains(tokens.Select(t => t.RuleName).ToList(), "INTERPOLATED_INTERPOLATION_START");
        CollectionAssert.Contains(tokens.Select(t => t.RuleName).ToList(), "INTERPOLATED_STRING_END");
    }

    /// <summary>
    /// Ensures that parser output keeps operator precedence in the parse tree shape.
    /// </summary>
    [TestMethod]
    public void ParseKeepsBinaryOperatorPrecedenceInParseTree()
    {
        var parser = new CStyleTokenParser();
        ParseNode root = parser.Parse("1 + 2 * 3");

        Assert.IsFalse(root is ErrorNode);
        Assert.IsTrue(ContainsBinaryOperator(root, "operation_plus", "+"));
        Assert.IsTrue(ContainsBinaryOperator(root, "operation_mul", "*"));
    }

    /// <summary>
    /// Ensures assignment grammar supports property, indexer, and declaration assignments.
    /// </summary>
    [TestMethod]
    public void ParseSupportsExtendedAssignmentTargets()
    {
        var parser = new CStyleTokenParser();

        ParseNode propertyAssign = parser.Parse("person.Name = value");
        ParseNode indexerAssign = parser.Parse("items[index] = value");
        ParseNode declarationAssign = parser.Parse("int total = 42");

        Assert.IsFalse(propertyAssign is ErrorNode);
        Assert.IsFalse(indexerAssign is ErrorNode);
        Assert.IsFalse(declarationAssign is ErrorNode);
    }

    /// <summary>
    /// Ensures identifier parts support chained member/indexer/invocation patterns.
    /// </summary>
    [TestMethod]
    public void ParseSupportsIdentifierPartChains()
    {
        var parser = new CStyleTokenParser();

        ParseNode chainedCall = parser.Parse("root.child[index](arg)");
        ParseNode indexedThenCall = parser.Parse("values[0](item)");

        Assert.IsFalse(chainedCall is ErrorNode);
        Assert.IsFalse(indexedThenCall is ErrorNode);
    }

    /// <summary>
    /// Ensures control-flow keyword grammars parse typical C-style instructions.
    /// </summary>
    [TestMethod]
    public void ParseSupportsIfForWhileAndDoWhileInstructions()
    {
        var parser = new CStyleTokenParser();

        ParseNode ifNode = parser.Parse("if (a < b) result = a else result = b");
        ParseNode forNode = parser.Parse("for (i = 0; i < 10; i = i + 1) total = total + i");
        ParseNode whileNode = parser.Parse("while (ready) counter = counter - 1");
        ParseNode doWhileNode = parser.Parse("do counter = counter + 1 while (counter < 10);");

        Assert.IsFalse(ifNode is ErrorNode);
        Assert.IsFalse(forNode is ErrorNode);
        Assert.IsFalse(whileNode is ErrorNode);
        Assert.IsFalse(doWhileNode is ErrorNode);
    }

    /// <summary>
    /// Ensures switch instructions with case/default sections are supported by the grammar.
    /// </summary>
    [TestMethod]
    public void ParseSupportsSwitchInstruction()
    {
        var parser = new CStyleTokenParser();

        ParseNode switchNode = parser.Parse("switch (value) { case 1: result = one case 2: result = two default: result = other }");

        Assert.IsFalse(switchNode is ErrorNode);
    }

    /// <summary>
    /// Ensures grammar supports var/type rules, using, try-catch and method/lambda declarations.
    /// </summary>
    [TestMethod]
    public void ParseSupportsTypeUsingTryCatchAndMethodLambdaRules()
    {
        var parser = new CStyleTokenParser();

        ParseNode varDeclaration = parser.Parse("var total = 42");
        ParseNode usingDirective = parser.Parse("using System.IO;");
        ParseNode usingInstruction = parser.Parse("using (resource) value = value + 1");
        ParseNode tryCatch = parser.Parse("try { value = call() } catch (Exception ex) { value = fallback }");
        ParseNode methodDeclaration = parser.Parse("public int Sum(int a, int b) { total = a + b }");
        ParseNode lambdaDeclaration = parser.Parse("(int x) => x + 1");

        Assert.IsFalse(varDeclaration is ErrorNode);
        Assert.IsFalse(usingDirective is ErrorNode);
        Assert.IsFalse(usingInstruction is ErrorNode);
        Assert.IsFalse(tryCatch is ErrorNode);
        Assert.IsFalse(methodDeclaration is ErrorNode);
        Assert.IsFalse(lambdaDeclaration is ErrorNode);
    }

    /// <summary>
    /// Checks whether the parse tree contains a binary rule node with a given operator token.
    /// </summary>
    /// <param name="node">Current parse node to inspect.</param>
    /// <param name="ruleName">Expected parser rule name.</param>
    /// <param name="operatorText">Expected operator token text.</param>
    /// <returns><c>true</c> when a matching rule/operator pair is found; otherwise <c>false</c>.</returns>
    private static bool ContainsBinaryOperator(ParseNode node, string ruleName, string operatorText)
    {
        if (node is ParserNode parserNode)
        {
            if (parserNode.Rule.Name == ruleName && parserNode.Children.Any(child => child is LexerNode lexerNode && lexerNode.Token.Text == operatorText))
            {
                return true;
            }

            foreach (ParseNode child in parserNode.Children)
            {
                if (ContainsBinaryOperator(child, ruleName, operatorText))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

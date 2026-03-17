using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

[TestClass]
public class BlockExpressionBuilderTests
{
    // ── CreateBlock — empty ─────────────────────────────────────────────────

    [TestMethod]
    public void CreateBlock_NoExpressions_ReturnsEmptyExpression()
    {
        var builder = new BlockExpressionBuilder();
        var result = builder.CreateBlock();

        Assert.AreEqual(ExpressionType.Default, result.NodeType);
        Assert.AreEqual(typeof(void), result.Type);
    }

    // ── CreateBlock — single expression, no variables ──────────────────────

    [TestMethod]
    public void CreateBlock_SingleExpressionNoVariables_ReturnsExpressionDirectly()
    {
        var builder = new BlockExpressionBuilder();
        var constant = Expression.Constant(42);
        builder.Add(constant);

        var result = builder.CreateBlock();

        // Should not wrap in a BlockExpression when there's just one expression and no variables
        Assert.IsNotInstanceOfType<BlockExpression>(result);
        Assert.AreSame(constant, result);
    }

    // ── CreateBlock — multiple expressions ─────────────────────────────────

    [TestMethod]
    public void CreateBlock_MultipleExpressions_ReturnsBlockExpression()
    {
        var builder = new BlockExpressionBuilder();
        builder.Add(Expression.Constant(1));
        builder.Add(Expression.Constant(2));

        var result = builder.CreateBlock();

        Assert.IsInstanceOfType<BlockExpression>(result);
    }

    // ── CreateBlock — variable deduplication ───────────────────────────────

    [TestMethod]
    public void CreateBlock_SameVariableInMultipleExpressions_DeclaredOnce()
    {
        var builder = new BlockExpressionBuilder();
        var x = builder.AddVariable(typeof(int), "x");

        // x appears in both expressions — should still be declared only once in the block
        builder.Add(Expression.Assign(x, Expression.Constant(1)));
        builder.Add(Expression.Add(x, Expression.Constant(2)));

        var result = builder.CreateBlock();

        Assert.IsInstanceOfType<BlockExpression>(result);
        var block = (BlockExpression)result;
        Assert.AreEqual(1, block.Variables.Count(v => v == x));
    }

    [TestMethod]
    public void CreateBlock_UnusedVariable_NotIncludedInBlock()
    {
        var builder = new BlockExpressionBuilder();
        var x = builder.AddVariable(typeof(int), "x");
        var y = builder.AddVariable(typeof(int), "y");

        // Only x is actually used
        builder.Add(Expression.Assign(x, Expression.Constant(1)));
        builder.Add(Expression.Add(x, Expression.Constant(2)));

        var block = (BlockExpression)builder.CreateBlock();

        // y is declared but never referenced, so it should not appear
        Assert.IsFalse(block.Variables.Any(v => v == y));
    }

    // ── GetVariable ─────────────────────────────────────────────────────────

    [TestMethod]
    public void GetVariable_ExistingName_ReturnsVariable()
    {
        var builder = new BlockExpressionBuilder();
        var x = builder.AddVariable(typeof(int), "x");

        var result = builder.GetVariable("x");

        Assert.AreSame(x, result);
    }

    [TestMethod]
    public void GetVariable_MissingName_ThrowsInvalidOperationException()
    {
        var builder = new BlockExpressionBuilder();

        Assert.ThrowsException<InvalidOperationException>(
            () => builder.GetVariable("missing"));
    }

    // ── GetOrCreateVariable ─────────────────────────────────────────────────

    [TestMethod]
    public void GetOrCreateVariable_ExistingVariable_ReturnsSameInstance()
    {
        var builder = new BlockExpressionBuilder();
        var x = builder.AddVariable(typeof(int), "x");

        var result = builder.GetOrCreateVariable(typeof(int), "x");

        Assert.AreSame(x, result);
    }

    [TestMethod]
    public void GetOrCreateVariable_NewVariable_CreatesIt()
    {
        var builder = new BlockExpressionBuilder();

        var result = builder.GetOrCreateVariable(typeof(string), "s");

        Assert.IsNotNull(result);
        Assert.AreEqual("s", result.Name);
        Assert.AreEqual(typeof(string), result.Type);
    }

    [TestMethod]
    public void GetOrCreateVariable_NewVariable_IsSubsequentlyRetrievable()
    {
        var builder = new BlockExpressionBuilder();
        var created = builder.GetOrCreateVariable(typeof(double), "d");

        var retrieved = builder.GetVariable("d");

        Assert.AreSame(created, retrieved);
    }
}

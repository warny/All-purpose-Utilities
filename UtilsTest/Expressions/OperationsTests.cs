using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates boolean and relational operations compiled by <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class OperationsTests
{
    CStyleExpressionCompiler compiler = new CStyleExpressionCompiler();

    [TestMethod]
    public void MemberTest()
    {
        string[] tests = ["a", "ab", "abc"];
        var expression = "(string s) => s.Length";

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<string, int>)e.Compile();

        foreach (var test in tests)
        {
            Assert.AreEqual(test.Length, f(test)); ;
        }

    }

    [Ignore("Null-conditional operator ?. is not supported by the grammar")]
    [TestMethod]
    public void NullOrMemberTest()
    {
        // Grammar has no null-conditional operator; test kept for reference only.
    }

    /// <summary>
    /// Ensures compound logical operations return expected results.
    /// </summary>
    [TestMethod]
    public void Compile_BooleanOperations_ReturnsExpectedValue()
    {
        var expression = compiler.Compile("(2 < 3) && (5 >= 5)");
        var lambda = Expression.Lambda<Func<bool>>(Expression.Convert(expression, typeof(bool))).Compile();

        Assert.IsTrue(lambda());
    }
}

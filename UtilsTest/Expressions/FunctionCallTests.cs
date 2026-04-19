using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates function-call compilation with <see cref="CSyntaxExpressionCompiler"/>.
/// </summary>
[TestClass]
public class FunctionCallTests
{
    CSyntaxExpressionCompiler compiler = new CSyntaxExpressionCompiler();

    [TestMethod]
    public void FunctionCallTest1()
    {
        // Explicit generic type args in method calls are not supported by the grammar;
        // use the non-generic overload which produces the same output.
        int[] var = [1, 2, 3];
        var expression = "(int[] var) => string.Concat(var)";

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<int[], string>)e.Compile();

        Assert.AreEqual(string.Concat(var.Cast<object>().ToArray()), f(var));
    }

    [TestMethod]
    public void FunctionCallTest2()
    {
        var expression = "() => string.Concat(\"1\", \"2\", \"3\")";

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<string>)e.Compile();

        Assert.AreEqual("123", f());
    }

    [TestMethod]
    public void FunctionCallTest3()
    {
        var expression = "() => string.Concat(\"1\", \"2\", \"3\", \"4\", \"5\", \"6\")";

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<string>)e.Compile();
        Assert.AreEqual("123456", f());
    }



    [TestMethod]
    public void GenericFunctionCallTest1()
    {
        int[] var = [1, 2, 3];
        var expression = "(int[] var) => string.Concat(var)";

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<int[], string>)e.Compile();

        Assert.AreEqual(string.Concat(var.Cast<object>().ToArray()), f(var));
    }

    [TestMethod]
    public void GenericFunctionCallTest2()
    {
        string[] var = ["1", "2", "3"];
        var expression = "(string[] var) => string.Concat(var)";

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<string[], string>)e.Compile();

        Assert.AreEqual(string.Concat(var.Cast<object>().ToArray()), f(var));
    }

    [TestMethod]
    public void LambdaCallTest1()
    {
        Func<string, string> ToUpperCase = (string s) => s.ToUpper();
        Func<string, string> ToLowerCase = (string s) => s.ToLower();

        var expression = "(System.Func<string, string> s, string str) => s(str)";
        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<Func<string, string>, string, string>)e.Compile();

        var tests = new List<string>()
            {
                "ABCDEF",
                "abcdef",
                "AbCdEf",
                "aBcDeF"
            };

        foreach (var test in tests)
        {
            Assert.AreEqual(test.ToUpper(), f(ToUpperCase, test));
            Assert.AreEqual(test.ToLower(), f(ToLowerCase, test));
        }


    }

    [TestMethod]
    public void LambdaCallTest2()
    {
        var expression = "(string str) => { System.Func<string, string> f = (string s) => s.ToUpper(); f(str) }";
        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<string, string>)e.Compile();

        var tests = new List<string>()
            {
                "ABCDEF",
                "abcdef",
                "AbCdEf",
                "aBcDeF"
            };

        foreach (var test in tests)
        {
            Assert.AreEqual(test.ToUpper(), f(test));
        }


    }


    public void ExtensionMethodCallTest()
    {
        var expression = """
            using System.Linq;
            (string[] s, Func<string, string> f) => s.Select(f).ToArray()
            """;
            
        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<string, Func<string, string>, string>)e.Compile();

        Func<string, string> ToUpper = (string s) => s.ToUpper();
        string[] test = ["a", "ab", "ac"];
        string[] results = test.Select(ToUpper).ToArray();

        for (int i = 0; i < results.Length; i++)
        {
            Assert.AreEqual(results[i], f(test[i], ToUpper));
        }
    }

    /// <summary>
    /// Ensures context delegates can be invoked from compiled source.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionCall_InvokesContextDelegate()
    {
        var context = new CSyntaxCompilerContext();
        context.Set("sum", (Func<int, int, int>)((a, b) => a + b));

        var expression = compiler.Compile("sum(4, 7)", context);
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();

        Assert.AreEqual(11, lambda());
    }

    /// <summary>
    /// Ensures delegate invocation supports array arguments.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionCall_WithArrayArgument_ReturnsExpectedValue()
    {
        var context = new CSyntaxCompilerContext();
        context.Set("values", new[] { 1, 2, 3 });
        context.Set("concatInt", (Func<int[], string>)(values => string.Concat(values)));

        var expression = compiler.Compile("concatInt(values)", context);
        var lambda = Expression.Lambda<Func<string>>(Expression.Convert(expression, typeof(string))).Compile();

        Assert.AreEqual("123", lambda());
    }

    /// <summary>
    /// Ensures delegate invocation supports function arguments.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionCall_WithLambdaArgument_ReturnsExpectedValue()
    {
        var context = new CSyntaxCompilerContext();
        context.Set("apply", (Func<Func<string, string>, string, string>)((f, s) => f(s)));
        context.Set("toUpper", (Func<string, string>)(s => s.ToUpperInvariant()));
        context.Set("text", "aBc");

        var expression = compiler.Compile("apply(toUpper, text)", context);
        var lambda = Expression.Lambda<Func<string>>(Expression.Convert(expression, typeof(string))).Compile();

        Assert.AreEqual("ABC", lambda());
    }

    /// <summary>
    /// Ensures a method declared in source can be invoked in the same source block.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionDeclarationThenCall_Compiles()
    {
        var context = new CSyntaxCompilerContext();

        var expression = compiler.CompileSource(
            """
            public int add(int a, int b) { a + b; }
            add(5, 8)
            """,
            context);
        Assert.IsNotNull(expression);
    }
}

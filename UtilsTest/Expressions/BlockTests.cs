using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates block-style expressions compiled by <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class BlockTests
{
    CStyleExpressionCompiler compiler = new CStyleExpressionCompiler();


    /// <summary>
    /// Ensures statement blocks return the last expression value.
    /// </summary>
    [TestMethod]
    public void Compile_BlockExpression_ReturnsLastValue()
    {
        var x = Expression.Variable(typeof(int), "x");
        var expression = compiler.Compile("{ x = 2; x + 3; }", new Dictionary<string, Expression> { ["x"] = x });
        var lambda = Expression.Lambda<Func<int>>(Expression.Block([x], Expression.Convert(expression, typeof(int)))).Compile();

        Assert.AreEqual(5, lambda());
    }

    [TestMethod]
    public void SimpleBlockTest1()
    {
        string[] tests = ["1", "2", "3"];
        var expression = "(string s) =>  { s; }";

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<string, string>)e.Compile();

        foreach (var test in tests)
        {
            Assert.AreEqual(test, f(test)); ;
        }
    }

    [TestMethod]
    public void SimpleBlockTest2()
    {
        string[] tests = ["1", "2", "3"];
        var expression =
            """
            (string s) => { 
                "2";  
                s; 
            }
            """;

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<string, string>)e.Compile();

        foreach (var test in tests)
        {
            Assert.AreEqual(test, f(test));
        }
    }

    [TestMethod]
    public void AssignTest()
    {
        var expression = "(string s) =>  { string inner=\"test\"; inner + s; }";

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<string, string>)e.Compile();

        string[] tests = ["1", "2", "3"];
        foreach (var test in tests)
        {
            Assert.AreEqual("test" + test, f(test));
        }
    }

    [TestMethod]
    public void WhileTest()
    {
        Random random = new Random();
        var expression = "(char c, int length) =>  { string result=\"\"; while (result.Length < length) { result += c.ToString(); }; result; }";

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<char, int, string>)e.Compile();

        char[] tests = ['a', 'b', 'c'];
        foreach (var test in tests)
        {
            var length = random.Next(5, 10);
            Assert.AreEqual(new string(test, length), f(test, length));
        }
    }

    [Ignore("'break' statement is not supported by the grammar")]
    [TestMethod]
    public void WhileIfBreakTest()
    {
        // Grammar has no break statement rule; test kept for reference only.
    }

    [TestMethod]
    public void ForTest()
    {
        Random random = new Random();
        var expression =
            """
            (char c, int length) => { 
                string result=""; 
                for (int i = 0; i < length; i++) { 
                    result += c.ToString(); 
                };
                result; 
            }
            """;

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<char, int, string>)e.Compile();

        char[] tests = ['a', 'b', 'c'];
        foreach (var test in tests)
        {
            var length = random.Next(5, 10);
            Assert.AreEqual(new string(test, length), f(test, length));
        }
    }

    [TestMethod]
    public void ForEachTest1()
    {
        Random random = new Random();
        var expression =
            """
            (int[] test) => { 
                int result=0; 
                foreach(int i in test) {
                    result += i;
                };
                result; 
            }
            """;

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<int[], int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            int[] test = new int[random.Next(5, 10)];
            for (int j = 0; j < test.Length; j++)
            {
                test[j] = random.Next(0, 100);
            }

            var length = random.Next(5, 10);
            Assert.AreEqual(test.Sum(), f(test));
        }
    }

    [TestMethod]
    public void ForEachTest2()
    {
        Random random = new Random();
        var expression =
            """
            using System.Collections;
            using System.Collections.Generic;

            (IEnumerable<int> test) => { 
                int result=0; 
                foreach(int i in test) {
                    result += i;
                };
                result; 
            }
            """;

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<IEnumerable<int>, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            int[] test = new int[random.Next(5, 10)];
            for (int j = 0; j < test.Length; j++)
            {
                test[j] = random.Next(0, 100);
            }

            var length = random.Next(5, 10);
            Assert.AreEqual(test.Sum(), f(test));
        }
    }

    [TestMethod]
    public void ForEachTest3()
    {
        Random random = new Random();
        var expression =
            """
            using System.Collections;
            using System.Collections.Generic;
            
                        (IEnumerable test) => { 
                int result=0; 
                foreach(int i in test) {
                    result += i;
                };
                result; 
            }
            """;

        var e = (LambdaExpression)compiler.Compile(expression);
        var f = (Func<IEnumerable, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            int[] test = new int[random.Next(5, 10)];
            for (int j = 0; j < test.Length; j++)
            {
                test[j] = random.Next(0, 100);
            }

            var length = random.Next(5, 10);
            Assert.AreEqual(test.Sum(), f(test));
        }
    }

}

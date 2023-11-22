using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

[TestClass]
public class BlockTests
{
    [TestMethod]
    public void SimpleBlockTest1()
    {
        string[] tests = ["1", "2", "3"];
        var expression = "(string s) =>  { s; }";

        var e = ExpressionParser.Parse(expression);
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

        var e = ExpressionParser.Parse(expression);
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

        var e = ExpressionParser.Parse(expression);
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

        var e = ExpressionParser.Parse(expression);
        var f = (Func<char, int, string>)e.Compile();

        char[] tests = ['a', 'b', 'c'];
        foreach (var test in tests)
        {
            var length = random.Next(5, 10);
            Assert.AreEqual(new string(test, length), f(test, length));
        }
    }

    [TestMethod]
    public void WhileIfBreakTest()
    {
        Random random = new();
        var expression = "(char c, int length) =>  { string result=\"\"; while (true) { if(result.Length >= length) break; else result += c.ToString(); }; result; }";

        var e = ExpressionParser.Parse(expression);
        var f = (Func<char, int, string>)e.Compile();

        char[] tests = ['a', 'b', 'c'];
        foreach (var test in tests)
        {
            var length = random.Next(5, 10);
            Assert.AreEqual(new string(test, length), f(test, length));
        }
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

        var e = ExpressionParser.Parse(expression);
        var f = (Func<char, int, string>)e.Compile();

        char[] tests = ['a', 'b', 'c'];
        foreach (var test in tests)
        {
            var length = random.Next(5, 10);
            Assert.AreEqual(new string(test, length), f(test, length));
        }
    }


}

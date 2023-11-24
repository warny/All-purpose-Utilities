using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

[TestClass]
public class FunctionCallTests
{
    [TestMethod]
    public void FunctionCallTest1()
    {
        int[] var = [1, 2, 3];
        var expression = "(int[] var) => string.Concat<int>(var)";

        var e = ExpressionParser.Parse(expression);
        var f = (Func<int[], string>)e.Compile();

        Assert.AreEqual(string.Concat<int>(var), f(var));
    }

    [TestMethod]
    public void FunctionCallTest2()
    {
        var expression = "() => string.Concat(\"1\", \"2\", \"3\")";

        var e = ExpressionParser.Parse(expression);
        var f = (Func<string>)e.Compile();

        Assert.AreEqual("123", f());
    }

    [TestMethod]
    public void FunctionCallTest3()
    {
        var expression = "() => string.Concat(\"1\", \"2\", \"3\", \"4\", \"5\", \"6\")";

        var e = ExpressionParser.Parse(expression);
        var f = (Func<string>)e.Compile();
        Assert.AreEqual("123456", f());
    }



    [TestMethod]
    public void GenericFunctionCallTest1()
    {
        int[] var = [1, 2, 3];
        var expression = "(int[] var) => string.Concat(var)";

        var e = ExpressionParser.Parse(expression);
        var f = (Func<int[], string>)e.Compile();

        Assert.AreEqual(string.Concat(var.Cast<object>().ToArray()), f(var));
    }

    [TestMethod]
    public void GenericFunctionCallTest2()
    {
        string[] var = ["1", "2", "3"];
        var expression = "(string[] var) => string.Concat(var)";

        var e = ExpressionParser.Parse(expression);
        var f = (Func<string[], string>)e.Compile();

        Assert.AreEqual(string.Concat(var.Cast<object>().ToArray()), f(var));
    }

    [TestMethod]
    public void LambdaCallTest1()
    {
        Func<string, string> ToUpperCase = (string s) => s.ToUpper();
        Func<string, string> ToLowerCase = (string s) => s.ToLower();

        var expression = "(System.Func<string, string> s, string str) => s(str)";
        var e = ExpressionParser.Parse(expression);
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
        var e = ExpressionParser.Parse(expression);
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
        var expression = "(string[] s, Func<string, string> f) => s.Select(f).ToArray()";
        var e = ExpressionParser.Parse(expression, ["System.Linq"]);
        var f = (Func<string, Func<string, string>, string>)e.Compile();

        Func<string, string> ToUpper = (string s) => s.ToUpper();
        string[] test = ["a", "ab", "ac"];
        string[] results = test.Select(ToUpper).ToArray();

        for (int i = 0; i < results.Length; i++)
        {
            Assert.AreEqual(results[i], f(test[i], ToUpper));
        }
    }


}


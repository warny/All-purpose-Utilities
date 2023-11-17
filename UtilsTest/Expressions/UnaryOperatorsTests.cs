using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

[TestClass]
public class UnaryOperatorsTests
{
    [TestMethod]
    public void PlusTest()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(int x) => +x");
        var f = (Func<int, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
            Assert.AreEqual(x, f(x));
        }
    }

    [TestMethod]
    public void MinusTest1()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(int x) => -x");
        var f = (Func<int, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
            Assert.AreEqual(-x, f(x));
        }
    }

    [TestMethod]
    public void MinusTest2()
    {
        var r = new Random();


        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
            var e = ExpressionParser.Parse($"-{x}");
            var f = (Func<int>)e.Compile();
            Assert.AreEqual(-x, f());
        }
    }

    [TestMethod]
    public void NotTest()
    {
        var e = ExpressionParser.Parse("(bool x) => !x");
        var f = (Func<bool, bool>)e.Compile();

        foreach (var x in new bool[] { true, false })
        {
            Assert.AreEqual(!x, f(x));
        }
    }

    [TestMethod]
    public void ComplementTest()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(int x) => ~x");
        var f = (Func<int, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
            Assert.AreEqual(~x, f(x));
        }
    }

    [TestMethod]
    public void SizeofTypeofTest()
    {
        Type[] types = [
            typeof(string),
            typeof(int),
            typeof(long),
            typeof(float),
            typeof(double),
            typeof(bool),
            typeof(byte),
            this.GetType()
        ];

        foreach (var type in types) {
            var TypeOf = (Func<Type>)ExpressionParser.Compile($"typeof({type.FullName})");

            Assert.AreEqual(type, TypeOf());
            if (type.IsValueType)
            {
                var SizeOf = (Func<int>)ExpressionParser.Compile($"sizeof({type.FullName})");
                Assert.AreEqual(Marshal.SizeOf(type), SizeOf());
            }
        }

    }

    [TestMethod]
    public void NewTest()
    {
        string[] tests = [
            "test1",
            "test2",
            "Test3",
            "TEST4",
            "tEST5"
        ];

        foreach (var test in tests)
        {
            var table = "new char[] { '" + string.Join("', '", test.ToCharArray()) + "' }";
            var e = ExpressionParser.Parse($"new string({table})");
            var f = (Func<string>)e.Compile();
            Assert.AreEqual(test, f());
        }
    }

    [TestMethod]
    public void CastTests()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(int x) => (double)x");
        var f = (Func<int, double>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
            Assert.AreEqual((double)x, f(x));
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.Collections;
using Utils.Mathematics;
using Utils.Objects;

namespace UtilsTest.Mathematics;

[TestClass]
public class MathExTests
{
    [TestMethod]
    public void IntBetweenTest()
    {
        int lowerBound = 5;
        int upperBound = 10;

        var tests = new (int value, bool resultII, bool resultEI, bool resultIE, bool resultEE)[] {
                (4, false, false, false, false),
                (5, true, false, true, false),
                (7, true, true, true, true),
                (9, true, true, true, true),
                (10, true, true, false, false),
                (11, false, false, false, false),
            };

        foreach (var test in tests)
        {
            Assert.AreEqual(test.resultII, test.value.Between(lowerBound, upperBound));
            Assert.AreEqual(test.resultEI, test.value.Between(lowerBound, upperBound, includeLowerBound: false));
            Assert.AreEqual(test.resultIE, test.value.Between(lowerBound, upperBound, includeUpperBound: false));
            Assert.AreEqual(test.resultEE, test.value.Between(lowerBound, upperBound, includeLowerBound: false, includeUpperBound: false));
        }
    }

    [TestMethod]
    public void DoubleBetweenTest()
    {
        double lowerBound = 5;
        double upperBound = 10;

        var tests = new (double value, bool resultII, bool resultEI, bool resultIE, bool resultEE)[] {
                (4, false, false, false, false),
                (5, true, false, true, false),
                (7, true, true, true, true),
                (9, true, true, true, true),
                (10, true, true, false, false),
                (11, false, false, false, false),
            };

        foreach (var test in tests)
        {
            Assert.AreEqual(test.resultII, test.value.Between(lowerBound, upperBound));
            Assert.AreEqual(test.resultEI, test.value.Between(lowerBound, upperBound, includeLowerBound: false));
            Assert.AreEqual(test.resultIE, test.value.Between(lowerBound, upperBound, includeUpperBound: false));
            Assert.AreEqual(test.resultEE, test.value.Between(lowerBound, upperBound, includeLowerBound: false, includeUpperBound: false));
        }
    }

    [TestMethod]
    public void RoundTest()
    {
        var tests = new (double number, double @base, double result)[]
        {
            (1, 2, 2),
            (2, 3, 3),
            (3, 2, 4),
            (3, 3, 3),
            (1.24, 0.5, 1),
            (1.25, 0.5, 1.5),
            (1.26, 0.5, 1.5),
            (1.74, 0.5, 1.5),
            (1.75, 0.5, 2),
            (1.76, 0.5, 2),

            (-1, 2, 0),
            (-2, 3, -3),
            (-3, 2, -2),
            (-3, 3, -3),
            (-1.24, 0.5, -1),
            (-1.25, 0.5, -1),
            (-1.26, 0.5, -1.5),
            (-1.74, 0.5, -1.5),
            (-1.75, 0.5, -1.5),
            (-1.76, 0.5, -2),
        };

        foreach (var test in tests)
        {
            var result = MathEx.Round(test.number, test.@base);
            Assert.AreEqual(test.result, result, $"Round({test.number}, {test.@base})");
        }
    }

    [TestMethod]
    public void FloorTest()
    {
        var tests = new (double number, double @base, double result)[]
        {
            (1, 2, 0),
            (2, 3, 0),
            (3, 2, 2),
            (3, 3, 3),
            (1.24, 0.5, 1),
            (1.25, 0.5, 1),
            (1.26, 0.5, 1),
            (1.74, 0.5, 1.5),
            (1.75, 0.5, 1.5),
            (1.76, 0.5, 1.5),

            (-1, 2, -2),
            (-2, 3, -3),
            (-3, 2, -4),
            (-3, 3, -3),
            (-1.24, 0.5, -1.5),
            (-1.25, 0.5, -1.5),
            (-1.26, 0.5, -1.5),
            (-1.74, 0.5, -2),
            (-1.75, 0.5, -2),
            (-1.76, 0.5, -2),
        };

        foreach (var test in tests)
        {
            var result = MathEx.Floor(test.number, test.@base);
            Assert.AreEqual(test.result, result, $"Floor({test.number}, {test.@base})");
        }
    }

    [TestMethod]
    public void CeilingTest()
    {
        var tests = new (double number, double @base, double result)[]
        {
            (1, 2, 2),
            (2, 3, 3),
            (3, 2, 4),
            (3, 3, 3),
            (1.24, 0.5, 1.5),
            (1.25, 0.5, 1.5),
            (1.26, 0.5, 1.5),
            (1.74, 0.5, 2),
            (1.75, 0.5, 2),
            (1.76, 0.5, 2),

            (-1, 2, 0),
            (-2, 3, 0),
            (-3, 2, -2),
            (-3, 3, -3),
            (-1.24, 0.5, -1),
            (-1.25, 0.5, -1),
            (-1.26, 0.5, -1),
            (-1.74, 0.5, -1.5),
            (-1.75, 0.5, -1.5),
            (-1.76, 0.5, -1.5),
        };

        foreach (var test in tests)
        {
            var result = MathEx.Ceiling(test.number, test.@base);
            Assert.AreEqual(test.result, result, $"Ceiling({test.number}, {test.@base})");
        }
    }


    [TestMethod]
    public void IsMultipleOfTest()
    {
        Assert.IsTrue(MathEx.IsMultipleOf(12, 4));
        Assert.IsFalse(MathEx.IsMultipleOf(13, 4));
        Assert.IsTrue(MathEx.IsMultipleOf(0, 5));
        Assert.IsTrue(MathEx.IsMultipleOf(-6, 3));
        Assert.IsFalse(MathEx.IsMultipleOf(-7, 3));
        Assert.ThrowsException<DivideByZeroException>(() => MathEx.IsMultipleOf(5, 0));
    }

    [TestMethod]
    public void GcdTest()
    {
        Assert.AreEqual(6, MathEx.Gcd(12, 18));
        Assert.AreEqual(1, MathEx.Gcd(7, 13));
        Assert.AreEqual(5, MathEx.Gcd(0, 5));
        Assert.AreEqual(5, MathEx.Gcd(5, 0));
        Assert.AreEqual(0, MathEx.Gcd(0, 0));
        Assert.AreEqual(4, MathEx.Gcd(-8, 4));
        Assert.AreEqual(4, MathEx.Gcd(8, -4));
        Assert.AreEqual(4, MathEx.Gcd(-8, -4));
    }

    [TestMethod]
    public void LcmTest()
    {
        Assert.AreEqual(36, MathEx.Lcm(12, 18));
        Assert.AreEqual(91, MathEx.Lcm(7, 13));
        Assert.AreEqual(0, MathEx.Lcm(0, 5));
        Assert.AreEqual(0, MathEx.Lcm(5, 0));
        Assert.AreEqual(12, MathEx.Lcm(-4, 6));
        Assert.AreEqual(12, MathEx.Lcm(4, -6));
    }

    [TestMethod]
    public void PascalTriangleTest()
    {
        var tests = new (int line, int[] values)[] {
            ( 3, new[] { 1,3,3,1 } ), // utilise le cache d'initialisation
            ( 8, new[] { 1, 8, 28, 56, 70, 56, 28, 8, 1, } ), // calcule la 8° ligne, met en cache la 7 et la 8
            ( 7, new[] { 1, 7, 21, 35, 35, 21, 7, 1, } ) // récupère le cache de la 7 calculé par la ligne précédente
        };

        var comparer = EnumerableEqualityComparer<int>.Default;

        foreach (var test in tests)
        {
            var result = MathEx.ComputePascalTriangleLine(test.line);
            Assert.IsTrue(comparer.Equals(test.values, result), $"{{ {string.Join(", ", result)} }} is different from {{ {string.Join(", ", test.values)} }} expected");
        }
    }
}

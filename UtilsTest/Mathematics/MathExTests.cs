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

    // ── IsPowerOfTwo ──────────────────────────────────────────────────────────

    [TestMethod]
    public void IsPowerOfTwo_PowersOfTwo_ReturnTrue()
    {
        Assert.IsTrue(MathEx.IsPowerOfTwo(1));
        Assert.IsTrue(MathEx.IsPowerOfTwo(2));
        Assert.IsTrue(MathEx.IsPowerOfTwo(4));
        Assert.IsTrue(MathEx.IsPowerOfTwo(1024));
    }

    [TestMethod]
    public void IsPowerOfTwo_NonPowers_ReturnFalse()
    {
        Assert.IsFalse(MathEx.IsPowerOfTwo(0));
        Assert.IsFalse(MathEx.IsPowerOfTwo(3));
        Assert.IsFalse(MathEx.IsPowerOfTwo(6));
        Assert.IsFalse(MathEx.IsPowerOfTwo(-2));
    }

    // ── Lerp ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Lerp_AtZero_ReturnsA()
    {
        Assert.AreEqual(1.0, MathEx.Lerp(1.0, 5.0, 0.0));
    }

    [TestMethod]
    public void Lerp_AtOne_ReturnsB()
    {
        Assert.AreEqual(5.0, MathEx.Lerp(1.0, 5.0, 1.0));
    }

    [TestMethod]
    public void Lerp_AtHalf_ReturnsMidpoint()
    {
        Assert.AreEqual(3.0, MathEx.Lerp(1.0, 5.0, 0.5));
    }

    [TestMethod]
    public void Lerp_Extrapolation_WorksBeyondRange()
    {
        Assert.AreEqual(9.0, MathEx.Lerp(1.0, 5.0, 2.0));  // t=2 → 1 + 2*(5-1) = 9
        Assert.AreEqual(-3.0, MathEx.Lerp(1.0, 5.0, -1.0)); // t=-1 → 1 + (-1)*(5-1) = -3
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

    // ── Min ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Min_ThreeValues_ReturnsSmallest()
    {
        Assert.AreEqual(1, MathEx.Min(3, 1, 2));
        Assert.AreEqual(1, MathEx.Min(1, 2, 3));
        Assert.AreEqual(1, MathEx.Min(2, 3, 1));
    }

    [TestMethod]
    public void Min_SingleValue_ReturnsThatValue()
    {
        Assert.AreEqual(42, MathEx.Min(42));
    }

    [TestMethod]
    public void Min_AllEqual_ReturnsValue()
    {
        Assert.AreEqual(5, MathEx.Min(5, 5, 5));
    }

    [TestMethod]
    public void Min_EmptyArray_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => MathEx.Min<int>());
    }

    [TestMethod]
    public void Min_WithComparer_UsesCustomOrder()
    {
        // Reverse comparer → Min returns the largest natural value
        var rev = Comparer<int>.Create((a, b) => b.CompareTo(a));
        Assert.AreEqual(9, MathEx.Min(rev, 3, 9, 5));
    }

    // ── Max ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Max_ThreeValues_ReturnsLargest()
    {
        Assert.AreEqual(3, MathEx.Max(3, 1, 2));
        Assert.AreEqual(3, MathEx.Max(1, 2, 3));
        Assert.AreEqual(3, MathEx.Max(2, 3, 1));
    }

    [TestMethod]
    public void Max_SingleValue_ReturnsThatValue()
    {
        Assert.AreEqual(42, MathEx.Max(42));
    }

    [TestMethod]
    public void Max_AllEqual_ReturnsValue()
    {
        Assert.AreEqual(5, MathEx.Max(5, 5, 5));
    }

    [TestMethod]
    public void Max_EmptyArray_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => MathEx.Max<int>());
    }

    [TestMethod]
    public void Max_WithComparer_UsesCustomOrder()
    {
        // Reverse comparer → Max returns the smallest natural value
        var rev = Comparer<int>.Create((a, b) => b.CompareTo(a));
        Assert.AreEqual(1, MathEx.Max(rev, 3, 9, 1));
    }

    // ── Clamp ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Clamp_ValueInRange_ReturnsSameValue()
    {
        Assert.AreEqual(5, MathEx.Clamp(5, 1, 10));
    }

    [TestMethod]
    public void Clamp_ValueBelowMin_ReturnsMin()
    {
        Assert.AreEqual(1, MathEx.Clamp(0, 1, 10));
    }

    [TestMethod]
    public void Clamp_ValueAboveMax_ReturnsMax()
    {
        Assert.AreEqual(10, MathEx.Clamp(20, 1, 10));
    }

    [TestMethod]
    public void Clamp_OnMinBoundary_ReturnsMin()
    {
        Assert.AreEqual(1, MathEx.Clamp(1, 1, 10));
    }

    [TestMethod]
    public void Clamp_OnMaxBoundary_ReturnsMax()
    {
        Assert.AreEqual(10, MathEx.Clamp(10, 1, 10));
    }

    [TestMethod]
    public void Clamp_MinGreaterThanMax_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => MathEx.Clamp(5, 10, 1));
    }

    [TestMethod]
    public void Clamp_WithComparer_UsesCustomOrder()
    {
        // Reverse comparer: "min"=9, "max"=1 in natural order means clamp(5) stays at 5
        var rev = Comparer<int>.Create((a, b) => b.CompareTo(a));
        Assert.AreEqual(5, MathEx.Clamp(5, 9, 1, rev));
        Assert.AreEqual(9, MathEx.Clamp(10, 9, 1, rev)); // 10 "below" min=9 → returns 9
        Assert.AreEqual(1, MathEx.Clamp(0, 9, 1, rev));  // 0 "above" max=1 → returns 1
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

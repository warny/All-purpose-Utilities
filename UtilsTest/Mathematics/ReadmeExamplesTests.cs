using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;
using Utils.Mathematics.Expressions;
using Utils.Mathematics.Fourier;
using Utils.Mathematics.LinearAlgebra;
// Fully qualify System.Numerics.Complex to avoid ambiguity with
// Utils.Mathematics.LinearAlgebra.Vector<T> vs System.Numerics.Vector<T>.
using Complex = System.Numerics.Complex;
// Fully qualify System.Array to avoid ambiguity with the UtilsTest.Array namespace.
using SysArray = System.Array;

namespace UtilsTest.Mathematics;

/// <summary>
/// Executable versions of the README examples for Utils.Mathematics, asserting the
/// mathematically expected results. These tests act as a compile-and-correctness guard:
/// if the API drifts, these tests break before the README becomes misleading
/// (see TODO-2026-07-11-pass6.md item #79).
/// </summary>
[TestClass]
public class ReadmeExamplesTests
{
    private const double Delta = 1e-9;

    // ── MathEx ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Readme_MathEx_ComputePascalTriangleLine()
    {
        int[] line = MathEx.ComputePascalTriangleLine(4);
        CollectionAssert.AreEqual(new[] { 1, 4, 6, 4, 1 }, line);
    }

    [TestMethod]
    public void Readme_MathEx_RoundToSignificantDigits()
    {
        Assert.AreEqual(123000000, (int)MathEx.RoundToSignificantDigits(123456789, 3));
        Assert.AreEqual(120000000, (int)MathEx.RoundToSignificantDigits(123456789, 2));
        Assert.AreEqual(100000000, (int)MathEx.RoundToSignificantDigits(123456789, 1));
        Assert.AreEqual(130, (int)MathEx.RoundToSignificantDigits(125, 2));
        Assert.AreEqual(120, (int)MathEx.RoundToSignificantDigits(124, 2));
        Assert.AreEqual(-123000000, (int)MathEx.RoundToSignificantDigits(-123456789, 3));
        Assert.AreEqual(0, (int)MathEx.RoundToSignificantDigits(0, 3));
    }

    [TestMethod]
    public void Readme_MathEx_Round()
    {
        Assert.AreEqual(1200, MathEx.Round(1234, 100));
        Assert.AreEqual(1300, MathEx.Round(1250, 100));
        Assert.AreEqual(1200, MathEx.Floor(1234, 100));
        Assert.AreEqual(1300, MathEx.Ceiling(1201, 100));
    }

    [TestMethod]
    public void Readme_MathEx_Mod()
    {
        Assert.AreEqual(2, MathEx.Mod(-1, 3));
    }

    [TestMethod]
    public void Readme_MathEx_GcdLcm()
    {
        Assert.AreEqual(4, MathEx.Gcd(12, 8));
        Assert.AreEqual(12, MathEx.Lcm(4, 6));
    }

    // ── FFT ──────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Readme_Fft_ForwardTransform()
    {
        Complex[] signal = [1, 1, 0, 0, 0, 0, 0, 0]; // 8 samples
        FastFourierTransform.Transform(signal);
        // DC bin magnitude = sum of all samples = 2
        Assert.AreEqual(2.0, signal[0].Magnitude, Delta);
    }

    [TestMethod]
    public void Readme_Fft_GetFrequencies()
    {
        Complex[] signal = [1, 1, 0, 0, 0, 0, 0, 0]; // 8 samples, sampleRate = 8
        FastFourierTransform.Transform(signal);
        double sampleRate = 8.0;
        double[] frequencies = signal.GetFrequencies(sampleRate);
        // GetFrequencies returns N/2 = 4 bins: 0, 1, 2, 3 Hz
        Assert.AreEqual(4, frequencies.Length);
        Assert.AreEqual(0.0, frequencies[0], Delta);
        Assert.AreEqual(1.0, frequencies[1], Delta);
        Assert.AreEqual(2.0, frequencies[2], Delta);
        Assert.AreEqual(3.0, frequencies[3], Delta);
    }

    [TestMethod]
    public void Readme_Fft_SubRangeViaSlice()
    {
        // README: extract sub-range, transform, copy back
        Complex[] buffer = new Complex[16];
        for (int i = 0; i < 16; i++) buffer[i] = new Complex(1, 0);
        Complex[] subRange = buffer[4..12]; // length 8
        FastFourierTransform.Transform(subRange);
        SysArray.Copy(subRange, 0, buffer, 4, subRange.Length);
        // DC of the 8-sample slice = 8 (sum of eight 1s)
        Assert.AreEqual(8.0, buffer[4].Magnitude, Delta);
    }

    // ── Symbolic expressions ─────────────────────────────────────────────────

    [TestMethod]
    public void Readme_Symbolic_Derivative_Double()
    {
        // f(x) = x³ - 2x  →  f'(x) = 3x² - 2  →  f'(3) = 25
        Expression<Func<double, double>> f = x => x * x * x - 2 * x;
        LambdaExpression df = f.Derivate();
        double value = (double)df.Compile().DynamicInvoke(3.0)!;
        Assert.AreEqual(25.0, value, Delta);
    }

    [TestMethod]
    public void Readme_Symbolic_Integral_Double()
    {
        // F(x) = ∫3·x² dx = x³  →  F(2) = 8
        // Use Math.Pow(x,2) so the engine sees a Power node (x*x as Multiply is unsupported).
        Expression<Func<double, double>> f = x => 3 * Math.Pow(x, 2);
        LambdaExpression F = f.Integrate();
        double value = (double)F.Compile().DynamicInvoke(2.0)!;
        Assert.AreEqual(8.0, value, Delta);
    }

    [TestMethod]
    public void Readme_Symbolic_Derivative_Float()
    {
        // f(x) = x² + x  →  f'(x) = 2x + 1  →  f'(3) = 7
        Expression<Func<float, float>> f = x => x * x + x;
        LambdaExpression df = f.Derivate<float>("x");
        float d = (float)df.Compile().DynamicInvoke(3f)!;
        Assert.AreEqual(7f, d, 1e-5f);
    }

    // ── Vector ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Readme_Vector_ConstructionAndProperties()
    {
        var v = new Vector<double>(3.0, 4.0);
        Assert.AreEqual(5.0, v.Norm, Delta);
        Assert.AreEqual(2, v.Dimension);
        Assert.AreEqual(3.0, v[0], Delta);
    }

    [TestMethod]
    public void Readme_Vector_Arithmetic()
    {
        var a = new Vector<double>(1.0, 2.0, 3.0);
        var b = new Vector<double>(4.0, 5.0, 6.0);

        Vector<double> sum = a + b;
        Assert.AreEqual(5.0, sum[0], Delta);
        Assert.AreEqual(7.0, sum[1], Delta);
        Assert.AreEqual(9.0, sum[2], Delta);

        Vector<double> diff = b - a;
        Assert.AreEqual(3.0, diff[0], Delta);
        Assert.AreEqual(3.0, diff[1], Delta);
        Assert.AreEqual(3.0, diff[2], Delta);

        Vector<double> scaled = 2.0 * a;
        Assert.AreEqual(2.0, scaled[0], Delta);
        Assert.AreEqual(4.0, scaled[1], Delta);
        Assert.AreEqual(6.0, scaled[2], Delta);

        double dot = a * b; // 1*4 + 2*5 + 3*6 = 32
        Assert.AreEqual(32.0, dot, Delta);
    }

    [TestMethod]
    public void Readme_Vector_Normalize()
    {
        var v = new Vector<double>(3.0, 0.0, 4.0);
        Vector<double> unit = v.Normalize();
        Assert.AreEqual(0.6, unit[0], Delta);
        Assert.AreEqual(0.0, unit[1], Delta);
        Assert.AreEqual(0.8, unit[2], Delta);
        Assert.AreEqual(1.0, unit.Norm, Delta);
    }

    [TestMethod]
    public void Readme_Vector_CrossProduct()
    {
        var u = new Vector<double>(1.0, 0.0, 0.0);
        var v = new Vector<double>(0.0, 1.0, 0.0);
        Vector<double> normal = Vector<double>.Product(u, v); // (0, 0, 1)
        Assert.AreEqual(0.0, normal[0], Delta);
        Assert.AreEqual(0.0, normal[1], Delta);
        Assert.AreEqual(1.0, normal[2], Delta);
    }

    [TestMethod]
    public void Readme_Vector_Barycenter()
    {
        var a = new Vector<double>(0.0, 0.0);
        var b = new Vector<double>(4.0, 0.0);
        var c = new Vector<double>(0.0, 4.0);

        var (_, center) = Vector<double>.ComputeBarycenter(a, b, c);
        Assert.AreEqual(4.0 / 3.0, center[0], Delta);
        Assert.AreEqual(4.0 / 3.0, center[1], Delta);
    }

    [TestMethod]
    public void Readme_Vector_WeightedBarycenter()
    {
        var a = new Vector<double>(0.0, 0.0);
        var b = new Vector<double>(4.0, 0.0);
        var c = new Vector<double>(0.0, 4.0);

        // weights: 1, 2, 3 → total = 6
        // weighted center = (1*(0,0) + 2*(4,0) + 3*(0,4)) / 6 = (8/6, 12/6) = (4/3, 2)
        var (totalWeight, weighted) = Vector<double>.ComputeBarycenter(
            wp => wp.w, wp => wp.v,
            (w: 1.0, v: a), (w: 2.0, v: b), (w: 3.0, v: c));

        Assert.AreEqual(6.0, totalWeight, Delta);
        Assert.AreEqual(8.0 / 6.0, weighted[0], Delta);
        Assert.AreEqual(12.0 / 6.0, weighted[1], Delta);
    }

    // ── Matrix ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Readme_Matrix_Arithmetic()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
        var b = new Matrix<double>(new double[,] { { 5, 6 }, { 7, 8 } });

        Matrix<double> sum = a + b;
        Assert.AreEqual(6.0, sum[0, 0], Delta);
        Assert.AreEqual(12.0, sum[1, 1], Delta);

        Matrix<double> product = a * b; // [[19,22],[43,50]]
        Assert.AreEqual(19.0, product[0, 0], Delta);
        Assert.AreEqual(22.0, product[0, 1], Delta);
        Assert.AreEqual(43.0, product[1, 0], Delta);
        Assert.AreEqual(50.0, product[1, 1], Delta);

        var v = new Vector<double>(1.0, 0.0);
        Vector<double> transformed = a * v; // (1, 3)
        Assert.AreEqual(1.0, transformed[0], Delta);
        Assert.AreEqual(3.0, transformed[1], Delta);
    }

    [TestMethod]
    public void Readme_Matrix_Determinant()
    {
        var m = new Matrix<double>(new double[,] { { 2, 1 }, { 5, 3 } });
        Assert.AreEqual(1.0, m.Determinant, Delta);
    }

    [TestMethod]
    public void Readme_Matrix_Inversion()
    {
        var m = new Matrix<double>(new double[,] { { 2, 1 }, { 5, 3 } });
        Matrix<double> inv = m.Invert();
        Assert.AreEqual(3.0, inv[0, 0], Delta);
        Assert.AreEqual(-1.0, inv[0, 1], Delta);
        Assert.AreEqual(-5.0, inv[1, 0], Delta);
        Assert.AreEqual(2.0, inv[1, 1], Delta);
    }

    [TestMethod]
    public void Readme_Matrix_LuDecomposition_SatisfiesPTimesAEqualsLTimesU()
    {
        var m = new Matrix<double>(new double[,] { { 2, 1 }, { 5, 3 } });
        var (L, U, P) = m.DiagonalizeLU();

        // P * m == L * U
        Matrix<double> lhs = P * m;
        Matrix<double> rhs = L * U;
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 2; col++)
                Assert.AreEqual(lhs[row, col], rhs[row, col], Delta, $"[{row},{col}]");
    }

    [TestMethod]
    public void Readme_Matrix_Properties()
    {
        var m = new Matrix<double>(new double[,] { { 2, 1 }, { 5, 3 } });
        Assert.AreEqual(2, m.Rows);
        Assert.AreEqual(2, m.Columns);
        Assert.IsTrue(m.IsSquare);
        Assert.IsFalse(m.IsDiagonal);
    }

    // ── MatrixTransformations ─────────────────────────────────────────────────

    [TestMethod]
    public void Readme_Transformations_IdentityAndDiagonal()
    {
        Matrix<double> I = MatrixTransformations.Identity<double>(3);
        Assert.AreEqual(3, I.Rows);
        Assert.IsTrue(I.IsIdentity);

        Matrix<double> D = MatrixTransformations.Diagonal<double>(2.0, 3.0);
        Assert.AreEqual(2.0, D[0, 0], Delta);
        Assert.AreEqual(3.0, D[1, 1], Delta);
    }

    [TestMethod]
    public void Readme_Transformations_ApplyToPoint()
    {
        // 2D scale (2x, 3y) → rotate π/4 → translate (5, 10) applied to (1, 0)
        Matrix<double> scale = MatrixTransformations.Scaling<double>(2.0, 3.0);
        Matrix<double> trans = MatrixTransformations.Translation<double>(5.0, 10.0);
        Matrix<double> rot   = MatrixTransformations.Rotation<double>(Math.PI / 4);
        Matrix<double> combined = trans * rot * scale;

        var point = new Vector<double>(1.0, 0.0);
        Vector<double> inHom      = point.ToNormalSpace();       // (1, 0, 1)
        Vector<double> transformed = combined * inHom;
        Vector<double> result      = transformed.FromNormalSpace();

        // After scale: (2, 0). After rot π/4: (√2, √2). After trans: (√2+5, √2+10).
        double expected0 = Math.Sqrt(2) + 5;
        double expected1 = Math.Sqrt(2) + 10;
        Assert.AreEqual(expected0, result[0], 1e-9);
        Assert.AreEqual(expected1, result[1], 1e-9);
    }

    // ── Polynomial ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Readme_Polynomial_ArithmeticAndDerivation()
    {
        // p = x² - 2  (coefficients ascending: -2, 0, 1)
        var p = new Polynomial<double>(-2.0, 0.0, 1.0);
        Assert.AreEqual(2, p.Degree);

        // p' = 2x (coefficients: 0, 2)
        Polynomial<double> dp = p.Derive();
        Assert.AreEqual(1, dp.Degree);
        Assert.AreEqual(0.0, dp[0], Delta);
        Assert.AreEqual(2.0, dp[1], Delta);

        // 3x² - 2 = p + 2x²  (2x² = Polynomial(0, 0, 2))
        var q = new Polynomial<double>(0.0, 0.0, 2.0);
        Polynomial<double> sum = p + q; // -2 + 3x²
        Assert.AreEqual(2, sum.Degree);
        Assert.AreEqual(-2.0, sum[0], Delta);
        Assert.AreEqual(3.0, sum[2], Delta);
    }

    // ── Line ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Readme_Line_DistanceToPoint()
    {
        var origin    = new Vector<double>(0.0, 0.0, 0.0);
        var direction = new Vector<double>(1.0, 0.0, 0.0); // along X axis
        var line      = new Line<double>(origin, direction);

        var point = new Vector<double>(3.0, 4.0, 0.0);
        double dist = line.DistanceTo(point); // perpendicular distance = 4
        Assert.AreEqual(4.0, dist, Delta);
    }
}

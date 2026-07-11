using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Mathematics.LinearAlgebra;

[TestClass]
public class MatrixQRTests
{
    private const double Tol = 1e-9;

    [TestMethod]
    public void DecomposeQR_2x2_QOrthogonal()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
        var (q, _) = a.DecomposeQR();
        // Qt * Q should be identity
        var qtq = q.Transpose() * q;
        Assert.AreEqual(1.0, qtq[0, 0], Tol);
        Assert.AreEqual(0.0, qtq[0, 1], Tol);
        Assert.AreEqual(0.0, qtq[1, 0], Tol);
        Assert.AreEqual(1.0, qtq[1, 1], Tol);
    }

    [TestMethod]
    public void DecomposeQR_2x2_RUpperTriangular()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
        var (_, r) = a.DecomposeQR();
        Assert.AreEqual(0.0, r[1, 0], Tol);
    }

    [TestMethod]
    public void DecomposeQR_2x2_ProductEqualsOriginal()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
        var (q, r) = a.DecomposeQR();
        var product = q * r;
        Assert.AreEqual(a[0, 0], product[0, 0], Tol);
        Assert.AreEqual(a[0, 1], product[0, 1], Tol);
        Assert.AreEqual(a[1, 0], product[1, 0], Tol);
        Assert.AreEqual(a[1, 1], product[1, 1], Tol);
    }

    [TestMethod]
    public void DecomposeQR_3x3_ProductEqualsOriginal()
    {
        var a = new Matrix<double>(new double[,]
        {
            { 1, -1, 4 },
            { 1,  4, -2 },
            { 1,  4, 2 }
        });
        var (q, r) = a.DecomposeQR();
        var product = q * r;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.AreEqual(a[i, j], product[i, j], Tol);
    }

    [TestMethod]
    public void DecomposeQR_MoreRowsThanColumns_Works()
    {
        var a = new Matrix<double>(new double[,]
        {
            { 1, 2 },
            { 3, 4 },
            { 5, 6 }
        });
        var (q, r) = a.DecomposeQR();
        var product = q * r;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 2; j++)
                Assert.AreEqual(a[i, j], product[i, j], Tol);
    }

    [TestMethod]
    public void DecomposeQR_MoreColumnsThanRows_Throws()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        Assert.ThrowsException<InvalidOperationException>(() => a.DecomposeQR());
    }

    [TestMethod]
    public void DecomposeQR_SingularMatrix_StillProducesValidFactorization()
    {
        // Regression: the previous Gram-Schmidt-based implementation rejected rank-deficient
        // columns outright. Householder reflections remain well-defined for a singular matrix - the
        // dependent column simply yields a (numerically) zero R diagonal entry - which is required
        // for ComputeEigenvalues to reach singular symmetric matrices at all.
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 2, 4 } });
        var (q, r) = a.DecomposeQR();

        var qtq = q.Transpose() * q;
        Assert.AreEqual(1.0, qtq[0, 0], Tol);
        Assert.AreEqual(0.0, qtq[0, 1], Tol);
        Assert.AreEqual(0.0, qtq[1, 0], Tol);
        Assert.AreEqual(1.0, qtq[1, 1], Tol);

        Assert.AreEqual(0.0, r[1, 0], Tol);

        var product = q * r;
        Assert.AreEqual(a[0, 0], product[0, 0], Tol);
        Assert.AreEqual(a[0, 1], product[0, 1], Tol);
        Assert.AreEqual(a[1, 0], product[1, 0], Tol);
        Assert.AreEqual(a[1, 1], product[1, 1], Tol);
    }

    [TestMethod]
    public void DecomposeQR_ZeroMatrix_ProducesOrthogonalQAndZeroR()
    {
        // The most degenerate rank-deficient case: every column dependent (zero). Q must still be
        // orthogonal (falls back to an arbitrary orthonormal basis) and R must be exactly zero.
        var a = Matrix<double>.Zero(3, 2);
        var (q, r) = a.DecomposeQR();

        var qtq = q.Transpose() * q;
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                Assert.AreEqual(i == j ? 1.0 : 0.0, qtq[i, j], Tol, $"[{i},{j}]");

        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                Assert.AreEqual(0.0, r[i, j], Tol, $"[{i},{j}]");
    }
}

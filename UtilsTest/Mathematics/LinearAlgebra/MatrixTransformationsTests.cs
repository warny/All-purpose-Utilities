using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Mathematics.LinearAlgebra;

[TestClass]
public class MatrixTransformationsTests
{
    private const double Delta = 1e-10;

    // ── Identity ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Identity_AppliedToVector_ReturnsUnchangedVector()
    {
        var m = MatrixTransformations.Identity<double>(3);
        var v = new Vector<double>(2d, 5d, 1d);
        var result = m * v;
        Assert.AreEqual(2d, result[0], Delta);
        Assert.AreEqual(5d, result[1], Delta);
        Assert.AreEqual(1d, result[2], Delta);
    }

    [TestMethod]
    public void Identity_IsIdentityMatrix()
    {
        var m = MatrixTransformations.Identity<double>(4);
        Assert.IsTrue(m.IsIdentity);
        Assert.AreEqual(1d, m.Determinant, Delta);
    }

    // ── Scaling ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void Scaling_ScalesHomogeneousVector()
    {
        // Scaling(2, 3) produces a 3×3 matrix (homogeneous 2D).
        var m = MatrixTransformations.Scaling<double>(2d, 3d);
        Assert.AreEqual(3, m.Rows);
        Assert.AreEqual(3, m.Columns);

        // Apply to homogeneous point (x=1, y=1, w=1).
        var v = new Vector<double>(1d, 1d, 1d);
        var result = m * v;
        Assert.AreEqual(2d, result[0], Delta);
        Assert.AreEqual(3d, result[1], Delta);
        Assert.AreEqual(1d, result[2], Delta);
    }

    [TestMethod]
    public void Scaling_UniformScale_DeterminantIsProduct()
    {
        var m = MatrixTransformations.Scaling<double>(2d, 3d);
        // Last homogeneous row: [0,0,1] → det = 2*3*1 = 6
        Assert.AreEqual(6d, m.Determinant, Delta);
    }

    // ── Translation ──────────────────────────────────────────────────────────

    [TestMethod]
    public void Translation_TranslatesHomogeneousPoint()
    {
        // The library uses standard matrix-times-column-vector multiplication
        // (Matrix<T> * Vector<T> computes result[row] = sum_col m[row,col] * v[col]), so
        // translation coefficients must live in the last COLUMN, not the last row.
        var m = MatrixTransformations.Translation<double>(5d, 7d);
        Assert.AreEqual(3, m.Rows);
        Assert.AreEqual(3, m.Columns);

        // Point (1, 2, 1) in homogeneous coordinates must translate to (1+5, 2+7, 1).
        var v = new Vector<double>(1d, 2d, 1d);
        var result = m * v;

        Assert.AreEqual(6d, result[0], Delta);
        Assert.AreEqual(9d, result[1], Delta);
        Assert.AreEqual(1d, result[2], Delta);

        Assert.AreEqual(1d, m[0, 0], Delta);
        Assert.AreEqual(1d, m[1, 1], Delta);
        Assert.AreEqual(1d, m[2, 2], Delta);
        Assert.AreEqual(5d, m[0, 2], Delta);
        Assert.AreEqual(7d, m[1, 2], Delta);
        Assert.AreEqual(0d, m[2, 0], Delta);
        Assert.AreEqual(0d, m[2, 1], Delta);
    }

    [TestMethod]
    public void Translation_3D_TranslatesHomogeneousPoint()
    {
        var m = MatrixTransformations.Translation<double>(1d, -2d, 3d);
        Assert.AreEqual(4, m.Rows);
        Assert.AreEqual(4, m.Columns);

        var v = new Vector<double>(10d, 10d, 10d, 1d);
        var result = m * v;

        Assert.AreEqual(11d, result[0], Delta);
        Assert.AreEqual(8d, result[1], Delta);
        Assert.AreEqual(13d, result[2], Delta);
        Assert.AreEqual(1d, result[3], Delta);
    }

    [TestMethod]
    public void Translation_ComposedWithItself_AddsOffsets()
    {
        // Composition must remain consistent with the column-vector convention: applying two
        // translations in sequence via matrix multiplication should add their offsets.
        var m1 = MatrixTransformations.Translation<double>(2d, 0d);
        var m2 = MatrixTransformations.Translation<double>(0d, 3d);
        var composed = m1 * m2;

        var v = new Vector<double>(0d, 0d, 1d);
        var result = composed * v;

        Assert.AreEqual(2d, result[0], Delta);
        Assert.AreEqual(3d, result[1], Delta);
        Assert.AreEqual(1d, result[2], Delta);
    }

    // ── Rotation ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Rotation_90Degrees_RotatesXAxisToYAxis()
    {
        double angle = Math.PI / 2;
        var m = MatrixTransformations.Rotation<double>(angle);
        Assert.AreEqual(3, m.Rows); // 2D: 3×3 homogeneous

        // Apply to (1, 0, 1) → should give approximately (0, 1, 1)
        var v = new Vector<double>(1d, 0d, 1d);
        var result = m * v;
        Assert.AreEqual(0d, result[0], Delta);
        Assert.AreEqual(1d, result[1], Delta);
        Assert.AreEqual(1d, result[2], Delta);
    }

    [TestMethod]
    public void Rotation_360Degrees_IsIdentity()
    {
        var m = MatrixTransformations.Rotation<double>(2 * Math.PI);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.AreEqual(i == j ? 1d : 0d, m[i, j], Delta, $"[{i},{j}]");
    }
}

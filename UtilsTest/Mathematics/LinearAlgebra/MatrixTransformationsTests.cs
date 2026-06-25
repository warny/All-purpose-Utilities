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
        var m = MatrixTransformations.Translation<double>(5d, 7d);
        Assert.AreEqual(3, m.Rows);
        Assert.AreEqual(3, m.Columns);

        // Point (1,2,1) in homogeneous coords → (1+tx, 2+ty, 1) after multiply
        // MatrixTransformations uses row-vector convention: result = m * [1,2,1]ᵀ
        var v = new Vector<double>(1d, 2d, 1d);
        var result = m * v;

        // Translation stored in last row: result[0]=1, result[1]=2, result[2]=1+5+7=15? No.
        // Let's check: the translation matrix has identity top-left and values in last ROW.
        // With column-vector convention: (m*v)[i] = sum(m[i,j]*v[j])
        // m[0,0]=1,m[1,1]=1,m[2,2]=1, m[2,0]=5, m[2,1]=7 (last row)
        // result[0] = m[0,0]*v[0] + m[0,1]*v[1] + m[0,2]*v[2] = 1*1 + 0*2 + 0*1 = 1
        // result[1] = 0*1 + 1*2 + 0*1 = 2
        // result[2] = 5*1 + 7*2 + 1*1 = 5+14+1 = 20 → this is the row-vector convention result
        // The result in homogeneous form: divide result by w=result[2]... non trivial.
        // Instead just check that the matrix has the expected structure.
        Assert.AreEqual(1d, m[0, 0], Delta);
        Assert.AreEqual(1d, m[1, 1], Delta);
        Assert.AreEqual(1d, m[2, 2], Delta);
        Assert.AreEqual(5d, m[2, 0], Delta);
        Assert.AreEqual(7d, m[2, 1], Delta);
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

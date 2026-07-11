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

    // ── Skew ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Skew_2D_ProducesHandCalculatedOffDiagonalCoefficients()
    {
        // Base dimension 2 needs d*(d-1) = 2 angles: one per off-diagonal position of the 2x2 block.
        double a0 = Math.Atan(2d);
        double a1 = Math.Atan(3d);
        var m = MatrixTransformations.Skew<double>(a0, a1);

        Assert.AreEqual(3, m.Rows);
        Assert.AreEqual(3, m.Columns);

        // Row-major order skipping the diagonal: [0,1] gets the first angle, [1,0] the second.
        Assert.AreEqual(2d, m[0, 1], Delta);
        Assert.AreEqual(3d, m[1, 0], Delta);

        // Diagonal and homogeneous row/column must remain untouched.
        Assert.AreEqual(1d, m[0, 0], Delta);
        Assert.AreEqual(1d, m[1, 1], Delta);
        Assert.AreEqual(1d, m[2, 2], Delta);
        Assert.AreEqual(0d, m[0, 2], Delta);
        Assert.AreEqual(0d, m[1, 2], Delta);
        Assert.AreEqual(0d, m[2, 0], Delta);
        Assert.AreEqual(0d, m[2, 1], Delta);
    }

    [TestMethod]
    public void Skew_3D_ProducesHandCalculatedOffDiagonalCoefficients()
    {
        // Base dimension 3 needs d*(d-1) = 6 angles.
        double[] tans = { 1d, 2d, 3d, 4d, 5d, 6d };
        double[] angles = System.Array.ConvertAll(tans, Math.Atan);
        var m = MatrixTransformations.Skew<double>(angles);

        Assert.AreEqual(4, m.Rows);
        Assert.AreEqual(4, m.Columns);

        // Row-major order skipping the diagonal for each row:
        // row 0 -> columns 1,2 ; row 1 -> columns 0,2 ; row 2 -> columns 0,1.
        Assert.AreEqual(1d, m[0, 1], Delta);
        Assert.AreEqual(2d, m[0, 2], Delta);
        Assert.AreEqual(3d, m[1, 0], Delta);
        Assert.AreEqual(4d, m[1, 2], Delta);
        Assert.AreEqual(5d, m[2, 0], Delta);
        Assert.AreEqual(6d, m[2, 1], Delta);

        for (int i = 0; i < 3; i++)
            Assert.AreEqual(1d, m[i, i], Delta, $"diagonal[{i}]");

        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(i == 3 ? 1d : 0d, m[3, i], Delta, $"homogeneous row [3,{i}]");
            Assert.AreEqual(i == 3 ? 1d : 0d, m[i, 3], Delta, $"homogeneous column [{i},3]");
        }
    }

    [TestMethod]
    public void Skew_InvalidAngleCount_Throws()
    {
        // 3 is not d*(d-1) for any integer d (0, 2, 6, 12, ...).
        Assert.ThrowsException<ArgumentException>(() => MatrixTransformations.Skew<double>(1d, 2d, 3d));
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

    // ── Transform ────────────────────────────────────────────────────────────

    [TestMethod]
    public void Transform_2D_ProducesHandCalculatedAffineBlockAndHomogeneousLastRow()
    {
        // Row-major [a, b, tx, c, d, ty]: row0 = [a,b,tx], row1 = [c,d,ty].
        var m = MatrixTransformations.Transform<double>(2d, 3d, 5d, 4d, 6d, 7d);

        Assert.AreEqual(3, m.Rows);
        Assert.AreEqual(3, m.Columns);

        Assert.AreEqual(2d, m[0, 0], Delta);
        Assert.AreEqual(3d, m[0, 1], Delta);
        Assert.AreEqual(5d, m[0, 2], Delta);
        Assert.AreEqual(4d, m[1, 0], Delta);
        Assert.AreEqual(6d, m[1, 1], Delta);
        Assert.AreEqual(7d, m[1, 2], Delta);

        // The final row must remain the homogeneous row, not be overwritten with supplied values.
        Assert.AreEqual(0d, m[2, 0], Delta);
        Assert.AreEqual(0d, m[2, 1], Delta);
        Assert.AreEqual(1d, m[2, 2], Delta);
    }

    [TestMethod]
    public void Transform_2D_MatchesManualMultiplicationOnHomogeneousPoint()
    {
        // Affine map: x' = 2x + 3y + 5, y' = 4x + 6y + 7.
        var m = MatrixTransformations.Transform<double>(2d, 3d, 5d, 4d, 6d, 7d);
        var v = new Vector<double>(1d, 1d, 1d);
        var result = m * v;

        Assert.AreEqual(2d * 1 + 3d * 1 + 5d, result[0], Delta);
        Assert.AreEqual(4d * 1 + 6d * 1 + 7d, result[1], Delta);
        Assert.AreEqual(1d, result[2], Delta);
    }

    [TestMethod]
    public void Transform_MatchesTranslationForIdentityLinearPart()
    {
        // An affine transform with the identity linear part and translation (5, 7) must behave
        // exactly like MatrixTransformations.Translation(5, 7).
        var transform = MatrixTransformations.Transform<double>(1d, 0d, 5d, 0d, 1d, 7d);
        var translation = MatrixTransformations.Translation<double>(5d, 7d);

        var v = new Vector<double>(2d, 3d, 1d);
        var transformResult = transform * v;
        var translationResult = translation * v;

        Assert.AreEqual(translationResult[0], transformResult[0], Delta);
        Assert.AreEqual(translationResult[1], transformResult[1], Delta);
        Assert.AreEqual(translationResult[2], transformResult[2], Delta);
    }

    [TestMethod]
    public void Transform_InvalidValueCount_Throws()
    {
        // 5 is not d*(d+1) for any integer d (0, 2, 6, 12, ...).
        Assert.ThrowsException<ArgumentException>(() => MatrixTransformations.Transform<double>(1d, 2d, 3d, 4d, 5d));
    }
}

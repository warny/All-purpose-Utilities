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

    /// <summary>
    /// Before TODO-pass5.md item #62 was fixed, <see cref="MatrixTransformations.Identity{T}"/> performed
    /// no dimension validation at all, disagreeing with <see cref="Matrix{T}.Identity(int)"/> (which
    /// rejects <c>size &lt;= 0</c>): a zero dimension silently built a 0×0 matrix instead of throwing.
    /// </summary>
    [TestMethod]
    public void Identity_ZeroDimension_ThrowsLikeMatrixIdentity()
        => Assert.ThrowsException<ArgumentException>(() => MatrixTransformations.Identity<double>(0));

    /// <summary>
    /// Same as <see cref="Identity_ZeroDimension_ThrowsLikeMatrixIdentity"/>, for a negative dimension:
    /// previously this failed through raw array allocation (an undocumented, unrelated exception) rather
    /// than the same validated <see cref="ArgumentException"/> as <see cref="Matrix{T}.Identity(int)"/>.
    /// </summary>
    [TestMethod]
    public void Identity_NegativeDimension_ThrowsLikeMatrixIdentity()
        => Assert.ThrowsException<ArgumentException>(() => MatrixTransformations.Identity<double>(-1));

    [TestMethod]
    public void Identity_MatchesMatrixIdentityFactory()
    {
        var viaTransformations = MatrixTransformations.Identity<double>(3);
        var viaMatrix = Matrix<double>.Identity(3);

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.AreEqual(viaMatrix[i, j], viaTransformations[i, j], Delta, $"[{i},{j}]");

        Assert.AreEqual(viaMatrix.IsDiagonal, viaTransformations.IsDiagonal);
        Assert.AreEqual(viaMatrix.IsTriangular, viaTransformations.IsTriangular);
        Assert.AreEqual(viaMatrix.IsIdentity, viaTransformations.IsIdentity);
        Assert.AreEqual(viaMatrix.Determinant, viaTransformations.Determinant, Delta);
    }

    // ── Diagonal ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Diagonal_WithZeroEntry_IsStillTriangularAndDiagonal()
    {
        // A diagonal matrix with a zero entry is still diagonal and triangular by definition; only
        // its invertibility/determinant is affected.
        var m = MatrixTransformations.Diagonal<double>(2d, 0d, 3d);
        Assert.IsTrue(m.IsDiagonal);
        Assert.IsTrue(m.IsTriangular);
        Assert.IsFalse(m.IsIdentity);
        Assert.AreEqual(0d, m.Determinant, Delta);
    }

    [TestMethod]
    public void Diagonal_MatchesMatrixDiagonalFactory()
    {
        var viaTransformations = MatrixTransformations.Diagonal<double>(2d, 0d, 3d);
        var viaMatrix = Matrix<double>.Diagonal(2d, 0d, 3d);

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.AreEqual(viaMatrix[i, j], viaTransformations[i, j], Delta, $"[{i},{j}]");

        Assert.AreEqual(viaMatrix.IsDiagonal, viaTransformations.IsDiagonal);
        Assert.AreEqual(viaMatrix.IsTriangular, viaTransformations.IsTriangular);
        Assert.AreEqual(viaMatrix.IsIdentity, viaTransformations.IsIdentity);
    }

    [TestMethod]
    public void Diagonal_NoValues_Throws()
        => Assert.ThrowsException<ArgumentException>(() => MatrixTransformations.Diagonal<double>());

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

    /// <summary>
    /// Before TODO-pass5.md item #66 was fixed, an empty angle list silently resolved to the degenerate
    /// base dimension <c>d = 1</c> (a meaningless "1×1 rotation") and returned a 2×2 homogeneous identity
    /// matrix, with no way to instead request the identity rotation for a chosen ambient dimension.
    /// </summary>
    [TestMethod]
    public void Rotation_NoAngles_ThrowsAmbiguousDimensionError()
        => Assert.ThrowsException<ArgumentException>(() => MatrixTransformations.Rotation<double>());

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

    // ── Structural metadata invariants (TODO-pass5 item #68) ────────────────────

    /// <summary>
    /// Asserts that a factory-constructed matrix's cached structural flags/determinant agree with what
    /// the same values would lazily recompute to from scratch (via the public array constructor, which
    /// always defers to <c>DetermineStructuralFlags</c>). Before item #68, several factories hardcoded
    /// <c>false</c> for flags that were not actually mathematically guaranteed for every input, which this
    /// helper would have caught: a hardcoded <c>false</c> next to a lazily-recomputed <c>true</c> is
    /// exactly the defect this item describes.
    /// </summary>
    private static void AssertStructuralMetadataMatchesRecomputation(Matrix<double> matrix)
    {
        var recomputed = new Matrix<double>(matrix.ToArray());
        Assert.AreEqual(recomputed.IsIdentity, matrix.IsIdentity, "IsIdentity mismatch vs. recomputation.");
        Assert.AreEqual(recomputed.IsTriangular, matrix.IsTriangular, "IsTriangular mismatch vs. recomputation.");
        Assert.AreEqual(recomputed.IsDiagonal, matrix.IsDiagonal, "IsDiagonal mismatch vs. recomputation.");
        Assert.AreEqual(recomputed.Determinant, matrix.Determinant, Delta, "Determinant mismatch vs. recomputation.");
    }

    [TestMethod]
    public void Identity_MetadataMatchesRecomputation()
        => AssertStructuralMetadataMatchesRecomputation(MatrixTransformations.Identity<double>(3));

    [TestMethod]
    public void Diagonal_MetadataMatchesRecomputation()
        => AssertStructuralMetadataMatchesRecomputation(MatrixTransformations.Diagonal<double>(2d, 0d, 3d));

    [TestMethod]
    public void Scaling_MetadataMatchesRecomputation()
        => AssertStructuralMetadataMatchesRecomputation(MatrixTransformations.Scaling<double>(2d, 3d));

    [TestMethod]
    public void Rotation_MetadataMatchesRecomputation()
        => AssertStructuralMetadataMatchesRecomputation(MatrixTransformations.Rotation<double>(Math.PI / 4));

    [TestMethod]
    public void Skew_MetadataMatchesRecomputation()
        => AssertStructuralMetadataMatchesRecomputation(MatrixTransformations.Skew<double>(0.5, 0.25));

    [TestMethod]
    public void Transform_MetadataMatchesRecomputation()
        => AssertStructuralMetadataMatchesRecomputation(MatrixTransformations.Transform<double>(2d, 3d, 5d, 4d, 6d, 7d));

    /// <summary>
    /// Before item #68, <see cref="MatrixTransformations.Translation{T}"/> always hardcoded
    /// <c>isIdentity: false</c>/<c>isDiagonal: false</c>, even though a translation by zero in every
    /// axis is, by value, exactly the identity matrix.
    /// </summary>
    [TestMethod]
    public void Translation_AllZeroValues_ReportsIdentity()
    {
        var m = MatrixTransformations.Translation<double>(0d, 0d);
        Assert.IsTrue(m.IsIdentity);
        Assert.IsTrue(m.IsDiagonal);
        Assert.IsTrue(m.IsTriangular);
        Assert.AreEqual(1d, m.Determinant, Delta);
        AssertStructuralMetadataMatchesRecomputation(m);
    }

    /// <summary>
    /// A translation matrix is always upper triangular by construction (translation entries only ever
    /// occupy strictly-upper positions), which was previously hardcoded to the wrong answer (<c>false</c>)
    /// alongside the correctly-false <see cref="Matrix{T}.IsDiagonal"/>/<see cref="Matrix{T}.IsIdentity"/>.
    /// </summary>
    [TestMethod]
    public void Translation_NonZeroValues_IsTriangularButNotDiagonalOrIdentity()
    {
        var m = MatrixTransformations.Translation<double>(5d, 7d);
        Assert.IsTrue(m.IsTriangular);
        Assert.IsFalse(m.IsDiagonal);
        Assert.IsFalse(m.IsIdentity);
        Assert.AreEqual(1d, m.Determinant, Delta);
        AssertStructuralMetadataMatchesRecomputation(m);
    }

    /// <summary>
    /// With zero angles, <see cref="MatrixTransformations.Skew{T}"/> resolves to the degenerate base
    /// dimension 1 (no off-diagonal position to fill) and returns a 2×2 identity matrix by value; before
    /// item #68 this was hardcoded to report <c>isIdentity: false</c>.
    /// </summary>
    [TestMethod]
    public void Skew_NoAngles_ReportsIdentity()
    {
        var m = MatrixTransformations.Skew<double>();
        Assert.IsTrue(m.IsIdentity);
        AssertStructuralMetadataMatchesRecomputation(m);
    }

    /// <summary>
    /// Supplying exactly the identity coefficients to <see cref="MatrixTransformations.Transform{T}"/>
    /// must report <see cref="Matrix{T}.IsIdentity"/> as <see langword="true"/> now that the factory no
    /// longer hardcodes <c>false</c>.
    /// </summary>
    [TestMethod]
    public void Transform_IdentityCoefficients_ReportsIdentity()
    {
        var m = MatrixTransformations.Transform<double>(1d, 0d, 0d, 0d, 1d, 0d);
        Assert.IsTrue(m.IsIdentity);
        AssertStructuralMetadataMatchesRecomputation(m);
    }
}

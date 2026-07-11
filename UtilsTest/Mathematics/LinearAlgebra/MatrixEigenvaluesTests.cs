using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Mathematics.LinearAlgebra;

[TestClass]
public class MatrixEigenvaluesTests
{
    private const double Tol = 1e-6;

    [TestMethod]
    public void IsSymmetric_SymmetricMatrix_ReturnsTrue()
    {
        var a = new Matrix<double>(new double[,] { { 4, 2 }, { 2, 3 } });
        Assert.IsTrue(a.IsSymmetric());
    }

    [TestMethod]
    public void IsSymmetric_NonSymmetricMatrix_ReturnsFalse()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
        Assert.IsFalse(a.IsSymmetric());
    }

    [TestMethod]
    public void IsSymmetric_ExplicitTolerance_AcceptsAsymmetryWithinOverride()
    {
        // A[0,1] and A[1,0] differ by 0.01, which the default tolerance would reject for a matrix of
        // this scale, but an explicit, sufficiently generous override accepts.
        var a = new Matrix<double>(new double[,] { { 4, 2.00 }, { 2.01, 3 } });
        Assert.IsFalse(a.IsSymmetric());
        Assert.IsTrue(a.IsSymmetric(symmetryTolerance: 0.1));
    }

    [TestMethod]
    public void IsSymmetric_InvalidTolerance_Throws()
    {
        var a = new Matrix<double>(new double[,] { { 4, 2 }, { 2, 3 } });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.IsSymmetric(symmetryTolerance: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.IsSymmetric(symmetryTolerance: -1d));
    }

    [TestMethod]
    public void ComputeEigenvalues_2x2_CorrectValues()
    {
        // [[2, 1], [1, 2]]  eigenvalues = 3, 1
        var a = new Matrix<double>(new double[,] { { 2, 1 }, { 1, 2 } });
        var (values, _) = a.ComputeEigenvalues();
        Assert.AreEqual(2, values.Length);
        // Sorted descending by magnitude: 3 then 1
        Assert.AreEqual(3.0, values[0], Tol);
        Assert.AreEqual(1.0, values[1], Tol);
    }

    [TestMethod]
    public void ComputeEigenvalues_3x3_Diagonal_CorrectValues()
    {
        // Diagonal matrix — eigenvalues are the diagonal entries
        var a = new Matrix<double>(new double[,]
        {
            { 5, 0, 0 },
            { 0, 3, 0 },
            { 0, 0, 1 }
        });
        var (values, _) = a.ComputeEigenvalues();
        double[] sorted = [5.0, 3.0, 1.0];
        for (int i = 0; i < 3; i++)
            Assert.AreEqual(sorted[i], values[i], Tol);
    }

    [TestMethod]
    public void ComputeEigenvalues_EigenvectorsOrthonormal()
    {
        var a = new Matrix<double>(new double[,] { { 4, 2 }, { 2, 3 } });
        var (_, vecs) = a.ComputeEigenvalues();
        // V^T * V should be identity
        var vtv = vecs.Transpose() * vecs;
        Assert.AreEqual(1.0, vtv[0, 0], Tol);
        Assert.AreEqual(0.0, vtv[0, 1], Tol);
        Assert.AreEqual(0.0, vtv[1, 0], Tol);
        Assert.AreEqual(1.0, vtv[1, 1], Tol);
    }

    [TestMethod]
    public void ComputeEigenvalues_EigenvectorsSatisfyAv()
    {
        // Verify A*v = λ*v for each eigenpair
        var a = new Matrix<double>(new double[,] { { 4, 2 }, { 2, 3 } });
        var (values, vecs) = a.ComputeEigenvalues();
        for (int j = 0; j < 2; j++)
        {
            var v = new Vector<double>(vecs[0, j], vecs[1, j]);
            var av = a * v;
            Assert.AreEqual(values[j] * v[0], av[0], Tol);
            Assert.AreEqual(values[j] * v[1], av[1], Tol);
        }
    }

    [TestMethod]
    public void ComputeEigenvalues_SingularDiagonalMatrix_Succeeds()
    {
        // Minimal rank-deficient symmetric example: previously failed because DecomposeQR rejected
        // the linearly dependent second column during QR iteration.
        var a = new Matrix<double>(new double[,] { { 1, 0 }, { 0, 0 } });
        var (values, _) = a.ComputeEigenvalues();
        Assert.AreEqual(1.0, values[0], Tol);
        Assert.AreEqual(0.0, values[1], Tol);
    }

    [TestMethod]
    public void ComputeEigenvalues_SingularSymmetricMatrix_Succeeds()
    {
        // [[1,1],[1,1]] is symmetric and rank-1 (determinant 0); eigenvalues are 2 and 0.
        var a = new Matrix<double>(new double[,] { { 1, 1 }, { 1, 1 } });
        var (values, vecs) = a.ComputeEigenvalues();
        Assert.AreEqual(2.0, values[0], Tol);
        Assert.AreEqual(0.0, values[1], Tol);

        for (int j = 0; j < 2; j++)
        {
            var v = new Vector<double>(vecs[0, j], vecs[1, j]);
            var av = a * v;
            Assert.AreEqual(values[j] * v[0], av[0], Tol);
            Assert.AreEqual(values[j] * v[1], av[1], Tol);
        }
    }

    [TestMethod]
    public void ComputeEigenvalues_ZeroMatrix_Succeeds()
    {
        var a = Matrix<double>.Zero(2, 2);
        var (values, _) = a.ComputeEigenvalues();
        Assert.AreEqual(0.0, values[0], Tol);
        Assert.AreEqual(0.0, values[1], Tol);
    }

    [TestMethod]
    public void ComputeEigenvalues_NonPositiveMaxIterations_Throws()
    {
        // Regression: maxIterations <= 0 made the QR-iteration loop never execute at all, so the
        // method silently returned the raw diagonal entries of the un-diagonalized input as
        // "eigenvalues", with no convergence check ever running.
        var a = new Matrix<double>(new double[,] { { 2, 1 }, { 1, 2 } });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.ComputeEigenvalues(0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.ComputeEigenvalues(-1));
    }

    [TestMethod]
    public void ComputeEigenvalues_InsufficientIterations_ThrowsInsteadOfReturningRawDiagonal()
    {
        // A non-diagonal matrix needs at least one QR-iteration step; with maxIterations = 1 it may
        // not have converged. Whether it throws or not is not the point of this test - the point is
        // that if it does not throw, the result must actually be validated as converged, not just
        // whatever the diagonal happens to be after too few iterations.
        var a = new Matrix<double>(new double[,] { { 2, 1 }, { 1, 2 } });
        try
        {
            var (values, _) = a.ComputeEigenvalues(1);
            // If it didn't throw, convergence was genuinely reached; the known eigenvalues (3, 1)
            // must hold.
            Assert.AreEqual(3.0, values[0], Tol);
            Assert.AreEqual(1.0, values[1], Tol);
        }
        catch (InvalidOperationException)
        {
            // Also an acceptable outcome: correctly reporting non-convergence instead of returning
            // an unvalidated result.
        }
    }

    [TestMethod]
    public void ComputeEigenvalues_NonSquare_Throws()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        Assert.ThrowsException<InvalidOperationException>(() => a.ComputeEigenvalues());
    }

    [TestMethod]
    public void ComputeEigenvalues_NonSymmetric_Throws()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
        Assert.ThrowsException<InvalidOperationException>(() => a.ComputeEigenvalues());
    }

    [TestMethod]
    public void ComputeEigenvalues_ExplicitConvergenceTolerance_ProducesValidResult()
    {
        var a = new Matrix<double>(new double[,] { { 2, 1 }, { 1, 2 } });
        var (values, _) = a.ComputeEigenvalues(convergenceTolerance: 1e-3);
        Assert.AreEqual(3.0, values[0], 1e-2);
        Assert.AreEqual(1.0, values[1], 1e-2);
    }

    [TestMethod]
    public void ComputeEigenvalues_InvalidConvergenceTolerance_Throws()
    {
        var a = new Matrix<double>(new double[,] { { 2, 1 }, { 1, 2 } });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.ComputeEigenvalues(convergenceTolerance: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.ComputeEigenvalues(convergenceTolerance: -1d));
    }

    [TestMethod]
    public void ComputeEigenvalues_Half_DiagonalMatrix_Succeeds()
    {
        // Regression: a hard-coded 1e-10 absolute tolerance is meaningless for Half; this exercises
        // both IsSymmetric's and the convergence check's tolerance for a type whose own machine
        // epsilon (~0.001) is far coarser than any fixed double-oriented literal.
        var a = new Matrix<Half>(new Half[,] { { (Half)5f, (Half)0f }, { (Half)0f, (Half)3f } });
        var (values, _) = a.ComputeEigenvalues();
        Assert.AreEqual((Half)5f, values[0]);
        Assert.AreEqual((Half)3f, values[1]);
    }

    [TestMethod]
    public void ComputeEigenvalues_Half_NonDiagonalScaledMatrix_Succeeds()
    {
        // Regression: previous Half coverage only exercised an already-diagonal matrix, which
        // trivially satisfies every invariant regardless of whether the scale-aware tolerance formula
        // is actually correct. This matrix has a large diagonal entry (100) alongside a small
        // off-diagonal coupling (1) - the exact "large scale next to a small but significant value"
        // shape the default-tolerance formula's documented known limitation is about - and requires
        // genuine (non-trivial) QR iteration to converge.
        // Characteristic equation lambda^2 - 102*lambda + 199 = 0 -> lambda ~= 100.01, ~= 1.99.
        var a = new Matrix<Half>(new Half[,]
        {
            { (Half)100f, (Half)1f },
            { (Half)1f, (Half)2f }
        });
        var (values, _) = a.ComputeEigenvalues();
        Assert.AreEqual(100.01, (double)values[0], 1.0);
        Assert.AreEqual(1.99, (double)values[1], 0.5);
    }
}

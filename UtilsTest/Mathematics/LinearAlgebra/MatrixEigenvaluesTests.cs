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
}

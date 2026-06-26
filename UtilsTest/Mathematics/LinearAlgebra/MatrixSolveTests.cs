using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Mathematics.LinearAlgebra;

[TestClass]
public class MatrixSolveTests
{
    private static Vector<double> V(params double[] v) => new(v);

    [TestMethod]
    public void Solve_2x2_Identity_ReturnsRhs()
    {
        var a = Matrix<double>.Diagonal(1.0, 1.0);
        var b = V(3, 7);
        var x = a.Solve(b);
        Assert.AreEqual(2, x.Dimension);
        Assert.AreEqual(3.0, x[0], 1e-10);
        Assert.AreEqual(7.0, x[1], 1e-10);
    }

    [TestMethod]
    public void Solve_2x2_KnownSystem()
    {
        // 2x + y = 5, x + 3y = 10  → x=1, y=3
        var a = new Matrix<double>(new double[,] { { 2, 1 }, { 1, 3 } });
        var b = V(5, 10);
        var x = a.Solve(b);
        Assert.AreEqual(1.0, x[0], 1e-9);
        Assert.AreEqual(3.0, x[1], 1e-9);
    }

    [TestMethod]
    public void Solve_3x3_KnownSystem()
    {
        // System with known solution x=[1,2,3]
        var a = new Matrix<double>(new double[,] { { 1, 2, 3 }, { 0, 1, 4 }, { 5, 6, 0 } });
        var b = V(14, 14, 17);  // 1+4+9, 0+2+12, 5+12+0
        var x = a.Solve(b);
        Assert.AreEqual(1.0, x[0], 1e-9);
        Assert.AreEqual(2.0, x[1], 1e-9);
        Assert.AreEqual(3.0, x[2], 1e-9);
    }

    [TestMethod]
    public void Solve_NonSquare_Throws()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        Assert.ThrowsException<InvalidOperationException>(() => a.Solve(V(1, 2)));
    }

    [TestMethod]
    public void Solve_WrongVectorDimension_Throws()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
        Assert.ThrowsException<ArgumentException>(() => a.Solve(V(1, 2, 3)));
    }

    [TestMethod]
    public void Solve_SingularMatrix_Throws()
    {
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 2, 4 } });
        Assert.ThrowsException<InvalidOperationException>(() => a.Solve(V(1, 2)));
    }
}

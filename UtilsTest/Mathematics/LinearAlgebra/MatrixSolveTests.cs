using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
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

    [TestMethod]
    public void Solve_NearSingularMatrix_ThrowsInsteadOfReturningGarbage()
    {
        // The second row is the first scaled by (1 + 1e-13): not exactly singular, but the pivot
        // remaining after elimination is tiny relative to the matrix's own magnitude. Dividing by
        // it would previously amplify rounding error into a huge/NaN "solution" that still looked
        // like a normal return value instead of a diagnosed failure.
        var a = new Matrix<double>(new double[,] { { 1, 2 }, { 2 + 2e-13, 4 + 4e-13 } });
        Assert.ThrowsException<InvalidOperationException>(() => a.Solve(V(1, 2)));
    }

    [TestMethod]
    public void Solve_ExplicitToleranceOverride_CanRejectAWellConditionedMatrix()
    {
        // A well-conditioned matrix solves fine under the default tolerance; passing a large
        // explicit override demonstrates the parameter genuinely changes the accept/reject
        // decision instead of being ignored.
        var a = Matrix<double>.Diagonal(1.0, 0.5);
        var x = a.Solve(V(1, 1));
        Assert.IsNotNull(x);

        Assert.ThrowsException<InvalidOperationException>(
            () => a.Solve(V(1, 1), relativeSingularityTolerance: 0.6));
    }

    [TestMethod]
    public void Solve_InvalidExplicitTolerance_Throws()
    {
        var a = Matrix<double>.Diagonal(1.0, 1.0);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.Solve(V(1, 2), relativeSingularityTolerance: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.Solve(V(1, 2), relativeSingularityTolerance: -1d));
    }

    [TestMethod]
    public void MachineEpsilon_IsNonZeroForHalf()
    {
        // Regression: a hard-coded T.CreateChecked(1e-10) underflows to exactly zero for Half
        // (whose smallest representable positive value is far larger than 1e-10), silently
        // collapsing the scale-aware singularity check back into an exact-zero-only comparison -
        // precisely the behavior the fix was meant to eliminate.
        var field = typeof(Matrix<Half>).GetField("MachineEpsilon", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field);
        var epsilon = (Half)field!.GetValue(null)!;
        Assert.AreNotEqual((Half)0, epsilon);
    }

    [TestMethod]
    public void DefaultSingularityRelativeTolerance_ScalesWithDimensionNotFlatConstant()
    {
        // Regression: an earlier version used a flat 100x-machine-epsilon multiplier regardless of
        // matrix size. For Half specifically that made the default relative tolerance for even a
        // small 2x2 matrix close to 10% of its magnitude, misclassifying ordinary invertible
        // matrices as singular. The default must scale with dimension instead of a fixed multiplier
        // so a small matrix gets a correspondingly small (tight) default tolerance.
        var method = typeof(Matrix<Half>).GetMethod("DefaultSingularityRelativeTolerance", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method);
        var tolerance2 = (Half)method!.Invoke(null, [2])!;
        var tolerance10 = (Half)method!.Invoke(null, [10])!;

        Assert.IsTrue(tolerance2 < (Half)0.01f, $"Default tolerance for a 2x2 Half matrix should be well under 1%, was {tolerance2}.");
        Assert.IsTrue(tolerance10 > tolerance2, "The default tolerance should grow with matrix dimension.");
    }

    [TestMethod]
    public void Solve_Half_WellConditionedSystem_Succeeds()
    {
        var a = Matrix<Half>.Diagonal((Half)2f, (Half)4f);
        var x = a.Solve(new Vector<Half>((Half)6f, (Half)8f));
        Assert.AreEqual((Half)3f, x[0]);
        Assert.AreEqual((Half)2f, x[1]);
    }

    [TestMethod]
    public void Solve_Half_RejectsNonZeroButNegligiblePivot()
    {
        // With the old literal-1e-10-derived tolerance (== 0 for Half), only an exactly-zero pivot
        // was rejected. This pivot (0.001) is non-zero but negligible relative to the matrix's own
        // scale under Half's own limited precision, and must still be rejected.
        var a = new Matrix<Half>(new Half[,] { { (Half)1f, (Half)0f }, { (Half)0f, (Half)0.001f } });
        Assert.ThrowsException<InvalidOperationException>(() => a.Solve(new Vector<Half>((Half)1f, (Half)1f)));
    }

    [TestMethod]
    public void Solve_Half_ModeratelyScaledInvertibleMatrix_DoesNotThrow()
    {
        // Regression: with the earlier flat 100x-machine-epsilon multiplier, the default relative
        // tolerance for Half was close to 10% of the matrix's magnitude, so even an ordinary
        // invertible matrix with a modest (20:1) ratio between its smallest and largest entries -
        // nothing close to numerically dangerous, even at Half's limited precision - was wrongly
        // rejected as singular.
        var a = Matrix<Half>.Diagonal((Half)1f, (Half)0.05f);
        var x = a.Solve(new Vector<Half>((Half)1f, (Half)0.05f));
        Assert.AreEqual((Half)1f, x[0]);
        Assert.AreEqual((Half)1f, x[1]);
    }
}

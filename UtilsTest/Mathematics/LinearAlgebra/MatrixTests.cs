using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Mathematics.LinearAlgebra
{

    [TestClass]
    public class MatrixTests
    {
        /// <summary>
        /// Ensures that LU factorisation preserves the source matrix and yields triangular factors.
        /// </summary>
        [TestMethod]
        public void DiagonalizeLUProducesTriangularFactorsWithoutMutatingSource()
        {
            Matrix<double> matrix = new Matrix<double>(new double[,]
            {
                                { 4d, 3d },
                                { 6d, 3d },
            });

            double[,] original = matrix.ToArray();
            (Matrix<double> L, Matrix<double> U) = matrix.DiagonalizeLU();
            AssertMatricesAreEqual(new Matrix<double>(original), matrix, 1e-12);

            for (int i = 0; i < matrix.Rows; i++)
            {
                Assert.AreEqual(1d, L[i, i], 1e-12, $"Diagonal entry at {i} was not one.");
                for (int j = 0; j < i; j++)
                {
                    Assert.AreEqual(0d, U[i, j], 1e-12, $"Upper matrix contained non-zero value at [{i}, {j}].");
                }
            }
        }

        /// <summary>
        /// Verifies that inverting a matrix leaves the original unchanged and produces an identity when multiplied.
        /// </summary>
        [TestMethod]
        public void InvertDoesNotMutateOriginalMatrix()
        {
            Matrix<double> matrix = new Matrix<double>(new double[,]
            {
                                { 4d, 7d },
                                { 2d, 6d },
            });

            double[,] originalComponents = matrix.ToArray();
            Matrix<double> inverse = matrix.Invert();

            AssertMatricesAreEqual(new Matrix<double>(originalComponents), matrix, 1e-12);

            Matrix<double> identity = matrix * inverse;

            double tolerance = 1e-9;
            for (int row = 0; row < identity.Rows; row++)
            {
                for (int col = 0; col < identity.Columns; col++)
                {
                    double expected = row == col ? 1d : 0d;
                    Assert.AreEqual(expected, identity[row, col], tolerance, $"Unexpected value at [{row}, {col}]");
                }
            }
        }

        /// <summary>
        /// Asserts that two matrices contain the same components within the provided tolerance.
        /// </summary>
        /// <param name="expected">Expected matrix.</param>
        /// <param name="actual">Matrix produced by the computation.</param>
        /// <param name="tolerance">Accepted numerical tolerance.</param>
        private static void AssertMatricesAreEqual(Matrix<double> expected, Matrix<double> actual, double tolerance)
        {
            Assert.AreEqual(expected.Rows, actual.Rows, "Row count mismatch.");
            Assert.AreEqual(expected.Columns, actual.Columns, "Column count mismatch.");

            for (int row = 0; row < expected.Rows; row++)
            {
                for (int col = 0; col < expected.Columns; col++)
                {
                    Assert.AreEqual(expected[row, col], actual[row, col], tolerance, $"Mismatch at [{row}, {col}]");
                }
            }
        }

        /*
        [TestMethod]
        public void TransformMatrixTests()
{
    System.Drawing.Drawing2D.Matrix Create2D(params Action<System.Drawing.Drawing2D.Matrix>[] actions)
    {
        var result = new System.Drawing.Drawing2D.Matrix(1, 0, 0, 1, 0, 0);
        foreach (var action in actions)
        {
            var tr = new System.Drawing.Drawing2D.Matrix(1, 0, 0, 1, 0, 0);
            action(tr);
            result.Multiply(tr);
        }
        return result;
    }

    var tests = new (string name, System.Drawing.Drawing2D.Matrix D2, Utils.Mathematics.LinearAlgebra.Matrix LA)[] {
        ("identitity", Create2D(), Matrix.Transform(1, 0, 0, 1, 0, 0)),
        ("translation", Create2D(m=>m.Translate(1,2)), Matrix.Translation(1,2)),
        ("rotation", Create2D(m=>m.Rotate(-60)), Matrix.Rotation(System.Math.PI / 3)),
        ("homothety", Create2D(m=>m.Scale(1,2)), Matrix.Scaling(1,2)),
        ("translation,rotation", Create2D(m=>m.Translate(1,2), m=>m.Rotate(-60)), Matrix.Rotation(System.Math.PI / 3) * Matrix.Translation(1, 2)), //le comportement de Drawing2D.Matrix est très bizarre ici
    };

    int testNumber = 0;
    foreach (var test in tests)
    {
        var components = test.D2.Elements;

        int i = 0;
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                Assert.AreEqual(components[i], test.LA[x, y], 0.001, $"La dimension[{x}, {y}] est différente sur le test #{testNumber} {test.name} sur \r\n[{string.Join(", ", test.D2.Elements.Select(x=>x.ToString("0.00")))}] et \r\n{test.LA}");
                i++;
            }
        }
        testNumber++;
    }

}
*/
    }
}

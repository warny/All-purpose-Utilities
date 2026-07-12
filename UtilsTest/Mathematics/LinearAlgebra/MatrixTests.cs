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
            (Matrix<double> L, Matrix<double> U, Matrix<double> P) = matrix.DiagonalizeLU();
            AssertMatricesAreEqual(new Matrix<double>(original), matrix, 1e-12);

            for (int i = 0; i < matrix.Rows; i++)
            {
                Assert.AreEqual(1d, L[i, i], 1e-12, $"Diagonal entry at {i} was not one.");
                for (int j = 0; j < i; j++)
                {
                    Assert.AreEqual(0d, U[i, j], 1e-12, $"Upper matrix contained non-zero value at [{i}, {j}].");
                }
            }

            AssertMatricesAreEqual(P * matrix, L * U, 1e-12);
        }

        /// <summary>
        /// Regression for the previous DiagonalizeLU implementation, which applied elimination row
        /// operations to an identity matrix instead of storing the elimination multipliers directly
        /// in L, so L * U did not reconstruct the (possibly permuted) original matrix.
        /// </summary>
        [TestMethod]
        public void DiagonalizeLUSatisfiesPermutedReconstructionIdentity_NoSwapNeeded()
        {
            // No pivoting occurs: |2| is already the largest first-column magnitude, so P must be
            // the identity and L * U must equal the original matrix directly.
            Matrix<double> matrix = new Matrix<double>(new double[,]
            {
                                { 2d, 1d },
                                { 1d, 3d },
            });

            (Matrix<double> L, Matrix<double> U, Matrix<double> P) = matrix.DiagonalizeLU();

            AssertMatricesAreEqual(MatrixTransformations.Identity<double>(2), P, 1e-12);
            AssertMatricesAreEqual(matrix, L * U, 1e-12);
        }

        [TestMethod]
        public void DiagonalizeLUSatisfiesPermutedReconstructionIdentity_WithSwap()
        {
            // Partial pivoting swaps rows here (|6| > |4|), so L * U reconstructs P * matrix, not
            // matrix itself.
            Matrix<double> matrix = new Matrix<double>(new double[,]
            {
                                { 4d, 3d },
                                { 6d, 3d },
            });

            (Matrix<double> L, Matrix<double> U, Matrix<double> P) = matrix.DiagonalizeLU();

            AssertMatricesAreEqual(P * matrix, L * U, 1e-12);
            // Sanity check that a swap actually happened, i.e. P is not the identity.
            Assert.AreNotEqual(1d, P[0, 0], 1e-12);
        }

        [TestMethod]
        public void DiagonalizeLU3x3SatisfiesPermutedReconstructionIdentity()
        {
            Matrix<double> matrix = new Matrix<double>(new double[,]
            {
                                { 2d, -1d, -2d },
                                { -4d, 6d, 3d },
                                { -4d, -2d, 8d },
            });

            (Matrix<double> L, Matrix<double> U, Matrix<double> P) = matrix.DiagonalizeLU();

            AssertMatricesAreEqual(P * matrix, L * U, 1e-9);
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

        [TestMethod]
        public void Invert_SingularMatrix_Throws()
        {
            var matrix = new Matrix<double>(new double[,] { { 1d, 2d }, { 2d, 4d } });
            Assert.ThrowsException<InvalidOperationException>(() => matrix.Invert());
        }

        [TestMethod]
        public void Invert_NearSingularMatrix_ThrowsInsteadOfReturningGarbage()
        {
            var matrix = new Matrix<double>(new double[,] { { 1d, 2d }, { 2d + 2e-13, 4d + 4e-13 } });
            Assert.ThrowsException<InvalidOperationException>(() => matrix.Invert());
        }

        // ── Structural metadata on the inverse (TODO-pass5 item #61) ───────────────

        /// <summary>
        /// Before the fix, <see cref="Matrix{T}.Invert"/> always hardcoded <c>isDiagonal: false</c> on the
        /// returned matrix regardless of its actual structure, permanently disabling lazy recomputation.
        /// The inverse of a diagonal matrix is itself diagonal (and therefore triangular).
        /// </summary>
        [TestMethod]
        public void Invert_DiagonalMatrix_ResultReportsDiagonalAndTriangular()
        {
            Matrix<double> diagonal = Matrix<double>.Diagonal(2d, 4d, 5d);
            Matrix<double> inverse = diagonal.Invert();

            Assert.IsTrue(inverse.IsDiagonal);
            Assert.IsTrue(inverse.IsTriangular);
            Assert.IsFalse(inverse.IsIdentity);
            AssertMatricesAreEqual(Matrix<double>.Diagonal(0.5d, 0.25d, 0.2d), inverse, 1e-9);
        }

        /// <summary>
        /// Same as <see cref="Invert_DiagonalMatrix_ResultReportsDiagonalAndTriangular"/>, but for a
        /// non-diagonal upper-triangular source: the inverse of an upper-triangular matrix is itself
        /// upper-triangular, so the recomputed metadata should report it as such.
        /// </summary>
        [TestMethod]
        public void Invert_TriangularMatrix_ResultReportsTriangular()
        {
            Matrix<double> upperTriangular = new Matrix<double>(new double[,]
            {
                { 2d, 3d },
                { 0d, 4d },
            });

            Matrix<double> inverse = upperTriangular.Invert();

            Assert.IsTrue(inverse.IsTriangular);
            Assert.IsFalse(inverse.IsDiagonal);
        }

        /// <summary>
        /// A general (non-triangular, non-diagonal) matrix's inverse is itself generally neither
        /// triangular nor diagonal; the recomputed metadata (rather than a hardcoded value) must still
        /// correctly report that.
        /// </summary>
        [TestMethod]
        public void Invert_GeneralMatrix_ResultReportsNotTriangularNotDiagonal()
        {
            Matrix<double> matrix = new Matrix<double>(new double[,]
            {
                { 4d, 7d },
                { 2d, 6d },
            });

            Matrix<double> inverse = matrix.Invert();

            Assert.IsFalse(inverse.IsTriangular);
            Assert.IsFalse(inverse.IsDiagonal);
            Assert.IsFalse(inverse.IsIdentity);
        }

        [TestMethod]
        public void Determinant_NearSingularMatrix_ReturnsZeroInsteadOfGarbage()
        {
            // Previously, dividing by the tiny (but non-zero) pivot left after elimination could
            // amplify rounding error into a huge/NaN determinant instead of the mathematically
            // expected near-zero value for a near-singular matrix.
            var matrix = new Matrix<double>(new double[,] { { 1d, 2d }, { 2d + 2e-13, 4d + 4e-13 } });
            Assert.AreEqual(0d, matrix.Determinant, 1e-6);
        }

        [TestMethod]
        public void Identity_ProducesCorrectDiagonalAndFlags()
        {
            var m = Matrix<double>.Identity(3);
            Assert.AreEqual(3, m.Rows);
            Assert.AreEqual(3, m.Columns);
            Assert.IsTrue(m.IsIdentity);
            Assert.IsTrue(m.IsTriangular);
            Assert.IsTrue(m.IsDiagonal);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    Assert.AreEqual(i == j ? 1d : 0d, m[i, j], 1e-12);
        }

        [TestMethod]
        public void Zero_ProducesAllZeroMatrix()
        {
            var m = Matrix<double>.Zero(2, 3);
            Assert.AreEqual(2, m.Rows);
            Assert.AreEqual(3, m.Columns);
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 3; j++)
                    Assert.AreEqual(0d, m[i, j], 1e-12);
        }

        [TestMethod]
        public void Diagonal_NonZero_SetsStructuralFlags()
        {
            var m = Matrix<double>.Diagonal(2d, 3d);
            Assert.IsTrue(m.IsTriangular);
            Assert.IsTrue(m.IsDiagonal);
            Assert.IsFalse(m.IsIdentity);
            Assert.AreEqual(2d, m[0, 0], 1e-12);
            Assert.AreEqual(3d, m[1, 1], 1e-12);
            Assert.AreEqual(0d, m[0, 1], 1e-12);
        }

        [TestMethod]
        public void Diagonal_WithZeroEntry_IsStillDiagonalAndTriangular()
        {
            // A diagonal matrix with a zero entry is still diagonal and triangular (just singular).
            var m = Matrix<double>.Diagonal(2d, 0d);
            Assert.IsTrue(m.IsTriangular);
            Assert.IsTrue(m.IsDiagonal);
            Assert.IsFalse(m.IsIdentity);
        }

        [TestMethod]
        public void IsTriangular_UpperTriangularMatrix_ReturnsTrue()
        {
            var m = new Matrix<double>(new double[,]
            {
                { 1d, 2d, 3d },
                { 0d, 4d, 5d },
                { 0d, 0d, 6d },
            });
            Assert.IsTrue(m.IsTriangular);
            Assert.IsFalse(m.IsDiagonal);
            Assert.IsFalse(m.IsIdentity);
        }

        [TestMethod]
        public void IsTriangular_NonTriangularMatrix_ReturnsFalse()
        {
            var m = new Matrix<double>(new double[,]
            {
                { 1d, 0d },
                { 1d, 1d },
            });
            // Lower triangular only — the other direction has a non-zero.
            Assert.IsTrue(m.IsTriangular);

            var m2 = new Matrix<double>(new double[,]
            {
                { 1d, 2d },
                { 3d, 4d },
            });
            Assert.IsFalse(m2.IsTriangular);
        }

        [TestMethod]
        public void IsTriangularWithin_RoundingNoise_ReturnsTrueButExactReturnsFalse()
        {
            // A value that should be zero but carries rounding noise from prior arithmetic must
            // fail the exact predicate while passing its explicit tolerance-aware counterpart -
            // the two are separate, deliberately opt-in predicates, not one silently-tolerant check.
            var m = new Matrix<double>(new double[,]
            {
                { 1d, 2d, 3d },
                { 1e-13, 4d, 5d },
                { 0d, 0d, 6d },
            });

            Assert.IsFalse(m.IsTriangular);
            Assert.IsTrue(m.IsTriangularWithin(1e-9));
            Assert.IsFalse(m.IsTriangularWithin(1e-15));
        }

        [TestMethod]
        public void IsDiagonalWithin_RoundingNoise_ReturnsTrueButExactReturnsFalse()
        {
            var m = new Matrix<double>(new double[,]
            {
                { 2d, 1e-13 },
                { 0d, 3d },
            });

            Assert.IsFalse(m.IsDiagonal);
            Assert.IsTrue(m.IsDiagonalWithin(1e-9));
        }

        [TestMethod]
        public void IsIdentityWithin_RoundingNoise_ReturnsTrueButExactReturnsFalse()
        {
            var m = new Matrix<double>(new double[,]
            {
                { 1d + 1e-13, 0d },
                { 1e-13, 1d },
            });

            Assert.IsFalse(m.IsIdentity);
            Assert.IsTrue(m.IsIdentityWithin(1e-9));
        }

        [TestMethod]
        public void IsNormalSpaceWithin_RoundingNoise_ReturnsTrueButExactReturnsFalse()
        {
            var m = new Matrix<double>(new double[,]
            {
                { 1d, 0d, 0d },
                { 0d, 1d, 0d },
                { 1e-13, 1e-13, 1d + 1e-13 },
            });

            Assert.IsFalse(m.IsNormalSpace);
            Assert.IsTrue(m.IsNormalSpaceWithin(1e-9));
        }

        [TestMethod]
        public void ToleranceAwarePredicates_InvalidTolerance_Throw()
        {
            // A NaN tolerance would make every ">" comparison false (vacuously "within tolerance"
            // for everything), and a negative tolerance would reject even an exact match.
            var m = Matrix<double>.Identity(2);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => m.IsTriangularWithin(double.NaN));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => m.IsTriangularWithin(-1d));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => m.IsDiagonalWithin(double.NaN));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => m.IsDiagonalWithin(-1d));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => m.IsIdentityWithin(double.NaN));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => m.IsIdentityWithin(-1d));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => m.IsNormalSpaceWithin(double.NaN));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => m.IsNormalSpaceWithin(-1d));
        }

        [TestMethod]
        public void Determinant_IdentityMatrix_ReturnsOne()
        {
            var m = Matrix<double>.Identity(3);
            Assert.AreEqual(1d, m.Determinant, 1e-12);
        }

        [TestMethod]
        public void Determinant_2x2_ReturnsCorrectValue()
        {
            var m = new Matrix<double>(new double[,] { { 1d, 2d }, { 3d, 4d } });
            Assert.AreEqual(-2d, m.Determinant, 1e-12);
        }

        [TestMethod]
        public void Determinant_SingularMatrix_ReturnsZero()
        {
            var m = new Matrix<double>(new double[,] { { 1d, 2d }, { 2d, 4d } });
            Assert.AreEqual(0d, m.Determinant, 1e-12);
        }

        [TestMethod]
        public void Determinant_3x3_ReturnsCorrectValue()
        {
            var m = new Matrix<double>(new double[,]
            {
                { 2d, -1d, 0d },
                { -1d, 2d, -1d },
                { 0d, -1d, 2d },
            });
            // det = 2*(4-1) - (-1)*(-2-0) + 0 = 6 - 2 = 4
            Assert.AreEqual(4d, m.Determinant, 1e-10);
        }

        [TestMethod]
        public void Transpose_NonSquare_SwapsRowsAndColumns()
        {
            var m = new Matrix<double>(new double[,] { { 1d, 2d, 3d }, { 4d, 5d, 6d } });
            var t = m.Transpose();
            Assert.AreEqual(3, t.Rows);
            Assert.AreEqual(2, t.Columns);
            Assert.AreEqual(1d, t[0, 0], 1e-12);
            Assert.AreEqual(4d, t[0, 1], 1e-12);
            Assert.AreEqual(2d, t[1, 0], 1e-12);
            Assert.AreEqual(5d, t[1, 1], 1e-12);
            Assert.AreEqual(3d, t[2, 0], 1e-12);
            Assert.AreEqual(6d, t[2, 1], 1e-12);
        }

        [TestMethod]
        public void Transpose_Square_IsInvolutory()
        {
            // Transposing twice returns the original matrix.
            var m = new Matrix<double>(new double[,] { { 1d, 2d }, { 3d, 4d } });
            AssertMatricesAreEqual(m, m.Transpose().Transpose(), 1e-12);
        }

        [TestMethod]
        public void GetRow_ReturnsCorrectVector()
        {
            var m = new Matrix<double>(new double[,] { { 1d, 2d, 3d }, { 4d, 5d, 6d } });
            var row = m.GetRow(1);
            Assert.AreEqual(3, row.Dimension);
            Assert.AreEqual(4d, row[0], 1e-12);
            Assert.AreEqual(5d, row[1], 1e-12);
            Assert.AreEqual(6d, row[2], 1e-12);
        }

        [TestMethod]
        public void GetColumn_ReturnsCorrectVector()
        {
            var m = new Matrix<double>(new double[,] { { 1d, 2d, 3d }, { 4d, 5d, 6d } });
            var col = m.GetColumn(2);
            Assert.AreEqual(2, col.Dimension);
            Assert.AreEqual(3d, col[0], 1e-12);
            Assert.AreEqual(6d, col[1], 1e-12);
        }

        // ── Empty/null input validation (jagged array and vector constructors) ─────

        [TestMethod]
        public void JaggedArrayConstructor_EmptyArray_ThrowsClearArgumentException()
        {
            // Regression: previously threw an incidental InvalidOperationException from
            // Enumerable.Max() on an empty sequence instead of a clear, documented rejection.
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(System.Array.Empty<double[]>()));
        }

        [TestMethod]
        public void JaggedArrayConstructor_NullRow_ThrowsClearArgumentException()
        {
            // Regression: previously threw an incidental NullReferenceException when computing the
            // null row's length.
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(new double[][] { new[] { 1d, 2d }, null! }));
        }

        [TestMethod]
        public void VectorConstructor_NoVectors_ThrowsClearArgumentException()
        {
            // Regression: previously threw an incidental IndexOutOfRangeException from accessing
            // vectors[0] unconditionally.
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(System.Array.Empty<Vector<double>>()));
        }

        [TestMethod]
        public void VectorConstructor_NullVector_ThrowsClearArgumentException()
        {
            // Regression: previously threw an incidental NullReferenceException when reading the
            // null vector's Dimension.
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(new Vector<double>(1d, 2d), null!));
        }

        [TestMethod]
        public void RawConstructor_NonPositiveDimensions_ThrowsClearArgumentException()
        {
            // Regression: the raw (rows, columns) constructor previously performed no validation at
            // all, relying on the CLR's own (undocumented, exception-type-inconsistent) behavior for
            // non-positive multidimensional array allocation instead of the explicit contract already
            // enforced by Identity/Zero/Diagonal.
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(0, 3));
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(3, 0));
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(-1, 3));
        }

        [TestMethod]
        public void ArrayConstructor_ZeroSizedArray_ThrowsClearArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(new double[0, 0]));
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(new double[0, 3]));
        }

        [TestMethod]
        public void JaggedArrayConstructor_AllRowsEmpty_ThrowsClearArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => new Matrix<double>(new double[][] { System.Array.Empty<double>() }));
        }

        [TestMethod]
        public void Equals_EqualMatrices_ReturnsTrue()
        {
            // Regression: Equals(Matrix<T>) used to require GetHashCode() equality before comparing
            // elements. A correct hash is a pure function of the components so this never produced a
            // wrong *answer* here, but the precondition was hazardous (and unnecessary) for any future
            // tolerance-aware equality/hashing change. Elements are compared directly now.
            var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
            var b = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
            Assert.IsTrue(a.Equals(b));
        }

        [TestMethod]
        public void Equals_DifferentValues_ReturnsFalse()
        {
            var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
            var b = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 5 } });
            Assert.IsFalse(a.Equals(b));
        }

        [TestMethod]
        public void Equals_Null_ReturnsFalse()
        {
            var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
            Assert.IsFalse(a.Equals((Matrix<double>?)null));
        }

        [TestMethod]
        public void Equals_SameReference_ReturnsTrue()
        {
            var a = new Matrix<double>(new double[,] { { 1, 2 }, { 3, 4 } });
            Assert.IsTrue(a.Equals(a));
        }

        [TestMethod]
        public void ToString_DefaultFormat_DoesNotRoundValues()
        {
            // Regression: ToString used to call T.Round(value, culture.NumberDecimalDigits) before
            // appending, silently discarding precision the caller never asked to lose.
            var m = new Matrix<double>(new double[,] { { 1.23456789012345 } });
            string s = m.ToString("", System.Globalization.CultureInfo.InvariantCulture);
            StringAssert.Contains(s, "1.23456789012345");
        }

        [TestMethod]
        public void ToString_NumericFormatAfterColon_IsForwardedToElements()
        {
            // The part of the composite format string after ':' is forwarded verbatim to each
            // element's own IFormattable.ToString, rather than being silently ignored.
            var m = new Matrix<double>(new double[,] { { 1.23456, 2.5 } });
            string s = m.ToString("S:F2", System.Globalization.CultureInfo.InvariantCulture);
            StringAssert.Contains(s, "1.23");
            StringAssert.Contains(s, "2.50");
        }

        [TestMethod]
        public void ToString_LayoutTokenOnly_UsesCorrectRowSeparator()
        {
            var m = new Matrix<double>(new double[,] { { 1 }, { 2 } });
            string s = m.ToString("C", null);
            Assert.AreEqual("{ { 1 }, { 2 } }", s);
        }

        [TestMethod]
        public void ToString_UnrecognizedLayoutToken_ThrowsFormatException()
        {
            var m = new Matrix<double>(new double[,] { { 1 } });
            Assert.ThrowsException<FormatException>(() => m.ToString("bogus", null));
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

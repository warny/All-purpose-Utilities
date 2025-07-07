using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Collections;
using Utils.Mathematics.LinearAlgebra;

namespace UtilsTest.Mathematics.LinearAlgebra
{
        [TestClass]
        public class MatrixIdentityCacheTests
        {
                [TestMethod]
                public void Identity_matrix_is_cached_and_cloned()
                {
                        // Access the generic cache via reflection to clear it for this test
                        var cacheType = typeof(MatrixTransformations).GetNestedType("IdentityCache`1", BindingFlags.NonPublic);
                        var cache = cacheType!.MakeGenericType(typeof(double));
                        var field = cache.GetField("Matrices", BindingFlags.NonPublic | BindingFlags.Static)!;
                        var dictionary = (System.Collections.IDictionary)field.GetValue(null)!;
                        dictionary.Clear();

                        Matrix<double> m1 = MatrixTransformations.Identity<double>(3);
                        var arrayField = typeof(Matrix<double>).GetField("components", BindingFlags.NonPublic | BindingFlags.Instance)!;
                        double[,] internalArray = (double[,])arrayField.GetValue(m1)!;
                        internalArray[0, 0] = 42.0;

                        Matrix<double> m2 = MatrixTransformations.Identity<double>(3);
                        double[,] array2 = (double[,])arrayField.GetValue(m2)!;

                        Assert.AreEqual(1.0, array2[0, 0], 0.0001);
                        Assert.AreEqual(1, dictionary.Count);
                        Assert.AreNotSame(m1, m2);
                }
        }
}

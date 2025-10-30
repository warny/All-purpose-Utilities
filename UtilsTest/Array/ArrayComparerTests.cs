using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Utils.Arrays;
using Utils.Collections;

namespace UtilsTest.Array
{
    [TestClass]
    public class ArrayComparerTests
    {
        [TestMethod]
        public void CompareSimpleEqualsArrays()
        {
            var array1 = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var array2 = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            EnumerableEqualityComparer<int> comparer = EnumerableEqualityComparer<int>.Default;
            Assert.IsTrue(comparer.Equals(array1, array2));
        }


        [TestMethod]
        public void CompareSimpleNonEqualsArrays()
        {
            var array1 = new[] { 1, 2, 3, 4, 5, 6, 7, 9, 8 };
            var array2 = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            EnumerableEqualityComparer<int> comparer = EnumerableEqualityComparer<int>.Default;
            Assert.IsFalse(comparer.Equals(array1, array2));
        }

        [TestMethod]
        public void CompareNestedEqualsArrays()
        {
            int[][] array1 = [
                [ 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 ],
                [ 2, 1, 2, 3, 4, 5, 6, 7, 8 ],
                [ 3, 1, 2, 3, 4, 5, 6, 7 ],
                [ 4, 1, 2, 3, 4, 5, 6 ]
            ];
            int[][] array2 = [
                [ 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 ],
                [ 2, 1, 2, 3, 4, 5, 6, 7, 8 ],
                [ 3, 1, 2, 3, 4, 5, 6, 7 ],
                [ 4, 1, 2, 3, 4, 5, 6 ]
            ];

            EnumerableEqualityComparer<int[]> comparer = EnumerableEqualityComparer<int[]>.Default;
            Assert.AreEqual(array1, array2, comparer);
        }

        [TestMethod]
        public void CompareNestedNonEqualsArrays()
        {
            int[][] array1 = [
                [1, 1, 2, 3, 4, 5, 6, 7, 8, 9],
                [ 2, 1, 2, 3, 4, 5, 6, 7, 8, 9 ],
                [ 3, 1, 2, 3, 4, 5, 6, 7, 8, 9 ],
                [ 4, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]
            ];
            int[][] array2 = [
                [ 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 ],
                [ 2, 1, 2, 3, 4, 5, 6, 7, 8, 9 ],
                [ 4, 1, 2, 3, 4, 5, 6, 7, 8, 9 ],
                [ 3, 1, 2, 3, 4, 5, 6, 7, 8, 9 ]
            ];

            EnumerableEqualityComparer<int[]> comparer = EnumerableEqualityComparer<int[]>.Default;
            Assert.IsFalse(comparer.Equals(array1, array2));
        }
    }
}

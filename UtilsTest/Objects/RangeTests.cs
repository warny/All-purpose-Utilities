using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Utils.Arrays;
using Utils.Collections;
using Utils.Range;

namespace UtilsTest.Objects
{
    [TestClass]
    public class RangeTests
    {
        EnumerableEqualityComparer<int> comparer = EnumerableEqualityComparer<int>.Default;

        [TestMethod]
        public void RangeTestFrom1()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 4, 7, 10 };
            var range = table.From(3, 3);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestFrom2()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 6, 7, 8, 9, 10 };
            var range = table.From(5);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestFrom3()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 6, 5, 4, 3, 2, 1 };
            var range = table.From(5, -1);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestTo1()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 1, 2, 3, 4, 5 };
            var range = table.To(4);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestTo2()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 1, 3, 5 };
            var range = table.To(4, 2);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestTo3()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 2, 4 };
            var range = table.To(3, 2);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestTo4()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 10, 9, 8, 7, 6 };
            var range = table.To(5, -1);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestTo5()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 9, 7, 5 };
            var range = table.To(4, -2);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestTo6()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 10, 8, 6 };
            var range = table.To(5, -2);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestBetween1()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 4, 5, 6, 7 };
            var range = table.Between(3, 6);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestBetween2()
        {
            int[] table = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
            int[] expected = [4, 6];
            var range = table.Between(3, 6, 2);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestBetween3()
        {
            int[] table = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
            int[] expected = [7, 5];
            var range = table.Between(3, 6, -2);
            Assert.AreEqual(expected[0], range[0]);
            Assert.AreEqual(expected[1], range[1]);
            Assert.IsTrue(comparer.Equals(expected, range));
        }

        [TestMethod]
        public void RangeTestReverse()
        {
            int[] table = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] expected = { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
            table.Reverse();
            Assert.AreEqual(expected[0], table[0]);
            Assert.AreEqual(expected[1], table[1]);
            Assert.IsTrue(comparer.Equals(expected, table));
        }

    }
}

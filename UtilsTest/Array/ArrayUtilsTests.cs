using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;

namespace UtilsTest.Array
{
    [TestClass]
    public class ArrayUtilsTests
    {
        [TestMethod]
        public void TestTrim()
        {
            var array = "abcdefghijklmnopqrstuvwxyz".ToArray();
            var result = array.Trim('y', 'z', 'a', 'b', 'm', 'n');
            string resultString = new string(result);

            Assert.AreEqual("cdefghijklmnopqrstuvwx", resultString);
        }

        [TestMethod]
        public void TestTrimLeft()
        {
            var array = "abcdefghijklmnopqrstuvwxyz".ToArray();
            var result = array.TrimStart('y', 'z', 'a', 'b', 'm', 'n');
            string resultString = new string(result);
            Assert.AreEqual("cdefghijklmnopqrstuvwxyz", resultString);
        }

        [TestMethod]
        public void TestTrimRight()
        {
            var array = "abcdefghijklmnopqrstuvwxyz".ToArray();
            var result = array.TrimEnd('y', 'z', 'a', 'b', 'm', 'n');
            string resultString = new string(result);
            Assert.AreEqual("abcdefghijklmnopqrstuvwx", resultString);
        }

        private static int[] InitIntArray()
        {
            Random random = new Random();
            return [
                random.Next(),
                random.Next(),
                random.Next(),
                random.Next(),
                random.Next(),
                random.Next(),
                random.Next(),
                random.Next(),
                random.Next(),
                random.Next(),
            ];
        }

        private static double[] InitDoubleArray()
        {
            Random random = new Random();
            double[] values = [
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble(),
            ];
            return values;
        }

        [TestMethod]
        public void ConvertToArrayOfIntTest()
        {
            int[] values = InitIntArray();

            var strings = values.Select(v => v.ToString()).ToArray();

            var result = strings.ConvertToArrayOf<int>();

            Assert.AreEqual(values.Length, result.Length);

            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(values[i], result[i]);
            }
        }


        [TestMethod]
        public void ConvertToArrayOfDoubleTest()
        {
            double[] values = InitDoubleArray();

            var strings = values.Select(v => v.ToString()).ToArray();

            var result = strings.ConvertToArrayOf<double>();

            Assert.AreEqual(values.Length, result.Length);

            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(values[i], result[i], 0.00000001);
            }
        }

        [TestMethod]
        public void CopyWithInvalidStartThrowsArgumentOutOfRangeException()
        {
            int[] values = [1, 2, 3];

            var exception = Assert.ThrowsException<ArgumentOutOfRangeException>(() => values.Copy(-1, 1));

            Assert.AreEqual("start", exception.ParamName);
        }

        [TestMethod]
        public void CopyWithInvalidLengthThrowsArgumentOutOfRangeException()
        {
            int[] values = [1, 2, 3];

            var exception = Assert.ThrowsException<ArgumentOutOfRangeException>(() => values.Copy(1, 5));

            Assert.AreEqual("length", exception.ParamName);
        }
    }
}

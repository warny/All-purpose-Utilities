using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        [TestMethod]
        public void ConvertToArrayOfIntTest()
        {
            int[] values = [0, 1, -1, 42, -42, 1_000_000, -1_000_000];

            var strings = values.Select(v => v.ToString(CultureInfo.CurrentCulture)).ToArray();

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
            double[] values = [0, 1, -1, 42.5, -42.25, 0.125, -0.03125];

            var strings = values.Select(v => v.ToString(CultureInfo.CurrentCulture)).ToArray();

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

            var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => values.Copy(-1, 1));

            Assert.AreEqual("start", exception.ParamName);
        }

        [TestMethod]
        public void CopyWithInvalidLengthThrowsArgumentOutOfRangeException()
        {
            int[] values = [1, 2, 3];

            var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => values.Copy(1, 5));

            Assert.AreEqual("length", exception.ParamName);
        }
    }
}

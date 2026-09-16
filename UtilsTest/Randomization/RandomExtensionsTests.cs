using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Randomization;

namespace UtilsTest.Randomization;

/// <summary>
/// Verifies deterministic contracts of random extension methods.
/// </summary>
[TestClass]
public class RandomExtensionsTests
{
    /// <summary>
    /// Proves that the final character index is passed through without an off-by-one error.
    /// </summary>
    [TestMethod]
    public void RandomString_CanSelectFinalCharacter()
    {
        ScriptedRandom rng = new([2]);

        Assert.AreEqual("C", rng.RandomString(1, ['A', 'B', 'C']));
    }

    /// <summary>
    /// Verifies null alphabet validation with a deterministic generator.
    /// </summary>
    [TestMethod]
    public void RandomString_NullCharArray_Throws()
    {
        ScriptedRandom rng = new([]);
        Assert.ThrowsExactly<ArgumentNullException>(() => rng.RandomString(5, (char[])null));
    }

    /// <summary>
    /// Verifies null alphabet validation for ranged lengths.
    /// </summary>
    [TestMethod]
    public void RandomString_NullCharArrayMinMax_Throws()
    {
        ScriptedRandom rng = new([]);
        Assert.ThrowsExactly<ArgumentNullException>(() => rng.RandomString(3, 8, (char[])null));
    }

    /// <summary>
    /// Verifies integration with the real seeded <see cref="Random"/> implementation.
    /// </summary>
    [TestMethod]
    public void RandomString_FixedLength_ReturnsCorrectLength()
    {
        Random rng = new(1);
        Assert.AreEqual(7, rng.RandomString(7).Length);
    }

    /// <summary>
    /// Verifies representative full-range single-precision bit patterns.
    /// </summary>
    [TestMethod]
    public void RandomFloat_ReinterpretsCompleteBitPattern()
    {
        float[] values = [-12.5f, 42.25f, float.PositiveInfinity, float.NaN];
        foreach (float expected in values)
        {
            float actual = new ScriptedRandom([], BitConverter.GetBytes(expected)).RandomFloat();
            if (float.IsNaN(expected))
                Assert.IsTrue(float.IsNaN(actual));
            else
                Assert.AreEqual(expected, actual);
        }
    }

    /// <summary>
    /// Verifies representative full-range double-precision bit patterns.
    /// </summary>
    [TestMethod]
    public void RandomDouble_ReinterpretsCompleteBitPattern()
    {
        double[] values = [-12.5, 42.25, double.NegativeInfinity, double.NaN];
        foreach (double expected in values)
        {
            double actual = new ScriptedRandom([], BitConverter.GetBytes(expected)).RandomDouble();
            if (double.IsNaN(expected))
                Assert.IsTrue(double.IsNaN(actual));
            else
                Assert.AreEqual(expected, actual);
        }
    }

    /// <summary>
    /// Supplies explicitly scripted integer choices and byte data to random extensions.
    /// </summary>
    private sealed class ScriptedRandom(int[] choices, byte[]? bytes = null) : Random
    {
        private int choiceIndex;

        /// <summary>
        /// Returns the next scripted choice after validating its requested range.
        /// </summary>
        public override int Next(int maxValue)
        {
            int value = choices[choiceIndex++];
            Assert.IsGreaterThanOrEqualTo(0, value);
            Assert.IsLessThan(maxValue, value);
            return value;
        }

        /// <summary>
        /// Returns a deterministic length for fixed-length requests.
        /// </summary>
        public override int Next(int minValue, int maxValue) => minValue;

        /// <summary>
        /// Copies the scripted binary pattern into the requested buffer.
        /// </summary>
        public override void NextBytes(byte[] buffer)
        {
            Assert.IsNotNull(bytes);
            CollectionAssert.AreEqual(new int[] { bytes.Length }, new int[] { buffer.Length });
            bytes.CopyTo(buffer, 0);
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.IO.BaseEncoding;

namespace UtilsTest.BaseEncoding;

/// <summary>
/// Tests the alphabet constraints and standard behavior of base descriptors.
/// </summary>
[TestClass]
public class BaseDescriptorTests
{
    /// <summary>
    /// Verifies exhaustively that alphabet lengths from zero through 257 accept only supported
    /// powers of two and that each accepted length produces the correct symbol bit width.
    /// </summary>
    [TestMethod]
    public void Constructor_LengthsZeroThrough257_AcceptsOnlySupportedPowersOfTwo()
    {
        for (int length = 0; length <= 257; length++)
        {
            char[] alphabet = Enumerable.Range(0, length).Select(value => (char)value).ToArray();
            bool isValid = length is >= 2 and <= 256 && (length & (length - 1)) == 0;

            if (!isValid)
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(
                    () => new TestBaseDescriptor(alphabet),
                    $"An alphabet containing {length} characters should be rejected.");
                continue;
            }

            var descriptor = new TestBaseDescriptor(alphabet);
            int expectedBitsWidth = 0;
            for (int remaining = length; remaining > 1; remaining >>= 1)
            {
                expectedBitsWidth++;
            }

            Assert.AreEqual(
                expectedBitsWidth,
                descriptor.BitsWidth,
                $"An alphabet containing {length} characters should use {expectedBitsWidth} bits per symbol.");
        }
    }

    /// <summary>
    /// Verifies that the predefined descriptors retain their established widths and conversions.
    /// </summary>
    [TestMethod]
    public void StandardDescriptors_RetainWidthsAndConversions()
    {
        byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };

        Assert.AreEqual(4, Bases.Base16.BitsWidth);
        Assert.AreEqual("4142434445", Bases.Base16.ToString(source));
        CollectionAssert.AreEqual(source, Bases.Base16.FromString("4142434445"));

        Assert.AreEqual(5, Bases.Base32.BitsWidth);
        Assert.AreEqual("IFBEGRCF", Bases.Base32.ToString(source));
        CollectionAssert.AreEqual(source, Bases.Base32.FromString("IFBEGRCF"));

        Assert.AreEqual(6, Bases.Base64.BitsWidth);
        Assert.AreEqual("QUJDREU=", Bases.Base64.ToString(source));
        CollectionAssert.AreEqual(source, Bases.Base64.FromString("QUJDREU="));
    }

    /// <summary>
    /// Provides a constructible descriptor for exercising protected base-class validation.
    /// </summary>
    private sealed class TestBaseDescriptor : BaseDescriptorBase
    {
        /// <summary>
        /// Initializes a descriptor with the supplied test alphabet.
        /// </summary>
        /// <param name="alphabet">The alphabet to validate.</param>
        public TestBaseDescriptor(char[] alphabet)
            : base(alphabet, "\n")
        {
        }
    }
}

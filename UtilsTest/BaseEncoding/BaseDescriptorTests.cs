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
            char[] alphabet = Enumerable.Range(0x100, length).Select(value => (char)value).ToArray();
            bool isValid = length is >= 2 and <= 256 && (length & (length - 1)) == 0;

            if (!isValid)
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(
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
    /// Verifies that both constructor paths report a null alphabet as the chars argument.
    /// </summary>
    [TestMethod]
    public void Constructor_NullAlphabet_ThrowsArgumentNullExceptionForChars()
    {
        ArgumentNullException stringException = Assert.ThrowsExactly<ArgumentNullException>(() => new TestBaseDescriptor((string)null!));
        ArgumentNullException arrayException = Assert.ThrowsExactly<ArgumentNullException>(() => new TestBaseDescriptor((char[])null!));

        Assert.AreEqual("chars", stringException.ParamName);
        Assert.AreEqual("chars", arrayException.ParamName);
    }

    /// <summary>
    /// Verifies that duplicate symbols are rejected explicitly regardless of their position.
    /// </summary>
    [DataTestMethod]
    [DataRow("AABC")]
    [DataRow("ABCA")]
    public void Constructor_DuplicateAlphabet_ThrowsArgumentException(string alphabet)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new TestBaseDescriptor(alphabet));

        Assert.AreEqual("chars", exception.ParamName);
        StringAssert.Contains(exception.Message, "duplicate");
    }

    /// <summary>
    /// Verifies that decoder-ignored separator and space characters cannot be alphabet symbols.
    /// </summary>
    [TestMethod]
    public void Constructor_AlphabetContainsIgnoredCharacter_ThrowsArgumentException()
    {
        Assert.AreEqual(1, new TestBaseDescriptor("AB", "").BitsWidth);
        Assert.ThrowsExactly<ArgumentException>(() => new TestBaseDescriptor("ABC-", "-"));
        Assert.ThrowsExactly<ArgumentException>(() => new TestBaseDescriptor("ABC ", ""));

        char platformSeparatorCharacter = Environment.NewLine[0];
        Assert.ThrowsExactly<ArgumentException>(() => new TestBaseDescriptor($"ABC{platformSeparatorCharacter}", null!));
    }

    /// <summary>
    /// Verifies that a filler cannot collide with data, separators, or ignored whitespace.
    /// </summary>
    [TestMethod]
    public void Constructor_FillerCollision_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new TestBaseDescriptor("ABCD", "", 'D', 4));
        Assert.ThrowsExactly<ArgumentException>(() => new TestBaseDescriptor("ABCD", "=\r\n", '=', 4));
        Assert.ThrowsExactly<ArgumentException>(() => new TestBaseDescriptor("ABCD", "", ' ', 4));
    }

    /// <summary>
    /// Verifies that an unused filler modulus remains compatible when no filler is configured.
    /// </summary>
    /// <param name="fillerMod">The historically accepted, inactive filler modulus.</param>
    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(8)]
    public void Constructor_WithoutFiller_PreservesInactiveFillerMod(int fillerMod)
    {
        var descriptor = new TestBaseDescriptor("ABCD", "", null, fillerMod);

        Assert.IsNull(descriptor.Filler);
        Assert.AreEqual(fillerMod, descriptor.FillerMod);
    }

    /// <summary>
    /// Verifies that an active filler requires the byte-aligned padding quantum.
    /// </summary>
    [TestMethod]
    public void Constructor_WithFillerAndIncoherentFillerMod_ThrowsArgumentOutOfRangeException()
    {
        foreach (int fillerMod in new int[] { -1, 0, 1, 3, 5 })
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TestBaseDescriptor("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/", "", '=', fillerMod));
    }

    /// <summary>
    /// Verifies the byte-aligned padding quantum for base-16-, base-32-, and base-64-like descriptors.
    /// </summary>
    [TestMethod]
    public void Constructor_ByteAlignedFillerMods_AreAccepted()
    {
        Assert.AreEqual(2, new TestBaseDescriptor("0123456789ABCDEF", "", '=', 2).FillerMod);
        Assert.AreEqual(8, new TestBaseDescriptor("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567", "", '=', 8).FillerMod);
        Assert.AreEqual(4, new TestBaseDescriptor("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/", "", '=', 4).FillerMod);
    }

    /// <summary>
    /// Verifies that valid custom padded descriptors round-trip every tested payload in strict mode.
    /// </summary>
    [TestMethod]
    public void CustomPaddedDescriptors_RoundTripPayloadLengthsZeroThrough32()
    {
        TestBaseDescriptor[] descriptors =
        {
            new("0123456789ABCDEF", "", '=', 2),
            new("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567", "", '=', 8),
            new("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/", "", '=', 4)
        };

        foreach (TestBaseDescriptor descriptor in descriptors)
        {
            for (int length = 0; length <= 32; length++)
            {
                byte[] source = Enumerable.Range(0, length).Select(value => (byte)(value * 17)).ToArray();
                CollectionAssert.AreEqual(source, descriptor.FromString(descriptor.ToString(source)));
            }
        }
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
        /// <param name="separator">The separator to use, or <see langword="null"/> for the platform newline.</param>
        /// <param name="filler">The optional padding character.</param>
        /// <param name="fillerMod">The padding quantum.</param>
        public TestBaseDescriptor(char[] alphabet, string separator = "", char? filler = null, int fillerMod = 0)
            : base(alphabet, separator, filler, fillerMod)
        {
        }

        /// <summary>
        /// Initializes a descriptor with the supplied string alphabet and formatting configuration.
        /// </summary>
        /// <param name="alphabet">The alphabet to validate.</param>
        /// <param name="separator">The separator to use, or <see langword="null"/> for the platform newline.</param>
        /// <param name="filler">The optional padding character.</param>
        /// <param name="fillerMod">The padding quantum.</param>
        public TestBaseDescriptor(string alphabet, string separator = "", char? filler = null, int fillerMod = 0)
            : base(alphabet, separator, filler, fillerMod)
        {
        }
    }
}

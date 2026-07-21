using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Utils.Security;

namespace Utils.Tests.Security;

[TestClass]
public class AuthenticatorTests
{
    // A key that meets the minimum 16-byte requirement.
    private static readonly byte[] ValidKey =
        [0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30,
         0x41, 0x42, 0x43, 0x44, 0x45, 0x46];

    [TestMethod]
    public void ComputeAuthenticator_ShouldReturnValidCode()
    {
        var authenticator = new Authenticator("HMACSHA256", ValidKey, 6, 30);
        string code = authenticator.ComputeAuthenticator();
        Assert.IsNotNull(code);
        Assert.AreEqual(6, code.Length);
        // Must be all digits.
        foreach (char c in code)
            Assert.IsTrue(char.IsAsciiDigit(c), $"Character '{c}' is not a digit.");
    }

    [TestMethod]
    public void VerifyAuthenticator_ShouldReturnTrueForValidCode()
    {
        var authenticator = new Authenticator("HMACSHA256", ValidKey, 6, 30);
        string validCode = authenticator.ComputeAuthenticator();
        bool result = authenticator.VerifyAuthenticator(1, validCode);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void VerifyAuthenticator_ShouldReturnFalseForInvalidCode()
    {
        var authenticator = new Authenticator("HMACSHA256", ValidKey, 6, 30);
        string validCode = authenticator.ComputeAuthenticator();
        // Use a code that is extremely unlikely to match the current code.
        bool result = authenticator.VerifyAuthenticator(1, "000000");
        // Note: there is a 1-in-1,000,000 chance this can produce a false positive;
        // acceptable for a unit test.
        bool couldAlsoBeValid = validCode == "000000";
        if (!couldAlsoBeValid)
            Assert.IsFalse(result);
    }

    [TestMethod]
    public void VerifyAuthenticator_ShouldReturnFalseForCodeWithInvalidLength()
    {
        var authenticator = new Authenticator("HMACSHA256", ValidKey, 6, 30);
        bool result = authenticator.VerifyAuthenticator(1, "12345");
        Assert.IsFalse(result);
    }

    // ------------------------------------------------------------------ #29 parameter validation

    [TestMethod]
    public void Constructor_ThrowsOnZeroDigits()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Authenticator("HMACSHA256", ValidKey, 0, 30));
    }

    [TestMethod]
    public void Constructor_ThrowsOnDigits10()
    {
        // 10 digits would require modulus 10_000_000_000 which exceeds int.MaxValue.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Authenticator("HMACSHA256", ValidKey, 10, 30));
    }

    [TestMethod]
    public void Constructor_ThrowsOnDigitsAbove9()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Authenticator("HMACSHA256", ValidKey, 11, 30));
    }

    [TestMethod]
    public void Constructor_Accepts9Digits()
    {
        var auth = new Authenticator("HMACSHA256", ValidKey, 9, 30);
        string code = auth.ComputeAuthenticator(12345L);
        Assert.AreEqual(9, code.Length);
        foreach (char c in code)
            Assert.IsTrue(char.IsAsciiDigit(c));
    }

    [TestMethod]
    public void Constructor_ThrowsOnZeroIntervalLength()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Authenticator("HMACSHA256", ValidKey, 6, 0));
    }

    [TestMethod]
    public void Constructor_ThrowsOnNegativeIntervalLength()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Authenticator("HMACSHA256", ValidKey, 6, -1));
    }

    [TestMethod]
    public void Constructor_ThrowsOnShortKey()
    {
        byte[] shortKey = [0x01, 0x02, 0x03]; // Only 3 bytes — below minimum.
        Assert.ThrowsExactly<ArgumentException>(() => new Authenticator("HMACSHA256", shortKey, 6, 30));
    }

    [TestMethod]
    public void VerifyAuthenticator_ThrowsOnNegativeRange()
    {
        var authenticator = new Authenticator("HMACSHA256", ValidKey, 6, 30);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => authenticator.VerifyAuthenticator(-1, "123456"));
    }

    // ------------------------------------------------------------------ #30 key isolation (mutation after construction)

    [TestMethod]
    public void KeyMutationAfterConstruction_DoesNotAffectGeneratedCodes()
    {
        byte[] key = (byte[])ValidKey.Clone();
        var authenticator = new Authenticator("HMACSHA256", key, 6, 30);
        long msg = authenticator.CurrentMessage;
        string codeBefore = authenticator.ComputeAuthenticator(msg);

        // Mutate the original array.
        Array.Fill(key, (byte)0xFF);

        string codeAfter = authenticator.ComputeAuthenticator(msg);
        Assert.AreEqual(codeBefore, codeAfter, "Key mutation after construction must not affect generated codes.");
    }

    // ------------------------------------------------------------------ #31 concurrency safety

    [TestMethod]
    public void ConcurrentComputations_ProduceSameResultAsIsolatedInstances()
    {
        var auth = new Authenticator("HMACSHA256", ValidKey, 6, 30);
        long msg = 100_000L; // Fixed counter for determinism.
        string expected = auth.ComputeAuthenticator(msg);
        const int threadCount = 20;
        var results = new ConcurrentBag<string>();

        Parallel.For(0, threadCount, _ => results.Add(auth.ComputeAuthenticator(msg)));

        foreach (string result in results)
            Assert.AreEqual(expected, result, "Concurrent computations must produce identical codes.");
    }

    // ------------------------------------------------------------------ #33 algorithm name validation

    [TestMethod]
    public void Constructor_ThrowsForUnknownAlgorithmName()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Authenticator("NONSENSE_ALGO", ValidKey, 6, 30));
    }

    [TestMethod]
    public void Constructor_ThrowsForNonHmacAlgorithmName()
    {
        // SHA256 is a valid algorithm but is not an HMAC.
        Assert.ThrowsExactly<ArgumentException>(() => new Authenticator("SHA256", ValidKey, 6, 30));
    }

    // ------------------------------------------------------------------ #34 GoogleAuthenticator factory uses SHA-1 (standard TOTP profile)

    [TestMethod]
    public void GoogleAuthenticator_UsesHmacSha1Profile()
    {
        var auth = Authenticator.GoogleAuthenticator(ValidKey);
        Assert.AreEqual("HMACSHA1", auth.AlgorithmName);
        Assert.AreEqual(6, auth.Digits);
        Assert.AreEqual(30, auth.IntervalLength);
    }

    [TestMethod]
    public void GoogleAuthenticatorSha256_UsesSha256Profile()
    {
        var auth = Authenticator.GoogleAuthenticatorSha256(ValidKey);
        Assert.AreEqual("HMACSHA256", auth.AlgorithmName);
    }

    // ------------------------------------------------------------------ #35 digit-only verification

    [TestMethod]
    public void VerifyAuthenticator_ReturnsFalseForNonDigitCode()
    {
        var authenticator = new Authenticator("HMACSHA256", ValidKey, 6, 30);
        bool result = authenticator.VerifyAuthenticator(1, "12a456");
        Assert.IsFalse(result, "Non-digit characters in the code must return false without attempting HMAC.");
    }

    [TestMethod]
    public void VerifyAuthenticator_ReturnsFalseForNullCode()
    {
        var authenticator = new Authenticator("HMACSHA256", ValidKey, 6, 30);
        bool result = authenticator.VerifyAuthenticator(1, null!);
        Assert.IsFalse(result);
    }
}

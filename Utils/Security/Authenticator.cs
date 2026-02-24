using System;
using System.Security.Cryptography;
using System.Text;
using Utils.Dates;

namespace Utils.Security;

/// <summary>
/// Provides a way to generate and verify time-based one-time passwords (TOTP), compatible with Google Authenticator and similar services.
/// </summary>
public class Authenticator
{
    /// <summary>
    /// The number of digits in the generated code.
    /// </summary>
    public int Digits { get; }

    /// <summary>
    /// The HMAC algorithm used for generating the hash.
    /// </summary>
    public HMAC Algorithm { get; }

    /// <summary>
    /// The key used for the HMAC algorithm.
    /// </summary>
    public byte[] Key { get; }

    /// <summary>
    /// The length of time, in seconds, that each generated code is valid for.
    /// </summary>
    public int IntervalLength { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Authenticator"/> class.
    /// </summary>
    /// <param name="algorithm">The name of the HMAC algorithm to use (e.g., "HMACSHA256").</param>
    /// <param name="key">The binary key used for generating the hash.</param>
    /// <param name="digits">The number of digits in the generated code.</param>
    /// <param name="intervalLength">The duration, in seconds, for which each code is valid.</param>
    public Authenticator(string algorithm, byte[] key, int digits, int intervalLength)
        : this((HMAC)CryptoConfig.CreateFromName(algorithm), key, digits, intervalLength) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Authenticator"/> class.
    /// </summary>
    /// <param name="algorithm">The HMAC algorithm used for generating the hash.</param>
    /// <param name="key">The binary key used for generating the hash.</param>
    /// <param name="digits">The number of digits in the generated code.</param>
    /// <param name="intervalLength">The duration, in seconds, for which each code is valid.</param>
    public Authenticator(HMAC algorithm, byte[] key, int digits, int intervalLength)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        ArgumentNullException.ThrowIfNull(key);
        Digits = digits;
        Algorithm = algorithm;
        Key = key;
        IntervalLength = intervalLength;
    }

    /// <summary>
    /// Creates an authenticator compatible with Google Authenticator.
    /// </summary>
    /// <param name="key">The binary key used for generating the hash.</param>
    /// <returns>An instance of <see cref="Authenticator"/> configured for Google Authenticator.</returns>
    public static Authenticator GoogleAuthenticator(byte[] key) => new Authenticator("HMACSHA256", key, 6, 30);

    /// <summary>
    /// Computes the current authentication code based on the current timestamp.
    /// </summary>
    /// <returns>The current one-time password.</returns>
    public string ComputeAuthenticator() => ComputeAuthenticator(CurrentMessage);

    /// <summary>
    /// Computes the authentication code based on a specific numeric message.
    /// </summary>
    /// <param name="message">The numeric message, typically a timestamp.</param>
    /// <returns>The computed one-time password.</returns>
    public string ComputeAuthenticator(long message)
    {
        byte[] byteMessage = BitConverter.GetBytes(message);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(byteMessage);
        }
        return ComputeAuthenticator(byteMessage);
    }

    /// <summary>
    /// Computes the authentication code based on a binary message.
    /// </summary>
    /// <param name="message">The binary message, typically a timestamp.</param>
    /// <returns>The computed one-time password.</returns>
    public string ComputeAuthenticator(byte[] message)
    {
        Algorithm.Key = Key;
        var hash = Algorithm.ComputeHash(message);

        // Calculate the offset
        int offset = hash[^1] & 0xf;

        // Get a 4-byte chunk from the hash
        var selectedBytes = new byte[sizeof(int)];
        Buffer.BlockCopy(hash, offset, selectedBytes, 0, sizeof(int));

        if (BitConverter.IsLittleEndian)
        {
            // Convert bytes to big-endian format
            Array.Reverse(selectedBytes);
        }

        int selectedInteger = BitConverter.ToInt32(selectedBytes, 0);
        // Remove the most significant bit for interoperability
        int binary = selectedInteger & 0x7FFFFFFF;

        // Compute the one-time password (OTP)
        int otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString(new string('0', Digits));
    }

    /// <summary>
    /// Verifies the authentication code by checking the current code and adjacent codes within the specified range.
    /// </summary>
    /// <param name="range">The number of previous and future codes to check.</param>
    /// <param name="code">The code to verify.</param>
    /// <returns><see langword="true"/> if the code is valid; otherwise, <see langword="false"/>.</returns>
    public bool VerifyAuthenticator(int range, string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != Digits)
        {
            return false;
        }

        long baseMessage = CurrentMessage;

        for (long i = 0; i <= range; i++)
        {
            if (SecureEquals(code, ComputeAuthenticator(baseMessage + i)) || SecureEquals(code, ComputeAuthenticator(baseMessage - i - 1)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Compares two authentication codes using a fixed-time operation to reduce timing side channels.
    /// </summary>
    /// <param name="left">First code to compare.</param>
    /// <param name="right">Second code to compare.</param>
    /// <returns><see langword="true"/> when both codes are identical; otherwise, <see langword="false"/>.</returns>
    private static bool SecureEquals(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    /// <summary>
    /// Gets the current message used to generate the code, typically based on the Unix timestamp.
    /// </summary>
    public long CurrentMessage => DateTime.UtcNow.ToUnixTimeStamp() / IntervalLength;
}

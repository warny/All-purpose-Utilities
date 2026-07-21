using System;
using System.Security.Cryptography;
using System.Text;
using Utils.Dates;

namespace Utils.Security;

/// <summary>
/// Provides a way to generate and verify time-based one-time passwords (TOTP), compatible
/// with Google Authenticator and similar services.
/// </summary>
/// <remarks>
/// <para>
/// This class is safe for concurrent use: each HMAC computation creates a fresh, keyed
/// algorithm instance that is disposed after use (#31, #32).
/// </para>
/// <para>
/// The secret key is copied at construction time and is never exposed through a public
/// property (#30). To export the key for provisioning purposes, call <see cref="ExportKey"/>.
/// </para>
/// </remarks>
public class Authenticator
{
    // Pre-computed power-of-ten table to avoid int overflow from Math.Pow (#29).
    // The HOTP truncation step yields a 31-bit value (max 2 147 483 647), which fits at
    // most 9 significant decimal digits. Supporting 10 digits would require a modulus of
    // 10 000 000 000 that exceeds int.MaxValue and produces non-uniform distribution.
    // Digits is therefore capped at 9 (#29).
    private static readonly int[] _powersOfTen = [1, 10, 100, 1_000, 10_000, 100_000,
        1_000_000, 10_000_000, 100_000_000, 1_000_000_000];

    // Minimum key lengths per algorithm (bytes). Shorter keys violate RFC 6238 guidance.
    private const int MinKeyLength = 16;

    /// <summary>
    /// The number of digits in the generated code.
    /// </summary>
    public int Digits { get; }

    /// <summary>
    /// The name of the HMAC algorithm used for generating the hash (e.g., "HMACSHA256").
    /// </summary>
    public string AlgorithmName { get; }

    /// <summary>
    /// The length of time, in seconds, that each generated code is valid for.
    /// </summary>
    public int IntervalLength { get; }

    /// <summary>
    /// A private, defensive copy of the key. Never exposed directly (#30).
    /// </summary>
    private readonly byte[] _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="Authenticator"/> class.
    /// </summary>
    /// <param name="algorithmName">
    /// The name of the HMAC algorithm to use (e.g., <c>"HMACSHA256"</c>, <c>"HMACSHA1"</c>).
    /// The name is resolved through <see cref="CryptoConfig"/>; unknown or non-HMAC names
    /// throw <see cref="ArgumentException"/> (#33).
    /// </param>
    /// <param name="key">
    /// The binary secret key. Must be at least <c>16</c> bytes. A defensive copy is made;
    /// mutating the original array after construction has no effect (#30).
    /// </param>
    /// <param name="digits">
    /// The number of digits in the generated code. Must be between 1 and 9 inclusive.
    /// Values above 9 are not supported: the HOTP truncation step yields a 31-bit value
    /// whose maximum (2 147 483 647) has only 10 digits but the required modulus of
    /// 10 000 000 000 exceeds <see cref="int.MaxValue"/> (#29).
    /// </param>
    /// <param name="intervalLength">
    /// The duration in seconds for which each code is valid. Must be positive (#29).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="algorithmName"/> or <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="algorithmName"/> does not resolve to a supported HMAC algorithm (#33),
    /// or when <paramref name="key"/> is shorter than the minimum length.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="digits"/> is not in [1, 9] or <paramref name="intervalLength"/> is not positive (#29).
    /// </exception>
    public Authenticator(string algorithmName, byte[] key, int digits, int intervalLength)
    {
        ArgumentNullException.ThrowIfNull(algorithmName);
        ArgumentNullException.ThrowIfNull(key);

        if (digits < 1 || digits > 9)
            throw new ArgumentOutOfRangeException(nameof(digits), digits, "Digits must be between 1 and 9.");

        if (intervalLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalLength), intervalLength, "IntervalLength must be positive.");

        if (key.Length < MinKeyLength)
            throw new ArgumentException($"Key must be at least {MinKeyLength} bytes.", nameof(key));

        // Validate the algorithm name by creating and immediately disposing a test instance (#33).
        object? candidate = CryptoConfig.CreateFromName(algorithmName);
        if (candidate is not HMAC testHmac)
        {
            (candidate as IDisposable)?.Dispose();
            throw new ArgumentException(
                $"'{algorithmName}' does not resolve to a supported HMAC algorithm.", nameof(algorithmName));
        }
        testHmac.Dispose();

        AlgorithmName = algorithmName;
        Digits = digits;
        IntervalLength = intervalLength;
        _key = (byte[])key.Clone(); // defensive copy (#30)
    }

    /// <summary>
    /// Returns a defensive copy of the secret key for provisioning or export purposes.
    /// </summary>
    /// <returns>A copy of the secret key bytes.</returns>
    public byte[] ExportKey() => (byte[])_key.Clone();

    /// <summary>
    /// Creates an authenticator configured with the standard TOTP profile:
    /// HMAC-SHA1, 6 digits, 30-second window (RFC 6238 / default Google Authenticator profile).
    /// </summary>
    /// <param name="key">The binary secret key. Must be at least <c>16</c> bytes.</param>
    /// <returns>An <see cref="Authenticator"/> compatible with default TOTP provisioning.</returns>
    /// <remarks>
    /// The previous factory used HMAC-SHA256 which does not match the default RFC 6238 profile
    /// and would produce codes that don't match a normally provisioned Google Authenticator
    /// account (#34). Use <see cref="GoogleAuthenticatorSha256"/> if SHA-256 is required.
    /// </remarks>
    public static Authenticator GoogleAuthenticator(byte[] key) => new("HMACSHA1", key, 6, 30);

    /// <summary>
    /// Creates an authenticator using HMAC-SHA256, 6 digits, 30-second window.
    /// This profile requires explicit provisioning metadata — it does not match the
    /// default Google Authenticator TOTP profile (#34).
    /// </summary>
    /// <param name="key">The binary secret key. Must be at least <c>16</c> bytes.</param>
    /// <returns>An <see cref="Authenticator"/> using the SHA-256 profile.</returns>
    public static Authenticator GoogleAuthenticatorSha256(byte[] key) => new("HMACSHA256", key, 6, 30);

    /// <summary>
    /// Computes the current authentication code based on the current UTC timestamp.
    /// </summary>
    /// <returns>The current one-time password.</returns>
    public string ComputeAuthenticator() => ComputeAuthenticator(CurrentMessage);

    /// <summary>
    /// Computes the authentication code for a specific counter value.
    /// </summary>
    /// <param name="message">The counter value (typically a timestamp divided by the interval).</param>
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
    /// Computes the authentication code from a binary message.
    /// </summary>
    /// <param name="message">The binary message.</param>
    /// <returns>The computed one-time password.</returns>
    public string ComputeAuthenticator(byte[] message)
    {
        // Create a fresh keyed HMAC instance per call to avoid shared state across threads (#31, #32).
        using HMAC hmac = (HMAC)CryptoConfig.CreateFromName(AlgorithmName)!;
        hmac.Key = (byte[])_key.Clone(); // copy so the HMAC cannot mutate our stored key

        var hash = hmac.ComputeHash(message);

        // Calculate the offset
        int offset = hash[^1] & 0xf;

        // Get a 4-byte chunk from the hash
        var selectedBytes = new byte[sizeof(int)];
        Buffer.BlockCopy(hash, offset, selectedBytes, 0, sizeof(int));

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(selectedBytes);
        }

        int selectedInteger = BitConverter.ToInt32(selectedBytes, 0);
        // Remove the most significant bit for interoperability
        int binary = selectedInteger & 0x7FFFFFFF;

        // Compute the OTP using the pre-computed power table to avoid int overflow (#29).
        int otp = binary % _powersOfTen[Digits];
        return otp.ToString(new string('0', Digits));
    }

    /// <summary>
    /// Verifies the authentication code by checking the current code and adjacent codes
    /// within the specified window.
    /// </summary>
    /// <param name="range">
    /// The number of previous and future counter windows to accept. Must be non-negative (#29).
    /// </param>
    /// <param name="code">The code to verify. Must consist of exactly <see cref="Digits"/> ASCII digits (#35).</param>
    /// <returns><see langword="true"/> if the code is valid within the window; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="range"/> is negative (#29).
    /// </exception>
    public bool VerifyAuthenticator(int range, string code)
    {
        if (range < 0)
            throw new ArgumentOutOfRangeException(nameof(range), range, "Range must be non-negative.");

        if (string.IsNullOrEmpty(code) || code.Length != Digits)
            return false;

        // Reject non-digit characters before performing HMAC operations (#35).
        foreach (char c in code)
        {
            if (c < '0' || c > '9')
                return false;
        }

        long baseMessage = CurrentMessage;

        for (long i = 0; i <= range; i++)
        {
            // Use checked arithmetic to prevent counter overflow on extreme ranges (#35).
            checked
            {
                if (SecureEquals(code, ComputeAuthenticator(baseMessage + i)))
                    return true;
                if (i > 0 && SecureEquals(code, ComputeAuthenticator(baseMessage - i)))
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
            return false;

        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    /// <summary>
    /// Gets the current TOTP counter value derived from the UTC clock.
    /// </summary>
    public long CurrentMessage => DateTime.UtcNow.ToUnixTimeStamp() / IntervalLength;
}

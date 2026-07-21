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
/// This class is safe for concurrent use: the <see cref="_algorithmFactory"/> is invoked
/// on each computation to produce a fresh, privately owned <see cref="HMAC"/> instance
/// that is disposed immediately after use (#31, #32).
/// </para>
/// <para>
/// The secret key is copied at construction time and is never exposed through a public
/// property (#30). To export the key for provisioning, call <see cref="ExportKey"/>.
/// </para>
/// </remarks>
public sealed class Authenticator
{
    // Pre-computed power-of-ten table to avoid int overflow from Math.Pow (#29).
    // The HOTP truncation step yields a 31-bit value (max 2 147 483 647), which fits at
    // most 9 significant decimal digits. Supporting 10 digits would require a modulus of
    // 10 000 000 000 that exceeds int.MaxValue and produces non-uniform distribution.
    // Digits is therefore capped at 9 (#29).
    private static readonly int[] _powersOfTen = [1, 10, 100, 1_000, 10_000, 100_000,
        1_000_000, 10_000_000, 100_000_000, 1_000_000_000];

    // Minimum key length (bytes). Shorter keys violate RFC 6238 guidance.
    private const int MinKeyLength = 16;

    /// <summary>
    /// Factory that produces a fresh, unkeyed <see cref="HMAC"/> instance on each invocation.
    /// </summary>
    /// <remarks>
    /// Contract requirements for caller-supplied factories:
    /// <list type="bullet">
    ///   <item>Must return a new, independent instance on every call.</item>
    ///   <item>Must never return <see langword="null"/>.</item>
    ///   <item>Must not return a shared or stateful instance.</item>
    /// </list>
    /// </remarks>
    private readonly Func<HMAC> _algorithmFactory;

    /// <summary>A private, defensive copy of the key. Never exposed directly (#30).</summary>
    private readonly byte[] _key;

    /// <summary>Gets the number of digits in the generated code.</summary>
    public int Digits { get; }

    /// <summary>Gets the duration in seconds for which each code is valid.</summary>
    public int IntervalLength { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="Authenticator"/> using a named HMAC algorithm.
    /// </summary>
    /// <param name="algorithmName">
    /// The algorithm name resolved through <see cref="CryptoConfig"/> (e.g. <c>"HMACSHA1"</c>,
    /// <c>"HMACSHA256"</c>). Throws if the name is blank or does not resolve to an
    /// <see cref="HMAC"/> (#33).
    /// </param>
    /// <param name="key">
    /// The binary secret key. Must be at least <c>16</c> bytes. A defensive copy is made;
    /// mutating the original array after construction has no effect (#30).
    /// </param>
    /// <param name="digits">
    /// The number of digits in the generated code. Must be in [1, 9]. Values above 9 are not
    /// supported because the HOTP 31-bit truncation step cannot produce a uniform 10-digit
    /// distribution within <see cref="int"/> range (#29).
    /// </param>
    /// <param name="intervalLength">
    /// The duration in seconds for which each code is valid. Must be positive (#29).
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="algorithmName"/> is null, blank, or does not resolve to an HMAC algorithm,
    /// or <paramref name="key"/> is shorter than the minimum length.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="digits"/> is not in [1, 9] or <paramref name="intervalLength"/> is not positive.
    /// </exception>
    public Authenticator(string algorithmName, byte[] key, int digits, int intervalLength)
        : this(CreateFactory(algorithmName), key, digits, intervalLength)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Authenticator"/> using a caller-supplied
    /// algorithm factory. Prefer this overload when using custom or hardware-backed HMAC
    /// implementations.
    /// </summary>
    /// <param name="algorithmFactory">
    /// A factory that returns a fresh, unkeyed <see cref="HMAC"/> instance on each call.
    /// See <see cref="_algorithmFactory"/> for the full contract. The factory is invoked
    /// once at construction time to validate that it produces a non-null instance.
    /// </param>
    /// <param name="key">
    /// The binary secret key. Must be at least <c>16</c> bytes. A defensive copy is made (#30).
    /// </param>
    /// <param name="digits">Number of digits, [1, 9] (#29).</param>
    /// <param name="intervalLength">Validity window in seconds. Must be positive (#29).</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="algorithmFactory"/> or <paramref name="key"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The factory returned <see langword="null"/>, or <paramref name="key"/> is shorter than
    /// the minimum required length.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="digits"/> is not in [1, 9] or <paramref name="intervalLength"/> is not positive.
    /// </exception>
    public Authenticator(Func<HMAC> algorithmFactory, byte[] key, int digits, int intervalLength)
    {
        ArgumentNullException.ThrowIfNull(algorithmFactory);
        ArgumentNullException.ThrowIfNull(key);

        if (digits < 1 || digits > 9)
            throw new ArgumentOutOfRangeException(nameof(digits), digits, "Digits must be between 1 and 9.");

        if (intervalLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalLength), intervalLength,
                "IntervalLength must be positive.");

        if (key.Length < MinKeyLength)
            throw new ArgumentException($"Key must be at least {MinKeyLength} bytes.", nameof(key));

        // Validate that the factory produces a usable instance at construction time.
        using HMAC test = algorithmFactory()
            ?? throw new ArgumentException(
                "The algorithm factory returned null.", nameof(algorithmFactory));

        _algorithmFactory = algorithmFactory;
        _key = (byte[])key.Clone(); // defensive copy (#30)
        Digits = digits;
        IntervalLength = intervalLength;
    }

    /// <summary>
    /// Returns a defensive copy of the secret key for provisioning or export purposes.
    /// </summary>
    public byte[] ExportKey() => (byte[])_key.Clone();

    /// <summary>
    /// Creates an authenticator configured with the standard TOTP profile:
    /// HMAC-SHA1, 6 digits, 30-second window (RFC 6238 / default Google Authenticator profile).
    /// </summary>
    /// <param name="key">The binary secret key. Must be at least <c>16</c> bytes.</param>
    /// <remarks>
    /// The previous factory used HMAC-SHA256, which does not match the default RFC 6238
    /// profile and would produce codes that do not match a normally provisioned Google
    /// Authenticator account (#34). Use <see cref="GoogleAuthenticatorSha256"/> for SHA-256.
    /// </remarks>
    public static Authenticator GoogleAuthenticator(byte[] key)
        => new(() => new HMACSHA1(), key, 6, 30);

    /// <summary>
    /// Creates an authenticator using HMAC-SHA256, 6 digits, 30-second window.
    /// This profile requires explicit provisioning metadata — it does not match the
    /// default Google Authenticator TOTP profile (#34).
    /// </summary>
    /// <param name="key">The binary secret key. Must be at least <c>16</c> bytes.</param>
    public static Authenticator GoogleAuthenticatorSha256(byte[] key)
        => new(() => new HMACSHA256(), key, 6, 30);

    /// <summary>
    /// Computes the current authentication code based on the current UTC timestamp.
    /// </summary>
    public string ComputeAuthenticator() => ComputeAuthenticator(CurrentMessage);

    /// <summary>
    /// Computes the authentication code for a specific counter value.
    /// </summary>
    /// <param name="message">The counter value (typically a timestamp divided by the interval).</param>
    public string ComputeAuthenticator(long message)
    {
        byte[] byteMessage = BitConverter.GetBytes(message);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(byteMessage);
        return ComputeAuthenticator(byteMessage);
    }

    /// <summary>
    /// Computes the authentication code from a binary message.
    /// </summary>
    /// <param name="message">The binary message.</param>
    public string ComputeAuthenticator(byte[] message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Fresh instance per call — thread-safe, no shared mutable state (#31, #32).
        using HMAC hmac = _algorithmFactory()
            ?? throw new InvalidOperationException(
                "The algorithm factory returned null. The factory must return a new HMAC instance on every call.");
        hmac.Key = _key;

        byte[] hash = hmac.ComputeHash(message);

        int offset = hash[^1] & 0xf;
        byte[] selectedBytes = new byte[sizeof(int)];
        Buffer.BlockCopy(hash, offset, selectedBytes, 0, sizeof(int));
        if (BitConverter.IsLittleEndian)
            Array.Reverse(selectedBytes);

        int binary = BitConverter.ToInt32(selectedBytes, 0) & 0x7FFFFFFF;

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
    /// <param name="code">
    /// The code to verify. Must consist of exactly <see cref="Digits"/> ASCII digit characters (#35).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the code is valid within the window; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="range"/> is negative.
    /// </exception>
    public bool VerifyAuthenticator(int range, string code)
    {
        if (range < 0)
            throw new ArgumentOutOfRangeException(nameof(range), range, "Range must be non-negative.");

        if (string.IsNullOrEmpty(code) || code.Length != Digits)
            return false;

        // Reject non-digit characters before performing any HMAC operations (#35).
        foreach (char c in code)
        {
            if (c < '0' || c > '9')
                return false;
        }

        long baseMessage = CurrentMessage;

        for (long i = 0; i <= range; i++)
        {
            // Checked arithmetic prevents counter overflow on extreme ranges (#35).
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

    /// <summary>
    /// Creates a <see cref="CryptoConfig"/>-based factory for the given algorithm name,
    /// validating the name eagerly so the string constructor throws <see cref="ArgumentException"/>
    /// rather than a deferred <see cref="CryptographicException"/> (#33).
    /// </summary>
    /// <param name="algorithmName">The HMAC algorithm name.</param>
    /// <returns>A factory that produces a fresh <see cref="HMAC"/> instance on each call.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="algorithmName"/> is null, whitespace, or does not resolve to an HMAC algorithm.
    /// </exception>
    private static Func<HMAC> CreateFactory(string algorithmName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithmName);

        // Validate eagerly: create and immediately dispose a probe instance so the string
        // constructor can throw ArgumentException with the caller's parameter name (#33).
        object? candidate = CryptoConfig.CreateFromName(algorithmName);
        if (candidate is not HMAC probe)
        {
            (candidate as IDisposable)?.Dispose();
            throw new ArgumentException(
                $"'{algorithmName}' does not resolve to a supported HMAC algorithm.",
                nameof(algorithmName));
        }
        probe.Dispose();

        // The lambda is the hot path: called once per computation.
        return () =>
            CryptoConfig.CreateFromName(algorithmName) as HMAC
            ?? throw new CryptographicException(
                $"'{algorithmName}' does not resolve to an HMAC algorithm.");
    }
}

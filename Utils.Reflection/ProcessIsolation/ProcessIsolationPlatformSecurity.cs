using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Controls whether and how certificate revocation is verified during an Authenticode check.
/// </summary>
public enum AuthenticodeRevocationPolicy
{
    /// <summary>
    /// No revocation check is performed. This is the fastest option but will accept a file signed
    /// with a compromised or expired certificate.
    /// </summary>
    None,
    /// <summary>
    /// Revocation is checked against locally cached CRL and OCSP data only; no network request is
    /// made. The check succeeds when the cache is fresh, and
    /// <see cref="AuthenticodeVerificationResult.Revocation"/> is
    /// <see cref="RevocationStatus.Offline"/> when the local cache is absent or stale.
    /// </summary>
    CacheOnly,
    /// <summary>
    /// Revocation is checked online (CRL/OCSP). A live revocation download is attempted; when the
    /// revocation server cannot be reached, <see cref="AuthenticodeVerificationResult.Revocation"/>
    /// is <see cref="RevocationStatus.Offline"/> rather than a hard failure.
    /// </summary>
    Online,
}

/// <summary>
/// Revocation status of the signing certificate returned by
/// <see cref="ProcessIsolationPlatformSecurity.VerifyAuthenticodeSignature"/>.
/// </summary>
public enum RevocationStatus
{
    /// <summary>Revocation was not requested (<see cref="AuthenticodeRevocationPolicy.None"/>).</summary>
    NotChecked,
    /// <summary>The signing certificate is not revoked.</summary>
    Valid,
    /// <summary>The signing certificate or a CA in the chain has been explicitly revoked.</summary>
    Revoked,
    /// <summary>
    /// The revocation service could not be reached; revocation status is unknown. The signature
    /// itself and the chain are considered valid when this status is reported.
    /// </summary>
    Offline,
}

/// <summary>
/// Structured result from <see cref="ProcessIsolationPlatformSecurity.VerifyAuthenticodeSignature"/>,
/// separating signature presence, chain trust, and revocation status.
/// </summary>
public readonly struct AuthenticodeVerificationResult
{
    /// <summary>The file carries an Authenticode signature block that can be decoded.</summary>
    public bool IsSigned { get; init; }

    /// <summary>
    /// The certificate chain is cryptographically valid and leads to a trusted root.
    /// <see langword="false"/> when the signature is forged, the chain is broken, or the root is
    /// not in the trusted-root store.
    /// </summary>
    public bool IsChainTrusted { get; init; }

    /// <summary>Revocation status of the signing certificate (and its CA chain).</summary>
    public RevocationStatus Revocation { get; init; }

    /// <summary>
    /// <see langword="true"/> when the file is signed, the chain is trusted, and the certificate
    /// is not revoked. A <see cref="RevocationStatus.Offline"/> or
    /// <see cref="RevocationStatus.NotChecked"/> status is treated as non-revoked here; callers
    /// with stricter requirements should inspect <see cref="Revocation"/> directly.
    /// </summary>
    public bool IsValid =>
        IsSigned && IsChainTrusted && Revocation != RevocationStatus.Revoked;
}

/// <summary>
/// Exposes standardized security helpers used by process-container consumers.
/// </summary>
public static class ProcessIsolationPlatformSecurity
{
    /// <summary>
    /// Verifies that the process connected to a named pipe is the expected process.
    /// </summary>
    /// <remarks>
    /// <b>Windows-only verification.</b> There is no built-in .NET API to resolve the peer
    /// process of a Unix domain socket (the underlying transport for
    /// <see cref="NamedPipeServerStream"/> on Linux/macOS) — doing so would require P/Invoking
    /// <c>getsockopt(SO_PEERCRED)</c> (Linux) or <c>getpeereid</c> (macOS/BSD), which this library
    /// does not currently implement. On non-Windows platforms this method therefore always
    /// returns <see langword="true"/> without checking anything: callers on those platforms get
    /// no IPC identity hardening from this method and must not treat a <see langword="true"/>
    /// result there as a verified identity.
    /// </remarks>
    /// <param name="pipe">Server-side named pipe stream.</param>
    /// <param name="expectedProcessId">Expected client process identifier.</param>
    /// <returns><see langword="true"/> when the connected process matches (Windows), or unconditionally on other platforms; otherwise <see langword="false"/>.</returns>
    public static bool IsExpectedNamedPipeClient(NamedPipeServerStream pipe, int expectedProcessId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        return TryGetNamedPipeClientProcessId(pipe, out uint clientPid) && clientPid == (uint)expectedProcessId;
    }

    /// <summary>
    /// Verifies an Authenticode signature and returns a structured result separating signature
    /// presence, chain trust, and revocation status.
    /// </summary>
    /// <remarks>
    /// Authenticode is a Windows-specific signing scheme with no equivalent on other platforms.
    /// Callers that need a cross-platform trust decision should branch on
    /// <see cref="OperatingSystem.IsWindows"/> themselves and decide what "trusted" means on other
    /// platforms — this method never silently reports success on non-Windows, unlike APIs that
    /// return <see langword="true"/> unconditionally when the platform cannot perform the check.
    /// </remarks>
    /// <param name="filePath">Path to the file to verify.</param>
    /// <param name="revocationPolicy">
    /// How to check certificate revocation. Defaults to <see cref="AuthenticodeRevocationPolicy.Online"/>
    /// (live CRL/OCSP query); use <see cref="AuthenticodeRevocationPolicy.CacheOnly"/> when network
    /// access is undesirable and <see cref="AuthenticodeRevocationPolicy.None"/> only when speed is
    /// critical and revocation is acceptable to skip.
    /// </param>
    /// <returns>A structured result describing signature, chain, and revocation status.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when called on a non-Windows platform.</exception>
    public static AuthenticodeVerificationResult VerifyAuthenticodeSignature(
        string filePath,
        AuthenticodeRevocationPolicy revocationPolicy = AuthenticodeRevocationPolicy.Online)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                $"{nameof(VerifyAuthenticodeSignature)} is Windows-only (Authenticode has no cross-platform " +
                $"equivalent). Callers must decide what \"trusted\" means on this platform instead of assuming " +
                $"success.");
        }

        return VerifyAuthenticode(filePath, revocationPolicy);
    }

    /// <summary>
    /// Returns whether a file carries a valid Authenticode signature.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method performs a live online revocation check (equivalent to
    /// <see cref="AuthenticodeRevocationPolicy.Online"/>). Use
    /// <see cref="VerifyAuthenticodeSignature"/> when you need to distinguish between signature
    /// validity, chain trust, and revocation status.
    /// </para>
    /// <para>
    /// Authenticode is a Windows-specific signing scheme with no equivalent on other platforms.
    /// Unlike <see cref="IsExpectedNamedPipeClient"/>, this method does not silently report
    /// success on non-Windows platforms — a caller using this as a trust gate ("should I run this
    /// binary?") must not receive a false sense of having verified anything. Callers that need a
    /// cross-platform trust decision should branch on <see cref="OperatingSystem.IsWindows"/>
    /// themselves and decide what "trusted" means for other platforms.
    /// </para>
    /// </remarks>
    /// <param name="filePath">Path to the file to verify.</param>
    /// <returns><see langword="true"/> when valid and trusted; otherwise <see langword="false"/>.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when called on a non-Windows platform.</exception>
    public static bool HasValidAuthenticodeSignature(string filePath) =>
        VerifyAuthenticodeSignature(filePath, AuthenticodeRevocationPolicy.Online).IsValid;

    /// <summary>
    /// Gets the client process identifier attached to a named pipe.
    /// </summary>
    /// <param name="pipe">Named pipe server stream.</param>
    /// <param name="processId">Resolved process identifier.</param>
    /// <returns><see langword="true"/> when resolved; otherwise <see langword="false"/>.</returns>
    [SupportedOSPlatform("windows")]
    private static bool TryGetNamedPipeClientProcessId(NamedPipeServerStream pipe, out uint processId)
    {
        IntPtr pipeHandle = pipe.SafePipeHandle.DangerousGetHandle();
        return WindowsNativeMethods.GetNamedPipeClientProcessId(pipeHandle, out processId);
    }

    /// <summary>
    /// Calls <c>WinVerifyTrust</c> and maps the result to an <see cref="AuthenticodeVerificationResult"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static AuthenticodeVerificationResult VerifyAuthenticode(
        string filePath, AuthenticodeRevocationPolicy policy)
    {
        IntPtr fileInfoPtr = IntPtr.Zero;
        try
        {
            var fileInfo = new WindowsNativeMethods.WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WindowsNativeMethods.WINTRUST_FILE_INFO>(),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero,
            };

            fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WindowsNativeMethods.WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, fDeleteOld: false);

            uint fdwRevocationChecks = policy == AuthenticodeRevocationPolicy.None
                ? WindowsNativeMethods.WTD_REVOKE_NONE
                : WindowsNativeMethods.WTD_REVOKE_WHOLECHAIN;

            uint dwProvFlags = policy == AuthenticodeRevocationPolicy.CacheOnly
                ? WindowsNativeMethods.WTD_CACHE_ONLY_URL_RETRIEVAL
                : 0u;

            var data = new WindowsNativeMethods.WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WindowsNativeMethods.WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WindowsNativeMethods.WTD_UI_NONE,
                fdwRevocationChecks = fdwRevocationChecks,
                dwUnionChoice = WindowsNativeMethods.WTD_CHOICE_FILE,
                pUnion = fileInfoPtr,
                dwStateAction = WindowsNativeMethods.WTD_STATEACTION_VERIFY,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = dwProvFlags,
                dwUIContext = 0,
            };

            var actionId = WindowsNativeMethods.WINTRUST_ACTION_GENERIC_VERIFY_V2;
            var hWnd = new IntPtr(-1);

            int result = WindowsNativeMethods.WinVerifyTrust(hWnd, ref actionId, ref data);

            data.dwStateAction = WindowsNativeMethods.WTD_STATEACTION_CLOSE;
            WindowsNativeMethods.WinVerifyTrust(hWnd, ref actionId, ref data);

            return MapResult(result, policy);
        }
        catch
        {
            // Marshal allocation or P/Invoke failure: treat as unsigned.
            return new AuthenticodeVerificationResult
            {
                IsSigned = false,
                IsChainTrusted = false,
                Revocation = RevocationStatus.NotChecked,
            };
        }
        finally
        {
            if (fileInfoPtr != IntPtr.Zero)
            {
                // WINTRUST_FILE_INFO.pcwszFilePath is marshaled as a separately-allocated LPWStr
                // block by StructureToPtr; DestroyStructure walks the struct's fields and frees
                // that block before we free the outer struct itself, avoiding a per-call leak of
                // the marshaled file path.
                Marshal.DestroyStructure<WindowsNativeMethods.WINTRUST_FILE_INFO>(fileInfoPtr);
                Marshal.FreeHGlobal(fileInfoPtr);
            }
        }
    }

    /// <summary>
    /// Maps a <c>WinVerifyTrust</c> HRESULT to an <see cref="AuthenticodeVerificationResult"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static AuthenticodeVerificationResult MapResult(int hresult, AuthenticodeRevocationPolicy policy)
    {
        if (hresult == 0)
        {
            return new AuthenticodeVerificationResult
            {
                IsSigned = true,
                IsChainTrusted = true,
                Revocation = policy == AuthenticodeRevocationPolicy.None
                    ? RevocationStatus.NotChecked
                    : RevocationStatus.Valid,
            };
        }

        if (hresult == WindowsNativeMethods.TRUST_E_NOSIGNATURE)
        {
            return new AuthenticodeVerificationResult
            {
                IsSigned = false,
                IsChainTrusted = false,
                Revocation = RevocationStatus.NotChecked,
            };
        }

        if (hresult == WindowsNativeMethods.CRYPT_E_REVOKED)
        {
            return new AuthenticodeVerificationResult
            {
                IsSigned = true,
                IsChainTrusted = true,
                Revocation = RevocationStatus.Revoked,
            };
        }

        if (hresult == WindowsNativeMethods.CRYPT_E_REVOCATION_OFFLINE)
        {
            return new AuthenticodeVerificationResult
            {
                IsSigned = true,
                IsChainTrusted = true,
                Revocation = RevocationStatus.Offline,
            };
        }

        // All other HRESULT values indicate the chain is not trusted (forged signature,
        // untrusted root, explicit distrust, etc.) but the file does carry a signature block.
        return new AuthenticodeVerificationResult
        {
            IsSigned = true,
            IsChainTrusted = false,
            Revocation = RevocationStatus.NotChecked,
        };
    }
}

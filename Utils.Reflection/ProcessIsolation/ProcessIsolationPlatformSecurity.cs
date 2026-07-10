using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Utils.Reflection.ProcessIsolation;

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
    /// Returns whether a file carries a valid Authenticode signature.
    /// </summary>
    /// <remarks>
    /// Authenticode is a Windows-specific signing scheme with no equivalent on other platforms.
    /// Unlike <see cref="IsExpectedNamedPipeClient"/>, this method does not silently report
    /// success on non-Windows platforms — a caller using this as a trust gate ("should I run this
    /// binary?") must not receive a false sense of having verified anything. Callers that need a
    /// cross-platform trust decision should branch on <see cref="OperatingSystem.IsWindows"/>
    /// themselves and decide what "trusted" means for other platforms.
    /// </remarks>
    /// <param name="filePath">Path to the file to verify.</param>
    /// <returns><see langword="true"/> when valid and trusted; otherwise <see langword="false"/>.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when called on a non-Windows platform.</exception>
    public static bool HasValidAuthenticodeSignature(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                $"{nameof(HasValidAuthenticodeSignature)} is Windows-only (Authenticode has no cross-platform " +
                $"equivalent). Callers must decide what \"trusted\" means on this platform instead of assuming " +
                $"success.");
        }

        return VerifyAuthenticode(filePath);
    }

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
    /// Performs Authenticode validation using WinVerifyTrust.
    /// </summary>
    /// <param name="filePath">Path to the file to validate.</param>
    /// <returns><see langword="true"/> if signature is valid and trusted.</returns>
    [SupportedOSPlatform("windows")]
    private static bool VerifyAuthenticode(string filePath)
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

            var data = new WindowsNativeMethods.WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WindowsNativeMethods.WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WindowsNativeMethods.WTD_UI_NONE,
                fdwRevocationChecks = WindowsNativeMethods.WTD_REVOKE_NONE,
                dwUnionChoice = WindowsNativeMethods.WTD_CHOICE_FILE,
                pUnion = fileInfoPtr,
                dwStateAction = WindowsNativeMethods.WTD_STATEACTION_VERIFY,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = 0,
                dwUIContext = 0,
            };

            var actionId = WindowsNativeMethods.WINTRUST_ACTION_GENERIC_VERIFY_V2;
            var hWnd = new IntPtr(-1);

            int result = WindowsNativeMethods.WinVerifyTrust(hWnd, ref actionId, ref data);

            data.dwStateAction = WindowsNativeMethods.WTD_STATEACTION_CLOSE;
            WindowsNativeMethods.WinVerifyTrust(hWnd, ref actionId, ref data);

            return result == 0;
        }
        catch
        {
            return false;
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
}

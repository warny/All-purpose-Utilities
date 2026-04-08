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
    /// <param name="pipe">Server-side named pipe stream.</param>
    /// <param name="expectedProcessId">Expected client process identifier.</param>
    /// <returns><see langword="true"/> when the connected process matches; otherwise <see langword="false"/>.</returns>
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
    /// <param name="filePath">Path to the file to verify.</param>
    /// <returns><see langword="true"/> when valid and trusted; otherwise <see langword="false"/>.</returns>
    public static bool HasValidAuthenticodeSignature(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
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
                Marshal.FreeHGlobal(fileInfoPtr);
            }
        }
    }
}

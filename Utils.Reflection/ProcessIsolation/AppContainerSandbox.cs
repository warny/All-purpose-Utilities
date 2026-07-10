using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Manages an AppContainer sandbox for the plugin worker process.
/// </summary>
/// <remarks>
/// <para>
/// An AppContainer is a Windows process isolation boundary that restricts:
/// <list type="bullet">
///   <item>Network access — sockets cannot be created unless a capability is explicitly declared.</item>
///   <item>File system writes — the container can only write to its own data folder
///     (<c>%LOCALAPPDATA%\Packages\{name}</c>) unless explicit ACEs are added.</item>
///   <item>Registry writes — restricted to the container's own hive.</item>
/// </list>
/// </para>
/// <para>
/// Additionally, a <b>Job Object</b> with <c>KillOnJobClose</c> is attached so the worker
/// is automatically terminated when the extension process exits, regardless of how it ends.
/// UI restrictions prevent the worker from interacting with the desktop (clipboard,
/// system parameters, display settings, etc.).
/// </para>
/// <para>
/// The AppContainer profile is stored in HKCU and requires no elevation to create.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class AppContainerSandbox : IProcessContainer
{
    private readonly IntPtr containerSid;
    private readonly IntPtr jobObjectHandle;
    private bool disposed;

    private AppContainerSandbox(IntPtr containerSid, IntPtr jobObjectHandle)
    {
        this.containerSid = containerSid;
        this.jobObjectHandle = jobObjectHandle;
    }

    /// <summary>
    /// Creates the AppContainer sandbox.
    /// Returns <see langword="null"/> on any failure so the caller can degrade gracefully.
    /// </summary>
    /// <param name="containerName">
    /// Stable name for the AppContainer profile.
    /// Changing this creates a new profile in HKCU.
    /// </param>
    /// <param name="displayName">Human-readable display name used by Windows profile metadata.</param>
    /// <param name="description">Human-readable profile description.</param>
    /// <param name="permissions">Requested process permissions.</param>
    public static AppContainerSandbox? TryCreate(
        string containerName,
        string displayName,
        string description,
        ProcessContainerPermissions permissions)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        if (!permissions.AllowDiskRead)
        {
            return null;
        }

        IntPtr sid;
        int hr = WindowsNativeMethods.CreateAppContainerProfile(
            containerName,
            displayName,
            description,
            IntPtr.Zero, 0,
            out sid);

        if (hr == WindowsNativeMethods.E_ALREADY_EXISTS)
        {
            hr = WindowsNativeMethods.DeriveAppContainerSidFromAppContainerName(containerName, out sid);
        }

        if (hr != 0 || sid == IntPtr.Zero)
        {
            return null;
        }

        // The Job Object with KillOnJobClose is what guarantees the worker dies if this process
        // crashes or is killed. If it cannot be created, degrade to "no container" (the caller
        // falls back to an unsandboxed process) rather than silently handing back a sandbox whose
        // most important safety net is missing.
        IntPtr job = CreateConfiguredJobObject();
        if (job == IntPtr.Zero)
        {
            WindowsNativeMethods.FreeSid(sid);
            return null;
        }

        return new AppContainerSandbox(sid, job);
    }

    /// <summary>
    /// Returns a <see cref="SecurityIdentifier"/> for the AppContainer, usable in .NET ACL APIs.
    /// </summary>
    public SecurityIdentifier GetContainerSid()
    {
        int len = WindowsNativeMethods.GetLengthSid(containerSid);
        byte[] bytes = new byte[len];
        Marshal.Copy(containerSid, bytes, 0, len);
        return new SecurityIdentifier(bytes, 0);
    }

    /// <summary>
    /// Returns the AppContainer SID for IPC ACL hardening.
    /// </summary>
    /// <param name="securityIdentifier">Resolved AppContainer SID.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    public bool TryGetSecurityIdentifier(out SecurityIdentifier? securityIdentifier)
    {
        securityIdentifier = GetContainerSid();
        return true;
    }

    /// <summary>
    /// Ensures <paramref name="directoryPath"/> exists and grants the AppContainer SID
    /// read+execute access so the worker can load DLLs from it.
    /// </summary>
    public void GrantDirectoryReadAccess(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            SecurityIdentifier sid = GetContainerSid();
            var security = new DirectorySecurity(directoryPath, AccessControlSections.Access);
            var rule = new FileSystemAccessRule(
                sid,
                FileSystemRights.ReadAndExecute | FileSystemRights.ListDirectory,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);
            security.AddAccessRule(rule);
            new DirectoryInfo(directoryPath).SetAccessControl(security);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            System.Security.SecurityException or
            ArgumentException)
        {
            // ACL modification is best-effort: the worker may fail to read the plugin DLLs, which
            // is preferable to blocking extension startup. Still trace the failure so it can be
            // diagnosed instead of silently manifesting as an unexplained DLL load failure later.
            Trace.TraceWarning(
                $"AppContainerSandbox.GrantDirectoryReadAccess failed for '{directoryPath}': {ex}");
        }
    }

    /// <summary>
    /// Starts <paramref name="exePath"/> inside the AppContainer and assigns it to the
    /// Job Object. Returns a <see cref="Process"/> wrapping the created process.
    /// </summary>
    /// <param name="executablePath">Absolute path to the executable to run.</param>
    /// <param name="arguments">Ordered arguments passed to the executable.</param>
    /// <exception cref="InvalidOperationException">Thrown when the process cannot be created.</exception>
    public Process StartProcess(string executablePath, IEnumerable<string> arguments)
    {
        string argumentString = BuildArgumentString(arguments);
        return StartProcessInternal(executablePath, argumentString);
    }

    /// <summary>
    /// Starts <paramref name="exePath"/> inside the AppContainer and assigns it to the
    /// Job Object. Returns a <see cref="Process"/> wrapping the created process.
    /// </summary>
    /// <param name="exePath">Absolute path to the executable to run.</param>
    /// <param name="arguments">Command-line arguments as a single escaped string.</param>
    /// <returns>The started process.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process cannot be created.</exception>
    private Process StartProcessInternal(string exePath, string arguments)
    {
        IntPtr attrList = IntPtr.Zero;
        IntPtr capPtr = IntPtr.Zero;
        IntPtr environmentPtr = IntPtr.Zero;

        try
        {
            attrList = AllocateAttributeList(attributeCount: 1);

            // CreateProcess inherits the caller's ENTIRE environment when lpEnvironment is
            // IntPtr.Zero — unlike LinuxBubblewrapContainer/MacOsSandboxExecContainer, which already
            // strip down to SandboxedProcessEnvironment's allowlist via ProcessStartInfo. Building an
            // explicit block here closes that gap for the Windows sandbox.
            environmentPtr = Marshal.StringToHGlobalUni(SandboxedProcessEnvironment.BuildWindowsEnvironmentBlock());

            // Marshal SECURITY_CAPABILITIES for the AppContainer.
            var caps = new WindowsNativeMethods.SECURITY_CAPABILITIES
            {
                AppContainerSid = containerSid,
                Capabilities = IntPtr.Zero,
                CapabilityCount = 0,
                Reserved = 0,
            };
            capPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WindowsNativeMethods.SECURITY_CAPABILITIES>());
            Marshal.StructureToPtr(caps, capPtr, false);

            bool updated = WindowsNativeMethods.UpdateProcThreadAttribute(
                attrList, 0,
                WindowsNativeMethods.PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES,
                capPtr,
                (IntPtr)Marshal.SizeOf<WindowsNativeMethods.SECURITY_CAPABILITIES>(),
                IntPtr.Zero, IntPtr.Zero);

            if (!updated)
            {
                throw new InvalidOperationException(
                    $"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");
            }

            var startupInfoEx = new WindowsNativeMethods.STARTUPINFOEX();
            startupInfoEx.StartupInfo.cb = Marshal.SizeOf<WindowsNativeMethods.STARTUPINFOEX>();
            startupInfoEx.lpAttributeList = attrList;

            // CreateProcess requires a mutable command-line buffer.
            var cmdLine = new StringBuilder($"\"{exePath}\" {arguments}");

            bool created = WindowsNativeMethods.CreateProcess(
                exePath, cmdLine,
                IntPtr.Zero, IntPtr.Zero,
                bInheritHandles: false,
                WindowsNativeMethods.EXTENDED_STARTUPINFO_PRESENT |
                WindowsNativeMethods.CREATE_NO_WINDOW |
                WindowsNativeMethods.CREATE_UNICODE_ENVIRONMENT,
                environmentPtr, null,
                ref startupInfoEx,
                out WindowsNativeMethods.PROCESS_INFORMATION procInfo);

            if (!created)
            {
                throw new InvalidOperationException(
                    $"CreateProcess failed: {Marshal.GetLastWin32Error()}");
            }

            int pid = procInfo.dwProcessId;

            // Attach to the Job Object before releasing the process handle so there is
            // no window where the process could escape the job.
            if (jobObjectHandle != IntPtr.Zero)
            {
                bool assigned = WindowsNativeMethods.AssignProcessToJobObject(jobObjectHandle, procInfo.hProcess);
                if (!assigned)
                {
                    int assignError = Marshal.GetLastWin32Error();
                    WindowsNativeMethods.CloseHandle(procInfo.hProcess);
                    WindowsNativeMethods.CloseHandle(procInfo.hThread);

                    // The process was created but could not be placed under the Job Object that
                    // guarantees KillOnJobClose. Running it anyway would silently drop that safety
                    // net, so terminate it instead of returning a process the caller believes is
                    // contained.
                    TerminateOrphanedProcess(pid);

                    throw new InvalidOperationException(
                        $"AssignProcessToJobObject failed: {assignError}. The sandboxed process was " +
                        $"terminated because it could not be attached to the Job Object that guarantees " +
                        $"it is killed if this process exits.");
                }
            }

            WindowsNativeMethods.CloseHandle(procInfo.hProcess);
            WindowsNativeMethods.CloseHandle(procInfo.hThread);

            return Process.GetProcessById(pid);
        }
        finally
        {
            if (attrList != IntPtr.Zero)
            {
                WindowsNativeMethods.DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }

            if (capPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(capPtr);
            }

            if (environmentPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(environmentPtr);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        _ = disposing; // Both handles are unmanaged; the cleanup is identical either way.

        if (disposed)
        {
            return;
        }

        disposed = true;

        if (containerSid != IntPtr.Zero)
        {
            WindowsNativeMethods.FreeSid(containerSid);
        }

        // Closing the Job Object handle triggers KillOnJobClose, terminating the worker.
        if (jobObjectHandle != IntPtr.Zero)
        {
            WindowsNativeMethods.CloseHandle(jobObjectHandle);
        }
    }

    /// <summary>
    /// Frees the unmanaged AppContainer SID and Job Object handle if <see cref="Dispose()"/> was
    /// never called (for example because an exception unwound past a missing <c>using</c>).
    /// </summary>
    ~AppContainerSandbox() => Dispose(false);

    // ─── Private helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Job Object with:
    /// <list type="bullet">
    ///   <item><c>KillOnJobClose</c> — worker is killed when this process exits.</item>
    ///   <item>Full UI restrictions — no clipboard, no display settings, no desktop access.</item>
    /// </list>
    /// </summary>
    private static IntPtr CreateConfiguredJobObject()
    {
        IntPtr job = WindowsNativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var basicLimits = new WindowsNativeMethods.JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            LimitFlags = WindowsNativeMethods.JOB_OBJECT_LIMIT.KillOnJobClose,
        };

        WindowsNativeMethods.SetInformationJobObject(
            job,
            WindowsNativeMethods.JOBOBJECTINFOCLASS.JobObjectBasicLimitInformation,
            ref basicLimits,
            Marshal.SizeOf<WindowsNativeMethods.JOBOBJECT_BASIC_LIMIT_INFORMATION>());

        var uiRestrictions = new WindowsNativeMethods.JOBOBJECT_BASIC_UI_RESTRICTIONS
        {
            UIRestrictionsClass =
                WindowsNativeMethods.JOB_OBJECT_UILIMIT.Handles |
                WindowsNativeMethods.JOB_OBJECT_UILIMIT.ReadClipboard |
                WindowsNativeMethods.JOB_OBJECT_UILIMIT.WriteClipboard |
                WindowsNativeMethods.JOB_OBJECT_UILIMIT.SystemParameters |
                WindowsNativeMethods.JOB_OBJECT_UILIMIT.DisplaySettings |
                WindowsNativeMethods.JOB_OBJECT_UILIMIT.GlobalAtoms |
                WindowsNativeMethods.JOB_OBJECT_UILIMIT.Desktop |
                WindowsNativeMethods.JOB_OBJECT_UILIMIT.ExitWindows,
        };

        WindowsNativeMethods.SetInformationJobObject(
            job,
            WindowsNativeMethods.JOBOBJECTINFOCLASS.JobObjectBasicUIRestrictions,
            ref uiRestrictions,
            Marshal.SizeOf<WindowsNativeMethods.JOBOBJECT_BASIC_UI_RESTRICTIONS>());

        return job;
    }

    /// <summary>
    /// Best-effort termination of a process that was created but could not be placed under the
    /// Job Object, so it must not be left running outside of the sandbox's lifetime guarantees.
    /// </summary>
    /// <param name="processId">Identifier of the process to terminate.</param>
    private static void TerminateOrphanedProcess(int processId)
    {
        try
        {
            using Process orphan = Process.GetProcessById(processId);
            if (!orphan.HasExited)
            {
                orphan.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process may have already exited on its own, or termination raced with exit;
            // either way there is nothing actionable left to do here.
        }
    }

    /// <summary>
    /// Allocates and initializes a <c>PROC_THREAD_ATTRIBUTE_LIST</c> for the given number of attributes.
    /// The caller must free this with <see cref="WindowsNativeMethods.DeleteProcThreadAttributeList"/>
    /// followed by <see cref="Marshal.FreeHGlobal"/>.
    /// </summary>
    private static IntPtr AllocateAttributeList(int attributeCount)
    {
        // First call with null buffer to query the required size.
        IntPtr size = IntPtr.Zero;
        WindowsNativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, attributeCount, 0, ref size);

        IntPtr attrList = Marshal.AllocHGlobal(size);

        if (!WindowsNativeMethods.InitializeProcThreadAttributeList(attrList, attributeCount, 0, ref size))
        {
            Marshal.FreeHGlobal(attrList);
            throw new InvalidOperationException(
                $"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");
        }

        return attrList;
    }

    /// <summary>
    /// Builds a process-safe command-line from the provided argument sequence.
    /// </summary>
    /// <param name="arguments">Ordered arguments to escape and join.</param>
    /// <returns>An escaped command-line argument string.</returns>
    internal static string BuildArgumentString(IEnumerable<string> arguments)
    {
        var escaped = new List<string>();
        foreach (string argument in arguments)
        {
            escaped.Add(QuoteArgument(argument));
        }

        return string.Join(' ', escaped);
    }

    /// <summary>
    /// Escapes one command-line argument for Windows <c>CreateProcess</c>.
    /// </summary>
    /// <param name="argument">Argument to escape.</param>
    /// <returns>The escaped argument.</returns>
    internal static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        if (!argument.Contains(' ') && !argument.Contains('\t') && !argument.Contains('"'))
        {
            return argument;
        }

        var sb = new StringBuilder(argument.Length + 2);
        sb.Append('"');

        int backslashCount = 0;
        foreach (char ch in argument)
        {
            if (ch == '\\')
            {
                backslashCount++;
                continue;
            }

            if (ch == '"')
            {
                sb.Append('\\', backslashCount * 2 + 1);
                sb.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                sb.Append('\\', backslashCount);
                backslashCount = 0;
            }

            sb.Append(ch);
        }

        if (backslashCount > 0)
        {
            sb.Append('\\', backslashCount * 2);
        }

        sb.Append('"');
        return sb.ToString();
    }
}

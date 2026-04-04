using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Utils.Parser.VisualStudio.Sandbox;

/// <summary>
/// Win32 P/Invoke declarations for AppContainer sandboxing and Job Object lifecycle management.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsNativeMethods
{
    // ─── userenv.dll ────────────────────────────────────────────────────────────

    /// <summary>Creates an AppContainer profile (idempotent; returns <see cref="E_ALREADY_EXISTS"/> when the profile exists).</summary>
    [DllImport("userenv.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    internal static extern int CreateAppContainerProfile(
        string pszAppContainerName,
        string pszDisplayName,
        string pszDescription,
        IntPtr pCapabilities,
        uint dwCapabilityCount,
        out IntPtr ppSidAppContainerSid);

    /// <summary>Derives the AppContainer SID from a container name without creating a new profile.</summary>
    [DllImport("userenv.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    internal static extern int DeriveAppContainerSidFromAppContainerName(
        string pszAppContainerName,
        out IntPtr ppsidAppContainerSid);

    // ─── advapi32.dll ────────────────────────────────────────────────────────────

    [DllImport("advapi32.dll")]
    internal static extern void FreeSid(IntPtr pSid);

    [DllImport("advapi32.dll")]
    internal static extern int GetLengthSid(IntPtr pSid);

    // ─── kernel32.dll ────────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList,
        int dwAttributeCount,
        int dwFlags,
        ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr Attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);

    [DllImport("kernel32.dll")]
    internal static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetInformationJobObject(
        IntPtr hJob,
        JOBOBJECTINFOCLASS JobObjectInformationClass,
        ref JOBOBJECT_BASIC_LIMIT_INFORMATION lpJobObjectInformation,
        int cbJobObjectInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetInformationJobObject(
        IntPtr hJob,
        JOBOBJECTINFOCLASS JobObjectInformationClass,
        ref JOBOBJECT_BASIC_UI_RESTRICTIONS lpJobObjectInformation,
        int cbJobObjectInformationLength);

    // ─── Constants ───────────────────────────────────────────────────────────────

    /// <summary>Flag for CreateProcess indicating that lpStartupInfo is a STARTUPINFOEX.</summary>
    internal const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

    internal const uint CREATE_NO_WINDOW = 0x08000000;

    /// <summary>
    /// PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES = ProcThreadAttributeValue(9, FALSE, TRUE, FALSE).
    /// Enables AppContainer sandboxing for the created process.
    /// </summary>
    internal static readonly IntPtr PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES = (IntPtr)0x00020009;

    /// <summary>HRESULT returned when the AppContainer profile already exists.</summary>
    internal const int E_ALREADY_EXISTS = unchecked((int)0x800700B7);

    // ─── Structures ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps to Win32 STARTUPINFOW. Fields that we don't use are kept as IntPtr (null) so the
    /// marshaler does not attempt string conversion for unused pointer fields.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    /// <summary>Maps to Win32 STARTUPINFOEXW.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    /// <summary>Security capabilities for an AppContainer process.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SECURITY_CAPABILITIES
    {
        public IntPtr AppContainerSid;
        public IntPtr Capabilities;     // PSID_AND_ATTRIBUTES — IntPtr.Zero for "no extra capabilities"
        public uint CapabilityCount;    // 0 = no extra capabilities declared
        public uint Reserved;
    }

    internal enum JOBOBJECTINFOCLASS
    {
        JobObjectBasicLimitInformation = 2,
        JobObjectBasicUIRestrictions = 4,
    }

    [Flags]
    internal enum JOB_OBJECT_LIMIT : uint
    {
        KillOnJobClose = 0x00002000,
    }

    [Flags]
    internal enum JOB_OBJECT_UILIMIT : uint
    {
        None = 0,
        Handles = 0x0001,
        ReadClipboard = 0x0002,
        WriteClipboard = 0x0004,
        SystemParameters = 0x0008,
        DisplaySettings = 0x0010,
        GlobalAtoms = 0x0020,
        Desktop = 0x0040,
        ExitWindows = 0x0080,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public JOB_OBJECT_LIMIT LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JOBOBJECT_BASIC_UI_RESTRICTIONS
    {
        public JOB_OBJECT_UILIMIT UIRestrictionsClass;
    }
}

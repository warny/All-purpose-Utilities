using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// Linux process container backed by <c>bwrap</c> (bubblewrap) when available.
/// </summary>
/// <remarks>
/// <b>File-read posture differs from Windows.</b> <see cref="StartProcess"/> binds the entire host
/// filesystem read-only (<c>--ro-bind / /</c>) rather than only the directories granted through
/// <see cref="GrantDirectoryReadAccess"/> (which is a no-op here for that reason). This does not
/// grant the sandboxed process any access it wouldn't already have as the same OS user — bubblewrap
/// does not unshare the user namespace's UID here, so normal Unix file permissions still apply — but
/// it is a materially broader read surface than <see cref="AppContainerSandbox"/>'s deny-by-default,
/// per-directory ACL model on Windows. Code that targets <see cref="IProcessContainer"/> generically
/// must not assume read access is scoped the same way on every platform.
/// </remarks>
internal sealed class LinuxBubblewrapContainer : IProcessContainer
{
    private const string BubblewrapExecutableName = "bwrap";
    private readonly ProcessContainerPermissions permissions;

    private LinuxBubblewrapContainer(ProcessContainerPermissions permissions)
    {
        this.permissions = permissions;
    }

    /// <summary>
    /// Creates a Linux process container when <c>bwrap</c> is available in the current environment.
    /// </summary>
    /// <returns>An initialized container, or <see langword="null"/> when unavailable.</returns>
    public static LinuxBubblewrapContainer? TryCreate(ProcessContainerPermissions permissions)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        if (!permissions.AllowDiskRead)
        {
            return null;
        }

        return CommandAvailability.Exists(BubblewrapExecutableName)
            ? new LinuxBubblewrapContainer(permissions)
            : null;
    }

    /// <summary>
    /// Starts a child process inside an isolated bubblewrap namespace.
    /// </summary>
    /// <param name="executablePath">Absolute path to the executable to run.</param>
    /// <param name="arguments">Ordered command-line arguments for the executable.</param>
    /// <returns>The started process.</returns>
    public Process StartProcess(string executablePath, IEnumerable<string> arguments)
    {
        List<string> wrappedArguments = BuildArguments(executablePath, arguments, permissions);

        var psi = new ProcessStartInfo(BubblewrapExecutableName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        SandboxedProcessEnvironment.ApplyMinimalEnvironment(psi);

        foreach (string argument in wrappedArguments)
        {
            psi.ArgumentList.Add(argument);
        }

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start sandboxed process with bubblewrap.");
    }

    /// <summary>
    /// Builds the <c>bwrap</c> argument list for the given permission set, without touching the OS.
    /// Extracted from <see cref="StartProcess"/> so the mapping from
    /// <see cref="ProcessContainerPermissions"/> to bubblewrap flags can be unit-tested on any
    /// platform, without actually being able to run <c>bwrap</c>.
    /// </summary>
    /// <param name="executablePath">Absolute path to the executable to run inside the sandbox.</param>
    /// <param name="arguments">Ordered command-line arguments for the executable.</param>
    /// <param name="permissions">Requested process permissions.</param>
    /// <returns>The complete, ordered <c>bwrap</c> argument list.</returns>
    internal static List<string> BuildArguments(
        string executablePath, IEnumerable<string> arguments, ProcessContainerPermissions permissions)
    {
        var wrappedArguments = new List<string>
        {
            "--die-with-parent",
            "--new-session",
            "--unshare-all",
            "--ro-bind",
            "/",
            "/",
            "--proc",
            "/proc",
        };

        if (permissions.AllowNetwork)
        {
            wrappedArguments.Add("--share-net");
        }

        if (permissions.AllowDeviceAccess)
        {
            wrappedArguments.Add("--dev-bind");
            wrappedArguments.Add("/dev");
            wrappedArguments.Add("/dev");
        }
        else
        {
            wrappedArguments.Add("--dev");
            wrappedArguments.Add("/dev");
        }

        if (permissions.AllowDiskWrite)
        {
            wrappedArguments.Add("--bind");
            wrappedArguments.Add("/tmp");
            wrappedArguments.Add("/tmp");
        }
        else
        {
            wrappedArguments.Add("--tmpfs");
            wrappedArguments.Add("/tmp");
        }

        wrappedArguments.Add("--");
        wrappedArguments.Add(executablePath);
        wrappedArguments.AddRange(arguments);

        return wrappedArguments;
    }

    /// <summary>
    /// No-op: <see cref="StartProcess"/> already binds the entire host filesystem read-only for
    /// every sandboxed process on Linux (see the class remarks), so there is no narrower grant to
    /// apply here.
    /// </summary>
    /// <param name="directoryPath">Unused.</param>
    public void GrantDirectoryReadAccess(string directoryPath)
    {
        _ = directoryPath;
    }

    /// <summary>
    /// Disposes the container instance.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Linux bubblewrap container does not expose a SID-style identifier for Windows ACLs.
    /// </summary>
    /// <param name="securityIdentifier">Always <see langword="null"/>.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    public bool TryGetSecurityIdentifier(out SecurityIdentifier? securityIdentifier)
    {
        securityIdentifier = null;
        return false;
    }
}

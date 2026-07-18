using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

using Utils.Reflection;

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
    private readonly string resolvedExecutablePath;

    private LinuxBubblewrapContainer(ProcessContainerPermissions permissions, string resolvedExecutablePath)
    {
        this.permissions = permissions;
        this.resolvedExecutablePath = resolvedExecutablePath;
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

        return CommandAvailability.TryResolve(BubblewrapExecutableName, out string? resolvedPath)
            ? new LinuxBubblewrapContainer(permissions, resolvedPath!)
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
        string[] argsArray = arguments as string[] ?? [.. arguments];
        string? ipcSocketPath = TryGetIpcSocketPath(argsArray);
        List<string> wrappedArguments = BuildArguments(executablePath, argsArray, permissions, ipcSocketPath);

        // Use the canonical absolute path resolved at TryCreate time to avoid TOCTOU races.
        var psi = new ProcessStartInfo(resolvedExecutablePath)
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
    /// <param name="ipcSocketPath">
    /// The host-side Unix-domain socket path the worker needs to reach. When provided alongside
    /// <see cref="ProcessContainerPermissions.AllowDiskWrite"/>, only this path is bind-mounted
    /// into the container's <c>/tmp</c>; the rest of <c>/tmp</c> is a fresh <c>tmpfs</c>. When
    /// <see langword="null"/>, <c>/tmp</c> is always an isolated <c>tmpfs</c> with no host content.
    /// </param>
    /// <returns>The complete, ordered <c>bwrap</c> argument list.</returns>
    internal static List<string> BuildArguments(
        string executablePath, IEnumerable<string> arguments, ProcessContainerPermissions permissions,
        string? ipcSocketPath = null)
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

        // Always start with a fresh tmpfs so host /tmp content is not visible. When the worker
        // needs IPC access, bind-mount the exact socket file — nothing more.
        wrappedArguments.Add("--tmpfs");
        wrappedArguments.Add("/tmp");

        if (permissions.AllowDiskWrite && ipcSocketPath != null)
        {
            wrappedArguments.Add("--bind");
            wrappedArguments.Add(ipcSocketPath);
            wrappedArguments.Add(ipcSocketPath);
        }

        wrappedArguments.Add("--");
        wrappedArguments.Add(executablePath);
        wrappedArguments.AddRange(arguments);

        return wrappedArguments;
    }

    /// <summary>
    /// Scans <paramref name="arguments"/> for the emit-worker pipe-name marker and derives the
    /// Unix-domain socket path that .NET's <see cref="System.IO.Pipes.NamedPipeServerStream"/>
    /// creates on Linux (<c>/tmp/CoreFxPipe_&lt;name&gt;</c>).
    /// </summary>
    private static string? TryGetIpcSocketPath(string[] arguments)
    {
        for (int i = 0; i + 1 < arguments.Length; i++)
        {
            if (arguments[i] == LibraryMapper.WorkerArgumentMarker)
            {
                return Path.Combine(Path.GetTempPath(), $"CoreFxPipe_{arguments[i + 1]}");
            }
        }

        return null;
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// macOS process container backed by <c>sandbox-exec</c> when available.
/// </summary>
internal sealed class MacOsSandboxExecContainer : IProcessContainer
{
    private const string SandboxExecutableName = "sandbox-exec";
    private readonly ProcessContainerPermissions permissions;

    private MacOsSandboxExecContainer(ProcessContainerPermissions permissions)
    {
        this.permissions = permissions;
    }

    /// <summary>
    /// Creates a macOS process container when <c>sandbox-exec</c> is available.
    /// </summary>
    /// <returns>An initialized container, or <see langword="null"/> when unavailable.</returns>
    public static MacOsSandboxExecContainer? TryCreate(ProcessContainerPermissions permissions)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        if (!permissions.AllowDiskRead)
        {
            return null;
        }

        return CommandAvailability.Exists(SandboxExecutableName)
            ? new MacOsSandboxExecContainer(permissions)
            : null;
    }

    /// <summary>
    /// Starts a child process with a restrictive <c>sandbox-exec</c> profile.
    /// </summary>
    /// <param name="executablePath">Absolute path to the executable to run.</param>
    /// <param name="arguments">Ordered command-line arguments for the executable.</param>
    /// <returns>The started process.</returns>
    public Process StartProcess(string executablePath, IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo(SandboxExecutableName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(BuildProfile());
        psi.ArgumentList.Add(executablePath);

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start sandboxed process with sandbox-exec.");
    }

    /// <summary>
    /// Grants read access to a directory.
    /// </summary>
    /// <param name="directoryPath">Directory that should remain readable.</param>
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
    /// macOS sandbox-exec container does not expose a SID-style identifier for Windows ACLs.
    /// </summary>
    /// <param name="securityIdentifier">Always <see langword="null"/>.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    public bool TryGetSecurityIdentifier(out SecurityIdentifier? securityIdentifier)
    {
        securityIdentifier = null;
        return false;
    }

    /// <summary>
    /// Builds a sandbox-exec profile from the selected permission set.
    /// </summary>
    /// <returns>A complete sandbox-exec profile string.</returns>
    private string BuildProfile()
    {
        var clauses = new List<string>
        {
            "(version 1)",
            "(deny default)",
            "(allow process*)",
            "(allow file-read*)",
            "(allow sysctl-read)",
        };

        if (permissions.AllowDiskWrite)
        {
            clauses.Add("(allow file-write*)");
        }

        if (permissions.AllowNetwork)
        {
            clauses.Add("(allow network*)");
        }

        if (permissions.AllowDeviceAccess)
        {
            clauses.Add("(allow iokit-open)");
        }

        if (permissions.AllowProcessDebugging)
        {
            clauses.Add("(allow process-info*)");
        }

        return string.Join(' ', clauses);
    }
}

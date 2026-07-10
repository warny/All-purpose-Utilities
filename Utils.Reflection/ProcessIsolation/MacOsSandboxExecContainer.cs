using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// macOS process container backed by <c>sandbox-exec</c> when available.
/// </summary>
/// <remarks>
/// <para>
/// <b>File-read and process operations are always allowed.</b> <see cref="BuildProfile"/> always
/// includes <c>(allow file-read*)</c> and <c>(allow process*)</c> regardless of the requested
/// <see cref="ProcessContainerPermissions"/> — the sandboxed process can read any file the OS user
/// can read (there is no per-directory scoping like <see cref="AppContainerSandbox"/> on Windows;
/// <see cref="GrantDirectoryReadAccess"/> is a no-op here for that reason), and the always-present
/// <c>process*</c> wildcard already covers most <c>process-info*</c> operations that
/// <see cref="ProcessContainerPermissions.AllowProcessDebugging"/> is meant to gate, so that flag
/// has limited additional effect on this platform.
/// </para>
/// <para>
/// <c>sandbox-exec</c> has been marked deprecated by Apple since macOS 10.12, with no announced
/// removal date as of this writing. If Apple removes it, this container will need to move to the
/// modern App Sandbox entitlement model instead.
/// </para>
/// </remarks>
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
        SandboxedProcessEnvironment.ApplyMinimalEnvironment(psi);

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(BuildProfile(permissions));
        psi.ArgumentList.Add(executablePath);

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start sandboxed process with sandbox-exec.");
    }

    /// <summary>
    /// No-op: <see cref="BuildProfile"/> always allows <c>file-read*</c> for every sandboxed
    /// process on macOS (see the class remarks), so there is no narrower grant to apply here.
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
    /// Builds a sandbox-exec profile from the selected permission set, without touching the OS.
    /// A <see langword="static"/> method (rather than an instance member reading the container's own
    /// <see cref="permissions"/> field) so the mapping from <see cref="ProcessContainerPermissions"/>
    /// to sandbox-exec clauses can be unit-tested on any platform, without actually being able to run
    /// <c>sandbox-exec</c>.
    /// </summary>
    /// <param name="permissions">Requested process permissions.</param>
    /// <returns>A complete sandbox-exec profile string.</returns>
    internal static string BuildProfile(ProcessContainerPermissions permissions)
    {
        var clauses = new List<string>
        {
            "(version 1)",
            "(deny default)",
            // Always allowed, regardless of requested permissions — see the class remarks for why
            // this makes file reads and most process introspection unconditionally available.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Utils.Reflection.ProcessIsolation;

/// <summary>
/// macOS process container backed by <c>sandbox-exec</c> when available.
/// </summary>
public sealed class MacOsSandboxExecContainer : IProcessContainer
{
    private const string SandboxExecutableName = "sandbox-exec";

    private const string Profile = "(version 1) (deny default) (allow process*) (allow file-read*) (allow sysctl-read)";

    private MacOsSandboxExecContainer()
    {
    }

    /// <summary>
    /// Creates a macOS process container when <c>sandbox-exec</c> is available.
    /// </summary>
    /// <returns>An initialized container, or <see langword="null"/> when unavailable.</returns>
    public static MacOsSandboxExecContainer? TryCreate()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        return CommandAvailability.Exists(SandboxExecutableName)
            ? new MacOsSandboxExecContainer()
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
        psi.ArgumentList.Add(Profile);
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
}
